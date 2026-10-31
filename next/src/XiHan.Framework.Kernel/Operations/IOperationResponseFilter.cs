// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 操作响应的过滤器。
/// </summary>
/// <remarks>
/// 操作成功后、响应对象写进上下文之前，调用器从调用的服务作用域里取出全部过滤器（任意生命周期），按登记顺序调用，后一个拿到前一个的输出（RFC-0011，ADR-0061）。HTTP、gRPC、MCP 编码出去的与类型化调用拿到的都是过滤后的响应。用途是按调用者裁剪响应，如字段级权限的掩码；失败的调用不过滤。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IOperationResponseFilter
{
    /// <summary>
    /// 过滤一次成功调用的响应。
    /// </summary>
    /// <remarks>
    /// 返回值必须是 <see cref="OperationDescriptor.ResponseType"/> 的实例（可以是新实例），否则调用器抛出 <see cref="InvalidOperationException"/>；不需要改动的原样返回 <paramref name="response"/>。
    /// </remarks>
    ValueTask<object> FilterAsync(InvocationContext context, OperationDescriptor operation, object response);
}
