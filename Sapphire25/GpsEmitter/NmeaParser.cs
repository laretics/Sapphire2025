using System.Globalization;
using GpsEmitter.Models;

namespace GpsEmitter;

/// <summary>
/// Parseo NMEA (RMC / GGA / VTG) alineado con Tourmaline26.Services.GPSService.
/// </summary>
public static class NmeaParser
{
    public static bool TryApply(string line, GpsBroadcastPacket target)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith('$'))
            return false;

        try
        {
            string[] parts = line.Split(',');
            if (parts.Length < 6)
                return false;

            string sentenceType = parts[0];

            if ((sentenceType is "$GPRMC" or "$GNRMC") && parts.Length > 9)
            {
                if (parts[2] != "A")
                    return false;

                double? lat = ParseLat(parts[3], parts[4]);
                double? lon = ParseLon(parts[5], parts[6]);
                DateTime? time = ParseTime(parts[1], parts[9]);

                if (lat is not null) target.Latitude = lat.Value;
                if (lon is not null) target.Longitude = lon.Value;
                if (time is not null) target.Time = time.Value;

                if (double.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out double knots))
                {
                    target.SpeedKnots = knots;
                    target.SpeedKmh = knots * 1.852;
                    target.SpeedMs = knots * 0.514444;
                }

                if (double.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out double course))
                    target.Course = course;

                if (target.FixQuality == 0)
                    target.FixQuality = 1;

                return true;
            }

            if ((sentenceType is "$GPGGA" or "$GNGGA") && parts.Length > 9)
            {
                if (parts[6] == "0")
                    return false;

                double? lat = ParseLat(parts[2], parts[3]);
                double? lon = ParseLon(parts[4], parts[5]);
                DateTime? time = ParseTime(parts[1], date: null);

                if (lat is not null) target.Latitude = lat.Value;
                if (lon is not null) target.Longitude = lon.Value;
                if (time is not null) target.Time = time.Value;

                if (int.TryParse(parts[6], out int quality))
                    target.FixQuality = quality;
                if (int.TryParse(parts[7], out int sats))
                    target.SatellitesUsed = sats;
                if (double.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out double hdop))
                    target.HDOP = hdop;
                if (double.TryParse(parts[9], NumberStyles.Float, CultureInfo.InvariantCulture, out double alt))
                    target.Altitude = alt;

                return true;
            }

            if ((sentenceType is "$GPVTG" or "$GNVTG") && parts.Length > 7)
            {
                if (double.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out double kmh))
                {
                    target.SpeedKmh = kmh;
                    target.SpeedKnots = kmh / 1.852;
                    target.SpeedMs = kmh / 3.6;
                }

                if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double course))
                    target.Course = course;

                return true;
            }
        }
        catch
        {
            // Línea mal formada: ignorar.
        }

        return false;
    }

    private static DateTime? ParseTime(string time, string? date)
    {
        try
        {
            int hour = int.Parse(time.AsSpan(0, 2));
            int min = int.Parse(time.AsSpan(2, 2));
            int sec = int.Parse(time.AsSpan(4, 2));

            if (!string.IsNullOrEmpty(date) && date.Length == 6)
            {
                int day = int.Parse(date.AsSpan(0, 2));
                int month = int.Parse(date.AsSpan(2, 2));
                int year = 2000 + int.Parse(date.AsSpan(4, 2));
                return new DateTime(year, month, day, hour, min, sec, DateTimeKind.Utc);
            }

            DateTime now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, now.Day, hour, min, sec, DateTimeKind.Utc);
        }
        catch
        {
            return null;
        }
    }

    private static double? ParseLat(string value, string hemi)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || v <= 0)
            return null;

        double deg = Math.Floor(v / 100);
        double min = v - deg * 100;
        double coord = deg + min / 60.0;
        if (hemi == "S") coord = -coord;
        return coord;
    }

    private static double? ParseLon(string value, string hemi)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || v <= 0)
            return null;

        double deg = Math.Floor(v / 100);
        double min = v - deg * 100;
        double coord = deg + min / 60.0;
        if (hemi == "W") coord = -coord;
        return coord;
    }
}
