// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace XiHan.Framework.Plugins.Wasm.Tests.Fixtures;

/// <summary>
/// 用 WAT 文本现场生成的最小 Wasm 插件（宿主接受 <c>.wat</c>，只为测试）。
/// </summary>
/// <remarks>
/// 一个操作 <c>wat.call</c>：调用一次宿主导入（<c>xihan.invoke</c> 或 <c>xihan.publish</c>），请求是生成时写死的 JSON。经 <c>invoke</c> 时把宿主的 <c>InvokeResponse</c> 原样作为自己的应答，经 <c>publish</c> 时丢弃宿主应答、回成功。其余导出回固定应答；清单的权限由用例决定，用来核对回调的权限与截止时间。释放时删除插件目录。
/// </remarks>
internal sealed class WatPlugin : IDisposable
{
    /// <summary>
    /// 清单 ID。
    /// </summary>
    public const string PluginId = "xihan.tests.wat";

    /// <summary>
    /// 唯一的操作。
    /// </summary>
    public const string Operation = "wat.call";

    /// <summary>
    /// 数据段起点。
    /// </summary>
    private const int DataStart = 1024;

    /// <summary>
    /// 堆的起点（第二页）：<c>xihan_alloc</c> 从这里线性分配，到线性内存末尾时回卷。
    /// </summary>
    private const int HeapStart = 65536;

    /// <summary>
    /// 线性内存页数。
    /// </summary>
    private const int Pages = 4;

    private WatPlugin(string directory) => Directory = directory;

    /// <summary>
    /// 插件目录（含清单与 <c>plugin.wat</c>）。
    /// </summary>
    public string Directory { get; }

    /// <summary>
    /// 生成一个经 <c>xihan.invoke</c> 调用宿主操作的插件。
    /// </summary>
    /// <remarks>
    /// <paramref name="operation"/> 是宿主操作 ID；请求不带元数据，即插件自己不给截止时间。
    /// </remarks>
    public static WatPlugin Invoking(string operation, IReadOnlyList<string> permissions)
        => Create("invoke", new JsonObject { ["operation"] = operation, ["payload"] = Convert.ToBase64String("""{"name":"wat"}"""u8) }, permissions, null);

    /// <summary>
    /// 生成一个声明 <see cref="Operation"/> 要求权限码 <paramref name="permission"/> 的插件。
    /// </summary>
    /// <remarks>
    /// 用来核对宿主拒绝引用了未登记权限码的代理操作；导出与 <see cref="Invoking"/> 相同。
    /// </remarks>
    public static WatPlugin Guarded(string permission)
        => Create("invoke", new JsonObject { ["operation"] = "pings.create", ["payload"] = Convert.ToBase64String("{}"u8) }, [], null, permission);

    /// <summary>
    /// 生成一个贡献中间件的插件。
    /// </summary>
    /// <remarks>
    /// <paramref name="pipeline"/> 是清单的 <c>contributes.pipeline</c>；<c>xihan_middleware</c> 总是回 <c>CONTINUE</c>。
    /// </remarks>
    public static WatPlugin WithMiddleware(JsonArray pipeline)
        => Create("invoke", new JsonObject { ["operation"] = "pings.create", ["payload"] = Convert.ToBase64String("{}"u8) }, [], pipeline);

