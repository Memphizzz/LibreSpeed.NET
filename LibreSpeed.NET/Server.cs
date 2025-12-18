using System;

namespace LibreSpeed.NET
{
    /// <summary>
    /// Represents a LibreSpeed test server.
    /// </summary>
    public class Server
    {
        /// <summary>
        /// Display name of the server.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Base URL of the server (e.g., "https://speedtest.example.com/")
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// Relative or absolute URL for download test (default: "garbage.php")
        /// </summary>
        public string DownloadUrl { get; set; } = "garbage.php";

        /// <summary>
        /// Relative or absolute URL for upload test (default: "empty.php")
        /// </summary>
        public string UploadUrl { get; set; } = "empty.php";

        /// <summary>
        /// Relative or absolute URL for ping test (default: "empty.php")
        /// </summary>
        public string PingUrl { get; set; } = "empty.php";

        /// <summary>
        /// Relative or absolute URL for getting client IP (default: "getIP.php")
        /// </summary>
        public string GetIpUrl { get; set; } = "getIP.php";

        /// <summary>
        /// Measured latency in milliseconds (populated after ping test).
        /// </summary>
        public double Latency { get; set; }

        /// <summary>
        /// Measured jitter in milliseconds (populated after ping test).
        /// </summary>
        public double Jitter { get; set; }

        /// <summary>
        /// Constructs the full URL for a given endpoint.
        /// </summary>
        public Uri GetEndpointUrl(string endpoint)
        {
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var absoluteUri))
                return absoluteUri;

            var baseUri = new Uri(BaseUrl.EndsWith("/") ? BaseUrl : BaseUrl + "/");
            return new Uri(baseUri, endpoint);
        }

        public override string ToString() => $"{Name} ({BaseUrl})";
    }
}
