// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义模型拒绝的值的规则：模型与读入共用的一份
/// </summary>
/// <remarks>
/// 模型的 <c>init</c> 照这些规则以英文异常拒绝（编程错误）；定义 JSON 的读入在构造模型之前照同一组规则核对，产出带路径、消息键与参数的诊断（ADR-0106 修订）。名字与活动类型的文法在 <see cref="WorkflowName.IsValid"/> 与 <see cref="Activities.ActivityType.IsValid"/>，节点与转移 ID 的文法在 <see cref="WorkflowIdentifiers.IsValidElementId"/>，这里不重复
/// </remarks>
internal static class WorkflowDefinitionRules
{
    /// <summary>
    /// 定义 ID 不为空
    /// </summary>
    public static bool IsValidDefinitionId([NotNullWhen(true)] string? value) => !string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// 版本号至少为 1
    /// </summary>
    public static bool IsValidVersion(int value) => value >= 1;

    /// <summary>
    /// 分类不超过 <see cref="WorkflowDefinition.MaxCategoryLength"/> 个字符
    /// </summary>
    public static bool IsValidCategory(string? value) => value is null || value.Length <= WorkflowDefinition.MaxCategoryLength;

    /// <summary>
    /// 超时为空或为正（定义与节点相同）
    /// </summary>
    public static bool IsValidTimeout(TimeSpan? value) => value is null || value > TimeSpan.Zero;

    /// <summary>
    /// 变量名不为空
    /// </summary>
    public static bool IsValidVariableName([NotNullWhen(true)] string? value) => !string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// 最多尝试次数至少为 1
    /// </summary>
    public static bool IsValidMaxAttempts(int value) => value >= 1;

    /// <summary>
    /// 首次重试延迟不为负
    /// </summary>
    public static bool IsValidFirstDelay(TimeSpan value) => value >= TimeSpan.Zero;

    /// <summary>
    /// 退避因子至少为 1
    /// </summary>
    public static bool IsValidBackoffFactor(double value) => value >= 1;
}
