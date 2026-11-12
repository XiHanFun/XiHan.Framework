// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 统一资源名：<c>xrn:&lt;partition&gt;:&lt;service&gt;:&lt;tenant&gt;:&lt;type&gt;/&lt;id&gt;</c>，用于策略判定与审计
/// </summary>
/// <remarks>
/// 构造与初始化器（含 <see langword="with"/>）按 <c>xrn</c> 文法校验每一段，不合法抛出 <see cref="ArgumentException"/>；全局资源的租户为 <see langword="null"/>，文本里是空段（ADR-0145）。<see langword="default"/> 不是合法的资源名
/// </remarks>
/// <param name="Partition">分区，例如 <c>cn</c>（<c>[a-z0-9-]+</c>）</param>
/// <param name="Service">服务（<c>[a-z0-9-]+</c>）</param>
/// <param name="Tenant">租户 ID，文法与 RFC-0007 的租户 ID 相同（只用小写，可含 <c>.</c>、<c>_</c>）；全局资源为 <see langword="null"/></param>
/// <param name="Type">资源类型（<c>[a-z0-9-]+</c>）</param>
/// <param name="Id">资源 ID，不含斜杠与空白</param>
[ApiLevel(Stability.Stable, "5.0")]
public readonly partial record struct ResourceName(string Partition, string Service, string? Tenant, string Type, string Id)
{
    /// <summary>
    /// 分区
    /// </summary>
    public string Partition { get; init => field = Segment(value, SegmentPattern(), nameof(Partition)); } = Segment(Partition, SegmentPattern(), nameof(Partition));

    /// <summary>
    /// 服务
    /// </summary>
    public string Service { get; init => field = Segment(value, SegmentPattern(), nameof(Service)); } = Segment(Service, SegmentPattern(), nameof(Service));

    /// <summary>
    /// 租户 ID
    /// </summary>
    /// <remarks>
    /// 全局资源为 <see langword="null"/>；空字符串不是合法的租户段
    /// </remarks>
    public string? Tenant { get; init => field = TenantSegment(value); } = TenantSegment(Tenant);

    /// <summary>
    /// 资源类型
    /// </summary>
    public string Type { get; init => field = Segment(value, SegmentPattern(), nameof(Type)); } = Segment(Type, SegmentPattern(), nameof(Type));

    /// <summary>
    /// 资源 ID
    /// </summary>
    public string Id { get; init => field = Segment(value, IdPattern(), nameof(Id)); } = Segment(Id, IdPattern(), nameof(Id));

    /// <summary>
    /// 解析资源名
    /// </summary>
    public static ResourceName Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out var name) ? name : throw new ArgumentException($"'{text}' is not a valid resource name; expected 'xrn:<partition>:<service>:<tenant>:<type>/<id>'.", nameof(text));
    }

    /// <summary>
    /// 尝试解析资源名
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? text, out ResourceName name)
    {
        name = default;
        if (text is null)
        {
            return false;
        }

        var match = Pattern().Match(text);
        if (!match.Success)
        {
            return false;
        }

        // 文本里的空租户段就是全局资源
        var tenant = match.Groups["t"].Value;
        name = new ResourceName(match.Groups["p"].Value, match.Groups["s"].Value, tenant.Length == 0 ? null : tenant, match.Groups["type"].Value, match.Groups["id"].Value);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => $"xrn:{Partition}:{Service}:{Tenant}:{Type}/{Id}";

    /// <summary>
    /// 校验一段并原样返回
    /// </summary>
    private static string Segment(string value, Regex pattern, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        return pattern.IsMatch(value) ? value : throw new ArgumentException($"'{value}' is not a valid resource name {name.ToLowerInvariant()} segment.", name);
    }

    /// <summary>
    /// 校验租户段：<see langword="null"/> 是全局资源
    /// </summary>
    private static string? TenantSegment(string? value) => value is null ? null : Segment(value, TenantPattern(), nameof(Tenant));

    /// <summary>
    /// 分区、服务、类型段的正则
    /// </summary>
    /// <remarks>
    /// 本类型的正则都以 <c>\z</c> 结尾整串匹配，<c>$</c> 会放过结尾的换行（ADR-0196）
    /// </remarks>
    [GeneratedRegex("^[a-z0-9-]+\\z")]
    private static partial Regex SegmentPattern();

    /// <summary>
    /// 租户段的正则，与 RFC-0007 的租户 ID 同一文法
    /// </summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,63}\\z")]
    private static partial Regex TenantPattern();

    /// <summary>
    /// ID 段的正则：不含斜杠与空白
    /// </summary>
    [GeneratedRegex("^[^/\\s]+\\z")]
    private static partial Regex IdPattern();

    /// <summary>
    /// 资源名格式的正则
    /// </summary>
    /// <remarks>
    /// 分区、服务、类型段为 <c>[a-z0-9-]+</c>；租户段为空（全局资源）或租户 ID，文法与 RFC-0007 的租户 ID 相同（小写字母或数字开头，之后是小写字母、数字、<c>.</c>、<c>_</c>、<c>-</c>，不超过 64 个字符），任何合法的租户 ID 拼进来都能原样解析回去；ID 段不含斜杠与空白
    /// </remarks>
    [GeneratedRegex("^xrn:(?<p>[a-z0-9-]+):(?<s>[a-z0-9-]+):(?<t>(?:[a-z0-9][a-z0-9._-]{0,63})?):(?<type>[a-z0-9-]+)/(?<id>[^/\\s]+)\\z")]
    private static partial Regex Pattern();
}
