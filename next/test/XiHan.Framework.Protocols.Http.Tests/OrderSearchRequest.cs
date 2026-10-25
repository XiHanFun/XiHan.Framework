// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// 测试用分页查询请求：带嵌套对象字段，验证 <c>list</c> 推导为 POST 自定义动词。
/// </summary>
/// <param name="Page">嵌套的分页参数。</param>
/// <param name="Keyword">关键字。</param>
internal sealed record OrderSearchRequest(OrderPaging? Page, string? Keyword);
