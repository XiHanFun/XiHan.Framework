#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanClockOptions
// Guid:ab304d4c-7359-43d8-8e7a-fc0d0b96a369
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 5:29:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Timing;

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
