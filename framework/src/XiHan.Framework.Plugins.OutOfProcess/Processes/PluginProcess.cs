// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;
using Error = XiHan.Framework.Primitives.Error;

namespace XiHan.Framework.Plugins.OutOfProcess.Processes;

/// <summary>
/// 一个插件子进程
/// </summary>
/// <remarks>
/// 启动、读握手行、建立 gRPC 通道、转发 stderr / stdout 到日志、等待退出与终止
/// </remarks>
internal sealed partial class PluginProcess : IAsyncDisposable
{
    /// <summary>
    /// 插件 stdout / stderr 一行最多保留的字符数
    /// </summary>
    /// <remarks>
    /// 插件不可信：超过的部分读掉丢弃、不进内存，行照常转成日志并另记一条告警；握手行超过即握手不合法（ADR-0185）
    /// </remarks>
    internal const int MaxOutputLineChars = 16 * 1024;

    /// <summary>
    /// 允许原样传给插件的环境变量
    /// </summary>
    /// <remarks>
    /// PATH、用户目录、临时目录、区域与时区，以及操作系统运行所需的系统变量
    /// </remarks>
    private static readonly string[] _inheritedVariables =
    [
        "PATH", "HOME", "USERPROFILE", "TMP", "TEMP", "TMPDIR", "LANG", "LC_ALL", "TZ",
        "SystemRoot", "windir", "ComSpec", "PATHEXT", "SystemDrive", "APPDATA", "LOCALAPPDATA", "ProgramData", "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432",
    ];

    /// <summary>
    /// 允许按前缀传给插件的环境变量
    /// </summary>
    private static readonly string[] _inheritedPrefixes = ["DOTNET_", "GO"];

    /// <summary>
    /// 进程
    /// </summary>
    private readonly Process _process;

    /// <summary>
    /// 清单 ID（日志用）
    /// </summary>
    private readonly string _pluginId;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 时钟（终止时的等待按它计时）
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 进程退出时以退出码完成
    /// </summary>
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// stdout 的按行读取：握手行与之后的普通输出共用，缓冲里读多的部分留给下一行
    /// </summary>
    private readonly PluginOutputReader _stdout;

    /// <summary>
    /// 到插件端点的通道
    /// </summary>
    /// <remarks>
    /// 握手行之后才有
    /// </remarks>
    private GrpcChannel? _channel;

    /// <summary>
    /// 插件服务客户端
    /// </summary>
    /// <remarks>
    /// 握手行之后才有
    /// </remarks>
    private Plugin.PluginClient? _client;

    /// <summary>
    /// 创建句柄并开始监视退出与 stderr
    /// </summary>
    private PluginProcess(Process process, string pluginId, string sessionToken, ILogger logger, TimeProvider clock)
    {
        _process = process;
        _pluginId = pluginId;
        _logger = logger;
        _clock = clock;
        SessionToken = sessionToken;
        _stdout = new PluginOutputReader(process.StandardOutput, MaxOutputLineChars);
        _ = MonitorExitAsync();
        _ = PumpAsync(new PluginOutputReader(process.StandardError, MaxOutputLineChars));
    }

    /// <summary>
    /// 本进程的会话令牌
    /// </summary>
    public string SessionToken { get; }

    /// <summary>
    /// 插件公布的端点
    /// </summary>
    /// <remarks>
    /// 握手行之后才有
    /// </remarks>
    public PluginEndpoint? Endpoint { get; private set; }

    /// <summary>
    /// 进程 ID
    /// </summary>
    public int ProcessId => _process.Id;

    /// <summary>
    /// 进程是否已退出
    /// </summary>
    public bool HasExited => _exited.Task.IsCompleted;

    /// <summary>
    /// 进程退出时以退出码完成的任务
    /// </summary>
    public Task<int> Exited => _exited.Task;

    /// <summary>
    /// 插件服务客户端
    /// </summary>
    public Plugin.PluginClient Client => _client ?? throw new InvalidOperationException("The plugin process has not published its endpoint yet.");

