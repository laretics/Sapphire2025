using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace AppSettingsEditor.Models.Tourmaline
{
    using System.ComponentModel.DataAnnotations;
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
        [Display(Name = "Serie", 
            Description = "Identificador de la serie para Tourmaline")]
        public string Series { get; set; } = "S8100";

        [Display(Name = "Nombre", 
            Description = "ID de esta unidad según Zafiro")]
        public string Name { get; set; } = "8105-8106";

        [Display(Name = "Última liberación", 
            Description = "Fecha y hora de la versión de este configurador")]
        public DateTime LastRelease { get; set; } = DateTime.UtcNow;

        [Display(Name = "Orientación HMI", 
            Description = "Lado al que está el HMI en la cabina")]
        public string ToniCruz { get; set; } = "left";

        [Display(Name = "URL Sapphire", 
            Description = "URL de cliente http rest Zafiro (Horarios y Taller)")]
        public string SapphireUrl { get; set; } = "https://material.trensfm.com:5031";

        [Display(Name = "URL MVB", 
            Description = "Endpoint del servicio MVB")]
        public string MVBUrl { get; set; } = "http://172.16.20.11:8000/data";

        [Display(Name = "Reintentos MVB", 
            Description = "Número máximo de reintentos antes de abandonar")]
        public int MVBRetries { get; set; } = 6;

        [Display(Name = "URL Tourmaline Experience", 
            Description = "Endpoint del simulador Tourmaline Experience")]
        public string TExperienceUrl { get; set; } = "http://172.16.20.12:5005";

        [Display(Name = "Stream Tourmaline Experience", 
            Description = "URL del stream para TFT y HMI")]
        public string TExperienceStr { get; set; } = "http://172.16.20.12:5005/stream";

        [Display(Name = "URL SfmInfo", 
            Description = "Endpoint de la información de incidencias y horarios desde tierra")]
        public string SfmInfoUrl { get; set; } = "https://info.trensfm.com:8084";

        [Display(Name = "Token SfmInfo", 
            Description = "Token de acceso para información de incidencias y horarios desde tierra")]
        public string SfmInfoToken { get; set; } = "SFM2026";

        [Display(Name = "GPS", Description = "Configuración del puerto GPS")]
        public GpsConfig Gps { get; set; } = new();

        [Display(Name = "Distancia de bienvenida (m)",
            Description = "Metros desde el PK de origen durante los que se muestra la pantalla de bienvenida")]
        public int WelcomeDistanceMeters { get; set; } = 150;

        [Display(Name = "Factor de zoom del mapa",
            Description = "Multiplica el zoom del overlay SFM (función de la velocidad). 1 = default; 1.2 acerca vías en TFT 1024×768")]
        public double MapZoomFactor { get; set; } = 1;
    }

    public class GpsConfig
    {
        [Display(Name = "Puerto", Description = "Puerto serie físico al que se conecta el GPS")]
        public string Port { get; set; } = "COM6";
        [Display(Name = "Velocidad", Description = "En baudios")]
        public int BaudRate { get; set; } = 9600;
        [Display(Name = "Nº Bits", Description = "Bits de datos")]
        public int DataBits { get; set; } = 8;
        [Display(Name = "Paridad", Description = "Paridad")]
        public string Parity { get; set; } = "None";
        [Display(Name = "Bits de parada", Description = "Bits de Parada")]
        public int StopBits { get; set; } = 1;
        [Display(Name = "Handshake", Description = "HandShake")]
        public string Handshake { get; set; } = "None";
    }

    public class Device
    {
        [Display(Name = "Dirección", 
            Description = "Dirección para identificar al dispositivo cuando pide actualización")]
        public string Address { get; set; } = "172.16.0.0";
        [Display(Name = "Tipo" , Description = "HMI: Pantalla del Maquinista, TFT: Monitor viajeros, LED: Teleindicador")]
        public string Type { get; set; } = "";
        //[Display(Name = "", Description = "")]
        public string Coach { get; set; } = "";
        //[Display(Name = "", Description = "")]
        public string Side { get; set; } = "";
        //[Display(Name = "", Description = "")]
        public int HeaderSize { get; set; }
        //[Display(Name = "", Description = "")]
        public int Lines { get; set; }
        //[Display(Name = "", Description = "")]
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
