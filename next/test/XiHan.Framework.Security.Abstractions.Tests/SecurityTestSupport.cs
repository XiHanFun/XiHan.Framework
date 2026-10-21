// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.Abstractions.Tests;

internal sealed class DictionaryGrantStore(params PermissionGrant[] grants) : IPermissionGrantStore
{
    public ValueTask<IReadOnlyCollection<string>> GetGrantsAsync(GrantSubject subject, string? tenantId, CancellationToken cancellationToken)
        => ValueTask.FromResult<IReadOnlyCollection<string>>(grants
            .Where(g => g.Subject == subject && (g.TenantId is null || g.TenantId == tenantId))
            .Select(static g => g.Permission)
            .ToList());
}

internal sealed class StaticHandler(string scheme, Func<AuthenticationCredential, Result<Principal>> authenticate) : IAuthenticationHandler
{
    public string Scheme => scheme;

    public ValueTask<Result<Principal>> AuthenticateAsync(AuthenticationCredential credential, InvocationContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(authenticate(credential));
}

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
