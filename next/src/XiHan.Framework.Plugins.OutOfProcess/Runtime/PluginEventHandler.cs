// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.OutOfProcess.Runtime;

/// <summary>
/// 把订阅主题的消息转交给订阅了它的插件。
/// </summary>
/// <remarks>
/// 按主题登记一次，实际路由由宿主按握手声明决定。
/// </remarks>
internal sealed class PluginEventHandler(OutOfProcessPluginHost host) : IMessageHandler
{
    /// <inheritdoc />
    public ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(context);
        return host.DeliverAsync(delivery, context);
    }
}
