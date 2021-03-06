﻿using System;
using Xunit;
using JsonWebToken.Cryptography;

namespace JsonWebToken.Tests.Cryptography
{
    // Test data set from https://csrc.nist.gov/CSRC/media/Projects/Cryptographic-Algorithm-Validation-Program/documents/aes/AESAVS.pdf
    public abstract class AesTests
    {
        private protected abstract AesEncryptor CreateEncryptor();

        private protected abstract AesDecryptor CreateDecryptor();

        protected void VerifyGfsBoxKat(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> expectedCiphertext, ReadOnlySpan<byte> key)
        {
            var iv = ByteUtils.HexToByteArray("00000000000000000000000000000000");
            var encryptor = CreateEncryptor();
            Span<byte> ciphertext = new byte[(plaintext.Length + 16) & ~15];
            encryptor.Encrypt(key, plaintext, iv, ciphertext);

            // The last 16 bytes are ignored as the test data sets are for ECB mode
            Assert.Equal(expectedCiphertext.ToArray(), ciphertext.Slice(0, ciphertext.Length - 16).ToArray());
        }

        protected void VerifyKeySboxKat(ReadOnlySpan<byte> key, ReadOnlySpan<byte> expectedCiphertext)
        {
            var iv = ByteUtils.HexToByteArray("00000000000000000000000000000000");
            var plaintext = ByteUtils.HexToByteArray("00000000000000000000000000000000");
            var encryptor = CreateEncryptor();
            Span<byte> ciphertext = new byte[(plaintext.Length + 16) & ~15];
            encryptor.Encrypt(key, plaintext, iv, ciphertext);

            // The last 16 bytes are ignored as the test data sets are for ECB mode
            Assert.Equal(expectedCiphertext.ToArray(), ciphertext.Slice(0, ciphertext.Length - 16).ToArray());
        }

        protected void VerifyVarTxtKat(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> iv, ReadOnlySpan<byte> expectedCiphertext)
        {
            var encryptor = CreateEncryptor();
            Span<byte> ciphertext = new byte[(plaintext.Length + 16) & ~15];
            encryptor.Encrypt(key, plaintext, iv, ciphertext);

            // The last 16 bytes are ignored as the test data sets are for ECB mode
            Assert.Equal(expectedCiphertext.ToArray(), ciphertext.Slice(0, ciphertext.Length - 16).ToArray());
        }

        protected void VerifyEmptySpan(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
        {
            var encryptor = CreateEncryptor();
            ReadOnlySpan<byte> plaintext = ReadOnlySpan<byte>.Empty;
            Span<byte> ciphertext = new byte[(plaintext.Length + 16) & ~15];
            encryptor.Encrypt(key, plaintext, iv, ciphertext);
        }
    }
}
