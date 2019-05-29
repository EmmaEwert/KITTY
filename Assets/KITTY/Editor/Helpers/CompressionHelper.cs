namespace KITTY {
	using System;
	using System.IO;
	using System.IO.Compression;

	///<summary>
	///Miscellaneous helper methods for compression.
	///</summary>
	internal static class CompressionHelper {
		public static byte[] DecompressGZip(byte[] input) {
			using (var inStream = new GZipStream(new MemoryStream(input), CompressionMode.Decompress)) {
				var length = BitConverter.ToInt32(input, input.Length - 4);
				var buffer = new byte[length];
				var outStream = new MemoryStream();
				inStream.Read(buffer, 0, length);
				outStream.Write(buffer, 0, length);
				return outStream.ToArray();
			}
		}

		public static byte[] DecompressZlib(byte[] input) {
			var stream = new MemoryStream(input, index: 2, input.Length - 2);
			using (var inStream = new DeflateStream(stream, CompressionMode.Decompress)) {
				var outStream = new MemoryStream();
				var bufferSize = 65536;
				var buffer = new byte[bufferSize];
				var count = 0;
				while ((count = inStream.Read(buffer, offset: 0, bufferSize)) > 0) {
					outStream.Write(buffer, offset: 0, count);
				}
				return outStream.ToArray();
			}
		}
	}
}