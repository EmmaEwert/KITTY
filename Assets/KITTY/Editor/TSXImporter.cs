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
		Tileset tileset;

		public static Tileset Load(AssetImportContext context, XElement document) {
			// Tileset object
			var tileset = ScriptableObject.CreateInstance<Tileset>();

			// Attributes
			tileset.name    = (string )document.Attribute("name");
			var tilewidth   = (int    )document.Attribute("tilewidth");
			var tileheight  = (int    )document.Attribute("tileheight");
			var spacing     = (int?   )document.Attribute("spacing") ?? 0;
			var margin      = (int?   )document.Attribute("margin") ?? 0;
			var tilecount   = (int?   )document.Attribute("tilecount") ?? 0;
			var columns     = (int?   )document.Attribute("columns") ?? 0;

			context.AddObjectToAsset($"tileset_{tileset.name}", tileset);

			// Elements
			var tileoffsetElement  = document.Element("tileoffset");
			var tileoffset  = new Vector2Int(
				(int?)tileoffsetElement?.Attribute("x") ?? 0,
				(int?)tileoffsetElement?.Attribute("y") ?? 0
			);
			var imageSource = (string)document.Element("image")?.Attribute("source");
			var tileCollection = document
				.Elements("tile")
				.Select(t => LoadTile(t))
				.OrderBy(t => t.id)
				.ToArray();

			tilecount = Mathf.Max(tilecount, tileCollection.LastOrDefault().id + 1);

			// Texture
			Texture2D texture = null;
			if (imageSource == null) {
				columns = tilecount;
			} else {
				texture = LoadTexture(imageSource, context);
				columns = texture.width / tilewidth;
				tilecount = columns * texture.height / tileheight;
			}

			var tileArray = new (GameObject prefab, Texture2D texture, Shape[] shapes)[tilecount];
			for (var i = 0; i < tileArray.Length; ++i) {
				tileArray[i].texture = texture;
			}
			foreach (var tile in tileCollection) {
				tileArray[tile.id] = (
					prefab: PrefabHelper.Load(tile.type, context),
					texture: texture ?? LoadTexture(tile.image, context),
					shapes: tile.shapes
				);
			}

			// Tiles
			tileset.tiles = new Tileset.Tile[tilecount];
			var pivot = Vector2.one * 0.5f - tileoffset / new Vector2(tilewidth, tileheight);
			for (var i = 0; i < tileset.tiles.Length; ++i) {
				var rows = tilecount / columns;
				var tileSpacing = new Vector4(tilewidth + spacing, tileheight + spacing, tilewidth, tileheight);
				var tileMargin = new Vector4(margin, margin, 0, 0);
				var position = new Vector4(i % columns, rows - i / columns - 1, 1, 1);
				var rect = Vector4.Scale(position, tileSpacing) + tileMargin;
				var tileTexture = tileArray[i].texture;
				tileset.tiles[i].prefab = tileArray[i].prefab;
				tileset.tiles[i].texture = tileTexture;
				tileset.tiles[i].pivot = pivot;
				if (string.IsNullOrEmpty(imageSource)) { // Image collection
					tileset.tiles[i].rect = new Rect(0, 0, tileTexture?.width ?? 0, tileTexture?.height ?? 0);
				} else if (texture) {
					tileset.tiles[i].rect = new Rect(rect.x, rect.y + (tileTexture.height - rows * tileheight - rows * spacing - tileMargin.x), rect.z, rect.w);
				} else {
					continue;
				}
				var shapes = tileArray[i].shapes;
				if (shapes != null) {
					tileset.tiles[i].shapes = new Vector2[shapes.Length][];
					for (var j = 0; j < shapes.Length; ++j) {
						var obj = shapes[j];
						// Rectangle shape
						if (obj.width > 0 && obj.height > 0) {
							tileset.tiles[i].shapes[j] = new [] {
								new Vector2(obj.x, rect.w - obj.y),
								new Vector2(obj.x, rect.w - obj.y - obj.height),
								new Vector2(obj.x + obj.width, rect.w - obj.y - obj.height),
								new Vector2(obj.x + obj.width, rect.w - obj.y)
							};
						// Polygon shape
						} else if (obj.points != null) {
							var points = obj.points.Split(' ');
							tileset.tiles[i].shapes[j] = new Vector2[points.Length];
							for (var k = 0; k < points.Length; ++k) {
								var point = points[k].Split(',');
								var x = float.Parse(point[0]);
								var y = float.Parse(point[1]);
								tileset.tiles[i].shapes[j][k] =
									new Vector2(obj.x + x, rect.w - obj.y - y);
							}
						}
					}
				}
			}

			return tileset;
		}

		///<summary>Construct a tileset from Tiled by instantiating one tile per texture sprite.</summary>
		public override void OnImportAsset(AssetImportContext context) {
			tileset = Load(context, XDocument.Load(assetPath).Element("tileset"));
		}

		static Texture2D LoadTexture(string filename, AssetImportContext context) {
			var imageAssetPath = Path.GetFullPath(
				Path.GetDirectoryName(context.assetPath)
				+ Path.DirectorySeparatorChar
				+ (Path.IsPathRooted(filename)
					? Path.GetFileName(filename)
					: filename)
			);
			imageAssetPath = "Assets" + imageAssetPath.Substring(Application.dataPath.Length);
			var texture = AssetDatabase.LoadMainAssetAtPath(imageAssetPath) as Texture2D;
			if (texture) {
				context.DependsOnSourceAsset(imageAssetPath);
			} else {
				throw new FileNotFoundException($"could not load texture \"{imageAssetPath}\" ({filename}) of tileset \"{context.assetPath}\".");
			}
			return texture;
		}

		static (int id, string type, string image, Shape[] shapes) LoadTile(XElement element) {
			return (
				id: (int)element.Attribute("id"),
				type: (string)element.Attribute("type"),
				image: (string)element.Element("image")?.Attribute("source"),
				shapes: element
					.Element("objectgroup")?
					.Elements("object")
					.Select(o => new Shape {
						id = (uint)o.Attribute("id"),
						name = (string)o.Attribute("name"),
						type = (string)o.Attribute("type"),
						x = (float)o.Attribute("x"),
						y = (float)o.Attribute("y"),
						width = (float?)o.Attribute("width") ?? 0,
						height = (float?)o.Attribute("height") ?? 0,
						rotation = (float?)o.Attribute("rotation") ?? 0,
						points = (string)o.Element("polygon")?.Attribute("points"),
					}).ToArray()
			);
		}

		struct Shape {
			public uint id;
			public string name;
			public string type;
			public float x;
			public float y;
			public float width;
			public float height;
			public float rotation;
			public string points;
		}
	}
}