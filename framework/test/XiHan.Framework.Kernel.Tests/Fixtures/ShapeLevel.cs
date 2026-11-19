// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 枚举载荷用例的级别，读成整数还是成员名由序列化上下文决定
/// </summary>
internal enum ShapeLevel : byte
{
    /// <summary>
    /// 低
    /// </summary>
    Low = 1,

    /// <summary>
    /// 高
    /// </summary>
    High = 2,
}
