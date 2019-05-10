namespace KITTY {
	using UnityEngine;

	///<summary>Representation of a Tiled tileset.</summary>
	internal partial class Tileset : ScriptableObject {
		public int tilewidth;
		public int tileheight;
		public int spacing;
		public int margin;
		public int tilecount;
		public int columns;

		public Vector2Int tileoffset;
		public string imageSource;

		public Texture2D texture;
		public Sprite[] sprites;
		public Tile[] tiles;

		public Vector2 spritePivot;
	}
}