using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppSettingsEditor.Models.Tourmaline
{
    using System.Text.Json.Serialization;

    public class AppConfig
    {
        public bool DetailedErrors { get; set; } = true;
        public string? Urls { get; set; }

        public LoggingConfig Logging { get; set; } = new();
        public string AllowedHosts { get; set; } = "*";

        public SystemConfiguration SystemConfiguration { get; set; } = new();
        public List<Device> Devices { get; set; } = new();
        public List<Camera> Cameras { get; set; } = new();
    }

    public class LoggingConfig
    {
        public LogLevel LogLevel { get; set; } = new();
    }

    public class LogLevel
    {
        public string Default { get; set; } = "Information";
        [JsonPropertyName("Microsoft.AspNetCore")]
        public string MicrosoftAspNetCore { get; set; } = "Warning";
    }

    public class SystemConfiguration
    {
        public string Series { get; set; } = "S8100";
        public string Name { get; set; } = "8105-8106";
        public DateTime LastRelease { get; set; } = new DateTime(2025, 3, 15);

        public string ToniCruz { get; set; } = "left";
        public string SapphireUrl { get; set; } = "";
        public string MVBUrl { get; set; } = "";
        public int MVBRetries { get; set; } = 6;

        public string TExperienceUrl { get; set; } = "";
        public string TExperienceStr { get; set; } = "";
        public string SfmInfoUrl { get; set; } = "";
        public string SfmInfoToken { get; set; } = "";

        public GpsConfig Gps { get; set; } = new();
    }

    public class GpsConfig
    {
        public string Port { get; set; } = "COM6";
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public string Parity { get; set; } = "None";
        public int StopBits { get; set; } = 1;
        public string Handshake { get; set; } = "None";
    }

    public class Device
    {
        public string Address { get; set; } = "";
        public string Type { get; set; } = "";
        public string Coach { get; set; } = "";
        public string Side { get; set; } = "";
        public int HeaderSize { get; set; }
        public int Lines { get; set; }
        public string PublicId { get; set; } = "";
    }

    public class Camera
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int CoachId { get; set; }
        public bool Essential { get; set; }
        public string Address { get; set; } = "";
        public string CameraType { get; set; } = "";
        public string Codec { get; set; } = "R2P";
    }
}