    /// <summary>
    /// 启动进程、读握手行并建立通道
    /// </summary>
    /// <remarks>
    /// 任一步失败都终止进程并返回错误；握手期间取消同样先终止进程，再抛出 <see cref="OperationCanceledException"/>
    /// </remarks>
    public static async Task<Result<PluginProcess>> LaunchAsync(PluginLaunch launch, TimeProvider clock, ILogger logger, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo(launch.Executable)
        {
            WorkingDirectory = launch.Directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // 只继承白名单环境变量，再追加协议约定的变量（handshake.md §1）
        var inherited = info.Environment.ToList();
        info.Environment.Clear();
        foreach (var (name, value) in inherited)
        {
            if (value is not null && (_inheritedVariables.Contains(name, StringComparer.OrdinalIgnoreCase) || _inheritedPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
            {
                info.Environment[name] = value;
            }
        }

        info.Environment[PluginProtocol.ProtocolVersionsVariable] = string.Join(',', PluginProtocol.SupportedVersions.Select(static v => v.ToString(CultureInfo.InvariantCulture)));
        info.Environment[PluginProtocol.PrimitivesVariable] = launch.Primitives.ToString(CultureInfo.InvariantCulture);
        info.Environment[PluginProtocol.SessionVariable] = launch.SessionToken;
        info.Environment[PluginProtocol.HostAddressVariable] = launch.HostEndpoint.ToString();
        info.Environment[PluginProtocol.InstanceVariable] = launch.InstanceId;
        info.Environment[PluginProtocol.LocaleVariable] = launch.Locale;

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        try
        {
            if (!process.Start())
            {
                process.Dispose();
                return Error.Unexpected(PluginErrors.Unexpected, PluginMessages.ProcessFailed, details: MessageArguments.Of(("id", launch.PluginId), ("phase", "launch"), ("reason", "the process could not be started")));
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            process.Dispose();
            return Error.Unexpected(PluginErrors.Unexpected, PluginMessages.ProcessFailed, details: MessageArguments.Of(("id", launch.PluginId), ("phase", "launch"), ("reason", exception.Message)));
        }

        var handle = new PluginProcess(process, launch.PluginId, launch.SessionToken, logger, clock);
        Result<PluginEndpoint> line;
        try
        {
            line = await handle.ReadHandshakeLineAsync(launch.HandshakeTimeout, clock, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // 取消或读 stdout 出错时进程已经启动，调用方拿不到句柄：先终止并释放，再把异常原样抛出
            await handle.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        if (!line.TryGetValue(out var endpoint))
        {
            await handle.DisposeAsync().ConfigureAwait(false);
            return line.Error!;
        }

        handle.Endpoint = endpoint;
        handle._channel = PluginChannelFactory.Create(endpoint);
        // 每次调用都带会话令牌：插件据此认出宿主，本机别的进程连上插件端点也冒充不了宿主（handshake.md §4，ADR-0202）
        var session = launch.SessionToken;
        handle._client = new Plugin.PluginClient(handle._channel.Intercept(metadata =>
        {
            metadata.Add(PluginProtocol.SessionMetadataKey, session);
            return metadata;
        }));
        // 握手行之后 stdout 只是普通输出，按 Information 记录
        _ = handle.PumpAsync(handle._stdout);
        return handle;
    }

    /// <summary>
    /// 在宽限内等待进程自行退出
    /// </summary>
    /// <remarks>
    /// 返回是否退出；取消与宽限到期一样返回假，由调用方继续终止进程
    /// </remarks>
    public async Task<bool> WaitForExitAsync(TimeSpan grace, TimeProvider clock, CancellationToken cancellationToken)
    {
        try
        {
            // WaitAsync 在进程先退出时撤掉计时器，不像 Task.Delay 那样空跑到宽限结束
            await _exited.Task.WaitAsync(grace, clock, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 停止流程被取消也不能留下进程：按未退出返回，调用方随后发终止信号
            return false;
        }
    }

    /// <summary>
    /// 关闭 stdin：按约定插件应随之自行退出（<c>handshake.md</c> §1）
    /// </summary>
    public void CloseInput()
    {
        try
        {
            _process.StandardInput.Close();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            // stdin 可能已经关闭
        }
    }

    /// <summary>
    /// 终止进程树并等待退出
    /// </summary>
    public Task KillAsync() => ProcessTermination.KillAsync(_process, _exited.Task, _clock);

    /// <summary>
    /// 停止超时后的终止：先发终止信号，5 秒内仍未退出再终止进程树（<c>lifecycle.md</c> §5）
    /// </summary>
    /// <remarks>
    /// 终止信号是关闭 stdin，Unix 上同时发 SIGTERM。返回进程是否在信号之后自行退出
    /// </remarks>
    public Task<bool> TerminateAsync() => ProcessTermination.TerminateAsync(_process, _exited.Task, CloseInput, _clock);

    /// <summary>
    /// 关闭 stdin（让插件自行退出）、释放通道、终止仍在运行的进程并释放句柄
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        CloseInput();
        _channel?.Dispose();
        if (!HasExited)
        {
            await KillAsync().ConfigureAwait(false);
        }

        _process.Dispose();
    }

    /// <summary>
    /// 读 stdout 第一行并解析为端点：超时、进程先退出或行不合法都是失败
    /// </summary>
    private async Task<Result<PluginEndpoint>> ReadHandshakeLineAsync(TimeSpan timeout, TimeProvider clock, CancellationToken cancellationToken)
    {
        // 超过上限即返回：握手行超长就不合法，不等行尾，也不把整行留在内存里
        var read = _stdout.ReadLineAsync(stopAtLimit: true, cancellationToken).AsTask();
        Task completed;
        try
        {
            // 超时交给 WaitAsync：读到行或进程退出时计时器随之撤销，不像 Task.Delay 那样空跑到握手时限；取消照常抛出
            completed = await Task.WhenAny(read, _exited.Task).WaitAsync(timeout, clock, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return new Error(PluginErrors.Timeout, PluginMessages.ProcessTimeout, MessageArguments.Of(("id", _pluginId), ("phase", "handshake")));
        }

        if (completed == read)
        {
            if (await read.ConfigureAwait(false) is not var (line, truncated))
            {
                return Error.Unexpected(PluginErrors.Crashed, PluginMessages.ProcessCrashed, details: MessageArguments.Of(("id", _pluginId), ("exitCode", HasExited ? _exited.Task.Result : -1)));
            }

            if (truncated)
            {
                return new Error(PluginErrors.InvalidHandshake, PluginMessages.HandshakeInvalid, MessageArguments.Of(("id", _pluginId), ("reason", $"handshake line longer than {MaxOutputLineChars} characters")));
            }

            return PluginEndpoint.TryParseHandshakeLine(line, out var endpoint)
                ? Result.Success(endpoint)
                : new Error(PluginErrors.InvalidHandshake, PluginMessages.HandshakeInvalid, MessageArguments.Of(("id", _pluginId), ("reason", $"invalid handshake line '{line}'")));
        }

        // 没读到行就是进程先退出了
        return Error.Unexpected(PluginErrors.Crashed, PluginMessages.ProcessCrashed, details: MessageArguments.Of(("id", _pluginId), ("exitCode", _exited.Task.Result)));
    }

    /// <summary>
    /// 等待进程退出并记录退出码
    /// </summary>
    private async Task MonitorExitAsync()
    {
        try
        {
            await _process.WaitForExitAsync().ConfigureAwait(false);
            _exited.TrySetResult(_process.ExitCode);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            _exited.TrySetResult(-1);
        }
    }

    /// <summary>
    /// 逐行把插件输出转为日志
    /// </summary>
    /// <remarks>
    /// 以 <c>{</c> 开头且能解析的行按结构化日志（<c>level</c>、<c>message</c>）处理，其余按 Information。超过 <see cref="MaxOutputLineChars"/> 的行只记前面的部分，另记一条告警
    /// </remarks>
    private async Task PumpAsync(PluginOutputReader reader)
    {
        try
        {
            while (await reader.ReadLineAsync(stopAtLimit: false, CancellationToken.None).ConfigureAwait(false) is var (line, truncated))
            {
                if (truncated)
                {
                    LogLineTruncated(_logger, _pluginId, MaxOutputLineChars);
                }

                if (line.Length == 0)
                {
                    continue;
                }

                var (level, message) = Parse(line);
                LogOutput(_logger, level, _pluginId, message);
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // 管道随进程退出关闭
        }
    }

    /// <summary>
    /// 解析一行输出的级别与消息
    /// </summary>
    private static (LogLevel Level, string Message) Parse(string line)
    {
        if (line[0] != '{')
        {
            return (LogLevel.Information, line);
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (LogLevel.Information, line);
            }

            var level = document.RootElement.TryGetProperty("level", out var levelElement) && levelElement.ValueKind == JsonValueKind.String
                ? levelElement.GetString() switch
                {
                    "trace" => LogLevel.Trace,
                    "debug" => LogLevel.Debug,
                    "warning" => LogLevel.Warning,
                    "error" => LogLevel.Error,
                    "critical" => LogLevel.Critical,
                    _ => LogLevel.Information,
                }
                : LogLevel.Information;
            var message = document.RootElement.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString() ?? line
                : line;
            return (level, message);
        }
        catch (JsonException)
        {
            return (LogLevel.Information, line);
        }
    }

    /// <summary>
    /// 记录插件输出的一行
    /// </summary>
    [LoggerMessage(Message = "[plugins.{PluginId}] {Line}")]
    private static partial void LogOutput(ILogger logger, LogLevel level, string pluginId, string line);

    /// <summary>
    /// 插件写了超长的一行
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin '{PluginId}' wrote an output line longer than {MaxChars} characters; the rest of the line was dropped.")]
    private static partial void LogLineTruncated(ILogger logger, string pluginId, int maxChars);
}
