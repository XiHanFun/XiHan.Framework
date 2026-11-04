// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Handlers;

/// <summary>
/// 订阅方式
/// </summary>
/// <remarks>
/// 决定一条消息在订阅了同一主题的多个实例之间怎么分（RFC-0009「订阅方式」，ADR-0079）。同一主题可以同时有两种方式的处理器，两种方式各自消费、各自重试
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public enum SubscriptionMode
{
    /// <summary>
    /// 竞争消费，订阅了主题的实例里只有一个收到
    /// </summary>
    /// <remarks>
    /// 缺省方式，工作队列语义：至少一次，重试到上限后进死信；订阅方离线期间的消息由支持留存的代理保留
    /// </remarks>
    Competing = 0,

    /// <summary>
    /// 广播，每个在线实例各收到一份
    /// </summary>
    /// <remarks>
    /// 用于断开本实例上的连接、清本地缓存这类每个节点都要做的事。尽力而为：实例不在线、重启、与代理断开期间的消息收不到，不回放历史；重试到上限后丢弃并记日志，不进死信；不共享去重
    /// </remarks>
    Broadcast = 1,
}
