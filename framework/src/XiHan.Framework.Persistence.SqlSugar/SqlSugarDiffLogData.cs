// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 仓储写入开启差异日志时交给 SqlSugar 的业务数据
/// </summary>
/// <remarks>
/// <c>AopEvents.OnDiffLogEvent</c> 收到的 <c>DiffLogModel.BusinessData</c> 就是它。回调在写入执行后、由执行写入的调用同步触发（不等提交），AsyncLocal 照常可读；作用域服务经 <see cref="Services"/> 取，当前调用的上下文经其中的 <c>IInvocationAccessor</c> 取。要缓冲到提交后再落库时在回调里复制所需的值：SqlSugar 在分表插入的各组之间复用同一个 <c>DiffLogModel</c>，留下的引用会被后一组改掉。成员经初始化器给出，以后加字段只加属性（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Experimental, "5.0")]
public sealed record SqlSugarDiffLogData
{
    /// <summary>
    /// 被写入的实体类型
    /// </summary>
    public required Type EntityType { get; init; }

    /// <summary>
    /// 执行写入的仓储所在作用域的服务提供器
    /// </summary>
    public required IServiceProvider Services { get; init; }
}
