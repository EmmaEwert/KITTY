namespace KITTY {
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Xml.Linq;
	using UnityEditor;
	using UnityEditor.Experimental.AssetImporters;
	using UnityEngine;

	///<summary>
	///Tiled TSX tileset importer.
	///</summary>
	[ScriptedImporter(2, "tsx", 1)]
	internal class TSXImporter : ScriptedImporter {
		///<summary>
		///Loads and returns a Tileset asset, given a TSX structure.
		///</summary>
		public static Tileset Load(AssetImportContext context, TSX tsx) {
			// Image collection tilesets can have gaps in their IDs; use the highest ID instead.
			var tilecount = Mathf.Max(tsx.tilecount, tsx.tiles.LastOrDefault().id + 1);

			// Texture
			Texture2D texture = null;
			var columns = 0;
			var rows = 0;
			if (tsx.image.source == null) {
				// Image collection tilesets don't have images or column counts; use a single row.
				columns = tilecount;
				rows = 1;
			} else {
				// Single-image tilesets merge margin and spacing, and add a 1-pixel clamped border.
				texture = LoadTexture(
					context,
					tsx.image.source,
					tsx.image.trans,
					tsx.margin,
					tsx.spacing,
					tsx.tilewidth,
					tsx.tileheight
				);
				// Column count might not exist, so just compute it instead.
				columns = texture.width / (tsx.tilewidth + 2);
				rows = texture.height / (tsx.tileheight + 2);
				// Tile count depends only on column and row count.
				tilecount = columns * rows;
			}

			// Single-image tilesets all use the same texture.
			var tiles = new (
				GameObject prefab,
				Texture2D texture,
				TSX.Tile.Object[] objects,
				TSX.Tile.Frame[] frames,
				Property[] properties
			)[tilecount];
			for (var i = 0; i < tiles.Length; ++i) {
				tiles[i].texture = texture;
			}

			// Image collection tilesets each have their own texture and transparency color.
			foreach (var tile in tsx.tiles) {
				tiles[tile.id] = (
					PrefabHelper.Load(tile.type, context),
					texture ?? LoadTexture(context, tile.image.source, tile.image.trans),
					tile.objects,
					tile.frames,
					tile.properties
				);
			}

			// Tileset tiles are pseudo-representations of actual tiles; we can't instantiate a tile
			// properly until we know its sprite's pixels per unit, which depends on the tile*map*.
			var tileset = ScriptableObject.CreateInstance<Tileset>();
			tileset.name = tsx.name;
			tileset.tiles = new Tileset.Tile[tilecount];
			var pivot = Vector2.one * 0.5f - new Vector2(tsx.tileoffset.x, tsx.tileoffset.y)
				/ new Vector2(tsx.tilewidth, tsx.tileheight);
			// Since the tileset image is recreated with a 1-pixel clamped border around each tile,
			// we end up with a margin value of 1px and a spacing value of 2px
			for (var i = 0; i < tileset.tiles.Length; ++i) {
				var tileTexture = tiles[i].texture;
				tileset.tiles[i].prefab = tiles[i].prefab;
				tileset.tiles[i].texture = tileTexture;
				tileset.tiles[i].pivot = pivot;
				if (texture == null) {
					// Image collection tileset rect is just the full tile texture
					tileset.tiles[i].rect = new Rect(
						0,
						0,
						tileTexture?.width ?? 0,
						tileTexture?.height ?? 0
					);
				} else {
					// Single-image tileset rect needs to respect the added 1-pixel clamped border
					// around each tile, and read tiles from the top instead of the bottom
					tileset.tiles[i].rect = new Rect(
						(tsx.tilewidth  + 2) * ( i % columns    ) + 1,
						(tsx.tileheight + 2) * (-i / columns - 1) + 1 + texture.height,
						tsx.tilewidth,
						tsx.tileheight
					);
				}
				tileset.tiles[i].objects =
					ParseObjects(tiles[i].objects, tileset.tiles[i].rect.height);
				tileset.tiles[i].frames = new Tileset.Tile.Frame[tiles[i].frames?.Length ?? 0];
				for (var j = 0; j < tileset.tiles[i].frames.Length; ++j) {
					var frame = tiles[i].frames[j];
					Rect rect;
					if (texture == null) {
						// Image collection tileset animated tiles
						tileTexture = tiles[frame.tileid].texture;
						rect = new Rect(0, 0, tileTexture.width, tileTexture.height);
					} else {
						// Single-image tileset animated tiles
						rect = new Rect(
							(tsx.tilewidth  + 2) * ( frame.tileid % columns    ) + 1,
							(tsx.tileheight + 2) * (-frame.tileid / columns - 1) + 1
								+ texture.height,
							tsx.tilewidth,
							tsx.tileheight
						);
					}
					tileset.tiles[i].frames[j] = new Tileset.Tile.Frame {
						texture = tileTexture,
						rect = rect,
						duration = frame.duration,
					};
				}
				tileset.tiles[i].properties = tiles[i].properties ?? new Property[0];
			}

			context.AddObjectToAsset($"Tileset {tileset.name}", tileset);
			if (context.assetPath.EndsWith(".tsx")) {
				context.SetMainObject(tileset);
			}
			return tileset;
		}

		///<summary>
		///Construct a tileset from Tiled by instantiating one tile per texture sprite.
		///</summary>
		public override void OnImportAsset(AssetImportContext context) {
			Load(context, new TSX(XDocument.Load(assetPath).Element("tileset")));
		}

		///<summary>
		///Parse collision shapes; either position and size, or list of points.
		///</summary>
		static Tileset.Tile.Object[] ParseObjects(TSX.Tile.Object[] tsxObjects, float height) {
			if (tsxObjects == null) {
				return null;
			}
			var objects = new List<Tileset.Tile.Object>();
			for (var j = 0; j < tsxObjects.Length; ++j) {
				var @object = tsxObjects[j];
				// Rectangle shape.
				if (@object.width > 0 && @object.height > 0) {
					objects.Add(new Tileset.Tile.Object {
						points = new [] {
							new Vector2(@object.x, height - @object.y),
							new Vector2(@object.x, height - @object.y - @object.height),
							new Vector2(@object.x + @object.width, height - @object.y - @object.height),
							new Vector2(@object.x + @object.width, height - @object.y)
						}
					});
				// Polygon shape.
				} else if (@object.points != null) {
					var points = @object.points.Split(' ');
					objects.Add(new Tileset.Tile.Object { points = new Vector2[points.Length] });
					for (var k = 0; k < points.Length; ++k) {
						var point = points[k].Split(',');
						var x = float.Parse(point[0]);
						var y = float.Parse(point[1]);
						objects.Last().points[k] = new Vector2(@object.x + x, height - @object.y - y);
					}
				}
			}
			return objects.Count == 0 ? null : objects.ToArray();
		}

		///<summary>
		///Load texture asset based on filename, optionally making a color transparent
		///</summary>
		static Texture2D LoadTexture(
			AssetImportContext context,
			string filename,
			string trans,
			int margin = 0,
			int spacing = 0,
			int tilewidth = 0,
			int tileheight = 0
		) {
			var imageAssetPath = Path.GetFullPath(
				Path.GetDirectoryName(context.assetPath)
				+ Path.DirectorySeparatorChar
				+ (Path.IsPathRooted(filename)
					? Path.GetFileName(filename)
					: filename)
			);
			imageAssetPath = "Assets" + imageAssetPath.Substring(Application.dataPath.Length);
			var texture = AssetDatabase.LoadMainAssetAtPath(imageAssetPath) as Texture2D;
			if (!texture) {
				throw new FileNotFoundException($"could not load texture \"{imageAssetPath}\" ({filename}) of tileset \"{context.assetPath}\".");
			}

			// We only need a readable texture for single-image tilesets and/or a defined
			// transparent color
			if (!texture.isReadable && ((tilewidth > 0 && tileheight > 0) || trans != null)) {
				// Instead of failing and requiring the user to flag a texture as readable, we
				// can work around an unreadable texture by rendering it to a RenderTexture and
				// reading pixels from that instead of the source texture.
				var renderTexture = RenderTexture.GetTemporary(
					texture.width,
					texture.height,
					depthBuffer: 0,
					RenderTextureFormat.Default,
					RenderTextureReadWrite.Linear
				);
				Graphics.Blit(texture, renderTexture);
				var previousRenderTexture = RenderTexture.active;
				RenderTexture.active = renderTexture;
				texture = new Texture2D(texture.width, texture.height) {
					filterMode = texture.filterMode
				};
				texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
				texture.Apply();
				RenderTexture.active = previousRenderTexture;
				RenderTexture.ReleaseTemporary(renderTexture);
			}

			// If a transparent color is defined, we have to copy all the pixels and clear the
			// pixels matching that color.
			var transparent = Color.clear;
			if (trans != null) {
				ColorUtility.TryParseHtmlString(
					$"#{trans}".Substring(trans.Length - 6),
					out transparent
				);
			}

			// Clear margin and spacing directly in the single-image tileset texture, and add a
			// 1-pixel clamped border around each tile instead.
			if (tilewidth > 0 && tileheight > 0) {
				var columns = (texture.width - margin) / (tilewidth + spacing);
				var rows = (texture.height - margin) / (tileheight + spacing);
				var tilesetTexture = new Texture2D(
					columns * tilewidth + columns * 2,
					rows * tileheight + rows * 2
				) { filterMode = texture.filterMode };
				for (var y = 0; y < rows; ++y) {
					var sourceY = y * (tileheight + spacing) + texture.height
						- rows * (tileheight + spacing);
					var destinationY = y * tileheight + y * 2 + 1;
					for (var x = 0; x < columns; ++x) {
						var sourceX = x * (tilewidth + spacing) + margin;
						var destinationX = x * tilewidth + x * 2 + 1;
						var pixels = texture.GetPixels(sourceX, sourceY, tilewidth, tileheight);
						// Clear pixels matching the defined transparent color.
						if (transparent != Color.clear) {
							pixels = pixels
								.Select(c => c == transparent ? Color.clear : c)
								.ToArray();
						}
						// Add a 1-pixel clamped border around the tile.
						for (var dy = -1; dy <= 1; ++dy)
						for (var dx = -1; dx <= 1; ++dx) {
							if (Mathf.Abs(dx) == Mathf.Abs(dy)) { continue; }
							tilesetTexture.SetPixels(
								destinationX + dx,
								destinationY + dy,
								tilewidth,
								tileheight,
								pixels
							);
						}
						// Add the actual tile in the middle.
						tilesetTexture.SetPixels(
							destinationX,
							destinationY,
							tilewidth,
							tileheight,
							pixels
						);
					}
				}
				tilesetTexture.Apply();
				texture = tilesetTexture;
				texture.name = $"Texture {filename}";
				context.AddObjectToAsset($"Texture {filename}", texture);
			// Clear transparent color in image collection tilesets.
			} else if (transparent != Color.clear) {
				var transparentTexture = new Texture2D(
					texture.width, texture.height, TextureFormat.ARGB32, mipChain: false
				) { filterMode = texture.filterMode };
				// Read all pixel colors, and clear those that match the defined transparent color.
				var colors = texture.GetPixels();
				colors = colors.Select(c => c == transparent ? Color.clear : c).ToArray();
				transparentTexture.SetPixels(colors);
				transparentTexture.Apply();
				texture = transparentTexture;
				texture.name = $"Texture {filename}";
				context.AddObjectToAsset($"Texture {filename}", texture);
			}

			context.DependsOnSourceAsset(imageAssetPath);
			return texture;
		}
	}
}