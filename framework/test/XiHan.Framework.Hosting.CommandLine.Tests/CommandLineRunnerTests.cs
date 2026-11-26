// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Hosting.CommandLine.Runtime;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Security.InMemory;

namespace XiHan.Framework.Hosting.CommandLine.Tests;

/// <summary>
/// 命令行运行器
/// </summary>
/// <remarks>
/// 帮助、内置命令、invoke 与简写、载荷来源、上下文选项、失败映射与退出码
/// </remarks>
[Collection(nameof(ProcessEnvironmentGroup))]
public sealed class CommandLineRunnerTests : IAsyncLifetime
{
    /// <summary>
    /// 两个主体：alice 是 editor（可读写订单），bob 是 viewer（只读）
    /// </summary>
    private static readonly KeyValuePair<string, string?>[] _security =
    [
        new("XiHan:Security:InMemory:Principals:alice:ApiKey", "dev-alice-key"),
        new("XiHan:Security:InMemory:Principals:alice:Roles", "editor"),
        new("XiHan:Security:InMemory:Principals:bob:ApiKey", "dev-bob-key"),
        new("XiHan:Security:InMemory:Principals:bob:Roles", "viewer"),
        new("XiHan:Security:InMemory:Grants:role:editor", "orders.read,orders.write"),
        new("XiHan:Security:InMemory:Grants:role:viewer", "orders.read"),
    ];

    /// <summary>
    /// 已启动的宿主
    /// </summary>
    private IHost _host = null!;

    /// <summary>
    /// 运行器
    /// </summary>
    private CommandLineRunner _runner = null!;

