using Serilog;
using XiHan.Framework.Logging.Abstractions;

namespace XiHan.Framework.Logging.Serilog;

/// <summary>
/// 表示基于 <c>Serilog</c> 的日志适配器。
/// </summary>
public sealed class SerilogLoggerAdapter : ILoggerAdapter
{
    private readonly ILogger _logger;

    /// <summary>
    /// 初始化日志适配器实例。
    /// </summary>
    /// <param name="logger">Serilog 日志实例。</param>
    public SerilogLoggerAdapter(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void LogInformation(string message)
    {
        _logger.Information("{Message}", message);
    }
}
