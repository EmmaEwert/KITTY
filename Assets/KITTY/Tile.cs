namespace KITTY {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using UnityEngine;
	using UnityEngine.Tilemaps;

	///<summary>
	///Tile asset inheriting from Unity's built-in tile, with support for variable-framerate
	///animation, as well as external prefab instantiation and property application.
	///</summary>
	[Serializable]
	public class Tile : UnityEngine.Tilemaps.Tile {
		public Frame[] frames;
		public Property[] properties;
		public GameObject prefab;

		///<summary>
		///Tile animation `data` expects its `animatedSprites` field populated by a list of sprites
		///to be cycled through at a constant framerate.
		///</summary>
		public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData data) {
			if (frames == null || frames.Length == 0) { return false; }
			// Tiled supports individual frame durations per frame, but Unity doesn't. This is
			// solved by finding the greatest divisor between all frame durations, using that for
			// the fixed framerate, and replicating each frame enough times to match its original
			// duration.
			var duration = GreatestCommonDivisor(frames.Select(f => f.duration).ToArray());
			var sprites = new List<Sprite>();
			for (var i = 0; i < frames.Length; ++i) {
				for (var j = 0; j < frames[i].duration / duration; ++j) {
					sprites.Add(frames[i].sprite);
				}
			}
			data.animatedSprites = sprites.ToArray();
			data.animationSpeed = 1000f / duration;
			data.animationStartTime = 0f;
			return true;
		}

		int GreatestCommonDivisor(int[] ns) {
			var result = ns[0];
			for (var i = 1; i < ns.Length; ++i) {
				result = GreatestCommonDivisor(ns[i], result);
			}
			return result;
		}

		int GreatestCommonDivisor(int a, int b) => a == 0 ? b : GreatestCommonDivisor(b % a, a);

		///<summary>
		///A single animation frame for a tile, defined by its individual sprite and duration.
		///</summary>
		[Serializable]
		public struct Frame {
			public Sprite sprite;
			public int duration;
		}
	}
}