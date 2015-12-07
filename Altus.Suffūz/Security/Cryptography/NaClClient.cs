using System;
using System.Collections.Generic;
using System.Text;
using Altus.Suffūz.Security.Cryptography.Box;

namespace Altus.Suffūz.Security.Cryptography
{
    public class NaClClient
    {
        public static void CreateKeys(out byte[] publicKey, out byte[] privateKey)
        {
            Curve25519XSalsa20Poly1305.KeyPair(out publicKey, out privateKey);
        }

        private NaClClient(byte[] myPublicKey, byte[] myPrivateKey, byte[] remotePublicKey)
        {
            PublicKey = myPublicKey;
            PrivateKey = myPrivateKey;
            RemotePublicKey = remotePublicKey;

            _noncePfx = new byte[8];
            RandomBytes.generate(_noncePfx);
        }

        public byte[] PublicKey { get; private set; }
        public byte[] PrivateKey { get; private set; }
        public byte[] RemotePublicKey { get; private set; }
        public byte[] Nonce { get; private set; }
        private byte[] _noncePfx;

        public static NaClClient Create(byte[] myPublicKey, byte[] myPrivateKey, byte[] remotePublicKey)
        {
            return new NaClClient(myPublicKey, myPrivateKey, remotePublicKey);
        }

        private byte[] NewNonce()
        {
            byte[] guid = Guid.NewGuid().ToByteArray();
            byte[] nonce = new byte[24];
            Buffer.BlockCopy(_noncePfx, 0, nonce, 0, 8);
            Buffer.BlockCopy(guid, 0, nonce, 8, 16);
            return nonce;
        }

        public byte[] Encrypt(byte[] message, out byte[] nonce)
        {
            return Encrypt(message, 0, message.Length, out nonce);
        }

        public byte[] Encrypt(byte[] message, int index, int length, out byte[] nonce)
        {
            byte[] payload = new byte[length + 32];
            Buffer.BlockCopy(message, index, payload, 32, length);
            
            byte[] cipher = new byte[length - index + 32];
            nonce = NewNonce();
            Curve25519XSalsa20Poly1305.Box(cipher, payload, nonce, RemotePublicKey, PrivateKey);
            return cipher;
        }

        public byte[] Decrypt(byte[] cipher, byte[] nonce)
        {
            byte[] message = new byte[cipher.Length];
            Curve25519XSalsa20Poly1305.Open(message, cipher, nonce, RemotePublicKey, PrivateKey);
            byte[] decrypted = new byte[cipher.Length - 32];
            Buffer.BlockCopy(message, 32, decrypted, 0, cipher.Length - 32);
            return decrypted;
        }
    }
}
