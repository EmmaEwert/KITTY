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
			// if (tmx.infinite) {
				//throw new NotImplementedException("Infinite: " + tmx.infinite);
			// }
			var layout = tmx.orientation == "orthogonal"
				? GridLayout.CellLayout.Rectangle
				: tmx.orientation == "isometric"
					? GridLayout.CellLayout.Isometric
					: GridLayout.CellLayout.Hexagon;

			var gids = new uint[tmx.layers.Length][][];
			var gidSet = new HashSet<uint>();
			// TODO: & 0x1fffffff the gidSet values from tile gids[]
			for (var i = 0; i < tmx.layers.Length; ++i) {
				var layer = tmx.layers[i];
				if (layer.data.chunks != null) { // Tile layer
					gids[i] = new uint[layer.data.chunks.Length][];
					for (var j = 0; j < gids[i].Length; ++j) {
						gids[i][j] = ParseGIDs(
							layer.data.encoding,
							layer.data.compression,
							layer.data.chunks[j].value,
							layer.width,
							layout
						);
						gidSet.UnionWith(gids[i][j]);
					}
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
					for (var j = 0; j < tilesetTiles[i].sprites.Length; ++j) {
						context.AddObjectToAsset($"frame{i + tsx.firstgid:0000}:{j}", tilesetTiles[i].sprites[j]);
					}
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
				if (layer.data.chunks != null) {
					// Tilemap
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
					// Chunks
					for (var j = 0; j < layer.data.chunks.Length; ++j) {
						var layerTiles = new Tile[gids[i][j].Length];
						for (var k = 0; k < layerTiles.Length; ++k) {
							layerTiles[k] = tiles[gids[i][j][k] & 0x1ffffff]; // 3 MSB are for flipping
						}
						var size = new Vector3Int(layer.data.chunks[j].width, layer.data.chunks[j].height, 1);
						var bounds = new BoundsInt(
							new Vector3Int(
								layer.width - layer.data.chunks[j].width + layer.data.chunks[j].x,
								layer.height - layer.data.chunks[j].height - layer.data.chunks[j].y,
								0
							),
						size);
						tilemap.SetTilesBlock(bounds, layerTiles);

						// Flipped tiles
						for (var k = 0; k < gids[i][j].Length; ++k) {
							var diagonal   = (gids[i][j][k] >> 29) & 1;
							var vertical   = (gids[i][j][k] >> 30) & 1;
							var horizontal = (gids[i][j][k] >> 31) & 1;
							var flips = new Vector4(diagonal, vertical, horizontal, 0);
							if (flips.sqrMagnitude > 0f) {
								var position = new Vector3Int(
									layer.width - layer.data.chunks[j].width + k % layer.data.chunks[j].width,
									layer.height - layer.data.chunks[j].height + k / layer.data.chunks[j].width,
									0
								);
								var transform = Matrix4x4.TRS(
									Vector3.zero,
									Quaternion.AngleAxis(diagonal * 180, new Vector3(1, 1, 0)),
									Vector3.one - (diagonal == 1 ? new Vector3(flips.y, flips.z, flips.w) : new Vector3(flips.z, flips.y, flips.w)) * 2
								);
								tilemap.SetTransformMatrix(position, transform);
							}
						}
					}


					renderer.sortOrder = layout == GridLayout.CellLayout.Hexagon ? TilemapRenderer.SortOrder.BottomLeft : TilemapRenderer.SortOrder.TopLeft;
					renderer.sortingOrder = i;

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

						// Prefab instantiation based on object type
						} else {
							gameObject = PrefabUtility.InstantiatePrefab(gameObject) as GameObject;
							gameObject.name = $"{@object.name} {@object.id}".Trim();
							foreach (var component in gameObject.GetComponentsInChildren<MonoBehaviour>()) {
								ApplyProperties(@object.properties, component);
							}
						}

						gameObject.transform.parent = layerObject.transform;

						// Object sprite
						var sprite = tile?.sprite;
						if (sprite) {
							var diagonal   = ((@object.gid >> 29) & 1) == 1 ? true : false;
							var vertical   = ((@object.gid >> 30) & 1) == 1 ? true : false;
							var horizontal = ((@object.gid >> 31) & 1) == 1 ? true : false;
							gameObject.transform.localRotation *= Quaternion.Euler(0f, 0f, -@object.rotation);
							gameObject.transform.localRotation *= Quaternion.Euler(vertical ? 180f : 0f, horizontal ? 180f : 0f, 0f);
							gameObject.transform.localPosition = new Vector3(@object.x / tmx.tilewidth, -@object.y / tmx.tileheight + tmx.height, 0);
							gameObject.transform.localPosition += horizontal ? -gameObject.transform.right * @object.width / tmx.tilewidth : Vector3.zero;
							gameObject.transform.localPosition += vertical ? -gameObject.transform.up * @object.height / tmx.tileheight : Vector3.zero;
							var renderer = new GameObject("Renderer").AddComponent<SpriteRenderer>();
							renderer.transform.SetParent(gameObject.transform, worldPositionStays: false);
							renderer.sprite = sprite;
							renderer.sortingOrder = i;
							renderer.spriteSortPoint = SpriteSortPoint.Pivot;
							renderer.drawMode = SpriteDrawMode.Sliced; // HACK: Makes renderer.size work
							renderer.size = new Vector2(@object.width, @object.height) / tmx.tilewidth;
							renderer.color = new Color(1f, 1f, 1f, layer.opacity);
							renderer.transform.localPosition = new Vector3(@object.width / tmx.tilewidth / 2f, @object.height / tmx.tileheight / 2f);
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
					}
				}
			}

			collider.generationType = CompositeCollider2D.GenerationType.Synchronous;
		}

		void ApplyProperties(TMX.Layer.Object.Property[] properties, MonoBehaviour component) {
			foreach (var field in component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
				var attribute = field.GetCustomAttribute<TiledPropertyAttribute>();
				if (attribute == null) { continue; }
				var fieldName = attribute.name ?? field.Name.ToLower();
				var fieldType = field.FieldType.ToString();
				switch (fieldType) {
					case "System.String":  fieldType = "string"; break;
					case "System.Int32":   fieldType = "int";    break;
					case "System.Single":  fieldType = "float";  break;
					case "System.Boolean": fieldType = "bool";   break;
				}
				foreach (var property in properties) {
					if (property.name.ToLower().Replace(" ", "") == fieldName && property.type == fieldType) {
						switch (property.type) {
							case "string": field.SetValue(component,             property.value);  break;
							case "int":    field.SetValue(component,   int.Parse(property.value)); break;
							case "float":  field.SetValue(component, float.Parse(property.value)); break;
							case "bool":   field.SetValue(component,  bool.Parse(property.value)); break;
						}
					}
				}
			}
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
