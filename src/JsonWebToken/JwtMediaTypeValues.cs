﻿namespace JsonWebToken
{
    /// <summary>Represents the media types 'typ' of JWT. Used for explicit typing. See https://tools.ietf.org/html/rfc8725#section-3.11</summary>
    public static class JwtMediaTypeValues
    {
        /// <summary>https://tools.ietf.org/html/rfc7519#section-5.1</summary>
        public const string Jwt = "JWT";

        /// <summary>https://tools.ietf.org/html/rfc2046#section-4.5.1</summary>
        public const string OctetStream = "octet-stream";

        /// <summary>https://tools.ietf.org/html/rfc5147</summary>
        internal const string? Plain = "plain";
    }  
}
