namespace KITTY {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using UnityEngine;

	///<summary>
	///Asset based on a Tiled tileset.
	///</summary>
	internal class Tileset : ScriptableObject {
		public Tile[] tiles;

		///<summary>
		///Structure based on a single tile in a Tiled tileset. Handles actual Tile instantiation.
		///</summary>
		[Serializable]
		public struct Tile {
			public Texture2D texture;
			public Rect rect;
			public Vector2 pivot;
			public GameObject prefab;
			[HideInInspector] public Object[] objects;
			[HideInInspector] public Frame[] frames;
			[HideInInspector] public Property[] properties;

			///<summary>
			///Instantiate an actual Tile based on this tileset tile.
			///</summary>
			public KITTY.Tile Instantiate(float pixelsPerUnit) {
				var tile = ScriptableObject.CreateInstance<KITTY.Tile>();
				tile.sprite = Sprite.Create(
					texture,
					rect,
					pivot,
					pixelsPerUnit,
					extrude: 0,
					SpriteMeshType.FullRect,
					border: Vector4.zero,
					generateFallbackPhysicsShape: false
				);
				tile.color = Color.white;
				tile.prefab = prefab;
				tile.hideFlags = HideFlags.HideInHierarchy;
				tile.name = "Tile";
				tile.transform = Matrix4x4.identity;
				if (tile.sprite) {
					tile.sprite.hideFlags = HideFlags.HideInHierarchy;
					tile.sprite.name = "Sprite";
				}

				// Animation frames.
				// TODO: Support image collection tileset animation
				if (frames.Length > 0) {
					var sprites = new Sprite[frames.Length];
					var tileFrames = new List<KITTY.Tile.Frame>();
					for (var i = 0; i < sprites.Length; ++i) {
						sprites[i] = Sprite.Create(
							frames[i].texture,
							frames[i].rect,
							pivot,
							pixelsPerUnit,
							extrude: 0,
							SpriteMeshType.FullRect,
							border: Vector4.zero,
							generateFallbackPhysicsShape: false
						);
						sprites[i].hideFlags = HideFlags.HideInHierarchy;
						tileFrames.Add(new KITTY.Tile.Frame {
							sprite = sprites[i],
							duration = frames[i].duration
						});
					}
					tile.frames = tileFrames.ToArray();
				} else {
					tile.frames = new KITTY.Tile.Frame[0];
				}

				// Collision shapes.
				// TODO: Grid collidertype when the only collider is a single full rect
				if (objects?.Length > 0) {
					tile.sprite?.OverridePhysicsShape(objects.Select(s => s.points).ToList());
					tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.Sprite;
				} else {
					tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
				}

				tile.properties = properties ?? new Property[0];
				return tile;
			}

			///<summary>
			///A single collision shape polygon for a tile, defined by an array of 2D points.
			///</summary>
			[Serializable]
			public struct Object {
				public Vector2[] points;
			}

			///<summary>
			///A single animation frame for a tile, defined by its `rect` in the tileset image.
			///</summary>
			[Serializable]
			public struct Frame {
				public Texture2D texture;
				public Rect rect;
				public int duration;
			}
		}
	}
}