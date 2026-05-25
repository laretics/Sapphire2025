using BlazorBootstrap;
using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Collections.Generic;
using static System.Net.Mime.MediaTypeNames;
using Tourmaline26.Logic;

namespace Tourmaline26.Services
{
    public class LEDDisplayService
    {
        internal LedPanelController mvarController { get; private set; }
        internal List<DeviceMapped> mcolPanels { get; private set; }
        public LEDDisplayService(HttpClient client)
        {
            mvarController = new LedPanelController(client);
            mcolPanels = new List<DeviceMapped>();          
        }
        internal void Init(List<DeviceMapped> devices)
        {
            foreach(DeviceMapped auxDevice in devices)
            {
                if (auxDevice.Type == Enums.DeviceType.Led)
                    mcolPanels.Add(auxDevice);
            }
        }

        public async Task ClearAsync()
        {
            foreach (DeviceMapped auxDevice in mcolPanels)
                await mvarController.ClearAsync(auxDevice.Address);
        }
        public async Task PushAsync(string message,bool scroll = true, Alignment alignment = Alignment.Center)
        {
            foreach(DeviceMapped auxDevice in mcolPanels)
                await mvarController.PushMessageAsync(auxDevice.Address, message,scroll, alignment);
        }





    }
}
