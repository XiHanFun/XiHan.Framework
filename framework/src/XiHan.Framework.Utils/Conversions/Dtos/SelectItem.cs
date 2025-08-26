#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SelectItem
// Guid:569bf839-bfd3-4f7a-8968-d235a0b5b7c6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/4 3:03:19
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Conversions.Dtos;

/// <summary>
/// 选择项
/// </summary>
public class SelectItem
{
    /// <summary>
    /// 键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 值
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 文本
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
