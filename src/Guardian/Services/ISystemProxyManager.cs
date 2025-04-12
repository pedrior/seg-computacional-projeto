namespace Guardian.Services;

public readonly record struct ProxySettings(string Host, int Port);

public interface ISystemProxyManager
{
    void Configure(ProxySettings settings);
    
    void Disable();
}