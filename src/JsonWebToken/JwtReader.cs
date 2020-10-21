﻿// Copyright (c) 2020 Yann Crumeyrolle. All rights reserved.
// Licensed under the MIT license. See LICENSE in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using JsonWebToken.Internal;

namespace JsonWebToken
{
    /// <summary>
    /// Reads and validates a JWT.
    /// </summary>
    public sealed partial class JwtReader
    {
        private readonly IKeyProvider[] _encryptionKeyProviders;
        private readonly JwtHeaderCache _headerCache = new JwtHeaderCache();

        /// <summary>
        /// Initializes a new instance of <see cref="JwtReader"/>.
        /// </summary>
        /// <param name="encryptionKeyProviders"></param>
        public JwtReader(ICollection<IKeyProvider> encryptionKeyProviders)
        {
            if (encryptionKeyProviders is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.encryptionKeyProviders);
            }

            _encryptionKeyProviders = encryptionKeyProviders.Where(p => p != null).ToArray();
        }

        /// <summary>
        /// Initializes a new instance of <see cref="JwtReader"/>.
        /// </summary>
        /// <param name="encryptionKeys"></param>
        public JwtReader(params Jwk[] encryptionKeys)
           : this(new Jwks(encryptionKeys))
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="JwtReader"/>.
        /// </summary>
        /// <param name="encryptionKeyProvider"></param>
        public JwtReader(IKeyProvider encryptionKeyProvider)
            : this(new[] { encryptionKeyProvider })
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="JwtReader"/>.
        /// </summary>
        /// <param name="encryptionKeys"></param>
        public JwtReader(Jwks encryptionKeys)
            : this(new StaticKeyProvider(encryptionKeys))
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="JwtReader"/>.
        /// </summary>
        /// <param name="encryptionKey"></param>
        public JwtReader(Jwk encryptionKey)
            : this(new Jwks(encryptionKey))
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="JwtReader"/>.
        /// </summary>
        public JwtReader()
            : this(Array.Empty<IKeyProvider>())
        {
        }

        /// <summary>
        /// Defines whether the header will be cached. Default is <c>true</c>.
        /// </summary>
        public bool EnableHeaderCaching { get; set; } = true;

        /// <summary>
        /// Reads and validates a JWT encoded as a JWS or JWE in compact serialized format.
        /// </summary>
        /// <param name="token">The JWT encoded as JWE or JWS</param>
        /// <param name="policy">The validation policy.</param>
        public TokenValidationResult TryReadToken(string token, TokenValidationPolicy policy)
        {
            if (token is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.token);
            }

