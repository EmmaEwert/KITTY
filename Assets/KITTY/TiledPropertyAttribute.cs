namespace KITTY {
	using System;

	///<summary>
	///Attribute indicating that a field should be assigned from a Tiled custom property, if any.
	///</summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
	public class TiledPropertyAttribute : Attribute {
		public string name;

		///<summary>
		///Assign from Tiled custom property, optionally with a different `name`.
		///</summary>
		public TiledPropertyAttribute(string name = null) {
			this.name = name;
		}
	}
}