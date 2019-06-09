namespace KITTY {
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using UnityEditor;
	using UnityEditor.Experimental.AssetImporters;

	///<summary>
	///Temporary tilemap datastructure, used when importing TMX.
	///</summary>
	internal struct Map {
		public Layer[] layers;
		public Tile[] tiles;
		public Property[] properties;

		TMX tmx;

		public string orientation => tmx.orientation;
		public int height => tmx.height;
		public int tilewidth => tmx.tilewidth;
		public int tileheight => tmx.tileheight;

		///<summary>
		///Parse layer data and instantiate tileset tiles from a given TMX.
		///</summary>
		public Map(AssetImportContext context, TMX tmx) {
			layers = ParseLayers(tmx.orientation, tmx.layers);
			tiles = ParseTilesets(context, tmx.tilesets, tmx.tilewidth);
			properties = tmx.properties;
			this.tmx = tmx;
		}

		///<summary>
		///Build an array of layers, each an array of chunks, each an array of global IDs.
		///</summary>
		static Layer[] ParseLayers(string orientation, TMX.Layer[] tmxLayers) {
			var layers = new Layer[tmxLayers.Length];
			for (var i = 0; i < layers.Length; ++i) {
				var tmxLayer = tmxLayers[i];
				layers[i].name       = tmxLayer.name;
				layers[i].opacity    = tmxLayer.opacity;
				layers[i].width      = tmxLayer.width;
				layers[i].height     = tmxLayer.height;
				layers[i].properties = tmxLayer.properties;
				layers[i].objects    = tmxLayer.objects;
				layers[i].chunks     = new Layer.Chunk[tmxLayer.data.chunks.Length];
				for (var j = 0; j < layers[i].chunks.Length; ++j) {
					var chunk = tmxLayer.data.chunks[j];
					layers[i].chunks[j] = ParseChunk(
						tmxLayer.data.encoding,
						tmxLayer.data.compression,
						chunk.value,
						chunk.width,
						orientation
					);
					layers[i].chunks[j].width  = chunk.width;
					layers[i].chunks[j].height = chunk.height;
					layers[i].chunks[j].x      = chunk.x;
					layers[i].chunks[j].y      = chunk.y;
				}
			}
			return layers;
		}

		///<summary>
		///Decode, decompress, and reorder rows of global tile IDs
		///</summary>
		static Layer.Chunk ParseChunk(
			string encoding,
			string compression,
			string data,
			int width,
			string orientation
		) {
			// Decode
			byte[] input;
			switch (encoding) {
				case "base64": input = Convert.FromBase64String(data); break;
				default: throw new NotImplementedException("Encoding: " + (encoding ?? "xml"));
			}

			// Decompress
			byte[] output;
			switch (compression) {
				case null:   output = input;             break;
				case "gzip": output = CompressionHelper.DecompressGZip(input); break;
				case "zlib": output = CompressionHelper.DecompressZlib(input); break;
				default: throw new NotImplementedException("Compression: " + compression);
			}

			// Parse bytes as uint32 global IDs, reordered according to cell layout.
			var gids = new uint[output.Length / 4];
			Buffer.BlockCopy(output, 0, gids, 0, output.Length);
			switch (orientation) {
				case "orthogonal": return new Layer.Chunk {
					gids = ArrayHelper.Reverse(gids, stride: width)
				};
				case "isometric": return new Layer.Chunk {
					gids = ArrayHelper.Swizzle(gids, stride: width).Reverse().ToArray()
				};
				case "hexagonal": return new Layer.Chunk { gids = gids };
				default: throw new NotImplementedException($"Orientation: {orientation}");
			}
		}

		///<summary>
		///Load tiles from all tilesets, including sprites and frames, if any.
		///</summary>
		static Tile[] ParseTilesets(AssetImportContext context, TSX[] tilesets, int tilewidth) {
			var tiles = new List<Tile> { null }; // Global IDs start from 1
			foreach (var tsx in tilesets) {
				var tileset = ParseTileset(context, tsx);
				while (tiles.Count < tsx.firstgid) {
					tiles.Add(null);
				}
				for (var i = 0; i < tileset.tiles.Length; ++i) {
					var gid = tsx.firstgid + i;
					var tile = tileset.tiles[i].Instantiate(tilewidth);
					tiles.Add(tile);
					if (!tile) { continue; }
					context.AddObjectToAsset($"Tile {gid}", tile);
					if (!tile.sprite) { continue; }
					context.AddObjectToAsset($"Sprite {gid}", tile.sprite);
					for (var j = 0; j < tile.frames.Length; ++j) {
						context.AddObjectToAsset($"Frame {gid} {j}", tile.frames[j].sprite);
					}
				}
			}
			return tiles.ToArray();
		}

		///<summary>
		///Load embedded or external tileset.
		///</summary>
		static Tileset ParseTileset(AssetImportContext context, TSX tsx) {
			// Load embedded tileset.
			if (tsx.source == null) {
				return TSXImporter.Load(context, tsx);
			}

			// Load external tileset, respecting relative paths.
			var source = PathHelper.AssetPath(
				Path.GetDirectoryName(context.assetPath) +
				Path.DirectorySeparatorChar +
				tsx.source
			);
			context.DependsOnSourceAsset(source);
			return AssetDatabase.LoadAssetAtPath<Tileset>(source);
		}

		///<summary>
		///Layer structure for importing a TMX layer as a tilemap or a group of objects.
		///</summary>
		public struct Layer {
			public string name;
			public float opacity;
			public int width;
			public int height;
			public Property[] properties;
			public Chunk[] chunks;
			public TMX.Layer.Object[] objects;

			///<summary>
			///Layer chunk containing the global tile IDs, used by Tiled only for infinite maps, but
			///used here for fixed maps as well, for ease of use.
			///</summary>
			public struct Chunk {
				public int width;
				public int height;
				public int x;
				public int y;
				public uint[] gids;
			}
		}
	}
}