// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

namespace XiHan.Framework.Security.InMemory;

/// <summary>
/// 内存主体存储：按 API Key 查主体。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryPrincipalStore
{
    private readonly Dictionary<string, PrincipalRecord> _byId = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>
    /// 创建并装载选项里的主体；超出容量抛出 <see cref="FrameworkException"/>。
    /// </summary>
    public InMemoryPrincipalStore(InMemorySecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Capacity = options.MaxPrincipals;
        foreach (var principal in options.Principals)
        {
            Set(principal).ThrowIfFailure();
        }
    }

    /// <summary>
    /// 容量上限。
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 当前主体数。
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _byId.Count;
            }
        }
    }

    /// <summary>
    /// 新增或替换；新增超出容量返回 <see cref="SecurityErrors.CapacityExceeded"/>。
    /// </summary>
    public Result Set(PrincipalRecord principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        lock (_gate)
        {
            if (!_byId.ContainsKey(principal.Id) && _byId.Count >= Capacity)
            {
                return Error.Unavailable(SecurityErrors.CapacityExceeded, $"内存主体存储已达容量上限 {Capacity}。");
            }

            _byId[principal.Id] = principal;
            return Result.Success();
        }
    }

    /// <summary>
    /// 删除。
    /// </summary>
    public bool Remove(string principalId)
    {
        ArgumentNullException.ThrowIfNull(principalId);
        lock (_gate)
        {
            return _byId.Remove(principalId);
        }
    }

    /// <summary>
    /// 按 API Key 查找；比较为常量时间。
    /// </summary>
    public Maybe<PrincipalRecord> FindByApiKey(string apiKey)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        var candidate = Encoding.UTF8.GetBytes(apiKey);
        lock (_gate)
        {
            foreach (var principal in _byId.Values)
            {
                var expected = Encoding.UTF8.GetBytes(principal.ApiKey);
                if (expected.Length == candidate.Length && CryptographicOperations.FixedTimeEquals(expected, candidate))
                {
                    return Maybe.Some(principal);
                }
            }

            return default;
        }
    }
}

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

/// <summary>
/// 有界的内存权限授予存储。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryPermissionGrantStore : IPermissionGrantStore
{
    private readonly Dictionary<(string Kind, string Id, string? Tenant), HashSet<string>> _grants = [];
    private readonly Lock _gate = new();
    private int _count;

    /// <summary>
    /// 创建并装载选项里的授予；超出容量抛出 <see cref="FrameworkException"/>。
    /// </summary>
    public InMemoryPermissionGrantStore(InMemorySecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Capacity = options.MaxGrants;
        foreach (var grant in options.Grants)
        {
            Grant(grant).ThrowIfFailure();
        }
    }

    /// <summary>
    /// 容量上限。
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 当前授予数。
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyCollection<string>> GetGrantsAsync(GrantSubject subject, string? tenantId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (_grants.TryGetValue((subject.Kind, subject.Id, null), out var global))
            {
                result.UnionWith(global);
            }

            if (tenantId is not null && _grants.TryGetValue((subject.Kind, subject.Id, tenantId), out var scoped))
            {
                result.UnionWith(scoped);
            }

            return ValueTask.FromResult<IReadOnlyCollection<string>>(result);
        }
    }

    /// <summary>
    /// 授予；已存在返回成功且不计数，超出容量返回 <see cref="SecurityErrors.CapacityExceeded"/>。
    /// </summary>
    public Result Grant(PermissionGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        lock (_gate)
        {
            var key = (grant.Subject.Kind, grant.Subject.Id, grant.TenantId);
            if (!_grants.TryGetValue(key, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                _grants[key] = set;
            }

            if (set.Contains(grant.Permission))
            {
                return Result.Success();
            }

            if (_count >= Capacity)
            {
                return Error.Unavailable(SecurityErrors.CapacityExceeded, $"内存授予存储已达容量上限 {Capacity}。");
            }

            set.Add(grant.Permission);
            _count++;
            return Result.Success();
        }
    }

    /// <summary>
    /// 撤销；存在返回 <see langword="true"/>。
    /// </summary>
    public bool Revoke(PermissionGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        lock (_gate)
        {
            if (_grants.TryGetValue((grant.Subject.Kind, grant.Subject.Id, grant.TenantId), out var set) && set.Remove(grant.Permission))
            {
                _count--;
                return true;
            }

            return false;
        }
    }
}
