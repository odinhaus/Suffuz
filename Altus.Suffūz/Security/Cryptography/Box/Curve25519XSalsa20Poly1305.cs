using System;
using Altus.Suffūz.Security.Cryptography;

namespace Altus.Suffūz.Security.Cryptography.Box {
	public class Curve25519XSalsa20Poly1305 {
		/* constants */
		public const int PUBLICKEYBYTES = 32;
		public const int SECRETKEYBYTES = 32;
		public const int BEFORENMBYTES = 32;
		public const int NONCEBYTES = 24;
		public const int ZEROBYTES = 32;
		public const int BOXZEROBYTES = 16;

		//Never written to
		static Byte[] sigma = new Byte[16] {(Byte)'e', (Byte)'x', (Byte)'p', (Byte)'a', //[16] = "expand 32-byte k";
											(Byte)'n', (Byte)'d', (Byte)' ', (Byte)'3',
											(Byte)'2', (Byte)'-', (Byte)'b', (Byte)'y',
											(Byte)'t', (Byte)'e', (Byte)' ', (Byte)'k', };
		//static Byte[] n = new Byte[16];


		/* static pointer based methods */
		static unsafe public int GetPublicKey(Byte* pk, Byte* sk) {
			return ScalarMult.Curve25519.CryptoScalarMultBase(pk, sk);
		}
		static unsafe public int AfterNM(Byte* c, Byte* m, UInt64 mlen, Byte* n, Byte* k) {
			return SecretBox.XSalsa20Poly1305.CryptoSecretBox(c, m, mlen, n, k);
		}
		static unsafe public int OpenAterNM(Byte* m, Byte* c, UInt64 clen, Byte* n, Byte* k) {
			return SecretBox.XSalsa20Poly1305.CryptoSecretBoxOpen(m, c, clen, n, k);
		}
		static unsafe public int BeforeNM(Byte* k, Byte* pk, Byte* sk) {
			Byte[] s = new Byte[32];
			fixed (Byte* sp = s, sigmap = sigma) { //, np = n
				ScalarMult.Curve25519.CryptoScalarMult(sp, sk, pk);
				return Core.HSalsa20.CryptoCore(k, null, sp, sigmap); //k, np, sp, sigmap
			}
		}
		static unsafe public int Box(Byte* c, Byte* m, UInt64 mlen, Byte* n, Byte* pk, Byte* sk) {
			Byte[] k = new Byte[BEFORENMBYTES];
			fixed (Byte* kp = k) {
				BeforeNM(kp, pk, sk);
				return AfterNM(c, m, mlen, n, kp);
			}
		}
		static unsafe public int Open(Byte* m, Byte* c, UInt64 clen, Byte* n, Byte* pk, Byte* sk) {
			Byte[] k = new Byte[BEFORENMBYTES];
			fixed (Byte* kp = k) {
				BeforeNM(kp, pk, sk);
				return OpenAterNM(m, c, clen, n, kp);
			}
		}

		/* static array based methods */
		static unsafe public int KeyPair(out Byte[] pk, out Byte[] sk) {
			sk = new Byte[32];
			pk = new Byte[32];
			RandomBytes.generate(sk); //randombytes(sk, 32);
			fixed (Byte* skp = sk, pkp = pk) return ScalarMult.Curve25519.CryptoScalarMultBase(pkp, skp);
		}
		static unsafe public int GetPublicKey(out Byte[] pk, Byte[] sk) {
			pk = new Byte[32];
			fixed (Byte* skp = sk, pkp = pk) return GetPublicKey(pkp, skp);
		}
		static unsafe public int BeforeNM(Byte[] k, Byte[] pk, Byte[] sk) {
			fixed (Byte* kp = k, pkp = pk, skp = sk) return BeforeNM(kp, pkp, skp);
		}
		static unsafe public Byte[] BeforeNM(Byte[] pk, Byte[] sk) {
			Byte[] k = new Byte[BEFORENMBYTES];
			int ret;
			fixed (Byte* kp = k, pkp = pk, skp = sk) ret = BeforeNM(kp, pkp, skp);
			if (ret != 0) throw new Exception("Error in crypto_box_beforenm: " + ret.ToString());
			return k;
		}
		static unsafe public int AfterNM(Byte[] c, Byte[] m, Byte[] n, Byte[] k) {
			fixed (Byte* cp = c, mp = m, np = n, kp = k) return AfterNM(cp, mp, (ulong)m.Length, np, kp);
		}
		static unsafe public int OpenAfterNM(Byte[] m, Byte[] c, Byte[] n, Byte[] k) {
			fixed (Byte* cp = c, mp = m, np = n, kp = k) return OpenAterNM(mp, cp, (ulong)c.Length, np, kp);
		}
		static unsafe public int Box(Byte[] c, Byte[] m, Byte[] n, Byte[] pk, Byte[] sk) {
			fixed (Byte* cp = c, mp = m, np = n, pkp = pk, skp = sk) return Box(cp, mp, (ulong)m.Length, np, pkp, skp);
		}
		static unsafe public int Open(byte[] m, Byte[] c, Byte[] n, Byte[] pk, Byte[] sk) {
			fixed (Byte* cp = c, mp = m, np = n, pkp = pk, skp = sk) return Open(mp, cp, (ulong)c.Length, np, pkp, skp);
		}

		static unsafe public int BoxAfterNM(Byte[] c, int coffset, Byte[] m, int moffset, int mlen, Byte[] n, Byte[] k) {
			fixed (Byte* cp = c, mp = m, np = n, kp = k) return AfterNM(cp + coffset, mp + moffset, (ulong)mlen, np, kp);
		}
		static unsafe public int OpenAfterNM(Byte[] m, int moffset, Byte[] c, int coffset, int clen, Byte[] n, Byte[] k) {
			fixed (Byte* cp = c, mp = m, np = n, kp = k) return OpenAterNM(mp + moffset, cp + coffset, (ulong)clen, np, kp);
		}
		static unsafe public int Box(Byte[] c, int coffset, Byte[] m, int moffset, int mlen, Byte[] n, Byte[] pk, Byte[] sk) {
			fixed (Byte* cp = c, mp = m, np = n, pkp = pk, skp = sk) return Box(cp + coffset, mp + moffset, (ulong)mlen, np, pkp, skp);
		}
		static unsafe public int Open(Byte[] m, int moffset, Byte[] c, int coffset, int clen, Byte[] n, Byte[] pk, Byte[] sk) {
			fixed (Byte* cp = c, mp = m, np = n, pkp = pk, skp = sk) return Open(mp + moffset, cp + coffset, (ulong)clen, np, pkp, skp);
		}
	}
}