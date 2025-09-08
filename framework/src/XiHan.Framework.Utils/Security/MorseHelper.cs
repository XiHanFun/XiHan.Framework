#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MorseHelper
// Guid:20f8c852-43be-43f7-b83f-59f47074f3ae
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 2:14:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;

namespace XiHan.Framework.Utils.Security;

/// <summary>
/// 摩尔斯电码编码解码辅助类
/// </summary>
/// <remarks>
/// 摩尔斯电码是一种将文本信息通过点（.）和划（-）进行编码的方法，广泛应用于电报通信和无线电通信领域。
/// </remarks>
public static class MorseHelper
{
    /// <summary>
    /// 默认字符分隔符（单个字符内的点划之间）
    /// </summary>
    public const string DefaultCharacterSeparator = " ";

    /// <summary>
    /// 默认单词分隔符（不同字符之间）
    /// </summary>
    public const string DefaultWordSeparator = " / ";

    /// <summary>
    /// 摩尔斯电码映射表（字符到摩尔斯电码）
    /// </summary>
    private static readonly Dictionary<char, string> CharToMorse = new()
    {
        // 字母 A-Z
        { 'A', ".-" }, { 'B', "-..." }, { 'C', "-.-." }, { 'D', "-.." },
        { 'E', "." }, { 'F', "..-." }, { 'G', "--." }, { 'H', "...." },
        { 'I', ".." }, { 'J', ".---" }, { 'K', "-.-" }, { 'L', ".-.." },
        { 'M', "--" }, { 'N', "-." }, { 'O', "---" }, { 'P', ".--." },
        { 'Q', "--.-" }, { 'R', ".-." }, { 'S', "..." }, { 'T', "-" },
        { 'U', "..-" }, { 'V', "...-" }, { 'W', ".--" }, { 'X', "-..-" },
        { 'Y', "-.--" }, { 'Z', "--.." },

        // 数字 0-9
        { '0', "-----" }, { '1', ".----" }, { '2', "..---" }, { '3', "...--" },
        { '4', "....-" }, { '5', "....." }, { '6', "-...." }, { '7', "--..." },
        { '8', "---.." }, { '9', "----." },

        // 标点符号
        { '.', ".-.-.-" }, { ',', "--..--" }, { '?', "..--.." }, { '\'', ".----." },
        { '!', "-.-.--" }, { '/', "-..-." }, { '(', "-.--." }, { ')', "-.--.-" },
        { '&', ".-..." }, { ':', "---..." }, { ';', "-.-.-." }, { '=', "-...-" },
        { '+', ".-.-." }, { '-', "-....-" }, { '_', "..--.-" }, { '"', ".-..-." },
        { '$', "...-..-" }, { '@', ".--.-." }, { ' ', " " }
    };

    /// <summary>
    /// 摩尔斯电码映射表（摩尔斯电码到字符）
    /// </summary>
    private static readonly Dictionary<string, char> MorseToChar;

    /// <summary>
    /// 静态构造函数，初始化反向映射表
    /// </summary>
    static MorseHelper()
    {
        MorseToChar = CharToMorse.Where(kvp => kvp.Key != ' ')
            .ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
    }

    /// <summary>
    /// 将文本编码为摩尔斯电码
    /// </summary>
    /// <param name="text">要编码的文本</param>
    /// <param name="characterSeparator">字符分隔符，默认为空格</param>
    /// <param name="wordSeparator">单词分隔符，默认为 " / "</param>
    /// <returns>编码后的摩尔斯电码</returns>
    /// <exception cref="ArgumentNullException">输入文本为空时抛出</exception>
    /// <exception cref="ArgumentException">包含不支持的字符时抛出</exception>
    public static string Encode(string text, string characterSeparator = DefaultCharacterSeparator, string wordSeparator = DefaultWordSeparator)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var result = new StringBuilder();
        var upperText = text.ToUpperInvariant();

