// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 随镜像构建的就绪探针
/// </summary>
/// <remarks>
/// chiseled 运行镜像里没有 shell、curl 与 wget，<c>runtime-deps</c> 里连 dotnet 也没有，Compose 的 healthcheck 在容器内执行，只能靠镜像自带的程序（<c>spec/packaging/app-manifest.md</c>「就绪探针」）。Dockerfile 在构建阶段从这里的源码编译它：AOT 时为原生可执行文件，否则为框架依赖的程序集。源码里不出现 <c>$</c> 与反斜杠，Dockerfile 的 heredoc 不会改写它
/// </remarks>
internal static class HealthProbe
{
    /// <summary>
    /// 运行镜像里的目录
    /// </summary>
    public const string Directory = "/xihan/healthprobe";

    /// <summary>
    /// 程序名
    /// </summary>
    public const string Name = "XiHanHealthProbe";

    /// <summary>
    /// 探测的路径
    /// </summary>
    public const string ReadyPath = "/health/ready";

    /// <summary>
    /// 构建阶段的源码目录
    /// </summary>
    public const string SourceDirectory = "/xihan/healthprobe-src";

    /// <summary>
    /// 构建阶段的发布目录
    /// </summary>
    public const string PublishDirectory = SourceDirectory + "/publish";

    /// <summary>
    /// 入口源码
    /// </summary>
    /// <remarks>
    /// 向本机端口发一次 HTTP/1.1 GET，2xx 退出 0，否则退出 1，参数不合法退出 2
    /// </remarks>
    public const string Program = """
        // 由 xihan pack 生成的就绪探针：向 127.0.0.1:<端口><路径> 发一次 HTTP/1.1 GET，2xx 退出 0，否则退出 1，参数不合法退出 2。
        using System.Net.Sockets;
        using System.Text;

        if (args.Length != 2 || !int.TryParse(args[0], out var port) || port is < 1 or > 65535 || !args[1].StartsWith('/'))
        {
            Console.Error.WriteLine("usage: XiHanHealthProbe <port> <path>");
            return 2;
        }

        var crlf = new string([(char)13, (char)10]);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port, timeout.Token);
            await using var stream = client.GetStream();
            var request = Encoding.ASCII.GetBytes("GET " + args[1] + " HTTP/1.1" + crlf + "Host: localhost" + crlf + "Connection: close" + crlf + crlf);
            await stream.WriteAsync(request, timeout.Token);

            // 状态行形如 HTTP/1.1 200：读满前 12 个字节，第 10 个是状态码的首位
            var status = new byte[12];
            var read = 0;
            while (read < status.Length)
            {
                var count = await stream.ReadAsync(status.AsMemory(read), timeout.Token);
                if (count == 0)
                {
                    break;
                }

                read += count;
            }

            return read == status.Length && status[9] == (byte)'2' ? 0 : 1;
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }

        """;

    /// <summary>
    /// 项目文件
    /// </summary>
    /// <remarks>
    /// 不变区域、可空与隐式 using；AOT 镜像在项目里声明 <c>PublishAot</c>（不在命令行传全局属性）
    /// </remarks>
    public static string Project(bool aot) => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <AssemblyName>{Name}</AssemblyName>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <InvariantGlobalization>true</InvariantGlobalization>
        {(aot ? "    <PublishAot>true</PublishAot>\n" : string.Empty)}  </PropertyGroup>
        </Project>

        """;

    /// <summary>
    /// Compose healthcheck 的 <c>test</c>（YAML 流式序列）
    /// </summary>
    public static string ComposeTest(AppManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var port = manifest.Ports.Http.ToString(CultureInfo.InvariantCulture);
        return manifest.Image.Aot
            ? $"[\"CMD\", \"{Directory}/{Name}\", \"{port}\", \"{ReadyPath}\"]"
            : $"[\"CMD\", \"dotnet\", \"{Directory}/{Name}.dll\", \"{port}\", \"{ReadyPath}\"]";
    }
}
