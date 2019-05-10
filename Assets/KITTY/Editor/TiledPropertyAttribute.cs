namespace KITTY {
	using System;

	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
	internal class TiledPropertyAttribute : Attribute {
		public string name;

		public TiledPropertyAttribute(string name = null) {
			this.name = name;
		}
	}
}