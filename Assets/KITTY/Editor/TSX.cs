namespace KITTY {
	using System.Linq;
	using System.Xml.Linq;

	///<summary>
	///Direct C# representation of a Tiled TSX tileset.
	///</summary>
	internal struct TSX {
		// Attributes
		public int firstgid;
		public string source;
		public string name;
		public int tilewidth;
		public int tileheight;
		public int spacing;
		public int margin;
		public int tilecount;
		public int columns;

		// Elements
		public Tileoffset tileoffset;
		public Image image;
		public Tile[] tiles;

		public TSX(XElement element, string assetPath = "") {
			// Attributes
			firstgid    = (int?  )element.Attribute("firstgid") ?? 0;
			source      = (string)element.Attribute("source");
			if (!string.IsNullOrEmpty(source)) { // External tileset
				var tsx = new TSX(XDocument.Load(assetPath + source).Element("tileset"));
				name = tsx.name;
				tilewidth = tsx.tilewidth;
				tileheight = tsx.tileheight;
				spacing = tsx.spacing;
				margin = tsx.margin;
				tilecount = tsx.tilecount;
				columns = tsx.columns;
				tileoffset = tsx.tileoffset;
				image = tsx.image;
				tiles = tsx.tiles;
				return;
			}
			name        = (string)element.Attribute("name");
			tilewidth   = (int   )element.Attribute("tilewidth");
			tileheight  = (int   )element.Attribute("tileheight");
			spacing     = (int?  )element.Attribute("spacing") ?? 0;
			margin      = (int?  )element.Attribute("margin") ?? 0;
			tilecount   = (int?  )element.Attribute("tilecount") ?? 0;
			columns     = (int?  )element.Attribute("columns") ?? 0;

			// Elements
			tileoffset = new Tileoffset(element.Element("tileoffset"));
			image = new Image(element.Element("image"));
			tiles = element
				.Elements("tile")
				.Select(t => new Tile(t))
				.OrderBy(t => t.id)
				.ToArray();
		}

		public struct Tileoffset {
			public int x;
			public int y;

			public Tileoffset(XElement element) {
				x = (int?)element?.Attribute("x") ?? 0;
				y = (int?)element?.Attribute("y") ?? 0;
			}
		}

		public struct Image {
			public string source;
			public string trans;

			public Image(XElement element) {
				source = (string)element?.Attribute("source");
				trans  = (string)element?.Attribute("trans");
			}
		}

		public struct Tile {
			public int id;
			public string type;
			public Image image;

			public Object[] objects;
			public Frame[] frames;
			public Property[] properties;

			public Tile(XElement element) {
				id = (int)element.Attribute("id");
				type = (string)element.Attribute("type");
				image = new Image(element.Element("image"));
				objects = element
					.Element("objectgroup")
					?.Elements("object")
					.Select(@object => new Object(@object))
					.ToArray();
				frames = element
					.Element("animation")
					?.Elements("frame")
					.Select(frame => new Frame(frame))
					.ToArray();
				properties = element
					.Elements("properties")
					?.Elements("property")
					.Select(p => new Property {
						name = (string)p.Attribute("name"),
						type = (string)p.Attribute("type") ?? "string",
						value = (string)p.Attribute("value") ?? p.Value,
					})
					.ToArray();
			}

			public struct Object {
				public int id;
				public string name;
				public string type;
				public float x;
				public float y;
				public float width;
				public float height;
				public float rotation;
				public string points;

				public Object(XElement element) {
					id       = (int   )element.Attribute("id");
					name     = (string)element.Attribute("name");
					type     = (string)element.Attribute("type");
					x        = (float )element.Attribute("x");
					y        = (float )element.Attribute("y");
					width    = (float?)element.Attribute("width") ?? 0;
					height   = (float?)element.Attribute("height") ?? 0;
					rotation = (float?)element.Attribute("rotation") ?? 0;
					points   = (string)element.Element("polygon")?.Attribute("points");
				}
			}

			public struct Frame {
				public int tileid;
				public int duration;

				public Frame(XElement element) {
					tileid   = (int?)element?.Attribute("tileid") ?? 0;
					duration = (int?)element?.Attribute("duration") ?? 0;
				}
			}
		}
	}
}