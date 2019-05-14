namespace KITTY {
	using System;
	using UnityEngine;
	using UnityEngine.Tilemaps;

	[Serializable]
	public class Tile : UnityEngine.Tilemaps.Tile {
		public Sprite[] sprites;
		public int duration;

		public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData data) {
			if (sprites == null || sprites.Length == 0) { return false; }
			data.animatedSprites = sprites;
			data.animationSpeed = 1000f / duration;
			data.animationStartTime = 0f;
			return true;
		}
	}
}