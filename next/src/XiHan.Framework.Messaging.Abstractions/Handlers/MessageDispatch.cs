// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Handlers;

/// <summary>
/// 把一次投递交给处理器。
/// </summary>
/// <remarks>
/// 返回失败表示投递失败，由代理重试或死信。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public delegate ValueTask<Result> MessageDispatch(MessageDelivery delivery, CancellationToken cancellationToken);
