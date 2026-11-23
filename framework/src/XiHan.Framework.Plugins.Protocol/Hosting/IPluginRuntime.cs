// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.Proxies;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.Protocol.Hosting;

/// <summary>
/// 名册里的一个插件运行时
/// </summary>
/// <remarks>
/// <see cref="PluginRoster{TRuntime}"/> 只经它看运行时（ADR-0260）：清单与握手声明、状态，以及把事件交给它。启动、停止与释放是载体的事，由各宿主直接调运行时
/// </remarks>
internal interface IPluginRuntime : IPluginInvoker
{
    /// <summary>
    /// 清单
    /// </summary>
    Manifest Manifest { get; }

    /// <summary>
    /// 首次成功握手的结果
    /// </summary>
    /// <remarks>
    /// 尚未握手成功为空；一旦有值不再变
    /// </remarks>
    HandshakeInfo? Handshake { get; }

    /// <summary>
    /// 当前状态
    /// </summary>
    LifecycleState State { get; }

    /// <summary>
    /// 当前错误
    /// </summary>
    Error? Error { get; }

    /// <summary>
    /// 对外的状态快照
    /// </summary>
    PluginStatus Status { get; }

    /// <summary>
    /// 把一条消息投递给插件
    /// </summary>
    /// <param name="delivery">投递</param>
    /// <param name="context">调用上下文</param>
    ValueTask<Result> DeliverAsync(MessageDelivery delivery, InvocationContext context);
}
