// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 条件属性的值种类。
/// </summary>
/// <remarks>
/// 决定条件值怎么解析、属性值怎么比较。整数值是持久化契约。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public enum ConditionValueKind
{
    /// <summary>
    /// 字符串（按序数比较，不区分大小写）。
    /// </summary>
    [SuppressMessage("Naming", "CA1720:标识符包含类型名称", Justification = "值种类的名字就是它描述的类型；与 4.x 及应用存储的取值一一对应。")]
    String = 0,

    /// <summary>
    /// 数字（按十进制比较）。
    /// </summary>
    Number = 1,

    /// <summary>
    /// 布尔（<c>true</c> / <c>false</c> / <c>1</c> / <c>0</c>）。
    /// </summary>
    Boolean = 2,

    /// <summary>
    /// 日期时间。
    /// </summary>
    /// <remarks>
    /// ISO 8601；不带偏移的按编译选项里的时区解释。
    /// </remarks>
    DateTime = 3,

    /// <summary>
    /// 日期（<c>yyyy-MM-dd</c>）。
    /// </summary>
    Date = 4,

    /// <summary>
    /// 一天中的时刻（<c>HH:mm</c> 或 <c>HH:mm:ss</c>）。
    /// </summary>
    TimeOfDay = 5,

    /// <summary>
    /// IP 地址。
    /// </summary>
    /// <remarks>
    /// 条件值里的集合可写 CIDR 网段。
    /// </remarks>
    IpAddress = 6,
}
