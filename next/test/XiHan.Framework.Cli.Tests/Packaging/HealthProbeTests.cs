// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using XiHan.Framework.Cli.Packaging;

namespace XiHan.Framework.Cli.Tests.Packaging;

/// <summary>
/// Dockerfile 内联的就绪探针。
/// </summary>
/// <remarks>
/// 从生成的 Dockerfile 里取出 heredoc 写成项目，用本机 SDK 构建，再对回环地址上的桩服务运行：确认镜像里构建的就是能用的探针（<c>spec/packaging/app-manifest.md</c>「就绪探针」）。
/// </remarks>
public sealed partial class HealthProbeTests
{
    [Fact]
    public async Task Probe_FromTheGeneratedDockerfile_BuildsAndReportsReadiness()
    {
        using var fixture = new ManifestFixture("""{ "name": "simple", "project": "Orders.csproj" }""");
        var manifest = AppManifestReader.Read(fixture.ManifestPath).ValueOrThrow();
        var dockerfile = Packager.Generate(manifest, fixture.Directory, fixture.OutputDirectory, PackKinds.Dockerfile).Single(static o => o.Path == "Dockerfile").Content;
        var source = Directory.CreateTempSubdirectory("xihan-healthprobe-");
        try
        {
            var heredocs = Heredoc().Matches(dockerfile);
            Assert.Equal(["XiHanHealthProbe.csproj", "Program.cs"], heredocs.Select(static m => Path.GetFileName(m.Groups["path"].Value)));
            Assert.DoesNotContain(heredocs, static m => m.Groups["body"].Value.Contains('$', StringComparison.Ordinal) || m.Groups["body"].Value.Contains('\\', StringComparison.Ordinal));
            foreach (Match heredoc in heredocs)
            {
                await File.WriteAllTextAsync(Path.Combine(source.FullName, Path.GetFileName(heredoc.Groups["path"].Value)), heredoc.Groups["body"].Value);
            }

            var output = Path.Combine(source.FullName, "out");
            var build = await RunAsync(["build", Path.Combine(source.FullName, "XiHanHealthProbe.csproj"), "--configuration", "Release", "--output", output]);
            Assert.True(build.ExitCode == 0, build.Output);
            var probe = Path.Combine(output, "XiHanHealthProbe.dll");

            int port;
            using (var server = new StatusServer())
            {
                port = server.Port;
                Assert.Equal(0, (await RunAsync([probe, Port(port), "/health/ready"])).ExitCode);
                Assert.Equal(1, (await RunAsync([probe, Port(port), "/health/live"])).ExitCode);
            }

            // 服务已关：连接被拒，按未就绪退出 1；参数不合法退出 2
            Assert.Equal(1, (await RunAsync([probe, Port(port), "/health/ready"])).ExitCode);
            Assert.Equal(2, (await RunAsync([probe, "not-a-port", "/health/ready"])).ExitCode);
        }
        finally
        {
            source.Delete(recursive: true);
        }
    }

    /// <summary>
    /// 端口的文本。
    /// </summary>
    private static string Port(int port) => port.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// 运行 <c>dotnet</c> 并收集输出。
    /// </summary>
    /// <remarks>
    /// 不继承 <c>MSBuild*</c> 环境变量，免得测试宿主的构建环境渗进子构建；最多等 3 分钟。
    /// </remarks>
    private static async Task<(int ExitCode, string Output)> RunAsync(IReadOnlyList<string> arguments)
    {
        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        foreach (var name in info.Environment.Keys.Where(static k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            info.Environment.Remove(name);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("dotnet could not be started.");
        process.StandardInput.Close();
        var output = new StringBuilder();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await process.WaitForExitAsync(timeout.Token);
        output.Append(await stdout).Append(await stderr);
        return (process.ExitCode, output.ToString());
    }

    /// <summary>
    /// Dockerfile 里的 <c>COPY &lt;&lt;"EOF" 路径</c> heredoc。
    /// </summary>
    [GeneratedRegex("^COPY <<\"EOF\" (?<path>\\S+)\\n(?<body>.*?)^EOF$", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex Heredoc();
}
