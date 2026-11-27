// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Benchmarks;

/// <summary>
/// 基准用的订单摘要
/// </summary>
/// <remarks>
/// 文本、金额、可空值与列表各有一个，接近一般操作的响应
/// </remarks>
/// <param name="Id">订单 ID</param>
/// <param name="Customer">客户名</param>
/// <param name="Total">金额</param>
/// <param name="Priority">优先级；没有为 <see langword="null"/></param>
/// <param name="Tags">标签</param>
public sealed record OrderSummary(string Id, string Customer, decimal Total, int? Priority, IReadOnlyList<string> Tags);
