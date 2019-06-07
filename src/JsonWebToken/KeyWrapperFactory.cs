﻿// Copyright (c) 2018 Yann Crumeyrolle. All rights reserved.
// Licensed under the MIT license. See the LICENSE file in the project root for more information.

using System;

namespace JsonWebToken
{
    /// <summary>
    /// Represents a factory of <see cref="KeyWrapper"/>.
    /// </summary>
    public abstract class KeyWrapperFactory : IDisposable
    {
        /// <summary>
        /// Dispose the managed resources.
        /// </summary>
        /// <param name="disposing"></param>
        protected abstract void Dispose(bool disposing);

        /// <summary>
        /// Dispose the managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Creates a <see cref="KeyWrapper"/>.
        /// </summary>
        /// <param name="key">The key used for key wrapping.</param>
        /// <param name="encryptionAlgorithm">The encryption algorithm.</param>
        /// <param name="contentEncryptionAlgorithm">The content encryption algorithm.</param>
        public abstract KeyWrapper Create(Jwk key, EncryptionAlgorithm encryptionAlgorithm, KeyManagementAlgorithm contentEncryptionAlgorithm);
    }
}