namespace KITTY {
	using System;
	using System.Collections.Generic;
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
			[HideInInspector] public Frame[] frames;

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
				tile.gameObject = prefab;
				tile.hideFlags = HideFlags.HideInHierarchy;
				tile.name = "Tile";
				tile.transform = Matrix4x4.identity;
				tile.sprite.hideFlags = HideFlags.HideInHierarchy;
				tile.sprite.name = "Sprite";
				if (frames.Length > 0) {
					var sprites = new Sprite[frames.Length];
					// TODO: Support image collection tileset animation
					for (var i = 0; i < sprites.Length; ++i) {
						sprites[i] = Sprite.Create(
							texture,
							frames[i].rect,
							pivot,
							pixelsPerUnit,
							extrude: 0,
							SpriteMeshType.FullRect,
							border: Vector4.zero,
							generateFallbackPhysicsShape: false
						);
						sprites[i].hideFlags = HideFlags.HideInHierarchy;
					}
					var duration = GreatestCommonDivisor(frames.Select(f => f.duration).ToArray());
					var tileSprites = new List<Sprite>();
					for (var i = 0; i < sprites.Length; ++i) {
						for (var j = 0; j < frames[i].duration / duration; ++j) {
							tileSprites.Add(sprites[i]);
						}
					}
					tile.sprites = tileSprites.ToArray();
					tile.duration = duration;
				} else {
					tile.sprites = new Sprite[0];
				}
				if (objects?.Length > 0) {
					tile.sprite.OverridePhysicsShape(objects.Select(s => s.points).ToList());
					// TODO: Grid collidertype when the collider is a single full rect
					tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.Sprite;
				} else {
					tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
				}
				return tile;
			}

			[Serializable]
			public struct Object {
				public Vector2[] points;
			}

			[Serializable]
			public struct Frame {
				public Rect rect;
				public int duration;
			}

			int GreatestCommonDivisor(int[] ns) {
				var result = ns[0];
				for (var i = 1; i < ns.Length; ++i) {
					result = GreatestCommonDivisor(ns[i], result);
				}
				return result;
			}

			int GreatestCommonDivisor(int a, int b) => a == 0 ? b : GreatestCommonDivisor(b % a, a);
		}
	}
}