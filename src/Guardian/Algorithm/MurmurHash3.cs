using System.Runtime.CompilerServices;

namespace Guardian.Algorithm;

public static class MurmurHash3
{
    public static uint Hash128Bit32(ReadOnlySpan<byte> data, uint seed)
    {
        const uint c1 = 0xcc9e2d51;
        const uint c2 = 0x1b873593;

        var length = data.Length;
        var nblocks = length / 4;

        var h1 = seed;
        
        // body
        for (var i = 0; i < nblocks; i++)
        {
            var i4 = i * 4;
            var k1 = GetBlock(data, i4);

            k1 *= c1;
            k1 = RotateLeft(k1, 15);
            k1 *= c2;

            h1 ^= k1;
            h1 = RotateLeft(h1, 13);
            h1 = h1 * 5 + 0xe6546b64;
        }

        // tail
        uint k2 = 0;
        var tailIndex = nblocks * 4;
        
        switch (length & 3)
        {
            case 3:
                k2 ^= (uint)data[tailIndex + 2] << 16;
                goto case 2;
            case 2:
                k2 ^= (uint)data[tailIndex + 1] << 8;
                goto case 1;
            case 1:
                k2 ^= data[tailIndex];
                k2 *= c1;
                k2 = RotateLeft(k2, 15);
                k2 *= c2;
                h1 ^= k2;
                
                break;
        }

        // finalization
        h1 ^= (uint)length;
        h1 = FMix(h1);
        
        return h1;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateLeft(uint x, int r) => (x << r) | (x >> (32 - r));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetBlock(ReadOnlySpan<byte> data, int i) => BitConverter.ToUInt32(data[i..]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint FMix(uint h)
    {
        h ^= h >> 16;
        h *= 0x85ebca6b;
        h ^= h >> 13;
        h *= 0xc2b2ae35;
        h ^= h >> 16;

        return h;
    }
}