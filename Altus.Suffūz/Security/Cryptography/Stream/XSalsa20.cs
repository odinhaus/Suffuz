using System;

namespace Altus.Suffūz.Security.Cryptography.Stream {
	static unsafe class XSalsa20 {
		const int crypto_stream_xsalsa20_ref_KEYBYTES = 32;
		const int crypto_stream_xsalsa20_ref_NONCEBYTES = 24;

		//Never written to
		static Byte[] sigma = new Byte[16] {(Byte)'e', (Byte)'x', (Byte)'p', (Byte)'a', //[16] = "expand 32-byte k";
											(Byte)'n', (Byte)'d', (Byte)' ', (Byte)'3',
											(Byte)'2', (Byte)'-', (Byte)'b', (Byte)'y',
											(Byte)'t', (Byte)'e', (Byte)' ', (Byte)'k', }; 

		public static int CryptoStream(Byte* c, int clen, Byte* n, Byte* k) {
			Byte[] subkey = new Byte[32];
			fixed (Byte* subkeyp = subkey, sigmap = sigma) {
				Core.HSalsa20.CryptoCore(subkeyp, n, k, sigmap);
				return Salsa20.CryptoStream(c, clen, n + 16, subkeyp);
			}
		}

		public static int CryptoStreamXor(Byte* c, Byte* m, UInt64 mlen, Byte* n, Byte* k) {
			Byte[] subkey = new Byte[32];
			fixed (Byte* subkeyp = subkey, sigmap = sigma) {
				Core.HSalsa20.CryptoCore(subkeyp, n, k, sigmap);
				return Salsa20.CryptoStreamXor(c, m, (int)mlen, n + 16, subkeyp);
			}
		}
	}
}