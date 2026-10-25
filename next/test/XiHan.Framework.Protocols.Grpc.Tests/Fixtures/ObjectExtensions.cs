// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Protocols.Grpc.Tests.Fixtures;

/// <summary>
/// 测试断言里的小工具。
/// </summary>
internal static class ObjectExtensions
{
    /// <summary>
    /// 把值投影成另一个值，便于在一条断言里比较多个字段。
    /// </summary>
    public static TOut Let<TIn, TOut>(this TIn value, Func<TIn, TOut> project) => project(value);
}
