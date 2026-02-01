#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumInfo
// Guid:ee068353-ab88-4140-b766-a2b961ab67c3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/01 05:10:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Enums;

/// <summary>
/// 枚举信息
/// </summary>
public record EnumInfo
{
    /// <summary>
    /// 枚举类型
    /// </summary>
    public Type Type { get; set; } = null!;

    /// <summary>
    /// 底层类型
    /// </summary>
    public Type UnderlyingType { get; set; } = null!;

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 是否为标志枚举
    /// </summary>
    public bool IsFlags { get; set; }

    /// <summary>
    /// 所有值
    /// </summary>
    public object[] Values { get; set; } = [];

    /// <summary>
    /// 所有名称
    /// </summary>
    public string[] Names { get; set; } = [];
}