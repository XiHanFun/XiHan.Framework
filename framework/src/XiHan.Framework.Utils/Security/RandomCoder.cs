// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.Utils.Constants;

namespace XiHan.Framework.Utils.Security;

/// <summary>
/// 随机码生成器
/// </summary>
/// <remarks>
/// 全部方法基于加密安全随机数（<see cref="RandomNumberGenerator"/>），可直接用于验证码、临时密码、密钥片段等安全敏感场景。
/// 非安全场景（抖动、打散、采样）建议使用 <see cref="Core.RandomHelper"/>（更轻量）。
/// </remarks>
public static class RandomCoder
{
    /// <summary>
    /// 随机数字
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义数字字符源</param>
    /// <returns>随机数字字符串</returns>
    public static string GetNumber(int? length = 6, string? source = null)
    {
        return GetSecureRandom(length ?? 6, source ?? DefaultConsts.Digits);
    }

    /// <summary>
    /// 随机特殊符号
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义特殊符号字符源</param>
    /// <returns>随机特殊符号字符串</returns>
    public static string GetSpecialChars(int? length = 6, string? source = null)
    {
        return GetSecureRandom(length ?? 6, source ?? DefaultConsts.SpecialCharactersWithoutQuotes);
    }

    /// <summary>
    /// 随机字母
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义字母字符源</param>
    /// <returns>随机字母字符串</returns>
    public static string GetLetter(int? length = 6, string? source = null)
    {
        return GetSecureRandom(length ?? 6, source ?? (DefaultConsts.UppercaseLetters + DefaultConsts.LowercaseLetters));
    }

    /// <summary>
    /// 随机大写字母
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义大写字母字符源</param>
    /// <returns>随机大写字母字符串</returns>
    public static string GetUpperLetter(int? length = 6, string? source = null)
    {
        return GetSecureRandom(length ?? 6, source?.ToUpperInvariant() ?? DefaultConsts.UppercaseLetters);
    }

    /// <summary>
    /// 随机小写字母
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义小写字母字符源</param>
    /// <returns>随机小写字母字符串</returns>
    public static string GetLowerLetter(int? length = 6, string? source = null)
    {
        return GetSecureRandom(length ?? 6, source?.ToLowerInvariant() ?? DefaultConsts.LowercaseLetters);
    }

    /// <summary>
    /// 随机字母或数字
    /// </summary>
    /// <param name="length">生成长度 默认6个字符</param>
    /// <param name="source">自定义字母或数字字符源</param>
    /// <returns>随机字母或数字字符串</returns>
    public static string GetNumberOrLetter(int? length = 6, string? source = null)
    {
        var defaultSource = DefaultConsts.Digits + DefaultConsts.UppercaseLetters + DefaultConsts.LowercaseLetters;
        return GetSecureRandom(length ?? 6, source ?? defaultSource);
    }

    /// <summary>
    /// 生成强密码(包含字母、数字和特殊字符的组合)
    /// </summary>
    /// <remarks>
    /// 使用默认字符源时保证每类字符（数字/大写/小写/特殊符号）至少出现一次，再以加密安全洗牌打散位置；
    /// 指定自定义字符源时退化为从该源均匀采样。
    /// </remarks>
    /// <param name="length">生成长度 默认12个字符</param>
    /// <param name="source">自定义强密码字符源</param>
    /// <returns>随机强密码字符串</returns>
    public static string GetStrongPassword(int? length = 12, string? source = null)
    {
        var effectiveLength = length ?? 12;
        if (source is not null)
        {
            return GetSecureRandom(effectiveLength, source);
        }

        return GetWithGuaranteedSets(
            effectiveLength,
            [DefaultConsts.Digits, DefaultConsts.UppercaseLetters, DefaultConsts.LowercaseLetters, DefaultConsts.SpecialCharactersWithoutQuotes]);
    }

    /// <summary>
    /// 生成包含指定字符类型的随机字符串
    /// </summary>
    /// <remarks>
    /// 当长度不小于所选字符类数量时，保证每个所选类型至少出现一个字符。
    /// </remarks>
    /// <param name="length">生成长度</param>
    /// <param name="includeNumbers">是否包含数字</param>
    /// <param name="includeUpperLetters">是否包含大写字母</param>
    /// <param name="includeLowerLetters">是否包含小写字母</param>
    /// <param name="includeSpecialChars">是否包含特殊字符</param>
    /// <returns>根据指定条件生成的随机字符串</returns>
    public static string GetCustom(int length = 8, bool includeNumbers = true, bool includeUpperLetters = true,
        bool includeLowerLetters = true, bool includeSpecialChars = false)
    {
        var sets = new List<string>(4);

        if (includeNumbers)
        {
            sets.Add(DefaultConsts.Digits);
        }

        if (includeUpperLetters)
        {
            sets.Add(DefaultConsts.UppercaseLetters);
        }

        if (includeLowerLetters)
        {
            sets.Add(DefaultConsts.LowercaseLetters);
        }

        if (includeSpecialChars)
        {
            sets.Add(DefaultConsts.SpecialCharactersWithoutQuotes);
        }

        // 如果没有选择任何字符集，默认使用数字和字母
        if (sets.Count == 0)
        {
            sets.Add(DefaultConsts.Digits);
            sets.Add(DefaultConsts.UppercaseLetters);
            sets.Add(DefaultConsts.LowercaseLetters);
        }

        return GetWithGuaranteedSets(length, [.. sets]);
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
            var area = RandomNumberGenerator.GetInt32(16, 88);
            var code = area == 55 ? RandomNumberGenerator.GetInt32(1, 90) : RandomNumberGenerator.GetInt32(1, 94);
            strtem.Append(Encoding.GetEncoding("GB2312").GetString([Convert.ToByte(area + 160), Convert.ToByte(code + 160)]));
        }

        return strtem.ToString();
    }

    /// <summary>
    /// 从字符源生成加密安全的随机字符串
    /// </summary>
    private static string GetSecureRandom(int length, string source)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentException.ThrowIfNullOrEmpty(source, nameof(source));

        return RandomNumberGenerator.GetString(source, length);
    }

    /// <summary>
    /// 生成保证每个字符集至少出现一次的随机字符串（长度不足以覆盖全部字符集时退化为合并源均匀采样）
    /// </summary>
    private static string GetWithGuaranteedSets(int length, string[] sets)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        var combined = string.Concat(sets);
        if (length < sets.Length)
        {
            return GetSecureRandom(length, combined);
        }

        var buffer = new char[length];
        var index = 0;

        // 每类先各取一枚，确保覆盖
        foreach (var set in sets)
        {
            buffer[index++] = set[RandomNumberGenerator.GetInt32(set.Length)];
        }

        // 其余从合并源均匀采样
        for (; index < length; index++)
        {
            buffer[index] = combined[RandomNumberGenerator.GetInt32(combined.Length)];
        }

        // 加密安全 Fisher-Yates 洗牌，避免"前 N 位固定类别"的位置泄露
        for (var i = buffer.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }

        return new string(buffer);
    }
}