    /// <summary>
    /// 生成一个经 <c>xihan.publish</c> 发布一条消息的插件。
    /// </summary>
    public static WatPlugin Publishing(string topic, IReadOnlyList<string> permissions)
        => Create("publish", new JsonObject
        {
            ["envelopes"] = new JsonArray(new JsonObject
            {
                ["topic"] = topic,
                ["id"] = "wat-1",
                ["source"] = "wat",
                ["type"] = "wat.created",
                ["data"] = Convert.ToBase64String("{}"u8),
            }),
        }, permissions, null);

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 留给系统清理临时目录
        }
    }

    /// <summary>
    /// 写清单与模块文本。
    /// </summary>
    private static WatPlugin Create(string import, JsonObject request, IReadOnlyList<string> permissions, JsonArray? pipeline, string? operationPermission = null)
    {
        var contributes = new JsonObject { ["operations"] = new JsonArray(Operation) };
        if (pipeline is not null)
        {
            contributes["pipeline"] = pipeline;
        }

        var manifest = new JsonObject
        {
            ["id"] = PluginId,
            ["package"] = "WatPlugin",
            ["version"] = "1.0.0",
            ["primitives"] = "1",
            ["stability"] = "experimental",
            ["kind"] = "plugin",
            ["isolation"] = "wasm",
            ["contributes"] = contributes,
            ["permissions"] = new JsonArray(permissions.Select(static p => (JsonNode)p).ToArray()),
            ["activation"] = new JsonArray("onStartup"),
            ["entry"] = "plugin.wat",
        }.ToJsonString();
        var handshake = new JsonObject
        {
            ["protocolVersion"] = 1,
            ["primitives"] = 1,
            ["manifest"] = manifest,
            ["operations"] = new JsonArray(operationPermission is null
                ? new JsonObject { ["id"] = Operation, ["stability"] = "experimental" }
                : new JsonObject { ["id"] = Operation, ["stability"] = "experimental", ["permission"] = operationPermission }),
        }.ToJsonString();

        var segments = new StringBuilder();
        var offset = DataStart;
        (int Offset, int Length) Place(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            segments.Append(CultureInfo.InvariantCulture, $"  (data (i32.const {offset}) \"{Escape(bytes)}\")\n");
            var placed = (offset, bytes.Length);
            offset += (bytes.Length + 7) & ~7;
            return placed;
        }

        var handshakeData = Place(handshake);
        var okData = Place("""{"status":{"ok":true}}""");
        var healthData = Place("""{"state":"LIFECYCLE_STATE_READY"}""");
        var requestData = Place(request.ToJsonString());
        var continueData = Place("""{"action":"MIDDLEWARE_ACTION_CONTINUE"}""");
        if (offset >= HeapStart)
        {
            throw new InvalidOperationException("The data segments overflow into the heap.");
        }

        // invoke：宿主的 InvokeResponse 原样作为自己的应答；publish：丢弃宿主的 PublishResponse，回成功
        var call = import == "invoke"
            ? $"(call $callback (i32.const {requestData.Offset}) (i32.const {requestData.Length}))"
            : $"(drop (call $callback (i32.const {requestData.Offset}) (i32.const {requestData.Length}))) (call $pack (i32.const {okData.Offset}) (i32.const {okData.Length}))";
        // 贡献了中间件才导出 xihan_middleware：总是回 CONTINUE
        var middleware = pipeline is null
            ? string.Empty
            : $"(func (export \"xihan_middleware\") (param i32 i32) (result i64) (call $pack (i32.const {continueData.Offset}) (i32.const {continueData.Length})))";
        var module = $$"""
            (module
              (import "xihan" "{{import}}" (func $callback (param i32 i32) (result i64)))
              (memory (export "memory") {{Pages}})
              (global $heap (mut i32) (i32.const {{HeapStart}}))
            {{segments}}
              (func $pack (param $pointer i32) (param $length i32) (result i64)
                (i64.or (i64.shl (i64.extend_i32_u (local.get $pointer)) (i64.const 32)) (i64.extend_i32_u (local.get $length))))
              (func (export "xihan_abi_version") (result i32) (i32.const 1))
              (func (export "xihan_alloc") (param $size i32) (result i32) (local $pointer i32)
                (if (i32.gt_u (i32.add (global.get $heap) (local.get $size)) (i32.const {{Pages * 65536}}))
                  (then (global.set $heap (i32.const {{HeapStart}}))))
                (local.set $pointer (global.get $heap))
                (global.set $heap (i32.add (global.get $heap) (local.get $size)))
                (local.get $pointer))
              (func (export "xihan_free") (param i32 i32))
              (func (export "xihan_handshake") (param i32 i32) (result i64) (call $pack (i32.const {{handshakeData.Offset}}) (i32.const {{handshakeData.Length}})))
              (func (export "xihan_configure") (param i32 i32) (result i64) (call $pack (i32.const {{okData.Offset}}) (i32.const {{okData.Length}})))
              (func (export "xihan_start") (param i32 i32) (result i64) (call $pack (i32.const {{okData.Offset}}) (i32.const {{okData.Length}})))
              (func (export "xihan_stop") (param i32 i32) (result i64) (call $pack (i32.const {{okData.Offset}}) (i32.const {{okData.Length}})))
              (func (export "xihan_health") (param i32 i32) (result i64) (call $pack (i32.const {{healthData.Offset}}) (i32.const {{healthData.Length}})))
              (func (export "xihan_invoke") (param i32 i32) (result i64) {{call}})
              {{middleware}}
            )
            """;

        var directory = Path.Combine(Path.GetTempPath(), "xihan-plugin-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "xihan.manifest.json"), manifest);
        File.WriteAllText(Path.Combine(directory, "plugin.wat"), module);
        return new WatPlugin(directory);
    }

    /// <summary>
    /// 字节逐个写成 WAT 字符串的 <c>\hh</c> 转义。
    /// </summary>
    private static string Escape(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length * 3);
        foreach (var value in bytes)
        {
            builder.Append('\\').Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
