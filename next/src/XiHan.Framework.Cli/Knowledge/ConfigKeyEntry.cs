// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 一个配置键。
/// </summary>
internal sealed record ConfigKeyEntry
{
    /// <summary>
    /// 相对节路径的键。
    /// </summary>
    /// <remarks>
    /// 冒号分层；按名字或下标展开的段写成占位符，如 <c>Principals:{name}:ApiKey</c>。以 <c>XiHan:</c> 开头的是绝对路径：Options 类读了自己节以外的键。
    /// </remarks>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 类型：string、integer、number、boolean 或 enum。
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 不写这个键时的缺省值，按配置里的写法。
    /// </summary>
    /// <remarks>
    /// 缺省不是字面量（如取应用名）时为空，说明在 <see cref="Summary"/>。
    /// </remarks>
    public string? Default { get; set; }

    /// <summary>
    /// 不写这个键（且没有绑定）时读取失败。
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// 闭区间下限。
    /// </summary>
    public double? Minimum { get; set; }

    /// <summary>
    /// 开区间下限。
    /// </summary>
    public double? ExclusiveMinimum { get; set; }

    /// <summary>
    /// 闭区间上限。
    /// </summary>
    public double? Maximum { get; set; }

    /// <summary>
    /// 枚举允许的取值。
    /// </summary>
    public IReadOnlyList<string>? Values { get; set; }

    /// <summary>
    /// 能通过校验的示例值。
    /// </summary>
    public string? Sample { get; set; }

    /// <summary>
    /// 一句说明。
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}
