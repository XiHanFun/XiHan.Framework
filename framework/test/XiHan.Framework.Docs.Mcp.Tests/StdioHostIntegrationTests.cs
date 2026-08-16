// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 宿主进程集成测试：像真实 MCP 客户端那样把服务端拉起来跑一遍
/// </summary>
/// <remarks>
/// 存在的理由只有一条：stdout 是 JSON-RPC 协议通道，混进去任何一行非协议文本，
/// 连接当场就废。而这条失效路径进程内测不出来——日志提供程序写到哪个流、
/// 宿主启动时打不打横幅、有没有人顺手写了一句 <c>Console.WriteLine</c>，
/// 都只在进程真正跑起来时才有答案。手工验证过一次不算数：它不会在 CI 里重跑。
/// <para>
/// stdin 必须一直握着。用 <c>echo '...' | dotnet ...</c> 灌一行然后关掉管道的话，
/// EOF 会让传输层在响应写出前就拆掉连接，看到的现象是「请求成功了但 stdout 是空的」，
/// 极易被误读成服务端坏了。真实客户端全程持有 stdin，这里也一样。
/// </para>
/// </remarks>
public class StdioHostIntegrationTests
{
    /// <summary>
    /// 服务端构建产物的文件名
    /// </summary>
    private const string ServerAssemblyName = "XiHan.Framework.Docs.Mcp.dll";

    /// <summary>
    /// 构建产物缺失时的跳过说明
    /// </summary>
    private const string SkipReason =
        "未找到服务端构建产物 framework/tool/XiHan.Framework.Docs.Mcp/bin/<配置>/<目标框架>/"
        + ServerAssemblyName
        + "，请先执行：dotnet build framework/tool/XiHan.Framework.Docs.Mcp/XiHan.Framework.Docs.Mcp.csproj -c Release";

    /// <summary>
    /// 出处引用的形态：反引号包住的 docs 路径，后跟起止行号
    /// </summary>
    private static readonly Regex CitationPattern =
        new(@"`(docs/guide/[^`]+\.md)` 第 (\d+)-(\d+) 行", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// 首个响应的等待上限，放宽到足以覆盖 CI 上的冷启动与全量建索引
    /// </summary>
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 后续响应与退出的等待上限
    /// </summary>
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 走完 initialize、notifications/initialized、tools/call 三步，
    /// 标准输出必须自始至终只有 JSON-RPC 行，且检索结果带真实出处
    /// </summary>
    [Fact]
    public void 握手与检索全程标准输出只有协议行()
    {
        var serverAssembly = FindServerAssembly();
        Assert.SkipWhen(serverAssembly is null, SkipReason);

        using var server = McpServerProcess.Start(serverAssembly!, ResolveRepositoryRoot());

        server.Send(BuildRequest(1, "initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "xihan-docs-mcp-tests", version = "1.0" }
        }));
        var initializeResponse = server.ReadResponse(1, HandshakeTimeout);

        Assert.Equal(
            "XiHan.Framework.Docs.Mcp",
            initializeResponse["result"]?["serverInfo"]?["name"]?.GetValue<string>());

        server.Send(BuildNotification("notifications/initialized"));

        server.Send(BuildRequest(2, "tools/call", new
        {
            name = "search_docs",
            arguments = new { query = "分布式事件什么时候发出去", source = "guide", limit = 2 }
        }));
        var searchResponse = server.ReadResponse(2, ResponseTimeout);

        var result = searchResponse["result"];
        Assert.True(
            result?["isError"]?.GetValue<bool>() is not true,
            $"search_docs 返回了错误结果：{searchResponse.ToJsonString()}");

        var text = result?["content"]?[0]?["text"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(text), $"检索结果没有文本内容：{searchResponse.ToJsonString()}");

        // 出处是这个服务端的全部价值所在：拿不出「哪篇文档第几行」，
        // 返回的正文对调用方来说和凭记忆编的没有区别
        var citation = CitationPattern.Match(text!);
        Assert.True(citation.Success, $"检索结果里没有形如 `docs/...md` 第 x-y 行 的出处：\n{Excerpt(text!)}");
        Assert.Contains("docs/guide/event-bus.md", text!, StringComparison.Ordinal);

        var startLine = int.Parse(citation.Groups[2].Value, CultureInfo.InvariantCulture);
        var endLine = int.Parse(citation.Groups[3].Value, CultureInfo.InvariantCulture);
        Assert.True(startLine >= 1, $"起始行号不合法：{startLine}");
        Assert.True(endLine >= startLine, $"行区间倒置：{startLine}-{endLine}");

        var exitCode = server.CloseInputAndWaitForExit(ResponseTimeout);
        Assert.Equal(0, exitCode);

        // 这才是本条测试真正守着的东西：整段会话里 stdout 的每一行都必须是协议行。
        // 只断言「能读到响应」是不够的——日志混在响应前后照样能被读到响应。
        Assert.True(
            server.StandardOutputLines.Count >= 2,
            $"标准输出只有 {server.StandardOutputLines.Count} 行，握手与检索的响应没有全部写出。"
            + $"标准错误：\n{server.StandardError}");

        foreach (var line in server.StandardOutputLines)
        {
            Assert.True(
                IsJsonRpcLine(line),
                $"标准输出出现了非 JSON-RPC 行，MCP 连接会被它打断：\n{line}");
        }
    }

