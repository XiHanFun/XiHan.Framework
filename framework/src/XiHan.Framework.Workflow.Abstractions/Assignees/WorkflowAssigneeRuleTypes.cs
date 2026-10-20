// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.Assignees;

/// <summary>
/// 框架内置的受理人规则类型
/// </summary>
/// <remarks>
/// 内置类型不依赖任何组织架构数据；角色、部门、主管等类型由应用注册自己的 <see cref="IWorkflowAssigneeProvider"/>。
/// </remarks>
public static class WorkflowAssigneeRuleTypes
{
    /// <summary>
    /// 指定用户（参数为受理人标识列表，至少一个）
    /// </summary>
    public const string User = "User";

    /// <summary>
    /// 流程发起人（无参数；实例未记录发起人时解析为空）
    /// </summary>
    public const string Starter = "Starter";

    /// <summary>
    /// 表达式（参数为一条表达式，求值为标识列表或逗号分隔字符串，常用于引用启动变量）
    /// </summary>
    public const string Expression = "Expression";
}
