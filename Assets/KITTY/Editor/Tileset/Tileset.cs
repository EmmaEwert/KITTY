namespace KITTY {
	using System;
	using System.Linq;
	using UnityEngine;

	///<summary>Asset based on Tiled tileset.</summary>
	internal class Tileset : ScriptableObject {
		public Tile[] tiles;

		///<summary>Representation of a single tile in a Tiled tileset.</summary>
		[Serializable]
		public struct Tile {
			public Texture2D texture;
			public Rect rect;
			public Vector2 pivot;
			public GameObject prefab;
			[NonSerialized] public Vector2[][] shapes;
			[HideInInspector, SerializeField] Shape[] _shapes;

			public UnityEngine.Tilemaps.Tile Instantiate(float pixelsPerUnit) {
				var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
				tile.sprite = UnityEngine.Sprite.Create(
					texture,
					rect,
					pivot,
					pixelsPerUnit,
					extrude: 0,
					SpriteMeshType.FullRect,
					border: Vector4.zero,
					generateFallbackPhysicsShape: false
				);
				tile.sprite.hideFlags = HideFlags.HideInHierarchy;
				tile.sprite.name = "Sprite";
				if (shapes?.Length > 0) {
					tile.sprite.OverridePhysicsShape(shapes.ToList());
				}

				if (tile.sprite.GetPhysicsShapeCount() > 0) {
					// TODO: Grid collidertype when the collider is a single full rect
					tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.Sprite;
				} else {
					tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
				}
				tile.color = Color.white;
				tile.gameObject = prefab;
				tile.hideFlags = HideFlags.HideInHierarchy;
				tile.name = "Tile";
				//tile.sprite = sprite;
				tile.transform = Matrix4x4.identity;
				return tile;
			}

			public void OnAfterDeserialize() {
				if (_shapes == null) { return; }
				shapes = new Vector2[_shapes.Length][];
				for (var i = 0; i < _shapes.Length; ++i) {
					shapes[i] = _shapes[i].points;
				}
			}

			public void OnBeforeSerialize() {
				if (shapes == null) { return; }
				_shapes = shapes.Select(s => new Shape { points = s }).ToArray();
			}

			[Serializable]
			struct Shape {
				public Vector2[] points;
			}
		}
	}
}