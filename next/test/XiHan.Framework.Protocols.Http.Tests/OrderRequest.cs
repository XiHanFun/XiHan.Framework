// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// 测试用订单请求：各字段分别来自路由值、请求体与查询串。
/// </summary>
/// <param name="Id">路由值 <c>{id}</c>。</param>
/// <param name="Name">请求体字段；为 <c>fail</c> 时操作返回冲突。</param>
/// <param name="Page">查询串，验证字符串到数字的绑定。</param>
/// <param name="Tags">多值查询串。</param>
/// <param name="Reason">请求体字段。</param>
internal sealed record OrderRequest(string? Id, string? Name, int Page = 0, string[]? Tags = null, string? Reason = null);
