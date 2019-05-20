namespace KITTY {
	using System;
	using UnityEngine;
	using UnityEngine.Tilemaps;

	[Serializable]
	public class Tile : UnityEngine.Tilemaps.Tile {
		public Sprite[] sprites;
		public int duration;

		public override bool StartUp(Vector3Int position, ITilemap tilemap, GameObject gameObject) {
			if (gameObject) {
				gameObject.name = gameObject.name.Substring(0, gameObject.name.Length - 7);
				gameObject.name += $" {position.x},{position.y}";
				gameObject.transform.localPosition -= new Vector3(0.5f, 0.5f);
			}
			return true;
		}

		public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData data) {
			if (sprites == null || sprites.Length == 0) { return false; }
			data.animatedSprites = sprites;
			data.animationSpeed = 1000f / duration;
			data.animationStartTime = 0f;
			return true;
		}
	}
}