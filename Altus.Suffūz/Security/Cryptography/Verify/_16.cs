using System;
using System.Collections.Generic;
using System.Text;

namespace Altus.Suffūz.Security.Cryptography.Verify {
	unsafe static class _16 {
		const int crypto_verify_16_ref_BYTES = 16;

		public static int CryptoVerify(Byte* x, Byte* y) {
			UInt32 differentbits = 0;
			for (int i = 0; i < 15; i++) differentbits |= (UInt32)(x[i] ^ y[i]);
			return (1 & (((Int32)differentbits - 1) >> 8)) - 1;
		}
	}
}