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
        public LEDDisplayService(LedPanelController controller)
        {
            mvarController = controller;
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

        public async Task Print(bool inside, string message, bool scroll = true, Alignment alignment = Alignment.Center)
        {
            if(mvarLastMessage!=message)
            {
                //await Cls();
                foreach (DeviceMapped auxDevice in mcolPanels)
                {
                    if(auxDevice.HeaderSize<1)
                    {
                        if(inside)
                            await mvarController.Print(auxDevice.Address, message, scroll, alignment);
                    }
                    else
                    {
                        if (!inside)
                            await mvarController.Print(auxDevice.Address, message, scroll, alignment);
                    }                                        
                }                    
                mvarLastMessage = message;
            }
        }
        public async Task Draw(bool inside, string bitmapId, bool scroll = false, Alignment alignment = Alignment.Center)
        {
            // Lee el archivo BMP como array de bytes
            string auxPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "bmp", $"{bitmapId}.bmp");
            if(File.Exists(auxPath))
            {
                byte[] bmpBytes = await File.ReadAllBytesAsync(auxPath);
                foreach (DeviceMapped auxDevice in mcolPanels)
                {
                    if (auxDevice.HeaderSize < 1)
                    {
                        if (inside)
                            await mvarController.PushBitmapAsync(auxDevice.Address, bmpBytes);
                    }
                    else
                    {
                        if (!inside)
                            await mvarController.PushBitmapAsync(auxDevice.Address, bmpBytes);
                    }
                }
            }
        }

        public async Task Cls()
        {
            if(mvarLastMessage.Length>0)
            {
                foreach (DeviceMapped auxDevice in mcolPanels)
                    await mvarController.Cls(auxDevice.Address);
                mvarLastMessage = string.Empty;
            }
        }
    }
}
