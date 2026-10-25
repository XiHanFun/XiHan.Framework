// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.Wasm.Tests.Fixtures;

/// <summary>
/// 以仓库里提交的 EchoWasmPlugin 产物目录（samples/EchoWasmPlugin/plugin）启动测试宿主的辅助。
/// </summary>
internal static class EchoWasmPluginHost
{
    /// <summary>
    /// 示例插件的清单 ID。
    /// </summary>
    public const string PluginId = "xihan.samples.echowasm";

    /// <summary>
    /// 示例插件目录（含清单与 echo.wasm）。
    /// </summary>
    public static string Directory { get; } = Path.Combine(LocateRoot(), "samples", "EchoWasmPlugin", "plugin");

    /// <summary>
    /// 规范的向量目录。
    /// </summary>
    public static string VectorsDirectory { get; } = Path.Combine(LocateRoot(), "spec", "conformance", "vectors", "plugin");

    /// <summary>
    /// 宿主配置：插件目录、授予的权限、短健康轮询与细 epoch 粒度。
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string?>> Configuration(params (string Key, string Value)[] extra)
    {
        var values = new List<KeyValuePair<string, string?>>
        {
            new("XiHan:Plugins:Wasm:Directories:0", Directory),
            new("XiHan:Plugins:Wasm:Grants:xihan.samples.echowasm:0", "capability:kernel.pipeline"),
            new("XiHan:Plugins:Wasm:HealthIntervalSeconds", "1"),
            new("XiHan:Plugins:Wasm:EpochIntervalMilliseconds", "5"),
            new("XiHan:Echo:Greeting", "hello"),
        };
        values.AddRange(extra.Select(static e => new KeyValuePair<string, string?>(e.Key, e.Value)));
        return values;
    }

    /// <summary>
    /// 启动只装配 Wasm 插件特性的宿主。
    /// </summary>
    public static Task<TestHost> StartAsync(Action<AppBuilder>? configure = null, params (string Key, string Value)[] extra)
        => TestHost.StartAsync(
            b =>
            {
                b.UseFeature<WasmPluginsFeature>();
                configure?.Invoke(b);
            },
            Configuration(extra));

    /// <summary>
    /// 经内核调用器调用一个操作，载荷为 JSON 文本。
    /// </summary>
    public static async Task<OperationResponse> InvokeAsync(TestHost host, string operation, string json, IReadOnlyDictionary<string, string>? baggage = null)
        => await host.InvokeOperationRawAsync(new OperationRequest(operation, Encoding.UTF8.GetBytes(json)), baggage);

    /// <summary>
    /// 解析响应载荷。
    /// </summary>
    public static JsonElement Payload(OperationResponse response)
        => JsonDocument.Parse(response.Payload).RootElement.Clone();

    /// <summary>
    /// 当前插件宿主。
    /// </summary>
    public static WasmPluginHost PluginHost(TestHost host) => host.Services.GetRequiredService<WasmPluginHost>();

    /// <summary>
    /// 轮询直到条件成立或超时。
    /// </summary>
    public static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(100);
        }

        return condition();
    }

    /// <summary>
    /// 向上找到含解决方案文件的目录。
    /// </summary>
    private static string LocateRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.Next.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The solution root was not found.");
    }
}
