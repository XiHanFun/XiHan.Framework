// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
