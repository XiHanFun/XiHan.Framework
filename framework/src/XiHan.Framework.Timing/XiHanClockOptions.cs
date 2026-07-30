// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Timing;

/// <summary>
/// 时钟选项
/// </summary>
public class XiHanClockOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanClockOptions()
    {
        Kind = DateTimeKind.Unspecified;
    }

    /// <summary>
    /// 时间类型
    /// </summary>
    public DateTimeKind Kind { get; set; }
}
