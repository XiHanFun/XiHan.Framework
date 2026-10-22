// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions;

namespace XiHan.Framework.Protocols.CloudEvents.Tests;

/// <summary>
/// 按内容比较信封：记录的默认相等比较对 <see cref="ReadOnlyMemory{T}"/> 与字典只比引用。
/// </summary>
internal static class EnvelopeAssert
{
    /// <summary>
    /// 断言两个信封的全部属性、数据字节与扩展属性相同。
    /// </summary>
    public static void Same(MessageEnvelope expected, MessageEnvelope actual)
    {
        Assert.Equal((expected.Id, expected.Source, expected.Type, expected.Time, expected.Subject, expected.DataContentType), (actual.Id, actual.Source, actual.Type, actual.Time, actual.Subject, actual.DataContentType));
        Assert.Equal(expected.Data.ToArray(), actual.Data.ToArray());
        Assert.Equal(expected.Extensions.OrderBy(static e => e.Key, StringComparer.Ordinal), actual.Extensions.OrderBy(static e => e.Key, StringComparer.Ordinal));
    }
}
