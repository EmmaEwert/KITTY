namespace KITTY {
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using UnityEditor;
	using UnityEditor.Experimental.AssetImporters;
	using UnityEngine;

	///<summary>
	///Helper method for loading prefabs based on name.
	///
	///Note: PrefabHelper.cache must be cleared manually on reimport.
	///</summary>
	internal static class PrefabHelper {
        public static Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

		///<summary>
		///Load prefab with `name`. If multiple prefabs exist, the most relevant one is selected
		///based on the path prefix similarity to the source asset path.
		///</summary>
		// TODO: Heuristic for relevance in case of multiple results.
        public static GameObject Load(string name, AssetImportContext context, bool warn = true) {
            if (string.IsNullOrEmpty(name)) { return null; }
            if (cache.TryGetValue(name, out var cachedPrefab)) {
                return cachedPrefab;
            }
            var filename = name + ".prefab";
            var asset = (
                from guid in AssetDatabase.FindAssets($"{name} t:gameobject")
                let path = AssetDatabase.GUIDToAssetPath(guid)
                where Path.GetFileName(path) == filename
                let prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                where prefab != null
				orderby PrefixMatch(path, context.assetPath) descending
                select new { prefab, path }
            ).FirstOrDefault();
            cache.Add(name, asset?.prefab);
			if (warn && string.IsNullOrEmpty(asset?.path)) {
				Debug.LogWarning($"No prefab named \"{name}\" could be found, skipping.");
			}
            return asset?.prefab;
        }

		///<summary>
		///Compare two strings, returning the number of matching prefix characters.
		///</summary>
		static int PrefixMatch(string a, string b) {
			var match = 0;
			for (var i = 0; i < a.Length; ++i) {
				if (b.Length < i - 1 || a[i] != b[i]) { return match; }
				++match;
			}
			return match;
		}
	}
}
