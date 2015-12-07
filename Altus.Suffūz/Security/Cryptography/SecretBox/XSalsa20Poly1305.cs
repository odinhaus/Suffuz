using System;

namespace Altus.Suffūz.Security.Cryptography.SecretBox {
	unsafe static class XSalsa20Poly1305 {
		const int crypto_secretbox_KEYBYTES = 32;
		const int crypto_secretbox_NONCEBYTES = 24;
		const int crypto_secretbox_ZEROBYTES = 32;
		const int crypto_secretbox_BOXZEROBYTES = 16;

		static public int CryptoSecretBox(Byte* c, Byte* m, UInt64 mlen, Byte* n, Byte* k) {
			if (mlen < 32) return -1;
			Stream.XSalsa20.CryptoStreamXor(c, m, mlen, n, k);
			OneTimeAuth.Poly1305.CryptoOneTimeAuth(c + 16, c + 32, mlen - 32, c);
			for (int i = 0; i < 16; ++i) c[i] = 0;
			return 0;
		}

		static public int CryptoSecretBoxOpen(Byte* m, Byte* c, UInt64 clen, Byte* n, Byte* k) {
			if (clen < 32) return -1;
			Byte[] subkey = new Byte[32];
			fixed (Byte* subkeyp = subkey) {
				Stream.XSalsa20.CryptoStream(subkeyp, 32, n, k);
				if (OneTimeAuth.Poly1305.CryptoOnetimeAuthVerify(c + 16, c + 32, clen - 32, subkeyp) != 0) return -1;
			}
			Stream.XSalsa20.CryptoStreamXor(m, c, clen, n, k);
			for (int i = 0; i < 32; ++i) m[i] = 0;
			return 0;
		}
	}
}