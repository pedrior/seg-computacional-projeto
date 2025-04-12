using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Guardian.Filters;
using Guardian.Proxy;
using Guardian.Services;

namespace Guardian.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly ProxySettings ProxySettings = new("127.0.0.1", 8888);

    private readonly HashSet<string> blockedUrls = [];
    private readonly BlocklistFilter defaultBlocklistFilter = new();
    private readonly BlocklistFilter gamblingBlocklistFilter = new();
    private readonly BlocklistFilter nfswBlocklistFilter = new();
    private readonly BlocklistFilter trackersBlocklistFilter = new();
    private readonly BlocklistFilter blacklistBlocklistFilter = new();
    private readonly BlocklistFilter whitelistBlocklistFilter = new();

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

        blacklistBlocklistFilter.Clear();
        blacklistBlocklistFilter.LoadBlocklist(await File.ReadAllLinesAsync(path));
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

        whitelistBlocklistFilter.Clear();
        whitelistBlocklistFilter.LoadBlocklist(await File.ReadAllLinesAsync(path));
    }

    partial void OnUseDefaultFilterChanged(bool value)
    {
        if (value)
        {
            defaultBlocklistFilter.LoadBlocklist(FilterLoader.LoadDefaultFilter());
        }
        else
        {
            defaultBlocklistFilter.Clear();
        }
    }

    partial void OnUseGamblingFilterChanged(bool value)
    {
        if (value)
        {
            gamblingBlocklistFilter.LoadBlocklist(FilterLoader.LoadGamblingFilter());
        }
        else
        {
            gamblingBlocklistFilter.Clear();
        }
    }

    partial void OnUseNfswFilterChanged(bool value)
    {
        if (value)
        {
            nfswBlocklistFilter.LoadBlocklist(FilterLoader.LoadNsfwFilter());
        }
        else
        {
            nfswBlocklistFilter.Clear();
        }
    }

    partial void OnUseTrackersFilterChanged(bool value)
    {
        if (value)
        {
            trackersBlocklistFilter.LoadBlocklist(FilterLoader.LoadTrackersFilter());
        }
        else
        {
            trackersBlocklistFilter.Clear();
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
            blacklistBlocklistFilter.LoadBlocklist(File.ReadAllLines("blacklist.txt"));
        }
        
        if (File.Exists("whitelist.txt"))
        {
            whitelistBlocklistFilter.LoadBlocklist(File.ReadAllLines("whitelist.txt"));
        }

        proxyServerWorker = new BackgroundWorker();

        proxyServerWorker.DoWork += async (_, _) =>
        {
            proxyServer = new ProxyServer(ProxySettings.Port);
            proxyServer.UseFilter(ProxyServerFilter);

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

    private bool ProxyServerFilter(string url)
    {
        TotalRequests++;

        if (whitelistBlocklistFilter.Contains(url))
        {
            return true;
        }
        
        if (blacklistBlocklistFilter.Contains(url) ||
            defaultBlocklistFilter.Contains(url) ||
            gamblingBlocklistFilter.Contains(url) ||
            nfswBlocklistFilter.Contains(url) ||
            trackersBlocklistFilter.Contains(url))
        {
            if (blockedUrls.Add(url))
            {
                TotalBlockedRequests++;
            }

            return false;
        }

        return true;
    }
}