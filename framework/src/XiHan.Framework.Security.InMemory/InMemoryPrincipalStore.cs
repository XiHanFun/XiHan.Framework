// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

namespace XiHan.Framework.Security.InMemory;

/// <summary>
/// 内存主体存储：按 API Key 查主体
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class InMemoryPrincipalStore
{
    /// <summary>
    /// 严格的 UTF-8：遇到孤立的代理项抛出，不换成 U+FFFD
    /// </summary>
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// 主体记录，按主体 ID 索引（区分大小写）
    /// </summary>
    private readonly Dictionary<string, PrincipalRecord> _byId = new(StringComparer.Ordinal);

    /// <summary>
    /// 保护主体表的锁
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 创建并装载选项里的主体
    /// </summary>
    /// <remarks>
    /// 超出容量抛出 <see cref="FrameworkException"/>
    /// </remarks>
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
    /// 容量上限
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 当前主体数
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
    /// 新增或替换
    /// </summary>
    /// <remarks>
    /// 新增超出容量返回 <see cref="SecurityErrors.CapacityExceeded"/>
    /// </remarks>
    public Result Set(PrincipalRecord principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        lock (_gate)
        {
            // 替换已有主体不占新容量，只有新增才受上限约束
            if (!_byId.ContainsKey(principal.Id) && _byId.Count >= Capacity)
            {
                return Error.Unavailable(SecurityErrors.CapacityExceeded, SecurityMessages.PrincipalStoreCapacityExceeded, MessageArguments.Of(("capacity", Capacity)));
            }

            _byId[principal.Id] = principal;
            return Result.Success();
        }
    }

    /// <summary>
    /// 删除
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
    /// 按 API Key 查找
    /// </summary>
    /// <remarks>
    /// 比较为常量时间，按严格的 UTF-8 字节比较：有孤立代理项的 <paramref name="apiKey"/> 编码不成，与任何主体都不相等（<see cref="PrincipalRecord.ApiKey"/> 拒收孤立的代理项）
    /// </remarks>
    public Maybe<PrincipalRecord> FindByApiKey(string apiKey)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        byte[] candidate;
        try
        {
            // 宽松的编码把孤立的代理项都换成 U+FFFD，两个不同的 key 编码后相等；编码不成的候选不等于任何一个已装载的 key，不必再比。提前返回只透露候选自己编码不成，与已装载的 key 无关
            candidate = _strictUtf8.GetBytes(apiKey);
        }
        catch (EncoderFallbackException)
        {
            return default;
        }

        lock (_gate)
        {
            // 逐条做常量时间比较而不是字典查找，避免通过耗时差异推断 API Key；长度不同直接跳过，长度本身不是秘密
            foreach (var principal in _byId.Values)
            {
                var expected = _strictUtf8.GetBytes(principal.ApiKey);
                if (expected.Length == candidate.Length && CryptographicOperations.FixedTimeEquals(expected, candidate))
                {
                    return Maybe.Some(principal);
                }
            }

            return default;
        }
    }
}
