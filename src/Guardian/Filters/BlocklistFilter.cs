using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance.Buffers;

namespace Guardian.Filters;

using System;
using System.Collections.Generic;

public sealed class BlocklistFilter : IDisposable
{
    private const int MaxDomainDepth = 127;
    
    private readonly TrieNode root = new();
    private readonly StringPool stringPool = new();

    public void LoadBlocklist(IEnumerable<string> blocklist)
    {
        foreach (var entry in blocklist)
        {
            if (entry.StartsWith("*.", StringComparison.Ordinal) && entry.Length > 2)
            {
                ProcessDomain(entry.AsSpan(2));
            }
        }
    }
    
    public void Clear()
    {
        root.Children.Clear();
        GC.Collect();
    }

    private void ProcessDomain(ReadOnlySpan<char> domain)
    {
        var segments = new List<string>();
        var start = 0;
        
        for (var i = 0; i <= domain.Length; i++)
        {
            if (i < domain.Length && domain[i] != '.')
            {
                continue;
            }

            if (start < i)
            {
                var segment = domain[start..i];
                segments.Add(GetPooledKey(segment));
            }
            start = i + 1;
        }

        if (segments.Count is 0 or > MaxDomainDepth)
        {
            return;
        }

        var currentNode = root;
        for (var i = segments.Count - 1; i >= 0; i--)
        {
            var key = segments[i];
            
            ref var childNode = ref CollectionsMarshal.GetValueRefOrAddDefault(
                currentNode.Children, 
                key, 
                out var exists);
            
            if (!exists)
            {
                // Cycle check: Ensure we don't create loops
                if (childNode == currentNode)
                {
                    throw new InvalidOperationException("Trie cycle detected");
                }

                childNode = new TrieNode();
            }
            
            currentNode = childNode!;
        }
        
        currentNode.IsBlocking = true;
    }

    public bool Contains(ReadOnlySpan<char> url)
    {
        if (url.IsEmpty)
        {
            return false;
        }

        var host = ExtractHost(url);
        if (host.IsEmpty)
        {
            return false;
        }

        var segments = new List<string>();
        var start = 0;
        for (var i = 0; i <= host.Length; i++)
        {
            if (i < host.Length && host[i] != '.')
            {
                continue;
            }

            if (start < i)
            {
                var segment = host[start..i];
                segments.Add(GetPooledKey(segment));
            }
            
            start = i + 1;
        }

        if (segments.Count is 0 or > MaxDomainDepth)
        {
            return false;
        }

        var currentNode = root;
        for (var i = segments.Count - 1; i >= 0; i--)
        {
            var key = segments[i];
            
            if (!currentNode.Children.TryGetValue(key, out currentNode))
            {
                break;
            }

            if (currentNode.IsBlocking)
            {
                return true;
            }

            // Fail-safe: Prevent infinite traversal
            if (i == 0 && currentNode == root)
            {
                break;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetPooledKey(ReadOnlySpan<char> segment)
    {
        Span<char> buffer = stackalloc char[segment.Length];
        segment.ToLowerInvariant(buffer);

        return stringPool.GetOrAdd(buffer);
    }

    private static ReadOnlySpan<char> ExtractHost(ReadOnlySpan<char> url)
    {
        var schemeEnd = url.IndexOf("://");
        var authorityStart = schemeEnd >= 0
            ? schemeEnd + 3
            : 0;

        var pathStart = url[authorityStart..].IndexOfAny('/', '?', '#');

        var authoritySpan = pathStart >= 0
            ? url.Slice(authorityStart, pathStart)
            : url[authorityStart..];

        var portIndex = authoritySpan.IndexOf(':');
        return portIndex > 0 ? authoritySpan[..portIndex] : authoritySpan;
    }

    public void Dispose() => stringPool.Reset();

    private sealed class TrieNode
    {
        public Dictionary<string, TrieNode> Children { get; } = new(StringComparer.Ordinal);

        public bool IsBlocking { get; set; }
    }
}