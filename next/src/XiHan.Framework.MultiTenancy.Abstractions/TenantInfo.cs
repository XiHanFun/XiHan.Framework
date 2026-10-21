// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 租户信息。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record TenantInfo
{
    private static readonly IReadOnlyDictionary<string, string> _noProperties = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 租户 ID。
    /// </summary>
    public required string Id
    {
        get => field;
        init => field = TenantIdentifier.Validate(value);
    }

    /// <summary>
    /// 显示名。
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 是否启用；禁用的租户解析时被拒绝。默认启用。
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 租户专属属性（连接、配额等），键区分大小写。
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = _noProperties;
}
