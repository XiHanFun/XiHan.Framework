// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 载荷路径向量里的订单请求：<c>{ lines: [ { quantity } ] }</c>
/// </summary>
/// <param name="Lines">明细</param>
internal sealed record VectorOrder(List<VectorLine> Lines);
