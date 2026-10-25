// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Auditing.Abstractions.Tests.Fixtures;

/// <summary>
/// 创建请求。
/// </summary>
/// <param name="Amount">金额。</param>
internal sealed record CreateOrderRequest(decimal Amount);
