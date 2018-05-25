﻿using System;
using System.Collections.Generic;

namespace JsonWebToken
{
    public interface IJwtPayloadDescriptor
    {
        ICollection<string> Audiences { get; set; }
        DateTime? ExpirationTime { get; set; }
        DateTime? IssuedAt { get; set; }
        string Issuer { get; set; }
        string JwtId { get; set; }
        DateTime? NotBefore { get; set; }
    }
}