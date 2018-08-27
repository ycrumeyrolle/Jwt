﻿using Newtonsoft.Json.Linq;
using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace JsonWebToken
{
    public abstract class EncryptedJwtDescriptor<TPayload> : JwtDescriptor<TPayload> where TPayload : class
    {
        private static readonly RandomNumberGenerator _randomNumberGenerator = RandomNumberGenerator.Create();

        public EncryptedJwtDescriptor(JObject header, TPayload payload)
            : base(header, payload)
        {
        }

        public EncryptedJwtDescriptor(TPayload payload)
            : base(payload)
        {
        }

        public EncryptionAlgorithm EncryptionAlgorithm
        {
            get => (EncryptionAlgorithm)GetHeaderParameter(HeaderParameters.Enc);
            set => Header[HeaderParameters.Enc] = (string)value;
        }

        public string CompressionAlgorithm
        {
            get => GetHeaderParameter(HeaderParameters.Zip);
            set => Header[HeaderParameters.Zip] = value;
        }

#if NETCOREAPP2_1
        protected string EncryptToken(string payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            int payloadLength = payload.Length;
            byte[] payloadToReturnToPool = null;
            Span<byte> encodedPayload = payloadLength > Constants.MaxStackallocBytes
                             ? (payloadToReturnToPool = ArrayPool<byte>.Shared.Rent(payloadLength)).AsSpan(0, payloadLength)
                             : stackalloc byte[payloadLength];

            try
            {
                Encoding.UTF8.GetBytes(payload, encodedPayload);
                return EncryptToken(encodedPayload);
            }
            finally
            {
                if (payloadToReturnToPool != null)
                {
                    ArrayPool<byte>.Shared.Return(payloadToReturnToPool);
                }
            }
        }
#else
        protected string EncryptToken(string payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var encodedPayload = Encoding.UTF8.GetBytes(payload);
            return EncryptToken(encodedPayload);
        }
#endif
        protected unsafe string EncryptToken(Span<byte> payload)
        {
            EncryptionAlgorithm encryptionAlgorithm = EncryptionAlgorithm;
            KeyManagementAlgorithm contentEncryptionAlgorithm = (KeyManagementAlgorithm)Algorithm;
            bool isDirectEncryption = contentEncryptionAlgorithm == KeyManagementAlgorithm.Direct;

            AuthenticatedEncryptionProvider encryptionProvider = null;
            KeyWrapProvider kwProvider = null;
            if (isDirectEncryption)
            {
                encryptionProvider = Key.CreateAuthenticatedEncryptionProvider(encryptionAlgorithm);
            }
            else
            {
                kwProvider = Key.CreateKeyWrapProvider(encryptionAlgorithm, contentEncryptionAlgorithm);
                if (kwProvider == null)
                {
                    throw new JsonWebTokenEncryptionFailedException(ErrorMessages.FormatInvariant(ErrorMessages.NotSuportedAlgorithmForKeyWrap, encryptionAlgorithm));
                }
            }

            var header = Header;
            Span<byte> wrappedKey = contentEncryptionAlgorithm.ProduceEncryptedKey
                                        ? stackalloc byte[kwProvider.GetKeyWrapSize()]
                                        : null;
            if (!isDirectEncryption)
            {
                JsonWebKey cek;
                try
                {
                    if (!kwProvider.TryWrapKey(null, header, wrappedKey, out cek, out var keyWrappedBytesWritten))
                    {
                        throw new JsonWebTokenEncryptionFailedException(ErrorMessages.KeyWrapFailed);
                    }
                }
                finally
                {
                    Key.ReleaseKeyWrapProvider(kwProvider);
                }

                encryptionProvider = cek.CreateAuthenticatedEncryptionProvider(encryptionAlgorithm);
            }

            if (encryptionProvider == null)
            {
                throw new JsonWebTokenEncryptionFailedException(ErrorMessages.FormatInvariant(ErrorMessages.NotSupportedEncryptionAlgorithm, encryptionAlgorithm));
            }

            if (header[HeaderParameters.Kid] == null && Key.Kid != null)
            {
                header[HeaderParameters.Kid] = Key.Kid;
            }

            try
            {
                var headerJson = Serialize(header);
                int headerJsonLength = headerJson.Length;
                int base64EncodedHeaderLength = Base64Url.GetArraySizeRequiredToEncode(headerJsonLength);

                byte[] arrayByteToReturnToPool = null;
                char[] arrayCharToReturnToPool = null;
                char[] buffer64HeaderToReturnToPool = null;
                byte[] arrayCiphertextToReturnToPool = null;
                Span<byte> asciiEncodedHeader = base64EncodedHeaderLength > Constants.MaxStackallocBytes
                                    ? (arrayByteToReturnToPool = ArrayPool<byte>.Shared.Rent(base64EncodedHeaderLength)).AsSpan(0, base64EncodedHeaderLength)
                                    : stackalloc byte[base64EncodedHeaderLength];

                try
                {
                    Span<byte> utf8EncodedHeader = asciiEncodedHeader.Slice(0, headerJsonLength);
                    Span<char> base64EncodedHeader = base64EncodedHeaderLength > Constants.MaxStackallocBytes
                                                    ? (buffer64HeaderToReturnToPool = ArrayPool<char>.Shared.Rent(base64EncodedHeaderLength)).AsSpan(0, base64EncodedHeaderLength)
                                                    : stackalloc char[base64EncodedHeaderLength];
#if NETCOREAPP2_1
                    Encoding.UTF8.GetBytes(headerJson, utf8EncodedHeader);
                    int bytesWritten = Base64Url.Base64UrlEncode(utf8EncodedHeader, base64EncodedHeader);
                    Encoding.ASCII.GetBytes(base64EncodedHeader, asciiEncodedHeader);
#else
                    int bytesWritten;
                    fixed (char* rawPtr = &MemoryMarshal.GetReference(headerJson.AsSpan()))
                    fixed (byte* utf8Ptr = &MemoryMarshal.GetReference(utf8EncodedHeader))
                    fixed (char* b64Ptr = &MemoryMarshal.GetReference(base64EncodedHeader))
                    fixed (byte* header8Ptr = &MemoryMarshal.GetReference(asciiEncodedHeader))
                    {
                        Encoding.UTF8.GetBytes(rawPtr, headerJson.Length, utf8Ptr, utf8EncodedHeader.Length);
                        bytesWritten = Base64Url.Base64UrlEncode(utf8EncodedHeader, base64EncodedHeader);
                        Encoding.ASCII.GetBytes(b64Ptr, base64EncodedHeader.Length, header8Ptr, asciiEncodedHeader.Length);
                    }
#endif                  
                    CompressionProvider compressionProvider = null;
                    if (CompressionAlgorithm != null)
                    {
                        compressionProvider = CompressionProvider.CreateCompressionProvider(CompressionAlgorithm);
                        if (compressionProvider == null)
                        {
                            throw new JsonWebTokenEncryptionFailedException(ErrorMessages.FormatInvariant(ErrorMessages.NotSuportedCompressionAlgorithm, CompressionAlgorithm));
                        }
                    }

                    if (compressionProvider != null)
                    {
                        payload = compressionProvider.Compress(payload);
                    }

                    int ciphertextLength = encryptionProvider.GetCiphertextSize(payload.Length);
                    Span<byte> tag = stackalloc byte[encryptionProvider.GetTagSize()];
                    Span<byte> ciphertext = ciphertextLength > Constants.MaxStackallocBytes
                                                ? (arrayCiphertextToReturnToPool = ArrayPool<byte>.Shared.Rent(ciphertextLength)).AsSpan(0, ciphertextLength)
                                                : stackalloc byte[ciphertextLength];
#if NETCOREAPP2_1
                    Span<byte> nonce = stackalloc byte[encryptionProvider.GetNonceSize()];
                    RandomNumberGenerator.Fill(nonce);
#else
                    var nonce = new byte[encryptionProvider.GetNonceSize()];
                    _randomNumberGenerator.GetBytes(nonce);
#endif
                    encryptionProvider.Encrypt(payload, nonce, asciiEncodedHeader, ciphertext, tag);

                    int encryptionLength =
                        base64EncodedHeader.Length
                        + Base64Url.GetArraySizeRequiredToEncode(nonce.Length)
                        + Base64Url.GetArraySizeRequiredToEncode(ciphertext.Length)
                        + Base64Url.GetArraySizeRequiredToEncode(tag.Length)
                        + (Constants.JweSegmentCount - 1);
                    if (wrappedKey != null)
                    {
                        encryptionLength += Base64Url.GetArraySizeRequiredToEncode(wrappedKey.Length);
                    }

                    Span<char> encryptedToken = encryptionLength > Constants.MaxStackallocBytes
                                                ? (arrayCharToReturnToPool = ArrayPool<char>.Shared.Rent(encryptionLength)).AsSpan(0, encryptionLength)
                                                : stackalloc char[encryptionLength];

                    base64EncodedHeader.CopyTo(encryptedToken);
                    encryptedToken[bytesWritten++] = '.';
                    if (wrappedKey != null)
                    {
                        bytesWritten += Base64Url.Base64UrlEncode(wrappedKey, encryptedToken.Slice(bytesWritten));
                    }

                    encryptedToken[bytesWritten++] = '.';
                    bytesWritten += Base64Url.Base64UrlEncode(nonce, encryptedToken.Slice(bytesWritten));
                    encryptedToken[bytesWritten++] = '.';
                    bytesWritten += Base64Url.Base64UrlEncode(ciphertext, encryptedToken.Slice(bytesWritten));
                    encryptedToken[bytesWritten++] = '.';
                    bytesWritten += Base64Url.Base64UrlEncode(tag, encryptedToken.Slice(bytesWritten));
                    //Debug.Assert(encryptedToken.Length == bytesWritten);

                    return encryptedToken.ToString();
                }
                finally
                {
                    if (arrayCharToReturnToPool != null)
                    {
                        ArrayPool<char>.Shared.Return(arrayCharToReturnToPool);
                    }

                    if (arrayByteToReturnToPool != null)
                    {
                        ArrayPool<byte>.Shared.Return(arrayByteToReturnToPool);
                    }

                    if (buffer64HeaderToReturnToPool != null)
                    {
                        ArrayPool<char>.Shared.Return(buffer64HeaderToReturnToPool);
                    }

                    if (arrayCiphertextToReturnToPool != null)
                    {
                        ArrayPool<byte>.Shared.Return(arrayCiphertextToReturnToPool);
                    }
                }
                //#else
                //                byte[] arrayCiphertextToReturnToPool = null;
                //                var utf8Header = Encoding.UTF8.GetBytes(Serialize(Header));
                //                var base64Header = Base64Url.Encode(utf8Header);
                //                CompressionProvider compressionProvider = null;
                //                if (CompressionAlgorithm != null)
                //                {
                //                    compressionProvider = CompressionProvider.CreateCompressionProvider(CompressionAlgorithm);
                //                    if (compressionProvider == null)
                //                    {
                //                        throw new JsonWebTokenEncryptionFailedException(ErrorMessages.FormatInvariant(ErrorMessages.NotSuportedCompressionAlgorithm, CompressionAlgorithm));
                //                    }
                //                }

                //                if (compressionProvider != null)
                //                {
                //                    payload = compressionProvider.Compress(payload);
                //                }

                //                try
                //                {
                //                    int ciphertextLength = encryptionProvider.GetCiphertextSize(payload.Length);
                //                    var nonce = new byte[16];
                //                    Span<byte> tag = stackalloc byte[encryptionProvider.GetTagSize()];
                //                    Span<byte> ciphertext = ciphertextLength > Constants.MaxStackallocBytes
                //                                                ? (arrayCiphertextToReturnToPool = ArrayPool<byte>.Shared.Rent(ciphertextLength)).AsSpan(0, ciphertextLength)
                //                                                : stackalloc byte[ciphertextLength];
                //                    _randomNumberGenerator.GetNonZeroBytes(nonce);
                //                    encryptionProvider.Encrypt(nonce, payload, ciphertext, tag, Encoding.ASCII.GetBytes(base64Header));
                //                    if (wrappedKey == null)
                //                    {
                //                        return string.Join(
                //                            ".",
                //                            base64Header,
                //                            string.Empty,
                //                            Base64Url.Base64UrlEncode(nonce),
                //                            Base64Url.Base64UrlEncode(ciphertext),
                //                            Base64Url.Base64UrlEncode(tag));
                //                    }
                //                    else
                //                    {
                //                        return string.Join(
                //                            ".",
                //                            base64Header,
                //                            Base64Url.Base64UrlEncode(wrappedKey),
                //                            Base64Url.Base64UrlEncode(nonce),
                //                            Base64Url.Base64UrlEncode(ciphertext),
                //                            Base64Url.Base64UrlEncode(tag));
                //                    }
                //                }
                //                finally
                //                {
                //                    if (arrayCiphertextToReturnToPool != null)
                //                    {
                //                        ArrayPool<byte>.Shared.Return(arrayCiphertextToReturnToPool);
                //                    }
                //                }
                //#endif
            }
            catch (Exception ex)
            {
                throw new JsonWebTokenEncryptionFailedException(ErrorMessages.FormatInvariant(ErrorMessages.EncryptionFailed, encryptionAlgorithm, Key.Kid), ex);
            }
        }
    }
}