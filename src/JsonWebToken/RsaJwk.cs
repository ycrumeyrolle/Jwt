﻿// Copyright (c) 2018 Yann Crumeyrolle. All rights reserved.
// Licensed under the MIT license. See the LICENSE file in the project root for more information.

using JsonWebToken.Internal;
using System;
using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;

namespace JsonWebToken
{
    /// <summary>
    /// Represents a RSA JSON Web Key as defined in https://tools.ietf.org/html/rfc7518#section-6.
    /// </summary>
    public sealed class RsaJwk : AsymmetricJwk
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RsaJwk"/>.
        /// </summary>
        public RsaJwk(
            byte[] n,
            byte[] e,
            byte[] d,
            byte[] p,
            byte[] q,
            byte[] dp,
            byte[] dq,
            byte[] qi)
        {
            D = d ?? throw new ArgumentNullException(nameof(d));
            DP = dp ?? throw new ArgumentNullException(nameof(dp));
            DQ = dq ?? throw new ArgumentNullException(nameof(dq));
            QI = qi ?? throw new ArgumentNullException(nameof(qi));
            P = p ?? throw new ArgumentNullException(nameof(p));
            Q = q ?? throw new ArgumentNullException(nameof(q));
            E = e ?? throw new ArgumentNullException(nameof(e));
            N = n ?? throw new ArgumentNullException(nameof(n));
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RsaJwk"/>.
        /// </summary>
        public RsaJwk(
            string d,
            string p,
            string q,
            string dp,
            string dq,
            string qi,
            string e,
            string n)
        {
            if (d == null)
            {
                throw new ArgumentNullException(nameof(d));
            }

            if (p == null)
            {
                throw new ArgumentNullException(nameof(p));
            }

            if (q == null)
            {
                throw new ArgumentNullException(nameof(q));
            }

            if (dp == null)
            {
                throw new ArgumentNullException(nameof(dp));
            }

            if (dq == null)
            {
                throw new ArgumentNullException(nameof(dq));
            }

            if (qi == null)
            {
                throw new ArgumentNullException(nameof(qi));
            }

            if (e == null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            if (n == null)
            {
                throw new ArgumentNullException(nameof(n));
            }

            D = Base64Url.Base64UrlDecode(d);
            DP = Base64Url.Base64UrlDecode(dp);
            DQ = Base64Url.Base64UrlDecode(dq);
            QI = Base64Url.Base64UrlDecode(qi);
            P = Base64Url.Base64UrlDecode(p);
            Q = Base64Url.Base64UrlDecode(q);
            E = Base64Url.Base64UrlDecode(e);
            N = Base64Url.Base64UrlDecode(n);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RsaJwk"/>.
        /// </summary>
        public RsaJwk(RSAParameters rsaParameters)
        {
            D = rsaParameters.D;
            DP = rsaParameters.DP;
            DQ = rsaParameters.DQ;
            QI = rsaParameters.InverseQ;
            P = rsaParameters.P;
            Q = rsaParameters.Q;
            E = rsaParameters.Exponent;
            N = rsaParameters.Modulus;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RsaJwk"/>.
        /// </summary>
        public RsaJwk(byte[] e, byte[] n)
        {
            E = e ?? throw new ArgumentNullException(nameof(e));
            N = n ?? throw new ArgumentNullException(nameof(n));
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RsaJwk"/>.
        /// </summary>
        public RsaJwk(string e, string n)
        {
            if (e == null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            if (n == null)
            {
                throw new ArgumentNullException(nameof(n));
            }

            E = Base64Url.Base64UrlDecode(e);
            N = Base64Url.Base64UrlDecode(n);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RsaJwk"/>.
        /// </summary>
        public RsaJwk()
        {
        }

        /// <inheritsdoc />
        public override ReadOnlySpan<byte> Kty => JwkTypeNames.Rsa;

        /// <summary>
        /// Exports the RSA parameters from the <see cref="RsaJwk"/>.
        /// </summary>
        /// <returns></returns>
        public RSAParameters ExportParameters()
        {
            if (N == null || E == null)
            {
                Errors.ThrowInvalidRsaKey(this);
            }

            RSAParameters parameters = new RSAParameters
            {
                D = D,
                DP = DP,
                DQ = DQ,
                InverseQ = QI,
                P = P,
                Q = Q,
                Exponent = E,
                Modulus = N
            };

            return parameters;
        }

        /// <inheritsdoc />
        public override bool IsSupported(SignatureAlgorithm algorithm)
        {
            return algorithm.Category == AlgorithmCategory.Rsa;
        }

        /// <inheritsdoc />
        public override bool IsSupported(KeyManagementAlgorithm algorithm)
        {
            return algorithm.Category == AlgorithmCategory.Rsa;
        }

        /// <inheritsdoc />
        public override bool IsSupported(EncryptionAlgorithm algorithm)
        {
            return false;
        }

        /// <inheritsdoc />
        public override Signer CreateSigner(SignatureAlgorithm algorithm, bool willCreateSignatures)
        {
            if (algorithm is null)
            {
                return null;
            }

            if (IsSupported(algorithm))
            {
                return new RsaSigner(this, algorithm, willCreateSignatures);
            }

            return null;
        }

        /// <inheritsdoc />
        public override KeyWrapper CreateKeyWrapper(EncryptionAlgorithm encryptionAlgorithm, KeyManagementAlgorithm contentEncryptionAlgorithm)
        {
            if (IsSupported(contentEncryptionAlgorithm))
            {
                return new RsaKeyWrapper(this, encryptionAlgorithm, contentEncryptionAlgorithm);
            }

            return null;
        }

        /// <inheritsdoc />
        public override bool HasPrivateKey => D != null && DP != null && DQ != null && P != null && Q != null && QI != null;

        /// <inheritsdoc />
        public override int KeySizeInBits => N?.Length != 0 ? N.Length << 3 : 0;

        /// <summary>
        /// Gets or sets the 'dp' (First Factor CRT Exponent).
        /// </summary>
        public byte[] DP { get; set; }

        /// <summary>
        /// Gets or sets the 'dq' (Second Factor CRT Exponent).
        /// </summary>
        public byte[] DQ { get; set; }

        /// <summary>
        /// Gets or sets the 'e' ( Exponent).
        /// </summary>
        public byte[] E { get; set; }

        /// <summary>
        /// Gets or sets the 'n' (Modulus).
        /// </summary>
        public byte[] N { get; set; }

        /// <summary>
        /// Gets or sets the 'p' (First Prime Factor).
        /// </summary>
        public byte[] P { get; set; }

        /// <summary>
        /// Gets or sets the 'q' (Second  Prime Factor).
        /// </summary>
        public byte[] Q { get; set; }

        /// <summary>
        /// Gets or sets the 'qi' (First CRT Coefficient).
        /// </summary>
        public byte[] QI { get; set; }

        /// <summary>
        /// Generates a new RSA key.
        /// </summary>
        /// <param name="sizeInBits">The key size in bits.</param>
        /// <param name="withPrivateKey"></param>
        /// <returns></returns>
        public static RsaJwk GenerateKey(int sizeInBits, bool withPrivateKey) => GenerateKey(sizeInBits, withPrivateKey, null);

        /// <summary>
        /// Generates a new random <see cref="RsaJwk"/>.
        /// </summary>
        /// <param name="sizeInBits"></param>
        /// <param name="withPrivateKey"></param>
        /// <param name="algorithm"></param>
        /// <returns></returns>
        public static RsaJwk GenerateKey(int sizeInBits, bool withPrivateKey, string algorithm)
        {
#if NETSTANDARD2_0
            using (RSA rsa = new RSACng())
#else
            using (RSA rsa = RSA.Create())
#endif
            {
                rsa.KeySize = sizeInBits;
                RSAParameters rsaParameters = rsa.ExportParameters(withPrivateKey);

                var key = FromParameters(rsaParameters, false);
                if (algorithm != null)
                {
                    key.Alg = algorithm;
                }

                return key;
            }
        }

        /// <summary>
        /// Returns a new instance of <see cref="RsaJwk"/>.
        /// </summary>
        /// <param name="parameters">A <see cref="RSAParameters"/> that contains the key parameters.</param>
        /// <param name="computeThumbprint">Defines whether the thumbprint of the key should be computed </param>
        public static RsaJwk FromParameters(RSAParameters parameters, bool computeThumbprint)
        {
            var key = new RsaJwk(parameters);
            if (computeThumbprint)
            {
                key.Kid = key.ComputeThumbprint();
            }

            return key;
        }

        /// <summary>
        /// Returns a new instance of <see cref="RsaJwk"/>.
        /// </summary>
        /// <param name="parameters">A <see cref="RSAParameters"/> that contains the key parameters.</param>
        public static RsaJwk FromParameters(RSAParameters parameters) => FromParameters(parameters, false);

        /// <inheritdoc />
        public override byte[] Canonicalize()
        {
            using (var bufferWriter = new ArrayBufferWriter<byte>())
            {
                Utf8JsonWriter writer = new Utf8JsonWriter(bufferWriter, new JsonWriterState(new JsonWriterOptions { Indented = false, SkipValidation = true }));
                writer.WriteStartObject();
                writer.WriteString(JwkParameterNames.EUtf8, Base64Url.Base64UrlEncode(E));
                writer.WriteString(JwkParameterNames.KtyUtf8, Kty);
                writer.WriteString(JwkParameterNames.NUtf8, Base64Url.Base64UrlEncode(N));
                writer.WriteEndObject();
                writer.Flush();

                return bufferWriter.WrittenSpan.ToArray();
            }
        }

        /// <inheritsdoc />
        public override byte[] ToByteArray()
        {
            throw new NotImplementedException();
        }

        internal static RsaJwk Populate(JwtObject @object)
        {
            var key = new RsaJwk();
            for (int i = 0; i < @object.Count; i++)
            {
                var property = @object[i];
                var name = property.Utf8Name.Span;
                switch (property.Type)
                {
                    case JwtTokenType.Array:
                        key.Populate(name, (JwtArray)property.Value);
                        break;
                    case JwtTokenType.String:
                        key.Populate(name, (string)property.Value);
                        break;
                    case JwtTokenType.Utf8String:
                        if (name.SequenceEqual(JwkParameterNames.NUtf8))
                        {
                            key.N = Base64Url.Base64UrlDecode( (string)property.Value);
                        }
                        else if (name.SequenceEqual(JwkParameterNames.EUtf8))
                        {
                            key.E = Base64Url.Base64UrlDecode((string)property.Value);
                        }
                        else if (name.SequenceEqual(JwkParameterNames.DUtf8))
                        {
                            key.D = Base64Url.Base64UrlDecode((string)property.Value);
                        }
                        else if (name.SequenceEqual(JwkParameterNames.DPUtf8))
                        {
                            key.DP = Base64Url.Base64UrlDecode((string)property.Value);
                        }
                        else if (name.SequenceEqual(JwkParameterNames.DQUtf8))
                        {
                            key.DQ = Base64Url.Base64UrlDecode((string)property.Value);
                        }
                        else if (name.SequenceEqual(JwkParameterNames.PUtf8))
                        {
                            key.P = Base64Url.Base64UrlDecode((string)property.Value);
                        }
                        else if (name.SequenceEqual(JwkParameterNames.QUtf8))
                        {
                            key.Q = Base64Url.Base64UrlDecode((string)property.Value);
                        }
                        else if (name.SequenceEqual(JwkParameterNames.QIUtf8))
                        {
                            key.QI = Base64Url.Base64UrlDecode((string)property.Value);
                        }
                        else
                        {
                            key.Populate(name, (string)property.Value);
                        }
                        break;
                    default:
                        break;
                }
            }

            return key;
        }

        internal unsafe static Jwk FromJsonReaderFast(ref Utf8JsonReader reader)
        {
            var key = new RsaJwk();

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:

                        var propertyName = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
                        fixed (byte* pPropertyName = propertyName)
                        {
                            reader.Read();
                            switch (reader.TokenType)
                            {
                                case JsonTokenType.StartObject:
                                    PopulateObject(ref reader, pPropertyName, propertyName.Length, key);
                                    break;
                                case JsonTokenType.StartArray:
                                    PopulateArray(ref reader, pPropertyName, propertyName.Length, key);
                                    break;
                                case JsonTokenType.String:
                                    switch (propertyName.Length)
                                    {
                                        case 1:
                                            if (*pPropertyName == (byte)'e')
                                            {
                                                key.E = Base64Url.Base64UrlDecode(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan);
                                            }
                                            else if (*pPropertyName == (byte)'n')
                                            {
                                                key.N = Base64Url.Base64UrlDecode(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan);
                                            }
                                            else if (*pPropertyName == (byte)'p')
                                            {
                                                key.P = Base64Url.Base64UrlDecode(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan);
                                            }
                                            else if (*pPropertyName == (byte)'q')
                                            {
                                                key.Q = Base64Url.Base64UrlDecode(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan);
                                            }
                                            break;

                                        case 2:
                                            var pKtyShort = (short*)pPropertyName;
                                            if (*pKtyShort == 26993u)
                                            {
                                                key.QI = Base64Url.Base64UrlDecode(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan);
                                            }
                                            else if (*pKtyShort == 28772u)
                                            {
                                                key.DP = Base64Url.Base64UrlDecode(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan);
                                            }
                                            else if (*pKtyShort == 29028u)
                                            {
                                                key.DQ = Base64Url.Base64UrlDecode(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan);
                                            }
                                            break;
                                        case 3:
                                            PopulateThree(ref reader, pPropertyName, key);
                                            break;
                                        case 8:
                                            PopulateEight(ref reader, pPropertyName, key);
                                            break;
                                        default:
                                            break;
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                        break;
                    case JsonTokenType.EndObject:
                        return key;
                    default:
                        break;
                }
            }

            Errors.ThrowMalformedKey();
            return null;
        }

        internal override void WriteComplementTo(ref Utf8JsonWriter writer)
        {
            writer.WriteString(JwkParameterNames.NUtf8, Base64Url.Base64UrlEncode(N));
            writer.WriteString(JwkParameterNames.EUtf8, Base64Url.Base64UrlEncode(E));
            if (D != null)
            {
                writer.WriteString(JwkParameterNames.DUtf8, Base64Url.Base64UrlEncode(D));
            }

            if (DP != null)
            {
                writer.WriteString(JwkParameterNames.DPUtf8, Base64Url.Base64UrlEncode(DP));
            }

            if (DQ != null)
            {
                writer.WriteString(JwkParameterNames.DQUtf8, Base64Url.Base64UrlEncode(DQ));
            }

            if (P != null)
            {
                writer.WriteString(JwkParameterNames.PUtf8, Base64Url.Base64UrlEncode(P));
            }

            if (Q != null)
            {
                writer.WriteString(JwkParameterNames.QUtf8, Base64Url.Base64UrlEncode(Q));
            }

            if (QI != null)
            {
                writer.WriteString(JwkParameterNames.QIUtf8, Base64Url.Base64UrlEncode(QI));
            }
        }
    }
}