            if (policy is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.policy);
            }

            if (token.Length == 0)
            {
                return TokenValidationResult.MalformedToken();
            }

            int length = Utf8.GetMaxByteCount(token.Length);
            if (length > policy.MaximumTokenSizeInBytes)
            {
                return TokenValidationResult.MalformedToken();
            }

            byte[]? utf8ArrayToReturnToPool = null;
            var utf8Token = length <= Constants.MaxStackallocBytes
                  ? stackalloc byte[length]
                  : (utf8ArrayToReturnToPool = ArrayPool<byte>.Shared.Rent(length));
            try
            {
                int bytesWritten = Utf8.GetBytes(token, utf8Token);
                return TryReadToken(utf8Token.Slice(0, bytesWritten), policy);
            }
            finally
            {
                if (utf8ArrayToReturnToPool != null)
                {
                    ArrayPool<byte>.Shared.Return(utf8ArrayToReturnToPool);
                }
            }
        }

        /// <summary>
        /// Reads and validates a JWT encoded as a JWS or JWE in compact serialized format.
        /// </summary>
        /// <param name="utf8Token">The JWT encoded as JWE or JWS.</param>
        /// <param name="policy">The validation policy.</param>
        public TokenValidationResult TryReadToken(in ReadOnlySequence<byte> utf8Token, TokenValidationPolicy policy)
        {
            if (utf8Token.IsSingleSegment)
            {
                return TryReadToken(utf8Token.First.Span, policy);
            }

            return TryReadToken(utf8Token.ToArray(), policy);
        }

        /// <summary>
        /// Reads and validates a JWT encoded as a JWS or JWE in compact serialized format.
        /// </summary>
        /// <param name="utf8Token">The JWT encoded as JWE or JWS.</param>
        /// <param name="policy">The validation policy.</param>
        public TokenValidationResult TryReadToken(ReadOnlySpan<byte> utf8Token, TokenValidationPolicy policy)
        {
            if (policy is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.policy);
            }

            TokenValidationResult result;
            if (utf8Token.IsEmpty)
            {
                result = TokenValidationResult.MalformedToken();
                goto TokenAnalyzed;
            }

            if (utf8Token.Length > policy.MaximumTokenSizeInBytes)
            {
                result = TokenValidationResult.MalformedToken();
                goto TokenAnalyzed;
            }

            Span<TokenSegment> segments = stackalloc TokenSegment[Constants.JweSegmentCount];
            ref TokenSegment segmentsRef = ref MemoryMarshal.GetReference(segments);
            int segmentCount = Tokenizer.Tokenize(utf8Token, ref segmentsRef);
            if (segmentCount < Constants.JwsSegmentCount)
            {
                result = TokenValidationResult.MalformedToken();
                goto TokenAnalyzed;
            }

            var headerSegment = segmentsRef;
            if (headerSegment.IsEmpty)
            {
                result = TokenValidationResult.MalformedToken();
                goto TokenAnalyzed;
            }

            JwtHeader? header;
            var rawHeader = utf8Token.Slice(0, headerSegment.Length);
            int headerJsonDecodedLength = Base64Url.GetArraySizeRequiredToDecode(rawHeader.Length);
            int payloadjsonDecodedLength;
            int jsonBufferLength;
            if (segmentCount == Constants.JwsSegmentCount)
            {
                payloadjsonDecodedLength = Base64Url.GetArraySizeRequiredToDecode(Unsafe.Add(ref segmentsRef, 1).Length);
                jsonBufferLength = Math.Max(headerJsonDecodedLength, payloadjsonDecodedLength);
            }
            else
            {
                payloadjsonDecodedLength = 0;
                jsonBufferLength = headerJsonDecodedLength;
            }

            byte[]? jsonBufferToReturnToPool = null;
            var jsonBuffer = jsonBufferLength <= Constants.MaxStackallocBytes
              ? stackalloc byte[jsonBufferLength]
              : (jsonBufferToReturnToPool = ArrayPool<byte>.Shared.Rent(jsonBufferLength));
            try
            {
                if (EnableHeaderCaching)
                {
                    if (!_headerCache.TryGetHeader(rawHeader, out header))
                    {
                        header = GetJsonHeader(rawHeader, jsonBuffer.Slice(0, headerJsonDecodedLength), policy);
                        _headerCache.AddHeader(rawHeader, header);
                    }
                }
                else
                {
                    header = GetJsonHeader(rawHeader, jsonBuffer.Slice(0, rawHeader.Length), policy);
                }

                result = policy.TryValidateHeader(header);
                if (result.Succedeed)
                {
                    result = segmentCount switch
                    {
                        Constants.JwsSegmentCount => TryReadJws(utf8Token, jsonBuffer.Slice(0, payloadjsonDecodedLength), policy, ref segmentsRef, header),
                        Constants.JweSegmentCount => TryReadJwe(utf8Token, policy, rawHeader, ref segmentsRef, header),
                        _ => TokenValidationResult.MalformedToken(),
                    };
                }
            }
            catch (FormatException formatException)
            {
                result = TokenValidationResult.MalformedToken(formatException);
                goto TokenAnalyzed;
            }
            catch (JsonException readerException)
            {
                result = TokenValidationResult.MalformedToken(readerException);
                goto TokenAnalyzed;
            }
            catch (InvalidOperationException invalidOperationException) when (invalidOperationException.InnerException is DecoderFallbackException)
            {
                result = TokenValidationResult.MalformedToken(invalidOperationException);
                goto TokenAnalyzed;
            }
            finally
            {
                if (jsonBufferToReturnToPool != null)
                {
                    ArrayPool<byte>.Shared.Return(jsonBufferToReturnToPool);
                }
            }

        TokenAnalyzed:
            return result;
        }

        private TokenValidationResult TryReadJwe(
            ReadOnlySpan<byte> utf8Buffer,
            TokenValidationPolicy policy,
            ReadOnlySpan<byte> rawHeader,
            ref TokenSegment segments,
            JwtHeader header)
        {
            TokenSegment encryptionKeySegment = Unsafe.Add(ref segments, 1);
            TokenSegment ivSegment = Unsafe.Add(ref segments, 2);
            TokenSegment ciphertextSegment = Unsafe.Add(ref segments, 3);
            TokenSegment authenticationTagSegment = Unsafe.Add(ref segments, 4);
            var enc = header.EncryptionAlgorithm;
            if (enc is null)
            {
                return TokenValidationResult.MissingEncryptionAlgorithm();
            }

            if (!TryGetContentEncryptionKeys(header, utf8Buffer.Slice(encryptionKeySegment.Start, encryptionKeySegment.Length), enc, out var keys))
            {
                return TokenValidationResult.EncryptionKeyNotFound();
            }

            var rawInitializationVector = utf8Buffer.Slice(ivSegment.Start, ivSegment.Length);
            var rawCiphertext = utf8Buffer.Slice(ciphertextSegment.Start, ciphertextSegment.Length);
            var rawAuthenticationTag = utf8Buffer.Slice(authenticationTagSegment.Start, authenticationTagSegment.Length);

            int decryptedLength = Base64Url.GetArraySizeRequiredToDecode(rawCiphertext.Length);
            byte[]? decryptedArrayToReturnToPool = null;
            var decryptedBytes = decryptedLength <= Constants.MaxStackallocBytes
                  ? stackalloc byte[decryptedLength]
                  : (decryptedArrayToReturnToPool = ArrayPool<byte>.Shared.Rent(decryptedLength));

            try
            {
                if (TryDecryptToken(keys, rawHeader, rawCiphertext, rawInitializationVector, rawAuthenticationTag, enc, decryptedBytes, out SymmetricJwk? decryptionKey, out int bytesWritten))
                {
                    decryptedBytes = decryptedBytes.Slice(0, bytesWritten);
                }
                else
                {
                    return TokenValidationResult.DecryptionFailed();
                }

                bool compressed;
                ReadOnlySequence<byte> decompressedBytes = default;
                var zip = header.CompressionAlgorithm;
                if (zip is null)
                {
                    compressed = false;
                }
                else
                {
                    Compressor compressor = zip.Compressor;
                    if (compressor is null)
                    {
                        return TokenValidationResult.InvalidHeader(HeaderParameters.ZipUtf8);
                    }

                    try
                    {
                        compressed = true;
                        decompressedBytes = compressor.Decompress(decryptedBytes);
                    }
                    catch (Exception e)
                    {
                        return TokenValidationResult.DecompressionFailed(e);
                    }
                }

                Jwt jwe;
                if (policy.IgnoreNestedToken)
                {
                    jwe = new Jwt(header, compressed ? decompressedBytes.ToArray() : decryptedBytes.ToArray(), decryptionKey);
                }
                else
                {
                    var decryptionResult = compressed
                        ? TryReadToken(decompressedBytes, policy)
                        : TryReadToken(decryptedBytes, policy);
                    if (!(decryptionResult.Token is null) && decryptionResult.Succedeed)
                    {
                        jwe = new Jwt(header, decryptionResult.Token, decryptionKey);
                    }
                    else
                    {
                        if (decryptionResult.Status == TokenValidationStatus.MalformedToken && !policy.HasValidation)
                        {
                            // The decrypted payload is not a nested JWT
                            jwe = new Jwt(header, compressed ? decompressedBytes.ToArray() : decryptedBytes.ToArray(), decryptionKey);
                        }
                        else
                        {
                            return decryptionResult;
                        }
                    }
                }

                return TokenValidationResult.Success(jwe);
            }
            finally
            {
                if (decryptedArrayToReturnToPool != null)
                {
                    ArrayPool<byte>.Shared.Return(decryptedArrayToReturnToPool);
                }
            }
        }

        private static TokenValidationResult TryReadJws(
            ReadOnlySpan<byte> utf8Buffer,
            Span<byte> jsonBuffer,
            TokenValidationPolicy policy,
            ref TokenSegment segments,
            JwtHeader header)
        {
            TokenSegment headerSegment = segments;
            TokenSegment payloadSegment = Unsafe.Add(ref segments, 1);
            TokenSegment signatureSegment = Unsafe.Add(ref segments, 2);
            var rawPayload = utf8Buffer.Slice(payloadSegment.Start, payloadSegment.Length);
            var result = policy.TryValidateSignature(header, utf8Buffer.Slice(headerSegment.Start, headerSegment.Length + payloadSegment.Length + 1), utf8Buffer.Slice(signatureSegment.Start, signatureSegment.Length));
            if (!result.Succedeed)
            {
                return TokenValidationResult.SignatureValidationFailed(result);
            }

            Exception malformedException;
            JwtPayload payload;
            try
            {
                payload = GetJsonPayload(rawPayload, jsonBuffer, policy);
            }
            catch (FormatException formatException)
            {
                malformedException = formatException;
                goto Malformed;
            }
            catch (JsonException readerException)
            {
                malformedException = readerException;
                goto Malformed;
            }
            catch (InvalidOperationException invalidOperationException) when (invalidOperationException.InnerException is DecoderFallbackException)
            {
                malformedException = invalidOperationException;
                goto Malformed;
            }

            Jwt jws = new Jwt(header, payload, result.SigningKey);
            return policy.TryValidateJwt(jws);

        Malformed:
            return TokenValidationResult.MalformedToken(exception: malformedException);
        }

        private static JwtPayload GetJsonPayload(ReadOnlySpan<byte> data, Span<byte> buffer, TokenValidationPolicy policy)
        {
            Base64Url.Decode(data, buffer);
            return JwtPayloadParser.ParsePayload(buffer, policy);
        }

        private static JwtHeader GetJsonHeader(ReadOnlySpan<byte> data, Span<byte> buffer, TokenValidationPolicy policy)
        {
            Base64Url.Decode(data, buffer);
            return JwtHeaderParser.ParseHeader(buffer, policy);
        }

        private static bool TryDecryptToken(
            List<SymmetricJwk> keys,
            ReadOnlySpan<byte> rawHeader,
            ReadOnlySpan<byte> rawCiphertext,
            ReadOnlySpan<byte> rawInitializationVector,
            ReadOnlySpan<byte> rawAuthenticationTag,
            EncryptionAlgorithm encryptionAlgorithm,
            Span<byte> decryptedBytes,
            [NotNullWhen(true)] out SymmetricJwk? key,
            out int bytesWritten)
        {
            int ciphertextLength = Base64Url.GetArraySizeRequiredToDecode(rawCiphertext.Length);
            int initializationVectorLength = Base64Url.GetArraySizeRequiredToDecode(rawInitializationVector.Length);
            int authenticationTagLength = Base64Url.GetArraySizeRequiredToDecode(rawAuthenticationTag.Length);
            int headerLength = rawHeader.Length;
            int bufferLength = ciphertextLength + headerLength + initializationVectorLength + authenticationTagLength;
            byte[]? arrayToReturn = null;
            Span<byte> buffer = bufferLength < Constants.MaxStackallocBytes
                ? stackalloc byte[bufferLength]
                : (arrayToReturn = ArrayPool<byte>.Shared.Rent(bufferLength));

            Span<byte> ciphertext = buffer.Slice(0, ciphertextLength);
            Span<byte> header = buffer.Slice(ciphertextLength, headerLength);
            Span<byte> initializationVector = buffer.Slice(ciphertextLength + headerLength, initializationVectorLength);
            Span<byte> authenticationTag = buffer.Slice(ciphertextLength + headerLength + initializationVectorLength, authenticationTagLength);
            try
            {
                Base64Url.Decode(rawCiphertext, ciphertext, out int _, out int ciphertextBytesWritten);
                Debug.Assert(ciphertext.Length == ciphertextBytesWritten);

                char[]? headerArrayToReturn = null;
                try
                {
                    int utf8HeaderLength = Utf8.GetMaxCharCount(header.Length);
                    Span<char> utf8Header = utf8HeaderLength < Constants.MaxStackallocBytes
                        ? stackalloc char[utf8HeaderLength]
                        : (headerArrayToReturn = ArrayPool<char>.Shared.Rent(utf8HeaderLength));

                    utf8HeaderLength = Utf8.GetChars(rawHeader, utf8Header);
                    Ascii.GetBytes(utf8Header.Slice(0, utf8HeaderLength), header);
                }
                finally
                {
                    if (headerArrayToReturn != null)
                    {
                        ArrayPool<char>.Shared.Return(headerArrayToReturn);
                    }
                }

                Base64Url.Decode(rawInitializationVector, initializationVector, out int _, out int ivBytesWritten);
                Debug.Assert(initializationVector.Length == ivBytesWritten);

                Base64Url.Decode(rawAuthenticationTag, authenticationTag, out int _, out int authenticationTagBytesWritten);
                Debug.Assert(authenticationTag.Length == authenticationTagBytesWritten);

                bytesWritten = 0;
                var decryptor = encryptionAlgorithm.Decryptor;

                for (int i = 0; i < keys.Count; i++)
                {
                    key = keys[i];
                    if (decryptor.TryDecrypt(
                        key.K,
                        ciphertext,
                        header,
                        initializationVector,
                        authenticationTag,
                        decryptedBytes,
                        out bytesWritten))
                    {
                        return true;
                    }
                }

                key = null;
                return false;
            }
            finally
            {
                if (arrayToReturn != null)
                {
                    ArrayPool<byte>.Shared.Return(arrayToReturn);
                }
            }
        }

        private bool TryGetContentEncryptionKeys(JwtHeader header, ReadOnlySpan<byte> rawEncryptedKey, EncryptionAlgorithm enc, [NotNullWhen(true)] out List<SymmetricJwk>? keys)
        {
            KeyManagementAlgorithm? alg = header.KeyManagementAlgorithm;

            if (alg is null)
            {
                keys = null;
                return false;
            }
            else if (alg == KeyManagementAlgorithm.Direct)
            {
                keys = new List<SymmetricJwk>(1);
                for (int i = 0; i < _encryptionKeyProviders.Length; i++)
                {
                    var keySet = _encryptionKeyProviders[i].GetKeys(header);
                    for (int j = 0; j < keySet.Length; j++)
                    {
                        var key = keySet[j];
                        if (key is SymmetricJwk symJwk && symJwk.CanUseForKeyWrapping(alg))
                        {
                            keys.Add(symJwk);
                        }
                    }
                }
            }
            else
            {
                int decodedSize = Base64Url.GetArraySizeRequiredToDecode(rawEncryptedKey.Length);

                byte[]? encryptedKeyToReturnToPool = null;
                byte[]? unwrappedKeyToReturnToPool = null;
                Span<byte> encryptedKey = decodedSize <= Constants.MaxStackallocBytes ?
                    stackalloc byte[decodedSize] :
                    encryptedKeyToReturnToPool = ArrayPool<byte>.Shared.Rent(decodedSize);
                try
                {
                    var operationResult = Base64Url.Decode(rawEncryptedKey, encryptedKey, out _, out int bytesWritten);
                    Debug.Assert(operationResult == OperationStatus.Done);
                    encryptedKey = encryptedKey.Slice(0, bytesWritten);

                    var keyUnwrappers = new List<(int, KeyUnwrapper)>(1);
                    int maxKeyUnwrapSize = 0;
                    for (int i = 0; i < _encryptionKeyProviders.Length; i++)
                    {
                        var keySet = _encryptionKeyProviders[i].GetKeys(header);
                        for (int j = 0; j < keySet.Length; j++)
                        {
                            var key = keySet[j];
                            if (key.CanUseForKeyWrapping(alg))
                            {
                                if (key.TryGetKeyUnwrapper(enc, alg, out var keyUnwrapper))
                                {
                                    int keyUnwrapSize = keyUnwrapper.GetKeyUnwrapSize(encryptedKey.Length);
                                    keyUnwrappers.Add((keyUnwrapSize, keyUnwrapper));
                                    if (maxKeyUnwrapSize < keyUnwrapSize)
                                    {
                                        maxKeyUnwrapSize = keyUnwrapSize;
                                    }
                                }
                            }
                        }
                    }

                    keys = new List<SymmetricJwk>(1);
                    Span<byte> unwrappedKey = maxKeyUnwrapSize <= Constants.MaxStackallocBytes ?
                        stackalloc byte[maxKeyUnwrapSize] :
                        unwrappedKeyToReturnToPool = ArrayPool<byte>.Shared.Rent(maxKeyUnwrapSize);
                    for (int i = 0; i < keyUnwrappers.Count; i++)
                    {
                        var kpv = keyUnwrappers[i];
                        var temporaryUnwrappedKey = unwrappedKey.Length != kpv.Item1 ? unwrappedKey.Slice(0, kpv.Item1) : unwrappedKey;
                        if (kpv.Item2.TryUnwrapKey(encryptedKey, temporaryUnwrappedKey, header, out int keyUnwrappedBytesWritten))
                        {
                            var jwk = new SymmetricJwk(unwrappedKey.Slice(0, keyUnwrappedBytesWritten).ToArray());
                            keys.Add(jwk);
                        }
                    }
                }
                finally
                {
                    if (encryptedKeyToReturnToPool != null)
                    {
                        ArrayPool<byte>.Shared.Return(encryptedKeyToReturnToPool, true);
                    }

                    if (unwrappedKeyToReturnToPool != null)
                    {
                        ArrayPool<byte>.Shared.Return(unwrappedKeyToReturnToPool, true);
                    }
                }
            }

            return keys.Count != 0;
        }
    }
}