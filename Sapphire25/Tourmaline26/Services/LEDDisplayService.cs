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
        internal string mvarLastMessage = string.Empty; //Último mensaje enviado.
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

        public async Task ClearAsync(bool force = false)
        {
            if(mvarLastMessage.Length>0 || force)
            {
                mvarLastMessage = string.Empty;
                foreach (DeviceMapped auxDevice in mcolPanels)
                    await mvarController.ClearAsync(auxDevice.Address);
            }
        }
        public async Task PushAsync(string message,bool scroll = true, Alignment alignment = Alignment.Center, bool force = false)
        {
            if(!mvarLastMessage.Equals(message) || force)
            {
                mvarLastMessage = message;
                foreach (DeviceMapped auxDevice in mcolPanels)
                    await mvarController.PushMessageAsync(auxDevice.Address, message, scroll, alignment);
            }
        }

        public async Task Print(string message, bool scroll = true, Alignment alignment = Alignment.Center)
        {
            foreach (DeviceMapped auxDevice in mcolPanels)
                await mvarController.Print(auxDevice.Address, message, scroll, alignment);
        }
        public async Task Cls()
        {
            foreach (DeviceMapped auxDevice in mcolPanels)
                await mvarController.Cls(auxDevice.Address);
        }
    }
}
