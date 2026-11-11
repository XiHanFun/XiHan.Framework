// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Http;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Endpoints;

/// <summary>
/// 操作端点的载荷绑定
/// </summary>
/// <remarks>
/// 协议适配器把它挂在端点元数据上，<c>UseXiHan()</c> 把它包成调用上下文里的载荷来源（<c>OperationPayloadSource</c>），Validate 阶段（Authorize 之后）才调用它把 HTTP 请求绑定为 XGI 载荷，端点内只剩执行；没有认证、没有权限或被限流的调用不会读请求体（ADR-0176）。绑定失败（请求体不是 JSON、超过上限等）返回的失败结果是调用的结果，写成 Problem Details
/// </remarks>
/// <param name="httpContext">当前请求</param>
/// <param name="cancellationToken">取消令牌</param>
[ApiLevel(Stability.Preview, "5.0")]
public delegate ValueTask<Result<ReadOnlyMemory<byte>>> OperationPayloadBinder(HttpContext httpContext, CancellationToken cancellationToken);
