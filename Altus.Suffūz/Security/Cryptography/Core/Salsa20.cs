using System;

namespace Altus.Suffūz.Security.Cryptography.Core
{
	static unsafe class Salsa20 {
		const int crypto_core_salsa20_ref_OUTPUTBYTES = 64;
		const int crypto_core_salsa20_ref_INPUTBYTES = 16;
		const int crypto_core_salsa20_ref_KEYBYTES = 32;
		const int crypto_core_salsa20_ref_CONSTBYTES = 16;

		//#include "crypto_core.h"

		const int ROUNDS = 20;

		static UInt32 Rotate(UInt32 u, int c) {
			return (u << c) | (u >> (32 - c));
		}

		static uint LoadLE(Byte* x) {
			return (UInt32)(x[0]) | (((UInt32)(x[1])) << 8) | (((UInt32)(x[2])) << 16) | (((UInt32)(x[3])) << 24);
		}

		static void StoreLE(Byte* x, UInt32 u) {
			x[0] = (Byte)u; u >>= 8;
			x[1] = (Byte)u; u >>= 8;
			x[2] = (Byte)u; u >>= 8;
			x[3] = (Byte)u;
		}

		public static int CryptoCore(Byte* outv, Byte* inv, Byte* k, Byte* c) {
			UInt32 x0, x1, x2, x3, x4, x5, x6, x7, x8, x9, x10, x11, x12, x13, x14, x15;
			UInt32 j0, j1, j2, j3, j4, j5, j6, j7, j8, j9, j10, j11, j12, j13, j14, j15;
			int i;

			j0 = x0 = LoadLE(c + 0);
			j1 = x1 = LoadLE(k + 0);
			j2 = x2 = LoadLE(k + 4);
			j3 = x3 = LoadLE(k + 8);
			j4 = x4 = LoadLE(k + 12);
			j5 = x5 = LoadLE(c + 4);
			j6 = x6 = LoadLE(inv + 0);
			j7 = x7 = LoadLE(inv + 4);
			j8 = x8 = LoadLE(inv + 8);
			j9 = x9 = LoadLE(inv + 12);
			j10 = x10 = LoadLE(c + 8);
			j11 = x11 = LoadLE(k + 16);
			j12 = x12 = LoadLE(k + 20);
			j13 = x13 = LoadLE(k + 24);
			j14 = x14 = LoadLE(k + 28);
			j15 = x15 = LoadLE(c + 12);

			for (i = ROUNDS; i > 0; i -= 2) {
				x4 ^= Rotate(x0 + x12, 7);
				x8 ^= Rotate(x4 + x0, 9);
				x12 ^= Rotate(x8 + x4, 13);
				x0 ^= Rotate(x12 + x8, 18);
				x9 ^= Rotate(x5 + x1, 7);
				x13 ^= Rotate(x9 + x5, 9);
				x1 ^= Rotate(x13 + x9, 13);
				x5 ^= Rotate(x1 + x13, 18);
				x14 ^= Rotate(x10 + x6, 7);
				x2 ^= Rotate(x14 + x10, 9);
				x6 ^= Rotate(x2 + x14, 13);
				x10 ^= Rotate(x6 + x2, 18);
				x3 ^= Rotate(x15 + x11, 7);
				x7 ^= Rotate(x3 + x15, 9);
				x11 ^= Rotate(x7 + x3, 13);
				x15 ^= Rotate(x11 + x7, 18);
				x1 ^= Rotate(x0 + x3, 7);
				x2 ^= Rotate(x1 + x0, 9);
				x3 ^= Rotate(x2 + x1, 13);
				x0 ^= Rotate(x3 + x2, 18);
				x6 ^= Rotate(x5 + x4, 7);
				x7 ^= Rotate(x6 + x5, 9);
				x4 ^= Rotate(x7 + x6, 13);
				x5 ^= Rotate(x4 + x7, 18);
				x11 ^= Rotate(x10 + x9, 7);
				x8 ^= Rotate(x11 + x10, 9);
				x9 ^= Rotate(x8 + x11, 13);
				x10 ^= Rotate(x9 + x8, 18);
				x12 ^= Rotate(x15 + x14, 7);
				x13 ^= Rotate(x12 + x15, 9);
				x14 ^= Rotate(x13 + x12, 13);
				x15 ^= Rotate(x14 + x13, 18);
			}

			x0 += j0;
			x1 += j1;
			x2 += j2;
			x3 += j3;
			x4 += j4;
			x5 += j5;
			x6 += j6;
			x7 += j7;
			x8 += j8;
			x9 += j9;
			x10 += j10;
			x11 += j11;
			x12 += j12;
			x13 += j13;
			x14 += j14;
			x15 += j15;

			StoreLE(outv + 0, x0);
			StoreLE(outv + 4, x1);
			StoreLE(outv + 8, x2);
			StoreLE(outv + 12, x3);
			StoreLE(outv + 16, x4);
			StoreLE(outv + 20, x5);
			StoreLE(outv + 24, x6);
			StoreLE(outv + 28, x7);
			StoreLE(outv + 32, x8);
			StoreLE(outv + 36, x9);
			StoreLE(outv + 40, x10);
			StoreLE(outv + 44, x11);
			StoreLE(outv + 48, x12);
			StoreLE(outv + 52, x13);
			StoreLE(outv + 56, x14);
			StoreLE(outv + 60, x15);

			return 0;
		}
	}
}