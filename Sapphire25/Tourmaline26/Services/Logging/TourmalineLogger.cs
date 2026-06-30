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
        private readonly long mvarMaxFileSizeBytes;
        private readonly int mvarMaxBackups;
        private readonly ConcurrentDictionary<string, object> mcolFileLocks = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateOnly> mcolLastDateByFile = new(StringComparer.OrdinalIgnoreCase); 
        private DateTime lastDate = DateTime.MinValue;

        public TourmalineLogger(
            string rootDirectory, 
            LogLevel minLevel=LogLevel.Trace,
            long maxFileSizeInBytes = 5* 1024 * 1024,
            int maxBackups = 5)
        {
            if (maxFileSizeInBytes < 1)
                throw new ArgumentOutOfRangeException(nameof(maxFileSizeInBytes));
            if (maxBackups < 1)
                throw new ArgumentOutOfRangeException(nameof(maxBackups));

            mvarRootDirectory = Path.GetFullPath(rootDirectory);
            mvarMinLevel = minLevel;
            mvarMaxFileSizeBytes = maxFileSizeInBytes;
            mvarMaxBackups = maxBackups;

            Directory.CreateDirectory(mvarRootDirectory);
        }

        private void EnsureDirectory(string filePath)
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
        }
        
        private static string GetBackupPath(string filePath, int backupIndex)
        {
            string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);

            string backupFileName = $"{fileNameWithoutExtension}.{backupIndex}{extension}";
            return string.IsNullOrWhiteSpace(directory)
                ? backupFileName
                : Path.Combine(directory, backupFileName);
        }

        private void RotateIfNeeded(string filePath, int nextEntryBytes)
        {
            long currentSize = File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
            if (currentSize + nextEntryBytes <= mvarMaxFileSizeBytes) return; //No hace falta rotar.

            //Desplaza los backups.
            for(int i = mvarMaxBackups-1;i>=1;i--)
            {
                string source = GetBackupPath(filePath, i);
                string destination = GetBackupPath(filePath, i + 1);

                if(File.Exists(source))
                {
                    if (File.Exists(destination))
                        File.Delete(destination);

                    File.Move(source, destination);
                }                
            }

            //Cambia el archivo actual a .1
            string firstBackup = GetBackupPath(filePath, 1);
            if (File.Exists(firstBackup))
                File.Delete(firstBackup);

            if (File.Exists(filePath))
                File.Move(filePath, firstBackup);

            //Fuerza que aparezca la cabecera de fecha en el nuevo archivo
            mcolLastDateByFile.TryRemove(filePath, out _);
        }

        public ILogger CreateLogger(string categoryName)
            => new ClassFileLogger(this, categoryName);

        internal void Write(LogLevel logLevel, string categoryName, EventId eventId, string message, Exception? exception)
        {
            if (logLevel < mvarMinLevel)
                return;

            string filePath = GetFilePath(categoryName);
            EnsureDirectory(filePath);

            object gate = mcolFileLocks.GetOrAdd(filePath, _ => new object());

            lock(gate)
            {
                string entry = BuildEntry(filePath, logLevel, eventId, message, exception);
                RotateIfNeeded(filePath, Encoding.UTF8.GetByteCount(entry));
                EnsureDirectory(filePath);
                File.AppendAllText(filePath, entry, Encoding.UTF8);
            }
        }

        private string BuildEntry(
            string filePath, 
            LogLevel logLevel, 
            EventId eventId,
            string message,
            Exception? exception)
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.Now);
            DateOnly lastDate = mcolLastDateByFile.GetOrAdd(filePath, _ => DateOnly.MinValue);

            StringBuilder sb = new();
            if (lastDate != today)
            {
                sb.AppendLine(); //Primera línea en blanco con el cambio de fecha
                sb.AppendLine($"New Date {DateTime.Now.ToString("yyyy-MM-dd")}");
                sb.AppendLine("===================");
                sb.AppendLine(); //Espacio antes del comienzo del nuevo texto.
                mcolLastDateByFile[filePath] = today;
            }
            sb.Append(DateTime.Now.ToString("HH:mm:ss.fff"));
            sb.Append(" [");
            sb.Append(logLevel);
            sb.Append("] (");
            //sb.Append("] ");
            //sb.Append(categoryName);
            //sb.Append(" (");
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

            return sb.ToString();
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
            mcolLastDateByFile.Clear();
        }


    }
}
