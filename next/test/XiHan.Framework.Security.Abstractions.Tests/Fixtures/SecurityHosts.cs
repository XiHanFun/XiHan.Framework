// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.Abstractions.Tests.Fixtures;

/// <summary>
/// 安全测试共用的服务、登记表、调用上下文与主体构造。
/// </summary>
internal static class SecurityHosts
{
    /// <summary>
    /// 测试用权限定义 <c>orders.read</c>。
    /// </summary>
    public static readonly PermissionDefinition OrdersRead = new("orders.read", "读订单");

    /// <summary>
    /// 构造服务提供者：注册安全选项（默认实例）后交给 <paramref name="configure"/> 追加。
    /// </summary>
    public static IServiceProvider Services(Action<IServiceCollection>? configure = null, SecurityOptions? options = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(options ?? new SecurityOptions());
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 构造登记了给定权限定义的登记表。
    /// </summary>
    public static PermissionRegistry Registry(params PermissionDefinition[] definitions)
    {
        var registry = new PermissionRegistry();
        foreach (var definition in definitions)
        {
            registry.Add(definition).ThrowIfFailure();
        }

        return registry;
    }

    /// <summary>
    /// 用给定服务创建测试调用上下文。
    /// </summary>
    public static InvocationContext Context(IServiceProvider services) => TestInvocationContext.Create(services);

    /// <summary>
    /// 已认证主体 <c>alice</c>，携带给定角色的 <c>role</c> 声明。
    /// </summary>
    public static Principal Alice(params string[] roles)
        => new("alice", "test") { Claims = roles.Select(static r => new KeyValuePair<string, string>("role", r)).ToList() };
}