    /// <summary>
    /// 仓库根无效时以退出码 1 结束，原因写在标准错误，标准输出一个字节都不能有
    /// </summary>
    /// <remarks>
    /// 三条断言各自挡一种退化：退出码钉住「异常被接住并转成退出码」——
    /// 把 catch 去掉的话，未处理异常在 Linux 上是 134、Windows 上是另一个大负数，不会是 1；
    /// 标准输出为空钉住「错误信息没走错流」；标准错误的内容钉住「说清楚了是哪个路径、缺什么」，
    /// 而不是一句无从下手的空退出。
    /// </remarks>
    [Fact]
    public void 仓库根无效时退出码为一且原因只写到标准错误()
    {
        var serverAssembly = FindServerAssembly();
        Assert.SkipWhen(serverAssembly is null, SkipReason);

        var invalidRoot = Path.Combine(Path.GetTempPath(), "xihan-invalid-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(invalidRoot);

        try
        {
            using var server = McpServerProcess.Start(serverAssembly!, invalidRoot);

            var exitCode = server.CloseInputAndWaitForExit(HandshakeTimeout);

            Assert.Equal(1, exitCode);
            Assert.Empty(server.StandardOutputLines);
            Assert.Contains("XIHAN_DOCS_ROOT", server.StandardError, StringComparison.Ordinal);
            Assert.Contains(invalidRoot, server.StandardError, StringComparison.Ordinal);
            Assert.Contains("docs", server.StandardError, StringComparison.Ordinal);
            Assert.Contains("framework", server.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(invalidRoot, recursive: true);
        }
    }

    /// <summary>
    /// 截取一段用于失败诊断的文本，整篇章节原文动辄上千字，不该灌进 CI 日志
    /// </summary>
    /// <param name="text">原文</param>
    /// <returns>不超过 400 字符的摘录</returns>
    private static string Excerpt(string text)
    {
        return text.Length <= 400 ? text : text[..400] + "……（已截断）";
    }

    /// <summary>
    /// 构造一条 JSON-RPC 请求，非 ASCII 字符由序列化器转义，不受管道编码影响
    /// </summary>
    /// <param name="id">请求标识</param>
    /// <param name="method">方法名</param>
    /// <param name="parameters">参数对象</param>
    /// <returns>单行 JSON 文本</returns>
    private static string BuildRequest(int id, string method, object parameters)
    {
        return JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters });
    }

    /// <summary>
    /// 构造一条无需响应的 JSON-RPC 通知
    /// </summary>
    /// <param name="method">方法名</param>
    /// <returns>单行 JSON 文本</returns>
    private static string BuildNotification(string method)
    {
        return JsonSerializer.Serialize(new { jsonrpc = "2.0", method });
    }

    /// <summary>
    /// 判断一行标准输出是否为合法的 JSON-RPC 报文
    /// </summary>
    /// <param name="line">标准输出的一行</param>
    /// <returns>是协议行时为 true</returns>
    private static bool IsJsonRpcLine(string line)
    {
        return TryParseJsonObject(line) is { } payload
            && payload["jsonrpc"]?.GetValue<string>() == "2.0";
    }

