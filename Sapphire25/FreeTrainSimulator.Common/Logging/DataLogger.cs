using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FreeTrainSimulator.Common.Logging
{
    public sealed class DataLogger : IDisposable
    {
        private const int cacheSize = 2048 * 1024;  // 2 Megs
        private readonly string filePath;
        private readonly StringBuilder cache = new StringBuilder(cacheSize);

        public SeparatorChar Separator { get; private set; } = SeparatorChar.Comma;

        public DataLogger(string filePath)
        {
            this.filePath = filePath;
        }
        public DataLogger(string filePath, SeparatorChar separator)
        {
            this.filePath = filePath;
            Separator = separator;
        }

        public void Data(string data)
        {
            cache.Append(data);
            cache.Append((char)Separator);
        }

        public void AddHeadline(string headline)
        {
            cache.Append(headline);
            Flush();
        }

        public void EndLine()
        {
            if (cache.Length > 0)
                cache.Length--;
            cache.AppendLine();
            if (cache.Length >= cacheSize)
                Flush();
        }

        public void Flush()
        {
            Task.Run(() =>
            {
                string data = cache.ToString();
                cache.Clear();
                File.AppendAllText(filePath, data);
            });
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
