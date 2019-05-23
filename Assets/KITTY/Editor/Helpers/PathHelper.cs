namespace KITTY {
	using System.IO;
	using UnityEngine;

	public static class PathHelper {
		public static string AssetDirectory(string assetPath) => (
			"Assets" 
			+ Path.GetFullPath(Path.GetDirectoryName(assetPath))
			+ Path.DirectorySeparatorChar
		).Substring(Application.dataPath.Length);

		public static string AssetPath(string relativeAssetPath) => (
			"Assets"
			+ Path.GetFullPath(relativeAssetPath)
		).Substring(Application.dataPath.Length);
	}
}