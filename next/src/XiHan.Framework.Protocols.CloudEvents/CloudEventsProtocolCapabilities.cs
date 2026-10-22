// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.CloudEvents;

/// <summary>
/// CloudEvents 协议适配器的能力 ID 与接口版本。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class CloudEventsProtocolCapabilities
{
    /// <summary>CloudEvents 协议：消息信封经 CloudEvents HTTP 绑定进出。</summary>
    public const string CloudEvents = "protocols.cloudevents";

    /// <summary>当前接口版本。</summary>
    public const int Version = 1;
}
