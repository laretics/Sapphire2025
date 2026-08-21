using System.Net;

namespace Tourmaline26.Logic
{
    public class DeviceCollection : List<DeviceMapped>
    {
        public DeviceMapped? ByAddress(IPAddress? ip)
        {
            if (ip is null)
                return null;
            foreach (DeviceMapped device in this)
            {
                if (ClientAddress.Equals(device.Address, ip))
                    return device;
            }
            return null;
        }

        public DeviceCollection ByCoach(Enums.CoachEnum coach)
        {
            var result = new DeviceCollection();
            foreach (var device in this)
            {
                if (device.Coach == coach)
                    result.Add(device);
            }
            return result;
        }
        public DeviceCollection ByType(Enums.DeviceType type)
        {
            var result = new DeviceCollection();
            foreach (var device in this)
            {
                if (device.Type == type)
                    result.Add(device);
            }
            return result;
        }
    }
}
