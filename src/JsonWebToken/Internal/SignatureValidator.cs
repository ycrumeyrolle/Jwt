﻿// Copyright (c) 2018 Yann Crumeyrolle. All rights reserved.
// Licensed under the MIT license. See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace JsonWebToken.Internal
{
    internal sealed class SignatureValidator : IValidator
    {
        private readonly IKeyProvider _keyProvider;
        private readonly bool _supportUnsecure;
        private readonly SignatureAlgorithm _algorithm;

        public SignatureValidator(IKeyProvider keyProvider, bool supportUnsecure, SignatureAlgorithm algorithm)
        {
            _keyProvider = keyProvider;
            _supportUnsecure = supportUnsecure;
            _algorithm = algorithm;
        }

        /// <inheritdoc />
        public TokenValidationResult TryValidate(in TokenValidationContext context)
        {
            var jwt = context.Jwt;
            if (context.ContentSegment.Length == 0 && context.SignatureSegment.Length == 0)
            {
                // This is not a JWS
                return TokenValidationResult.Success(jwt);
            }

            var token = context.Token;
            if (token.Length <= context.ContentSegment.Length + 1)
            {
                if (_supportUnsecure && jwt.SignatureAlgorithm == SignatureAlgorithm.None)
                {
                    return TokenValidationResult.Success(jwt);
                }

                return TokenValidationResult.MissingSignature(jwt);
            }

            int signatureBytesLength;
            var signatureSegment = token.Slice(context.SignatureSegment.Start);
            try
            {
                signatureBytesLength = Base64Url.GetArraySizeRequiredToDecode(signatureSegment);
            }
            catch (FormatException)
            {
                return TokenValidationResult.MalformedSignature();
            }

            Span<byte> signatureBytes = stackalloc byte[signatureBytesLength];
            try
            {
                Base64Url.Base64UrlDecode(signatureSegment, signatureBytes, out int byteConsumed, out int bytesWritten);
                Debug.Assert(bytesWritten == signatureBytes.Length);
            }
            catch (FormatException)
            {
                return TokenValidationResult.MalformedSignature();
            }

            bool keysTried = false;
            var encodedBytes = token.Slice(context.ContentSegment.Start, context.ContentSegment.Length);
            var keys = ResolveSigningKey(jwt);
            for (int i = 0; i < keys.Count; i++)
            {
                Jwk key = keys[i];
                var alg = _algorithm != SignatureAlgorithm.Empty ? _algorithm : (SignatureAlgorithm)key.Alg;
                if (TryValidateSignature(context, encodedBytes, signatureBytes, key, alg))
                {
                    jwt.SigningKey = key;
                    return TokenValidationResult.Success(jwt);
                }

                keysTried = true;
            }

            if (keysTried)
            {
                return TokenValidationResult.InvalidSignature(jwt);
            }

            return TokenValidationResult.KeyNotFound(jwt);
        }

        private static bool TryValidateSignature(in TokenValidationContext context, ReadOnlySpan<byte> encodedBytes, ReadOnlySpan<byte> signature, Jwk key, SignatureAlgorithm algorithm)
        {
            var signatureProvider = context.SignatureFactory.Create(key, algorithm, willCreateSignatures: false);
            if (signatureProvider == null)
            {
                return false;
            }

            return signatureProvider.Verify(encodedBytes, signature);
        }

        private List<Jwk> ResolveSigningKey(Jwt jwt)
        {
            var keys = new List<Jwk>(1);
            var keySet = _keyProvider.GetKeys(jwt.Header);
            if (keySet != null)
            {
                for (int j = 0; j < keySet.Count; j++)
                {
                    var key = keySet[j];
                    if ((string.IsNullOrEmpty(key.Use) || string.Equals(key.Use, JsonWebKeyUseNames.Sig, StringComparison.Ordinal)) &&
                        (string.IsNullOrEmpty(key.Alg) || string.Equals(key.Alg, jwt.Header.Alg, StringComparison.Ordinal)))
                    {
                        keys.Add(key);
                    }
                }
            }

            return keys;
        }
    }
}