        for (var i = 0; i < upperText.Length; i++)
        {
            var character = upperText[i];

            if (character == ' ')
            {
                // 处理空格（单词分隔符）
                if (result.Length > 0 && !result.ToString().EndsWith(wordSeparator))
                {
                    result.Append(wordSeparator);
                }
                continue;
            }

            if (!CharToMorse.TryGetValue(character, out var morseCode))
            {
                throw new ArgumentException($"不支持的字符: '{character}'", nameof(text));
            }

            // 添加摩尔斯电码
            if (result.Length > 0 && !result.ToString().EndsWith(wordSeparator))
            {
                result.Append(characterSeparator);
            }
            result.Append(morseCode);
        }

        return result.ToString().Trim();
    }

    /// <summary>
    /// 将摩尔斯电码解码为文本
    /// </summary>
    /// <param name="morseCode">要解码的摩尔斯电码</param>
    /// <param name="characterSeparator">字符分隔符，默认为空格</param>
    /// <param name="wordSeparator">单词分隔符，默认为 " / "</param>
    /// <returns>解码后的文本</returns>
    /// <exception cref="ArgumentNullException">输入摩尔斯电码为空时抛出</exception>
    /// <exception cref="ArgumentException">包含无效摩尔斯电码时抛出</exception>
    public static string Decode(string morseCode, string characterSeparator = DefaultCharacterSeparator, string wordSeparator = DefaultWordSeparator)
    {
        ArgumentNullException.ThrowIfNull(morseCode);

        if (string.IsNullOrEmpty(morseCode))
        {
            return string.Empty;
        }

        var result = new StringBuilder();

        // 先按单词分隔符分割
        var words = morseCode.Split([wordSeparator], StringSplitOptions.None);

        for (var wordIndex = 0; wordIndex < words.Length; wordIndex++)
        {
            var word = words[wordIndex].Trim();

            if (string.IsNullOrEmpty(word))
            {
                continue;
            }

            // 按字符分隔符分割每个单词
            var characters = word.Split([characterSeparator], StringSplitOptions.RemoveEmptyEntries);

            foreach (var character in characters)
            {
                var trimmedChar = character.Trim();

                if (string.IsNullOrEmpty(trimmedChar))
                {
                    continue;
                }

                if (!MorseToChar.TryGetValue(trimmedChar, out var decodedChar))
                {
                    throw new ArgumentException($"无效的摩尔斯电码: '{trimmedChar}'", nameof(morseCode));
                }

                result.Append(decodedChar);
            }

            // 在单词之间添加空格（除了最后一个单词）
            if (wordIndex < words.Length - 1)
            {
                result.Append(' ');
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// 验证字符是否支持摩尔斯电码编码
    /// </summary>
    /// <param name="character">要验证的字符</param>
    /// <returns>是否支持</returns>
    public static bool IsSupported(char character)
    {
        return CharToMorse.ContainsKey(char.ToUpperInvariant(character));
    }

    /// <summary>
    /// 验证文本是否完全支持摩尔斯电码编码
    /// </summary>
    /// <param name="text">要验证的文本</param>
    /// <returns>是否完全支持</returns>
    public static bool IsTextSupported(string text)
    {
        return string.IsNullOrEmpty(text) || text.ToUpperInvariant().All(IsSupported);
    }

    /// <summary>
    /// 验证摩尔斯电码格式是否有效
    /// </summary>
    /// <param name="morseCode">要验证的摩尔斯电码</param>
    /// <param name="characterSeparator">字符分隔符</param>
    /// <param name="wordSeparator">单词分隔符</param>
    /// <returns>是否有效</returns>
    public static bool IsMorseCodeValid(string morseCode, string characterSeparator = DefaultCharacterSeparator, string wordSeparator = DefaultWordSeparator)
    {
        if (string.IsNullOrEmpty(morseCode))
        {
            return true;
        }

        try
        {
            // 尝试解码，如果成功则格式有效
            Decode(morseCode, characterSeparator, wordSeparator);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取支持的所有字符列表
    /// </summary>
    /// <returns>支持的字符列表</returns>
    public static IEnumerable<char> GetSupportedCharacters()
    {
        return CharToMorse.Keys.Where(c => c != ' ').OrderBy(c => c);
    }

    /// <summary>
    /// 获取字符对应的摩尔斯电码
    /// </summary>
    /// <param name="character">字符</param>
    /// <returns>对应的摩尔斯电码，如果不支持则返回 null</returns>
    public static string? GetMorseCode(char character)
    {
        return CharToMorse.TryGetValue(char.ToUpperInvariant(character), out var morseCode) ? morseCode : null;
    }

    /// <summary>
    /// 获取摩尔斯电码对应的字符
    /// </summary>
    /// <param name="morseCode">摩尔斯电码</param>
    /// <returns>对应的字符，如果无效则返回 null</returns>
    public static char? GetCharacter(string morseCode)
    {
        return string.IsNullOrEmpty(morseCode) ? null : MorseToChar.TryGetValue(morseCode.Trim(), out var character) ? character : null;
    }

    /// <summary>
    /// 清理摩尔斯电码字符串（移除多余空格和格式化）
    /// </summary>
    /// <param name="morseCode">要清理的摩尔斯电码</param>
    /// <param name="characterSeparator">字符分隔符</param>
    /// <param name="wordSeparator">单词分隔符</param>
    /// <returns>清理后的摩尔斯电码</returns>
    public static string CleanMorseCode(string morseCode, string characterSeparator = DefaultCharacterSeparator, string wordSeparator = DefaultWordSeparator)
    {
        if (string.IsNullOrEmpty(morseCode))
        {
            return string.Empty;
        }

        // 规范化空格和分隔符
        var cleaned = morseCode.Trim();

        // 替换多个连续空格为单个空格
        while (cleaned.Contains("  "))
        {
            cleaned = cleaned.Replace("  ", " ");
        }

        // 标准化字符分隔符（如果不是默认的空格）
        if (characterSeparator != DefaultCharacterSeparator)
        {
            // 将标准空格分隔符替换为自定义分隔符
            var parts = cleaned.Split([wordSeparator], StringSplitOptions.None);
            for (var i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Replace(" ", characterSeparator);
            }
            cleaned = string.Join(wordSeparator, parts);
        }

        // 确保单词分隔符格式正确
        if (wordSeparator != DefaultWordSeparator)
        {
            cleaned = cleaned.Replace(DefaultWordSeparator, wordSeparator);
        }

        return cleaned;
    }

    /// <summary>
    /// 将摩尔斯电码转换为音频表示（用于调试和可视化）
    /// </summary>
    /// <param name="morseCode">摩尔斯电码</param>
    /// <param name="dotSound">点的音频表示，默认为 "dit"</param>
    /// <param name="dashSound">划的音频表示，默认为 "dah"</param>
    /// <param name="characterPause">字符间暂停表示，默认为 "pause"</param>
    /// <returns>音频表示字符串</returns>
    public static string ToAudioRepresentation(string morseCode, string dotSound = "dit", string dashSound = "dah", string characterPause = "pause")
    {
        if (string.IsNullOrEmpty(morseCode))
        {
            return string.Empty;
        }

        var result = new StringBuilder();

        for (var i = 0; i < morseCode.Length; i++)
        {
            var character = morseCode[i];

            switch (character)
            {
                case '.':
                    result.Append(dotSound);
                    break;

                case '-':
                    result.Append(dashSound);
                    break;

                case ' ':
                    result.Append(characterPause);
                    break;

                case '/':
                    result.Append(" [word break] ");
                    break;

                default:
                    continue;
            }

            // 在音频元素之间添加空格
            if (i < morseCode.Length - 1 && morseCode[i + 1] != ' ' && character != ' ')
            {
                result.Append(' ');
            }
        }

        return result.ToString().Trim();
    }

    /// <summary>
    /// 生成摩尔斯电码参考表
    /// </summary>
    /// <param name="includeNumbers">是否包含数字，默认为 true</param>
    /// <param name="includePunctuation">是否包含标点符号，默认为 true</param>
    /// <returns>格式化的参考表字符串</returns>
    public static string GenerateReferenceTable(bool includeNumbers = true, bool includePunctuation = true)
    {
        var result = new StringBuilder();
        result.AppendLine("摩尔斯电码参考表");
        result.AppendLine("==================");
        result.AppendLine();

        // 字母
        result.AppendLine("字母:");
        foreach (var kvp in CharToMorse.Where(kvp => char.IsLetter(kvp.Key)).OrderBy(kvp => kvp.Key))
        {
            result.AppendLine($"{kvp.Key} : {kvp.Value}");
        }

        if (includeNumbers)
        {
            result.AppendLine();
            result.AppendLine("数字:");
            foreach (var kvp in CharToMorse.Where(kvp => char.IsDigit(kvp.Key)).OrderBy(kvp => kvp.Key))
            {
                result.AppendLine($"{kvp.Key} : {kvp.Value}");
            }
        }

        if (includePunctuation)
        {
            result.AppendLine();
            result.AppendLine("标点符号:");
            foreach (var kvp in CharToMorse.Where(kvp => !char.IsLetterOrDigit(kvp.Key) && kvp.Key != ' ').OrderBy(kvp => kvp.Key))
            {
                result.AppendLine($"{kvp.Key} : {kvp.Value}");
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// 统计摩尔斯电码的基本信息
    /// </summary>
    /// <param name="morseCode">摩尔斯电码</param>
    /// <returns>统计信息</returns>
    public static MorseCodeStatistics GetStatistics(string morseCode)
    {
        if (string.IsNullOrEmpty(morseCode))
        {
            return new MorseCodeStatistics();
        }

        var dotCount = morseCode.Count(c => c == '.');
        var dashCount = morseCode.Count(c => c == '-');
        var characterCount = morseCode.Split([' '], StringSplitOptions.RemoveEmptyEntries)
            .Count(segment => segment != "/");
        var wordCount = morseCode.Split([" / "], StringSplitOptions.RemoveEmptyEntries).Length;

        return new MorseCodeStatistics
        {
            DotCount = dotCount,
            DashCount = dashCount,
            CharacterCount = characterCount,
            WordCount = wordCount,
            TotalLength = morseCode.Length,
            EstimatedTransmissionTime = CalculateTransmissionTime(dotCount, dashCount, characterCount, wordCount)
        };
    }

    /// <summary>
    /// 计算预估传输时间（以秒为单位，基于标准摩尔斯电码时序）
    /// </summary>
    /// <param name="dotCount">点的数量</param>
    /// <param name="dashCount">划的数量</param>
    /// <param name="characterCount">字符数量</param>
    /// <param name="wordCount">单词数量</param>
    /// <returns>预估传输时间（秒）</returns>
    private static double CalculateTransmissionTime(int dotCount, int dashCount, int characterCount, int wordCount)
    {
        // 标准摩尔斯电码时序（单位：基本时间单位）
        // 点：1个单位
        // 划：3个单位
        // 字符内间隔：1个单位
        // 字符间间隔：3个单位
        // 单词间间隔：7个单位

        const double BasicTimeUnit = 0.1; // 假设基本时间单位为0.1秒

        var totalUnits = (dotCount * 1) +           // 点
                        (dashCount * 3) +           // 划
                        ((dotCount + dashCount - characterCount) * 1) + // 字符内间隔
                        ((characterCount - wordCount) * 3) +    // 字符间间隔
                        ((wordCount - 1) * 7);                 // 单词间间隔

        return totalUnits * BasicTimeUnit;
    }
}

/// <summary>
/// 摩尔斯电码统计信息
/// </summary>
public class MorseCodeStatistics
{
    /// <summary>
    /// 点的数量
    /// </summary>
    public int DotCount { get; set; }

    /// <summary>
    /// 划的数量
    /// </summary>
    public int DashCount { get; set; }

    /// <summary>
    /// 字符数量
    /// </summary>
    public int CharacterCount { get; set; }

    /// <summary>
    /// 单词数量
    /// </summary>
    public int WordCount { get; set; }

    /// <summary>
    /// 总长度
    /// </summary>
    public int TotalLength { get; set; }

    /// <summary>
    /// 预估传输时间（秒）
    /// </summary>
    public double EstimatedTransmissionTime { get; set; }

    /// <summary>
    /// 转换为字符串表示
    /// </summary>
    /// <returns>统计信息字符串</returns>
    public override string ToString()
    {
        return $"点: {DotCount}, 划: {DashCount}, 字符: {CharacterCount}, 单词: {WordCount}, " +
               $"总长度: {TotalLength}, 预估传输时间: {EstimatedTransmissionTime:F2}秒";
    }
}
