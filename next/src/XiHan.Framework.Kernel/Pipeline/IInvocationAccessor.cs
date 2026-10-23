// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 当前调用的上下文访问器（RFC-0003）：作用域服务，内核在调用进入管道时设置、离开时恢复上一层，同一作用域内嵌套调用各看到自己的上下文。
/// 操作处理器与中间件直接用参数里的上下文；本接口给调用链深处不传上下文的服务（领域服务、仓储装饰器、从 4.x 迁来的"当前用户 / 当前租户"包装）用。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IInvocationAccessor
{
    /// <summary>
    /// 当前调用的上下文；不在调用内（作用域刚创建、后台任务、调用已结束）为无值。
    /// </summary>
    Maybe<InvocationContext> Current { get; }
}
