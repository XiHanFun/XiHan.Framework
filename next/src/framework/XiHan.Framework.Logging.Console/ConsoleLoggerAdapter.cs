using XiHan.Framework.Logging.Abstractions;

namespace XiHan.Framework.Logging.Console;

/// <summary>
/// 表示控制台日志适配器的最小实现。
/// </summary>
public sealed class ConsoleLoggerAdapter : ILoggerAdapter
{
    /// <inheritdoc />
    public void LogInformation(string message)
    {
        System.Console.WriteLine(message);
    }
}
