using System.Net;

namespace Tourmaline26.Components.Services.Logic
{
    /// <summary>
    /// Esto es uno de los dispositivos del sistema de información al viajero.
    /// </summary>
    public class DeviceMapped
    {
        public IPAddress Address { get; set; } = new IPAddress(0);
        public DeviceType Type { get; set; } = DeviceType.TFT;
        public CoachEnum Coach { get; set; } = CoachEnum.Undeterminated;
        public Orientation Side { get; set; } = Orientation.Neutral;
        public string PublicId { get; set; } = "Coche 1"; //Esto es lo que se muestra en público en el panel.
        public void SetParameters(string address, string type, string coach, string side, string publicId)
        {
            Address = IPAddress.Parse(address);
            Type = (DeviceType)Enum.Parse(typeof(DeviceType), type);
            Coach = (CoachEnum)Enum.Parse(typeof(CoachEnum), coach);
            Side = (Orientation)Enum.Parse(typeof(Orientation), side);
            PublicId = publicId;
        }
        public enum DeviceType
        {
            HMI,
            TFT,
            Led,
            Camera,
            Server3D
        }
        public enum CoachEnum
        {
            Undeterminated=0,
            M1=1,
            M2=2,
            M3=3,
            M4=4,
            N1=1,
            N2=2,
            N3=3,
            N4=4,
            R1=1,
            R2=2,
            R3=3,
            R4=4,
        }
        public enum Orientation
        {
            Neutral=0,
            Forward=1,
            Backward=2
        }
    }
}
