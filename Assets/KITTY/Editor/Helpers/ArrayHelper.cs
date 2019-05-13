namespace KITTY {
	///<summary>Miscellaneous `Array` helper methods</summary>
	internal static class ArrayHelper {
		///<summary>Reverse the row order in a 2D block of elements, assuming width `stride`.</summary>
		public static T[] Reverse<T>(T[] input, int stride) {
			var size = input.Length / stride;
			var output = new T[input.Length];
			for (var row = 0; row < size; ++row) {
				for (var col = 0; col < stride; ++col) {
					var i = row * stride + col;
					var j = (size - row - 1) * stride + col;
					output[i] = input[j];
				}
			}
			return output;
		}

		public static T[] Swizzle<T>(T[] input, int stride) {
			var size = input.Length / stride;
			var output = new T[input.Length];
			for (var row = 0; row < size; ++row) {
				for (var col = 0; col < stride; ++col) {
					var i = col * stride + row;
					var j = row * stride + col;
					output[i] = input[j];
				}
			}
			return output;
		}
	}
}