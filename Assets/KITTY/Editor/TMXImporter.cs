namespace KITTY {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Reflection;
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
			var name = Path.GetFileNameWithoutExtension(context.assetPath);

			// Instantiate a prefab named after the tilemap name, if any.
			var prefab = PrefabHelper.Load(name, context, map.properties.Length > 0);
			GameObject gameObject;
			if (prefab) {
				gameObject = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
				foreach (var component in gameObject.GetComponentsInChildren<MonoBehaviour>()) {
					Property.Apply(map.properties, component);
				}
			} else {
				gameObject = new GameObject(name);
			}

			var grid = gameObject.AddComponent<Grid>();
			gameObject.isStatic = true;

			switch (map.orientation) {
				case "orthogonal": grid.cellLayout = CellLayout.Rectangle; break;
				case "isometric":  grid.cellLayout = CellLayout.Isometric; break;
				case "hexagonal":  grid.cellLayout = CellLayout.Hexagon;   break;
				default: throw new NotImplementedException($"Orientation: {map.orientation}");
			}

			// Grid cells are always 1 unit wide; their height respects the aspect ratio.
			grid.cellSize = new Vector3(1f, (float)map.tileheight / map.tilewidth, 1f);

			gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
			gameObject.AddComponent<CompositeCollider2D>();

			context.AddObjectToAsset("Grid", gameObject);
			context.SetMainObject(gameObject);

			return gameObject;
		}

		///<summary>
		///Instantiate a prefab or create a GameObject for each layer.
		///</summary>
		static GameObject[] CreateLayers(AssetImportContext context, Map map) {
			var animators = new Dictionary<uint, AnimatorController>();
			var layerObjects = new GameObject[map.layers.Length];

			// A layer can be either a tile layer (has chunks), or an object layer (no chunks).
			for (var i = 0; i < layerObjects.Length; ++i) {
				var layer = map.layers[i];

				// Instantiate a prefab named after the layer name, if any.
				var prefab = PrefabHelper.Load(layer.name, context, layer.properties.Length > 0);
				if (prefab) {
					layerObjects[i] = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
					foreach (var component in layerObjects[i].GetComponentsInChildren<MonoBehaviour>()) {
						Property.Apply(layer.properties, component);
					}
				} else {
					layerObjects[i] = new GameObject(layer.name);
				}

				if (layer.chunks.Length > 0) {
					CreateTileLayer(layerObjects[i], map, layer, order: i);
				} else {
					CreateObjectLayer(context, layerObjects[i], map, layer, order: i, animators);
				}
				layerObjects[i].isStatic = true;
			}

			return layerObjects;
		}

		///<summary>
		///Add and configure a Tilemap component for a layer.
		///</summary>
		static void CreateTileLayer(
			GameObject layerObject,
			Map map,
			Map.Layer layer,
			int order
		) {
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
				CreateTileObjects(map, layer, chunk, layerObject);
			}
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

				// Flip tiles along X, Y, and diagonally based on the flip flags.
				if (vertical == 1) {
					rotation *= Quaternion.Euler(180f, 0f, 0f);
				}
				if (horizontal == 1) {
					rotation *= Quaternion.Euler(0f, 180f, 0f);
				}
				if (diagonal == 1) {
					rotation *= Quaternion.AngleAxis(180f, new Vector3(-1f, 1f, 0f));
				}
				var transform = Matrix4x4.TRS(pos: Vector3.zero, rotation, s: Vector3.one);
				tilemap.SetTransformMatrix(position, transform);
			}
		}

		///<summary>
		///Create a GameObject for each Tile with a defined type and thus potentially a prefab.
		///</summary>
		static void CreateTileObjects(
			Map map,
			Map.Layer layer,
			Map.Layer.Chunk chunk,
			GameObject layerObject
		) {
			var chunkPosition = new Vector3Int(chunk.x, layer.height - chunk.height - chunk.y, 0);

			for (var k = 0; k < chunk.gids.Length; ++k) {
				var gid = chunk.gids[k];
				var tile = map.tiles[gid & 0x1ffffff];
				if (!tile?.prefab) { continue; }

				// The 3 most significant bits indicate in what way the tile should be flipped.
				var diagonal   = (gid >> 29) & 1;
				var vertical   = (gid >> 30) & 1;
				var horizontal = (gid >> 31) & 1;

				var position = chunkPosition + new Vector3Int(k % chunk.width, k / chunk.width, 0);

				// Since a tile's GameObject is instantiated automatically in Edit Mode and *again*
				// in Play Mode, we can't use the built in approach, and instead have to
				// handle instantiation ourselves to avoid duplicate GameObjects.
				var gameObject = PrefabUtility.InstantiatePrefab(
					tile.prefab, layerObject.transform
				) as GameObject;
				if (gameObject) {
					gameObject.name += $" {position.x},{position.y}";
					gameObject.transform.localPosition = position;
					if (vertical == 1) {
						gameObject.transform.localRotation *= Quaternion.Euler(180f, 0f, 0f);
						gameObject.transform.localPosition -= gameObject.transform.up;
					}
					if (horizontal == 1) {
						gameObject.transform.localRotation *= Quaternion.Euler(0f, 180f, 0f);
						gameObject.transform.localPosition -= gameObject.transform.right;
					}
					if (diagonal == 1) {
						gameObject.transform.localRotation *=
							Quaternion.AngleAxis(180f, new Vector3(-1f, 1f, 0f));
						gameObject.transform.localPosition -= gameObject.transform.up;
						gameObject.transform.localPosition -= gameObject.transform.right;
					}
					foreach (var component in gameObject.GetComponentsInChildren<MonoBehaviour>()) {
						Property.Apply(tile.properties, component);
					}
				}
			}
		}
		

		///<summary>
		///Create a GameObject for each Tiled object in layer.
		///</summary>
		static void CreateObjectLayer(
			AssetImportContext context,
			GameObject layerObject,
			Map map,
			Map.Layer layer,
			int order,
			Dictionary<uint, AnimatorController> animators
		) {
			foreach (var @object in layer.objects) {
				var tile = map.tiles[(int)(@object.gid & 0x1fffffff)];
				var sprite = tile?.sprite;
				var gameObject = CreateObject(context, @object, tile);
				var size =
					new Vector2(@object.width / map.tilewidth, @object.height / map.tileheight);

				// Since Tiled's up is Y-negative while Unity's is Y-positive, the Y position is
				// effectively reversed.
				gameObject.transform.parent = layerObject.transform;
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
				renderer.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;

				// A new animator controller is created for each unique tile, if necessary.
				if (tile.frames.Length > 1) {
					if (!animators.TryGetValue(@object.gid & 0x1fffffff, out var controller)) {
						controller = CreateAnimatorController(context, $"{@object.gid}", tile);
						animators[@object.gid & 0x1fffffff] = controller;
					}
					var animator = renderer.gameObject.AddComponent<Animator>();
					animator.runtimeAnimatorController = controller;

					// Apply animations to all components.
					foreach (var component in gameObject.GetComponentsInChildren<MonoBehaviour>()) {
						ApplyAnimation(context, controller, tile.frames, component);
					}
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
		}

		///<summary>
		///Load prefab based on object or tile type, if any, or create a GameObject otherwise.
		///</summary>
		static GameObject CreateObject(
			AssetImportContext context,
			TMX.Layer.Object @object,
			Tile tile
		) {
			var tileName = tile?.prefab ? tile.prefab.name : string.Empty;
			var name = $"{@object.name ?? @object.type ?? tileName}";
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

			} else if (tile?.prefab) {
				// Instantiate GameObject based on tile type.
				gameObject = PrefabUtility.InstantiatePrefab(tile.prefab) as GameObject;

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
			controller.hideFlags = HideFlags.HideInHierarchy;
			controller.AddLayer("Base Layer");
			controller.layers[0].stateMachine = new AnimatorStateMachine();
			var stateMachine = controller.layers[0].stateMachine;

			// The Start and End parameters define what frame index the desired animation sequence
			// should start and end at, respectively.
			var startParameter = new AnimatorControllerParameter {
				name = "Start",
				type = AnimatorControllerParameterType.Int,
				defaultInt = 0,
			};
			var endParameter = new AnimatorControllerParameter {
				name = "End",
				type = AnimatorControllerParameterType.Int,
				defaultInt = tile.frames.Length - 1,
			};
			controller.AddParameter(startParameter);
			controller.AddParameter(endParameter);
			// The Speed parameter simply controls the animation playback speed.
			var speedParameter = new AnimatorControllerParameter {
				name = "Speed",
				type = AnimatorControllerParameterType.Float,
				defaultFloat = 1,
			};
			controller.AddParameter(speedParameter);

			var binding = new EditorCurveBinding {
				type = typeof(SpriteRenderer),
				path = "",
				propertyName = "m_Sprite",
			};

			// Each frame is defined as its own state, with individual durations and immediate
			// transitions.
			var states = new AnimatorState[tile.frames.Length];
			for (var j = 0; j < states.Length; ++j) {
				var clip = new AnimationClip();
				clip.frameRate = 1000f / tile.frames[j].duration;
				clip.name = $"{name} {j}";
				clip.hideFlags = HideFlags.HideInHierarchy;
				var keyframes = new [] {
					new ObjectReferenceKeyframe { time = 0, value = tile.frames[j].sprite }
				};
				AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
				states[j] = stateMachine.AddState($"{j}");
				states[j].motion = clip;
				states[j].speedParameter = "Speed";
				states[j].speedParameterActive = true;
				states[j].writeDefaultValues = false;
				context.AddObjectToAsset($"Clip {name} {j}", clip);
				context.AddObjectToAsset($"State {name} {j}", states[j]);
			}

			// By default, the entire animation sequence plays continuously.
			// Each frame state transitions to the next after its duration; the last frame state,
			// defined by the value of the End property, loops back to the frame defined by the
			// value of the Start property, through the restart state.
			// If the Start property is ahead of the current state, or the End property is behind
			// the current state, the Start state is immediately transitioned to, through the
			// Restart state.
			var restartState = stateMachine.AddState("Restart");
			context.AddObjectToAsset($"State Restart {name}", restartState);
			for (var j = 0; j < states.Length; ++j) {
				// Back; loop back to start if the current state is ahead of the end state.
				var transition = states[j].AddTransition(restartState);
				transition.hasExitTime = false;
				transition.duration = 0f;
				transition.interruptionSource = TransitionInterruptionSource.Destination;
				transition.AddCondition(AnimatorConditionMode.Less, j, "End");
				context.AddObjectToAsset($"Transition Back {name} {j}", transition);

				// Forward; skip ahead to the start state if the current state is behind it.
				transition = states[j].AddTransition(restartState);
				transition.hasExitTime = false;
				transition.duration = 0f;
				transition.interruptionSource = TransitionInterruptionSource.Destination;
				transition.AddCondition(AnimatorConditionMode.Greater, j, "Start");
				context.AddObjectToAsset($"Transition Forward {name} {j}", transition);

				// Replay; go back to the start state if the current state is the end state.
				transition = states[j].AddTransition(restartState);
				transition.hasExitTime = true;
				transition.exitTime = 1f;
				transition.duration = 0f;
				transition.interruptionSource = TransitionInterruptionSource.Destination;
				transition.AddCondition(AnimatorConditionMode.Equals, j, "End");
				context.AddObjectToAsset($"Transition Restart {name} {j}", transition);

				// Restart; connect the restart state back to all start states.
				transition = restartState.AddTransition(states[j]);
				transition.hasExitTime = false;
				transition.duration = 0f;
				transition.interruptionSource = TransitionInterruptionSource.Destination;
				transition.AddCondition(AnimatorConditionMode.Equals, j, "Start");
				context.AddObjectToAsset($"Transition Start {name} {j}", transition);

				// Play; keep going to the next state until the end state is reached.
				if (j < states.Length - 1) {
					transition = states[j].AddTransition(states[j+1]);
					transition.hasExitTime = true;
					transition.exitTime = 1f;
					transition.duration = 0f;
					transition.interruptionSource = TransitionInterruptionSource.Destination;
					transition.AddCondition(AnimatorConditionMode.Less, j + 1, "Start");
					transition.AddCondition(AnimatorConditionMode.Greater, j, "End");
					context.AddObjectToAsset($"Transition Play {name} {j}", transition);
				}
			}

			context.AddObjectToAsset($"Controller {name}", controller);
			context.AddObjectToAsset($"StateMachine {name}", controller.layers[0].stateMachine);

			return controller;
		}

		///<summary>
		///Apply animation state name hashes to a component's fields if they're decorated with
		///TiledAnimationAttribute. States are automatically generated based on passed in frame
		///indices.
		///</summary>
		static void ApplyAnimation(
			AssetImportContext context,
			AnimatorController controller,
			Tile.Frame[] frames,
			MonoBehaviour component
		) {
			foreach (var field in component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
				var attribute = field.GetCustomAttribute<TiledAnimationAttribute>();
				if (attribute == null) { continue; }
				var fieldName = ObjectNames.NicifyVariableName(field.Name);
				var fieldType = field.FieldType.ToString();
				if (fieldType != "System.Int32") {
					Debug.LogWarning("Expected [TiledAnimation] to decorate int field, skipping.");
					continue;
				}
				var binding = new EditorCurveBinding {
					type = typeof(SpriteRenderer),
					path = "",
					propertyName = "m_Sprite",
				};
				// Each frame is defined as its own state, with individual durations and immediate
				// transitions.
				var clip = new AnimationClip();
				clip.frameRate = 1000f;
				clip.name = $"{fieldName}";
				clip.hideFlags = HideFlags.HideInHierarchy;
				var keyframes = new ObjectReferenceKeyframe[attribute.frames.Length + 1];
				var time = 0f;
				for (var i = 0; i < keyframes.Length - 1; ++i) {
					var frame = frames[attribute.frames[i]];
					keyframes[i] = new ObjectReferenceKeyframe {
						time = time,
						value = frame.sprite,
					};
					time += frame.duration / 1000f;
				}
				keyframes[keyframes.Length - 1] = new ObjectReferenceKeyframe {
					time = time,
					value = frames[attribute.frames[attribute.frames.Length - 1]].sprite
				};
				AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
				var stateMachine = controller.layers[0].stateMachine;
				var state = stateMachine.AddState(fieldName);
				state.motion = clip;
				state.speedParameter = "Speed";
				state.speedParameterActive = true;
				state.writeDefaultValues = false;
				var transition = state.AddTransition(state);
				transition.hasExitTime = true;
				transition.exitTime = 1f;
				transition.duration = 0f;
				transition.interruptionSource = TransitionInterruptionSource.Destination;
				context.AddObjectToAsset($"Clip {fieldName}", clip);
				context.AddObjectToAsset($"State {fieldName}", state);
				context.AddObjectToAsset($"Transition {fieldName}", transition);
				field.SetValue(component, state.nameHash);
			}
		}
	}
}
