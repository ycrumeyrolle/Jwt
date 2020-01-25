﻿using BenchmarkDotNet.Attributes;
using System.Collections.Generic;

namespace JsonWebToken.Performance
{
    [Config(typeof(DefaultCoreConfig))]
    [BenchmarkCategory("CI-CD")]
    public class WriteCompressedToken : WriteToken
    {
        [GlobalSetup]
        public void Setup()
        {
            Jwt(new BenchmarkPayload("JWE-DEF-0"));
            Wilson(new BenchmarkPayload("JWE-DEF-0"));
            WilsonJwt(new BenchmarkPayload("JWE-DEF-0"));
        }

        [Benchmark(Baseline = true)]
        [ArgumentsSource(nameof(GetPayloadValues))]
        public override byte[] Jwt(BenchmarkPayload payload)
        {
            return JwtCore(payload.JwtDescriptor);
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetPayloadValues))]
        public override string Wilson(BenchmarkPayload payload)
        {
            return WilsonCore(payload.WilsonDescriptor);
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetPayloadValues))]
        public override string WilsonJwt(BenchmarkPayload payload)
        {
            return WilsonJweCompressedCore(payload.WilsonJwtDescriptor);
        }

        [Benchmark]
        [ArgumentsSource(nameof(GetPayloadValues))]
        public override string JoseDotNet(BenchmarkPayload payload)
        {
            return JoseDotNetJweCompressedCore(payload.JoseDescriptor);
        }

        public override string JwtDotNet(BenchmarkPayload payload)
        {
            throw new System.NotImplementedException();
        }

        public override IEnumerable<string> GetPayloads()
        {
            for (int i = 0; i < 10; i++)
            {
                yield return "JWE-DEF-" + i;
            }
        }
    }
}