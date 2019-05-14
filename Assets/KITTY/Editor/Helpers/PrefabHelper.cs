namespace KITTY {
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using UnityEditor;
	using UnityEditor.Experimental.AssetImporters;
	using UnityEngine;

	///<summary>Helper method for loading prefabs based on name and type.</summary>
	internal static class PrefabHelper {
        public static Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

		///<summary>Load first prefab with `name`.</summary>
        public static GameObject Load(string name, AssetImportContext context) {
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
                select new { prefab, path }
            ).FirstOrDefault();
            cache.Add(name, asset?.prefab);
			if (!string.IsNullOrEmpty(asset?.path)) {
				context?.DependsOnSourceAsset(asset.path);
			} else {
				Debug.LogWarning($"No prefab named \"{name}\" could be found, skipping.");
			}
            return asset?.prefab;
        }
	}
}
