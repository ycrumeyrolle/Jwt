﻿using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NETCOREAPP3_0
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace JsonWebToken
{
    /// <summary>
    /// Computes SHA2-512 hash values.
    /// </summary>
    public class Sha384 : ShaAlgorithm
    {
        private const int BlockSize = 128;

        /// <inheritsdoc />
        public override int HashSize => 48;

        /// <inheritsdoc />
        public override void ComputeHash(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> prepend = default)
        {
            if (destination.Length < HashSize)
            {
                ThrowHelper.ThrowArgumentException_DestinationTooSmall(destination.Length, HashSize);
            }

            Span<ulong> state = stackalloc ulong[] {
                0xcbbb9d5dc1059ed8ul,
                0x629a292a367cd507ul,
                0x9159015a3070dd17ul,
                0x152fecd8f70e5939ul,
                0x67332667ffc00b31ul,
                0x8eb44a8768581511ul,
                0xdb0c2e0d64f98fa7ul,
                0x47b5481dbefa4fa4ul
            };
            Span<ulong> W = stackalloc ulong[80];
            ref ulong w = ref MemoryMarshal.GetReference(W);
            ref ulong stateRef = ref MemoryMarshal.GetReference(state);
            if (!prepend.IsEmpty)
            {
                Debug.Assert(prepend.Length == BlockSize);
                Sha512.Transform(ref stateRef, ref MemoryMarshal.GetReference(prepend), ref w);
            }

            ref byte srcRef = ref MemoryMarshal.GetReference(source);
            ref byte srcEndRef = ref Unsafe.Add(ref srcRef, source.Length - BlockSize + 1);
#if NETCOREAPP3_0
            if (Avx2.IsSupported)
            {
                ref byte srcSimdEndRef = ref Unsafe.Add(ref srcRef, source.Length - 4 * BlockSize + 1);
                if (Unsafe.IsAddressLessThan(ref srcRef, ref srcSimdEndRef))
                {
                    Vector256<ulong>[] returnToPool;
                    Span<Vector256<ulong>> wAvx = (returnToPool = ArrayPool<Vector256<ulong>>.Shared.Rent(80));
                    try
                    {
                        ref Vector256<ulong> wRef = ref MemoryMarshal.GetReference(wAvx);
                        do
                        {
                            Sha512.Transform(ref stateRef, ref srcRef, ref wRef);
                            srcRef = ref Unsafe.Add(ref srcRef, BlockSize * 4);
                        } while (Unsafe.IsAddressLessThan(ref srcRef, ref srcSimdEndRef));
                    }
                    finally
                    {
                        ArrayPool<Vector256<ulong>>.Shared.Return(returnToPool);
                    }
                }
            }
#endif

            while (Unsafe.IsAddressLessThan(ref srcRef, ref srcEndRef))
            {
                Sha512.Transform(ref stateRef, ref srcRef, ref w);
                srcRef = ref Unsafe.Add(ref srcRef, BlockSize);
            }

            int dataLength = source.Length + prepend.Length;
            int remaining = dataLength & (BlockSize - 1);

            Span<byte> lastBlock = stackalloc byte[BlockSize];
            ref byte lastBlockRef = ref MemoryMarshal.GetReference(lastBlock);
            Unsafe.CopyBlockUnaligned(ref lastBlockRef, ref srcRef, (uint)remaining);

            // Pad the last block
            Unsafe.Add(ref lastBlockRef, remaining) = 0x80;
            lastBlock.Slice(remaining + 1).Clear();
            if (remaining >= BlockSize - 2 * sizeof(ulong))
            {
                Sha512.Transform(ref stateRef, ref lastBlockRef, ref w);
                lastBlock.Slice(0, BlockSize - 2 * sizeof(ulong)).Clear();
            }

            // Append to the padding the total message's length in bits and transform.
            ulong bitLength = (ulong)dataLength << 3;
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref lastBlockRef, BlockSize - 16), 0ul); // Don't support input length > 2^64
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref lastBlockRef, BlockSize - 8), BinaryPrimitives.ReverseEndianness(bitLength));
            Sha512.Transform(ref stateRef, ref lastBlockRef, ref w);

            // reverse all the bytes when copying the final state to the output hash.
            ref byte destinationRef = ref MemoryMarshal.GetReference(destination);
#if NETCOREAPP3_0
            if (Avx2.IsSupported)
            {
                Unsafe.WriteUnaligned(ref destinationRef, Avx2.Shuffle(Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.As<ulong, byte>(ref stateRef)), Sha512.LittleEndianMask256));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationRef, 32), Ssse3.Shuffle(Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref Unsafe.As<ulong, byte>(ref stateRef), 32)), Sha512.LittleEndianMask128));
            }
            else if (Ssse3.IsSupported)
            {
                Unsafe.WriteUnaligned(ref destinationRef, Ssse3.Shuffle(Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.As<ulong, byte>(ref stateRef)), Sha512.LittleEndianMask128));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationRef, 16), Ssse3.Shuffle(Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref Unsafe.As<ulong, byte>(ref stateRef), 16)), Sha512.LittleEndianMask128));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationRef, 32), Ssse3.Shuffle(Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref Unsafe.As<ulong, byte>(ref stateRef), 32)), Sha512.LittleEndianMask128));
            }
            else
#endif
            {
                Unsafe.WriteUnaligned(ref destinationRef, BinaryPrimitives.ReverseEndianness(stateRef));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationRef, 8), BinaryPrimitives.ReverseEndianness(Unsafe.Add(ref stateRef, 1)));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationRef, 16), BinaryPrimitives.ReverseEndianness(Unsafe.Add(ref stateRef, 2)));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationRef, 24), BinaryPrimitives.ReverseEndianness(Unsafe.Add(ref stateRef, 3)));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationRef, 32), BinaryPrimitives.ReverseEndianness(Unsafe.Add(ref stateRef, 4)));
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationRef, 40), BinaryPrimitives.ReverseEndianness(Unsafe.Add(ref stateRef, 5)));
            }
        }
    }
}
