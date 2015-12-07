using System;

namespace Altus.Suffūz.Security.Cryptography.OneTimeAuth {
	unsafe static class Poly1305 {
		const int CRYPTO_BYTES = 16;
		const int CRYPTO_KEYBYTES = 32;

		//Never written to
		static UInt32[] minusp = new UInt32[17] { 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 252 };

		public static int CryptoOnetimeAuthVerify(Byte* h, Byte* inv, UInt64 inlen, Byte* k) {
			Byte[] correct = new Byte[16];
			fixed (Byte* correctp = correct) {
				CryptoOneTimeAuth(correctp, inv, inlen, k);
				return Verify._16.CryptoVerify(h, correctp);
			}
		}

		static void Add(UInt32[] h, UInt32[] c) { //h[17], c[17]
			UInt32 j;
			UInt32 u;
			u = 0;
			for (j = 0; j < 17; ++j) { u += h[j] + c[j]; h[j] = u & 255; u >>= 8; }
		}

		static void Squeeze(UInt32[] h) { //h[17]
			UInt32 u = 0;
			for (int j = 0; j < 16; ++j) { u += h[j]; h[j] = u & 255; u >>= 8; }
			u += h[16]; h[16] = u & 3;
			u = 5 * (u >> 2);
			for (int j = 0; j < 16; ++j) { u += h[j]; h[j] = u & 255; u >>= 8; }
			u += h[16]; h[16] = u;
		}

		static void Freeze(UInt32[] h) { //h[17]
			UInt32[] horig = new UInt32[17];
			for (int j = 0; j < 17; ++j) horig[j] = h[j];
			Add(h, minusp);
			UInt32 negative = (UInt32)(-(h[16] >> 7));
			for (int j = 0; j < 17; ++j) h[j] ^= negative & (horig[j] ^ h[j]);
		}

		static void Mulmod(UInt32[] h, UInt32[] r) { //h[17], r[17]
			UInt32[] hr = new UInt32[17];
			for (uint i = 0; i < 17; ++i) {
				UInt32 u = 0;
				for (uint j = 0; j <= i; ++j) u += h[j] * r[i - j];
				for (uint j = i + 1; j < 17; ++j) u += 320 * h[j] * r[i + 17 - j];
				hr[i] = u;
			}
			for (int i = 0; i < 17; ++i) h[i] = hr[i];
			Squeeze(h);
		}

		public static int CryptoOneTimeAuth(Byte* outv, Byte* inv, UInt64 inlen, Byte* k) {
			UInt32 j;
			UInt32[] r = new UInt32[17];
			UInt32[] h = new UInt32[17];
			UInt32[] c = new UInt32[17];

			r[0] = k[0];
			r[1] = k[1];
			r[2] = k[2];
			r[3] = (UInt32)(k[3] & 15);
			r[4] = (UInt32)(k[4] & 252);
			r[5] = k[5];
			r[6] = k[6];
			r[7] = (UInt32)(k[7] & 15);
			r[8] = (UInt32)(k[8] & 252);
			r[9] = k[9];
			r[10] = k[10];
			r[11] = (UInt32)(k[11] & 15);
			r[12] = (UInt32)(k[12] & 252);
			r[13] = k[13];
			r[14] = k[14];
			r[15] = (UInt32)(k[15] & 15);
			r[16] = 0;

			for (j = 0; j < 17; ++j) h[j] = 0;

			while (inlen > 0) {
				for (j = 0; j < 17; ++j) c[j] = 0;
				for (j = 0; (j < 16) && (j < inlen); ++j) c[j] = inv[j];
				c[j] = 1;
				inv += j; inlen -= j;
				Add(h, c);
				Mulmod(h, r);
			}

			Freeze(h);

			for (j = 0; j < 16; ++j) c[j] = k[j + 16];
			c[16] = 0;
			Add(h, c);
			for (j = 0; j < 16; ++j) outv[j] = (Byte)h[j];
			return 0;
		}
	}
}