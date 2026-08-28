// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Security;

namespace XiHan.Framework.Utils.Tests.Security;

/// <summary>
/// 摩尔斯电码辅助类测试
/// </summary>
/// <remarks>
/// 码表本身是国际标准，字母、数字与常用标点的编码全部锁死；
/// 编解码往返是大小写归一化的（编码前统一转大写），所以往返断言的期望值也必须是大写。
/// </remarks>
public class MorseHelperTests
{
    /// <summary>
    /// 分隔符常量是对外契约的一部分，不能悄悄改
    /// </summary>
    [Fact]
    public void Separators_HaveDocumentedDefaults()
    {
        Assert.Equal(" ", MorseHelper.DefaultCharacterSeparator);
        Assert.Equal(" / ", MorseHelper.DefaultWordSeparator);
    }

    /// <summary>
    /// 字母编码遵循国际摩尔斯码表
    /// </summary>
    [Theory]
    [InlineData('A', ".-")]
    [InlineData('E', ".")]
    [InlineData('S', "...")]
    [InlineData('O', "---")]
    [InlineData('T', "-")]
    [InlineData('Z', "--..")]
    public void GetMorseCode_ForLetters_MatchesStandardTable(char character, string expected)
    {
        Assert.Equal(expected, MorseHelper.GetMorseCode(character));
    }

    /// <summary>
    /// 数字编码遵循国际摩尔斯码表
    /// </summary>
    [Theory]
    [InlineData('0', "-----")]
    [InlineData('1', ".----")]
    [InlineData('5', ".....")]
    [InlineData('9', "----.")]
    public void GetMorseCode_ForDigits_MatchesStandardTable(char character, string expected)
    {
        Assert.Equal(expected, MorseHelper.GetMorseCode(character));
    }

    /// <summary>
    /// 常用标点编码遵循国际摩尔斯码表
    /// </summary>
    [Theory]
    [InlineData('.', ".-.-.-")]
    [InlineData(',', "--..--")]
    [InlineData('?', "..--..")]
    [InlineData('@', ".--.-.")]
    [InlineData('/', "-..-.")]
    public void GetMorseCode_ForPunctuation_MatchesStandardTable(char character, string expected)
    {
        Assert.Equal(expected, MorseHelper.GetMorseCode(character));
    }

    /// <summary>
    /// 小写字母按大写查表
    /// </summary>
    [Fact]
    public void GetMorseCode_IsCaseInsensitive()
    {
        Assert.Equal(MorseHelper.GetMorseCode('A'), MorseHelper.GetMorseCode('a'));
    }

    /// <summary>
    /// 不支持的字符返回 null
    /// </summary>
    [Fact]
    public void GetMorseCode_ForUnsupportedCharacter_ReturnsNull()
    {
        Assert.Null(MorseHelper.GetMorseCode('中'));
    }

    /// <summary>
    /// 反查：电码转字符
    /// </summary>
    [Fact]
    public void GetCharacter_ResolvesKnownCodes()
    {
        Assert.Equal('S', MorseHelper.GetCharacter("..."));
        Assert.Equal('O', MorseHelper.GetCharacter("---"));
        Assert.Null(MorseHelper.GetCharacter("......"));
        Assert.Null(MorseHelper.GetCharacter(string.Empty));
    }

    /// <summary>
    /// 单词内字符用空格分隔
    /// </summary>
    [Fact]
    public void Encode_JoinsCharactersWithCharacterSeparator()
    {
        Assert.Equal("... --- ...", MorseHelper.Encode("SOS"));
    }

    /// <summary>
    /// 单词之间用 " / " 分隔
    /// </summary>
    [Fact]
    public void Encode_JoinsWordsWithWordSeparator()
    {
        Assert.Equal("... --- ... / ... --- ...", MorseHelper.Encode("SOS SOS"));
    }

    /// <summary>
    /// 编码前统一转大写
    /// </summary>
    [Fact]
    public void Encode_UpperCasesInputFirst()
    {
        Assert.Equal(MorseHelper.Encode("SOS"), MorseHelper.Encode("sos"));
    }

