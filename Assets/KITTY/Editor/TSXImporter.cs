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
			}

			// Single-image tilesets all use the same texture.
			var tiles = new (GameObject prefab, Texture2D texture, TSX.Tile.Object[] objects)[tilecount];
			for (var i = 0; i < tiles.Length; ++i) {
				tiles[i].texture = texture;
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
			for (var i = 0; i < tileset.tiles.Length; ++i) {
				var tileTexture = tiles[i].texture;
				tileset.tiles[i].prefab = tiles[i].prefab;
				tileset.tiles[i].texture = tileTexture;
				tileset.tiles[i].pivot = pivot;
				tileset.tiles[i].rect = new Rect(
					texture ? (tsx.tilewidth + tsx.spacing) * (i % columns) + tsx.margin: 0,
					texture ? (tsx.tileheight + tsx.spacing) * (rows - i / columns - 1) + texture.height - rows * tsx.tileheight - rows * tsx.spacing + tsx.margin : 0,
					texture ? tsx.tilewidth : tileTexture?.width ?? 0,
					texture ? tsx.tileheight : tileTexture?.height ?? 0
				);
				tileset.tiles[i].objects = ParseObjects(tiles[i].objects, tileset.tiles[i].rect.height);
			}

			context.AddObjectToAsset($"tileset_{tileset.name}", tileset);
			return tileset;
		}

		///<summary>Construct a tileset from Tiled by instantiating one tile per texture sprite.</summary>
		public override void OnImportAsset(AssetImportContext context) {
			Load(context, XDocument.Load(assetPath).Element("tileset"));
		}

		///<summary>Parse collision shapes; either position and size, or list of points.</summary>
		static Tileset.Tile.Object[] ParseObjects(TSX.Tile.Object[] tsxObjects, float height) {
			if (tsxObjects == null) {
				return null;
			}
			var objects = new Tileset.Tile.Object[tsxObjects.Length];
			for (var j = 0; j < objects.Length; ++j) {
				var obj = tsxObjects[j];
				// Rectangle shape
				if (obj.width > 0 && obj.height > 0) {
					objects[j] = new Tileset.Tile.Object {
						points = new [] {
							new Vector2(obj.x, height - obj.y),
							new Vector2(obj.x, height - obj.y - obj.height),
							new Vector2(obj.x + obj.width, height - obj.y - obj.height),
							new Vector2(obj.x + obj.width, height - obj.y)
						}
					};
				// Polygon shape
				} else if (obj.points != null) {
					var points = obj.points.Split(' ');
					objects[j] = new Tileset.Tile.Object {
						points = new Vector2[points.Length]
					};
					for (var k = 0; k < points.Length; ++k) {
						var point = points[k].Split(',');
						var x = float.Parse(point[0]);
						var y = float.Parse(point[1]);
						objects[j].points[k] =
							new Vector2(obj.x + x, height - obj.y - y);
					}
				}
			}
			return objects;
		}

		///<summary>Load texture asset based on filename, optionally making a color transparent</summary>
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