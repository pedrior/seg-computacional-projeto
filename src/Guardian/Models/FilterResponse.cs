namespace Guardian.Models;

public readonly struct FilterResponse(string url, bool isBlocked, int tookUs = 0)
{
    public string Url { get; } = url;

    public bool IsBlocked { get; } = isBlocked;

    public int TookUs { get; } = tookUs;
}