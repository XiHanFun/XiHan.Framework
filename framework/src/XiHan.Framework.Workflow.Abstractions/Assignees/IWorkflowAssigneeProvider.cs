// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.Assignees;

/// <summary>
/// 受理人解析器（一种规则类型一个解析器）
/// </summary>
/// <remarks>
/// 解析器在人工任务节点执行时、于实例执行锁内被调用，应只读取数据、不产生副作用；
/// 数据源不可用等无法得出结论的情况必须抛出异常（节点随之故障、可重试），不得返回空列表冒充"无人可审"。
/// 同一规则类型只允许注册一个解析器。
/// </remarks>
public interface IWorkflowAssigneeProvider
{
    /// <summary>
    /// 规则类型（与 <see cref="WorkflowAssigneeRule.Type"/> 大小写不敏感匹配）
    /// </summary>
    string RuleType { get; }

    /// <summary>
    /// 校验规则参数（流程发布前调用）
    /// </summary>
    /// <param name="rule">规则</param>
    /// <returns>错误信息列表（为空表示合法）</returns>
    IReadOnlyList<string> Validate(WorkflowAssigneeRule rule);

    /// <summary>
    /// 解析受理人
    /// </summary>
    /// <param name="context">解析上下文</param>
    /// <returns>受理人标识列表（按审批顺序；可为空，表示该规则当前无人命中）</returns>
    Task<IReadOnlyList<string>> ResolveAsync(WorkflowAssigneeResolveContext context);
}
