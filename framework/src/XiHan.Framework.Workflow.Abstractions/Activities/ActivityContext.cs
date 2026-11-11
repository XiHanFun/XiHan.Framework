// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 活动执行的上下文
/// </summary>
/// <remarks>
/// 定义、实例与节点实例的只读视图、节点、变量视图、节点私有状态、本次执行的服务作用域与调用上下文，以及属性读取与模板渲染的便捷方法
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public class ActivityContext
{
    /// <summary>
    /// 由引擎创建
    /// </summary>
    internal ActivityContext(WorkflowDefinition definition, WorkflowInstance instance, WorkflowNode node, WorkflowNodeInstance nodeInstance, WorkflowVariables variables, IServiceProvider services, InvocationContext invocation, IWorkflowExpressionEvaluator evaluator, TimeProvider clock)
    {
        Definition = definition;
        InstanceRecord = instance;
        Instance = new WorkflowInstanceView(instance);
        Node = node;
        NodeInstanceRecord = nodeInstance;
        NodeInstance = new WorkflowNodeInstanceView(nodeInstance);
        Variables = variables;
        Services = services;
        Invocation = invocation;
        Evaluator = evaluator;
        Clock = clock;
    }

    /// <summary>
    /// 定义
    /// </summary>
    public WorkflowDefinition Definition { get; }

    /// <summary>
    /// 实例
    /// </summary>
    /// <remarks>
    /// 只读视图；状态变更只经引擎
    /// </remarks>
    public WorkflowInstanceView Instance { get; }

    /// <summary>
    /// 引擎正在修改的实例
    /// </summary>
    /// <remarks>
    /// 只给引擎派生恢复上下文用，不交给扩展代码：引擎在活动返回后把它写回存储
    /// </remarks>
    internal WorkflowInstance InstanceRecord { get; }

    /// <summary>
    /// 节点
    /// </summary>
    public WorkflowNode Node { get; }

    /// <summary>
    /// 节点实例
    /// </summary>
    /// <remarks>
    /// 只读视图；私有状态经 <see cref="State"/> 读写
    /// </remarks>
    public WorkflowNodeInstanceView NodeInstance { get; }

    /// <summary>
    /// 引擎正在修改的节点实例
    /// </summary>
    /// <remarks>
    /// 只给引擎派生恢复上下文与暴露 <see cref="State"/> 用，不交给扩展代码
    /// </remarks>
    internal WorkflowNodeInstance NodeInstanceRecord { get; }

    /// <summary>
    /// 实例变量
    /// </summary>
    /// <remarks>
    /// 读；写经输出合并
    /// </remarks>
    public WorkflowVariables Variables { get; }

    /// <summary>
    /// 节点私有状态（可改，跨挂起保留）
    /// </summary>
    public JsonObject State => NodeInstanceRecord.State;

    /// <summary>
    /// 本次执行的服务作用域
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// 本次执行的调用上下文（经完整管道）
    /// </summary>
    public InvocationContext Invocation { get; }

    /// <summary>
    /// 表达式求值器
    /// </summary>
    public IWorkflowExpressionEvaluator Evaluator { get; }

    /// <summary>
    /// 时钟
    /// </summary>
    public TimeProvider Clock { get; }

    /// <summary>
    /// 表达式作用域：实例变量（可再加临时值）
    /// </summary>
    public ExpressionScope Scope() => new(Variables.AsJsonObject(), Clock);

    /// <summary>
    /// 读节点属性
    /// </summary>
    /// <remarks>
    /// 没有为 <see langword="null"/>
    /// </remarks>
    public JsonNode? Property(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Node.Properties.TryGetPropertyValue(name, out var value) ? value : null;
    }

    /// <summary>
    /// 读节点属性并转成 <typeparamref name="T"/>
    /// </summary>
    /// <remarks>
    /// 没有或转不成返回 <paramref name="fallback"/>
    /// </remarks>
    public T? Property<T>(string name, T? fallback = default) => WorkflowValues.ConvertOr(Property(name), fallback);

    /// <summary>
    /// 读字符串属性并按模板渲染
    /// </summary>
    /// <remarks>
    /// 没有属性返回 <paramref name="fallback"/>（不渲染）
    /// </remarks>
    public Result<string> RenderProperty(string name, string? fallback = null)
    {
        var value = Property(name);
        if (value is null)
        {
            return fallback ?? string.Empty;
        }

        if (value is JsonValue scalar)
        {
            return Evaluator.Render(WorkflowValues.AsString(scalar), Scope());
        }

        return value.ToJsonString();
    }

    /// <summary>
    /// 把字符串属性当表达式求值
    /// </summary>
    /// <remarks>
    /// 非字符串属性原样作为有值返回；没有属性是成功的无值（ADR-0059）
    /// </remarks>
    public Result<Maybe<JsonNode>> EvaluateProperty(string name)
    {
        var value = Property(name);
        if (value is JsonValue scalar && scalar.TryGetValue<string>(out var expression))
        {
            return Evaluator.Evaluate(expression, Scope());
        }

        return Result.Success(Maybe.From(value));
    }

    /// <summary>
    /// 属性不合法的错误
    /// </summary>
    /// <remarks>
    /// 给自定义活动用：<paramref name="reason"/> 原样放进参数，不随调用方的区域本地化；要本地化的原因用应用自己的消息键返回错误（RFC-0010）。内置活动按种类各有消息键（ADR-0105）
    /// </remarks>
    public static Error PropertyInvalid(string name, string reason)
        => Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.ActivityPropertyInvalid, MessageArguments.Of(("name", name), ("reason", reason)));
}
