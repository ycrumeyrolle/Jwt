﻿// Copyright (c) 2018 Yann Crumeyrolle. All rights reserved.
// Licensed under the MIT license. See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace JsonWebToken
{
    /// <summary>
    /// Provides compression and decompression services, based on <typeparamref name="TStream"/>.
    /// </summary>
    public abstract class Compressor<TStream> : Compressor where TStream : Stream
    {
        /// <summary>
        /// Creates a decompression <see cref="Stream"/>.
        /// </summary>
        public abstract TStream CreateDecompressionStream(Stream inputStream);

        /// <summary>
        /// Creates a compression <see cref="Stream"/>.
        /// </summary> 
        public abstract TStream CreateCompressionStream(Stream inputStream);

        /// <inheritdoc />
        public override Span<byte> Compress(ReadOnlySpan<byte> ciphertext)
        {
            using (var outputStream = new MemoryStream())
            using (var compressionStream = CreateCompressionStream(outputStream))
            {
#if !NETSTANDARD2_0
                compressionStream.Write(ciphertext);
#else
                compressionStream.Write(ciphertext.ToArray(), 0, ciphertext.Length);
#endif
                compressionStream.Flush();
                compressionStream.Close();
                return outputStream.ToArray();
            }
        }

        /// <inheritdoc />
        public override unsafe Span<byte> Decompress(ReadOnlySpan<byte> compressedCiphertext)
        {
            fixed (byte* pinnedCompressedCiphertext = compressedCiphertext)
            {
                using (var inputStream = new UnmanagedMemoryStream(pinnedCompressedCiphertext, compressedCiphertext.Length, compressedCiphertext.Length, FileAccess.Read))
                using (var compressionStream = CreateDecompressionStream(inputStream))
                {
                    var buffer = new byte[Constants.DecompressionBufferLength];
                    int uncompressedLength = 0;
                    int readData;
                    while ((readData = compressionStream.Read(buffer, uncompressedLength, Constants.DecompressionBufferLength)) != 0)
                    {
                        uncompressedLength += readData;
                        if (readData < Constants.DecompressionBufferLength)
                        {
                            break;
                        }

                        if (uncompressedLength == buffer.Length)
                        {
                            Array.Resize(ref buffer, buffer.Length * 2);
                        }
                    }

                    return new Span<byte>(buffer, 0, uncompressedLength);
                }
            }
        }
    }
}
