﻿namespace JsonWebToken
{
    public class PlaintextJweDescriptor : EncodedJwtDescriptor<string>
    {
        public override string Encode()
        {
            return EncryptToken(Payload);
        }
    }
}