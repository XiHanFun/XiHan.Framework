// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Cryptography;
using System.Text;
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
