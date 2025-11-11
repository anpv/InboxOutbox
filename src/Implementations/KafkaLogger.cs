using Confluent.Kafka;

namespace InboxOutbox.Implementations;

public static partial class KafkaLogger
{
    [LoggerMessage(Message = "[{Facility}] {Name}: {Message}")]
    private static partial void Write(
        ILogger logger,
        LogLevel level,
        string facility,
        string name,
        string message);

    public static void Log(ILogger logger, LogMessage msg)
    {
        var level = msg.Level switch
        {
            SyslogLevel.Emergency or
                SyslogLevel.Critical or
                SyslogLevel.Warning or
                SyslogLevel.Alert => LogLevel.Warning,
            SyslogLevel.Error => LogLevel.Error,
            SyslogLevel.Notice or SyslogLevel.Info => LogLevel.Information,
            SyslogLevel.Debug => LogLevel.Debug,
            _ => LogLevel.None
        };

        if (level != LogLevel.None)
        {
            Write(logger, level, msg.Facility, msg.Name, msg.Message);
        }
    }
}