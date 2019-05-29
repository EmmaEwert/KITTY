namespace KITTY {
	using System.IO;
	using UnityEngine;

	///<summary>
	///Miscellaneous Unity-specific filesystem path helper methods.
	///</summary>
	public static class PathHelper {
		///<summary>
		///Application-relative asset directory, respecting `../`.
		///</summary>
		public static string AssetDirectory(string assetPath) => (
			"Assets" 
			+ Path.GetFullPath(Path.GetDirectoryName(assetPath))
			+ Path.DirectorySeparatorChar
		).Substring(Application.dataPath.Length);

		///<summary>
		///Application-relative asset path, respecting `../`.
		///</summary>
		public static string AssetPath(string assetPath) => (
			"Assets"
			+ Path.GetFullPath(assetPath)
		).Substring(Application.dataPath.Length);
	}
}