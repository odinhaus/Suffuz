using System;

namespace Altus.Suffūz.Security.Cryptography {
	class RandomBytes {
		static Random rnd = new Random();
		public static void generate(Byte[] x) {
			rnd.NextBytes(x);
		}
	}
}