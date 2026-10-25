// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Protocols.Http.Tests.Fixtures;

/// <summary>
/// 不含 <c>id</c> 的测试请求：验证自定义动词走集合级路由 <c>POST /orders:report</c>。
/// </summary>
/// <param name="Name">请求体字段。</param>
/// <param name="Page">查询串，验证字符串到数字的绑定。</param>
internal sealed record OrderReportRequest(string? Name, int Page = 0);
