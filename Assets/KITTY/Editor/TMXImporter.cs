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
	using static UnityEngine.GridLayout;

	///<summary>Tiled TMX tilemap importer.</summary>
	[ScriptedImporter(1, "tmx", 2)]
	internal class TMXImporter : ScriptedImporter {
		///<summary>
		///Construct a tilemap from Tiled, adding named prefab instances based on Type.
		///</summary>
		public override void OnImportAsset(AssetImportContext context) {
			// Load tilemap TMX and any embedded tilesets, respecting relative paths.
			var assetDirectory = PathHelper.AssetDirectory(assetPath);
			var tmx = new TMX(XDocument.Load(assetPath).Element("map"), assetDirectory);

			// Parse information from tilemap TMX.
			var layout = ParseLayout(tmx.orientation);
			var layers = ParseLayers(layout, tmx.layers);
			var tiles = ParseTilesets(context, tmx.tilesets, tmx.tilewidth);

			// Generate tilemap grid prefab and layer game objects.
			var grid = GenerateGrid(context, layout, tmx.tilewidth, tmx.tileheight);
			var gameObjects = GenerateLayerGameObjects(context, layout, tmx, tiles, layers);

			// Parent the layer game objects to the tilemap grid prefab.
			foreach (var gameObject in gameObjects) {
				gameObject.transform.parent = grid.transform;
			}
		}

		///<summary>Determine what cell layout to use based on tilemap orientation.</summary>
		CellLayout ParseLayout(string orientation) {
			switch (orientation) {
				case "orthogonal": return CellLayout.Rectangle;
				case "isometric":  return CellLayout.Isometric;
				case "hexagonal":  return CellLayout.Hexagon;
				default: throw new NotImplementedException($"Orientation: {orientation}");
			}
		}

		///<summary>
		///Build an array of layers, each an array of chunks, each an array of global IDs.
		///</summary>
		Layer[] ParseLayers(CellLayout layout, TMX.Layer[] tmxLayers) {
			var layers = new Layer[tmxLayers.Length];
			for (var i = 0; i < layers.Length; ++i) {
				var tmxLayer = tmxLayers[i];
				layers[i].name = tmxLayer.name;
				layers[i].opacity = tmxLayer.opacity;
				layers[i].chunks = new Layer.Chunk[tmxLayer.data.chunks.Length];
				layers[i].width = tmxLayer.width;
				layers[i].height = tmxLayer.height;
				for (var j = 0; j < layers[i].chunks.Length; ++j) {
					var chunk = tmxLayer.data.chunks[j];
					layers[i].chunks[j] = ParseChunk(
						tmxLayer.data.encoding,
						tmxLayer.data.compression,
						chunk.value,
						chunk.width,
						layout
					);
					layers[i].chunks[j].width  = chunk.width;
					layers[i].chunks[j].height = chunk.height;
					layers[i].chunks[j].x      = chunk.x;
					layers[i].chunks[j].y      = chunk.y;
				}
			}
			return layers;
		}

		///<summary>Decode, decompress, and reorder rows of global tile IDs</summary>
		Layer.Chunk ParseChunk(
			string encoding,
			string compression,
			string data,
			int width,
			CellLayout layout
		) {
			// Decode
			byte[] input;
			switch (encoding) {
				case "base64": input = Convert.FromBase64String(data); break;
				default: throw new NotImplementedException("Encoding: " + (encoding ?? "xml"));
			}
			// Decompress
			byte[] output;
			switch (compression) {
				case null:   output = input;             break;
				case "gzip": output = CompressionHelper.DecompressGZip(input); break;
				case "zlib": output = CompressionHelper.DecompressZlib(input); break;
				default: throw new NotImplementedException("Compression: " + compression);
			}
			// Parse bytes as uint32 gids, reordered according to cell layout.
			var gids = new uint[output.Length / 4];
			Buffer.BlockCopy(output, 0, gids, 0, output.Length);
			switch (layout) {
				case CellLayout.Rectangle: return new Layer.Chunk {
					gids = ArrayHelper.Reverse(gids, stride: width)
				};
				case CellLayout.Isometric: return new Layer.Chunk {
					gids = ArrayHelper.Swizzle(gids, stride: width).Reverse().ToArray()
				};
				case CellLayout.Hexagon: return new Layer.Chunk { gids = gids };
				default: throw new NotImplementedException($"Layout: {layout}");
			}
		}

		///<summary>Load tiles from all tilesets.</summary>
		List<Tile> ParseTilesets(AssetImportContext context, TSX[] tilesets, int tilewidth) {
			var tiles = new List<Tile> { null }; // Global IDs start from 1
			foreach (var tsx in tilesets) {
				var tileset = ParseTileset(context, tsx);
				for (var i = 0; i < tileset.tiles.Length; ++i) {
					var gid = tsx.firstgid + i;
					var tile = tileset.tiles[i].Instantiate(tilewidth);
					tiles.Add(tile);
					if (!tile) { continue; }
					context.AddObjectToAsset($"Tile {gid}", tile);
					if (!tile.sprite) { continue; }
					context.AddObjectToAsset($"Sprite {gid}", tile.sprite);
					for (var j = 0; j < tile.sprites.Length; ++j) {
						context.AddObjectToAsset($"Sprite {gid} {j}", tile.sprites[j]);
					}
				}
			}
			return tiles;
		}

		///<summary>Load embedded or external tileset.</summary>
		Tileset ParseTileset(AssetImportContext context, TSX tsx) {
			// Load embedded tileset.
			if (tsx.source == null) {
				return TSXImporter.Load(context, tsx);
			// Load external tileset, respecting relative paths.
			} else {
				var source = PathHelper.AssetPath(
					Path.GetDirectoryName(assetPath) +
					Path.DirectorySeparatorChar +
					tsx.source
				);
				context.DependsOnSourceAsset(source);
				return AssetDatabase.LoadAssetAtPath<Tileset>(source);
			}
		}

		///<summary>Instantiate and configure main grid game object.</summary>
		GameObject GenerateGrid(
			AssetImportContext context,
			CellLayout layout,
			int tilewidth,
			int tileheight
		) {
			var grid = new GameObject(Path.GetFileNameWithoutExtension(context.assetPath));
			grid.AddComponent<Grid>().cellLayout = layout;
			grid.GetComponent<Grid>().cellSize = new Vector3(1f, (float)tileheight / tilewidth, 1f);
			grid.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
			grid.AddComponent<CompositeCollider2D>();
			grid.isStatic = true;
			context.AddObjectToAsset("Grid", grid);
			context.SetMainObject(grid);
			return grid;
		}

		GameObject[] GenerateLayerGameObjects(
			AssetImportContext context,
			CellLayout layout,
			TMX tmx,
			List<Tile> tiles,
			Layer[] layers
		) {
			PrefabHelper.cache.Clear();
			var animators = new Dictionary<uint, AnimatorController>();
			var layerObjects = new GameObject[layers.Length];
			for (var i = 0; i < tmx.layers.Length; ++i) {
				var layer = layers[i];
				var tmxLayer = tmx.layers[i];
				var layerObject = new GameObject(layer.name);
				layerObject.isStatic = true;
				layerObjects[i] = layerObject;
				if (layer.chunks.Length > 0) {
					GenerateTilemapLayer(tiles, layer, layerObject, sortingOrder: i, layout);
				} else {
					GenerateObjectLayer(
						context,
						tiles,
						layerObject,
						tmx,
						tmxLayer.objects,
						layer.opacity,
						sortingOrder: i,
						animators
					);
				}

			}
			return layerObjects;
		}

		///<summary>Add and configure tilemap component for layer.</summary>
		private void GenerateTilemapLayer(
			List<Tile> tiles,
			Layer layer,
			GameObject layerObject,
			int sortingOrder,
			CellLayout layout
		) {
			var tilemap = layerObject.AddComponent<UnityEngine.Tilemaps.Tilemap>();
			var tilemapRenderer = layerObject.AddComponent<TilemapRenderer>();
			layerObject.AddComponent<TilemapCollider2D>().usedByComposite = true;
			tilemap.color = new Color(1f, 1f, 1f, layer.opacity);
			if (layout == CellLayout.Hexagon) {
				tilemap.orientation = UnityEngine.Tilemaps.Tilemap.Orientation.Custom;
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
				var chunkTiles = new Tile[chunk.gids.Length];
				for (var k = 0; k < chunkTiles.Length; ++k) {
					// 3 MSB are for flipping
					chunkTiles[k] = tiles[(int)(chunk.gids[k] & 0x1ffffff)];
				}
				var position = new Vector3Int(
					chunk.x,
					layer.height - chunk.height - chunk.y,
					0
				);
				var size = new Vector3Int(chunk.width, chunk.height, 1);
				var bounds = new BoundsInt(position, size);
				tilemap.SetTilesBlock(bounds, chunkTiles);

				// Flipped tiles
				for (var k = 0; k < chunk.gids.Length; ++k) {
					var gid = chunk.gids[k];
					var diagonal   = (gid >> 29) & 1;
					var vertical   = (gid >> 30) & 1;
					var horizontal = (gid >> 31) & 1;
					var flips = new Vector4(diagonal, vertical, horizontal, 0);
					if (flips.sqrMagnitude == 0f) { continue; }
					var tilePosition = new Vector3Int(
						layer.width  - chunk.width  + chunk.x + k % chunk.width + chunk.x,
						layer.height - chunk.height - chunk.y + k / chunk.width - chunk.y,
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
				}
			}
			tilemapRenderer.sortingOrder = sortingOrder;
			tilemapRenderer.sortOrder = layout == CellLayout.Hexagon
				? TilemapRenderer.SortOrder.BottomLeft
				: TilemapRenderer.SortOrder.TopLeft;
		}

		///<summary>Generate a game object for each Tiled object in layer.</summary>
		private void GenerateObjectLayer(
			AssetImportContext context,
			List<Tile> tiles,
			GameObject layerObject,
			TMX tmx,
			TMX.Layer.Object[] objects,
			float opacity,
			int sortingOrder,
			Dictionary<uint, AnimatorController> animators
		) {
			// Object layer
			foreach (var @object in objects) {
				var tile = tiles[(int)(@object.gid & 0x1fffffff)];
				var sprite = tile?.sprite;
				var gameObject = GenerateGameObject(context, @object, tile);

				// Transform
				gameObject.transform.parent = layerObject.transform;
				gameObject.transform.localPosition = new Vector3(
					@object.x / tmx.tilewidth,
					-@object.y / tmx.tileheight + tmx.height
				);
				gameObject.transform.localRotation *=
					Quaternion.Euler(0f, 0f, -@object.rotation);

				// Non-tile-based object
				if (!sprite) {
					gameObject.transform.localPosition +=
						Vector3.down * @object.height / tmx.tileheight;
					continue;
				}

				// Tile-based object
				// Flips
				var diagonal   = ((@object.gid >> 29) & 1) == 1;
				var vertical   = ((@object.gid >> 30) & 1) == 1;
				var horizontal = ((@object.gid >> 31) & 1) == 1;

				// Transform
				gameObject.transform.localRotation *=
					Quaternion.Euler(vertical ? 180f : 0f, horizontal ? 180f : 0f, 0f);
				if (horizontal) {
					gameObject.transform.localPosition -=
						gameObject.transform.right * @object.width / tmx.tilewidth;
				}
				if (vertical) {
					gameObject.transform.localPosition -=
						gameObject.transform.up * @object.height / tmx.tileheight;
				}
				var childPosition = new Vector3(
					@object.width / tmx.tilewidth / 2f,
					@object.height / tmx.tileheight / 2f
				);

				// Renderer
				var renderer = new GameObject("Renderer").AddComponent<SpriteRenderer>();
				renderer.transform.SetParent(gameObject.transform);
				renderer.transform.localPosition = childPosition;
				renderer.transform.localRotation = Quaternion.identity;
				renderer.sprite = sprite;
				renderer.sortingOrder = sortingOrder;
				renderer.spriteSortPoint = SpriteSortPoint.Pivot;
				renderer.drawMode = SpriteDrawMode.Sliced; // HACK: Makes renderer.size work
				renderer.size = new Vector2(@object.width, @object.height) / tmx.tilewidth;
				renderer.color = new Color(1f, 1f, 1f, opacity);

				// Collider
				if (tile.colliderType == UnityEngine.Tilemaps.Tile.ColliderType.Sprite) {
					var shapeCount = sprite.GetPhysicsShapeCount();
					for (var j = 0; j < shapeCount; ++j) {
						var points = new List<Vector2>();
						sprite.GetPhysicsShape(j, points);
						var collider = new GameObject($"Collider {j}");
						collider.transform.SetParent(gameObject.transform);
						collider.transform.localPosition = childPosition;
						collider.transform.localRotation = Quaternion.identity;
						collider.AddComponent<PolygonCollider2D>().points =
							points.ToArray();
					}
				}

				// Animator
				if (tile.sprites.Length > 1) {
					if (!animators.TryGetValue(@object.gid & 0x1fffffff, out var controller)) {
						controller = GenerateAnimatorController(context, @object.gid, tile);
						animators[@object.gid & 0x1fffffff] = controller;
					}
					var animator = renderer.gameObject.AddComponent<Animator>();
					animator.runtimeAnimatorController = controller;
				}
			}
		}

		///<summary>Load or generate GameObject based on object or tile type, if any.</summary>
		GameObject GenerateGameObject(AssetImportContext context, TMX.Layer.Object @object, Tile tile) {
			GameObject gameObject;
			var properties = Property.Merge(@object.properties, tile?.properties);
			var icon = string.Empty;

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

			// Icon
			if (!string.IsNullOrEmpty(icon)) {
				InternalEditorGUIHelper.SetIconForObject(
					gameObject,
					EditorGUIUtility.IconContent(icon).image
				);
			}

			return gameObject;
		}

		///<summary>Generate animator controller for objects based on animated tiles.</summary>
		AnimatorController GenerateAnimatorController(AssetImportContext context, uint gid, Tile tile) {
			var controller = new AnimatorController();
			var name = $"{gid}";
			controller.name = name;
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
				clip.name = $"{name} {j}";
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
				context.AddObjectToAsset($"Clip {name} {j}", clip);
				context.AddObjectToAsset($"State {name} {j}", states[j]);
			}
			for (var j = 0; j < states.Length; ++j) {
				var destination = states[(j+1) % states.Length];
				var transition = states[j].AddTransition(destination);
				transition.hasExitTime = true;
				transition.exitTime = 1f;
				transition.duration = 0f;
				context.AddObjectToAsset($"Transition {name} {j}", transition);
			}
			//animationCache[@object.gid & 0x1fffffff] = controller;
			context.AddObjectToAsset($"Controller {name}", controller);
			context.AddObjectToAsset($"StateMachine {name}", controller.layers[0].stateMachine);
			return controller;
		}

		struct Layer {
			public string name;
			public float opacity;
			public int width;
			public int height;
			public Chunk[] chunks;
			public struct Chunk {
				public int width;
				public int height;
				public int x;
				public int y;
				public uint[] gids;
			}
		}
	}
}
