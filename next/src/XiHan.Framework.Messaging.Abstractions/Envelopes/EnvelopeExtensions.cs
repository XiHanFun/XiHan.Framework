// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Envelopes;

/// <summary>
/// 框架约定的 CloudEvents 扩展属性名。
/// </summary>
/// <remarks>
/// 扩展属性名只含小写字母与数字、不超过 20 个字符。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static partial class EnvelopeExtensions
{
    /// <summary>
    /// 租户 ID，派发时进入 Baggage <c>xihan.tenant</c>。
    /// </summary>
    public const string Tenant = "xihantenant";

    /// <summary>
    /// 关联 ID，派发时进入 Baggage <c>xihan.correlation</c>。
    /// </summary>
    public const string Correlation = "xihancorrelation";

    /// <summary>
    /// W3C <c>traceparent</c>（CloudEvents 分布式追踪扩展）。
    /// </summary>
    public const string TraceParent = "traceparent";

    /// <summary>
    /// 分区键：流能力按它选分区，同一主题内同键的消息按发布顺序投递。
    /// </summary>
    public const string PartitionKey = "xihankey";

    /// <summary>
    /// 扩展属性名是否合法。
    /// </summary>
    public static bool IsValidName(string? name) => name is not null && NamePattern().IsMatch(name);

    /// <summary>
    /// 扩展属性名文法：小写字母与数字，1 到 20 个字符。
    /// </summary>
    [GeneratedRegex("^[a-z0-9]{1,20}$")]
    private static partial Regex NamePattern();
}
