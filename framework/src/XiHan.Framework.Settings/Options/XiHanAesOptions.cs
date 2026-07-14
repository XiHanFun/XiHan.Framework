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
    /// 密钥（加密设置的唯一必配项；AES 的 Key/IV 均由它经 PBKDF2 派生，任意长度字符串均可）
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// IV（保留项；当前实现由 <see cref="Key"/> 派生 IV，无需单独配置）
    /// </summary>
    public string Iv { get; set; } = string.Empty;
}
