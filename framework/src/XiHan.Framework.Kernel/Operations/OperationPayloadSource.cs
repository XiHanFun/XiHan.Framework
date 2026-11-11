// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 延迟读取的操作载荷
/// </summary>
/// <remarks>
/// 宿主在管道开始前放进上下文（<see cref="OperationInvocationExtensions.SetOperationPayloadSource"/>），不在进管道前读请求体；Validate 阶段（排在 Authorize 之后）经 <see cref="OperationInvocationExtensions.ResolveOperationPayloadAsync"/> 读一次，没有认证、没有权限或被限流的调用不会让宿主缓冲请求体（ADR-0176）。读取失败（请求体不是 JSON、超过上限等）返回失败结果，调用以它结束
/// </remarks>
/// <param name="cancellationToken">取消令牌</param>
[ApiLevel(Stability.Stable, "5.0")]
public delegate ValueTask<Result<ReadOnlyMemory<byte>>> OperationPayloadSource(CancellationToken cancellationToken);
