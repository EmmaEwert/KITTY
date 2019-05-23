namespace KITTY {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Xml.Linq;
	using UnityEditor;
	using UnityEditor.Animations;
	using UnityEditor.Experimental.AssetImporters;
	using UnityEngine;
	using UnityEngine.Tilemaps;

	///<summary>Tiled TMX tilemap importer.</summary>
	[ScriptedImporter(1, "tmx", 2)]
	internal class TMXImporter : ScriptedImporter {
		///<summary>
		///Construct a tilemap from Tiled, adding named prefab instances based on Type.
		///</summary>
		public override void OnImportAsset(AssetImportContext context) {
			// Load tilemap TMX and any embedded tilesets, respecting relative paths.
			var assetName = Path.GetFileNameWithoutExtension(assetPath);
			var assetDirectory = PathHelper.AssetDirectory(assetPath);
			var tmx = new TMX(XDocument.Load(assetPath).Element("map"), assetDirectory);

			// Determine what cell layout to use based on tilemap orientation.
			GridLayout.CellLayout layout;
			switch (tmx.orientation) {
				case "orthogonal": layout = GridLayout.CellLayout.Rectangle; break;
				case "isometric":  layout = GridLayout.CellLayout.Isometric; break;
				case "hexagonal":  layout = GridLayout.CellLayout.Hexagon;   break;
				default: throw new NotImplementedException($"Orientation: {tmx.orientation}");
			}

			// Build an array of layers, each an array of chunks, each an array of global IDs.
			var layers = new Layer[tmx.layers.Length];
			for (var i = 0; i < layers.Length; ++i) {
				var layer = tmx.layers[i];
				layers[i].chunks = new Layer.Chunk[layer.data.chunks.Length];
				for (var j = 0; j < layers[i].chunks.Length; ++j) {
					layers[i].chunks[j] = ParseChunk(
						layer.data.encoding,
						layer.data.compression,
						layer.data.chunks[j].value,
						layer.data.chunks[j].width,
						layout
					);
				}
			}
			
			// Load tiles in use from all tilesets, pad with `null`s to align global IDs
			var tileset = new List<Tile> { null }; // Global IDs start from 1
			foreach (var tsx in tmx.tilesets) {
				Tileset tsxTileset = null;
				// Load embedded tileset.
				if (tsx.source == null) {
					tsxTileset = TSXImporter.Load(context, tsx);
				// Load external tileset, respecting relative paths.
				} else {
					var tilesetSource = PathHelper.AssetPath(assetDirectory + tsx.source);
					tsxTileset = AssetDatabase.LoadAssetAtPath<Tileset>(tilesetSource);
					context.DependsOnSourceAsset(tilesetSource);
				}
				// Instantiate all tiles, along with their associated sprites.
				var gid = tsx.firstgid;
				foreach (var tsxTile in tsxTileset.tiles) {
					var tile = tsxTile.Instantiate(tmx.tilewidth);
					for (var j = 0; j < tile.sprites.Length; ++j) {
						context.AddObjectToAsset($"Sprite {gid} {j}", tile.sprites[j]);
					}
					context.AddObjectToAsset($"Sprite {gid}", tile.sprite);
					context.AddObjectToAsset($"Tile {gid++}", tile);
					tileset.Add(tile);
				}
			}

			// Instantiate and configure main grid game object.
			var grid = new GameObject(assetName, typeof(Grid));
			grid.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
			grid.isStatic = true;
			grid.GetComponent<Grid>().cellLayout = layout;
			grid.GetComponent<Grid>().cellSize =
				new Vector3(1f, (float)tmx.tileheight / tmx.tilewidth, 1f);
			context.AddObjectToAsset("Grid", grid);
			context.SetMainObject(grid);
			var gridCollider = grid.AddComponent<CompositeCollider2D>();
			gridCollider.generationType = CompositeCollider2D.GenerationType.Manual;

			// Layers
			PrefabHelper.cache.Clear();
			var animationCache = new Dictionary<uint, AnimatorController>();
			for (var i = 0; i < tmx.layers.Length; ++i) {
				var tmxLayer = tmx.layers[i];
				var layer = layers[i];
				var layerObject = new GameObject(tmxLayer.name);
				layerObject.transform.parent = grid.transform;
				layerObject.isStatic = true;

				// Tile layer
				if (layer.chunks.Length > 0) {
					// Tilemap
					var tilemap = layerObject.AddComponent<Tilemap>();
					var renderer = layerObject.AddComponent<TilemapRenderer>();
					layerObject.AddComponent<TilemapCollider2D>().usedByComposite = true;
					tilemap.color = new Color(1f, 1f, 1f, tmxLayer.opacity);
					if (layout == GridLayout.CellLayout.Hexagon) {
						tilemap.orientation = Tilemap.Orientation.Custom;
						tilemap.orientationMatrix = Matrix4x4.TRS(
							Vector3.zero,
							Quaternion.Euler(0f, 180f, 180f),
							Vector3.one
						);
						tilemap.transform.localScale = new Vector3(1f, -1f, 1f);
					}
					// Chunks
					for (var j = 0; j < layer.chunks.Length; ++j) {
						var chunk = layer.chunks[j];
						var tmxChunk = tmxLayer.data.chunks[j];
						var tiles = new Tile[chunk.gids.Length];
						for (var k = 0; k < tiles.Length; ++k) {
							// 3 MSB are for flipping
							tiles[k] = tileset[(int)(chunk.gids[k] & 0x1ffffff)];
						}
						var position = new Vector3Int(
							tmxChunk.x,
							tmxLayer.height - tmxChunk.height - tmxChunk.y,
							0
						);
						var size = new Vector3Int(tmxChunk.width, tmxChunk.height, 1);
						var bounds = new BoundsInt(position, size);
						tilemap.SetTilesBlock(bounds, tiles);
						// Flipped tiles
						for (var k = 0; k < chunk.gids.Length; ++k) {
							var gid = chunk.gids[k];
							var diagonal   = (gid >> 29) & 1;
							var vertical   = (gid >> 30) & 1;
							var horizontal = (gid >> 31) & 1;
							var flips = new Vector4(diagonal, vertical, horizontal, 0);
							if (flips.sqrMagnitude > 0f) {
								var tilePosition = new Vector3Int(
									tmxLayer.width - tmxChunk.width + tmxChunk.x
										+ k % tmxChunk.width + tmxChunk.x,
									tmxLayer.height - tmxChunk.height - tmxChunk.y
										+ k / tmxChunk.width - tmxChunk.y,
									0
								);
								var transform = Matrix4x4.TRS(
									Vector3.zero,
									Quaternion.AngleAxis(diagonal * 180, new Vector3(1, 1, 0)),
									Vector3.one - (diagonal == 1
										? new Vector3(flips.y, flips.z, flips.w)
										: new Vector3(flips.z, flips.y, flips.w)
									) * 2
								);
								tilemap.SetTransformMatrix(tilePosition, transform);
								Debug.Log(tilePosition);
								Debug.Log(transform);
							}
						}
					}
					renderer.sortingOrder = i;
					renderer.sortOrder = layout == GridLayout.CellLayout.Hexagon
						? TilemapRenderer.SortOrder.BottomLeft
						: TilemapRenderer.SortOrder.TopLeft;
				}

				// Object layer
				foreach (var @object in tmxLayer.objects) {
					var tile = tileset[(int)(@object.gid & 0x1fffffff)];
					var properties = Property.Merge(@object.properties, tile?.properties);
					string icon = null;
					GameObject gameObject; // Doubles as prefab when object has type

					if (string.IsNullOrEmpty(@object.type)) {
						// Tile object instantiation when object has no set type, but tile does
						if (tile?.gameObject) {
							var name = $"{tile.gameObject.name} {@object.id}";
							gameObject = Instantiate(tile.gameObject);
							gameObject.name = name;
							var components = gameObject.GetComponentsInChildren<MonoBehaviour>();
							foreach (var component in components) {
								Property.Apply(properties, component);
							}
						// Default instantiation when object has no set type
						} else {
							gameObject = new GameObject($"{@object.name} {@object.id}".Trim());
							icon = "sv_label_0";
						}

					// Warn instantiation when object has type but no prefab was found
					// TODO: Consider tile type
					} else if (null == (gameObject = PrefabHelper.Load(@object.type, context))) {
						var name = string.IsNullOrEmpty(@object.name) ? @object.type : @object.name;
						gameObject = new GameObject($"{name} {@object.id}".Trim());
						icon = "sv_label_6";

					// Prefab instantiation based on object type
					} else {
						gameObject = PrefabUtility.InstantiatePrefab(gameObject) as GameObject;
						gameObject.name = $"{@object.name ?? @object.type} {@object.id}".Trim();
						var components = gameObject.GetComponentsInChildren<MonoBehaviour>();
						foreach (var component in components) {
							Property.Apply(properties, component);
						}
					}

					gameObject.transform.parent = layerObject.transform;

					// Object sprite
					var sprite = tile?.sprite;
					if (sprite) {
						var diagonal   = ((@object.gid >> 29) & 1) == 1 ? true : false;
						var vertical   = ((@object.gid >> 30) & 1) == 1 ? true : false;
						var horizontal = ((@object.gid >> 31) & 1) == 1 ? true : false;
						// Transform
						gameObject.transform.localRotation *=
							Quaternion.Euler(0f, 0f, -@object.rotation)
							* Quaternion.Euler(vertical ? 180f : 0f, horizontal ? 180f : 0f, 0f);
						gameObject.transform.localPosition = new Vector3(
							@object.x / tmx.tilewidth,
							-@object.y / tmx.tileheight + tmx.height
						);
						if (horizontal) {
							gameObject.transform.position -=
								gameObject.transform.right * @object.width / tmx.tilewidth;
						}
						if (vertical) {
							gameObject.transform.position -=
								gameObject.transform.up * @object.height / tmx.tileheight;
						}
						var localPosition = new Vector3(
							@object.width / tmx.tilewidth / 2f,
							@object.height / tmx.tileheight / 2f
						);
						// Renderer
						var renderer = new GameObject("Renderer").AddComponent<SpriteRenderer>();
						renderer.transform.SetParent(gameObject.transform);
						renderer.transform.localPosition = localPosition;
						renderer.transform.localRotation = Quaternion.identity;
						renderer.sprite = sprite;
						renderer.sortingOrder = i;
						renderer.spriteSortPoint = SpriteSortPoint.Pivot;
						renderer.drawMode = SpriteDrawMode.Sliced; // HACK: Makes renderer.size work
						renderer.size = new Vector2(@object.width, @object.height) / tmx.tilewidth;
						renderer.color = new Color(1f, 1f, 1f, tmxLayer.opacity);
						// Collider
						if (tile.colliderType == UnityEngine.Tilemaps.Tile.ColliderType.Sprite) {
							var shapeCount = sprite.GetPhysicsShapeCount();
							for (var j = 0; j < shapeCount; ++j) {
								var points = new List<Vector2>();
								sprite.GetPhysicsShape(j, points);
								var collider = new GameObject($"Collider {j}");
								collider.transform.SetParent(gameObject.transform);
								collider.transform.localPosition = localPosition;
								collider.transform.localRotation = Quaternion.identity;
								collider.AddComponent<PolygonCollider2D>().points =
									points.ToArray();
							}
						}
						// Animation
						if (tile.sprites.Length > 1) {
							if (!animationCache.TryGetValue(
								@object.gid & 0x1fffffff, out var controller
							)) {
								controller = new AnimatorController();
								controller.name = $"{@object.gid}";
								controller.AddLayer("Base Layer");
								controller.layers[0].stateMachine = new AnimatorStateMachine();
								controller.hideFlags = HideFlags.HideInHierarchy;
								var stateMachine = controller.layers[0].stateMachine;
								var binding = new EditorCurveBinding {
									type = typeof(SpriteRenderer),
									path = "",
									propertyName = "m_Sprite",
								};
								var states = new AnimatorState[tile.sprites.Length];
								for (var j = 0; j < states.Length; ++j) {
									var clip = new AnimationClip();
									clip.frameRate = 1000f / tile.duration;
									clip.name = $"{@object.gid} {j}";
									clip.hideFlags = HideFlags.HideInHierarchy;
									var keyframes = new [] {
										new ObjectReferenceKeyframe {
											time = 0,
											value = tile.sprites[j],
										}
									};
									AnimationUtility.SetObjectReferenceCurve(
										clip,
										binding,
										keyframes
									);
									states[j] = stateMachine.AddState($"{j}");
									states[j].motion = clip;
									states[j].writeDefaultValues = false;
									context.AddObjectToAsset($"Clip {@object.gid} {j}", clip);
									context.AddObjectToAsset($"State {@object.gid} {j}", states[j]);
								}
								for (var j = 0; j < states.Length; ++j) {
									var destination = states[(j+1) % states.Length];
									var transition = states[j].AddTransition(destination);
									transition.hasExitTime = true;
									transition.exitTime = 1f;
									transition.duration = 0f;
									context.AddObjectToAsset(
										$"Transition {@object.gid} {j}",
										transition
									);
								}
								animationCache[@object.gid & 0x1fffffff] = controller;
								context.AddObjectToAsset($"Controller {@object.gid}", controller);
								context.AddObjectToAsset(
									$"StateMachine {@object.gid}",
									controller.layers[0].stateMachine
								);
							}
							var animator = renderer.gameObject.AddComponent<Animator>();
							animator.runtimeAnimatorController = controller;
						}
					} else {
						gameObject.transform.localPosition = new Vector3(
							@object.x / tmx.tilewidth,
							-@object.y / tmx.tileheight
								+ tmx.height
								- @object.height / tmx.tileheight
						);
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

			gridCollider.generationType = CompositeCollider2D.GenerationType.Synchronous;
		}

		///<summary>Decode, decompress, and reorder rows of global tile IDs</summary>
		Layer.Chunk ParseChunk(
			string encoding,
			string compression,
			string data,
			int width,
			GridLayout.CellLayout layout
		) {
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
				return new Layer.Chunk { gids = ArrayHelper.Reverse(gids, stride: width) };
			} else if (layout == GridLayout.CellLayout.Isometric) {
				return new Layer.Chunk {
					gids = ArrayHelper.Swizzle(gids, stride: width).Reverse().ToArray()
				};
			} else {
				return new Layer.Chunk { gids = gids };
			}
		}

		struct Layer {
			public Chunk[] chunks;
			public struct Chunk {
				public uint[] gids;
			}
		}
	}
}
