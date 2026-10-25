// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Protocols.Http.Tests.Fixtures;

/// <summary>
/// 测试用筛选请求：布尔、数值枚举、长整数数组与整数字段，验证路由值与查询串按属性类型绑定。
/// </summary>
/// <param name="Id">路由值 <c>{id}</c>，长整数。</param>
/// <param name="Active">查询串布尔。</param>
/// <param name="State">查询串数值枚举。</param>
/// <param name="Ids">查询串长整数数组（单值也写成数组）。</param>
/// <param name="Page">查询串整数。</param>
internal sealed record OrderFilterRequest(long Id, bool Active, OrderState State, long[]? Ids = null, int Page = 0);
