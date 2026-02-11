#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAesOptions
// Guid:c83f458d-bee6-4ffc-9ff9-68061d90e6bf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/25 04:04:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Settings.Options;

/// <summary>
/// 曦寒 Aes 选项
/// </summary>
public class XiHanAesOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Settings:Aes";

    /// <summary>
    /// 密钥
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// IV
    /// </summary>
    public string Iv { get; set; } = string.Empty;
}
