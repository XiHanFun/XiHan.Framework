// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.CloudEvents.Inbound;

/// <summary>
/// CloudEvents HTTP 绑定的内容模式。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum CloudEventsContentMode
{
    /// <summary>
    /// binary：属性在 <c>ce-*</c> 头里，数据是请求体，<c>Content-Type</c> 就是数据内容类型。
    /// </summary>
    Binary = 0,

    /// <summary>
    /// structured：整个事件是 <c>application/cloudevents+json</c> 的请求体。
    /// </summary>
    Structured = 1,

    /// <summary>
    /// batch：<c>application/cloudevents-batch+json</c> 的事件数组；只用于入站。
    /// </summary>
    Batch = 2,
}
