// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Wasmtime;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Wasm;

/// <summary>
/// 一个已编译并按 ABI v1 校验过导出 / 导入表的模块（<c>spec/plugin/wasm.md</c> §1、§3、§4）。
/// </summary>
internal sealed class WasmModule : IDisposable
{
    /// <summary>
    /// 宿主提供的导入模块名。
    /// </summary>
    public const string ImportModule = "xihan";

    /// <summary>
    /// WASI preview 1 的导入模块名。
    /// </summary>
    public const string WasiModule = "wasi_snapshot_preview1";

    /// <summary>
    /// 必需的 <c>(ptr, len) -> i64</c> 调用导出。
    /// </summary>
    public static readonly string[] RequiredCalls = ["xihan_handshake", "xihan_configure", "xihan_start", "xihan_stop", "xihan_health", "xihan_invoke"];

    /// <summary>
    /// 可选的 <c>(ptr, len) -> i64</c> 调用导出。
    /// </summary>
    public static readonly string[] OptionalCalls = ["xihan_handle_event", "xihan_middleware"];

    /// <summary>
    /// 创建。
    /// </summary>
    private WasmModule(Module module, IReadOnlyList<string> calls, bool hasInitialize)
    {
        Module = module;
        Calls = calls;
        HasInitialize = hasInitialize;
    }

    /// <summary>
    /// 编译后的模块。
    /// </summary>
    public Module Module { get; }

    /// <summary>
    /// 模块导出的全部调用（必需 + 存在的可选）。
    /// </summary>
    public IReadOnlyList<string> Calls { get; }

    /// <summary>
    /// 是否导出 <c>_initialize</c>（WASI reactor 初始化）。
    /// </summary>
    public bool HasInitialize { get; }

    /// <summary>
    /// 是否导出 <c>xihan_handle_event</c>。
    /// </summary>
    public bool HasEventHandler => Calls.Contains("xihan_handle_event", StringComparer.Ordinal);

    /// <summary>
    /// 是否导出 <c>xihan_middleware</c>。
    /// </summary>
    public bool HasMiddleware => Calls.Contains("xihan_middleware", StringComparer.Ordinal);

    /// <summary>
    /// 编译文件（<c>.wasm</c> 二进制；<c>.wat</c> 文本只为示例与测试）并校验导出 / 导入表。
    /// </summary>
    public static Result<WasmModule> Load(Engine engine, string path, string pluginId)
    {
        Module module;
        try
        {
            module = Path.GetExtension(path).Equals(".wat", StringComparison.OrdinalIgnoreCase)
                ? Module.FromText(engine, pluginId, File.ReadAllText(path))
                : Module.FromFile(engine, path);
        }
        catch (Exception exception) when (exception is WasmtimeException or IOException or UnauthorizedAccessException)
        {
            return Invalid(pluginId, exception.Message);
        }

        var validated = Validate(module, pluginId);
        if (validated.IsFailure)
        {
            module.Dispose();
            return validated.Error!;
        }

        return validated;
    }

    /// <inheritdoc />
    public void Dispose() => Module.Dispose();

    /// <summary>
    /// 校验：reactor 形态、内存导出、必需与可选导出的签名、导入只来自 <c>xihan</c> 与 WASI。
    /// </summary>
    private static Result<WasmModule> Validate(Module module, string pluginId)
    {
        var exports = module.Exports.ToDictionary(static e => e.Name, static e => e, StringComparer.Ordinal);
        if (exports.ContainsKey("_start"))
        {
            return Invalid(pluginId, "command modules exporting '_start' are not accepted; build a WASI reactor");
        }

        if (!exports.TryGetValue("memory", out var memory) || memory is not MemoryExport)
        {
            return Invalid(pluginId, "the module must export its linear memory as 'memory'");
        }

        if (!HasFunction(exports, "xihan_abi_version", [], [ValueKind.Int32]))
        {
            return Invalid(pluginId, "missing export 'xihan_abi_version' () -> i32");
        }

        if (!HasFunction(exports, "xihan_alloc", [ValueKind.Int32], [ValueKind.Int32]))
        {
            return Invalid(pluginId, "missing export 'xihan_alloc' (i32) -> i32");
        }

        if (!HasFunction(exports, "xihan_free", [ValueKind.Int32, ValueKind.Int32], []))
        {
            return Invalid(pluginId, "missing export 'xihan_free' (i32, i32)");
        }

        var calls = new List<string>();
        foreach (var name in RequiredCalls)
        {
            if (!HasFunction(exports, name, [ValueKind.Int32, ValueKind.Int32], [ValueKind.Int64]))
            {
                return Invalid(pluginId, $"missing export '{name}' (i32, i32) -> i64");
            }

            calls.Add(name);
        }

        foreach (var name in OptionalCalls)
        {
            if (!exports.ContainsKey(name))
            {
                continue;
            }

            if (!HasFunction(exports, name, [ValueKind.Int32, ValueKind.Int32], [ValueKind.Int64]))
            {
                return Invalid(pluginId, $"export '{name}' must have the signature (i32, i32) -> i64");
            }

            calls.Add(name);
        }

        var hasInitialize = exports.TryGetValue("_initialize", out var initialize) && initialize is FunctionExport { Parameters.Count: 0, Results.Count: 0 };
        foreach (var import in module.Imports)
        {
            if (import.ModuleName == WasiModule)
            {
                continue;
            }

            if (import.ModuleName != ImportModule || import is not FunctionImport function)
            {
                return Invalid(pluginId, $"import '{import.ModuleName}.{import.Name}' is not provided by the host");
            }

            var ok = function.Name switch
            {
                "log" => Matches(function.Parameters, function.Results, [ValueKind.Int32, ValueKind.Int32, ValueKind.Int32], []),
                "invoke" or "publish" => Matches(function.Parameters, function.Results, [ValueKind.Int32, ValueKind.Int32], [ValueKind.Int64]),
                _ => false,
            };
            if (!ok)
            {
                return Invalid(pluginId, $"import '{ImportModule}.{function.Name}' is unknown or has an unexpected signature");
            }
        }

        return new WasmModule(module, calls, hasInitialize);
    }

    /// <summary>
    /// 名字对应的导出是给定签名的函数。
    /// </summary>
    private static bool HasFunction(Dictionary<string, Export> exports, string name, ValueKind[] parameters, ValueKind[] results)
        => exports.TryGetValue(name, out var export) && export is FunctionExport function && Matches(function.Parameters, function.Results, parameters, results);

    /// <summary>
    /// 签名相符。
    /// </summary>
    private static bool Matches(IReadOnlyList<ValueKind> parameters, IReadOnlyList<ValueKind> results, ValueKind[] expectedParameters, ValueKind[] expectedResults)
        => parameters.SequenceEqual(expectedParameters) && results.SequenceEqual(expectedResults);

    /// <summary>
    /// 模块不合法。
    /// </summary>
    private static Error Invalid(string pluginId, string reason)
        => new(PluginErrors.InvalidModule, PluginMessages.ModuleInvalid, MessageArguments.Of(("id", pluginId), ("reason", reason)));
}
