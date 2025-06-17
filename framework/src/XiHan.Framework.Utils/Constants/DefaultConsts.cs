#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultConsts
// Guid:5a6b031c-669d-4867-9ebb-89eaa2e8f269
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/17 14:17:51
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Constants;

/// <summary>
/// DefaultConsts
/// </summary>
public class DefaultConsts
{
    /// <summary>
    /// 特殊字符
    /// </summary>
    public const string SpecialCharacters = @"!@#$%^&*()-_=+[]{}|;:'"",.<>?/";

    /// <summary>
    /// 特殊字符(不包含引号)
    /// </summary>
    public const string SpecialCharactersWithoutQuotes = @"!@#$%^&*()-_=+[]{}|;,.<>?/";

    /// <summary>
    /// 大写字母
    /// </summary>
    public const string UppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// 小写字母
    /// </summary>
    public const string LowercaseLetters = "abcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// 字母
    /// </summary>
    public const string Letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// 数字
    /// </summary>
    public const string Digits = "0123456789";
}
