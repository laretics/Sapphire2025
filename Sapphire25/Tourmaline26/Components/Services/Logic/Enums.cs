namespace Tourmaline26.Components.Services.Logic
{
	public static class Enums
	{

		public enum CameraType:byte
		{
			None=0,
			Inside=1,
			Frontal=2,
			Outside=3,
			Pantograph=4,
			Mirror=5,
			Other=255			
		}
		public enum CameraCodec:byte
		{
			None=0,
			R2P=1,
			other=255
		}
		public enum TrainSeries:byte
		{
			None=0,
			S6100=1,
			S7100=2,
			S8100=3,
			S9100=4,
			S1100=5,
			ManFGC=6,
			Other=255
		}
	}
}