    /// <summary>
    /// 空串编码为空串
    /// </summary>
    [Fact]
    public void Encode_WithEmptyText_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, MorseHelper.Encode(string.Empty));
    }

    /// <summary>
    /// 入参为 null 时抛空引用参数异常
    /// </summary>
    [Fact]
    public void Encode_WhenTextNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => { _ = MorseHelper.Encode(null!); });
        Assert.Throws<ArgumentNullException>(() => { _ = MorseHelper.Decode(null!); });
    }

    /// <summary>
    /// 含不支持字符时抛参数异常并点名该字符
    /// </summary>
    [Fact]
    public void Encode_WithUnsupportedCharacter_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => { _ = MorseHelper.Encode("中文"); });

        Assert.Contains("不支持的字符", exception.Message);
    }

    /// <summary>
    /// 解码非法电码时抛参数异常
    /// </summary>
    [Fact]
    public void Decode_WithInvalidCode_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => { _ = MorseHelper.Decode("........"); });

        Assert.Contains("无效的摩尔斯电码", exception.Message);
    }

    /// <summary>
    /// 空串解码为空串
    /// </summary>
    [Fact]
    public void Decode_WithEmptyCode_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, MorseHelper.Decode(string.Empty));
    }

    /// <summary>
    /// 编解码往返（大小写归一化为大写）
    /// </summary>
    [Theory]
    [InlineData("SOS")]
    [InlineData("HI THERE")]
    [InlineData("HELLO WORLD")]
    [InlineData("ABC 123")]
    [InlineData("WHAT?")]
    public void EncodeDecode_RoundTrips(string text)
    {
        Assert.Equal(text, MorseHelper.Decode(MorseHelper.Encode(text)));
    }

    /// <summary>
    /// 自定义分隔符的编解码往返
    /// </summary>
    [Fact]
    public void EncodeDecode_WithCustomSeparators_RoundTrips()
    {
        const string CharacterSeparator = "|";
        const string WordSeparator = " // ";

        var encoded = MorseHelper.Encode("SOS SOS", CharacterSeparator, WordSeparator);

        Assert.Equal("...|---|... // ...|---|...", encoded);
        Assert.Equal("SOS SOS", MorseHelper.Decode(encoded, CharacterSeparator, WordSeparator));
    }

    /// <summary>
    /// 字符支持性判定
    /// </summary>
    [Theory]
    [InlineData('a', true)]
    [InlineData('Z', true)]
    [InlineData('7', true)]
    [InlineData('@', true)]
    [InlineData(' ', true)]
    [InlineData('中', false)]
    [InlineData('~', false)]
    public void IsSupported_ClassifiesCharacters(char character, bool expected)
    {
        Assert.Equal(expected, MorseHelper.IsSupported(character));
    }

    /// <summary>
    /// 整段文本支持性判定
    /// </summary>
    [Theory]
    [InlineData("HELLO WORLD", true)]
    [InlineData("hello, world!", true)]
    [InlineData("", true)]
    [InlineData("你好", false)]
    public void IsTextSupported_ClassifiesText(string text, bool expected)
    {
        Assert.Equal(expected, MorseHelper.IsTextSupported(text));
    }

    /// <summary>
    /// 电码格式有效性判定
    /// </summary>
    [Theory]
    [InlineData("... --- ...", true)]
    [InlineData("... --- ... / ... --- ...", true)]
    [InlineData("", true)]
    [InlineData("........", false)]
    public void IsMorseCodeValid_ClassifiesCode(string morseCode, bool expected)
    {
        Assert.Equal(expected, MorseHelper.IsMorseCodeValid(morseCode));
    }

    /// <summary>
    /// 支持的字符集不含空格，且按序输出
    /// </summary>
    /// <remarks>
    /// 26 个字母 + 10 个数字 + 18 个标点 = 54 个；空格虽然在内部映射表里，但不属于「可编码字符」对外暴露。
    /// </remarks>
    [Fact]
    public void GetSupportedCharacters_ExcludesSpaceAndIsSorted()
    {
        var characters = MorseHelper.GetSupportedCharacters().ToList();

        Assert.Equal(54, characters.Count);
        Assert.DoesNotContain(' ', characters);
        Assert.Equal([.. characters.OrderBy(c => c)], characters);
        Assert.All(characters, c => Assert.NotNull(MorseHelper.GetMorseCode(c)));
    }

    /// <summary>
    /// 清理多余空格，保留标准分隔形态
    /// </summary>
    [Fact]
    public void CleanMorseCode_CollapsesRedundantSpaces()
    {
        Assert.Equal("... ---", MorseHelper.CleanMorseCode("  ...   ---  "));
        Assert.Equal(string.Empty, MorseHelper.CleanMorseCode(string.Empty));
    }

    /// <summary>
    /// 音频表示逐符号展开点划
    /// </summary>
    [Fact]
    public void ToAudioRepresentation_MapsDotsAndDashes()
    {
        Assert.Equal("dit dah", MorseHelper.ToAudioRepresentation(".-"));
        Assert.Equal("di da", MorseHelper.ToAudioRepresentation(".-", "di", "da"));
        Assert.Equal(string.Empty, MorseHelper.ToAudioRepresentation(string.Empty));
    }

    /// <summary>
    /// 统计信息覆盖点划数、字符数、单词数与预估时长
    /// </summary>
    /// <remarks>
    /// 预估时长按标准时序推导：点 1 单位、划 3 单位、字符内间隔 1 单位、字符间间隔 3 单位、
    /// 单词间间隔 7 单位，基本时间单位 0.1 秒。"... --- ..." 即 (6 + 9 + 6 + 6 + 0) × 0.1 = 2.7 秒。
    /// </remarks>
    [Fact]
    public void GetStatistics_CountsSymbolsAndEstimatesDuration()
    {
        var statistics = MorseHelper.GetStatistics("... --- ...");

        Assert.Equal(6, statistics.DotCount);
        Assert.Equal(3, statistics.DashCount);
        Assert.Equal(3, statistics.CharacterCount);
        Assert.Equal(1, statistics.WordCount);
        Assert.Equal(11, statistics.TotalLength);
        Assert.True(Math.Abs(2.7 - statistics.EstimatedTransmissionTime) < 1e-9);
    }

    /// <summary>
    /// 空电码的统计信息全为零
    /// </summary>
    [Fact]
    public void GetStatistics_WithEmptyCode_ReturnsZeroedStatistics()
    {
        var statistics = MorseHelper.GetStatistics(string.Empty);

        Assert.Equal(0, statistics.DotCount);
        Assert.Equal(0, statistics.DashCount);
        Assert.Equal(0, statistics.CharacterCount);
        Assert.Equal(0, statistics.WordCount);
        Assert.Equal(0, statistics.TotalLength);
        Assert.Equal(0d, statistics.EstimatedTransmissionTime);
    }

    /// <summary>
    /// 统计信息的字符串表示包含各项数值
    /// </summary>
    [Fact]
    public void MorseCodeStatistics_ToString_ContainsAllCounters()
    {
        var text = new MorseCodeStatistics
        {
            DotCount = 6,
            DashCount = 3,
            CharacterCount = 3,
            WordCount = 1,
            TotalLength = 11,
            EstimatedTransmissionTime = 2.7
        }.ToString();

        Assert.Contains("点: 6", text);
        Assert.Contains("划: 3", text);
        Assert.Contains("字符: 3", text);
        Assert.Contains("单词: 1", text);
        Assert.Contains("总长度: 11", text);

        // 时长用 F2 格式化，小数分隔符随区域设置变化，这里只断言字段存在而不锁具体数字串
        Assert.Contains("预估传输时间", text);
    }

    /// <summary>
    /// 参考表按开关包含数字与标点分区
    /// </summary>
    [Fact]
    public void GenerateReferenceTable_RespectsSectionSwitches()
    {
        var full = MorseHelper.GenerateReferenceTable();
        var lettersOnly = MorseHelper.GenerateReferenceTable(includeNumbers: false, includePunctuation: false);

        Assert.Contains("字母:", full);
        Assert.Contains("数字:", full);
        Assert.Contains("标点符号:", full);
        Assert.Contains("A : .-", full);

        Assert.Contains("字母:", lettersOnly);
        Assert.DoesNotContain("数字:", lettersOnly);
        Assert.DoesNotContain("标点符号:", lettersOnly);
    }
}
