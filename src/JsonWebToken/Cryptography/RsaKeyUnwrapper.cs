﻿// Copyright (c) 2020 Yann Crumeyrolle. All rights reserved.
// Licensed under the MIT license. See LICENSE in the project root for license information.

using System;
using System.Security.Cryptography;

namespace JsonWebToken.Internal
{
    /// <summary>
    /// Provides RSA key key unwrapping services.
    /// </summary>
    internal sealed class RsaKeyUnwrapper : KeyUnwrapper
    {
        private readonly RSA _rsa;
        private readonly RSAEncryptionPadding _padding;
        private bool _disposed;

        public RsaKeyUnwrapper(RsaJwk key, EncryptionAlgorithm encryptionAlgorithm, KeyManagementAlgorithm contentEncryptionAlgorithm)
            : base(key, encryptionAlgorithm, contentEncryptionAlgorithm)
        {
#if SUPPORT_SPAN_CRYPTO
            _rsa = RSA.Create(key.ExportParameters());
#else
#if NET461 || NET47
            _rsa = new RSACng();
#else
            _rsa = RSA.Create();
#endif
            _rsa.ImportParameters(key.ExportParameters());
#endif

            if (contentEncryptionAlgorithm == KeyManagementAlgorithm.RsaOaep)
            {
                _padding = RSAEncryptionPadding.OaepSHA1;
            }
            else if (contentEncryptionAlgorithm == KeyManagementAlgorithm.RsaPkcs1)
            {
                _padding = RSAEncryptionPadding.Pkcs1;
            }
            else if (contentEncryptionAlgorithm == KeyManagementAlgorithm.RsaOaep256)
            {
                _padding = RSAEncryptionPadding.OaepSHA256;
            }
            else if (contentEncryptionAlgorithm == KeyManagementAlgorithm.RsaOaep384)
            {
                _padding = RSAEncryptionPadding.OaepSHA384;
            }
            else if (contentEncryptionAlgorithm == KeyManagementAlgorithm.RsaOaep512)
            {
                _padding = RSAEncryptionPadding.OaepSHA512;
            }
            else
            {
                ThrowHelper.ThrowNotSupportedException_AlgorithmForKeyWrap(contentEncryptionAlgorithm);
                _padding = RSAEncryptionPadding.CreateOaep(new HashAlgorithmName()); // will never occur
            }
        }

        /// <inheritsdoc />
        public override bool TryUnwrapKey(ReadOnlySpan<byte> key, Span<byte> destination, JwtHeader header, out int bytesWritten)
        {
            if (key.IsEmpty)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);
            }

            if (header == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.header);
            }

            if (_disposed)
            {
                ThrowHelper.ThrowObjectDisposedException(GetType());
            }

            try
            {
#if SUPPORT_SPAN_CRYPTO
                Span<byte> tmp = stackalloc byte[destination.Length * 2];
                var res= _rsa.TryDecrypt(key, tmp, _padding, out bytesWritten);
                if (res)
                {
                    if (bytesWritten > destination.Length)
                    {
                        throw new Exception($"bytesWritten > destination.Length ({bytesWritten} > {destination.Length}" );
                    }

                    tmp = tmp.Slice(0, bytesWritten);
                    tmp.CopyTo(destination);
                }

                return res;
#else
                var result = _rsa.Decrypt(key.ToArray(), _padding);
                bytesWritten = result.Length;
                result.CopyTo(destination);

                return true;
#endif
            }
            catch (CryptographicException)
            {
                bytesWritten = 0;
                return false;
            }
        }

        /// <inheritsdoc />
        public override int GetKeyUnwrapSize(int wrappedKeySize)
            => EncryptionAlgorithm.RequiredKeySizeInBytes;

        /// <inheritsdoc />
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _rsa.Dispose();
                }

                _disposed = true;
            }
        }
    }
}
