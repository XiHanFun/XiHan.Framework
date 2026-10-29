// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 一个操作。
/// </summary>
/// <remarks>
/// 接收请求，在 Handle 阶段执行，返回结果。类型即契约，请求与响应类型由源生成器产出 Schema；可恢复失败走 <see cref="Result{T}"/>，不抛异常。实现按作用域从服务容器解析。
/// </remarks>
/// <typeparam name="TRequest">请求类型。</typeparam>
/// <typeparam name="TResponse">响应类型；成功值不能为 <see langword="null"/>。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public interface IOperation<TRequest, TResponse>
{
    /// <summary>
    /// 执行操作。
    /// </summary>
    ValueTask<Result<TResponse>> HandleAsync(TRequest request, InvocationContext context);
}
