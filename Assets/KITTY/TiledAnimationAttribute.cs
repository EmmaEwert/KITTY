namespace KITTY {
	using System;

	///<summary>
	///Attribute indicating that a field should be assigned a state hash from a generated
	///animation state.
	///</summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
	public class TiledAnimationAttribute : Attribute {
		public int[] frames;

		///<summary>
		///Assign state hash from Tiled animation sequence.
		///</summary>
		public TiledAnimationAttribute(params int[] frames) {
			this.frames = frames;
		}
	}
}
