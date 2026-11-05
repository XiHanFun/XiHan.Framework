// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace XiHan.Framework.Samples.Tests.Fixtures;

/// <summary>
/// 以独立进程运行的 SimpleApp
/// </summary>
/// <remarks>
/// 跑的是示例真正的 <c>Program.cs</c> 与 <c>appsettings.json</c>，不是仿照它装配的夹具，README 列出的命令才算验证过。两个端口都换成空闲端口，不占示例缺省的 5000、5001
/// </remarks>
public sealed class SimpleAppProcess : IAsyncLifetime
{
    /// <summary>
    /// 等示例就绪的上限
    /// </summary>
    private static readonly TimeSpan _startTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 示例进程的输出，启动失败时写进异常
    /// </summary>
    private readonly StringBuilder _output = new();

    /// <summary>
    /// 示例进程
    /// </summary>
    private Process? _process;

    /// <summary>
    /// 指向示例 HTTP 端口的客户端
    /// </summary>
    public HttpClient Client { get; } = new();

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var directory = OutputDirectory();
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.Combine(directory, "SimpleApp.dll"));
        var http = $"http://127.0.0.1:{FreePort().ToString(CultureInfo.InvariantCulture)}";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["Kestrel__Endpoints__Http__Url"] = http;
        start.Environment["Kestrel__Endpoints__Grpc__Url"] = $"http://127.0.0.1:{FreePort().ToString(CultureInfo.InvariantCulture)}";
        Client.BaseAddress = new Uri(http);

        _process = new Process { StartInfo = start };
        _process.OutputDataReceived += Capture;
        _process.ErrorDataReceived += Capture;
        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await WaitUntilReadyAsync(_process).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        if (_process is null)
        {
            return;
        }

        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().ConfigureAwait(false);
        }

        _process.Dispose();
    }

    /// <summary>
    /// 轮询就绪探针
    /// </summary>
    /// <remarks>
    /// 进程提前退出就不再等，把它的输出写进异常，配置错误一眼能看出来
    /// </remarks>
    private async Task WaitUntilReadyAsync(Process process)
    {
        using var deadline = new CancellationTokenSource(_startTimeout);
        while (!deadline.IsCancellationRequested && !process.HasExited)
        {
            try
            {
                using var response = await Client.GetAsync(new Uri("/health/ready", UriKind.Relative), deadline.Token).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // 还没开始监听
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"SimpleApp did not become ready. Output:{Environment.NewLine}{CapturedOutput()}");
    }

    /// <summary>
    /// 记下一行进程输出
    /// </summary>
    private void Capture(object sender, DataReceivedEventArgs line)
    {
        // 两条输出流在不同线程回调
        lock (_output)
        {
            _output.AppendLine(line.Data);
        }
    }

    /// <summary>
    /// 到目前为止的进程输出
    /// </summary>
    private string CapturedOutput()
    {
        lock (_output)
        {
            return _output.ToString();
        }
    }

    /// <summary>
    /// 示例的构建输出目录
    /// </summary>
    /// <remarks>
    /// 配置与目标框架同这个测试项目，项目引用保证它已构建
    /// </remarks>
    private static string OutputDirectory()
    {
        var testOutput = new DirectoryInfo(AppContext.BaseDirectory);
        var configuration = testOutput.Parent?.Name ?? throw new InvalidOperationException("The test output directory has no configuration segment.");
        var directory = testOutput;
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("The framework workspace could not be located."), "samples", "SimpleApp", "bin", configuration, testOutput.Name);
    }

    /// <summary>
    /// 取一个当前空闲的回环端口
    /// </summary>
    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
