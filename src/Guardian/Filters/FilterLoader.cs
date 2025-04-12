using System.IO;

namespace Guardian.Filters;

internal static class FilterLoader
{
    private const string DefaultFilterName = "default.txt";
    private const string GamblingFilterName = "gambling.txt";
    private const string NsfwFilterName = "nsfw.txt";
    private const string TrackersFilterName = "trackers.txt";

    public static IEnumerable<string> LoadDefaultFilter() => LoadEntries(DefaultFilterName);

    public static IEnumerable<string> LoadGamblingFilter() => LoadEntries(GamblingFilterName);

    public static IEnumerable<string> LoadNsfwFilter() => LoadEntries(NsfwFilterName);

    public static IEnumerable<string> LoadTrackersFilter() => LoadEntries(TrackersFilterName);

    private static string[] LoadEntries(string filterName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Filters", filterName);
        return !File.Exists(path) ? [] : File.ReadAllLines(path);
    }
}