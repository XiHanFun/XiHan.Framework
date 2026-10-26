// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Http;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Endpoints;

/// <summary>
/// 操作端点的载荷绑定：协议适配器把它挂在端点元数据上，<c>UseXiHan()</c> 在进入内核管道前调用它把 HTTP 请求绑定为 XGI 载荷并放进上下文，
/// 让 Validate 阶段能校验载荷、端点内只剩执行。绑定失败（请求体不是 JSON 对象等）返回的失败结果直接写成 Problem Details，不进管道。
/// </summary>
/// <param name="httpContext">当前请求。</param>
/// <param name="cancellationToken">取消令牌。</param>
[ApiLevel(Stability.Preview, "5.0")]
public delegate ValueTask<Result<ReadOnlyMemory<byte>>> OperationPayloadBinder(HttpContext httpContext, CancellationToken cancellationToken);
