// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;
using Error = XiHan.Framework.Primitives.Error;

namespace XiHan.Framework.Plugins.OutOfProcess;

/// <summary>
/// 一个插件子进程：启动、读握手行、建立 gRPC 通道、转发 stderr / stdout 到日志、等待退出与终止。
/// </summary>
internal sealed partial class PluginProcess : IAsyncDisposable
{
    /// <summary>
    /// 允许原样传给插件的环境变量：PATH、用户目录、临时目录、区域与时区，以及操作系统运行所需的系统变量。
    /// </summary>
    private static readonly string[] _inheritedVariables =
    [
        "PATH", "HOME", "USERPROFILE", "TMP", "TEMP", "TMPDIR", "LANG", "LC_ALL", "TZ",
        "SystemRoot", "windir", "ComSpec", "PATHEXT", "SystemDrive", "APPDATA", "LOCALAPPDATA", "ProgramData", "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432",
    ];

    /// <summary>
    /// 允许按前缀传给插件的环境变量。
    /// </summary>
    private static readonly string[] _inheritedPrefixes = ["DOTNET_", "GO"];

    /// <summary>
    /// 进程。
    /// </summary>
    private readonly Process _process;

    /// <summary>
    /// 清单 ID（日志用）。
    /// </summary>
    private readonly string _pluginId;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 进程退出时以退出码完成。
    /// </summary>
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 到插件端点的通道；握手行之后才有。
    /// </summary>
    private GrpcChannel? _channel;

    /// <summary>
    /// 插件服务客户端；握手行之后才有。
    /// </summary>
    private Plugin.PluginClient? _client;

    /// <summary>
    /// 创建句柄并开始监视退出与 stderr。
    /// </summary>
    private PluginProcess(Process process, string pluginId, string sessionToken, ILogger logger)
    {
        _process = process;
        _pluginId = pluginId;
        _logger = logger;
        SessionToken = sessionToken;
        _ = MonitorExitAsync();
        _ = PumpAsync(process.StandardError);
    }

    /// <summary>
    /// 本进程的会话令牌。
    /// </summary>
    public string SessionToken { get; }

    /// <summary>
    /// 插件公布的端点；握手行之后才有。
    /// </summary>
    public PluginEndpoint? Endpoint { get; private set; }

    /// <summary>
    /// 进程 ID。
    /// </summary>
    public int ProcessId => _process.Id;

    /// <summary>
    /// 进程是否已退出。
    /// </summary>
    public bool HasExited => _exited.Task.IsCompleted;

    /// <summary>
    /// 进程退出时以退出码完成的任务。
    /// </summary>
    public Task<int> Exited => _exited.Task;

    /// <summary>
    /// 插件服务客户端。
    /// </summary>
    public Plugin.PluginClient Client => _client ?? throw new InvalidOperationException("The plugin process has not published its endpoint yet.");

    /// <summary>
    /// 启动进程、读握手行并建立通道；任一步失败都终止进程并返回错误。
    /// </summary>
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

        var handle = new PluginProcess(process, launch.PluginId, launch.SessionToken, logger);
        var line = await handle.ReadHandshakeLineAsync(launch.HandshakeTimeout, clock, cancellationToken).ConfigureAwait(false);
        if (!line.TryGetValue(out var endpoint))
        {
            await handle.DisposeAsync().ConfigureAwait(false);
            return line.Error!;
        }

        handle.Endpoint = endpoint;
        handle._channel = PluginChannelFactory.Create(endpoint);
        handle._client = new Plugin.PluginClient(handle._channel);
        // 握手行之后 stdout 只是普通输出，按 Information 记录
        _ = handle.PumpAsync(process.StandardOutput);
        return handle;
    }

    /// <summary>
    /// 在宽限内等待进程自行退出；返回是否退出。
    /// </summary>
    public async Task<bool> WaitForExitAsync(TimeSpan grace, TimeProvider clock, CancellationToken cancellationToken)
    {
        var completed = await Task.WhenAny(_exited.Task, Task.Delay(grace, clock, cancellationToken)).ConfigureAwait(false);
        return completed == _exited.Task;
    }

    /// <summary>
    /// 终止进程树并等待退出。
    /// </summary>
    public async Task KillAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // 进程已经退出或无法再被操作：按已退出处理
        }

        await Task.WhenAny(_exited.Task, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
    }

    /// <summary>
    /// 关闭 stdin（让插件自行退出）、释放通道、终止仍在运行的进程并释放句柄。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            _process.StandardInput.Close();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            // stdin 可能已经关闭
        }

        _channel?.Dispose();
        if (!HasExited)
        {
            await KillAsync().ConfigureAwait(false);
        }

        _process.Dispose();
    }

    /// <summary>
    /// 读 stdout 第一行并解析为端点：超时、进程先退出或行不合法都是失败。
    /// </summary>
    private async Task<Result<PluginEndpoint>> ReadHandshakeLineAsync(TimeSpan timeout, TimeProvider clock, CancellationToken cancellationToken)
    {
        var read = _process.StandardOutput.ReadLineAsync(cancellationToken).AsTask();
        var completed = await Task.WhenAny(read, _exited.Task, Task.Delay(timeout, clock, cancellationToken)).ConfigureAwait(false);
        if (completed == read)
        {
            var line = await read.ConfigureAwait(false);
            if (line is null)
            {
                return Error.Unexpected(PluginErrors.Crashed, PluginMessages.ProcessCrashed, details: MessageArguments.Of(("id", _pluginId), ("exitCode", HasExited ? _exited.Task.Result : -1)));
            }

            return PluginEndpoint.TryParseHandshakeLine(line, out var endpoint)
                ? Result.Success(endpoint)
                : new Error(PluginErrors.InvalidHandshake, PluginMessages.HandshakeInvalid, MessageArguments.Of(("id", _pluginId), ("reason", $"invalid handshake line '{line}'")));
        }

        if (completed == _exited.Task)
        {
            return Error.Unexpected(PluginErrors.Crashed, PluginMessages.ProcessCrashed, details: MessageArguments.Of(("id", _pluginId), ("exitCode", _exited.Task.Result)));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new Error(PluginErrors.Timeout, PluginMessages.ProcessTimeout, MessageArguments.Of(("id", _pluginId), ("phase", "handshake")));
    }

    /// <summary>
    /// 等待进程退出并记录退出码。
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
    /// 逐行把插件输出转为日志：以 <c>{</c> 开头且能解析的行按结构化日志（<c>level</c>、<c>message</c>）处理，其余按 Information。
    /// </summary>
    private async Task PumpAsync(StreamReader reader)
    {
        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
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
    /// 解析一行输出的级别与消息。
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
    /// 记录插件输出的一行。
    /// </summary>
    [LoggerMessage(Message = "[plugins.{PluginId}] {Line}")]
    private static partial void LogOutput(ILogger logger, LogLevel level, string pluginId, string line);
}
