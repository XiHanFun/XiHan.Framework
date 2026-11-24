// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Diagnostics;

/// <summary>
/// 调用方给的截止时间读不出时的限频告警
/// </summary>
/// <remarks>
/// 截止时间的文本读不出时调用照常按没有截止时间处理（RFC-0003 规则 8）；各载体在那之后到这里记一条 Warning，让用了读不出的写法的调用方有迹可查（ADR-0279）。
/// 日志不带原文：它是调用方写的，原样进日志可以伪造日志行、也可以把日志撑大；只带原文的长度与宿主自己掌握的标识（头名、匹配到的操作 ID、插件的清单 ID）。
/// 限频：请求头与插件回调各算一类，每一类在 <see cref="Window"/> 里最多记一条，窗口过后的下一条带上这期间被压掉的条数；状态是每类两个计数字段，不按调用方或原文分别计数。内核登记为单例，状态随宿主的容器存亡；可以在多个线程上同时调用
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class UnreadableDeadlineLog
{
    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 请求头这一类的限频门
    /// </summary>
    private readonly LogRateGate _headers;

    /// <summary>
    /// 插件回调这一类的限频门
    /// </summary>
    private readonly LogRateGate _plugins;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="loggerFactory">日志工厂</param>
    /// <param name="clock">时钟，限频的窗口按它的单调时间计</param>
    public UnreadableDeadlineLog(ILoggerFactory loggerFactory, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(clock);
        _logger = loggerFactory.CreateLogger<UnreadableDeadlineLog>();
        _headers = new LogRateGate(clock, Window);
        _plugins = new LogRateGate(clock, Window);
    }

    /// <summary>
    /// 限频的窗口：每一类告警在这段时间里最多记一条
    /// </summary>
    public static TimeSpan Window { get; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 请求头里的截止时间读不出
    /// </summary>
    /// <remarks>
    /// HTTP 载体的 <c>xihan-deadline</c> 与 gRPC 的 <c>grpc-timeout</c> 共用这一类。调用方按请求去重后再报，被压掉的条数才对得上请求数
    /// </remarks>
    /// <param name="header">头名；由报告的代码给出，不取自请求</param>
    /// <param name="length">头的原值的字符数</param>
    /// <param name="operationId">请求匹配到的操作 ID；没有匹配到操作时为 <see langword="null"/></param>
    public void ReportHeader(string header, int length, string? operationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(header);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        // 级别没开时不进限频门：否则计数一直累加，打开级别后的第一条会带上一个没有意义的数
        if (!_logger.IsEnabled(LogLevel.Warning) || !_headers.TryEnter(out var suppressed))
        {
            return;
        }

        if (operationId is null)
        {
            LogHeader(_logger, header, length, suppressed);
        }
        else
        {
            LogHeaderOfOperation(_logger, header, length, operationId, suppressed);
        }
    }

    /// <summary>
    /// 插件回调的元数据里的截止时间读不出
    /// </summary>
    /// <param name="pluginId">插件的清单 ID</param>
    /// <param name="length"><c>deadline</c> 的原文的字符数</param>
    public void ReportPluginMetadata(string pluginId, int length)
    {
        ArgumentException.ThrowIfNullOrEmpty(pluginId);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        // 与请求头分开计：外部调用方刷请求头时，插件一侧的告警不被压掉
        if (!_logger.IsEnabled(LogLevel.Warning) || !_plugins.TryEnter(out var suppressed))
        {
            return;
        }

        LogPluginMetadata(_logger, pluginId, length, suppressed);
    }

    /// <summary>
    /// 请求头读不出，请求没有匹配到操作
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The value of request header '{Header}' ({Length} characters) could not be read as a deadline and is ignored; the invocation continues without it. Similar warnings suppressed since the previous one: {Suppressed}.")]
    private static partial void LogHeader(ILogger logger, string header, int length, long suppressed);

    /// <summary>
    /// 请求头读不出，请求匹配到了操作
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The value of request header '{Header}' ({Length} characters) could not be read as a deadline and is ignored; the invocation of operation '{Operation}' continues without it. Similar warnings suppressed since the previous one: {Suppressed}.")]
    private static partial void LogHeaderOfOperation(ILogger logger, string header, int length, string operation, long suppressed);

    /// <summary>
    /// 插件回调的元数据读不出
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The deadline in the callback metadata of plugin '{Plugin}' ({Length} characters) could not be read and is ignored; the callback continues without it. Similar warnings suppressed since the previous one: {Suppressed}.")]
    private static partial void LogPluginMetadata(ILogger logger, string plugin, int length, long suppressed);
}
