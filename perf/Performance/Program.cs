﻿using JsonWebToken;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Performance
{
    internal class Program
    {
        private static readonly SymmetricJwk _key = SymmetricJwk.FromBase64Url("U1oK6e4BAR4kKTdyA1OqEFYwX9pIrswuUMNt8qW4z-k");
        private static readonly SymmetricJwk _keyToWrap = SymmetricJwk.FromBase64Url("gXoKEcss-xFuZceE6B3VkEMLw-f0h9tGfyaheF5jqP8");

        private const string Token1 = "eyJhbGciOiJIUzI1NiJ9.eyJqdGkiOiI3NTZFNjk3MTc1NjUyMDY5NjQ2NTZFNzQ2OTY2Njk2NTcyIiwiaXNzIjoiaHR0cHM6Ly9pZHAuZXhhbXBsZS5jb20vIiwiaWF0IjoxNTA4MTg0ODQ1LCJhdWQiOiI2MzZDNjk2NTZFNzQ1RjY5NjQiLCJleHAiOjE2MjgxODQ4NDV9.i2JGGP64mggd3WqUj7oX8_FyYh9e_m1MNWI9Q-f-W3g";
        private static readonly Jwk SharedKey = new SymmetricJwk("GdaXeVyiJwKmz5LFhcbcng")
        {
            Use = "sig",
            Kid = "kid-hs256",
            Alg = SignatureAlgorithm.HmacSha256.Name
        };
        private static readonly Jwk EncryptionKey = SymmetricJwk.GenerateKey(256, KeyManagementAlgorithm.Aes256KW);
        private static readonly JwtReader _reader = new JwtReader(SharedKey, EncryptionKey);
        private static readonly JwtWriter _writer = new JwtWriter();
        private static readonly TokenValidationPolicy policy = new TokenValidationPolicyBuilder()
                    .RequireSignature(SharedKey)
                    .Build();

        static IDictionary<string, object> dictionary = new Dictionary<string, object>()
        {
            { "sub", "sub value" },
            { "jti", "1234567890" },
            { "exp", 12345678 },
            //{ "aud", new JArray(new[] { "https://example.org", "abcdef" }) },
            { "nbf", 23456789 }
        };

        static JObject json = JObject.FromObject(dictionary);

        private static void Main(string[] args)
        {
            Console.WriteLine("Starting...");
            //int size = System.Runtime.InteropServices.Marshal.SizeOf<SignatureAlgorithm>();
            //for (int i = 0; i < 5000000; i++)
            //{
            //    var result = _reader.TryReadToken(Token1, parameters);
            //}

            //foreach (var token in new [] { "enc-small" })
            //{
            //    var result = _reader.TryReadToken(Tokens.ValidTokens[token].AsSpan(), new TokenValidationBuilder().RequireSignature(Tokens.SigningKey).Build());

            //}
            var expires = new DateTime(2033, 5, 18, 5, 33, 20, DateTimeKind.Utc);
            var issuedAt = new DateTime(2017, 7, 14, 4, 40, 0, DateTimeKind.Utc);
            var jws = new JwsDescriptor()
            {
                IssuedAt = issuedAt,
                ExpirationTime = expires,
                Issuer = "https://idp.example.com/",
                Audience = "636C69656E745F6964",
                Key = SharedKey
            };
            var jwe = new JweDescriptor
            {
                Key = EncryptionKey,
                EncryptionAlgorithm = EncryptionAlgorithm.Aes128CbcHmacSha256,
                Payload = jws
            };
            var jwX = new PlaintextJweDescriptor("Hello world !");
            var jwB = new BinaryJweDescriptor(Encoding.UTF8.GetBytes("Hello world !"));
            var jwt = _writer.WriteToken(jws);
            _reader.EnableHeaderCaching = false;
            _writer.EnableHeaderCaching = false;
            while (true)
            {
                jwt = _writer.WriteToken(jws);
                //var result = _reader.TryReadToken(jwt.AsSpan(), policy);
            }

            //while (true)
            //{
            //    var jwt = _writer.WriteToken(jws);
            //}

            //var keyData = new byte[32];
            //RandomNumberGenerator.Fill(keyData);
            //var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            //var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(keyData);
            //key.CryptoProviderFactory.CacheSignatureProviders = true;
            //Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            //var signingKey = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, "HS256");
            //handler.CreateEncodedJwt(new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor());
            //while (true)
            //{
            //    handler.CreateJwtSecurityToken(issuer: "me", signingCredentials: signingKey);
            //}
            ////});

            //var kwp = new AesKeyWrapProvider(_key, CryptographicAlgorithm.Aes128CbcHmacSha256, SignatureAlgorithm.Aes256KW);
            //byte[] wrappedKey = new byte[kwp.GetKeyWrapSize()];
            //var unwrappedKey = new byte[kwp.GetKeyUnwrapSize(wrappedKey.Length)];
            //while (true)
            //{
            //    var wrapped = kwp.TryWrapKey(_keyToWrap, null, wrappedKey, out var cek, out var bytesWritten);
            //    var unwrapped = kwp.TryUnwrapKey(wrappedKey, unwrappedKey, null, out int keyWrappedBytesWritten);
            //}
        }
    }
}

