using System.Runtime.CompilerServices;

namespace Guardian.Algorithm;

public class CuckooFilter
{
    private readonly int bucketCount;
    private readonly int bucketSize;
    private readonly int maxKicks;
    private readonly ushort[][] buckets;
    private readonly ushort fingerprintMask;

    public CuckooFilter(int capacity, int bucketSize = 4, int fingerprintBits = 12, int maxKicks = 500)
    {
        if (fingerprintBits is <= 0 or > 16)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fingerprintBits),
                "Fingerprint bits must be between 1 and 16.");
        }

        this.bucketSize = bucketSize;
        this.maxKicks = maxKicks;

        fingerprintMask = (ushort)((1 << fingerprintBits) - 1);

        bucketCount = 1;
        while (bucketCount < Math.Max(1, capacity / bucketSize))
        {
            bucketCount <<= 1;
        }

        buckets = new ushort[bucketCount][];
        for (var i = 0; i < bucketCount; i++)
        {
            buckets[i] = new ushort[bucketSize];
        }
    }

    public bool Add(ReadOnlySpan<byte> item)
    {
        var fingerprint = ComputeFingerprint(item);
        var index1 = Index1(item);
        var index2 = Index2(index1, fingerprint);

        if (TryInsert(index1, fingerprint) || TryInsert(index2, fingerprint))
        {
            return true;
        }

        var index = Random.Shared.Next(2) is 0 ? index1 : index2;
        for (var i = 0; i < maxKicks; i++)
        {
            var slot = Random.Shared.Next(bucketSize);
            (buckets[index][slot], fingerprint) = (fingerprint, buckets[index][slot]);

            index = Index2(index, fingerprint);
            if (TryInsert(index, fingerprint))
            {
                return true;
            }
        }

        return false;
    }

    public bool Contains(ReadOnlySpan<byte> item)
    {
        var fingerprint = ComputeFingerprint(item);
        var index1 = Index1(item);
        var index2 = Index2(index1, fingerprint);

        return Contains(index1, fingerprint) || Contains(index2, fingerprint);
    }

    public bool Remove(ReadOnlySpan<byte> item)
    {
        var fingerprint = ComputeFingerprint(item);
        var index1 = Index1(item);
        var index2 = Index2(index1, fingerprint);

        return TryDelete(index1, fingerprint) || TryDelete(index2, fingerprint);
    }
    
    public void Clear()
    {
        foreach (var bucket in buckets)
        {
            Array.Clear(bucket, index: 0, length: bucket.Length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryInsert(int index, ushort fingerprint)
    {
        var bucket = buckets[index];
        for (var i = 0; i < bucketSize; i++)
        {
            if (bucket[i] is 0)
            {
                bucket[i] = fingerprint;
                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Contains(int index, ushort fingerprint)
    {
        for (var i = 0; i < buckets[index].Length; i++)
        {
            if (buckets[index][i] == fingerprint)
            {
                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryDelete(int index, ushort fingerprint)
    {
        var bucket = buckets[index];
        for (var i = 0; i < bucketSize; i++)
        {
            if (bucket[i] == fingerprint)
            {
                bucket[i] = 0;
                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort ComputeFingerprint(ReadOnlySpan<byte> item)
    {
        var hash = MurmurHash3.Hash128Bit32(item, 0);
        var fingerprint = (ushort)(hash & fingerprintMask);

        return fingerprint is 0 ? (ushort)1 : fingerprint;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Index1(ReadOnlySpan<byte> item)
    {
        var hash = MurmurHash3.Hash128Bit32(item, 0x9747b28c);
        return (int)(hash & (uint)(bucketCount - 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Index2(int index1, ushort fingerprint)
    {
        var data = BitConverter.GetBytes(fingerprint);
        var hash = MurmurHash3.Hash128Bit32(data, 0x85ebca6b);

        return (int)((index1 ^ hash) & (uint)(bucketCount - 1));
    }
}