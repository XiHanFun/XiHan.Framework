// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel;

/// <summary>
/// API 稳定性级别。
/// </summary>
public enum Stability
{
    /// <summary>
    /// 长期兼容，破坏性变更只能进 MAJOR 版本。
    /// </summary>
    Stable = 0,

    /// <summary>
    /// 允许调整，但必须文档标注。
    /// </summary>
    Preview = 1,

    /// <summary>
    /// 默认不承诺兼容，可能随时移除。
    /// </summary>
    Experimental = 2,

    /// <summary>
    /// 用户不得依赖，分析器会阻止引用。
    /// </summary>
    Internal = 3,

    /// <summary>
    /// 已弃用，将在下一个 MAJOR 版本中移除。
    /// </summary>
    Deprecated = 4
}

/// <summary>
/// 标记类型的 API 稳定性级别。
/// </summary>
[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
public sealed class ApiLevelAttribute : Attribute
{
    /// <summary>
    /// 稳定性级别。
    /// </summary>
    public Stability Stability { get; }

    /// <summary>
    /// 首次引入的版本号。
    /// </summary>
    public string Since { get; }

    /// <summary>
    /// 计划转为 Stable 的版本（仅 Preview 适用）。
    /// </summary>
    public string? ScheduledStable { get; set; }

    /// <summary>
    /// 标记稳定性级别。
    /// </summary>
    public ApiLevelAttribute(Stability stability, string since)
    {
        Stability = stability;
        Since = since;
    }
}
