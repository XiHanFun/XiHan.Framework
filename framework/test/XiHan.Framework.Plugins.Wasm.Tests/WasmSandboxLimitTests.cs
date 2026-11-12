// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Plugins.Wasm.Tests.Fixtures;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// Wasm 沙箱的资源上限（ADR-0186）
/// </summary>
/// <remarks>
/// 插件不可信：表元素数有上限，单条日志有长度上限，stdout / stderr 不落盘，宿主侧给每次调用计时——epoch 中断不了阻塞在 WASI 宿主函数里的调用（如 <c>poll_oneoff</c> 的睡眠）
/// </remarks>
public sealed class WasmSandboxLimitTests : IDisposable
{
    /// <summary>
    /// 插件工作目录的根
    /// </summary>
    private readonly DirectoryInfo _work = Directory.CreateTempSubdirectory("xihan-wasm-work-");

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            _work.Delete(recursive: true);
        }
        catch (IOException)
        {
            // 留给系统清理临时目录
        }
    }

    [Fact]
    public async Task TableGrowth_IsBounded()
    {
        // 增长失败返回 -1 即回成功；增长成功就走到 unreachable 陷阱
        using var plugin = WatPlugin.Custom(
            imports: string.Empty,
            body: "(if (i32.eq (table.grow $t (ref.null func) (i32.const 10000000)) (i32.const -1)) (then (return {ok}))) (unreachable)",
            fields: "(table $t 1 funcref)");
        await using var host = await StartAsync(plugin);

        var response = await CallAsync(host);

        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
    }

    [Fact]
    public async Task OneLogEntry_IsBounded()
    {
        var logs = new LogCapture();
        using var plugin = WatPlugin.Custom(
            imports: """(import "xihan" "log" (func $log (param i32 i32 i32)))""",
            body: "(call $log (i32.const 2) (i32.const 65536) (i32.const 2097152)) {ok}",
            pages: 40);
        await using var host = await StartAsync(plugin, services => services.AddLogging(builder => builder.AddProvider(logs)));

        var response = await CallAsync(host);

        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
        var entry = Assert.Single(logs.Entries, static e => e.Message.Contains('\0', StringComparison.Ordinal));
        // 修之前这一条有 2 MiB
        Assert.InRange(entry.Message.Length, 1, 32 * 1024);
    }

    [Fact]
    public async Task StandardOutput_IsNotWrittenToDisk()
    {
        // iov 在 60000：起点 65536、长度 1 MiB，写 8 次
        using var plugin = WatPlugin.Custom(
            imports: """(import "wasi_snapshot_preview1" "fd_write" (func $fd_write (param i32 i32 i32 i32) (result i32)))""",
            body: """
                (local $i i32)
                (i32.store (i32.const 60000) (i32.const 65536))
                (i32.store (i32.const 60004) (i32.const 1048576))
                (loop $again
                  (drop (call $fd_write (i32.const 1) (i32.const 60000) (i32.const 1) (i32.const 60008)))
                  (local.set $i (i32.add (local.get $i) (i32.const 1)))
                  (br_if $again (i32.lt_u (local.get $i) (i32.const 8))))
                {ok}
                """,
            pages: 20);
        await using var host = await StartAsync(plugin);

        var response = await CallAsync(host);

        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
        var written = Directory.EnumerateFiles(_work.FullName, "*", SearchOption.AllDirectories).Sum(static f => new FileInfo(f).Length);
        // 修之前 stdout.log 有 8 MiB
        Assert.InRange(written, 0, 64 * 1024);
    }

    [Fact]
    public async Task CallBlockedInAHostFunction_TimesOutOnTheHostSide()
    {
        // poll_oneoff 订阅一个 20 秒的单调时钟：宿主函数里睡眠，epoch 中断不了它
        using var plugin = WatPlugin.Custom(
            imports: """(import "wasi_snapshot_preview1" "poll_oneoff" (func $poll (param i32 i32 i32 i32) (result i32)))""",
            body: """
                (i32.store (i32.const 60016) (i32.const 1))
                (i64.store (i32.const 60024) (i64.const 20000000000))
                (drop (call $poll (i32.const 60000) (i32.const 60100) (i32.const 1) (i32.const 60200)))
                {ok}
                """);
        await using var host = await StartAsync(plugin, configuration: [new("XiHan:Plugins:Wasm:CallTimeoutSeconds", "1")]);
        var watch = Stopwatch.StartNew();

        // 修之前要等插件睡完 20 秒
        var response = await CallAsync(host).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(KernelErrors.DeadlineExceeded, response.Outcome.Error?.Code);
        Assert.InRange(watch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(8));
    }

    [Fact]
    public async Task SleepWithinTheDeadline_CompletesAndReportsTheClockEvent()
    {
        // 睡 50 毫秒：事件数写到 60200，是 1 才回成功
        using var plugin = WatPlugin.Custom(
            imports: """(import "wasi_snapshot_preview1" "poll_oneoff" (func $poll (param i32 i32 i32 i32) (result i32)))""",
            body: """
                (i32.store (i32.const 60016) (i32.const 1))
                (i64.store (i32.const 60024) (i64.const 50000000))
                (if (i32.ne (call $poll (i32.const 60000) (i32.const 60100) (i32.const 1) (i32.const 60200)) (i32.const 0)) (then (unreachable)))
                (if (i32.ne (i32.load (i32.const 60200)) (i32.const 1)) (then (unreachable)))
                {ok}
                """);
        await using var host = await StartAsync(plugin);
        var watch = Stopwatch.StartNew();

        var response = await CallAsync(host);

        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
        Assert.InRange(watch.Elapsed, TimeSpan.FromMilliseconds(40), TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// 装配只带这个插件的宿主
    /// </summary>
    private Task<TestHost> StartAsync(WatPlugin plugin, Action<IServiceCollection>? services = null, KeyValuePair<string, string?>[]? configuration = null)
        => TestHost.StartAsync(
            static b => b.UseFeature<WasmPluginsFeature>(),
            [new("XiHan:Plugins:Wasm:Directories:0", plugin.Directory), new("XiHan:Plugins:Wasm:WorkDirectory", _work.FullName), .. configuration ?? []],
            services);

    /// <summary>
    /// 调用插件唯一的操作
    /// </summary>
    private static async Task<OperationResponse> CallAsync(TestHost host)
        => await host.InvokeOperationRawAsync(new OperationRequest(WatPlugin.Operation, "{}"u8.ToArray()));
}
