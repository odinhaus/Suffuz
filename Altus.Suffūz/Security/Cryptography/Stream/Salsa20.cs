using System;

namespace Altus.Suffūz.Security.Cryptography.Stream {
	static unsafe class Salsa20 {
		const int crypto_stream_salsa20_ref_KEYBYTES = 32;
		const int crypto_stream_salsa20_ref_NONCEBYTES = 8;

		static Byte[] sigma = new Byte[16] {(Byte)'e', (Byte)'x', (Byte)'p', (Byte)'a', //[16] = "expand 32-byte k";
											(Byte)'n', (Byte)'d', (Byte)' ', (Byte)'3',
											(Byte)'2', (Byte)'-', (Byte)'b', (Byte)'y',
											(Byte)'t', (Byte)'e', (Byte)' ', (Byte)'k', }; 

		public static int CryptoStream(Byte* c, int clen, Byte* n, Byte* k) {
			Byte[] inv = new Byte[16];
			Byte[] block = new Byte[64];
			if (clen == 0) return 0;

			for (int i = 0; i < 8; ++i) inv[i] = n[i];
			for (int i = 8; i < 16; ++i) inv[i] = 0;

			while (clen >= 64) {
				fixed (Byte* invp = inv, sigmap = sigma) Core.Salsa20.CryptoCore(c, invp, k, sigmap);

				UInt32 u = 1;
				for (int i = 8; i < 16; ++i) {
					u += (UInt32)inv[i];
					inv[i] = (Byte)u;
					u >>= 8;
				}

				clen -= 64;
				c += 64;
			}

			if (clen != 0) {
				fixed (Byte* invp = inv, sigmap = sigma, blockp = block) Core.Salsa20.CryptoCore(blockp, invp, k, sigmap);
				for (int i = 0; i < clen; ++i) c[i] = block[i];
			}
			return 0;
		}

		public static int CryptoStreamXor(Byte* c, Byte* m, int mlen, Byte* n, Byte* k) {
			Byte[] inv = new Byte[16];
			Byte[] block = new Byte[64];
			if (mlen == 0) return 0;

			for (int i = 0; i < 8; ++i) inv[i] = n[i];
			for (int i = 8; i < 16; ++i) inv[i] = 0;

			while (mlen >= 64) {
				fixed (Byte* invp = inv, sigmap = sigma, blockp = block) Core.Salsa20.CryptoCore(blockp, invp, k, sigmap);
				for (int i = 0; i < 64; ++i) c[i] = (Byte)(m[i] ^ block[i]);

				UInt32 u = 1;
				for (int i = 8; i < 16; ++i) {
					u += (UInt32)inv[i];
					inv[i] = (Byte)u;
					u >>= 8;
				}

				mlen -= 64;
				c += 64;
				m += 64;
			}

			if (mlen != 0) {
				fixed (Byte* invp = inv, sigmap = sigma, blockp = block) Core.Salsa20.CryptoCore(blockp, invp, k, sigmap);
				for (int i = 0; i < mlen; ++i) c[i] = (Byte)(m[i] ^ block[i]);
			}
			return 0;
		}
	}
}