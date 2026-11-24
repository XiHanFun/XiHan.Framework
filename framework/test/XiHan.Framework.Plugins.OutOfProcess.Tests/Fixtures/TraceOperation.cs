// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

/// <summary>
/// 宿主侧的操作：回显这次调用的上游
/// </summary>
/// <remarks>
/// 响应的 <c>name</c> 是调用上下文的 parent-id、<c>principal</c> 是 trace-id。供追踪用例核对插件回调带回来的 traceparent：它就是宿主调用插件时写给插件的那一个
/// </remarks>
internal sealed class TraceOperation : IOperation<PingRequest, PingResponse>
{
    /// <inheritdoc />
    public ValueTask<Result<PingResponse>> HandleAsync(PingRequest request, InvocationContext context)
        => ValueTask.FromResult(Result.Success(new PingResponse(context.SpanId, context.TraceId, null, null)));
}
