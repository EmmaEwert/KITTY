namespace KITTY {
	using System;
	using System.Collections.Generic;
	using System.IO;
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
			var map = new Map(context, tmx);

			// Generate tilemap grid prefab and layer game objects.
			var grid = GenerateGrid(context, map);
			var gameObjects = GenerateLayerGameObjects(context, map);

			// Parent the layer game objects to the tilemap grid prefab.
			foreach (var gameObject in gameObjects) {
				gameObject.transform.parent = grid.transform;
			}
		}

		///<summary>Instantiate and configure main grid game object.</summary>
		GameObject GenerateGrid(
			AssetImportContext context,
			Map map
		) {
			var gameObject = new GameObject(Path.GetFileNameWithoutExtension(assetPath));
			var grid = gameObject.AddComponent<Grid>();
			grid.cellSize = new Vector3(1f, (float)map.tileheight / map.tilewidth, 1f);
			switch (map.orientation) {
				case "orthogonal": grid.cellLayout = CellLayout.Rectangle; break;
				case "isometric":  grid.cellLayout = CellLayout.Isometric; break;
				case "hexagonal":  grid.cellLayout = CellLayout.Hexagon;   break;
				default: throw new NotImplementedException($"Orientation: {map.orientation}");
			}
			gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
			gameObject.AddComponent<CompositeCollider2D>();
			gameObject.isStatic = true;
			context.AddObjectToAsset("Grid", gameObject);
			context.SetMainObject(gameObject);
			return gameObject;
		}

		///<summary>Generate gameobjects for each layer</summary>
		GameObject[] GenerateLayerGameObjects(
			AssetImportContext context,
			Map map
		) {
			PrefabHelper.cache.Clear();
			var animators = new Dictionary<uint, AnimatorController>();
			var layerObjects = new GameObject[map.layers.Length];
			for (var i = 0; i < layerObjects.Length; ++i) {
				var layer = map.layers[i];
				var layerObject = new GameObject(layer.name);
				layerObject.isStatic = true;
				layerObjects[i] = layerObject;
				if (layer.chunks.Length > 0) {
					GenerateTilemapLayer(
						map,
						layer,
						layerObject,
						sortingOrder: i
					);
				} else {
					GenerateObjectLayer(
						context,
						map,
						layer,
						layerObject,
						sortingOrder: i,
						animators
					);
				}

			}
			return layerObjects;
		}

		///<summary>Add and configure tilemap component for layer.</summary>
		private void GenerateTilemapLayer(
			Map map,
			Map.Layer layer,
			GameObject layerObject,
			int sortingOrder
		) {
			var tilemap = layerObject.AddComponent<UnityEngine.Tilemaps.Tilemap>();
			var tilemapRenderer = layerObject.AddComponent<TilemapRenderer>();
			layerObject.AddComponent<TilemapCollider2D>().usedByComposite = true;
			tilemap.color = new Color(1f, 1f, 1f, layer.opacity);
			if (map.orientation == "hexagonal") {
				tilemap.orientation = UnityEngine.Tilemaps.Tilemap.Orientation.Custom;
				tilemap.orientationMatrix = Matrix4x4.TRS(
					Vector3.zero,
					Quaternion.Euler(0f, 180f, 180f),
					Vector3.one
				);
				tilemap.transform.localScale = new Vector3(1f, -1f, 1f);
				tilemapRenderer.sortOrder = TilemapRenderer.SortOrder.BottomLeft;
			} else {
				tilemapRenderer.sortOrder = TilemapRenderer.SortOrder.TopLeft;
			}
			tilemapRenderer.sortingOrder = sortingOrder;

			// Chunks
			for (var j = 0; j < layer.chunks.Length; ++j) {
				var chunk = layer.chunks[j];
				var chunkTiles = new Tile[chunk.gids.Length];
				for (var k = 0; k < chunkTiles.Length; ++k) {
					// 3 MSB are for flipping
					chunkTiles[k] = map.tiles[(int)(chunk.gids[k] & 0x1ffffff)];
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
					if (diagonal + vertical + horizontal == 0f) { continue; }
					var tilePosition = new Vector3Int(
						layer.width  - chunk.width  + chunk.x + k % chunk.width,
						layer.height - chunk.height - chunk.y + k / chunk.width,
						0
					);
					var transform = Matrix4x4.TRS(
						pos: Vector3.zero,
						Quaternion.AngleAxis(diagonal * 180, new Vector3(-1, 1, 0)),
						Vector3.one - (diagonal == 1
							? new Vector3(vertical, horizontal)
							: new Vector3(horizontal, vertical)
						) * 2
					);
					tilemap.SetTransformMatrix(tilePosition, transform);
				}
			}
		}

		///<summary>Generate a game object for each Tiled object in layer.</summary>
		private void GenerateObjectLayer(
			AssetImportContext context,
			Map map,
			Map.Layer layer,
			GameObject layerObject,
			int sortingOrder,
			Dictionary<uint, AnimatorController> animators
		) {
			// Object layer
			foreach (var @object in layer.objects) {
				var tile = map.tiles[(int)(@object.gid & 0x1fffffff)];
				var sprite = tile?.sprite;
				var gameObject = GenerateGameObject(context, @object, tile);

				// Transform
				gameObject.transform.parent = layerObject.transform;
				gameObject.transform.localPosition = new Vector3(
					@object.x / map.tilewidth,
					-@object.y / map.tileheight + map.height
				);
				gameObject.transform.localRotation *=
					Quaternion.Euler(0f, 0f, -@object.rotation);

				// Non-tile-based object
				if (!sprite) {
					gameObject.transform.localPosition +=
						Vector3.down * @object.height / map.tileheight;
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
						gameObject.transform.right * @object.width / map.tilewidth;
				}
				if (vertical) {
					gameObject.transform.localPosition -=
						gameObject.transform.up * @object.height / map.tileheight;
				}
				var childPosition = new Vector3(
					@object.width / map.tilewidth / 2f,
					@object.height / map.tileheight / 2f
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
				renderer.size = new Vector2(@object.width, @object.height) / map.tilewidth;
				renderer.color = new Color(1f, 1f, 1f, layer.opacity);

				// Collider
				if (tile.colliderType == Tile.ColliderType.Sprite) {
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
	}
}
