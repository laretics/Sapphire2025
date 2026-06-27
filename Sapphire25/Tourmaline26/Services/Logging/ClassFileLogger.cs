using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Tourmaline26.Services.Logging
{
    internal sealed class ClassFileLogger:ILogger
    {
        private readonly TourmalineLogger mvarProvider;
        private readonly string mvarCategoryName;

        public ClassFileLogger(TourmalineLogger provider, string categoryName)
        {
            mvarProvider = provider;
            mvarCategoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel)
            => logLevel >= mvarProvider.mvarMinLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || null == formatter)
                return;

            string message = formatter(state, exception);

            if (string.IsNullOrWhiteSpace(message) && null == exception)
                return;

            mvarProvider.Write(logLevel, mvarCategoryName, eventId, message, exception);
        }
    }

    internal sealed class NullScope:IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }


    
}
