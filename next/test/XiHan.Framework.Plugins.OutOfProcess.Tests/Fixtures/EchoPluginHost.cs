// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

/// <summary>
/// 以已构建的 EchoPlugin 输出目录为插件目录启动测试宿主的辅助。
/// </summary>
internal static class EchoPluginHost
{
    /// <summary>
    /// 示例插件的清单 ID。
    /// </summary>
    public const string PluginId = "xihan.samples.echo";

    /// <summary>
    /// 示例插件的输出目录。
    /// </summary>
    /// <remarks>
    /// 测试输出在 .../bin/&lt;Configuration&gt;/net10.0/，示例在 samples/EchoPlugin/bin/&lt;Configuration&gt;/net10.0/。
    /// </remarks>
    public static string Directory { get; } = Locate();

    /// <summary>
    /// 规范的向量目录。
    /// </summary>
    public static string VectorsDirectory { get; } = Path.Combine(LocateRoot(), "spec", "conformance", "vectors", "plugin");

    /// <summary>
    /// 宿主配置：插件目录、授予的权限与较短的健康轮询。
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string?>> Configuration(bool grantPipeline = true, params (string Key, string Value)[] extra)
    {
        var values = new List<KeyValuePair<string, string?>>
        {
            new("XiHan:Plugins:OutOfProcess:Directories:0", Directory),
            new("XiHan:Plugins:OutOfProcess:HealthIntervalSeconds", "1"),
            new("XiHan:Plugins:OutOfProcess:StopGraceSeconds", "5"),
            new("XiHan:Echo:Greeting", "hello"),
        };
        if (grantPipeline)
        {
            values.Add(new("XiHan:Plugins:OutOfProcess:Grants:xihan.samples.echo:0", "capability:kernel.pipeline"));
        }

        values.AddRange(extra.Select(static e => new KeyValuePair<string, string?>(e.Key, e.Value)));
        return values;
    }

    /// <summary>
    /// 启动只装配进程外插件特性的宿主。
    /// </summary>
    public static Task<TestHost> StartAsync(Action<AppBuilder>? configure = null, bool grantPipeline = true, params (string Key, string Value)[] extra)
        => TestHost.StartAsync(
            b =>
            {
                b.UseFeature<OutOfProcessPluginsFeature>();
                configure?.Invoke(b);
            },
            Configuration(grantPipeline, extra));

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
    public static OutOfProcessPluginHost PluginHost(TestHost host) => host.Services.GetRequiredService<OutOfProcessPluginHost>();

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
    /// 从测试输出目录推出示例插件的输出目录。
    /// </summary>
    private static string Locate()
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory);
        var configuration = output.Parent!.Name;
        return Path.Combine(LocateRoot(), "samples", "EchoPlugin", "bin", configuration, output.Name);
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
