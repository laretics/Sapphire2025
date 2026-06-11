using BlazorBootstrap;
using Microsoft.AspNetCore.Components.Routing;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Tourmaline26.Logic
{
    //Emisor compartido para cada panel LED.
    public class LedPanelController
    {
        private readonly HttpClient mvarClient;
        private ILogger<LedPanelController> mvarLogger;
        private string Firmware { get; set; } = string.Empty;
        private string Checksum { get; set; } = string.Empty;
        private string Type { get; set; } = string.Empty;
        private int Id { get; set; }
        private int LoadedPages { get; set; }
        private int ActiveAlarms { get; set; }
        private int Temperature { get; set; }
        public LedPanelController(HttpClient client, ILogger<LedPanelController> logger)
        {
            mvarClient = client;
            mvarLogger = logger;
            mvarClient.Timeout = new TimeSpan(0, 0, 2); //Tiempo corto para evitar grandes retrasos
        }
        private void auxWebScrap(string html)
        {
            string pattern = @"
Versi.n\ del\ firmware\s*<!--VFIRM-->\s*""(?<firmware>[^""]*)"".*?
Checksum\ del\ firmware\s*<!--FCHECK-->\s*""(?<checksum>[^""]*)"".*?
Tipo\ de\ panel\s*<!--PNTYPE-->\s*""(?<type>[^""]*)"".*?
Identificador\s*<!--PNID-->\s*""(?<id>[^""]*)"".*?
N.mero\ de\ p.ginas\s*<!--NPAGES-->\s*""(?<pages>[^""]*)"".*?
N.mero\ de\ alarmas\s*<!--NALARM-->\s*""(?<alarms>[^""]*)"".*?
Temperatura\s*\(.C\)\s*<!--STEMP-->\s*""(?<temp>[^""]*)""";

            var regex = new Regex(pattern, RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
            var match = regex.Match(html);

            if (match.Success)
            {
                Firmware = match.Groups["firmware"].Value;
                Checksum = match.Groups["checksum"].Value;
                Type = match.Groups["type"].Value;
                string auxid = match.Groups["id"].Value;
                string auxpages = match.Groups["pages"].Value;
                string auxalarms = match.Groups["alarms"].Value;
                string auxtemp = match.Groups["temp"].Value;
                int auxValue = 0;
                int.TryParse(auxid, out auxValue);
                Id = auxValue;
                int.TryParse(auxpages, out auxValue);
                LoadedPages = auxValue;
                int.TryParse(auxalarms, out auxValue);
                ActiveAlarms = auxValue;
                int.TryParse(auxtemp, out auxValue);
                Temperature = auxValue;
            }
        }
        public async Task ReadStatus(IPAddress address)
        {
            string auxUrl = $"http://{address}/inicio.html";
            HttpResponseMessage respuesta = await mvarClient.GetAsync(auxUrl);
            if (respuesta.IsSuccessStatusCode)
            {
                byte[] buffer = await respuesta.Content.ReadAsByteArrayAsync();
                string html = Encoding.GetEncoding("ISO_8859-1").GetString(buffer);
                auxWebScrap(html);
            }
        }
        public async Task<HttpResponseMessage?> PushMessageAsync(IPAddress address, string text, bool scroll, Alignment alignment)
        {
            string auxAlign = "Center";
            switch (alignment)
            {
                case Alignment.Start:
                    auxAlign = "Left"; break;
                case Alignment.End:
                    auxAlign = "Right"; break;
            }

            //var parametros = new Dictionary<string, string>
            //{
            //    ["typepage"] = "P1S002",
            //    ["lvs"] = "0",
            //    ["diai"] = "0",
            //    ["mesi"] = "0",
            //    ["horai"] = "0",
            //    ["mini"] = "0",
            //    ["diaf"] = "0",
            //    ["mesf"] = "0",
            //    ["horaf"] = "0",
            //    ["minf"] = "0",
            //    ["data1"] = text,
            //    ["jsfs1"] = auxAlign,
            //    ["ofx1"] = scroll ? "Scroll" : "Static",
            //    ["ffx1"] = "0",
            //    ["falt1"] = "0",
            //    ["accion"] = "update"
            //};

            //var auxContent = new FormUrlEncodedContent(parametros);
            //string auxUrl = $"http://{address}/pagina1.html";
            //HttpResponseMessage respuesta = await mvarClient.PostAsync(auxUrl, auxContent);
            //return respuesta;
            string body =
    $"typepage=P1S002" +
    $"&lvs=0" +
    $"&diai=0" +
    $"&mesi=0" +
    $"&horai=0" +
    $"&mini=0" +
    $"&diaf=0" +
    $"&mesf=0" +
    $"&horaf=0" +
    $"&minf=0" +
    $"&data1={Uri.EscapeDataString(text)}" +
    $"&jsfs1={Uri.EscapeDataString(auxAlign)}" +
    $"&ofx1={(scroll ? "Scroll" : "Static")}" +
    $"&ffx1=0" +
    $"&falt1=0" +
    $"&accion=update";

            using var content = new StringContent(body, Encoding.ASCII, "application/x-www-form-urlencoded");
            string auxUrl = $"http://{address}/pagina1.html";
            HttpResponseMessage? response = null;
            try
            {
                response = await mvarClient.PostAsync(auxUrl, content);
                
            }
            catch (Exception ex)
            {
                mvarLogger.LogError($"Led panel error sending data to {address}. {ex.Message}");
            }
            return response;
        }
        public async Task<HttpResponseMessage> PushBitmapAsync(IPAddress address, byte[] bmpBytes)
        {
            var url = $"http://{address}/pagina1.html";
            using var form = new MultipartFormDataContent();

            form.Add(new StringContent("Static"), "ofx1");
            form.Add(new StringContent("0"), "ffx1");
            // El campo del bitmap:
            form.Add(new ByteArrayContent(bmpBytes), "fbmp2", "image.bmp");
            // Otros campos de ejemplo:
            form.Add(new StringContent("Center"), "jsfs2");
            form.Add(new StringContent("Static"), "ofx2");
            form.Add(new StringContent("0"), "ffx2");
            form.Add(new StringContent("update"), "accion");

            var respuesta = await mvarClient.PostAsync(url, form);
            return respuesta;
        }
        public async Task<HttpResponseMessage> PushBitmapAsync(IPAddress address, string bmpFilePath)
        {
            // Lee el archivo BMP como array de bytes
            byte[] bmpBytes = await File.ReadAllBytesAsync(bmpFilePath);

            // Llama al método polimórfico que ya tienes
            return await PushBitmapAsync(address, bmpBytes);
        }
        public async Task ClearAsync(IPAddress address)
        {
            await ReadStatus(address);
            while (LoadedPages > 0)
            {
                await ClearOneAsync(address);
                await ReadStatus(address);
            }
        }
        public async Task Print(IPAddress address, string message, bool scroll = false, Alignment alignment = Alignment.Center)
        {
            //await ClearAsync(address);
            await PushMessageAsync(address, message, scroll,alignment);
        }
        public async Task Cls(IPAddress address)
        {
            await ClearAsync(address);
        }
        internal async Task<HttpResponseMessage> ClearOneAsync(IPAddress address)
        {
            var parametros = new Dictionary<string, string>
            {
                ["typepage"] = "P1S002",
                ["lvs"] = "0",
                ["diai"] = "0",
                ["mesi"] = "0",
                ["horai"] = "0",
                ["mini"] = "0",
                ["diaf"] = "0",
                ["mesf"] = "0",
                ["horaf"] = "0",
                ["minf"] = "0",
                ["data1"] = " ",
                ["jsfs1"] = "Center",
                ["ofx1"] = "Static",
                ["ffx1"] = "0",
                ["falt1"] = "0",
                ["accion"] = "delete"
            };

            var auxContent = new FormUrlEncodedContent(parametros);
            string auxUrl = $"http://{address}/pagina1.html";
            HttpResponseMessage respuesta = await mvarClient.PostAsync(auxUrl, auxContent);
            string mensaje = await respuesta.Content.ReadAsStringAsync();
            return respuesta;
        }
    }
}
