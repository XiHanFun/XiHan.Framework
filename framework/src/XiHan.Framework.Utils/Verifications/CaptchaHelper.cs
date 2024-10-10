#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2022 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CaptchaHelper
// Guid:b18b6086-49ff-45f2-abb9-86a7a8505db4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-09-18 上午 02:20:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;

namespace XiHan.Framework.Utils.Verifications;

/// <summary>
/// 验证码帮助类
/// </summary>
public static class CaptchaHelper
{
    // 默认数字字符源
    private const string DefaultNumberSource = "0123456789";

    // 默认大写字母字符源
    private const string DefaultUpperLetterSource = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    // 默认小写字母字符源
    private const string DefaultLowerLetterSource = "abcdefghijklmnopqrstuvwxyz";

    // 默认字母或数字字符源
    private const string DefaultNumberOrLetterSource = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// 随机数字
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义数字字符源</param>
    /// <returns></returns>
    public static string CodeNumber(int? length, string? source)
    {
        return RandomTo(length ?? 6, source ?? DefaultNumberSource);
    }

    /// <summary>
    /// 随机大写字母
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义大写字母字符源</param>
    /// <returns></returns>
    public static string CodeUpperLetter(int? length, string? source)
    {
        return RandomTo(length ?? 6, source?.ToUpperInvariant() ?? DefaultUpperLetterSource);
    }

    /// <summary>
    /// 随机小写字母
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义小写字母字符源</param>
    /// <returns></returns>
    public static string CodeLowerLetter(int? length, string? source)
    {
        return RandomTo(length ?? 6, source?.ToLowerInvariant() ?? DefaultLowerLetterSource);
    }

    /// <summary>
    /// 随机字母或数字
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义字母或数字字符源</param>
    /// <returns></returns>
    public static string CodeNumberOrLetter(int? length, string? source)
    {
        return RandomTo(length ?? 6, source ?? DefaultNumberOrLetterSource);
    }

    /// <summary>
    /// 根据字符源生成随机字符
    /// </summary>
    /// <param name="length">生成长度</param>
    /// <param name="source">自定义字符源</param>
    /// <returns></returns>
    private static string RandomTo(int length, string source)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentException.ThrowIfNullOrEmpty(source);

        StringBuilder result = new();
        Random random = new(~unchecked((int)DateTime.Now.Ticks));
        for (var i = 0; i < length; i++)
        {
            result.Append(source[random.Next(0, source.Length)]);
        }

        return result.ToString();
    }
}