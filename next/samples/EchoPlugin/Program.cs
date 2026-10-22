// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using EchoPlugin;

// 进程外插件示例（RFC-0017）：以 gRPC 服务端实现 xihan.plugin.v1.Plugin，向 stdout 写一行握手行公布端点，
// 日志只走 stderr，stdin 关闭时自行退出。只依赖 protobuf 与 gRPC，不引用任何 XiHan 包。
var endpoint = PluginListener.Create();
var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { Args = args, ApplicationName = "EchoPlugin" });
builder.WebHost.UseKestrelCore();
builder.WebHost.ConfigureKestrel(kestrel => endpoint.Listen(kestrel));
builder.Services.AddLogging(static logging => logging.AddConsole(static console => console.LogToStandardErrorThreshold = LogLevel.Trace));
builder.Services.AddRoutingCore();
builder.Services.AddGrpc();
builder.Services.AddSingleton<PluginState>();
builder.Services.AddSingleton<HostClient>();

var app = builder.Build();
app.MapGrpcService<EchoPluginService>();
await app.StartAsync().ConfigureAwait(false);

// 握手行：宿主读到后才连接（handshake.md §2）
Console.Out.WriteLine(endpoint.HandshakeLine);
await Console.Out.FlushAsync().ConfigureAwait(false);

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
    }

    app.Lifetime.StopApplication();
});

await app.WaitForShutdownAsync().ConfigureAwait(false);
