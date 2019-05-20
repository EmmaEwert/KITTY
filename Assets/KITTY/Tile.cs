namespace KITTY {
	using System;
	using System.Reflection;
	using UnityEngine;
	using UnityEngine.Tilemaps;

	[Serializable]
	public class Tile : UnityEngine.Tilemaps.Tile {
		public Sprite[] sprites;
		public int duration;
		public Property[] properties;

		public override bool StartUp(Vector3Int position, ITilemap tilemap, GameObject gameObject) {
			if (gameObject) {
				gameObject.name = gameObject.name.Substring(0, gameObject.name.Length - 7);
				gameObject.name += $" {position.x},{position.y}";
				gameObject.transform.localPosition -= new Vector3(0.5f, 0.5f);
				foreach (var component in gameObject.GetComponentsInChildren<MonoBehaviour>()) {
					ApplyProperties(properties, component);
				}
			}
			return true;
		}

		public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData data) {
			if (sprites == null || sprites.Length == 0) { return false; }
			data.animatedSprites = sprites;
			data.animationSpeed = 1000f / duration;
			data.animationStartTime = 0f;
			return true;
		}

		void ApplyProperties(Property[] properties, MonoBehaviour component) {
			foreach (var field in component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
				var attribute = field.GetCustomAttribute<TiledPropertyAttribute>();
				if (attribute == null) { continue; }
				var fieldName = attribute.name ?? field.Name.ToLower();
				var fieldType = field.FieldType.ToString();
				switch (fieldType) {
					case "System.String":  fieldType = "string"; break;
					case "System.Int32":   fieldType = "int";    break;
					case "System.Single":  fieldType = "float";  break;
					case "System.Boolean": fieldType = "bool";   break;
				}
				foreach (var property in properties) {
					if (property.name.ToLower().Replace(" ", "") == fieldName && property.type == fieldType) {
						switch (property.type) {
							case "string": field.SetValue(component,             property.value);  break;
							case "int":    field.SetValue(component,   int.Parse(property.value)); break;
							case "float":  field.SetValue(component, float.Parse(property.value)); break;
							case "bool":   field.SetValue(component,  bool.Parse(property.value)); break;
						}
					}
				}
			}
		}

		[Serializable]
		public struct Property {
			public string name;
			public string type;
			public string value;
		}
	}
}