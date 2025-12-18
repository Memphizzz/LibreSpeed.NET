# LibreSpeed.NET

A .NET client library for [LibreSpeed](https://github.com/librespeed/speedtest) speed test servers.

Measure download speed, upload speed, ping, and jitter against self-hosted or public LibreSpeed servers.

## Installation

```bash
dotnet add package LibreSpeed.NET
```

## Quick Start

### One-liner with public servers

```csharp
using LibreSpeed.NET;

using var client = new LibreSpeedClient();
var result = await client.RunWithBestServerAsync();

Console.WriteLine($"Server: {result.Server.Name}");
Console.WriteLine($"Ping: {result.Latency:F2} ms (jitter: {result.Jitter:F2} ms)");
Console.WriteLine($"Download: {result.DownloadSpeed:F2} Mbps");
Console.WriteLine($"Upload: {result.UploadSpeed:F2} Mbps");
```

### Custom/self-hosted server

```csharp
using LibreSpeed.NET;

var server = new Server
{
    Name = "My Server",
    BaseUrl = "https://speedtest.example.com/backend/"
};

using var client = new LibreSpeedClient();
var result = await client.RunAsync(server);
Console.WriteLine(result); // Ping: 12.34ms (+/-1.23ms) | Download: 95.67 Mbps | Upload: 45.23 Mbps
```

### Multiple servers with automatic selection

```csharp
using LibreSpeed.NET;

var servers = new List<Server>
{
    new Server { Name = "Frankfurt", BaseUrl = "https://fra.speedtest.clouvider.net/backend/" },
    new Server { Name = "Amsterdam", BaseUrl = "https://ams.speedtest.clouvider.net/backend/" },
    new Server { Name = "London", BaseUrl = "https://lon.speedtest.clouvider.net/backend/" }
};

using var client = new LibreSpeedClient();
var bestServer = await client.GetBestServerAsync(servers);
Console.WriteLine($"Best server: {bestServer.Name} ({bestServer.Latency:F0}ms)");

var result = await client.RunAsync(bestServer);
```

### Fetch public server list

```csharp
using LibreSpeed.NET;

// Fetch all public servers from librespeed.org
var servers = await ServerList.FetchPublicServersAsync();

foreach (var server in servers.Take(5))
{
    Console.WriteLine($"{server.Name}: {server.BaseUrl}");
}
```

## Configuration

```csharp
var client = new LibreSpeedClient
{
    ParallelStreams = 4,      // Number of concurrent connections (default: 3)
    DownloadDuration = 10,    // Download test duration in seconds (default: 15)
    UploadDuration = 10,      // Upload test duration in seconds (default: 15)
    PingCount = 5,            // Number of ping samples (default: 10)
    DownloadChunkSize = 100,  // Chunk size in MB (default: 100)
    UploadChunkSize = 1024 * 1024  // Upload chunk size in bytes (default: 1MB)
};
```

## Progress Reporting

```csharp
var client = new LibreSpeedClient
{
    OnDownloadProgress = progress => Console.WriteLine($"Download: {progress:P0}"),
    OnUploadProgress = progress => Console.WriteLine($"Upload: {progress:P0}")
};
```

## Individual Tests

```csharp
using var client = new LibreSpeedClient();

// Ping only
var (latency, jitter) = await client.TestPingAsync(server);

// Download only
var (downloadSpeed, bytesDownloaded) = await client.TestDownloadAsync(server);

// Upload only
var (uploadSpeed, bytesUploaded) = await client.TestUploadAsync(server);

// Get client IP
var ip = await client.GetClientIpAsync(server);
```

## Cancellation Support

All async methods support cancellation:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var result = await client.RunAsync(server, cts.Token);
```

## Custom HttpClient

For advanced scenarios (proxies, custom handlers, etc.):

```csharp
var httpClient = new HttpClient(new HttpClientHandler
{
    Proxy = new WebProxy("http://proxy:8080")
});

using var client = new LibreSpeedClient(httpClient);
```

## License

MIT