    /// <summary>
    /// 启动宿主：Minimal 组合（本地化）+ 内存安全 + 回显特性 + 命令行宿主
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(_security);
        builder.AddXiHan(static xihan => xihan.UseMinimalProfile().UseFeature<InMemorySecurityFeature>().UseFeature<EchoFeature>());
        builder.Services.AddXiHanCommandLine();
        builder.Services.AddSingleton<OriginLog>();
        _host = builder.Build();
        await _host.StartAsync();
        _runner = _host.Services.GetRequiredService<CommandLineRunner>();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    /// <summary>
    /// 运行一条命令行，返回退出码、stdout 与 stderr
    /// </summary>
    private async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await _runner.RunAsync(args, output, error, CancellationToken.None);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>
    /// 解析 stdout 里的 JSON
    /// </summary>
    private static JsonElement Json(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task Help_AndBuiltInCommands()
    {
        // 没有命令：帮助写到 stderr，退出码 64；help：写到 stdout，退出码 0
        var (usage, _, usageError) = await RunAsync();
        Assert.Equal(CommandLineExitCodes.Usage, usage);
        Assert.Contains("invoke", usageError, StringComparison.Ordinal);
        var (help, helpOutput, _) = await RunAsync("help");
        Assert.Equal(CommandLineExitCodes.Success, help);
        Assert.Contains("graph", helpOutput, StringComparison.Ordinal);
        Assert.Contains("Usage:", helpOutput, StringComparison.Ordinal);
        Assert.Equal(CommandLineExitCodes.Success, (await RunAsync("graph", "--help")).ExitCode);

        var (_, graph, _) = await RunAsync("graph");
        Assert.Contains("xihan.test.echo", graph, StringComparison.Ordinal);
        var (_, pipeline, _) = await RunAsync("pipeline");
        Assert.Contains("AuthorizationMiddleware", pipeline, StringComparison.Ordinal);
        var (_, operations, _) = await RunAsync("operations");
        Assert.Contains("orders.create\tStable\tHttp, CommandLine\tpermission=orders.write", operations, StringComparison.Ordinal);
        Assert.Contains("orders.get\tStable\tCommandLine\t-", operations, StringComparison.Ordinal);

        var (unknown, _, unknownError) = await RunAsync("nope");
        Assert.Equal(CommandLineExitCodes.Usage, unknown);
        Assert.Equal(CommandLineErrors.CommandUnknown, Json(unknownError).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Invoke_BindsPayloadFromJsonFileOrFields_AndPropagatesContext()
    {
        var (code, output, _) = await RunAsync("invoke", "orders.get", "--json", """{"name":"widget","page":2}""");
        Assert.Equal(CommandLineExitCodes.Success, code);
        var reply = Json(output);
        Assert.Equal(("orders.get", "widget", 2), (reply.GetProperty("operation").GetString(), reply.GetProperty("name").GetString(), reply.GetProperty("page").GetInt32()));
        // 调用来源：命令行与被调用的操作 ID，没有远端地址与 User-Agent
        Assert.Equal([new InvocationOrigin { Protocol = InvocationProtocols.CommandLine, Target = "orders.get" }], _host.Services.GetRequiredService<OriginLog>().Origins);

        // 简写 + 字段选项（字符串按序列化上下文读成数字，重复选项成数组）+ 上下文选项
        var (shorthand, shorthandOutput, _) = await RunAsync("orders", "get", "--name", "gizmo", "--page", "3", "--tags", "a", "--tags", "b", "--tenant", "acme", "--correlation", "c-1", "--timeout", "30");
        Assert.Equal(CommandLineExitCodes.Success, shorthand);
        reply = Json(shorthandOutput);
        Assert.Equal(("gizmo", 3, "acme", "c-1", true), (reply.GetProperty("name").GetString(), reply.GetProperty("page").GetInt32(), reply.GetProperty("tenant").GetString(), reply.GetProperty("correlation").GetString(), reply.GetProperty("hasDeadline").GetBoolean()));
        Assert.Equal(["a", "b"], reply.GetProperty("tags").EnumerateArray().Select(static t => t.GetString()));

        // 数据文件，@ 前缀可选；没有任何字段时是空对象
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        await File.WriteAllTextAsync(path, """{"name":"from-file"}""");
        try
        {
            Assert.Equal("from-file", Json((await RunAsync("orders", "get", "--data", "@" + path)).Output).GetProperty("name").GetString());
        }
        finally
        {
            File.Delete(path);
        }

        Assert.Equal(JsonValueKind.Null, Json((await RunAsync("orders", "get")).Output).GetProperty("name").ValueKind);
    }

    [Fact]
    public async Task Invoke_WritesFieldOptionsAsTheTypesOfTheRequestSchema()
    {
        // 数字样的值进字符串字段不变；超出 double 精度的整数不丢位；无值的标志是 true；单值进数组字段是一元数组；引用定义与 anyOf 都展开
        var (code, output, error) = await RunAsync("orders", "typed", "--name", "42", "--times", "2", "--total", "9007199254740993", "--ratio", "1e3", "--verbose", "--level", "3", "--tone", "warm", "--sizes", "1", "--tags", "7", "--notes", "5");
        Assert.Equal((CommandLineExitCodes.Success, string.Empty), (code, error));
        var reply = Json(output);
        Assert.Equal(("42", 2, 9007199254740993L, 1000d, true, 3, "warm"), (reply.GetProperty("name").GetString(), reply.GetProperty("times").GetInt32(), reply.GetProperty("total").GetInt64(), reply.GetProperty("ratio").GetDouble(), reply.GetProperty("verbose").GetBoolean(), reply.GetProperty("level").GetInt32(), reply.GetProperty("tone").GetString()));
        Assert.Equal([1], reply.GetProperty("sizes").EnumerateArray().Select(static s => s.GetInt32()));
        Assert.Equal(["7"], reply.GetProperty("tags").EnumerateArray().Select(static t => t.GetString()));
        Assert.Equal("5", reply.GetProperty("notes").GetString());

        // 重复的选项成数组且元素按 items；布尔可显式写 false；未声明的属性按 additionalProperties
        (code, output, error) = await RunAsync("orders", "typed", "--name", "Ada", "--sizes", "1", "--sizes", "2", "--verbose=false", "--extra", "5");
        Assert.Equal((CommandLineExitCodes.Success, string.Empty), (code, error));
        reply = Json(output);
        Assert.Equal([1, 2], reply.GetProperty("sizes").EnumerateArray().Select(static s => s.GetInt32()));
        Assert.False(reply.GetProperty("verbose").GetBoolean());

        // --json 的载荷原样进入，字符串的数字仍被 Schema 拒绝
        var (json, _, jsonError) = await RunAsync("invoke", "orders.typed", "--json", """{"name":"Ada","times":"2"}""", "--locale", "en-US");
        Assert.Equal(CommandLineExitCodes.Validation, json);
        Assert.Contains("$.times: expected type 'integer' but found string", Json(jsonError).GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("$.times: expected type 'integer' but found string", "--times", "abc")]
    [InlineData("$.times: expected type 'integer' but found string", "--times", "1.5")]
    [InlineData("$.times: expected type 'integer' but found array", "--times", "2", "--times", "3")]
    [InlineData("$.times: value must be <= 10", "--times", "99")]
    [InlineData("$.verbose: expected type 'boolean' but found string", "--verbose", "maybe")]
    [InlineData("$.ratio: expected type 'number' or 'null' but found string", "--ratio", "NaN")]
    [InlineData("$.sizes[0]: expected type 'integer' but found string", "--sizes", "x")]
    [InlineData("$.name: expected type 'string' but found array", "--name", "again")]
    [InlineData("$.tone: value matches none of the allowed schemas", "--tone", "hot")]
    [InlineData("$.extra: expected type 'integer' but found string", "--extra", "abc")]
    public async Task Invoke_LeavesUnconvertibleFieldOptionsToTheValidateStage(string violation, params string[] options)
    {
        // 转换不了不是用法错误：值留字符串进管道，由 Validate 阶段报字段级违规，退出码 2；已转换的值按 Schema 的范围报
        var (code, _, error) = await RunAsync(["orders", "typed", "--name", "Ada", "--locale", "en-US", .. options]);
        Assert.Equal(CommandLineExitCodes.Validation, code);
        var problem = Json(error);
        Assert.Equal("KERNEL_VALIDATION_003", problem.GetProperty("code").GetString());
        Assert.Contains(violation, problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invoke_MapsFailuresToExitCodes_AndLocalizesDetail()
    {
        // 操作失败：CONFLICT → 5，stderr 为 JSON
        var (conflict, _, conflictError) = await RunAsync("orders", "get", "--name", "fail");
        Assert.Equal(CommandLineExitCodes.Conflict, conflict);
        var problem = Json(conflictError);
        Assert.Equal("ORDERS_CONFLICT_001", problem.GetProperty("code").GetString());
        Assert.Equal(32, problem.GetProperty("traceId").GetString()!.Length);

        // 授权按契约：匿名 → 4（未认证），bob → 4（缺权限）且文案按 --locale，alice → 0；环境变量也能给凭据
        Assert.Equal(CommandLineExitCodes.Denied, (await RunAsync("orders", "create", "--name", "x")).ExitCode);
        var (denied, _, deniedError) = await RunAsync("orders", "create", "--name", "x", "--authorization", "ApiKey dev-bob-key", "--locale", "ja-JP");
        Assert.Equal(CommandLineExitCodes.Denied, denied);
        Assert.Equal(("SECURITY_DENIED_004", "権限 'orders.write' がありません。"), (Json(deniedError).GetProperty("code").GetString(), Json(deniedError).GetProperty("detail").GetString()));
        Assert.Equal(CommandLineExitCodes.Success, (await RunAsync("orders", "create", "--name", "x", "--authorization", "ApiKey dev-alice-key")).ExitCode);
        Environment.SetEnvironmentVariable(CommandLineInvocation.AuthorizationVariable, "ApiKey dev-alice-key");
        try
        {
            Assert.Equal(CommandLineExitCodes.Success, (await RunAsync("orders", "create", "--name", "x")).ExitCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CommandLineInvocation.AuthorizationVariable, null);
        }

        // 未暴露给命令行、内部与不存在的操作 → 3；用法错误 → 64
        foreach (var id in new[] { "orders.secret", "orders.audit", "orders.nope" })
        {
            var (notFound, _, notFoundError) = await RunAsync("invoke", id);
            Assert.Equal(CommandLineExitCodes.NotFound, notFound);
            Assert.Equal(CommandLineErrors.OperationNotExposed, Json(notFoundError).GetProperty("code").GetString());
        }

        foreach (var args in new[]
        {
            new[] { "invoke" },
            ["invoke", "orders.get", "--json", "[1]"],
            ["invoke", "orders.get", "--json", "not json"],
            ["invoke", "orders.get", "--data", Path.Combine(Path.GetTempPath(), "missing-" + Path.GetRandomFileName())],
            ["invoke", "orders.get", "--timeout", "abc"],
            ["invoke", "orders.get", "--timeout", "0"],
            ["invoke", "orders.get", "--authorization", "no-scheme"],
        })
        {
            var (usage, _, usageError) = await RunAsync(args);
            Assert.Equal(CommandLineExitCodes.Usage, usage);
            Assert.Equal(CommandLineErrors.UsageInvalid, Json(usageError).GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task Invoke_WithJsonThatCannotBeEncodedAsUtf8_IsAUsageError()
    {
        // --json 的文本严格编码成 UTF-8（RFC-0001「JSON 文本」，ADR-0312）：孤立的代理项编码不成，与不是 JSON 对象同样是用法错误；
        // 修之前换成 U+FFFD 照常调用，操作收到的名字里是 U+FFFD
        var (code, output, error) = await RunAsync("invoke", "orders.get", "--json", "{\"name\":\"wid\uD800get\"}");

        Assert.Equal(CommandLineExitCodes.Usage, code);
        Assert.Empty(output);
        var failure = Json(error);
        Assert.Equal(CommandLineErrors.UsageInvalid, failure.GetProperty("code").GetString());
        Assert.Equal("The value of --json must be a JSON object: the text has a lone surrogate and cannot be encoded as UTF-8", failure.GetProperty("detail").GetString());
        Assert.Empty(_host.Services.GetRequiredService<OriginLog>().Origins);

        // 代理对是合法的 UTF-16，照常编码、照常调用
        var (paired, pairedOutput, _) = await RunAsync("invoke", "orders.get", "--json", "{\"name\":\"wid😀get\"}");
        Assert.Equal(CommandLineExitCodes.Success, paired);
        Assert.Equal("wid😀get", Json(pairedOutput).GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("--name", "wid<high>get", "the value of option '--name' has a lone surrogate and cannot be encoded as UTF-8")]
    [InlineData("--name", "<low>", "the value of option '--name' has a lone surrogate and cannot be encoded as UTF-8")]
    [InlineData("--na<high>me", "widget", "an option name has a lone surrogate and cannot be encoded as UTF-8")]
    public async Task Invoke_WithAFieldOptionHoldingALoneSurrogate_IsTheSameUsageErrorAsJson(string option, string value, string reason)
    {
        // 字段选项写进载荷之前核对（RFC-0001「写出与转交」，ADR-0315）：与 --json 编码不成同一个用法错误，不发起调用；文案另用字段选项的消息键（ADR-0318），不读成「--json 的值必须是 JSON 对象」；
        // 修之前写出器把孤立的代理项写成转义的 U+FFFD 照常调用，操作收到的名字里是 U+FFFD。用例数据里只写占位：孤立的代理项不进测试用例的名字
        var (code, output, error) = await RunAsync("invoke", "orders.get", WithLoneSurrogates(option), WithLoneSurrogates(value), "--page", "2");

        Assert.Equal(CommandLineExitCodes.Usage, code);
        Assert.Empty(output);
        var failure = Json(error);
        Assert.Equal(CommandLineErrors.UsageInvalid, failure.GetProperty("code").GetString());
        Assert.Equal("The field options cannot be written as a JSON payload: " + reason, failure.GetProperty("detail").GetString());
        Assert.Empty(_host.Services.GetRequiredService<OriginLog>().Origins);
    }

    [Fact]
    public async Task Invoke_WithARepeatedFieldOption_ChecksEveryValue_AndPairsAreWrittenAsGiven()
    {
        // 重复的选项写成数组，每个元素都核对
        var (code, _, error) = await RunAsync("invoke", "orders.get", "--name", "widget", "--name", "gad\uDC00get");
        Assert.Equal(CommandLineExitCodes.Usage, code);
        Assert.Equal("The field options cannot be written as a JSON payload: the value of option '--name' has a lone surrogate and cannot be encoded as UTF-8", Json(error).GetProperty("detail").GetString());

        // 代理对是合法的 UTF-16，照常写出、照常调用
        var (paired, pairedOutput, _) = await RunAsync("invoke", "orders.get", "--name", "wid😀get");
        Assert.Equal(CommandLineExitCodes.Success, paired);
        Assert.Equal("wid😀get", Json(pairedOutput).GetProperty("name").GetString());
    }

    /// <summary>
    /// 把占位 <c>&lt;high&gt;</c>、<c>&lt;low&gt;</c> 换成孤立的高、低代理项
    /// </summary>
    private static string WithLoneSurrogates(string text)
        => text.Replace("<high>", "\uD800", StringComparison.Ordinal).Replace("<low>", "\uDC00", StringComparison.Ordinal);

    [Fact]
    public async Task Invoke_OfADeprecatedOperation_WritesOneNoticeToStderr_AndLeavesStdoutAndTheExitCodeAlone()
    {
        const string Notice = "Operation 'orders.archive' is deprecated and will be removed in a future major version.";

        // 弃用的操作照常调用：stdout 仍是一行响应 JSON，退出码 0；stderr 多一行提示（ADR-0280）
        var (code, output, error) = await RunAsync("invoke", "orders.archive", "--name", "widget", "--page", "2");
        Assert.Equal(CommandLineExitCodes.Success, code);
        Assert.Equal(Notice + Environment.NewLine, error);
        var line = Assert.Single(output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(("orders.archive", "widget", 2), (Json(line).GetProperty("operation").GetString(), Json(line).GetProperty("name").GetString(), Json(line).GetProperty("page").GetInt32()));

        // 同样的参数调不弃用的操作：stdout 只差操作 ID，stderr 是空的
        var (plainCode, plainOutput, plainError) = await RunAsync("invoke", "orders.get", "--name", "widget", "--page", "2");
        Assert.Equal(CommandLineExitCodes.Success, plainCode);
        Assert.Equal(output.Replace("orders.archive", "orders.get", StringComparison.Ordinal), plainOutput);
        Assert.Empty(plainError);

        // 简写一样提示，每次执行一行；文案按 --locale
        var (_, _, shorthandError) = await RunAsync("orders", "archive", "--name", "widget");
        Assert.Equal(Notice + Environment.NewLine, shorthandError);
        var (localizedCode, _, localizedError) = await RunAsync("orders", "archive", "--name", "widget", "--locale", "zh-CN");
        Assert.Equal(CommandLineExitCodes.Success, localizedCode);
        Assert.Equal("操作 'orders.archive' 已弃用，将在以后的大版本中移除。" + Environment.NewLine, localizedError);
    }

    [Fact]
    public async Task Invoke_OfADeprecatedOperation_KeepsTheFailureLineAndExitCode_AndSkipsTheNoticeOnUsageErrors()
    {
        // 调用失败：提示在前，失败的那一行 JSON 在后，退出码仍按 KIND；stdout 没有输出
        var (conflict, conflictOutput, conflictError) = await RunAsync("orders", "archive", "--name", "fail");
        Assert.Equal(CommandLineExitCodes.Conflict, conflict);
        Assert.Empty(conflictOutput);
        var lines = conflictError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal("Operation 'orders.archive' is deprecated and will be removed in a future major version.", lines[0]);
        Assert.Equal("ORDERS_CONFLICT_001", Json(lines[1]).GetProperty("code").GetString());

        // 用法错误没有发起调用：stderr 只有那一行 JSON，退出码 64
        foreach (var args in new[]
        {
            new[] { "invoke", "orders.archive", "--json", "not json" },
            ["orders", "archive", "--timeout", "0"],
            ["orders", "archive", "--authorization", "no-scheme"],
        })
        {
            var (usage, usageOutput, usageError) = await RunAsync(args);
            Assert.Equal(CommandLineExitCodes.Usage, usage);
            Assert.Empty(usageOutput);
            Assert.Equal(CommandLineErrors.UsageInvalid, Json(Assert.Single(usageError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))).GetProperty("code").GetString());
        }
    }

    [Theory]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("ja-jp", "ja-JP")]
    [InlineData("zh", "zh-CN")]
    [InlineData("de-AT", "de-DE")]
    public async Task FailuresBeforeTheInvocation_AreLocalizedByTheLocaleOption(string requested, string expected)
    {
        // 没有发起调用就返回的失败（用法错误与未暴露的操作）同样按 --locale 翻译：规范化后与调用一样协商出启用的区域（protocol/commandline.md「操作映射」，ADR-0325）；
        // 修之前在建调用上下文之前就返回，detail 总是默认区域，--locale 只对发起了的调用生效
        var localizer = _host.Services.GetRequiredService<IMessageLocalizer>();
        foreach (var (args, key, arguments) in new (string[] Args, string Key, IReadOnlyDictionary<string, object?>? Arguments)[]
        {
            (["nope"], CommandLineMessages.CommandUnknown, Primitives.MessageArguments.Of(("command", "nope"))),
            (["invoke"], CommandLineMessages.InvokeOperationMissing, null),
            (["invoke", "orders.secret"], CommandLineMessages.OperationNotExposed, Primitives.MessageArguments.Of(("id", "orders.secret"))),
            (["invoke", "orders.get", "--json", "[1]"], CommandLineMessages.InvokeJsonInvalid, Primitives.MessageArguments.Of(("reason", "the root must be an object"))),
            (["invoke", "orders.get", "--timeout", "abc"], CommandLineMessages.InvokeTimeoutInvalid, null),
            (["orders", "get", "--authorization", "no-scheme"], CommandLineMessages.InvokeAuthorizationInvalid, null),
        })
        {
            var (_, output, error) = await RunAsync([.. args, "--locale", requested]);
            var detail = Json(error).GetProperty("detail").GetString();

            Assert.Empty(output);
            Assert.Equal(localizer.Localize(key, expected, arguments), detail);
            Assert.NotEqual(localizer.Localize(key, "en-US", arguments), detail);
        }

        Assert.Empty(_host.Services.GetRequiredService<OriginLog>().Origins);
    }

    [Theory]
    [InlineData]
    [InlineData("--locale", "not a locale")]
    [InlineData("--locale", "")]
    public async Task FailuresBeforeTheInvocation_UseTheDefaultLocale_WithoutAValidLocaleOption(params string[] locale)
    {
        // --locale 没给或不合法：与调用一样不进 Baggage，按默认区域
        var (code, _, error) = await RunAsync(["invoke", "orders.get", "--timeout", "abc", .. locale]);

        Assert.Equal(CommandLineExitCodes.Usage, code);
        Assert.Equal("The value of --timeout must be a positive number of seconds.", Json(error).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task HelpAndTheOperationList_MarkTheDeprecatedOperation()
    {
        const string Notice = "Operation 'orders.archive' is deprecated and will be removed in a future major version.";

        // 操作列表：稳定性一列就是标注
        var (_, operations, _) = await RunAsync("operations");
        Assert.Contains("orders.archive\tDeprecated\tCommandLine\t-" + Environment.NewLine, operations, StringComparison.Ordinal);
        Assert.Single(operations.Split(Environment.NewLine), static row => row.Contains("\tDeprecated\t", StringComparison.Ordinal));

        // 带 --help 的命令行指向弃用的操作：帮助照常，之后空一行写同一句提示；写在 stdout，stderr 是空的
        var (_, general, _) = await RunAsync("help");
        foreach (var args in new[] { new[] { "orders", "archive", "--help" }, ["invoke", "orders.archive", "--help"] })
        {
            var (code, help, error) = await RunAsync(args);
            Assert.Equal(CommandLineExitCodes.Success, code);
            Assert.Equal(general + Environment.NewLine + Notice + Environment.NewLine, help);
            Assert.Empty(error);
        }

        // 其余情况的帮助一字不差：不弃用的操作、没有暴露给命令行的操作、内置命令、不带命令
        Assert.DoesNotContain("deprecated", general, StringComparison.Ordinal);
        foreach (var args in new[] { new[] { "orders", "get", "--help" }, ["invoke", "orders.get", "--help"], ["invoke", "orders.nope", "--help"], ["invoke", "--help"], ["graph", "--help"] })
        {
            Assert.Equal(general, (await RunAsync(args)).Output);
        }

        var (usage, _, usageError) = await RunAsync();
        Assert.Equal(CommandLineExitCodes.Usage, usage);
        Assert.Equal(general, usageError);
    }

    [Fact]
    public void ExitCodes_FollowErrorKinds()
    {
        Assert.Equal(CommandLineExitCodes.Validation, CommandLineExitCodes.For(Primitives.Error.Validation("X_VALIDATION_001", "x.y")));
        Assert.Equal(CommandLineExitCodes.NotFound, CommandLineExitCodes.For(Primitives.Error.NotFound("X_NOTFOUND_001", "x.y")));
        Assert.Equal(CommandLineExitCodes.Denied, CommandLineExitCodes.For(Primitives.Error.Denied("X_DENIED_001", "x.y")));
        Assert.Equal(CommandLineExitCodes.Unavailable, CommandLineExitCodes.For(Primitives.Error.Unavailable("X_UNAVAILABLE_001", "x.y")));
        Assert.Equal(CommandLineExitCodes.Unexpected, CommandLineExitCodes.For(Primitives.Error.Unexpected("X_UNEXPECTED_001", "x.y")));
        Assert.Equal(CommandLineExitCodes.Unexpected, CommandLineExitCodes.For(new Primitives.Error("X_CUSTOM_001", "x.y")));
    }
}
