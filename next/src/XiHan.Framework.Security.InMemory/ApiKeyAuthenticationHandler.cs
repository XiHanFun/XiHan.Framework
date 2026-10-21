// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

namespace XiHan.Framework.Security.InMemory;

/// <summary>
/// <c>ApiKey</c> 方案的认证处理器：把配置里的主体记录变成 <see cref="Principal"/>（角色声明、租户声明与附加声明）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ApiKeyAuthenticationHandler(InMemoryPrincipalStore store, SecurityOptions options) : IAuthenticationHandler
{
    /// <summary>
    /// 方案名 <c>ApiKey</c>。
    /// </summary>
    public const string SchemeName = "ApiKey";

    private readonly InMemoryPrincipalStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly SecurityOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public string Scheme => SchemeName;

    /// <inheritdoc />
    public ValueTask<Result<Principal>> AuthenticateAsync(AuthenticationCredential credential, InvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);
        if (!_store.FindByApiKey(credential.Value).TryGetValue(out var record))
        {
            return ValueTask.FromResult(Result.Failure<Principal>(Error.Denied(SecurityErrors.InvalidCredential, "API Key 无效。")));
        }

        var claims = new List<KeyValuePair<string, string>>(record.Roles.Count + record.Claims.Count + 1);
        claims.AddRange(record.Roles.Select(role => new KeyValuePair<string, string>(_options.RoleClaim, role)));
        if (record.Tenant is not null)
        {
            claims.Add(new KeyValuePair<string, string>("tenant", record.Tenant));
        }

        claims.AddRange(record.Claims);
        return ValueTask.FromResult(Result.Success(new Principal(record.Id, SchemeName) { DisplayName = record.DisplayName, Claims = claims }));
    }
}
