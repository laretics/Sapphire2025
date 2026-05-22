using BlazorBootstrap;
using System;
using System.Text;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

namespace Tourmaline26.Services
{
    public class LEDDisplayService
    {
        private readonly HttpClient mvarClient;

        public LEDDisplayService(HttpClient client)
        {
            mvarClient = client;
            mvarClient.Timeout = new TimeSpan(0, 0, 2); //Tiempo corto para evitar grandes retrasos
        }        
        public async Task<HttpResponseMessage> SendMessageAsync(string url, string text, bool scroll,Alignment alignment)
        {
            var parameters = new
            {
                typepage = "P1S002",
                lvs = "10",
                diai = "00",
                mesi = "00",
                horai = "00",
                mini = "00",
                diaf = "00",
                mesf = "00",
                horaf = "00",
                minf = "00",
                data1 = text,
                jsfs1 = "Center",
                ofx1 = "Static",
                ffx1 = "0",
                falt1 = "0",
                accion = "update"
            };
            StringContent auxContent = new StringContent(
                JsonSerializer.Serialize(parameters),
                Encoding.UTF8,
                "application/json");

            return await mvarClient.PostAsync(url, auxContent);
        }
        public async Task<HttpResponseMessage> Clear(string url)
        {
            var parameters = new
            {
                typepage = "P1S002",
                lvs = "10",
                diai = "00",
                mesi = "00",
                horai = "00",
                mini = "00",
                diaf = "00",
                mesf = "00",
                horaf = "00",
                minf = "00",
                data1 = " ",
                jsfs1 = "Center",
                ofx1 = "Static",
                ffx1 = "0",
                falt1 = "0",
                accion = "delete"
            };
            StringContent auxContent = new StringContent(
                JsonSerializer.Serialize(parameters),
                Encoding.UTF8,
                "application/json");

            return await mvarClient.PostAsync(url, auxContent);
        }
    }
}
