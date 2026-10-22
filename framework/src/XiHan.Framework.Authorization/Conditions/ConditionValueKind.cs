// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 条件属性的值种类，决定条件值怎么解析、属性值怎么比较
/// </summary>
public enum ConditionValueKind
{
    /// <summary>
    /// 字符串（按序数比较，不区分大小写）
    /// </summary>
    [Description("字符串")]
    String = 0,

    /// <summary>
    /// 数字（按十进制比较）
    /// </summary>
    [Description("数字")]
    Number = 1,

    /// <summary>
    /// 布尔
    /// </summary>
    [Description("布尔")]
    Boolean = 2,

    /// <summary>
    /// 日期时间（ISO 8601；不带偏移的按编译选项里的时区解释）
    /// </summary>
    [Description("日期时间")]
    DateTime = 3,

    /// <summary>
    /// 日期（yyyy-MM-dd）
    /// </summary>
    [Description("日期")]
    Date = 4,

    /// <summary>
    /// 一天中的时刻（HH:mm 或 HH:mm:ss）
    /// </summary>
    [Description("时刻")]
    TimeOfDay = 5,

    /// <summary>
    /// IP 地址；条件值里的集合可写 CIDR 网段
    /// </summary>
    [Description("IP 地址")]
    IpAddress = 6
}
