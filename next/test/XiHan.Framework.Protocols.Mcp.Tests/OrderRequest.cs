// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Protocols.Mcp.Tests;

/// <summary>
/// 测试用订单请求：全部字段来自 tool 的 <c>arguments</c>。
/// </summary>
/// <param name="Id">标识。</param>
/// <param name="Name">名字；为 <c>fail</c> 时操作返回冲突。</param>
/// <param name="Page">页码。</param>
/// <param name="Tags">标签。</param>
/// <param name="Reason">原因。</param>
internal sealed record OrderRequest(string? Id, string? Name, int Page = 0, string[]? Tags = null, string? Reason = null);
