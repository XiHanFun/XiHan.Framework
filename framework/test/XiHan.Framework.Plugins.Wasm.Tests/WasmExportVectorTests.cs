// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.Json;
using Wasmtime;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Wasm.Instances;
using XiHan.Framework.Plugins.Wasm.Runtime;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// 按 wasm-exports.json 核对宿主对模块导出与导入表的判定
/// </summary>
/// <remarks>
/// 向量是 spec/conformance/vectors/plugin/wasm-exports.json（ABI v1）。每个用例按向量现场生成一份 WAT 模块交给 <see cref="WasmModule.Load"/>，只核对导出与导入表的判定，不实例化
/// </remarks>
public sealed class WasmExportVectorTests : IDisposable
{
    /// <summary>
    /// 校验时用的清单 ID
    /// </summary>
    private const string PluginId = "xihan.tests.abi";

    /// <summary>
    /// 向量文件
    /// </summary>
    private static readonly JsonDocument _vectors = JsonDocument.Parse(File.ReadAllText(SpecVectors.PathOf("plugin", "wasm-exports.json")));

    /// <summary>
    /// 编译模块用的引擎
    /// </summary>
    private readonly Engine _engine = new();

    /// <summary>
    /// 写 WAT 文件的临时目录，释放时删除
    /// </summary>
    private readonly string _directory = Directory.CreateTempSubdirectory("xihan-wasm-abi-").FullName;

    /// <summary>
    /// 已写出的模块个数，用来给文件起不重复的名字
    /// </summary>
    private int _written;

    [Fact]
    public void AbiVersion_IsSupportedByTheHost()
    {
        var version = Root.GetProperty("abiVersion").GetUInt32();

        Assert.Contains(version, PluginProtocol.SupportedVersions);
    }

    [Fact]
    public void ModuleWithTheRequiredExports_IsAccepted_AndExposesTheRequiredCalls()
    {
        var required = Exports("required");

        using var module = Accept(required, []);

        // 必需的调用导出是 (ptr, len) -> i64 的那些，宿主按它们分派
        var calls = required.Where(static e => e.Parameters is ["i32", "i32"] && e.Results is ["i64"]).Select(static e => e.Name).ToArray();
        Assert.Equal(calls, WasmModule.RequiredCalls);
        Assert.Equal(calls, module.Calls);
        Assert.False(module.HasInitialize);
    }

    [Fact]
    public void ModuleMissingARequiredExport_IsRejected()
    {
        var required = Exports("required");
        foreach (var missing in required)
        {
            Reject(required.Where(e => e.Name != missing.Name), [], $"without '{missing.Name}'");
        }
    }

    [Fact]
    public void RequiredFunctionWithAnotherSignature_IsRejected()
    {
        var required = Exports("required");
        foreach (var changed in required.Where(static e => e.Kind == "function"))
        {
            // 多一个参数即签名不符
            var exports = required.Select(e => e.Name == changed.Name ? e with { Parameters = [.. e.Parameters, "i32"] } : e);
            Reject(exports, [], $"'{changed.Name}' with an extra parameter");
        }
    }

    [Fact]
    public void OptionalExports_AreAcceptedWithTheirSignatures()
    {
        var required = Exports("required");
        foreach (var optional in Exports("optional"))
        {
            using var module = Accept([.. required, optional], []);
            if (optional.Name == "_initialize")
            {
                Assert.True(module.HasInitialize);
            }
            else
            {
                Assert.Contains(optional.Name, module.Calls);
                Assert.Contains(optional.Name, WasmModule.OptionalCalls);
            }
        }
    }

    [Fact]
    public void OptionalCallWithAnotherSignature_IsRejected()
    {
        var required = Exports("required");
        foreach (var optional in Exports("optional").Where(static e => e.Name.StartsWith("xihan_", StringComparison.Ordinal)))
        {
            Reject([.. required, optional with { Results = [] }], [], $"'{optional.Name}' without its result");
        }
    }

    [Fact]
    public void RejectedExports_AreRefused()
    {
        var required = Exports("required");
        foreach (var rejected in Root.GetProperty("exports").GetProperty("rejected").EnumerateArray())
        {
            var name = rejected.GetProperty("name").GetString()!;
            Reject([.. required, new Export(name, "function", [], [])], [], $"exporting '{name}' ({rejected.GetProperty("reason").GetString()})");
        }
    }

    [Fact]
    public void HostImports_AreAcceptedWithTheirSignatures_AndRefusedOtherwise()
    {
        var required = Exports("required");
        var imports = Root.GetProperty("imports");
        foreach (var import in imports.GetProperty(WasmModule.ImportModule).EnumerateArray())
        {
            var name = import.GetProperty("name").GetString()!;
            var parameters = Types(import.GetProperty("params"));
            var results = Types(import.GetProperty("results"));

            Accept(required, [new Import(WasmModule.ImportModule, name, parameters, results)]).Dispose();
            Reject(required, [new Import(WasmModule.ImportModule, name, [.. parameters, "i32"], results)], $"import '{name}' with an extra parameter");
        }

        // 导入只来自 xihan 与 WASI：xihan 里不在表上的名字、表外的模块都不被宿主提供
        Reject(required, [new Import(WasmModule.ImportModule, "bogus", [], [])], "an unknown xihan import");
        Reject(required, [new Import("env", "log", ["i32", "i32", "i32"], [])], "an import from module 'env'");

        Assert.True(imports.TryGetProperty(WasmModule.WasiModule, out _));
        Accept(required, [new Import(WasmModule.WasiModule, "fd_write", ["i32", "i32", "i32", "i32"], ["i32"])]).Dispose();
    }

