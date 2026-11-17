// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.InteropServices;
using EchoPlugin;

// 进程外插件示例（RFC-0017）：以 gRPC 服务端实现 xihan.plugin.v1.Plugin，向 stdout 写一行握手行公布端点，
// 日志只走 stderr，stdin 关闭或收到 SIGTERM 时 3 秒内收尾退出。只依赖 protobuf 与 gRPC，不引用任何 XiHan 包
var endpoint = PluginListener.Create();
var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { Args = args, ApplicationName = "EchoPlugin" });
builder.WebHost.UseKestrelCore();
// 命名管道只允许当前用户连接（handshake.md §4）
if (OperatingSystem.IsWindows())
{
    builder.WebHost.UseNamedPipes(static pipes => pipes.CurrentUserOnly = true);
}
builder.WebHost.ConfigureKestrel(kestrel => endpoint.Listen(kestrel));
builder.Services.AddLogging(static logging => logging.AddConsole(static console => console.LogToStandardErrorThreshold = LogLevel.Trace));
builder.Services.AddRoutingCore();
// 只接受带本次会话令牌的调用（handshake.md §4）
builder.Services.AddGrpc(static grpc => grpc.Interceptors.Add<SessionGuard>());
builder.Services.AddSingleton<PluginState>();
builder.Services.AddSingleton<HostClient>();

var app = builder.Build();
var state = app.Services.GetRequiredService<PluginState>();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("EchoPlugin");
app.Lifetime.ApplicationStopped.Register(endpoint.Delete);
app.MapGrpcService<EchoPluginService>();

// 回调端点列表缺失、不合法或没有能连的项时照常服务，启动时记一条警告，回调都以 plugins.host.unavailable 失败（handshake.md §4）
if (app.Services.GetRequiredService<HostClient>().Endpoint is null)
{
    ProgramLog.NoCallbackEndpoint(logger);
}

await app.StartAsync().ConfigureAwait(false);
endpoint.Restrict();

// 握手行：宿主读到后才连接（handshake.md §2）
Console.Out.WriteLine(endpoint.HandshakeLine);
await Console.Out.FlushAsync().ConfigureAwait(false);

// 收尾请求（lifecycle.md §7.7）：可服务的插件进入 Draining、立即取消在途调用并等它们结束；启动过而停止逻辑还没跑过的跑一次（reason 为 shutdown）。
// 从收到请求起最多 3 秒（短于宿主发出终止信号后再等的 5 秒），到时不论是否完成都以退出码 0 退出
var shuttingDown = 0;
// 计时器要有引用拿着，否则可能在触发之前就被回收
Timer? deadline = null;
void Shutdown()
{
    if (Interlocked.Exchange(ref shuttingDown, 1) != 0)
    {
        return;
    }

    deadline = new Timer(static _ => Environment.Exit(0), null, TimeSpan.FromSeconds(3), Timeout.InfiniteTimeSpan);
    state.BeginShutdown();
    _ = Task.Run(async () =>
    {
        await state.DrainAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        if (state.ClaimStop())
        {
            ProgramLog.StoppingOnShutdown(logger);
        }

        state.CompleteStop();
        app.Lifetime.StopApplication();
    });
}

// SIGTERM 与 stdin 关闭同样处理；取消缺省的终止，交给上面的收尾
using var terminate = OperatingSystem.IsWindows() ? null : PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
{
    context.Cancel = true;
    Shutdown();
});

// 宿主关闭 stdin（退出或崩溃）时自行退出，不留孤儿进程（handshake.md §1）
_ = Task.Run(async () =>
{
    try
    {
        while (await Console.In.ReadLineAsync().ConfigureAwait(false) is not null)
        {
        }
    }
    catch (IOException)
    {
        // 读出错同读到结尾
    }

    Shutdown();
});

await app.WaitForShutdownAsync().ConfigureAwait(false);
GC.KeepAlive(deadline);
