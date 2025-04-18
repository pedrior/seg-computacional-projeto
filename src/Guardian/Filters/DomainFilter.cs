using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Guardian.Algorithm;

namespace Guardian.Filters;

public sealed class DomainFilter
{
    private const int MaxUrlLength = 2048;
    private const int MaxDomainLength = 253;
    private const char DomainLabelSeparator = '.';

    private static readonly Encoding Encoding = Encoding.Unicode; // UTF-16
    private static readonly char[] DomainEndCharacters = [':', '?', '#', '/'];

    private CuckooFilter? filter;

    public void Load(string[] domains)
    {
        filter = new CuckooFilter(domains.Length);
        foreach (var domain in domains)
        {
            filter.Add(Encoding.GetBytes(domain));
        }
    }

    public void Clear() => filter?.Clear();

    public bool Contains(ReadOnlySpan<char> url)
    {
        if (filter is null)
        {
            return false; // Filtro não carregado
        }

        if (url.Length > MaxUrlLength)
        {
            return false;
        }

        var domain = ParseDomain(url);
        if (domain.Length > MaxDomainLength)
        {
            return false;
        }

        var labelCount = CountDomainLabels(domain);

        // Itera o domínio desde o TLD, construíndo os sufixos.
        // exemplo "other.domain.com": (L1) "com" -> (L2) "domain.com" -> (L3) "other.domain.com"
        for (var labelIndex = 1; labelIndex <= labelCount; labelIndex++)
        {
            // Verifica se o sufixo está contido no filtro
            var suffix = BuildDomainSuffix(domain, labelIndex);
            if (filter.Contains(CastToBytes(suffix)))
            {
                return true;
            }
        }

        return false;
    }

    private static ReadOnlySpan<char> ParseDomain(ReadOnlySpan<char> url) =>
        url[FindDomainStartIndex(url)..FindDomainEndIndex(url)];

    private static int CountDomainLabels(ReadOnlySpan<char> domain) =>
        domain.Count(DomainLabelSeparator) + 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindDomainStartIndex(ReadOnlySpan<char> url)
    {
        var start = 0;
        if (url.StartsWith("www."))
        {
            start = 4;
        }

        if (url.StartsWith("http://www."))
        {
            start = 11;
        }

        if (url.StartsWith("https://www."))
        {
            start = 12;
        }

        return start;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindDomainEndIndex(ReadOnlySpan<char> url)
    {
        var end = url.IndexOfAny(DomainEndCharacters);
        return end is -1 ? url.Length : end;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<char> BuildDomainSuffix(ReadOnlySpan<char> domain, int labelIndex)
    {
        var end = domain.Length;
        
        for (var i = domain.Length - 1; i >= 0; i--)
        {
            if (domain[i] is not DomainLabelSeparator)
            {
                continue;
            }

            labelIndex--;
            if (labelIndex is 0)
            {
                return domain[(i + 1)..end];
            }
        }

        return domain;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<byte> CastToBytes(ReadOnlySpan<char> domain) =>
        MemoryMarshal.Cast<char, byte>(domain);
}