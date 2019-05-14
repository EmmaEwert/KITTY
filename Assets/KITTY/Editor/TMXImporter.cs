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
			var assetPathPrefix = Path.GetDirectoryName(assetPath) + Path.DirectorySeparatorChar;
			// TODO: Support ../source.tsx
			var tmx = new TMX(XDocument.Load(assetPath).Element("map"), assetPathPrefix);
			if (tmx.infinite) {
				throw new NotImplementedException("Infinite: " + tmx.infinite);
			}
			var layout = tmx.orientation == "orthogonal"
				? GridLayout.CellLayout.Rectangle
				: tmx.orientation == "isometric"
					? GridLayout.CellLayout.Isometric
					: GridLayout.CellLayout.Hexagon;

			var gids = new uint[tmx.layers.Length][];
			var gidSet = new HashSet<uint>();
			// TODO: & 0x1fffffff the gidSet values from tile gids[]
			for (var i = 0; i < tmx.layers.Length; ++i) {
				var layer = tmx.layers[i];
				if (layer.data.value != null) { // Tile layer
					gids[i] = ParseGIDs(
						layer.data.encoding,
						layer.data.compression,
						layer.data.value,
						layer.width,
						layout
					);
					gidSet.UnionWith(gids[i]);
				} else { // Object layer
					foreach (var @object in layer.objects) {
						gidSet.Add(@object.gid & 0x1fffffff);
					}
				}
			}

			// Tilesets
			var tiles = new Tile[1]; // Global Tile IDs start from 1
			foreach (var tsx in tmx.tilesets) {
				Tileset tileset = null;
				if (tsx.source == null) { // Embedded tileset
					tileset = TSXImporter.Load(context, tsx);
				} else { // External tileset
					tileset = AssetDatabase.LoadAssetAtPath<Tileset>(assetPathPrefix + tsx.source);
					context.DependsOnSourceAsset(assetPathPrefix + tsx.source);
				}
				var tilesetTiles = new Tile[tileset.tiles.Length];
				for (var i = 0; i < tilesetTiles.Length; ++i) {
					if (!gidSet.Contains((uint)(i + tsx.firstgid))) { continue; }
					tilesetTiles[i] = tileset.tiles[i].Instantiate(tmx.tilewidth);
					context.AddObjectToAsset($"tile{i + tsx.firstgid:0000}", tilesetTiles[i]);
					context.AddObjectToAsset($"sprite{i + tsx.firstgid:0000}", tilesetTiles[i].sprite);
				}
				ArrayUtility.AddRange(ref tiles, tilesetTiles);
			}

			// Grid
			var grid = new GameObject(Path.GetFileNameWithoutExtension(assetPath), typeof(Grid));
			grid.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
			grid.isStatic = true;
			grid.GetComponent<Grid>().cellLayout = layout;
			grid.GetComponent<Grid>().cellSize = new Vector3(1f, (float)tmx.tileheight / tmx.tilewidth, 1f);
			context.AddObjectToAsset("grid", grid);
			context.SetMainObject(grid);
			var collider = grid.AddComponent<CompositeCollider2D>();
			collider.generationType = CompositeCollider2D.GenerationType.Manual;

			// Layers
			PrefabHelper.cache.Clear();
			for (var i = 0; i < tmx.layers.Length; ++i) {
				var layer = tmx.layers[i];
				var layerObject = new GameObject(layer.name);
				layerObject.transform.parent = grid.transform;
				layerObject.isStatic = true;

				// Tile layer
				if (layer.data.value != null) {
					// Tilemap
					var layerTiles = new Tile[gids[i].Length];
					for (var j = 0; j < layerTiles.Length; ++j) {
						layerTiles[j] = tiles[gids[i][j] & 0x1ffffff]; // 3 MSB are for flipping
					}

					var size = new Vector3Int(layer.width, layer.height, 1);
					var bounds = new BoundsInt(Vector3Int.zero, size);
					var tilemap = layerObject.AddComponent<Tilemap>();
					var renderer = layerObject.AddComponent<TilemapRenderer>();
					layerObject.AddComponent<TilemapCollider2D>().usedByComposite = true;
					tilemap.color = new Color(1f, 1f, 1f, layer.opacity);
					tilemap.orientation = layout == GridLayout.CellLayout.Hexagon ? Tilemap.Orientation.Custom : Tilemap.Orientation.XY;
					tilemap.orientationMatrix = Matrix4x4.TRS(
						Vector3.zero,
						Quaternion.Euler(0f, 180f, 180f),
						Vector3.one
					);
					tilemap.transform.localScale = layout == GridLayout.CellLayout.Hexagon ? new Vector3(1f, -1f, 1f) : Vector3.one;
					tilemap.SetTilesBlock(bounds, layerTiles);
					renderer.sortOrder = layout == GridLayout.CellLayout.Hexagon ? TilemapRenderer.SortOrder.BottomLeft : TilemapRenderer.SortOrder.TopRight;
					renderer.sortingOrder = i;

					// Flipped tiles
					for (var j = 0; j < gids[i].Length; ++j) {
						var diagonal   = (gids[i][j] >> 29) & 1;
						var vertical   = (gids[i][j] >> 30) & 1;
						var horizontal = (gids[i][j] >> 31) & 1;
						var flips = new Vector4(diagonal, vertical, horizontal, 0);
						if (flips.sqrMagnitude > 0f) {
							var position = new Vector3Int(j % tmx.width, j / tmx.width, 0);
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
					var objects = layer.objects;//layer.Elements("object").ToArray();
					foreach (var @object in objects) {
						var tile = tiles[@object.gid & 0x1fffffff];
						string icon = null;
						GameObject gameObject; // Doubles as prefab when object has type

						// Default instantiation when object has no set type
						if (string.IsNullOrEmpty(@object.type)) {
							gameObject = new GameObject($"{@object.name} {@object.id}".Trim());
							icon = "sv_label_0";

						// Warn instantiation when object has type but no prefab was found
						} else if (null == (gameObject = PrefabHelper.Load(@object.type, context))) {
							var gameObjectName = string.IsNullOrEmpty(@object.name) ? @object.type : @object.name;
							gameObject = new GameObject($"{gameObjectName} {@object.id}".Trim());
							icon = "sv_label_6";
							Debug.LogWarning($"No prefab named \"{@object.type}\" could be found, skipping.");

						// Prefab instantiation based on object type
						} else {
							gameObject = PrefabUtility.InstantiatePrefab(gameObject) as GameObject;
							gameObject.name = $"{@object.name} {@object.id}".Trim();
							foreach (var component in gameObject.GetComponentsInChildren<MonoBehaviour>()) {
								foreach (var field in component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
									var attribute = field.GetCustomAttribute<TiledPropertyAttribute>();
									if (attribute == null) { continue; }
									Debug.Log($"Found attribute on {field.Name}");
									var fieldName = attribute.name ?? field.Name.ToLower();
									var fieldType = field.FieldType.ToString();
									switch (fieldType) {
										case "System.Int32": fieldType = "int"; break;
										case "System.Single": fieldType = "float"; break;
										case "System.String": fieldType = "string"; break;
										case "System.Boolean": fieldType = "bool"; break;
									}
									foreach (var prop in @object.properties) {
										Debug.Log($"Checking property {prop.name} == {fieldName} && {prop.type} == {fieldType}");
										if (prop.name.ToLower() == fieldName && prop.type == fieldType) {
											Debug.Log($"Found property {prop.name}");
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
							var diagonal   = ((@object.gid >> 29) & 1) == 1 ? true : false;
							var vertical   = ((@object.gid >> 30) & 1) == 1 ? true : false;
							var horizontal = ((@object.gid >> 31) & 1) == 1 ? true : false;
							gameObject.transform.localPosition = new Vector3(@object.x / tmx.tileheight, -@object.y / tmx.tileheight + tmx.height, 0);
							var renderer = new GameObject("Renderer").AddComponent<SpriteRenderer>();
							renderer.transform.SetParent(gameObject.transform, worldPositionStays: false);
							renderer.sprite = sprite;
							renderer.sortingOrder = i;
							renderer.drawMode = SpriteDrawMode.Sliced;
							renderer.size = new Vector2(@object.width, @object.height) / tmx.tilewidth;
							renderer.flipX = horizontal;
							renderer.flipY = vertical;
							renderer.color = new Color(1f, 1f, 1f, layer.opacity);
							gameObject.transform.localRotation = Quaternion.Euler(0f, 0f, -@object.rotation);
						} else {
							gameObject.transform.localPosition =
								new Vector3(@object.x / tmx.tileheight, -@object.y / tmx.tileheight + tmx.height - @object.height / tmx.tileheight, 0);
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
							var localPosition = new Vector2(@object.width, @object.height) / tmx.tilewidth / 2f;
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
		uint[] ParseGIDs(string encoding, string compression, string data, int width, GridLayout.CellLayout layout) {
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
			if (layout == GridLayout.CellLayout.Rectangle) {
				return ArrayHelper.Reverse(gids, stride: width);
			} else if (layout == GridLayout.CellLayout.Isometric) {
				return ArrayHelper.Swizzle(gids, stride: width).Reverse().ToArray();
			} else {
				return gids;
			}
		}

	}
}
