using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Guardian.Filters;
using Guardian.Models;
using Guardian.Proxy;
using Guardian.Services;

namespace Guardian.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly ProxySettings ProxySettings = new("127.0.0.1", 8888);

    private readonly DomainFilter defaultDomainFilter = new();
    private readonly DomainFilter gamblingDomainFilter = new();
    private readonly DomainFilter nfswDomainFilter = new();
    private readonly DomainFilter trackersDomainFilter = new();
    private readonly DomainFilter blacklistDomainFilter = new();
    private readonly DomainFilter whitelistDomainFilter = new();

    private readonly ISystemProxyManager systemProxyManager;

    private ProxyServer? proxyServer;
    private BackgroundWorker? proxyServerWorker;

    [ObservableProperty] private int totalRequests;
    [ObservableProperty] private int totalBlockedRequests;
    [ObservableProperty] private bool isProtectionEnabled;
    [ObservableProperty] private bool useDefaultFilter;
    [ObservableProperty] private bool useGamblingFilter;
    [ObservableProperty] private bool useNfswFilter;
    [ObservableProperty] private bool useTrackersFilter;

    public MainViewModel(ISystemProxyManager systemProxyManager)
    {
        ArgumentNullException.ThrowIfNull(systemProxyManager);

        this.systemProxyManager = systemProxyManager;
    }

    [RelayCommand]
    private void ToggleProtection()
    {
        if (IsProtectionEnabled)
        {
            DisableProtection();
        }
        else
        {
            EnableProtection();
        }

        IsProtectionEnabled = !IsProtectionEnabled;
    }

    [RelayCommand]
    private async Task EditBlacklistAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "blacklist.txt");
        if (!File.Exists(path))
        {
            await File.WriteAllTextAsync(path, string.Empty);
        }

        var process = Process.Start(new ProcessStartInfo("notepad.exe", path)
        {
            UseShellExecute = true
        });

        await process!.WaitForExitAsync();

        blacklistDomainFilter.Clear();
        blacklistDomainFilter.Load(await File.ReadAllLinesAsync(path));
    }

    [RelayCommand]
    private async Task EditWhitelistAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "whitelist.txt");
        if (!File.Exists(path))
        {
            await File.WriteAllTextAsync(path, string.Empty);
        }

        var process = Process.Start(new ProcessStartInfo("notepad.exe", path)
        {
            UseShellExecute = true
        });

        await process!.WaitForExitAsync();

        whitelistDomainFilter.Clear();
        whitelistDomainFilter.Load(await File.ReadAllLinesAsync(path));
    }

    partial void OnUseDefaultFilterChanged(bool value)
    {
        if (value)
        {
            defaultDomainFilter.Load(FilterLoader.LoadDefaultFilter());
        }
        else
        {
            defaultDomainFilter.Clear();
        }
    }

    partial void OnUseGamblingFilterChanged(bool value)
    {
        if (value)
        {
            gamblingDomainFilter.Load(FilterLoader.LoadGamblingFilter());
        }
        else
        {
            gamblingDomainFilter.Clear();
        }
    }

    partial void OnUseNfswFilterChanged(bool value)
    {
        if (value)
        {
            nfswDomainFilter.Load(FilterLoader.LoadNsfwFilter());
        }
        else
        {
            nfswDomainFilter.Clear();
        }
    }

    partial void OnUseTrackersFilterChanged(bool value)
    {
        if (value)
        {
            trackersDomainFilter.Load(FilterLoader.LoadTrackersFilter());
        }
        else
        {
            trackersDomainFilter.Clear();
        }
    }

    private void EnableProtection()
    {
        if (IsProtectionEnabled)
        {
            return;
        }

        // Load blacklist and whitelist filters
        if (File.Exists("blacklist.txt"))
        {
            blacklistDomainFilter.Load(File.ReadAllLines("blacklist.txt"));
        }

        if (File.Exists("whitelist.txt"))
        {
            whitelistDomainFilter.Load(File.ReadAllLines("whitelist.txt"));
        }

        proxyServerWorker = new BackgroundWorker();

        proxyServerWorker.DoWork += async (_, _) =>
        {
            proxyServer = new ProxyServer(ProxySettings.Port);
            proxyServer.UseFilter(UrlFilter);

            await proxyServer.StartAsync();
        };

        proxyServerWorker.RunWorkerAsync();

        systemProxyManager.Configure(ProxySettings);
    }

    private void DisableProtection()
    {
        if (!IsProtectionEnabled)
        {
            return;
        }

        systemProxyManager.Disable();

        proxyServer?.Dispose();
        proxyServerWorker?.Dispose();

        proxyServer = null;
        proxyServerWorker = null;
    }

    private bool UrlFilter((string full, string @short) urls)
    {
        TotalRequests++;

        var time = Stopwatch.GetTimestamp();
        TimeSpan elapsed;
        if (whitelistDomainFilter.Contains(urls.@short))
        {
            elapsed = Stopwatch.GetElapsedTime(time);
            NotifyUrlFiltered(new FilterResponse(urls.full, false, elapsed.Microseconds));

            return true;
        }

        if (blacklistDomainFilter.Contains(urls.@short) ||
            defaultDomainFilter.Contains(urls.@short) ||
            gamblingDomainFilter.Contains(urls.@short) ||
            nfswDomainFilter.Contains(urls.@short) ||
            trackersDomainFilter.Contains(urls.@short))
        {
            elapsed = Stopwatch.GetElapsedTime(time);
            NotifyUrlFiltered(new FilterResponse(urls.full, true, elapsed.Microseconds));

            TotalBlockedRequests++;

            return false;
        }

        elapsed = Stopwatch.GetElapsedTime(time);
        NotifyUrlFiltered(new FilterResponse(urls.full, false, elapsed.Microseconds));
        
        return true;
    }
    
    private void NotifyUrlFiltered(FilterResponse item) => UrlFiltered?.Invoke(this, item);

    public event EventHandler<FilterResponse>? UrlFiltered;
}