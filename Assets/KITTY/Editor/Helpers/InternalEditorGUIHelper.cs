namespace KITTY {
	using System.Reflection;
	using UnityEditor;
	using UnityEngine;

	///<summary>
	///Access to inaccessible `EditorGUIUtility` methods
	///</summary>
	internal static class InternalEditorGUIHelper {
		///<summary>
		///Wrapper for Unity internal `EditorGUIUtility.SetIconForObject`, used for setting a
		///built-in icon for a GameObject.
		///</summary>
		public static void SetIconForObject(GameObject gameObject, Texture texture) {
			typeof(EditorGUIUtility).InvokeMember(
				"SetIconForObject",
				BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.NonPublic,
				null, null,
				new object[] { gameObject, texture }
			);
		}
	}
}