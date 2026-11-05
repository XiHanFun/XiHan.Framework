// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Generators.Json;

/// <summary>
/// 与 <c>System.Text.Json.Serialization.JsonIgnoreCondition</c> 同值的忽略条件
/// </summary>
internal enum JsonIgnoreConditionKind
{
    /// <summary>
    /// 总是序列化
    /// </summary>
    Never = 0,

    /// <summary>
    /// 不序列化也不反序列化
    /// </summary>
    Always = 1,

    /// <summary>
    /// 值为类型默认值时不写出
    /// </summary>
    WhenWritingDefault = 2,

    /// <summary>
    /// 值为 null 时不写出
    /// </summary>
    WhenWritingNull = 3,

    /// <summary>
    /// 只读入、不写出
    /// </summary>
    WhenWriting = 4,

    /// <summary>
    /// 只写出、不读入
    /// </summary>
    WhenReading = 5,
}
