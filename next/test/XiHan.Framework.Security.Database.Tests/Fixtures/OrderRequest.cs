// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Security.Database.Tests.Fixtures;

/// <summary>
/// 写订单操作的请求：订单号。
/// </summary>
/// <param name="Number">订单号。</param>
internal sealed record OrderRequest(string Number);
