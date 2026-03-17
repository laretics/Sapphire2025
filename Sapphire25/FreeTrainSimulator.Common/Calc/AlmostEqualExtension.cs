using System;

namespace FreeTrainSimulator.Common.Calc
{
    public static class AlmostEqualExtension
    {
        /// <summary>
        /// Returns true when the floating point value is *close to* the given value,
        /// within a given tolerance.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="other">The value to compare with.</param>
        /// <param name="tolerance">The amount the two values may differ while still being considered equal</param>
        /// <returns></returns>
        public static bool AlmostEqual(this float value, float other, float tolerance)
        {
            return Math.Round(Math.Abs(value - other), 4) <= tolerance;       //added rounding to overcome float imprecision. Using max fractional 4 digits 
        }

        /// <summary>
        /// Returns true when the floating point value is *close to* the given value,
        /// within a given tolerance.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="other">The value to compare with.</param>
        /// <param name="tolerance">The amount the two values may differ while still being considered equal</param>
        /// <returns></returns>
        public static bool AlmostEqual(this double value, double other, double tolerance)
        {
            return Math.Round(Math.Abs(value - other), 4) <= tolerance;       //added rounding to overcome float imprecision. Using max fractional 4 digits 
        }
    }
}