    /// <summary>
    /// 尝试把一行文本解析成 JSON 对象，不是则返回 null
    /// </summary>
    /// <param name="line">待解析文本</param>
    /// <returns>解析成功时的 JSON 对象</returns>
    private static JsonObject? TryParseJsonObject(string line)
    {
        try
        {
            return JsonNode.Parse(line) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 定位仓库根，与服务端自身的解析方式保持一致
    /// </summary>
    /// <returns>仓库根的绝对路径</returns>
    private static string ResolveRepositoryRoot()
    {
        return DocSourceLocator.ResolveRepositoryRoot(
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable("XIHAN_DOCS_ROOT"));
    }

    /// <summary>
    /// 定位服务端构建产物，找不到时返回 null 由调用方跳过
    /// </summary>
    /// <returns>dll 的绝对路径，或 null</returns>
    /// <remarks>
    /// 目标框架与构建配置都从测试自身的输出目录反推（<c>bin/&lt;配置&gt;/&lt;目标框架&gt;</c>），
    /// 不写死；仓库根复用 <see cref="DocSourceLocator.ResolveRepositoryRoot"/>，
    /// 那本来就是它的职责。
    /// </remarks>
    private static string? FindServerAssembly()
    {
        string repositoryRoot;
        try
        {
            repositoryRoot = ResolveRepositoryRoot();
        }
        catch (DocsRootNotFoundException)
        {
            return null;
        }

        var testOutput = new DirectoryInfo(AppContext.BaseDirectory);
        var targetFramework = testOutput.Name;
        var ownConfiguration = testOutput.Parent?.Name;

        var binRoot = Path.Combine(repositoryRoot, "framework", "tool", "XiHan.Framework.Docs.Mcp", "bin");
        string[] configurations = ownConfiguration is null
            ? ["Release", "Debug"]
            : [ownConfiguration, "Release", "Debug"];

        foreach (var configuration in configurations.Distinct(StringComparer.Ordinal))
        {
            var candidate = Path.Combine(binRoot, configuration, targetFramework, ServerAssemblyName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// 被测服务端子进程：持有 stdin，逐行收集 stdout 与 stderr
    /// </summary>
    private sealed class McpServerProcess : IDisposable
    {
        private readonly BlockingCollection<string> _incoming = new();
        private readonly List<string> _standardOutputLines = [];
        private readonly StringBuilder _standardError = new();
        private readonly Lock _sync = new();
        private readonly Process _process;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="process">已配置好重定向的进程对象</param>
        private McpServerProcess(Process process)
        {
            _process = process;
        }

        /// <summary>
        /// 会话期间标准输出的全部行
        /// </summary>
        public IReadOnlyList<string> StandardOutputLines
        {
            get
            {
                lock (_sync)
                {
                    return [.. _standardOutputLines];
                }
            }
        }

        /// <summary>
        /// 会话期间标准错误的全部内容
        /// </summary>
        public string StandardError
        {
            get
            {
                lock (_sync)
                {
                    return _standardError.ToString();
                }
            }
        }

        /// <summary>
        /// 启动服务端子进程
        /// </summary>
        /// <param name="serverAssembly">服务端 dll 的绝对路径</param>
        /// <param name="docsRoot">传给子进程的 XIHAN_DOCS_ROOT</param>
        /// <returns>子进程包装对象</returns>
        public static McpServerProcess Start(string serverAssembly, string docsRoot)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ResolveDotnetHost(),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
                WorkingDirectory = Path.GetDirectoryName(serverAssembly)!
            };

            startInfo.ArgumentList.Add(serverAssembly);

            // 显式指定仓库根，测试就不依赖子进程从自己所在目录逐层向上找得对不对
            startInfo.Environment["XIHAN_DOCS_ROOT"] = docsRoot;

            var server = new McpServerProcess(new Process { StartInfo = startInfo });

            server._process.OutputDataReceived += (_, e) => server.OnStandardOutput(e.Data);
            server._process.ErrorDataReceived += (_, e) => server.OnStandardError(e.Data);

            server._process.Start();
            server._process.BeginOutputReadLine();
            server._process.BeginErrorReadLine();

            return server;
        }

        /// <summary>
        /// 往 stdin 写一行并立即刷出，写完不关闭管道
        /// </summary>
        /// <param name="payload">单行 JSON 文本</param>
        public void Send(string payload)
        {
            _process.StandardInput.WriteLine(payload);
            _process.StandardInput.Flush();
        }

        /// <summary>
        /// 读取指定标识的响应，途中遇到的通知按真实客户端的做法跳过
        /// </summary>
        /// <param name="id">请求标识</param>
        /// <param name="timeout">等待上限</param>
        /// <returns>响应报文</returns>
        /// <exception cref="TimeoutException">超时未等到对应响应</exception>
        public JsonObject ReadResponse(int id, TimeSpan timeout)
        {
            var elapsed = Stopwatch.StartNew();

            while (true)
            {
                var remaining = timeout - elapsed.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    throw BuildTimeoutException(id, timeout);
                }

                if (!_incoming.TryTake(out var line, (int)remaining.TotalMilliseconds))
                {
                    throw BuildTimeoutException(id, timeout);
                }

                if (TryParseJsonObject(line) is { } payload && MatchesId(payload, id))
                {
                    return payload;
                }
            }
        }

        /// <summary>
        /// 关闭 stdin 并等待子进程自行退出
        /// </summary>
        /// <param name="timeout">等待上限</param>
        /// <returns>退出码</returns>
        /// <exception cref="TimeoutException">超时未退出</exception>
        public int CloseInputAndWaitForExit(TimeSpan timeout)
        {
            _process.StandardInput.Close();

            if (!_process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                throw new TimeoutException(
                    $"子进程在 {timeout.TotalSeconds:F0} 秒内没有退出。标准错误：\n{StandardError}");
            }

            // 无参重载会一并等异步输出回调排空，之后收集到的行才是完整的
            _process.WaitForExit();

            return _process.ExitCode;
        }

        /// <summary>
        /// 结束子进程并释放资源，断言失败时也不留下游离进程
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }

                _process.WaitForExit();
            }
            catch (InvalidOperationException)
            {
                // 进程已经退出并被回收，没有什么可结束的
            }
            finally
            {
                _incoming.Dispose();
                _process.Dispose();
            }
        }

