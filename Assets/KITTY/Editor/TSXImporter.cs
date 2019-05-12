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

			var typedTiles = document
				.Elements("tile")
				.Where(t => t.Attribute("type") != null)
				.Select(t => (id: (int)t.Attribute("id"), type: (string)t.Attribute("type")))
				.ToDictionary(t => t.id, t => t.type);

			var tileShapes = document
				.Elements("tile")
				.Where(t => t.Element("objectgroup") != null)
				.Select(t => (
					id: (uint)t.Attribute("id"),
					objs: t.Element("objectgroup").Elements("object")
				))
				.Select(t => (
					id: t.id,
					objs: t.objs.Select(o => (
						id: (uint)o.Attribute("id"),
						name: (string)o.Attribute("name"),
						type: (string)o.Attribute("type"),
						x: (float)o.Attribute("x"),
						y: (float)o.Attribute("y"),
						width: (float?)o.Attribute("width") ?? 0,
						height: (float?)o.Attribute("height") ?? 0,
						rotation: (float?)o.Attribute("rotation") ?? 0,
						points: (string)o.Element("polygon")?.Attribute("points")
					)).ToArray()
				))
				.ToDictionary(t => t.id, t => t.objs);
			

			// Texture
			var tileTextures = new Dictionary<uint, Texture2D>();
			var tileIDs = new Dictionary<int, uint>();
			if (tileset.imageSource != null) {
				tileset.texture = LoadTexture(tileset.imageSource, context);
				if (tileset.columns == 0) {
					tileset.columns = tileset.texture.width / tileset.tilewidth;
				}
				if (tileset.tilecount == 0) {
					tileset.tilecount = tileset.columns * (tileset.texture.height / tileset.tileheight);
				}
			} else {
				tileTextures = document
					.Elements("tile")
					.Where(t => t.Element("image") != null)
					.Select(t => (
						id: (uint)t.Attribute("id"),
						texture: LoadTexture((string)t.Element("image").Attribute("source"), context)
					)).ToDictionary(t => t.id, t => t.texture);
				tileIDs = document
					.Elements("tile")
					.Where(t => t.Attribute("id") != null)
					.Select((t, i) => (
						i: i,
						id: (uint)t.Attribute("id")
					)).ToDictionary(t => t.i, t => t.id);
				tileset.columns = tileset.tilecount;
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
				if (tileTextures.TryGetValue(i, out var texture)) {
					tileset.sprites[i].texture = texture;
					tileset.sprites[i].rect = new Rect(0, 0, texture.width, texture.height);
				} else if (tileset.texture) {
					tileset.sprites[i].rect = new Rect(rect.x, rect.y + (tileset.texture.height - rows * tileset.tileheight), rect.z, rect.w);
				} else {
					continue;
				}
				tileset.sprites[i].tileset = tileset;
				if (tileShapes.TryGetValue(i, out var shapes)) {
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

			// Tile IDs for image collection tilesets
			for (var i = 0; i < tileIDs.Count; ++i) {
				tileset.tiles[i].id = tileIDs[i];
			}

			// Typed tiles
			foreach (var tile in typedTiles) {
				var id = tile.Key;
				var type = tile.Value;
				var gameObject = PrefabHelper.Load(type, context);
				if (gameObject) {
					tileset.tiles[id].gameObject = gameObject;
				} else {
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
	}
}