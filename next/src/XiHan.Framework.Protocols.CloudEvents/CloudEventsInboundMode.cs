// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.CloudEvents;

/// <summary>
/// 入站事件的处理方式。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum CloudEventsInboundMode
{
    /// <summary>在请求内经内核管道派发给主题的处理器：处理结果就是响应，发送方据此重试；主题没有处理器时 404。</summary>
    Dispatch = 0,

    /// <summary>发布到消息代理后立即 202：至少一次投递、重试与死信由代理负责。</summary>
    Publish = 1,
}
