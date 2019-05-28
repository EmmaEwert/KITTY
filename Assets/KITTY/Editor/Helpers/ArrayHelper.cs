namespace KITTY {
	///<summary>
	///Miscellaneous `Array` helper methods
	///</summary>
	internal static class ArrayHelper {
		///<summary>
		///Reverse the row order in a 2D block of elements, stored in a 1D array, assuming a width
		///of `stride`.
		///</summary>
		public static T[] Reverse<T>(T[] input, int stride) {
			var size = input.Length / stride;
			var output = new T[input.Length];
			for (var row = 0; row < size; ++row) {
				for (var column = 0; column < stride; ++column) {
					var i = row * stride + column;
					var j = (size - row - 1) * stride + column;
					output[i] = input[j];
				}
			}
			return output;
		}

		///<summary>
		///Flip rows and columns in a 2D block of elements, stored in a 1D array, assuming a width
		///of `stride`.
		///</summary>
		public static T[] Swizzle<T>(T[] input, int stride) {
			var size = input.Length / stride;
			var output = new T[input.Length];
			for (var row = 0; row < size; ++row) {
				for (var column = 0; column < stride; ++column) {
					var i = column * stride + row;
					var j = row * stride + column;
					output[i] = input[j];
				}
			}
			return output;
		}
	}
}