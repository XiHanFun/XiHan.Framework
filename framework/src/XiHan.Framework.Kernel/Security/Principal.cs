// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 当前操作的主体
/// </summary>
/// <remarks>
/// 用户、服务或系统。不可变；由 Authenticate 阶段写入上下文。已认证的主体经构造给出 ID 与认证方案，匿名主体只有 <see cref="Anonymous"/> 一个，它的 ID 与方案都是 <see langword="null"/>（ADR-0145）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record Principal
{
    /// <summary>
    /// 创建已认证的主体
    /// </summary>
    /// <param name="id">主体 ID</param>
    /// <param name="authenticationScheme">认证方案</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> 或 <paramref name="authenticationScheme"/> 为空或空白</exception>
    public Principal(string id, string authenticationScheme)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationScheme);
        Id = id;
        AuthenticationScheme = authenticationScheme;
    }

    /// <summary>
    /// 创建匿名主体
    /// </summary>
    private Principal()
    {
    }

    /// <summary>
    /// 匿名主体
    /// </summary>
    public static Principal Anonymous { get; } = new();

    /// <summary>
    /// 主体 ID
    /// </summary>
    /// <remarks>
    /// 匿名主体为 <see langword="null"/>；<see cref="IsAuthenticated"/> 为真时不为 <see langword="null"/>
    /// </remarks>
    public string? Id { get; }

    /// <summary>
    /// 认证方案
    /// </summary>
    /// <remarks>
    /// 匿名主体为 <see langword="null"/>
    /// </remarks>
    public string? AuthenticationScheme { get; }

    /// <summary>
    /// 显示名
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 声明集合
    /// </summary>
    /// <remarks>
    /// 同一类型可有多个值
    /// </remarks>
    public IReadOnlyList<KeyValuePair<string, string>> Claims { get; init; } = [];

    /// <summary>
    /// 是否已认证
    /// </summary>
    /// <remarks>
    /// 为真时 <see cref="Id"/> 与 <see cref="AuthenticationScheme"/> 都不为 <see langword="null"/>
    /// </remarks>
    [MemberNotNullWhen(true, nameof(Id), nameof(AuthenticationScheme))]
    public bool IsAuthenticated => Id is not null && AuthenticationScheme is not null;

    /// <summary>
    /// 取指定类型的全部声明值
    /// </summary>
    public IEnumerable<string> ClaimValues(string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return Claims.Where(c => string.Equals(c.Key, type, StringComparison.Ordinal)).Select(static c => c.Value);
    }

    /// <summary>
    /// 是否拥有指定类型与值的声明
    /// </summary>
    public bool HasClaim(string type, string value)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(value);
        return Claims.Any(c => string.Equals(c.Key, type, StringComparison.Ordinal) && string.Equals(c.Value, value, StringComparison.Ordinal));
    }
}
