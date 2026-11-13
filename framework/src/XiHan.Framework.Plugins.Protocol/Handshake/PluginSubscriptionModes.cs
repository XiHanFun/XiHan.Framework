// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Handlers;

namespace XiHan.Framework.Plugins.Protocol.Handshake;

/// <summary>
/// 订阅方式与协议文本之间的映射
/// </summary>
/// <remarks>
/// 握手的 <c>Subscription.mode</c> 与投递的 <c>HandleEventRequest.mode</c> 用同一组文本（RFC-0009「订阅方式」，ADR-0226）
/// </remarks>
internal static class PluginSubscriptionModes
{
    /// <summary>
    /// 竞争消费的协议文本
    /// </summary>
    public const string Competing = "competing";

    /// <summary>
    /// 广播的协议文本
    /// </summary>
    public const string Broadcast = "broadcast";

    /// <summary>
    /// 订阅方式转为协议文本
    /// </summary>
    /// <remarks>
    /// 宿主投递时总是写出方式、不写空，插件据此只交给以这种方式订阅的处理器（<c>spec/plugin/invocation.md</c> §4）
    /// </remarks>
    public static string ToProto(SubscriptionMode mode) => mode switch
    {
        SubscriptionMode.Competing => Competing,
        SubscriptionMode.Broadcast => Broadcast,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "The subscription mode is not defined."),
    };

    /// <summary>
    /// 解析协议文本里的订阅方式
    /// </summary>
    /// <remarks>
    /// 空即竞争消费（协议 v1 加这个字段之前的插件照旧），大小写不同或别的值都不认
    /// </remarks>
    public static bool TryParse(string text, out SubscriptionMode mode)
    {
        switch (text)
        {
            case "" or Competing:
                mode = SubscriptionMode.Competing;
                return true;
            case Broadcast:
                mode = SubscriptionMode.Broadcast;
                return true;
            default:
                mode = default;
                return false;
        }
    }
}
