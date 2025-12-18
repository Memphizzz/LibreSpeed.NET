using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;

namespace LibreSpeed.NET
{
    /// <summary>
    /// Provides methods to fetch LibreSpeed server lists from remote sources.
    /// </summary>
    public static class ServerList
    {
        /// <summary>
        /// Default URL for the public LibreSpeed server list.
        /// </summary>
        public const string DefaultServerListUrl = "https://librespeed.org/backend-servers/servers.php";

        /// <summary>
        /// Fetches the public LibreSpeed server list from librespeed.org.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of available public servers.</returns>
        public static Task<List<Server>> FetchPublicServersAsync(CancellationToken cancellationToken = default)
        {
            return FetchServersAsync(DefaultServerListUrl, cancellationToken);
        }

        /// <summary>
        /// Fetches a LibreSpeed server list from a custom URL.
        /// </summary>
        /// <param name="url">URL to fetch the server list from (JSON format).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of servers.</returns>
        public static async Task<List<Server>> FetchServersAsync(string url, CancellationToken cancellationToken = default)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LibreSpeed.NET/1.0");

            var response = await httpClient.GetStringAsync(url);
            var serverData = JsonConvert.DeserializeObject<List<ServerDefinition>>(response);

            return serverData.Select(definition => definition.ToServer()).ToList();
        }

        private class ServerDefinition
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("server")]
            public string ServerUrl { get; set; }

            [JsonProperty("dlURL")]
            public string DownloadUrl { get; set; }

            [JsonProperty("ulURL")]
            public string UploadUrl { get; set; }

            [JsonProperty("pingURL")]
            public string PingUrl { get; set; }

            [JsonProperty("getIpURL")]
            public string GetIpUrl { get; set; }

            [JsonProperty("sponsor")]
            public string Sponsor { get; set; }

            public Server ToServer()
            {
                // Handle protocol-relative URLs (//example.com)
                var baseUrl = ServerUrl;
                if (baseUrl.StartsWith("//"))
                    baseUrl = "https:" + baseUrl;

                // Ensure base URL ends with /
                if (!baseUrl.EndsWith("/"))
                    baseUrl += "/";

                var server = new Server
                {
                    Name = string.IsNullOrEmpty(Sponsor) ? Name : $"{Name} ({Sponsor})",
                    BaseUrl = baseUrl
                };

                // Only override defaults if JSON provided values
                if (!string.IsNullOrEmpty(DownloadUrl))
                    server.DownloadUrl = DownloadUrl;
                if (!string.IsNullOrEmpty(UploadUrl))
                    server.UploadUrl = UploadUrl;
                if (!string.IsNullOrEmpty(PingUrl))
                    server.PingUrl = PingUrl;
                if (!string.IsNullOrEmpty(GetIpUrl))
                    server.GetIpUrl = GetIpUrl;

                return server;
            }
        }
    }
}
