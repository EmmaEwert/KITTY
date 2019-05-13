namespace KITTY {
	using System.Linq;
	using System.Xml.Linq;

	internal struct TMX {
		// Attributes
		public string orientation;
		public int width;
		public int height;
		public int tilewidth;
		public int tileheight;
		public bool infinite;

		// Elements
		public TSX[] tilesets;
		public Layer[] layers;

		public TMX(XElement element, string assetPath) {
			// Attributes
			orientation = (string)element.Attribute("orientation");
			width       = (int   )element.Attribute("width");
			height 	    = (int   )element.Attribute("height");
			tilewidth   = (int   )element.Attribute("tilewidth");
			tileheight  = (int   )element.Attribute("tileheight");
			infinite    = ((int? )element.Attribute("infinite") ?? 0) != 0;

			// Elements
			tilesets = element
				.Elements("tileset")
				.Select(tsx => new TSX(tsx, assetPath)).ToArray();
			layers = element
				.Elements()
				.Where(e => e.Name == "layer" || e.Name == "objectgroup")
				.Select(l => new Layer(l))
				.ToArray();
		}

		public struct Layer {
			public int id;
			public string name;
			// Tile layer
			public int width;
			public int height;
			public float opacity;
			public bool visible;
			public float offsetx;
			public float offsety;
			public Data data;
			// Object layer
			public Object[] objects;

			public Layer(XElement element) {
				id      = (int?  )element.Attribute("id") ?? 0;
				name    = (string)element.Attribute("name");
				width   = (int?  )element.Attribute("width") ?? 0;
				height  = (int?  )element.Attribute("height") ?? 0;
				opacity = (float?)element.Attribute("opacity") ?? 1f;
				visible = ((int? )element.Attribute("visible") ?? 1) == 1;
				offsetx = (float?)element.Attribute("offsetx") ?? 0;
				offsety = (float?)element.Attribute("offsety") ?? 0;
				data = new Data(element.Element("data"));
				objects = element
					.Elements("object")
					.Select(o => new Object(o))
					.ToArray();
			}

			public struct Data {
				public string encoding;
				public string compression;
				public string value;

				public Data(XElement element) {
					encoding = (string)element?.Attribute("encoding");
					compression = (string)element?.Attribute("compression");
					value = element?.Value;
				}
			}

			public struct Object {
				public int id;
				public string name;
				public string type;
				public uint gid;
				public float x;
				public float y;
				public float width;
				public float height;
				public float rotation;

				public Property[] properties;

				public Object(XElement element) {
					id       = (int   )element.Attribute("id");
					name     = (string)element.Attribute("name");
					type     = (string)element.Attribute("type");
					gid      = (uint? )element.Attribute("gid") ?? 0;
					x        = (float )element.Attribute("x");
					y        = (float )element.Attribute("y");
					width    = (float?)element.Attribute("width") ?? 0;
					height   = (float?)element.Attribute("height") ?? 0;
					rotation = (float?)element.Attribute("rotation") ?? 0;
					properties = element
						.Elements("properties")
						?.Elements("property")
						.Select(p => new Property(p))
						.ToArray();
				}

				public struct Property {
					public string name;
					public string type;
					public string value;

					public Property(XElement element) {
						name = (string)element.Attribute("name");
						type = (string)element.Attribute("type") ?? "string";
						value = (string)element.Attribute("value") ?? element.Value;
					}
				}
			}
		}
	}
}