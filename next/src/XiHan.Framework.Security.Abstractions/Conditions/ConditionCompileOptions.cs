// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 编译选项。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ConditionCompileOptions
{
    /// <summary>
    /// 缺省选项（UTC，无额外登记项）。
    /// </summary>
    public static ConditionCompileOptions Default { get; } = new();

    /// <summary>
    /// 时区：解释不带偏移的日期时间条件值，以及把日期时间属性折算成日期、时刻。
    /// </summary>
    public TimeZoneInfo TimeZone
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = TimeZoneInfo.Utc;

    /// <summary>
    /// 只对这一次编译生效的额外登记项，优先于登记表（如某个权限点登记的资源行属性）。
    /// </summary>
    public IReadOnlyList<ConditionAttributeDescriptor> AdditionalAttributes
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = [];
}
