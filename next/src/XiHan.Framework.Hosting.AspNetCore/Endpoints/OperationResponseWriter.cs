// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Http;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Endpoints;

/// <summary>
/// 操作端点写出成功响应的方式。
/// </summary>
/// <remarks>
/// 协议适配器把它挂在端点元数据上（状态码、内容类型等按协议的约定），端点执行成功时用它写响应。管道成功结束而端点没有执行、上下文里已有响应对象时（Idempotency 阶段回放，RFC-0006），<c>UseXiHan()</c> 在响应未开始时用它写出同样的响应。
/// </remarks>
/// <param name="httpContext">当前请求。</param>
/// <param name="descriptor">端点的操作描述。</param>
/// <param name="payload">响应的 UTF-8 JSON（<see cref="OperationDescriptor.EncodeResponse"/> 的结果）。</param>
/// <param name="cancellationToken">取消令牌。</param>
[ApiLevel(Stability.Preview, "5.0")]
public delegate Task OperationResponseWriter(HttpContext httpContext, OperationDescriptor descriptor, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