        /// <summary>
        /// 判断报文的标识是否与请求一致
        /// </summary>
        /// <param name="payload">报文</param>
        /// <param name="id">请求标识</param>
        /// <returns>一致时为 true</returns>
        private static bool MatchesId(JsonObject payload, int id)
        {
            return payload["id"] is JsonValue value
                && value.TryGetValue<int>(out var actual)
                && actual == id;
        }

        /// <summary>
        /// 构造带诊断上下文的超时异常，把两个流的内容一并带出来
        /// </summary>
        /// <param name="id">请求标识</param>
        /// <param name="timeout">等待上限</param>
        /// <returns>超时异常</returns>
        private TimeoutException BuildTimeoutException(int id, TimeSpan timeout)
        {
            return new TimeoutException(
                $"等待 id={id} 的响应超过 {timeout.TotalSeconds:F0} 秒。"
                + $"\n已收到的标准输出：\n{string.Join("\n", StandardOutputLines)}"
                + $"\n标准错误：\n{StandardError}");
        }

        /// <summary>
        /// 定位 dotnet 宿主：测试进程自己就跑在它上面，从运行时目录回推比依赖 PATH 稳
        /// </summary>
        /// <returns>dotnet 可执行文件路径</returns>
        private static string ResolveDotnetHost()
        {
            var fileName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

            // 运行时目录形如 <dotnet 根>/shared/Microsoft.NETCore.App/<版本>/
            var runtimeDirectory = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory());
            var dotnetRoot = runtimeDirectory.Parent?.Parent?.Parent?.FullName;

            if (dotnetRoot is not null)
            {
                var candidate = Path.Combine(dotnetRoot, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return fileName;
        }

        /// <summary>
        /// 收到一行标准输出
        /// </summary>
        /// <param name="line">输出行，流结束时为 null</param>
        private void OnStandardOutput(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (_sync)
            {
                _standardOutputLines.Add(line);
            }

            try
            {
                _incoming.Add(line);
            }
            catch (ObjectDisposedException)
            {
                // 测试已经收尾，没有人再读了
            }
        }

        /// <summary>
        /// 收到一行标准错误
        /// </summary>
        /// <param name="line">输出行，流结束时为 null</param>
        private void OnStandardError(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (_sync)
            {
                _standardError.AppendLine(line);
            }
        }
    }
}
