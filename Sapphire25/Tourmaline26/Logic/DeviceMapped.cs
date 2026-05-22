using System.Net;
using System.Runtime.Serialization;

namespace Tourmaline26.Logic
{
    /// <summary>
    /// Esto es uno de los dispositivos del sistema de información al viajero.
    /// </summary>
    public class DeviceMapped
    {
        public IPAddress Address { get; private set; } = new IPAddress(0);
        public Enums.DeviceType Type { get; private set; } = Enums.DeviceType.TFT;
        public Enums.CoachEnum Coach { get; private set; } = Enums.CoachEnum.Undeterminated;
        public Enums.Orientation Side { get; private set; } = Enums.Orientation.Neutral;
        public int HeaderSize { get; private set; } = 30;
        public int Lines { get; private set; } = 7;
        public Enums.CameraType CameraType { get; private set; } = Enums.CameraType.None;
        public Enums.CameraCodec CameraCodec { get; private set; } = Enums.CameraCodec.None;
        public string PublicId { get; private set; } = "Coche 1"; //Esto es lo que se muestra en público en el panel.
        public void SetParameters(string? address, 
            string? type, string? coach, string? side,string? headerSize, string? lines, string? publicId)
        {
            Address = IPAddress.Parse(address??"0.0.0.0");
            Type = (Enums.DeviceType)Enum.Parse(typeof(Enums.DeviceType), type??"Led");
            Coach = (Enums.CoachEnum)Enum.Parse(typeof(Enums.CoachEnum), coach??"?");
            Side = (Enums.Orientation)Enum.Parse(typeof(Enums.Orientation), side??"Forward");
            int auxSize = 0;
            int auxLines = 0;
            if (int.TryParse(headerSize ?? "0", out auxSize))
                this.HeaderSize = auxSize;
            if (int.TryParse(lines ?? "0", out auxLines))
                this.Lines = auxLines;
            PublicId = publicId??"[]";
        }
        public void SetCameraParameters(string cameraType, string cameraCodec)
        {
            CameraType = (Enums.CameraType)Enum.Parse(typeof(Enums.CameraType), cameraType);
            CameraCodec = (Enums.CameraCodec)Enum.Parse(typeof(Enums.CameraCodec), cameraCodec);
        }
    }
}
