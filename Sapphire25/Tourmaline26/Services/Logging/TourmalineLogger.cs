using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;

namespace Tourmaline26.Services.Logging
{
    public class TourmalineLogger:ILoggerProvider
    {
        private readonly string mvarRootDirectory;
        internal readonly LogLevel mvarMinLevel;
        private readonly ConcurrentDictionary<string, object> mcolFileLocks = new(StringComparer.OrdinalIgnoreCase);

        public TourmalineLogger(string rootDirectory, LogLevel minLevel=LogLevel.Trace)
        {
            mvarRootDirectory = Path.GetFullPath(rootDirectory);
            mvarMinLevel = minLevel;
            Directory.CreateDirectory(mvarRootDirectory);
        }

        public ILogger CreateLogger(string categoryName)
            => new ClassFileLogger(this, categoryName);

        internal void Write(LogLevel logLevel, string categoryName, EventId eventId, string message, Exception? exception)
        {
            if (logLevel < mvarMinLevel)
                return;

            string filePath = GetFilePath(categoryName);
            string? directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            StringBuilder sb = new();
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.Append(" [");
            sb.Append(logLevel);
            sb.Append("] ");
            sb.Append(categoryName);
            sb.Append(" (");
            sb.Append(eventId.Id);
            sb.Append(")");
            if (!string.IsNullOrWhiteSpace(message))
            {
                sb.Append(": ");
                sb.Append(message);
            }
            sb.AppendLine();

            if (null != exception)
                sb.AppendLine(exception.ToString());

            sb.AppendLine();

            object gate = mcolFileLocks.GetOrAdd(filePath, _ => new object());
            lock(gate)
            {
                File.AppendAllText(filePath, sb.ToString(), Encoding.UTF8);
            }                                
        }

        private string GetFilePath(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
                categoryName = "Unknown";

            string[] parts = categoryName.Split(".", StringSplitOptions.RemoveEmptyEntries);

            if (0 == parts.Length)
                return Path.Combine(mvarRootDirectory, "Unknown.log");

            if (1 == parts.Length)
                return Path.Combine(mvarRootDirectory, Sanitize(parts[0]) + ".log");

            string[] folders = new string[parts.Length - 1];
            for (int i = 0; i < parts.Length - 1; i++)
                folders[i] = Sanitize(parts[i]);

            string fileName = Sanitize(parts[^1]) + ".log";

            string path = mvarRootDirectory;
            foreach (string folder in folders)
                path = Path.Combine(path, folder);

            return Path.Combine(path, fileName);
        }
            
        private static string Sanitize(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new(value.Length);

            foreach (char c in value)
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? "_" : c);

            return sb.Length == 0 ? "Unknown" : sb.ToString();
        }

        public void Dispose()
        {
            mcolFileLocks.Clear();
        }


    }
}
