using System;

namespace Altus.Suffūz.Security.Cryptography.ScalarMult {
	unsafe public static class Curve25519 {
		const int CRYPTO_BYTES = 32;
		const int CRYPTO_SCALARBYTES = 32;

		//Never written to (both)
		static Byte[] basev = new Byte[32] { 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; //[32] = {9};
		static UInt32[] minusp = new UInt32[32] { 19, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 128 };

		public static int CryptoScalarMultBase(Byte* q, Byte* n) {
			fixed (Byte* basevp = basev) return CryptoScalarMult(q, n, basevp);
		}
		public static int CryptoScalarMultBase(Byte[] q, Byte[] n) {
			fixed (Byte* basevp = basev, qp = q, np = n) return CryptoScalarMult(qp, np, basevp);
		}

		static void Add(UInt32[] outv, UInt32[] a, UInt32[] b) { //outv[32],a[32],b[32]
			fixed (UInt32* outvp = outv, ap = a, bp = b) Add(outvp, ap, bp);
		}
		static void Add(UInt32[] outv, UInt32[] a, UInt32* b) {
			fixed (UInt32* outvp = outv, ap = a) Add(outvp, ap, b);
		}
		static void Add(UInt32* outv, UInt32* a, UInt32* b) {
			UInt32 u = 0;
			for (int j = 0; j < 31; ++j) { u += a[j] + b[j]; outv[j] = u & 255; u >>= 8; }
			u += a[31] + b[31]; outv[31] = u;
		}

		static void Sub(UInt32* outv, UInt32[] a, UInt32* b) {//outv[32], a[32], b[32]
			UInt32 u = 218;
			for (int j = 0; j < 31; ++j) {
				u += a[j] + 65280 - b[j];
				outv[j] = u & 255;
				u >>= 8;
			}
			u += a[31] - b[31];
			outv[31] = u;
		}

		static void Squeeze(UInt32* a) { //a[32]
			UInt32 u = 0;
			for (int j = 0; j < 31; ++j) { u += a[j]; a[j] = u & 255; u >>= 8; }
			u += a[31]; a[31] = u & 127;
			u = 19 * (u >> 7);
			for (int j = 0; j < 31; ++j) { u += a[j]; a[j] = u & 255; u >>= 8; }
			u += a[31]; a[31] = u;
		}

		static void Freeze(UInt32* a) { //a[32]
			UInt32[] aorig = new UInt32[32];
			for (int j = 0; j < 32; ++j) aorig[j] = a[j];
			fixed (UInt32* minuspp = minusp) Add(a, a, minuspp);
			UInt32 negative = (UInt32)(-((a[31] >> 7) & 1));
			for (int j = 0; j < 32; ++j) a[j] ^= negative & (aorig[j] ^ a[j]);
		}

		static void Mult(UInt32[] outv, UInt32[] a, UInt32[] b) { //outv[32], a[32], b[32]
			fixed (UInt32* outvp = outv, ap = a, bp = b) Mult(outvp, ap, bp);
		}
		static void Mult(UInt32* outv, UInt32* a, UInt32* b) {
			UInt32 j;
			for (uint i = 0; i < 32; ++i) {
				UInt32 u = 0;
				for (j = 0; j <= i; ++j) u += a[j] * b[i - j];
				for (j = i + 1; j < 32; ++j) u += 38 * a[j] * b[i + 32 - j];
				outv[i] = u;
			}
			Squeeze(outv);
		}

		static void Mult121665(UInt32[] outv, UInt32[] a) { //outv[32], a[32]
			UInt32 j;
			UInt32 u = 0;
			for (j = 0; j < 31; ++j) { u += 121665 * a[j]; outv[j] = u & 255; u >>= 8; }
			u += 121665 * a[31]; outv[31] = u & 127;
			u = 19 * (u >> 7);
			for (j = 0; j < 31; ++j) { u += outv[j]; outv[j] = u & 255; u >>= 8; }
			u += outv[j]; outv[j] = u;
		}

		static void Square(UInt32[] outv, UInt32[] a) { //outv[32], a[32]
			fixed (UInt32* outvp = outv, ap = a) Square(outvp, ap);
		}
		static void Square(UInt32* outv, UInt32* a) {
			UInt32 j;
			for (uint i = 0; i < 32; ++i) {
				UInt32 u = 0;
				for (j = 0; j < i - j; ++j) u += a[j] * a[i - j];
				for (j = i + 1; j < i + 32 - j; ++j) u += 38 * a[j] * a[i + 32 - j];
				u *= 2;
				if ((i & 1) == 0) {
					u += a[i / 2] * a[i / 2];
					u += 38 * a[i / 2 + 16] * a[i / 2 + 16];
				}
				outv[i] = u;
			}
			Squeeze(outv);
		}

		static void Select(UInt32[] p, UInt32[] q, UInt32[] r, UInt32[] s, UInt32 b) { //p[64], q[64], r[64], s[64]
			UInt32 bminus1 = b - 1;
			for (int j = 0; j < 64; ++j) {
				UInt32 t = bminus1 & (r[j] ^ s[j]);
				p[j] = s[j] ^ t;
				q[j] = r[j] ^ t;
			}
		}

		static void MainLoop(UInt32[] work, Byte[] e) { //work[64], e[32]
			UInt32[] xzm1 = new UInt32[64];
			UInt32[] xzm = new UInt32[64];
			UInt32[] xzmb = new UInt32[64];
			UInt32[] xzm1b = new UInt32[64];
			UInt32[] xznb = new UInt32[64];
			UInt32[] xzn1b = new UInt32[64];
			UInt32[] a0 = new UInt32[64];
			UInt32[] a1 = new UInt32[64];
			UInt32[] b0 = new UInt32[64];
			UInt32[] b1 = new UInt32[64];
			UInt32[] c1 = new UInt32[64];
			UInt32[] r = new UInt32[32];
			UInt32[] s = new UInt32[32];
			UInt32[] t = new UInt32[32];
			UInt32[] u = new UInt32[32];

			for (int j = 0; j < 32; ++j) xzm1[j] = work[j];
			xzm1[32] = 1;
			for (int j = 33; j < 64; ++j) xzm1[j] = 0;

			xzm[0] = 1;
			for (int j = 1; j < 64; ++j) xzm[j] = 0;

			fixed (UInt32* xzmbp = xzmb, a0p = a0, xzm1bp = xzm1b, a1p = a1, b0p = b0, b1p = b1, c1p = c1, xznbp = xznb, up = u, xzn1bp = xzn1b, workp = work, sp = s, rp = r) {
				for (int pos = 254; pos >= 0; --pos) {
					UInt32 b = (UInt32)(e[pos / 8] >> (pos & 7));
					b &= 1;
					Select(xzmb, xzm1b, xzm, xzm1, b);
					Add(a0, xzmb, xzmbp + 32);
					Sub(a0p + 32, xzmb, xzmbp + 32);
					Add(a1, xzm1b, xzm1bp + 32);
					Sub(a1p + 32, xzm1b, xzm1bp + 32);
					Square(b0p, a0p);
					Square(b0p + 32, a0p + 32);
					Mult(b1p, a1p, a0p + 32);
					Mult(b1p + 32, a1p + 32, a0p);
					Add(c1, b1, b1p + 32);
					Sub(c1p + 32, b1, b1p + 32);
					Square(rp, c1p + 32);
					Sub(sp, b0, b0p + 32);
					Mult121665(t, s);
					Add(u, t, b0p);
					Mult(xznbp, b0p, b0p + 32);
					Mult(xznbp + 32, sp, up);
					Square(xzn1bp, c1p);
					Mult(xzn1bp + 32, rp, workp);
					Select(xzm, xzm1, xznb, xzn1b, b);
				}
			}

			for (int j = 0; j < 64; ++j) work[j] = xzm[j];
		}

		static void Recip(UInt32* outv, UInt32* z) { //outv[32], z[32]
			UInt32[] z2 = new UInt32[32];
			UInt32[] z9 = new UInt32[32];
			UInt32[] z11 = new UInt32[32];
			UInt32[] z2_5_0 = new UInt32[32];
			UInt32[] z2_10_0 = new UInt32[32];
			UInt32[] z2_20_0 = new UInt32[32];
			UInt32[] z2_50_0 = new UInt32[32];
			UInt32[] z2_100_0 = new UInt32[32];
			UInt32[] t0 = new UInt32[32];
			UInt32[] t1 = new UInt32[32];

			/* 2 */
			fixed (UInt32* z2p = z2) Square(z2p, z);
			/* 4 */
			Square(t1, z2);
			/* 8 */
			Square(t0, t1);
			/* 9 */
			fixed (UInt32* z9p = z9, t0p = t0) Mult(z9p, t0p, z);
			/* 11 */
			Mult(z11, z9, z2);
			/* 22 */
			Square(t0, z11);
			/* 2^5 - 2^0 = 31 */
			Mult(z2_5_0, t0, z9);

			/* 2^6 - 2^1 */
			Square(t0, z2_5_0);
			/* 2^7 - 2^2 */
			Square(t1, t0);
			/* 2^8 - 2^3 */
			Square(t0, t1);
			/* 2^9 - 2^4 */
			Square(t1, t0);
			/* 2^10 - 2^5 */
			Square(t0, t1);
			/* 2^10 - 2^0 */
			Mult(z2_10_0, t0, z2_5_0);

			/* 2^11 - 2^1 */
			Square(t0, z2_10_0);
			/* 2^12 - 2^2 */
			Square(t1, t0);
			/* 2^20 - 2^10 */
			for (int i = 2; i < 10; i += 2) { Square(t0, t1); Square(t1, t0); }
			/* 2^20 - 2^0 */
			Mult(z2_20_0, t1, z2_10_0);

			/* 2^21 - 2^1 */
			Square(t0, z2_20_0);
			/* 2^22 - 2^2 */
			Square(t1, t0);
			/* 2^40 - 2^20 */
			for (int i = 2; i < 20; i += 2) { Square(t0, t1); Square(t1, t0); }
			/* 2^40 - 2^0 */
			Mult(t0, t1, z2_20_0);

			/* 2^41 - 2^1 */
			Square(t1, t0);
			/* 2^42 - 2^2 */
			Square(t0, t1);
			/* 2^50 - 2^10 */
			for (int i = 2; i < 10; i += 2) { Square(t1, t0); Square(t0, t1); }
			/* 2^50 - 2^0 */
			Mult(z2_50_0, t0, z2_10_0);

			/* 2^51 - 2^1 */
			Square(t0, z2_50_0);
			/* 2^52 - 2^2 */
			Square(t1, t0);
			/* 2^100 - 2^50 */
			for (int i = 2; i < 50; i += 2) { Square(t0, t1); Square(t1, t0); }
			/* 2^100 - 2^0 */
			Mult(z2_100_0, t1, z2_50_0);

			/* 2^101 - 2^1 */
			Square(t1, z2_100_0);
			/* 2^102 - 2^2 */
			Square(t0, t1);
			/* 2^200 - 2^100 */
			for (int i = 2; i < 100; i += 2) { Square(t1, t0); Square(t0, t1); }
			/* 2^200 - 2^0 */
			Mult(t1, t0, z2_100_0);

			/* 2^201 - 2^1 */
			Square(t0, t1);
			/* 2^202 - 2^2 */
			Square(t1, t0);
			/* 2^250 - 2^50 */
			for (int i = 2; i < 50; i += 2) { Square(t0, t1); Square(t1, t0); }
			/* 2^250 - 2^0 */
			Mult(t0, t1, z2_50_0);

			/* 2^251 - 2^1 */
			Square(t1, t0);
			/* 2^252 - 2^2 */
			Square(t0, t1);
			/* 2^253 - 2^3 */
			Square(t1, t0);
			/* 2^254 - 2^4 */
			Square(t0, t1);
			/* 2^255 - 2^5 */
			Square(t1, t0);
			/* 2^255 - 21 */
			fixed (UInt32* t1p = t1, z11p = z11) Mult(outv, t1p, z11p);
		}

		public static int CryptoScalarMult(Byte* q, Byte* n, Byte* p) {
			UInt32[] work = new UInt32[96];
			Byte[] e = new Byte[32];
			for (int i = 0; i < 32; ++i) e[i] = n[i];
			e[0] &= 248;
			e[31] &= 127;
			e[31] |= 64;
			for (int i = 0; i < 32; ++i) work[i] = p[i];
			MainLoop(work, e);
			fixed (UInt32* workp = work) {
				Recip(workp + 32, workp + 32);
				Mult(workp + 64, workp, workp + 32);
				Freeze(workp + 64);
			}
			for (int i = 0; i < 32; ++i) q[i] = (Byte)work[64 + i];
			return 0;
		}
	}
}