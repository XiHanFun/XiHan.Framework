#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RandomCoder
// Guid:7fbc0368-1a12-4a65-bfb6-dcf2f1094f2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/9 5:17:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Utils.Security;

/// <summary>
/// 随机码生成器
/// </summary>
public static class RandomCoder
{
    /// <summary>
    /// 默认特殊符号字符源
    /// </summary>
    private static readonly string DefaultSpecialCharSource = "!@#$%^&*()-_=+[]{}|;:,.<>?/";

    /// <summary>
    /// 默认大写字母字符源
    /// </summary>
    private static readonly string DefaultUpperLetterSource = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// 默认小写字母字符源
    /// </summary>
    private static readonly string DefaultLowerLetterSource = "abcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// 默认数字字符源
    /// </summary>
    private static readonly string DefaultNumberSource = "0123456789";

    /// <summary>
    /// 随机数字
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义数字字符源</param>
    /// <returns>随机数字字符串</returns>
    public static string GetNumber(int? length = 6, string? source = null)
    {
        return RandomHelper.GetRandom(length ?? 6, source ?? DefaultNumberSource);
    }

    /// <summary>
    /// 随机特殊符号
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义特殊符号字符源</param>
    /// <returns>随机特殊符号字符串</returns>
    public static string GetSpecialChars(int? length = 6, string? source = null)
    {
        return RandomHelper.GetRandom(length ?? 6, source ?? DefaultSpecialCharSource);
    }

    /// <summary>
    /// 随机字母
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义字母字符源</param>
    /// <returns>随机字母字符串</returns>
    public static string GetLetter(int? length = 6, string? source = null)
    {
        return RandomHelper.GetRandom(length ?? 6, source ?? DefaultUpperLetterSource + DefaultLowerLetterSource);
    }

    /// <summary>
    /// 随机大写字母
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义大写字母字符源</param>
    /// <returns>随机大写字母字符串</returns>
    public static string GetUpperLetter(int? length = 6, string? source = null)
    {
        return RandomHelper.GetRandom(length ?? 6, source?.ToUpperInvariant() ?? DefaultUpperLetterSource);
    }

    /// <summary>
    /// 随机小写字母
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义小写字母字符源</param>
    /// <returns>随机小写字母字符串</returns>
    public static string GetLowerLetter(int? length = 6, string? source = null)
    {
        return RandomHelper.GetRandom(length ?? 6, source?.ToLowerInvariant() ?? DefaultLowerLetterSource);
    }

    /// <summary>
    /// 随机字母或数字
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义字母或数字字符源</param>
    /// <returns>随机字母或数字字符串</returns>
    public static string GetNumberOrLetter(int? length = 6, string? source = null)
    {
        var defaultSource = DefaultNumberSource + DefaultUpperLetterSource + DefaultLowerLetterSource;
        return RandomHelper.GetRandom(length ?? 6, source ?? defaultSource);
    }

    /// <summary>
    /// 生成强密码(包含字母、数字和特殊字符的组合)
    /// </summary>
    /// <param name="length">生成长度 默认12个字符</param>
    /// <param name="source">自定义强密码字符源</param>
    /// <returns>随机强密码字符串</returns>
    public static string GetStrongPassword(int? length = 12, string? source = null)
    {
        var defaultSource = DefaultNumberSource + DefaultUpperLetterSource + DefaultLowerLetterSource + DefaultSpecialCharSource;
        return RandomHelper.GetRandom(length ?? 12, source ?? defaultSource);
    }

    /// <summary>
    /// 生成包含指定字符类型的随机字符串
    /// </summary>
    /// <param name="length">生成长度</param>
    /// <param name="includeNumbers">是否包含数字</param>
    /// <param name="includeUpperLetters">是否包含大写字母</param>
    /// <param name="includeLowerLetters">是否包含小写字母</param>
    /// <param name="includeSpecialChars">是否包含特殊字符</param>
    /// <returns>根据指定条件生成的随机字符串</returns>
    public static string GetCustom(int length = 8, bool includeNumbers = true, bool includeUpperLetters = true,
        bool includeLowerLetters = true, bool includeSpecialChars = false)
    {
        var source = new StringBuilder();

        if (includeNumbers)
        {
            source.Append(DefaultNumberSource);
        }

        if (includeUpperLetters)
        {
            source.Append(DefaultUpperLetterSource);
        }

        if (includeLowerLetters)
        {
            source.Append(DefaultLowerLetterSource);
        }

        if (includeSpecialChars)
        {
            source.Append(DefaultSpecialCharSource);
        }

        // 如果没有选择任何字符集，默认使用数字和字母
        if (source.Length == 0)
        {
            source.Append(DefaultNumberSource + DefaultUpperLetterSource + DefaultLowerLetterSource);
        }

        return RandomHelper.GetRandom(length, source.ToString());
    }

    /// <summary>
    /// 随机汉字
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <returns>随机汉字字符串</returns>
    public static string GetChineseCharacters(int? length = 6)
    {
        //汉字由区位和码位组成(都为0-94,其中区位16-55为一级汉字区,56-87为二级汉字区,1-9为特殊字符区)
        var strtem = new StringBuilder();
        length ??= 6;

        for (var i = 0; i < length; i++)
        {
            var area = RandomHelper.GetRandom(16, 88);
            var code = area == 55 ? RandomHelper.GetRandom(1, 90) : RandomHelper.GetRandom(1, 94);
            _ = strtem.Append(Encoding.GetEncoding("GB2312").GetString([Convert.ToByte(area + 160), Convert.ToByte(code + 160)]));
        }

        return strtem.ToString();
    }
}
