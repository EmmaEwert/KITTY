namespace KITTY {
	using System.IO;
	using System.Linq;
	using System.Xml.Linq;
	using UnityEditor;
	using UnityEditor.Experimental.AssetImporters;
	using UnityEngine;

	///<summary>Tiled TSX tileset importer.</summary>
	[ScriptedImporter(2, "tsx", 1)]
	internal class TSXImporter : ScriptedImporter {
		public static Tileset Load(AssetImportContext context, XElement document) {
			var tsx = new TSX(document);

			// Image collection tilesets can have gaps in their IDs; use the highest ID instead.
			var tilecount = Mathf.Max(tsx.tilecount, tsx.tiles.LastOrDefault().id + 1);

			// Texture
			Texture2D texture = null;
			var columns = 0;
			var rows = 0;
			var tiles = new (GameObject prefab, Texture2D texture, TSX.Tile.Object[] objects)[tilecount];
			if (tsx.image.source == null) {
				// Image collection tilesets don't have images or column counts; use a single row.
				columns = tilecount;
				rows = 1;
			} else {
				texture = LoadTexture(tsx.image.source, tsx.image.trans, context);
				// Column count might not exist, so just compute it instead.
				columns = texture.width / tsx.tilewidth;
				rows = texture.height / tsx.tileheight;
				// Tile count depends only on column and row count.
				tilecount = columns * rows;
				// Single-image tilesets all use the same texture.
				for (var i = 0; i < tiles.Length; ++i) {
					tiles[i].texture = texture;
				}
			}

			// Image collection tilesets each have their own texture and transparency color.
			foreach (var tile in tsx.tiles) {
				tiles[tile.id] = (
					PrefabHelper.Load(tile.type, context),
					texture ?? LoadTexture(tile.image.source, tile.image.trans, context),
					tile.objects
				);
			}

			// Tiles
			var tileset = ScriptableObject.CreateInstance<Tileset>();
			tileset.name = (string)document.Attribute("name");
			tileset.tiles = new Tileset.Tile[tilecount];
			var pivot = Vector2.one * 0.5f - new Vector2(tsx.tileoffset.x, tsx.tileoffset.y) / new Vector2(tsx.tilewidth, tsx.tileheight);
			var spacing = new Vector4(tsx.tilewidth + tsx.spacing, tsx.tileheight + tsx.spacing, tsx.tilewidth, tsx.tileheight);
			var margin = new Vector4(tsx.margin, (texture?.height ?? 0) - rows * tsx.tileheight - rows * tsx.spacing, 0, 0);
			for (var i = 0; i < tileset.tiles.Length; ++i) {
				var tileTexture = tiles[i].texture;
				var position = new Vector4(
					texture ? i % columns : 0,
					texture ? rows - i / columns - 1 : 0,
					1, 1
				);
				var rect = texture
					? Vector4.Scale(position, spacing) + margin
					: new Vector4(0, 0, tileTexture?.width ?? 0, tileTexture?.height ?? 0);
				tileset.tiles[i].prefab = tiles[i].prefab;
				tileset.tiles[i].texture = tileTexture;
				tileset.tiles[i].pivot = pivot;
				tileset.tiles[i].rect = new Rect(rect.x, rect.y, rect.z, rect.w);
				// Collision shapes
				var objects = tiles[i].objects;
				if (objects != null) {
					tileset.tiles[i].objects = new Tileset.Tile.Object[objects.Length];
					for (var j = 0; j < tileset.tiles[i].objects.Length; ++j) {
						var obj = objects[j];
						// Rectangle shape
						if (obj.width > 0 && obj.height > 0) {
							tileset.tiles[i].objects[j] = new Tileset.Tile.Object {
								points = new [] {
									new Vector2(obj.x, rect.w - obj.y),
									new Vector2(obj.x, rect.w - obj.y - obj.height),
									new Vector2(obj.x + obj.width, rect.w - obj.y - obj.height),
									new Vector2(obj.x + obj.width, rect.w - obj.y)
								}
							};
						// Polygon shape
						} else if (obj.points != null) {
							var points = obj.points.Split(' ');
							tileset.tiles[i].objects[j] = new Tileset.Tile.Object {
								points = new Vector2[points.Length]
							};
							for (var k = 0; k < points.Length; ++k) {
								var point = points[k].Split(',');
								var x = float.Parse(point[0]);
								var y = float.Parse(point[1]);
								tileset.tiles[i].objects[j].points[k] =
									new Vector2(obj.x + x, rect.w - obj.y - y);
							}
						}
					}
				}
			}

			context.AddObjectToAsset($"tileset_{tileset.name}", tileset);
			return tileset;
		}

		///<summary>Construct a tileset from Tiled by instantiating one tile per texture sprite.</summary>
		public override void OnImportAsset(AssetImportContext context) {
			Load(context, XDocument.Load(assetPath).Element("tileset"));
		}

		static Texture2D LoadTexture(string filename, string trans, AssetImportContext context) {
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
			if (trans != null) {
				ColorUtility.TryParseHtmlString($"#{trans}".Substring(trans.Length - 6), out var transparent);
				var transparentTexture = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, mipChain: false) {
					filterMode = texture.filterMode
				};
				var colors = texture.GetPixels();
				colors = colors.Select(c => c == transparent ? Color.clear : c).ToArray();
				transparentTexture.SetPixels(colors);
				transparentTexture.name = $"texture_{filename}";
				context.AddObjectToAsset($"texture_{filename}", transparentTexture);
				texture = transparentTexture;
			}
			context.DependsOnSourceAsset(imageAssetPath);
			return texture;
		}
	}
}