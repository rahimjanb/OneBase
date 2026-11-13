using Microsoft.Extensions.Logging;

namespace OneBase.AI.Tests;

/// <summary>Журнал сервера в тесте: предупреждения и ошибки всех категорий — чтобы тест мог показать, почему что-то не записалось.</summary>
public sealed class CapturingLogs : ILoggerProvider
{
    public List<string> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Logger(CapturingLogs owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            lock (owner.Entries)
            {
                owner.Entries.Add($"{logLevel} {category}: {formatter(state, exception)} {exception?.GetType().Name}: {exception?.Message} {exception?.InnerException?.Message}");
            }
        }
    }
}
