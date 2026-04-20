namespace XiHan.Framework.Logging.Abstractions;

/// <summary>
/// 表示框架统一日志适配器抽象。
/// </summary>
public interface ILoggerAdapter
{
    /// <summary>
    /// 记录信息级日志。
    /// </summary>
    /// <param name="message">日志消息。</param>
    void LogInformation(string message);
}
