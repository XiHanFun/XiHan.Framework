// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 引擎保留的变量名、回调输入键与结果标签
/// </summary>
/// <remarks>
/// 定义、表达式与应用读取实例变量时用这些名字；引擎的实现类型不公开（ADR-0135）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowReservedNames
{
    /// <summary>
    /// 实例变量里记录最近一次"继续"错误的键
    /// </summary>
    /// <remarks>
    /// 值是 <see cref="WorkflowFault"/> 的 JSON（ADR-0106）：表达式读 <c>lastError.code</c>（错误码）与 <c>lastError.message</c>（按进程默认区域渲染的文本），展示用 <c>WorkflowJson.DeserializeFault(variables[LastErrorVariable])?.Localize()</c> 按调用方的区域渲染 <c>messageKey</c> 与 <c>arguments</c>
    /// </remarks>
    public const string LastErrorVariable = "lastError";

    /// <summary>
    /// 继续出错时的出边结果标签
    /// </summary>
    public const string ErrorOutcome = "error";

    /// <summary>
    /// 子流程回调输入：子实例 ID
    /// </summary>
    public const string ChildInstanceIdInput = "childInstanceId";

    /// <summary>
    /// 子流程回调输入：子实例状态
    /// </summary>
    public const string ChildStatusInput = "childStatus";

    /// <summary>
    /// 子流程回调输入：子实例变量
    /// </summary>
    public const string ChildVariablesInput = "childVariables";

    /// <summary>
    /// 子流程回调输入：子实例故障
    /// </summary>
    /// <remarks>
    /// 是 <see cref="WorkflowFault"/> 的 JSON（<c>code</c>、<c>message</c>、<c>messageKey</c>、<c>arguments</c>、<c>inner</c>），子实例没有故障时为空（ADR-0105）
    /// </remarks>
    public const string ChildFaultInput = "childFault";

    /// <summary>
    /// 信号启动时写入的变量：信号载荷
    /// </summary>
    public const string SignalVariable = "signal";

    /// <summary>
    /// 输出映射表达式作用域里本节点输出的名字
    /// </summary>
    public const string OutputsScopeName = "outputs";
}
