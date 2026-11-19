// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 枚举载荷用例的模式，类型上标了字符串枚举转换器
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ShapeMode>))]
internal enum ShapeMode
{
    /// <summary>
    /// 关
    /// </summary>
    Off = 0,

    /// <summary>
    /// 开
    /// </summary>
    On = 1,
}
