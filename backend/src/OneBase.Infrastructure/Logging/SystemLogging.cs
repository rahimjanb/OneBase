using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Domain.Audit;
using OneBase.Infrastructure.Linko;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Logging;

/// <summary>
/// Что попадает в журнал ошибок: у Linko и синхронизации — предупреждения и ошибки, у остального — только ошибки.
/// Журналы EF Core не пишутся: они дублируют ошибку, которую записывает сам код OneBase, и запись журнала
/// не должна порождать новые записи о себе.
/// </summary>
public static class SystemLogRules
{
    public static string SourceOf(string category, Exception? exception)
    {
        if (exception is LinkoApiException || category.EndsWith(".LinkoClient", StringComparison.Ordinal)
            || category.EndsWith(".IntegrationsController", StringComparison.Ordinal) || category.EndsWith(".LinkoSettingsStore", StringComparison.Ordinal))
        {
            return SystemLogSources.Linko;
        }

        return category.Contains(".Linko.", StringComparison.Ordinal) ? SystemLogSources.Sync : SystemLogSources.System;
    }

    public static bool Excluded(string category) =>
        category.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
        || category.StartsWith(typeof(SystemLogWriter).Namespace!, StringComparison.Ordinal);

    public static bool ShouldWrite(string source, LogLevel level) =>
        level >= LogLevel.Error || (level == LogLevel.Warning && source != SystemLogSources.System);
}

/// <summary>Вырезает из текста журнала то, что похоже на секреты: токены, ключи API, пароли из строк подключения.</summary>
public static partial class LogScrubber
{
    public static string Scrub(string text)
    {
        text = BearerPattern().Replace(text, "Bearer ***");
        text = KeyPattern().Replace(text, "sk-***");
        text = AssignmentPattern().Replace(text, m => $"{m.Groups[1].Value}{m.Groups[2].Value}***");
        return text;
    }

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerPattern();

    [GeneratedRegex(@"sk-[A-Za-z0-9_-]{8,}")]
    private static partial Regex KeyPattern();

    /// <summary>password=…, token: …, api_key=…, secret=… (до разделителя).</summary>
    [GeneratedRegex(@"\b(password|pwd|token|api[_-]?key|apikey|secret|signingkey)(\s*[=:]\s*)[^\s;,&""']+", RegexOptions.IgnoreCase)]
    private static partial Regex AssignmentPattern();
}

/// <summary>Очередь записей между логгером и фоновой записью в БД: логгер никогда не ждёт базу.</summary>
public sealed class SystemLogQueue
{
    private readonly Channel<SystemLog> _channel = Channel.CreateBounded<SystemLog>(
        new BoundedChannelOptions(5000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public ChannelReader<SystemLog> Reader => _channel.Reader;

    public void Enqueue(SystemLog entry) => _channel.Writer.TryWrite(entry);
}

public sealed class SystemLoggerProvider(SystemLogQueue queue) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new SystemLogger(categoryName, queue);

    public void Dispose()
    {
    }

    private sealed class SystemLogger(string category, SystemLogQueue queue) : ILogger
    {
        private const int MaxMessage = 4000;
        private const int MaxException = 16000;

        private readonly bool _excluded = SystemLogRules.Excluded(category);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => !_excluded && logLevel >= LogLevel.Warning && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var source = SystemLogRules.SourceOf(category, exception);
            if (!SystemLogRules.ShouldWrite(source, logLevel))
            {
                return;
            }

            queue.Enqueue(new SystemLog
            {
                Level = logLevel.ToString(),
                Source = source,
                Category = category.Length > 200 ? category[..200] : category,
                Message = Trim(LogScrubber.Scrub(formatter(state, exception)), MaxMessage),
                Exception = exception is null ? null : Trim(LogScrubber.Scrub(Describe(exception)), MaxException),
                TraceId = Activity.Current?.TraceId.ToString(),
            });
        }

        /// <summary>Тип и текст исключения с вложенными, затем стек.</summary>
        private static string Describe(Exception exception)
        {
            var sb = new StringBuilder();
            for (var e = exception; e is not null; e = e.InnerException)
            {
                sb.Append(e == exception ? string.Empty : "→ ").Append(e.GetType().FullName).Append(": ").AppendLine(e.Message);
            }

            sb.AppendLine().Append(exception.StackTrace);
            return sb.ToString();
        }

        private static string Trim(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
    }
}

/// <summary>Пишет журнал ошибок в БД пачками и раз в сутки удаляет записи старше 90 дней.</summary>
public sealed class SystemLogWriter(SystemLogQueue queue, IServiceScopeFactory scopes) : BackgroundService
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    private DateTimeOffset _lastCleanup = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<SystemLog>();
        try
        {
            while (await queue.Reader.WaitToReadAsync(stoppingToken))
            {
                while (batch.Count < 200 && queue.Reader.TryRead(out var entry))
                {
                    batch.Add(entry);
                }

                await WriteAsync(batch, CancellationToken.None);
                batch.Clear();
                await CleanupAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // остановка сервера — дописываем то, что осталось в очереди
            while (queue.Reader.TryRead(out var entry))
            {
                batch.Add(entry);
            }

            await WriteAsync(batch, CancellationToken.None);
        }
    }

    private async Task WriteAsync(List<SystemLog> batch, CancellationToken ct)
    {
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            db.SystemLogs.AddRange(batch);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // База недоступна — запись журнала не должна ронять сервер; ошибка видна в консоли.
            Console.Error.WriteLine($"Журнал ошибок: не удалось записать {batch.Count} записей: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastCleanup < TimeSpan.FromDays(1))
        {
            return;
        }

        _lastCleanup = now;
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var before = now - Retention;
            await db.SystemLogs.Where(l => l.Timestamp < before).ExecuteDeleteAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Журнал ошибок: не удалось удалить старые записи: {ex.GetType().Name}");
        }
    }
}
