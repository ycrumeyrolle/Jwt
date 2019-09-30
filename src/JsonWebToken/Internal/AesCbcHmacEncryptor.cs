﻿// Copyright (c) 2018 Yann Crumeyrolle. All rights reserved.
// Licensed under the MIT license. See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Security.Cryptography;

namespace JsonWebToken.Internal
{
    /// <summary>
    /// Provides authenticated encryption and decryption for AES CBC HMAC algorithm.
    /// </summary>
    public sealed class AesCbcHmacEncryptor : AuthenticatedEncryptor
    {
        private readonly SymmetricJwk _hmacKey;
        private readonly SymmetricSigner _signer;
        private readonly ObjectPool<Aes> _aesPool;
        private bool _disposed;

        public AesCbcHmacEncryptor(SymmetricJwk key, EncryptionAlgorithm encryptionAlgorithm)
        {
            if (key is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);
            }

            if (encryptionAlgorithm is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.encryptionAlgorithm);
            }

            if (encryptionAlgorithm.Category != EncryptionType.AesHmac)
            {
                ThrowHelper.ThrowNotSupportedException_EncryptionAlgorithm(encryptionAlgorithm);
            }

            if (key.KeySizeInBits < encryptionAlgorithm.RequiredKeySizeInBits)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_EncryptionKeyTooSmall(key, encryptionAlgorithm, encryptionAlgorithm.RequiredKeySizeInBits, key.KeySizeInBits);
            }

            int keyLength = encryptionAlgorithm.RequiredKeySizeInBits >> 4;

            var keyBytes = key.K;
            var aesKey = keyBytes.Slice(keyLength).ToArray();
            _hmacKey = SymmetricJwk.FromSpan(keyBytes.Slice(0, keyLength), false);

            _aesPool = key.Ephemeral ? new ObjectPool<Aes>(new AesPooledPolicy(aesKey), 1) : new ObjectPool<Aes>(new AesPooledPolicy(aesKey));
            if (!_hmacKey.TryGetSigner(encryptionAlgorithm.SignatureAlgorithm, out var signer))
            {
                ThrowHelper.ThrowNotSupportedException_SignatureAlgorithm(encryptionAlgorithm.SignatureAlgorithm);
            }

            _signer = (SymmetricSigner)signer;
        }

        /// <inheritdoc />
        public override int GetCiphertextSize(int plaintextSize)
        {
            return (plaintextSize + 16) & ~15;
        }

        /// <inheritdoc />
        public override int GetNonceSize()
        {
            return 16;
        }

        /// <inheritdoc />
        public override int GetBase64NonceSize()
        {
            return 22;
        }

        /// <inheritdoc />
        public override int GetTagSize()
        {
            return _signer.HashSizeInBytes;
        }

        /// <inheritdoc />
        public override int GetBase64TagSize()
        {
            return _signer.Base64HashSizeInBytes;
        }

        /// <inheritdoc />
        public override void Encrypt(
            ReadOnlySpan<byte> plaintext,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> associatedData,
            Span<byte> ciphertext,
            Span<byte> authenticationTag)
        {
            if (plaintext.IsEmpty)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.plaintext);
            }

            if (associatedData.IsEmpty)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.associatedData);
            }

            if (_disposed)
            {
                ThrowHelper.ThrowObjectDisposedException(GetType());
            }

            byte[]? arrayToReturnToPool = null;
            Aes aes = _aesPool.Get();
            try
            {
                aes.IV = nonce.ToArray();
                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    Transform(encryptor, plaintext, 0, plaintext.Length, ciphertext);
                }

                AesHmacHelper.ComputeAuthenticationTag(_signer, nonce, associatedData, ciphertext, authenticationTag);
            }
            catch
            {
                CryptographicOperations.ZeroMemory(ciphertext);
                throw;
            }
            finally
            {
                _aesPool.Return(aes);
                if (arrayToReturnToPool != null)
                {
                    ArrayPool<byte>.Shared.Return(arrayToReturnToPool);
                }
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            if (!_disposed)
            {
                _hmacKey.Dispose();
                _aesPool.Dispose();

                _disposed = true;
            }
        }

        private static int Transform(ICryptoTransform transform, ReadOnlySpan<byte> input, int inputOffset, int inputLength, Span<byte> output)
        {
            byte[] buffer = input.ToArray();
            int offset = inputOffset;
            int count = inputLength;
            var _inputBlockSize = transform.InputBlockSize;
            var _outputBlockSize = transform.OutputBlockSize;

            var _inputBuffer = new byte[_inputBlockSize];
            var _outputBuffer = new byte[_outputBlockSize];
            int _inputBufferIndex = 0;

            // write <= count bytes to the output stream, transforming as we go.
            // Basic idea: using bytes in the _InputBuffer first, make whole blocks,
            // transform them, and write them out.  Cache any remaining bytes in the _InputBuffer.
            int bytesToWrite = count;
            int currentInputIndex = offset;

            // if we have some bytes in the _InputBuffer, we have to deal with those first,
            // so let's try to make an entire block out of it
            int numOutputBytes;
            int outputLength = 0;
            while (bytesToWrite > 0)
            {
                if (bytesToWrite >= _inputBlockSize)
                {
                    // We have at least an entire block's worth to transform
                    int numWholeBlocks = bytesToWrite / _inputBlockSize;

                    // If the transform will handle multiple blocks at once, do that
                    if (transform.CanTransformMultipleBlocks && numWholeBlocks > 1)
                    {
                        int numWholeBlocksInBytes = numWholeBlocks * _inputBlockSize;

                        // Use ArrayPool.Shared instead of CryptoPool because the array is passed out.
                        byte[] tempOutputBuffer = ArrayPool<byte>.Shared.Rent(numWholeBlocks * _outputBlockSize);
                        numOutputBytes = 0;

                        try
                        {
                            numOutputBytes = transform.TransformBlock(buffer, currentInputIndex, numWholeBlocksInBytes, tempOutputBuffer, 0);

                            tempOutputBuffer.AsSpan(0, numOutputBytes).CopyTo(output.Slice(outputLength));
                            outputLength += numOutputBytes;

                            currentInputIndex += numWholeBlocksInBytes;
                            bytesToWrite -= numWholeBlocksInBytes;
                        }
                        finally
                        {
                            CryptographicOperations.ZeroMemory(new Span<byte>(tempOutputBuffer, 0, numOutputBytes));
                            ArrayPool<byte>.Shared.Return(tempOutputBuffer);
                        }
                    }
                    else
                    {
                        // do it the slow way
                        numOutputBytes = transform.TransformBlock(buffer, currentInputIndex, _inputBlockSize, _outputBuffer, 0);

                        _outputBuffer.AsSpan(0, numOutputBytes).CopyTo(output.Slice(outputLength));
                        outputLength += numOutputBytes;

                        currentInputIndex += _inputBlockSize;
                        bytesToWrite -= _inputBlockSize;
                    }
                }
                else
                {
                    // In this case, we don't have an entire block's worth left, so store it up in the
                    // input buffer, which by now must be empty.
                    Buffer.BlockCopy(buffer, currentInputIndex, _inputBuffer, 0, bytesToWrite);
                    _inputBufferIndex += bytesToWrite;
                    break;
                }
            }

            byte[] finalBytes = transform.TransformFinalBlock(_inputBuffer, 0, _inputBufferIndex);
            finalBytes.AsSpan(0, finalBytes.Length).CopyTo(output.Slice(outputLength));
            return outputLength + finalBytes.Length;
        }

        private sealed class AesPooledPolicy : PooledObjectFactory<Aes>
        {
            private readonly byte[] _key;

            public AesPooledPolicy(byte[] key)
            {
                _key = key;
            }

            public override Aes Create()
            {
                var aes = Aes.Create();
                aes.Key = _key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                return aes;
            }
        }
    }
}
