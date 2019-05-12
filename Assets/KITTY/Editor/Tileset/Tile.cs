namespace KITTY {
	using System;
	using UnityEngine;

	internal partial class Tileset : ScriptableObject {
		///<summary>Representation of a single tile in a Tiled tileset.</summary>
		[Serializable]
		public struct Tile {
			public uint id;
			public GameObject gameObject;

			public UnityEngine.Tilemaps.Tile Instantiate(UnityEngine.Sprite sprite) {
				var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
				if (sprite.GetPhysicsShapeCount() > 0) {
					// TODO: Grid collidertype when the collider is a single full rect
					tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.Sprite;
				} else {
					tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
				}
				tile.color = Color.white;
				tile.gameObject = gameObject;
				tile.hideFlags = HideFlags.HideInHierarchy;
				tile.name = "Tile";
				tile.sprite = sprite;
				tile.transform = Matrix4x4.identity;
				return tile;
			}
		}
	}
}

