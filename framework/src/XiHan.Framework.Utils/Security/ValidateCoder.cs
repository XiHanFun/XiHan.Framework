#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ValidateCoder
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
/// 验证码生成器
/// </summary>
public static class ValidateCoder
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
    public static string GetNumber(int? length, string? source)
    {
        return RandomHelper.GetRandom(length ?? 6, source ?? DefaultNumberSource);
    }

    /// <summary>
    /// 随机大写字母
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义大写字母字符源</param>
    /// <returns></returns>
    public static string GetUpperLetter(int? length, string? source)
    {
        return RandomHelper.GetRandom(length ?? 6, source?.ToUpperInvariant() ?? DefaultUpperLetterSource);
    }

    /// <summary>
    /// 随机小写字母
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义小写字母字符源</param>
    /// <returns></returns>
    public static string GetLowerLetter(int? length, string? source)
    {
        return RandomHelper.GetRandom(length ?? 6, source?.ToLowerInvariant() ?? DefaultLowerLetterSource);
    }

    /// <summary>
    /// 随机字母或数字
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义字母或数字字符源</param>
    /// <returns></returns>
    public static string GetNumberOrLetter(int? length, string? source)
    {
        return RandomHelper.GetRandom(length ?? 6, source ?? DefaultNumberOrLetterSource);
    }

    /// <summary>
    /// 随机汉字
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <returns>所有汉字</returns>
    public static string RandChineseCharacter(int? length)
    {
        //汉字由区位和码位组成(都为0-94,其中区位16-55为一级汉字区,56-87为二级汉字区,1-9为特殊字符区)
        int area, code;
        var strtem = new StringBuilder();
        length ??= 6;
        for (var i = 0; i < length; i++)
        {
            area = RandomHelper.GetRandom(16, 88);
            code = area == 55 ? RandomHelper.GetRandom(1, 90) : RandomHelper.GetRandom(1, 94);
            _ = strtem.Append(Encoding.GetEncoding("GB2312").GetString([Convert.ToByte(area + 160), Convert.ToByte(code + 160)]));
        }

        return strtem.ToString();
    }
}
