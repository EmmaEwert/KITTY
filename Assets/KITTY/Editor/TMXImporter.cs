namespace KITTY {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Xml.Linq;
	using UnityEditor;
	using UnityEditor.Experimental.AssetImporters;
	using UnityEngine;
	using UnityEngine.Tilemaps;

	///<summary>Tiled TMX tilemap importer.</summary>
	[ScriptedImporter(1, "tmx", 2)]
	internal class TMXImporter : ScriptedImporter {
		///<summary>Construct a tilemap from Tiled, adding named prefab instances based on Type.</summary>
		public override void OnImportAsset(AssetImportContext context) {
			var document = XDocument.Load(assetPath).Element("map");

			// Attributes
			var orientation = (string)document.Attribute("orientation");
			var width       =    (int)document.Attribute("width");
			var height      =    (int)document.Attribute("height");
			var tilewidth   =    (int)document.Attribute("tilewidth");
			var tileheight  =    (int)document.Attribute("tileheight");
			var infinite    =    ((int?)document.Attribute("infinite") ?? 0) != 0;
			var tilesets = document.Elements("tileset").ToArray();
			if (orientation != "orthogonal") {
				throw new NotImplementedException("Orientation: " + orientation);
			} else if (infinite) {
				throw new NotImplementedException("Infinite: " + infinite);
			}
			var layers = document
				.Elements()
				.Where(e => e.Name == "layer" || e.Name == "objectgroup")
				.ToArray();
			var gids = new uint[layers.Length][];
			var gidSet = new HashSet<uint>();
			for (var i = 0; i < layers.Length; ++i) {
				var layer = layers[i];
				var name = (string)layer.Attribute("name");
				if (layer.Name == "layer") { // Tile layer
					var layerWidth = (int)layer.Attribute("width");
					var layerData = layer.Element("data");
					gids[i] = ParseGIDs(
						(string)layerData.Attribute("encoding"),
						(string)layerData.Attribute("compression"),
						layerData.Value,
						layerWidth
					);
					gidSet.UnionWith(gids[i]);
				} else { // Object layer
					var objects = layer.Elements("object").ToArray();
					foreach (var obj in objects) {
						gidSet.Add((uint?)obj.Attribute("gid") ?? 0);
					}
				}
			}

			// Tilesets
			var assetPathPrefix = Path.GetDirectoryName(assetPath) + Path.DirectorySeparatorChar;
			var tiles = new Tile[1]; // Global Tile IDs start from 1
			var tilesetGIDs = 0;
			foreach (var tilesetElement in tilesets) {
				Tileset tileset = null;
				var firstgid = (int)tilesetElement.Attribute("firstgid");
				var tilesetPath = (string)tilesetElement.Attribute("source");
				if (tilesetPath == null) { // Embedded tileset
					tileset = TSXImporter.Load(context, tilesetElement);
				} else { // External tileset
					tileset = AssetDatabase.LoadAssetAtPath<Tileset>(assetPathPrefix + tilesetPath);
					context.DependsOnSourceAsset(assetPathPrefix + tilesetPath);
				}
				var tilesetTiles = new Tile[tileset.tiles.Length];
				for (var i = 0; i < tilesetTiles.Length; ++i) {
					if (!gidSet.Contains((uint)(i + firstgid))) { continue; }
					var sprite = tileset.sprites[i].Instantiate(tilewidth);
					var tile = tileset.tiles[i].Instantiate(sprite);
					context.AddObjectToAsset($"sprite{i + firstgid:0000}", sprite);
					context.AddObjectToAsset($"tile{i + firstgid:0000}", tile);
					tilesetTiles[i] = tile;
					tilesetGIDs++;
				}
				ArrayUtility.AddRange(ref tiles, tilesetTiles);
			}

			// Grid
			var grid = new GameObject(Path.GetFileNameWithoutExtension(assetPath), typeof(Grid));
			grid.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
			grid.isStatic = true;
			context.AddObjectToAsset("grid", grid);
			context.SetMainObject(grid);
			var collider = grid.AddComponent<CompositeCollider2D>();
			collider.generationType = CompositeCollider2D.GenerationType.Manual;

			// Layers
			var depth = layers.Length;
			PrefabHelper.cache.Clear();
			for (var i = 0; i < layers.Length; ++i) {
				var layer = layers[i];
				var name = (string)layer.Attribute("name");
				var offsetx =  ((float?)layer.Attribute("offsetx") ?? 0) / tilewidth;
				var offsety = -((float?)layer.Attribute("offsety") ?? 0) / tilewidth;
				var layerObject = new GameObject(name);
				layerObject.transform.parent = grid.transform;
				layerObject.transform.localPosition = new Vector3(offsetx, offsety, --depth);
				layerObject.isStatic = true;

				// Tile layer
				if (layer.Name == "layer") {
					var layerWidth = (int)layer.Attribute("width");
					var layerHeight = (int)layer.Attribute("height");
					var layerData = layer.Element("data");

					// Tilemap
					var layerTiles = new Tile[gids[i].Length];
					for (var j = 0; j < layerTiles.Length; ++j) {
						layerTiles[j] = tiles[gids[i][j] & 0x1ffffff]; // 3 MSB are for flipping
					}

					var size = new Vector3Int(layerWidth, layerHeight, 1);
					var bounds = new BoundsInt(Vector3Int.zero, size);
					var tilemap = layerObject.AddComponent<Tilemap>();
					var renderer = layerObject.AddComponent<TilemapRenderer>();
					layerObject.AddComponent<TilemapCollider2D>().usedByComposite = true;
					tilemap.SetTilesBlock(bounds, layerTiles);
					renderer.sortOrder = TilemapRenderer.SortOrder.TopLeft;

					// Flipped tiles
					for (var j = 0; j < gids[i].Length; ++j) {
						var diagonal   = (gids[i][j] >> 29) & 1;
						var vertical   = (gids[i][j] >> 30) & 1;
						var horizontal = (gids[i][j] >> 31) & 1;
						var flips = new Vector4(diagonal, vertical, horizontal, 0);
						if (flips.sqrMagnitude > 0f) {
							var position = new Vector3Int(j % width, j / width, 0);
							var transform = Matrix4x4.TRS(
								Vector3.zero,
								Quaternion.AngleAxis(diagonal * 180, new Vector3(1, 1, 0)),
								Vector3.one - (diagonal == 1 ? new Vector3(flips.y, flips.z, flips.w) : new Vector3(flips.z, flips.y, flips.w)) * 2
							);
							tilemap.SetTransformMatrix(position, transform);
						}
					}

				// Object layer
				} else {
					var objects = layer.Elements("object").ToArray();
					foreach (var obj in objects) {
						// Attributes
						var objectID     =    (int)obj.Attribute("id");
						var objectName   = (string)obj.Attribute("name");
						var objectType   = (string)obj.Attribute("type");
						var objectGID    =  (uint?)obj.Attribute("gid") ?? 0;
						var objectX      =  (float)obj.Attribute("x") / tileheight;
						var objectY      = -(float)obj.Attribute("y") / tileheight + height;
						var objectWidth  = (float?)obj.Attribute("width") ?? 0;
						var objectHeight = (float?)obj.Attribute("height") ?? 0;

						// Elements
						var objPropElems = obj.Element("properties")?.Elements("property");
						var props = new List<(string name, string type, string value)>();
						if (objPropElems != null) {
							foreach (var propElem in objPropElems) {
								var propName = (string)propElem.Attribute("name");
								var propType = (string)propElem.Attribute("type") ?? "string";
								var propValue = (string)propElem.Attribute("value") ?? propElem.Value;
								props.Add((propName, propType, propValue));
							}
						}

						var tile = tiles[objectGID & 0x1fffffff]; // TODO: Flip
						string icon = null;
						GameObject gameObject; // Doubles as prefab when object has type

						// Default instantiation when object has no set type
						if (string.IsNullOrEmpty(objectType)) {
							gameObject = new GameObject($"{objectName} {objectID}".Trim());
							icon = "sv_label_0";

						// Warn instantiation when object has type but no prefab was found
						} else if (null == (gameObject = PrefabHelper.Load(objectType, context))) {
							var gameObjectName = string.IsNullOrEmpty(objectName) ? objectType : objectName;
							gameObject = new GameObject($"{gameObjectName} {objectID}".Trim());
							icon = "sv_label_6";
							Debug.LogWarning($"No prefab named \"{objectType}\" could be found, skipping.");

						// Prefab instantiation based on object type
						} else {
							gameObject = PrefabUtility.InstantiatePrefab(gameObject) as GameObject;
							gameObject.name = $"{objectName} {objectID}".Trim();
							foreach (var component in gameObject.GetComponentsInChildren<MonoBehaviour>()) {
								foreach (var field in component.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic)) {
									var attribute = field.GetCustomAttribute<TiledPropertyAttribute>();
									if (attribute == null) { continue; }
									var fieldName = attribute.name ?? field.Name.ToLower();
									var fieldType = field.FieldType.ToString().ToLower();
									foreach (var prop in props) {
										if (prop.name.ToLower() == fieldName && prop.type == fieldType) {
											switch (prop.type) {
												case "string": field.SetValue(component, prop.value); break;
												case "int": field.SetValue(component, int.Parse(prop.value)); break;
												case "float": field.SetValue(component, float.Parse(prop.value)); break;
												case "bool": field.SetValue(component, bool.Parse(prop.value)); break;
											}
										}
									}
								}
							}
						}

						gameObject.transform.parent = layerObject.transform;

						// Object sprite
						var sprite = tile?.sprite;
						if (sprite) {
							gameObject.transform.localPosition = new Vector3(objectX, objectY, 0);
							var renderer = new GameObject("Renderer").AddComponent<SpriteRenderer>();
							renderer.transform.SetParent(gameObject.transform, worldPositionStays: false);
							renderer.sprite = sprite;
						} else {
							gameObject.transform.localPosition =
								new Vector3(objectX, objectY - (float)objectHeight / tileheight, 0);
						}

						// Icon
						if (icon != null) {
							InternalEditorGUIHelper.SetIconForObject(
								gameObject,
								EditorGUIUtility.IconContent(icon).image
							);
						}

						// Align children to center of object
						// TODO: Please don't maybe?
						foreach (Transform child in gameObject.transform) {
							var localPosition = new Vector2(objectWidth, objectHeight) / tilewidth / 2f;
							child.localPosition = new Vector3(
								localPosition.x,
								localPosition.y,
								child.localPosition.z
							);
						}
					}
				}
			}

			collider.generationType = CompositeCollider2D.GenerationType.Synchronous;
		}

		///<summary>Decode, decompress, and reorder rows of global tile IDs</summary>
		uint[] ParseGIDs(string encoding, string compression, string data, int width) {
			// Decoding
			byte[] input;
			switch (encoding) {
				case "base64": input = Convert.FromBase64String(data); break;
				default: throw new NotImplementedException("Encoding: " + (encoding ?? "xml"));
			}

			// Decompression
			byte[] output;
			switch (compression) {
				case null:   output = input;             break;
				case "gzip": output = CompressionHelper.DecompressGZip(input); break;
				case "zlib": output = CompressionHelper.DecompressZlib(input); break;
				default: throw new NotImplementedException("Compression: " + compression);
			}

			// Parse bytes as uint32 gids
			var gids = new uint[output.Length / 4];
			Buffer.BlockCopy(output, 0, gids, 0, output.Length);
			return ArrayHelper.Reverse(gids, stride: width);
		}

	}
}
