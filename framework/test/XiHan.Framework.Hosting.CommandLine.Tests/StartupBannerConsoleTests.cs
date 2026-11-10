// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Security.InMemory;

namespace XiHan.Framework.Hosting.CommandLine.Tests;

/// <summary>
/// 以命令行方式执行命令时启动 Banner 不进 stdout
/// </summary>
/// <remarks>
/// stdout 整段是命令结果（JSON、MCP stdio 的消息流同理）：缺省两边都没有 Banner，配置打开后只写 stderr。用例替换进程的标准输出与标准错误，归入不并行的集合
/// </remarks>
[Collection(nameof(ProcessConsoleGroup))]
public sealed class StartupBannerConsoleTests
{
    /// <summary>
    /// Banner 里框架名与版本那一行，不受颜色序列影响
    /// </summary>
    private static readonly string _frameworkLine = $"XiHan Framework v{FrameworkInfo.Version}";

    [Fact]
    public async Task RunXiHanCommandLineAsync_KeepsTheBannerOutOfStandardOutput()
    {
        var (code, output, error) = await RunAsync(enabled: null);
        Assert.Equal(CommandLineExitCodes.Success, code);
        // stdout 整段是命令的 JSON 结果，解析失败即说明混进了别的内容
        Assert.Equal("widget", JsonDocument.Parse(output).RootElement.GetProperty("name").GetString());
        Assert.DoesNotContain(_frameworkLine, error, StringComparison.Ordinal);

        (code, output, error) = await RunAsync(enabled: "true");
        Assert.Equal(CommandLineExitCodes.Success, code);
        Assert.Equal("widget", JsonDocument.Parse(output).RootElement.GetProperty("name").GetString());
        Assert.DoesNotContain(_frameworkLine, output, StringComparison.Ordinal);
        Assert.Contains(_frameworkLine, error, StringComparison.Ordinal);
    }

    /// <summary>
    /// 执行一条命令并收集两路输出
    /// </summary>
    /// <remarks>
    /// 以命令行方式执行一次 <c>invoke orders.get</c>，返回退出码与进程 stdout、stderr 上的全部文本
    /// </remarks>
    /// <param name="enabled"><c>XiHan:Kernel:Banner:Enabled</c> 的值；为 <see langword="null"/> 时不配置</param>
    private static async Task<(int Code, string Output, string Error)> RunAsync(string? enabled)
    {
        var originalOutput = Console.Out;
        var originalError = Console.Error;
        var output = new StringWriter();
        var error = new StringWriter();
        // 在构建宿主之前替换：控制台日志与 Banner 选项在构建或首次解析时取 stderr
        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            var builder = Host.CreateApplicationBuilder();
            if (enabled is not null)
            {
                builder.Configuration["XiHan:Kernel:Banner:Enabled"] = enabled;
            }

            builder.AddXiHan(static xihan => xihan.UseMinimalProfile().UseFeature<InMemorySecurityFeature>().UseFeature<EchoFeature>());
            builder.Services.AddXiHanCommandLine();
            int code;
            using (var host = builder.Build())
            {
                code = await host.RunXiHanCommandLineAsync(["invoke", "orders.get", "--json", """{"name":"widget"}"""], TestContext.Current.CancellationToken);
            }

            // 宿主释放后控制台日志已排空，读到的是完整输出
            return (code, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }
    }
}
