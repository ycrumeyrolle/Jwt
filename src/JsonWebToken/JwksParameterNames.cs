// Copyright (c) 2020 Yann Crumeyrolle. All rights reserved.
// Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Text.Json;

namespace JsonWebToken
{
    /// <summary>Names for Json Web Key Set parameters</summary>
    internal static class JwksParameterNames
    {
        public static readonly JsonEncodedText Keys = JsonEncodedText.Encode("keys");
    }
}
