﻿using System.Collections.Generic;
using System.Security.Claims;
using BenchmarkDotNet.Attributes;

namespace JsonWebToken.Performance
{
    [Config(typeof(DefaultCoreConfig))]
    [BenchmarkCategory("CI-CD")]
    public class ValidateUnsignedToken : ValidateToken
    {
        [GlobalSetup]
        public void Setup()
        {
            Jwt("JWT-empty");
            Wilson("JWT-empty");
            WilsonJwt("JWT-empty");
        }

        [Benchmark(Baseline = true)]
        [ArgumentsSource(nameof(GetTokens))]
        public override TokenValidationResult Jwt(string token)
        {
            return JwtCore(token, token.Contains("empty") ? TokenValidationPolicy.NoValidation : tokenValidationPolicyWithoutSignature);
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetTokens))]
        public override ClaimsPrincipal Wilson(string token)
        {
            return WilsonCore(token, token.Contains("empty") ? wilsonParametersWithoutValidation : wilsonParametersWithouSignature);
        }

        //[Benchmark]
        [ArgumentsSource(nameof(GetTokens))]
        public override Microsoft.IdentityModel.JsonWebTokens.TokenValidationResult WilsonJwt(string token)
        {
            return WilsonJwtCore(token, token.Contains("empty") ? wilsonParametersWithoutValidation : wilsonParametersWithouSignature);
        }

        public IEnumerable<string> GetTokens()
        {
            //yield return "JWT-empty";
            yield return "JWT-small";
            yield return "JWT-medium";
            //yield return "JWT-big";
        }
    }
}
