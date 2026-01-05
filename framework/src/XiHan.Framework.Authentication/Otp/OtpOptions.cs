#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OtpOptions
// Guid:e1f2a3b4-c5d6-7890-4567-123456789020
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authentication.Otp;

/// <summary>
/// OTP 配置选项
/// </summary>
public class OtpOptions
{
    /// <summary>
    /// 密钥长度
    /// </summary>
    public int SecretKeyLength { get; set; } = 32;

    /// <summary>
    /// OTP 代码位数
    /// </summary>
    public int Digits { get; set; } = 6;

    /// <summary>
    /// 时间步长（秒）
    /// </summary>
    public int TimeStep { get; set; } = 30;

    /// <summary>
    /// 允许的时间偏移量
    /// </summary>
    /// <remarks>
    /// 允许前后偏移的时间窗口数，用于处理时间不同步的问题
    /// </remarks>
    public int AllowedSkew { get; set; } = 1;

    /// <summary>
    /// 是否启用备用恢复码
    /// </summary>
    public bool EnableRecoveryCodes { get; set; } = true;

    /// <summary>
    /// 恢复码数量
    /// </summary>
    public int RecoveryCodesCount { get; set; } = 10;
}
