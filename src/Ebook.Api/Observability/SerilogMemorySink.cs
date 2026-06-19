using Serilog.Core;
using Serilog.Events;

namespace Ebook.Api.Observability;

/// <summary>Serilog sink que envia eventos para o InMemoryLogBuffer.</summary>
public sealed class SerilogMemorySink(InMemoryLogBuffer buffer) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        var level = logEvent.Level switch
        {
            LogEventLevel.Verbose     => "TRACE",
            LogEventLevel.Debug       => "DEBUG",
            LogEventLevel.Information => "INFO",
            LogEventLevel.Warning     => "WARN",
            LogEventLevel.Error       => "ERROR",
            LogEventLevel.Fatal       => "FATAL",
            _                         => "INFO",
        };

        var source = logEvent.Properties.TryGetValue("SourceContext", out var ctx)
            ? ctx.ToString().Trim('"').Split('.').LastOrDefault()
            : null;

        var message = logEvent.RenderMessage();
        if (logEvent.Exception is { } ex)
            message += $" | {ex.GetType().Name}: {ex.Message}";

        buffer.Add(level, message, source);
    }
}
