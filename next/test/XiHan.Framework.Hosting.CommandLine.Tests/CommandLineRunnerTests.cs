// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Hosting.CommandLine.Runtime;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Security.InMemory;

namespace XiHan.Framework.Hosting.CommandLine.Tests;

/// <summary>
/// 命令行运行器：帮助、内置命令、invoke 与简写、载荷来源、上下文选项、失败映射与退出码。
/// </summary>
public sealed class CommandLineRunnerTests : IAsyncLifetime
{
    /// <summary>
    /// 两个主体：alice 是 editor（可读写订单），bob 是 viewer（只读）。
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
    /// 已启动的宿主。
    /// </summary>
    private IHost _host = null!;

    /// <summary>
    /// 运行器。
    /// </summary>
    private CommandLineRunner _runner = null!;

    /// <summary>
    /// 启动宿主：Minimal 组合（本地化）+ 内存安全 + 回显特性 + 命令行宿主。
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(_security);
        builder.AddXiHan(static xihan => xihan.UseMinimalProfile().UseFeature<InMemorySecurityFeature>().UseFeature<EchoFeature>());
        builder.Services.AddXiHanCommandLine();
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
    /// 运行一条命令行，返回退出码、stdout 与 stderr。
    /// </summary>
    private async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await _runner.RunAsync(args, output, error, CancellationToken.None);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>
    /// 解析 stdout 里的 JSON。
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
