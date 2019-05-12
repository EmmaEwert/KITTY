namespace KITTY {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using UnityEngine;

	internal partial class Tileset : ScriptableObject {
		///<summary>Representation of a single sprite in a Tiled tileset.</summary>
		[Serializable]
		public struct Sprite : ISerializationCallbackReceiver {
			public Tileset tileset;
			public Rect rect;
			public Texture2D texture;
			[NonSerialized] public Vector2[][] shapes;
			[HideInInspector, SerializeField] Shape[] _shapes;

			public UnityEngine.Sprite Instantiate(float pixelsPerUnit) {
				var sprite = UnityEngine.Sprite.Create(
					texture,
					rect,
					tileset.spritePivot,
					pixelsPerUnit,
					extrude: 0,
					SpriteMeshType.FullRect,
					border: Vector4.zero,
					generateFallbackPhysicsShape: false
				);
				sprite.hideFlags = HideFlags.HideInHierarchy;
				sprite.name = "Sprite";
				if (shapes?.Length > 0) {
					sprite.OverridePhysicsShape((IList<Vector2[]>)shapes.ToList());
				}
				return sprite;
			}

			public void OnAfterDeserialize() {
				if (_shapes == null) { return; }
				shapes = new Vector2[_shapes.Length][];
				for (var i = 0; i < _shapes.Length; ++i) {
					shapes[i] = _shapes[i].points;
				}
			}

			public void OnBeforeSerialize() {
				if (shapes == null) { return; }
				_shapes = shapes.Select(s => new Shape { points = s }).ToArray();
			}

			[Serializable]
			struct Shape {
				public Vector2[] points;
			}
		}
	}
}

