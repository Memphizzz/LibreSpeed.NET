using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LibreSpeed.NET
{
    /// <summary>
    /// Client for performing LibreSpeed tests.
    /// </summary>
    public class LibreSpeedClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;

        /// <summary>
        /// Number of parallel streams for download/upload tests (default: 3).
        /// </summary>
        public int ParallelStreams { get; set; } = 3;

        /// <summary>
        /// Duration of download test in seconds (default: 15).
        /// </summary>
        public int DownloadDuration { get; set; } = 15;

        /// <summary>
        /// Duration of upload test in seconds (default: 15).
        /// </summary>
        public int UploadDuration { get; set; } = 15;

        /// <summary>
        /// Number of ping samples to take (default: 10).
        /// </summary>
        public int PingCount { get; set; } = 10;

        /// <summary>
        /// Chunk size for download test in MB (default: 100).
        /// </summary>
        public int DownloadChunkSize { get; set; } = 100;

        /// <summary>
        /// Chunk size for upload test in bytes (default: 1MB).
        /// </summary>
        public int UploadChunkSize { get; set; } = 1024 * 1024;

        /// <summary>
        /// Progress callback for download test (0.0 to 1.0).
        /// </summary>
        public Action<double> OnDownloadProgress { get; set; }

        /// <summary>
        /// Progress callback for upload test (0.0 to 1.0).
        /// </summary>
        public Action<double> OnUploadProgress { get; set; }

        /// <summary>
        /// Creates a new LibreSpeedClient with a default HttpClient.
        /// </summary>
        public LibreSpeedClient()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LibreSpeed.NET/1.0");
            _ownsHttpClient = true;
        }

        /// <summary>
        /// Creates a new LibreSpeedClient with a provided HttpClient.
        /// </summary>
        public LibreSpeedClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _ownsHttpClient = false;
        }

        /// <summary>
        /// Measures ping latency and jitter to a server.
        /// </summary>
        public async Task<(double latency, double jitter)> TestPingAsync(Server server, CancellationToken cancellationToken = default)
        {
            var pingUrl = server.GetEndpointUrl(server.PingUrl);
            var samples = new List<double>();

            for (var i = 0; i < PingCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sw = Stopwatch.StartNew();
                try
                {
                    var response = await _httpClient.GetAsync(pingUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    sw.Stop();
                    samples.Add(sw.Elapsed.TotalMilliseconds);
                }
                catch (Exception) when (i > 0)
                {
                    // Allow some failures after first successful ping
                }
            }

            if (samples.Count == 0)
                throw new Exception("All ping attempts failed");

            var latency = samples.Average();
            var jitter = samples.Count > 1
                ? Math.Sqrt(samples.Select(x => Math.Pow(x - latency, 2)).Average())
                : 0;

            server.Latency = latency;
            server.Jitter = jitter;

            return (latency, jitter);
        }

        /// <summary>
        /// Tests download speed from a server.
        /// </summary>
        public async Task<(double speedMbps, long totalBytes)> TestDownloadAsync(Server server, CancellationToken cancellationToken = default)
        {
            var downloadUrl = server.GetEndpointUrl(server.DownloadUrl);
            var urlWithParams = new Uri($"{downloadUrl}?ckSize={DownloadChunkSize}");

            long totalBytes = 0;
            var sw = Stopwatch.StartNew();
            var endTime = TimeSpan.FromSeconds(DownloadDuration);

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var tasks = new List<Task>();

                for (var i = 0; i < ParallelStreams; i++)
                {
                    tasks.Add(DownloadStreamAsync(urlWithParams, sw, endTime, cts.Token, bytes =>
                    {
                        Interlocked.Add(ref totalBytes, bytes);
                        OnDownloadProgress?.Invoke(Math.Min(1.0, sw.Elapsed.TotalSeconds / DownloadDuration));
                    }));
                }

                // Wait for duration to elapse
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(DownloadDuration), cancellationToken);
                }
                catch (TaskCanceledException) { }

                cts.Cancel();

                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException) { }
            }

            sw.Stop();
            var speedMbps = totalBytes * 8.0 / 1_000_000 / sw.Elapsed.TotalSeconds;

            return (speedMbps, totalBytes);
        }

        private async Task DownloadStreamAsync(Uri url, Stopwatch sw, TimeSpan endTime, CancellationToken cancellationToken, Action<int> onBytesReceived)
        {
            var buffer = new byte[65536];

            while (sw.Elapsed < endTime && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    using var stream = await response.Content.ReadAsStreamAsync();
                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        onBytesReceived(bytesRead);

                        if (sw.Elapsed >= endTime || cancellationToken.IsCancellationRequested)
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Retry on transient errors
                    if (sw.Elapsed >= endTime)
                        break;
                }
            }
        }

        /// <summary>
        /// Tests upload speed to a server.
        /// </summary>
        public async Task<(double speedMbps, long totalBytes)> TestUploadAsync(Server server, CancellationToken cancellationToken = default)
        {
            var uploadUrl = server.GetEndpointUrl(server.UploadUrl);

            long totalBytes = 0;
            var sw = Stopwatch.StartNew();
            var endTime = TimeSpan.FromSeconds(UploadDuration);

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var tasks = new List<Task>();

                for (var i = 0; i < ParallelStreams; i++)
                {
                    tasks.Add(UploadStreamAsync(uploadUrl, sw, endTime, cts.Token, bytes =>
                    {
                        Interlocked.Add(ref totalBytes, bytes);
                        OnUploadProgress?.Invoke(Math.Min(1.0, sw.Elapsed.TotalSeconds / UploadDuration));
                    }));
                }

                // Wait for duration to elapse
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(UploadDuration), cancellationToken);
                }
                catch (TaskCanceledException) { }

                cts.Cancel();

                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException) { }
            }

            sw.Stop();
            var speedMbps = (totalBytes * 8.0 / 1_000_000) / sw.Elapsed.TotalSeconds;

            return (speedMbps, totalBytes);
        }

        private async Task UploadStreamAsync(Uri url, Stopwatch sw, TimeSpan endTime, CancellationToken cancellationToken, Action<int> onBytesSent)
        {
            // Pre-generate random data for upload
            var uploadData = new byte[UploadChunkSize];
            new Random().NextBytes(uploadData);

            while (sw.Elapsed < endTime && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var content = new ByteArrayContent(uploadData);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                    using var response = await _httpClient.PostAsync(url, content, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    onBytesSent(uploadData.Length);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Retry on transient errors
                    if (sw.Elapsed >= endTime)
                        break;
                }
            }
        }

        /// <summary>
        /// Gets the client's public IP address from a server.
        /// </summary>
        public async Task<string> GetClientIpAsync(Server server, CancellationToken cancellationToken = default)
        {
            var ipUrl = server.GetEndpointUrl(server.GetIpUrl);
            var response = await _httpClient.GetAsync(ipUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return content.Trim();
        }

        /// <summary>
        /// Runs a complete speed test against a server.
        /// </summary>
        public async Task<SpeedTestResult> RunAsync(Server server, CancellationToken cancellationToken = default)
        {
            var result = new SpeedTestResult { Server = server };

            // Ping test
            var (latency, jitter) = await TestPingAsync(server, cancellationToken);
            result.Latency = latency;
            result.Jitter = jitter;

            // Download test
            var (downloadSpeed, bytesDownloaded) = await TestDownloadAsync(server, cancellationToken);
            result.DownloadSpeed = downloadSpeed;
            result.BytesDownloaded = bytesDownloaded;

            // Upload test
            var (uploadSpeed, bytesUploaded) = await TestUploadAsync(server, cancellationToken);
            result.UploadSpeed = uploadSpeed;
            result.BytesUploaded = bytesUploaded;

            // Get IP (optional, don't fail if unavailable)
            try
            {
                result.ClientIp = await GetClientIpAsync(server, cancellationToken);
            }
            catch { }

            return result;
        }

        /// <summary>
        /// Finds the server with the lowest latency from a list.
        /// </summary>
        public async Task<Server> GetBestServerAsync(IEnumerable<Server> servers, CancellationToken cancellationToken = default)
        {
            var serverList = servers.ToList();
            var tasks = serverList.Select(async s =>
            {
                try
                {
                    await TestPingAsync(s, cancellationToken);
                    return s;
                }
                catch
                {
                    s.Latency = double.MaxValue;
                    return s;
                }
            });

            await Task.WhenAll(tasks);
            return serverList.OrderBy(s => s.Latency).First();
        }

        /// <summary>
        /// Fetches public servers and finds the one with the lowest latency.
        /// </summary>
        /// <param name="maxServers">Maximum number of servers to test (default: 10).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The server with the lowest latency.</returns>
        public async Task<Server> GetBestPublicServerAsync(int maxServers = 10, CancellationToken cancellationToken = default)
        {
            var servers = await ServerList.FetchPublicServersAsync(cancellationToken);
            return await GetBestServerAsync(servers.Take(maxServers), cancellationToken);
        }

        /// <summary>
        /// Runs a complete speed test against the best available public server.
        /// </summary>
        /// <param name="maxServers">Maximum number of servers to test for latency (default: 10).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Speed test results.</returns>
        public async Task<SpeedTestResult> RunWithBestServerAsync(int maxServers = 10, CancellationToken cancellationToken = default)
        {
            var server = await GetBestPublicServerAsync(maxServers, cancellationToken);
            return await RunAsync(server, cancellationToken);
        }

        public void Dispose()
        {
            if (_ownsHttpClient)
                _httpClient?.Dispose();
        }
    }
}
