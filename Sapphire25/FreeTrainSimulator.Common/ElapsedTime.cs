namespace FreeTrainSimulator.Common
{
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct ElapsedTime
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        private static readonly ElapsedTime zero;

        public readonly double ClockSeconds;
        public readonly double RealSeconds;

        public static ref readonly ElapsedTime Zero => ref zero;

        public static ElapsedTime operator +(in ElapsedTime a, in ElapsedTime b)
        {
            return new ElapsedTime(a.ClockSeconds + b.ClockSeconds, a.RealSeconds + b.RealSeconds);
        }

        public static ElapsedTime Add(in ElapsedTime a, in ElapsedTime b)
        {
            return a + b;
        }

        public ElapsedTime(double clockSeconds, double realSeconds)
        {
            ClockSeconds = clockSeconds;
            RealSeconds = realSeconds;
        }
    }
}
