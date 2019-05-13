namespace KITTY {
	using System;
	using System.Linq;
	using UnityEngine;

	///<summary>Asset based on a Tiled tileset.</summary>
	internal class Tileset : ScriptableObject {
		public Tile[] tiles;

		///<summary>Asset based on a single tile in a Tiled tileset.</summary>
		[Serializable]
		public struct Tile {
			public Texture2D texture;
			public Rect rect;
			public Vector2 pivot;
			public GameObject prefab;
			[HideInInspector] public Object[] objects;

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
				if (objects?.Length > 0) {
					tile.sprite.OverridePhysicsShape(objects.Select(s => s.points).ToList());
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
				tile.transform = Matrix4x4.identity;
				return tile;
			}

			[Serializable]
			public struct Object {
				public Vector2[] points;
			}
		}
	}
}