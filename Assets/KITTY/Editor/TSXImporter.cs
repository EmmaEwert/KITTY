namespace KITTY {
	using System;
	using System.Collections.Generic;
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
			tileset.name        = (string )document.Attribute("name");
			tileset.tilewidth   = (int    )document.Attribute("tilewidth");
			tileset.tileheight  = (int    )document.Attribute("tileheight");
			tileset.spacing     = (int?   )document.Attribute("spacing") ?? 0;
			tileset.margin      = (int?   )document.Attribute("margin") ?? 0;
			tileset.tilecount   = (int?   )document.Attribute("tilecount") ?? 0;
			tileset.columns     = (int?   )document.Attribute("columns") ?? 0;

			context.AddObjectToAsset($"tileset_{tileset.name}", tileset);

			// Elements
			var tileoffset  = document.Element("tileoffset");
			tileset.tileoffset  = new Vector2Int(
				(int?)tileoffset?.Attribute("x") ?? 0,
				(int?)tileoffset?.Attribute("y") ?? 0
			);
			tileset.imageSource = (string)document.Element("image")?.Attribute("source");

			var tileCollection = document
				.Elements("tile")
				.Select(t => LoadTile(t))
				.OrderBy(t => t.id)
				.ToArray();

			tileset.tilecount = Mathf.Max(tileset.tilecount, tileCollection.LastOrDefault().id + 1);

			// Texture
			if (tileset.imageSource == null) {
				tileset.columns = tileset.tilecount;
			} else {
				tileset.texture = LoadTexture(tileset.imageSource, context);
				tileset.columns = tileset.texture.width / tileset.tilewidth;
				tileset.tilecount = tileset.columns * tileset.texture.height / tileset.tileheight;
			}

			var tileArray = new (GameObject prefab, Texture2D texture, Shape[] shapes)[tileset.tilecount];
			for (var i = 0; i < tileArray.Length; ++i) {
				tileArray[i].texture = tileset.texture;
			}
			foreach (var tile in tileCollection) {
				tileArray[tile.id] = (
					prefab: PrefabHelper.Load(tile.type, context),
					texture: tileset.texture ?? LoadTexture(tile.image, context),
					shapes: tile.shapes
				);
			}

			// Sprites
			tileset.sprites = new Tileset.Sprite[tileset.tilecount];
			tileset.spritePivot =
				Vector2.one * 0.5f
				- tileset.tileoffset
				/ new Vector2(tileset.tilewidth, tileset.tileheight);
			for (var i = 0u; i < tileset.sprites.Length; ++i) {
				var s0 = new Vector2Int(tileset.tilewidth, tileset.tileheight);
				var s1 = new Vector2Int(tileset.spacing, 0);
				var spacing = new Vector4(s0.x, s0.y, s0.x, s0.y) + new Vector4(s1.x, s1.x, s1.y, s1.y);
				var margin = new Vector4(tileset.margin, tileset.margin, 0, 0);
				var rows = tileset.tilecount / tileset.columns;
				var rect = Vector4.Scale(spacing, new Vector4(i % tileset.columns, rows - i / tileset.columns - 1, 1, 1)) + margin;
				var texture = tileArray[i].texture;
				tileset.sprites[i].texture = texture;
				tileset.sprites[i].tileset = tileset;
				if (string.IsNullOrEmpty(tileset.imageSource)) { // Image collection
					tileset.sprites[i].rect = new Rect(0, 0, texture?.width ?? 0, texture?.height ?? 0);
				} else if (tileset.texture) {
					tileset.sprites[i].rect = new Rect(rect.x, rect.y + (tileset.texture.height - rows * tileset.tileheight - rows * tileset.spacing - margin.x), rect.z, rect.w);
				} else {
					continue;
				}
				var shapes = tileArray[i].shapes;
				if (shapes != null) {
					tileset.sprites[i].shapes = new Vector2[shapes.Length][];
					for (var j = 0; j < shapes.Length; ++j) {
						var obj = shapes[j];
						// Rectangle shape
						if (obj.width > 0 && obj.height > 0) {
							tileset.sprites[i].shapes[j] = new [] {
								new Vector2(obj.x, rect.w - obj.y),
								new Vector2(obj.x, rect.w - obj.y - obj.height),
								new Vector2(obj.x + obj.width, rect.w - obj.y - obj.height),
								new Vector2(obj.x + obj.width, rect.w - obj.y)
							};
						// Polygon shape
						} else if (obj.points != null) {
							var points = obj.points.Split(' ');
							tileset.sprites[i].shapes[j] = new Vector2[points.Length];
							for (var k = 0; k < points.Length; ++k) {
								var point = points[k].Split(',');
								var x = float.Parse(point[0]);
								var y = float.Parse(point[1]);
								tileset.sprites[i].shapes[j][k] =
									new Vector2(obj.x + x, rect.w - obj.y - y);
							}
						}
					}
				}
			}

			// Tiles
			tileset.tiles = new Tileset.Tile[tileset.tilecount];

			// Typed tiles
			foreach (var tile in tileCollection) {
				var id = tile.id;
				var type = tile.type;
				var gameObject = PrefabHelper.Load(type, context);
				if (gameObject) {
					tileset.tiles[id].gameObject = gameObject;
				} else if (!string.IsNullOrEmpty(type)) {
					Debug.LogWarning($"No prefab named \"{type}\" could be found, skipping.");
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