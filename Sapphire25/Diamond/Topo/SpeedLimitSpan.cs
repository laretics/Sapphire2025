using Diamond.Basis;

namespace Diamond.Topo
{
	/// <summary>
	/// Limitación de velocidad fija: tramo lineal [PK, PKEnd) con un valor de velocidad.
	/// Misma geometría que un <c>item</c> de <c>&lt;limit&gt;</c> en el XML de topología.
	/// </summary>
	public class SpeedLimitSpan : Lineal<long, LongAxis>
	{
		private int mvarSpeed;

		public SpeedLimitSpan()
			: base()
		{
			mvarSpeed = 0;
		}

		public SpeedLimitSpan(long pk0, long pkf, int speed)
			: base(pk0, pkf - pk0)
		{
			Normalize();
			mvarSpeed = speed;
		}

		/// <summary>Velocidad máxima en el tramo (km/h).</summary>
		public int Speed
		{
			get { return mvarSpeed; }
			set { mvarSpeed = value; }
		}
	}
}
