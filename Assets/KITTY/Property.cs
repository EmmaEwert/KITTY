namespace KITTY {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using UnityEngine;

	[Serializable]
	public struct Property {
		public string name;
		public string type;
		public string value;

		public static Property[] Merge(Property[] primary, Property[] secondary) {
			var properties = new List<Property>();
			for (var i = 0; i < secondary?.Length; ++i) {
				if (!primary.Any(p => p.name == secondary[i].name)) {
					properties.Add(secondary[i]);
				}
			}
			properties.AddRange(primary);
			return properties.ToArray();
		}

		public static void Apply(Property[] properties, MonoBehaviour component) {
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
	}
}