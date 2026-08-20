using System.Globalization;
using BlazorBootstrap;
using Tourmaline26.Logic;

namespace Tourmaline26.Services
{
    /// <summary>
    /// Teleindicadores LED. El contenido lo decide
    /// <see cref="PassengerLedMapping"/> a partir del mismo modo que los TFT.
    /// </summary>
    public class LEDDisplayService
    {
        internal string mvarLastMessage = string.Empty; //Último mensaje enviado.
        private readonly Dictionary<string, string> mcolLastByDevice = new Dictionary<string, string>();
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
            if(mvarLastMessage.Length>0 || mcolLastByDevice.Count > 0 || force)
            {
                mvarLastMessage = string.Empty;
                mcolLastByDevice.Clear();
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
            await Print(inside, _ => message, scroll, alignment);
        }

        public async Task Print(bool inside, Func<DeviceMapped, string> messageForDevice, bool scroll = true, Alignment alignment = Alignment.Center)
        {
            foreach (DeviceMapped auxDevice in mcolPanels)
            {
                bool deviceInside = auxDevice.HeaderSize < 1;
                if (deviceInside != inside)
                    continue;

                string message = messageForDevice(auxDevice);
                string key = DeviceKey(auxDevice, inside);
                if (mcolLastByDevice.TryGetValue(key, out string? last) && last == message)
                    continue;

                await mvarController.Print(auxDevice.Address, message, scroll, alignment);
                mcolLastByDevice[key] = message;
                mvarLastMessage = message;
            }
        }

        /// <summary>
        /// Pinta interior y exterior según el mismo modo de los TFT,
        /// con reglas distintas para cada cara.
        /// </summary>
        public async Task RenderPassengerAsync(SessionConfiguration session)
        {
            Enums.PassengerLedKind interior = PassengerLedMapping.Resolve(session);
            Enums.PassengerLedExteriorKind exterior = PassengerLedMapping.ResolveExterior(session);

            if (interior == Enums.PassengerLedKind.Blank
                && exterior == Enums.PassengerLedExteriorKind.Blank)
            {
                await Cls();
                return;
            }

            bool hasAnnouncement = PassengerLedMapping.TryLedAnnouncement(
                session,
                out string announcement,
                out bool announcementInterior,
                out bool announcementExterior);

            if (hasAnnouncement
                && announcementInterior
                && interior != Enums.PassengerLedKind.OutOfService)
            {
                await Print(true, announcement, true);
            }
            else
            {
                await RenderInteriorAsync(session, interior);
            }

            if (hasAnnouncement
                && announcementExterior
                && exterior != Enums.PassengerLedExteriorKind.OutOfService)
            {
                await Print(false, announcement, true);
            }
            else
            {
                await RenderExteriorAsync(session, exterior);
            }
        }

        private async Task RenderInteriorAsync(SessionConfiguration session, Enums.PassengerLedKind kind)
        {
            switch (kind)
            {
                case Enums.PassengerLedKind.Blank:
                    return;
                case Enums.PassengerLedKind.OutOfService:
                    await Print(true, OutOfServiceDisplay.Combined, true);
                    return;
                case Enums.PassengerLedKind.ClockWeatherSpeed:
                    await ShowClockWeatherSpeed(session);
                    return;
                case Enums.PassengerLedKind.NextStation:
                    string next = PassengerLedMapping.NextStationName(session);
                    if (string.IsNullOrWhiteSpace(next))
                    {
                        await ShowDestinationAndCar(session);
                        return;
                    }
                    await Print(true, $"Propera estació {next}", true);
                    return;
                default:
                    await ShowDestinationAndCar(session);
                    return;
            }
        }

        private async Task RenderExteriorAsync(SessionConfiguration session, Enums.PassengerLedExteriorKind kind)
        {
            switch (kind)
            {
                case Enums.PassengerLedExteriorKind.Blank:
                    return;
                case Enums.PassengerLedExteriorKind.OutOfService:
                    await Print(false, OutOfServiceDisplay.Combined, true);
                    return;
                case Enums.PassengerLedExteriorKind.Destination:
                    string dest = PassengerLedMapping.DestinationName(session);
                    if (!string.IsNullOrWhiteSpace(dest))
                    {
                        await Print(false, dest, false);
                        return;
                    }
                    await PrintExteriorTrainNumber(session);
                    return;
                default:
                    await PrintExteriorTrainNumber(session);
                    return;
            }
        }

        private async Task PrintExteriorTrainNumber(SessionConfiguration session)
        {
            string number = PassengerLedMapping.TrainNumber(session);
            await Print(false, string.IsNullOrWhiteSpace(number) ? " " : number, false);
        }

        private async Task ShowDestinationAndCar(SessionConfiguration session)
        {
            string dest = PassengerLedMapping.DestinationName(session);
            if (string.IsNullOrWhiteSpace(dest)
                || session.InformationLevel != Enums.InformationLevel.Route)
            {
                await ShowClockWeatherSpeed(session);
                return;
            }

            await Print(
                true,
                device => $"Tren amb destinació {dest}. Cotxe {device.PublicCoachNumber}",
                true);
        }

        private async Task ShowClockWeatherSpeed(SessionConfiguration session)
        {
            string cadenaTemp = string.Empty;
            string cadenaSpeed = string.Empty;
            if (session.CurrentWeather is not null)
            {
                cadenaTemp = string.Format(
                    CultureInfo.InvariantCulture,
                    "   {0}ºC",
                    session.CurrentWeather.Temperature2m);
            }

            int auxSpeed = Math.Clamp(session.CurrentSpeed, 0, 100);
            if (auxSpeed > 40)
                cadenaSpeed = $"   {auxSpeed}km/h";

            DateTime clock = session.Cabin?.ClockNow ?? DateTime.Now;
            await Print(true, $"{clock:t}{cadenaTemp}{cadenaSpeed}", false);
        }

        private static string DeviceKey(DeviceMapped device, bool inside)
        {
            return string.Concat(device.Address.ToString(), inside ? "|in" : "|out");
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
            if(mvarLastMessage.Length>0 || mcolLastByDevice.Count > 0)
            {
                foreach (DeviceMapped auxDevice in mcolPanels)
                    await mvarController.Cls(auxDevice.Address);
                mvarLastMessage = string.Empty;
                mcolLastByDevice.Clear();
            }
        }
    }
}
