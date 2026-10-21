// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.Abstractions.Tests;

internal static class SecurityHosts
{
    public static readonly PermissionDefinition OrdersRead = new("orders.read", "读订单");

    public static IServiceProvider Services(Action<IServiceCollection>? configure = null, SecurityOptions? options = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(options ?? new SecurityOptions());
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    public static PermissionRegistry Registry(params PermissionDefinition[] definitions)
    {
        var registry = new PermissionRegistry();
        foreach (var definition in definitions)
        {
            registry.Add(definition).ThrowIfFailure();
        }

        return registry;
    }

    public static InvocationContext Context(IServiceProvider services) => TestInvocationContext.Create(services);

    public static Principal Alice(params string[] roles)
        => new("alice", "test") { Claims = roles.Select(static r => new KeyValuePair<string, string>("role", r)).ToList() };
}
