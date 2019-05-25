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
			PrefabHelper.cache.Clear();
			var grid = CreateGrid(context, map);
			var gameObjects = CreateLayers(context, map);

			// Parent the layer game objects to the tilemap grid prefab.
			foreach (var gameObject in gameObjects) {
				gameObject.transform.parent = grid.transform;
			}
		}

		///<summary>
		///Create and configure main Grid GameObject.
		///</summary>
		static GameObject CreateGrid(AssetImportContext context, Map map) {
			var gameObject = new GameObject(Path.GetFileNameWithoutExtension(context.assetPath));
			var grid = gameObject.AddComponent<Grid>();
			gameObject.isStatic = true;

			switch (map.orientation) {
				case "orthogonal": grid.cellLayout = CellLayout.Rectangle; break;
				case "isometric":  grid.cellLayout = CellLayout.Isometric; break;
				case "hexagonal":  grid.cellLayout = CellLayout.Hexagon;   break;
				default: throw new NotImplementedException($"Orientation: {map.orientation}");
			}
			grid.cellSize = new Vector3(1f, (float)map.tileheight / map.tilewidth, 1f);

			gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
			gameObject.AddComponent<CompositeCollider2D>();

			context.AddObjectToAsset("Grid", gameObject);
			context.SetMainObject(gameObject);

			return gameObject;
		}

		///<summary>
		///Create a GameObject for each layer.
		///</summary>
		static GameObject[] CreateLayers(AssetImportContext context, Map map) {
			var animators = new Dictionary<uint, AnimatorController>();
			var layerObjects = new GameObject[map.layers.Length];

			// A layer can be either a tile layer (has chunks), or an object layer (no chunks).
			for (var i = 0; i < layerObjects.Length; ++i) {
				var layer = map.layers[i];
				if (layer.chunks.Length > 0) {
					layerObjects[i] = CreateTileLayer(map, layer, order: i);
				} else {
					layerObjects[i] = CreateObjectLayer(context, map, layer, order: i, animators);
				}
				layerObjects[i].isStatic = true;
			}

			return layerObjects;
		}

		///<summary>
		///Add and configure a Tilemap component for a layer.
		///</summary>
		static GameObject CreateTileLayer(Map map, Map.Layer layer, int order) {
			var layerObject = new GameObject(layer.name);
			var tilemap = layerObject.AddComponent<UnityEngine.Tilemaps.Tilemap>();
			var renderer = layerObject.AddComponent<TilemapRenderer>();
			layerObject.AddComponent<TilemapCollider2D>().usedByComposite = true;
			tilemap.color = new Color(1f, 1f, 1f, layer.opacity);
			renderer.sortingOrder = order;

			// Hexagonal maps have to be rotated 180 degrees, flipped horizontally, and the tilemap
			// object has to be flipped vertically. Because of this, the sort order is also weird :/
			if (map.orientation == "hexagonal") {
				tilemap.orientation = UnityEngine.Tilemaps.Tilemap.Orientation.Custom;
				tilemap.orientationMatrix = Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 180f));
				tilemap.transform.localScale = new Vector3(1f, -1f, 1f);
				renderer.sortOrder = TilemapRenderer.SortOrder.BottomLeft;
			} else {
				renderer.sortOrder = TilemapRenderer.SortOrder.TopLeft;
			}

			// Tiled generally defines Y-positive as down, whereas Unity defines it as up.
			// This effectively means that all positions need to have their Y-component reversed.
			for (var j = 0; j < layer.chunks.Length; ++j) {
				var chunk = layer.chunks[j];
				var position = new Vector3Int(chunk.x, layer.height - chunk.height - chunk.y, 0);
				var size = new Vector3Int(chunk.width, chunk.height, 1);
				var bounds = new BoundsInt(position, size);
				tilemap.SetTilesBlock(bounds, GetTiles(map, layer, chunk));
				FlipTiles(tilemap, layer, chunk);
			}

			return layerObject;
		}

		///<summary>
		///Get an array of all tiles in a chunk.
		///</summary>
		static Tile[] GetTiles(Map map, Map.Layer layer, Map.Layer.Chunk chunk) {
			var tiles = new Tile[chunk.gids.Length];

			for (var k = 0; k < tiles.Length; ++k) {
				tiles[k] = map.tiles[chunk.gids[k] & 0x1ffffff];
			}

			return tiles;
		}

		///<summary>
		///Flip all tiles in a chunk according to their 3 most significant flip bits, if any.
		///</summary>
		static void FlipTiles(Tilemap tilemap, Map.Layer layer, Map.Layer.Chunk chunk) {
			var chunkPosition = new Vector3Int(chunk.x, layer.height - chunk.height - chunk.y, 0);

			for (var k = 0; k < chunk.gids.Length; ++k) {
				var gid = chunk.gids[k];

				// The 3 most significant bits indicate in what way the tile should be flipped.
				if (gid >> 29 == 0) { continue; }
				var diagonal   = (gid >> 29) & 1;
				var vertical   = (gid >> 30) & 1;
				var horizontal = (gid >> 31) & 1;

				var position = chunkPosition + new Vector3Int(k % chunk.width, k / chunk.width, 0);
				var rotation = Quaternion.identity;
				var scale = Vector3.one;

				// Scaling a tile transform by a negative amount effectively flips it.
				// If it's also flipped diagonally, vertical and horizontal scales are swapped.
				if (diagonal == 1) {
					rotation = Quaternion.AngleAxis(180f, new Vector3(-1f, 1f, 0f));
					scale -= new Vector3(vertical, horizontal) * 2f;
				} else {
					scale -= new Vector3(horizontal, vertical) * 2f;
				}

				var transform = Matrix4x4.TRS(pos: Vector3.zero, rotation, scale);
				tilemap.SetTransformMatrix(position, transform);
			}
		}

		///<summary>
		///Create a GameObject for each Tiled object in layer.
		///</summary>
		static GameObject CreateObjectLayer(
			AssetImportContext context,
			Map map,
			Map.Layer layer,
			int order,
			Dictionary<uint, AnimatorController> animators
		) {
			var layerGameObject = new GameObject(layer.name);

			foreach (var @object in layer.objects) {
				var tile = map.tiles[(int)(@object.gid & 0x1fffffff)];
				var sprite = tile?.sprite;
				var gameObject = CreateObject(context, @object, tile);
				var size =
					new Vector2(@object.width / map.tilewidth, @object.height / map.tileheight);

				// Since Tiled's up is Y-negative while Unity's is Y-positive, the Y position is
				// effectively reversed.
				gameObject.transform.parent = layerGameObject.transform;
				gameObject.transform.localPosition = new Vector3(
					@object.x / map.tilewidth,
					-@object.y / map.tileheight + map.height
				);

				// Tiled's rotation is clockwise, while Unity's is anticlockwise.
				gameObject.transform.localRotation *= Quaternion.Euler(0f, 0f, -@object.rotation);

				// Continue early if there's no tile or sprite associated with the object.
				if (!sprite) {
					gameObject.transform.localPosition += Vector3.down * size.y;
					continue;
				}

				// Position children of the GameObject at the center of the object.
				var childPosition = size / 2f;

				// Rotate and realign the object based on 3 most significant flip bits.
				//var diagonal   = ((@object.gid >> 29) & 1) == 1;
				var vertical   = ((@object.gid >> 30) & 1) == 1;
				var horizontal = ((@object.gid >> 31) & 1) == 1;
				if (vertical) {
					gameObject.transform.localRotation *= Quaternion.Euler(180f, 0f, 0f);
					gameObject.transform.localPosition -= gameObject.transform.up * size.y;
				}
				if (horizontal) {
					gameObject.transform.localRotation *= Quaternion.Euler(0f, 180f, 0f);
					gameObject.transform.localPosition -= gameObject.transform.right * size.x;
				}

				// Create a SpriteRenderer child object, and scale it according to object size. 
				var renderer = new GameObject("Renderer").AddComponent<SpriteRenderer>();
				renderer.transform.SetParent(gameObject.transform);
				renderer.transform.localPosition = childPosition;
				renderer.transform.localRotation = Quaternion.identity;
				renderer.sprite = sprite;
				renderer.sortingOrder = order;
				renderer.spriteSortPoint = SpriteSortPoint.Pivot;
				renderer.drawMode = SpriteDrawMode.Sliced; // HACK: Makes renderer.size work
				renderer.size = new Vector2(@object.width, @object.height) / map.tilewidth;
				renderer.color = new Color(1f, 1f, 1f, layer.opacity);

				// A new animator controller is created for each unique tile, if necessary.
				if (tile.sprites.Length > 1) {
					if (!animators.TryGetValue(@object.gid & 0x1fffffff, out var controller)) {
						controller = CreateAnimatorController(context, $"{@object.gid}", tile);
						animators[@object.gid & 0x1fffffff] = controller;
					}
					var animator = renderer.gameObject.AddComponent<Animator>();
					animator.runtimeAnimatorController = controller;
				}

				// A Collider child object is created for each collision shape defined in Tiled.
				if (tile.colliderType == Tile.ColliderType.Sprite) {
					var shapeCount = sprite.GetPhysicsShapeCount();
					for (var j = 0; j < shapeCount; ++j) {
						var points = new List<Vector2>();
						sprite.GetPhysicsShape(j, points);
						var collider = new GameObject($"Collider {j}");
						collider.AddComponent<PolygonCollider2D>().points = points.ToArray();
						collider.transform.SetParent(gameObject.transform);
						collider.transform.localPosition = childPosition;
						collider.transform.localRotation = Quaternion.identity;
					}
				}
			}

			return layerGameObject;
		}

		///<summary>
		///Load prefab based on object or tile type, if any, or create a GameObject otherwise.
		///</summary>
		static GameObject CreateObject(
			AssetImportContext context,
			TMX.Layer.Object @object,
			Tile tile
		) {
			var name = $"{@object.name ?? @object.type ?? tile?.gameObject?.name ?? ""}";
			name = $"{name} {@object.id}".Trim();
			GameObject gameObject = null;
			var properties = Property.Merge(@object.properties, tile?.properties);
			var icon = string.Empty;

			if (!string.IsNullOrEmpty(@object.type)) {
				// Instantiate prefab based on object type.
				var prefab = PrefabHelper.Load(@object.type, context);
				if (prefab) {
					gameObject = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
				}

			} else if (tile?.gameObject) {
				// Instantiate GameObject based on tile type.
				gameObject = Instantiate(tile.gameObject);

			} else {
				// Default instantiation when object and tile have no set type
				gameObject = new GameObject(name);
				icon = "sv_label_0";
			}

			if (gameObject != null) {
				// Apply properties to all behaviours.
				gameObject.name = name;
				var components = gameObject.GetComponentsInChildren<MonoBehaviour>();
				foreach (var component in components) {
					Property.Apply(properties, component);
				}

			} else {
				// Warn on instantiation when object has type but no prefab was found.
				gameObject = new GameObject(name);
				icon = "sv_label_6";
			}

			// Apply icon to GameObject, if any.
			if (!string.IsNullOrEmpty(icon)) {
				InternalEditorGUIHelper.SetIconForObject(
					gameObject,
					EditorGUIUtility.IconContent(icon).image
				);
			}

			return gameObject;
		}

		///<summary>
		///Create an animator controller for an object based on an animated tile's frames.
		///</summary>
		static AnimatorController CreateAnimatorController(
			AssetImportContext context,
			string name,
			Tile tile
		) {
			// Create an animator controller with a default layer and state machine.
			var controller = new AnimatorController();
			controller.name = name;
			controller.AddLayer("Base Layer");
			controller.hideFlags = HideFlags.HideInHierarchy;
			var stateMachine = controller.layers[0].stateMachine = new AnimatorStateMachine();
			var binding = new EditorCurveBinding {
				type = typeof(SpriteRenderer),
				path = "",
				propertyName = "m_Sprite",
			};

			/// Each frame is defined as its own state, with duration and immediate transitions.
			var states = new AnimatorState[tile.sprites.Length];
			for (var j = 0; j < states.Length; ++j) {
				var clip = new AnimationClip();
				clip.frameRate = 1000f / tile.duration;
				clip.name = $"{name} {j}";
				clip.hideFlags = HideFlags.HideInHierarchy;
				var keyframes = new [] {
					new ObjectReferenceKeyframe { time = 0, value = tile.sprites[j] }
				};
				AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
				states[j] = stateMachine.AddState($"{j}");
				states[j].motion = clip;
				states[j].writeDefaultValues = false;
				context.AddObjectToAsset($"Clip {name} {j}", clip);
				context.AddObjectToAsset($"State {name} {j}", states[j]);
			}

			// Each frame state transitions to the next at the end; the last frame state loops back.
			for (var j = 0; j < states.Length; ++j) {
				var destination = states[(j+1) % states.Length];
				var transition = states[j].AddTransition(destination);
				transition.hasExitTime = true;
				transition.exitTime = 1f;
				transition.duration = 0f;
				context.AddObjectToAsset($"Transition {name} {j}", transition);
			}

			context.AddObjectToAsset($"Controller {name}", controller);
			context.AddObjectToAsset($"StateMachine {name}", controller.layers[0].stateMachine);

			return controller;
		}
	}
}
