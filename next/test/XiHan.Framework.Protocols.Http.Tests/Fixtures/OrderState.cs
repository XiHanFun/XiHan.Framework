// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Protocols.Http.Tests.Fixtures;

/// <summary>
/// 测试用订单状态：上下文没有字符串枚举转换器，只能按数值绑定。
/// </summary>
internal enum OrderState
{
    /// <summary>待处理。</summary>
    Pending = 0,

    /// <summary>已关闭。</summary>
    Closed = 1,
}