    [Fact]
    public void ImportPermissions_AreTheOnesTheHostChecks()
    {
        var permissions = Root.GetProperty("imports").GetProperty(WasmModule.ImportModule).EnumerateArray()
            .Where(static i => i.TryGetProperty("permission", out _))
            .ToDictionary(static i => i.GetProperty("name").GetString()!, static i => i.GetProperty("permission").GetString()!, StringComparer.Ordinal);

        Assert.Equal(2, permissions.Count);
        Assert.Equal(WasmHostFunctions.PipelinePermission, permissions["invoke"]);
        Assert.Equal(WasmHostFunctions.MessagingPermission, permissions["publish"]);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _engine.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    /// <summary>
    /// 向量的根对象
    /// </summary>
    private static JsonElement Root => _vectors.RootElement;

    /// <summary>
    /// 读出导出表的一节（<c>required</c> 或 <c>optional</c>）
    /// </summary>
    /// <remarks>
    /// 没有 <c>kind</c> 的项是函数
    /// </remarks>
    private static Export[] Exports(string section)
        => [.. Root.GetProperty("exports").GetProperty(section).EnumerateArray().Select(static e => new Export(
            e.GetProperty("name").GetString()!,
            e.TryGetProperty("kind", out var kind) ? kind.GetString()! : "function",
            e.TryGetProperty("params", out var parameters) ? Types(parameters) : [],
            e.TryGetProperty("results", out var results) ? Types(results) : []))];

    /// <summary>
    /// 读出 Wasm 值类型的列表
    /// </summary>
    private static string[] Types(JsonElement array) => [.. array.EnumerateArray().Select(static t => t.GetString()!)];

    /// <summary>
    /// 生成模块并断言宿主接受它，返回校验过的模块
    /// </summary>
    private WasmModule Accept(IEnumerable<Export> exports, IEnumerable<Import> imports)
    {
        var loaded = WasmModule.Load(_engine, Write(exports, imports), PluginId);
        Assert.True(loaded.IsSuccess, loaded.IsFailure ? $"{loaded.Error!.Code}: {loaded.Error.Message}" : null);
        return loaded.ValueOrThrow();
    }

    /// <summary>
    /// 生成模块并断言宿主以模块不合法拒绝它
    /// </summary>
    private void Reject(IEnumerable<Export> exports, IEnumerable<Import> imports, string description)
    {
        var loaded = WasmModule.Load(_engine, Write(exports, imports), PluginId);
        if (loaded.TryGetValue(out var accepted))
        {
            accepted.Dispose();
        }

        Assert.True(loaded.IsFailure, $"A module {description} must be rejected.");
        Assert.Equal(PluginErrors.InvalidModule, loaded.Error!.Code);
    }

    /// <summary>
    /// 把导入与导出写成 WAT 文件
    /// </summary>
    /// <remarks>
    /// 函数体只压入结果类型的零值
    /// </remarks>
    private string Write(IEnumerable<Export> exports, IEnumerable<Import> imports)
    {
        var text = new StringBuilder("(module\n");

        // WAT 要求导入写在所有定义之前
        foreach (var import in imports)
        {
            text.Append(CultureInfo.InvariantCulture, $"  (import \"{import.Module}\" \"{import.Name}\" (func{Signature(import.Parameters, import.Results)}))\n");
        }

        foreach (var export in exports)
        {
            text.Append(export.Kind == "memory"
                ? $"  (memory (export \"{export.Name}\") 1)\n"
                : $"  (func (export \"{export.Name}\"){Signature(export.Parameters, export.Results)}{string.Concat(export.Results.Select(static r => $" {r}.const 0"))})\n");
        }

        text.Append(')');
        var path = Path.Combine(_directory, $"module-{++_written}.wat");
        File.WriteAllText(path, text.ToString());
        return path;
    }

    /// <summary>
    /// WAT 的参数与结果声明
    /// </summary>
    private static string Signature(string[] parameters, string[] results)
        => (parameters.Length > 0 ? $" (param {string.Join(' ', parameters)})" : string.Empty) + (results.Length > 0 ? $" (result {string.Join(' ', results)})" : string.Empty);

    /// <summary>
    /// 导出表的一项
    /// </summary>
    /// <param name="Name">导出名</param>
    /// <param name="Kind"><c>function</c> 或 <c>memory</c></param>
    /// <param name="Parameters">参数的 Wasm 值类型</param>
    /// <param name="Results">结果的 Wasm 值类型</param>
    private sealed record Export(string Name, string Kind, string[] Parameters, string[] Results);

    /// <summary>
    /// 导入表的一项
    /// </summary>
    /// <param name="Module">导入模块名</param>
    /// <param name="Name">导入名</param>
    /// <param name="Parameters">参数的 Wasm 值类型</param>
    /// <param name="Results">结果的 Wasm 值类型</param>
    private sealed record Import(string Module, string Name, string[] Parameters, string[] Results);
}
