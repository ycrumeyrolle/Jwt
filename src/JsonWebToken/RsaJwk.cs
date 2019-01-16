﻿// Copyright (c) 2018 Yann Crumeyrolle. All rights reserved.
// Licensed under the MIT license. See the LICENSE file in the project root for more information.

using JsonWebToken.Internal;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace JsonWebToken
{
    /// <summary>
    /// Represents a RSA JSON Web Key as defined in https://tools.ietf.org/html/rfc7518#section-6.
    /// </summary>
    public sealed class RsaJwk : AsymmetricJwk
    {
        private string _dp;
        private string _dq;
        private string _e;
        private string _n;
        private string _p;
        private string _q;
        private string _qi;

        /// <summary>
        /// Initializes a new instance of <see cref="RsaJwk"/>.
        /// </summary>
        public RsaJwk()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RsaJwk"/>.
        /// </summary>
        public RsaJwk(RSAParameters rsaParameters)
        {
            RawD = rsaParameters.D;
            RawDP = rsaParameters.DP;
            RawDQ = rsaParameters.DQ;
            RawQI = rsaParameters.InverseQ;
            RawP = rsaParameters.P;
            RawQ = rsaParameters.Q;
            RawE = rsaParameters.Exponent;
            RawN = rsaParameters.Modulus;
        }

        private RsaJwk(byte[] e, byte[] n)
            : this()
        {
            RawE = CloneByteArray(e);
            RawN = CloneByteArray(n);
        }

        /// <inheritsdoc />
        public override string Kty => JwkTypeNames.Rsa;

        /// <summary>
        /// Exports the RSA parameters from the <see cref="RsaJwk"/>.
        /// </summary>
        /// <returns></returns>
        public RSAParameters ExportParameters()
        {
            if (RawN == null || RawE == null)
            {
                Errors.ThrowInvalidRsaKey(this);
            }

            RSAParameters parameters = new RSAParameters
            {
                D = RawD,
                DP = RawDP,
                DQ = RawDQ,
                InverseQ = RawQI,
                P = RawP,
                Q = RawQ,
                Exponent = RawE,
                Modulus = RawN
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
        public override bool HasPrivateKey => RawD != null && RawDP != null && RawDQ != null && RawP != null && RawQ != null && RawQI != null;

        /// <inheritsdoc />
        public override int KeySizeInBits => RawN?.Length != 0 ? RawN.Length << 3 : 0;

        /// <summary>
        /// Gets or sets the 'dp' (First Factor CRT Exponent).
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, PropertyName = JwkParameterNames.DP, Required = Required.Default)]
        public string DP
        {
            get
            {
                if (_dp == null)
                {
                    if (RawDP != null && RawDP.Length != 0)
                    {
                        _dp = Base64Url.Base64UrlEncode(RawDP);
                    }
                }

                return _dp;
            }

            set
            {
                _dp = value;
                if (value != null)
                {
                    RawDP = Base64Url.Base64UrlDecode(value);
                }
                else
                {
                    RawDP = null;
                }
            }
        }

        /// <summary>
        /// Gets the 'dp' in its binary form.
        /// </summary>
        [JsonIgnore]
        public byte[] RawDP { get; private set; }

        /// <summary>
        /// Gets or sets the 'dq' (Second Factor CRT Exponent).
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, PropertyName = JwkParameterNames.DQ, Required = Required.Default)]
        public string DQ
        {
            get
            {
                if (_dq == null)
                {
                    if (RawDQ != null && RawDQ.Length != 0)
                    {
                        _dq = Base64Url.Base64UrlEncode(RawDQ);
                    }
                }

                return _dq;
            }

            set
            {
                _dq = value;
                if (value != null)
                {
                    RawDQ = Base64Url.Base64UrlDecode(value);
                }
                else
                {
                    RawDQ = null;
                }
            }
        }

        /// <summary>
        /// Gets the 'dq' in its binary form.
        /// </summary>
        [JsonIgnore]
        public byte[] RawDQ { get; private set; }

        /// <summary>
        /// Gets or sets the 'e' ( Exponent).
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, PropertyName = JwkParameterNames.E, Required = Required.Default)]
        public string E
        {
            get
            {
                if (_e == null)
                {
                    if (RawE != null && RawE.Length != 0)
                    {
                        _e = Base64Url.Base64UrlEncode(RawE);
                    }
                }

                return _e;
            }

            set
            {
                _e = value;
                if (value != null)
                {
                    RawE = Base64Url.Base64UrlDecode(value);
                }
                else
                {
                    RawE = null;
                }
            }
        }

        /// <summary>
        /// Gets the 'e' in its binary form.
        /// </summary>
        [JsonIgnore]
        public byte[] RawE { get; private set; }

        /// <summary>
        /// Gets or sets the 'n' (Modulus).
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, PropertyName = JwkParameterNames.N, Required = Required.Default)]
        public string N
        {
            get
            {
                if (_n == null)
                {
                    if (RawN != null && RawN.Length != 0)
                    {
                        _n = Base64Url.Base64UrlEncode(RawN);
                    }
                }

                return _n;
            }

            set
            {
                _n = value;
                if (value != null)
                {
                    RawN = Base64Url.Base64UrlDecode(value);
                }
                else
                {
                    RawN = null;
                }
            }
        }

        /// <summary>
        /// Gets the 'n' in its binary form.
        /// </summary>
        [JsonIgnore]
        public byte[] RawN { get; private set; }

        /// <summary>
        /// Gets or sets the 'oth' (Other Primes Info).
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, PropertyName = JwkParameterNames.Oth, Required = Required.Default)]
        public IList<string> Oth { get; set; }

        /// <summary>
        /// Gets or sets the 'p' (First Prime Factor).
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, PropertyName = JwkParameterNames.P, Required = Required.Default)]
        public string P
        {
            get
            {
                if (_p == null)
                {
                    if (RawP != null && RawP.Length != 0)
                    {
                        _p = Base64Url.Base64UrlEncode(RawP);
                    }
                }

                return _p;
            }

            set
            {
                _p = value;
                if (value != null)
                {
                    RawP = Base64Url.Base64UrlDecode(value);
                }
                else
                {
                    RawP = null;
                }
            }
        }

        /// <summary>
        /// Gets the 'p' in its binary form.
        /// </summary>
        [JsonIgnore]
        public byte[] RawP { get; private set; }

        /// <summary>
        /// Gets or sets the 'q' (Second  Prime Factor).
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, PropertyName = JwkParameterNames.Q, Required = Required.Default)]
        public string Q
        {
            get
            {
                if (_q == null)
                {
                    if (RawQ != null && RawQ.Length != 0)
                    {
                        _q = Base64Url.Base64UrlEncode(RawQ);
                    }
                }

                return _q;
            }

            set
            {
                _q = value;
                if (value != null)
                {
                    RawQ = Base64Url.Base64UrlDecode(value);
                }
                else
                {
                    RawQ = null;
                }
            }
        }

        /// <summary>
        /// Gets the 'q' in its binary form.
        /// </summary>
        [JsonIgnore]
        public byte[] RawQ { get; private set; }

        /// <summary>
        /// Gets or sets the 'qi' (First CRT Coefficient).
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, PropertyName = JwkParameterNames.QI, Required = Required.Default)]
        public string QI
        {
            get
            {
                if (_qi == null)
                {
                    if (RawQI != null && RawQI.Length != 0)
                    {
                        _qi = Base64Url.Base64UrlEncode(RawQI);
                    }
                }

                return _qi;
            }

            set
            {
                _qi = value;
                if (value != null)
                {
                    RawQI = Base64Url.Base64UrlDecode(value);
                }
                else
                {
                    RawQI = null;
                }
            }
        }

        /// <summary>
        /// Gets the 'qi' in its binary form.
        /// </summary>
        [JsonIgnore]
        public byte[] RawQI { get; private set; }

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
        /// <param name="parameters">A <see cref="byte"/> that contains the key parameters.</param>
        /// <param name="computeThumbprint">Defines whether the thumbprint of the key should be computed </param>
        public static RsaJwk FromParameters(RSAParameters parameters, bool computeThumbprint)
        {
            var key = new RsaJwk(parameters);
            if (computeThumbprint)
            {
                key.Kid = key.ComputeThumbprint(false);
            }

            return key;
        }

        /// <summary>
        /// Returns a new instance of <see cref="RsaJwk"/>.
        /// </summary>
        /// <param name="parameters">A <see cref="byte"/> that contains the key parameters.</param>
        public static RsaJwk FromParameters(RSAParameters parameters) => FromParameters(parameters, false);

        /// <inheritsdoc />
        public override Jwk Canonicalize()
        {
            return new RsaJwk(RawE, RawN);
        }

        /// <inheritsdoc />
        public override byte[] ToByteArray()
        {
            throw new NotImplementedException();
        }
    }
}
