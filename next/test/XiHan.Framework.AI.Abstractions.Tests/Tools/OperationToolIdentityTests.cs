// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions.Tests.Fixtures;
using XiHan.Framework.AI.Abstractions.Tools;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authentication;
using XiHan.Framework.Security.InMemory;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.Abstractions.Tests.Tools;

/// <summary>
/// 操作工具以调用方身份执行。
/// </summary>
/// <remarks>
/// 调用方经管道认证后绑定工具：有权限的主体放行、工具里看到的是调用方主体与租户，没有权限的主体与匿名调用被 Authorize 拒绝并以 JSON 错误交给模型。
/// </remarks>
public sealed class OperationToolIdentityTests
{
    /// <summary>
    /// 两个主体：alice 是 reader（有 <c>guarded.read</c>），bob 是 guest（没有）。
    /// </summary>
    private static readonly (string Key, string Value)[] _configuration =
    [
        ("XiHan:Security:InMemory:Principals:alice:ApiKey", "dev-alice-key"),
        ("XiHan:Security:InMemory:Principals:alice:Roles", "reader"),
        ("XiHan:Security:InMemory:Principals:bob:ApiKey", "dev-bob-key"),
        ("XiHan:Security:InMemory:Principals:bob:Roles", "guest"),
        ("XiHan:Security:InMemory:Grants:role:reader", GuardedFeature.Permission),
    ];

    [Fact]
    public async Task PermittedCaller_RunsTheToolAsItself()
    {
        await using var host = await StartAsync();

        var (reply, traceId) = await CallAsync(host, "dev-alice-key", tenant: "t1");
        Assert.Equal("alice", reply.GetProperty("echo").GetString());
        // 租户与追踪 ID 同样沿用调用方
        Assert.Equal("t1", reply.GetProperty("tenant").GetString());
        Assert.Equal(traceId, reply.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task CallerWithoutPermission_OrAnonymous_IsDenied()
    {
        await using var host = await StartAsync();

        var (denied, _) = await CallAsync(host, "dev-bob-key");
        Assert.Equal(SecurityErrors.PermissionMissing, denied.GetProperty("error").GetString());
        Assert.False(string.IsNullOrEmpty(denied.GetProperty("message").GetString()));

        var (anonymous, _) = await CallAsync(host, apiKey: null);
        Assert.Equal(SecurityErrors.NotAuthenticated, anonymous.GetProperty("error").GetString());
    }

    /// <summary>
    /// 以给定凭据发起一次经管道认证的调用，在其中绑定并调用 <c>guarded_whoami</c> 工具。
    /// </summary>
    /// <remarks>
    /// 给了租户时在调用里写入安全上下文，模拟 Tenant 阶段解析出的租户。返回工具结果与调用方的追踪 ID。
    /// </remarks>
    private static async Task<(JsonElement Reply, string TraceId)> CallAsync(TestHost host, string? apiKey, string? tenant = null)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var caller = App.CreateContext(scope.ServiceProvider, CancellationToken.None);
        if (apiKey is not null)
        {
            caller.SetCredential(new AuthenticationCredential("ApiKey", apiKey));
        }

        JsonElement? reply = null;
        var outcome = await host.Services.GetRequiredService<App>().InvokeAsync(caller, async context =>
        {
            if (tenant is not null)
            {
                context.SetTenant(new TenantContext(tenant));
            }

            var tool = Assert.IsType<OperationAIFunction>(host.Services.GetRequiredService<IAIToolCatalog>().Bind(context).Single(static tool => tool.Name == "guarded_whoami"));
            reply = Assert.IsType<JsonElement>(await tool.InvokeAsync(new AIFunctionArguments { ["text"] = "who" }, context.CancellationToken));
        });

        Assert.True(outcome.IsSuccess);
        return (reply!.Value, caller.TraceId);
    }

    /// <summary>
    /// 启动：本地化 + 内存安全 + 工具目录 + 带权限的操作。
    /// </summary>
    private static Task<TestHost> StartAsync()
        => TestHost.StartAsync(
            static b => b.UseFeature<InMemoryLocalizationFeature>().UseFeature<InMemorySecurityFeature>().UseFeature<AIToolsFeature>().UseFeature<GuardedFeature>(),
            _configuration.Select(static pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)));
}
