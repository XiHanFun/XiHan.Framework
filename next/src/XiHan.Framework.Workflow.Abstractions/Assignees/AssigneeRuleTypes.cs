// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Assignees;

/// <summary>
/// 内置指派规则类型。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class AssigneeRuleTypes
{
    /// <summary>
    /// 指定用户：参数即指派人。
    /// </summary>
    public const string User = "User";

    /// <summary>
    /// 发起人；无参数。
    /// </summary>
    public const string Starter = "Starter";

    /// <summary>
    /// 表达式：一个表达式，求值为列表或逗号分隔字符串。
    /// </summary>
    public const string Expression = "Expression";
}
