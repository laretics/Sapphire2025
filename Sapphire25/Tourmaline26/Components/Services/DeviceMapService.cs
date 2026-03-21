using Tourmaline26.Components.Services.Logic;

namespace Tourmaline26.Components.Services
{
    public class DeviceMapService
    {
        public DeviceCollection Devices { get; }
        public DeviceMapService(IConfiguration config)
        {
            IConfigurationSection section = config.GetSection("Devices");
            Devices = new DeviceCollection();
            foreach (IConfigurationSection deviceSection in section.GetChildren())
            {
                DeviceMapped nuevo = new DeviceMapped();
                nuevo.SetParameters(
                    deviceSection["address"], 
                    deviceSection["Type"], 
                    deviceSection["Coach"], 
                    deviceSection["Side"], 
                    deviceSection["PublicId"]);
                Devices.Add(nuevo);
            }
        }

    }
}
