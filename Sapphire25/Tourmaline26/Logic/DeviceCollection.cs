namespace Tourmaline26.Logic
{
    public class DeviceCollection : List<DeviceMapped>
    {
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
