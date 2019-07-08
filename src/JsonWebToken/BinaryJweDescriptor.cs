﻿// Copyright (c) 2018 Yann Crumeyrolle. All rights reserved.
// Licensed under the MIT license. See the LICENSE file in the project root for more information.

using System.Buffers;

namespace JsonWebToken
{
    /// <summary>
    /// Defines an encrypted JWT with a binary payload.
    /// </summary>
    public sealed class BinaryJweDescriptor : EncryptedJwtDescriptor<byte[]>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryJweDescriptor"/> class.
        /// </summary>
        public BinaryJweDescriptor()
            : base()
        {
        }   
        
        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryJweDescriptor"/> class.
        /// </summary>
        /// <param name="payload"></param>
        public BinaryJweDescriptor(byte[] payload)
            : base(payload)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryJweDescriptor"/> class.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="payload"></param>
        public BinaryJweDescriptor(JwtObject header, byte[] payload)
            : base(header, payload)
        {
        }

        /// <inheritdoc />
        public override void Encode(EncodingContext context, IBufferWriter<byte> output)
        {
            EncryptToken(context, Payload, output);
        }
    }
}
