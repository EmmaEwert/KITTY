namespace KITTY {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using UnityEngine;
	using UnityEngine.Tilemaps;

	[Serializable]
	public class Tile : UnityEngine.Tilemaps.Tile {
		public Frame[] frames;
		public Property[] properties;
		public GameObject prefab;

		public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData data) {
			if (frames == null || frames.Length == 0) { return false; }
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

		[Serializable]
		public struct Frame {
			public Sprite sprite;
			public int duration;
		}
	}
}