using System;

namespace LibreSpeed.NET
{
    /// <summary>
    /// Results from a LibreSpeed test.
    /// </summary>
    public class SpeedTestResult
    {
        /// <summary>
        /// The server that was tested.
        /// </summary>
        public Server Server { get; set; }

        /// <summary>
        /// Ping latency in milliseconds.
        /// </summary>
        public double Latency { get; set; }

        /// <summary>
        /// Ping jitter (variance) in milliseconds.
        /// </summary>
        public double Jitter { get; set; }

        /// <summary>
        /// Download speed in Mbps (megabits per second).
        /// </summary>
        public double DownloadSpeed { get; set; }

        /// <summary>
        /// Upload speed in Mbps (megabits per second).
        /// </summary>
        public double UploadSpeed { get; set; }

        /// <summary>
        /// Total bytes downloaded during the test.
        /// </summary>
        public long BytesDownloaded { get; set; }

        /// <summary>
        /// Total bytes uploaded during the test.
        /// </summary>
        public long BytesUploaded { get; set; }

        /// <summary>
        /// Client's public IP address (if retrieved).
        /// </summary>
        public string ClientIp { get; set; }

        /// <summary>
        /// Timestamp when the test was performed.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public override string ToString()
        {
            return $"Ping: {Latency:F2}ms (±{Jitter:F2}ms) | Download: {DownloadSpeed:F2} Mbps | Upload: {UploadSpeed:F2} Mbps";
        }
    }
}
