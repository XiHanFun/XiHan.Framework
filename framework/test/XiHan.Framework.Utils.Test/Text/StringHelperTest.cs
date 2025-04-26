using XiHan.Framework.Utils.Text;

namespace XiHan.Framework.Utils.Test.Text;

public class StringHelperTest
{
    #region 分割测试

    [Fact]
    public void GetStrList_WithValidInput_ReturnsCorrectList()
    {
        // Arrange
        var sourceStr = "a,b,c,d";

        // Act
        var result = StringHelper.GetStrList(sourceStr);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal("a", result[0]);
        Assert.Equal("b", result[1]);
        Assert.Equal("c", result[2]);
        Assert.Equal("d", result[3]);
    }

    [Fact]
    public void GetStrList_WithEmptyInput_ReturnsEmptyList()
    {
        // Arrange
        var sourceStr = "";

        // Act
        var result = StringHelper.GetStrList(sourceStr);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetStrList_WithDuplicates_ReturnsDuplicatesWhenAllowed()
    {
        // Arrange
        var sourceStr = "a,b,a,c";

        // Act
        var resultWithDuplicates = StringHelper.GetStrList(sourceStr, ',', true);
        var resultWithoutDuplicates = StringHelper.GetStrList(sourceStr, ',', false);

        // Assert
        Assert.Equal(4, resultWithDuplicates.Count);
        Assert.Equal(3, resultWithoutDuplicates.Count);
    }

    [Fact]
    public void GetStrArray_WithValidInput_ReturnsCorrectArray()
    {
        // Arrange
        var sourceStr = "a,b,c,d";

        // Act
        var result = StringHelper.GetStrArray(sourceStr);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Equal("a", result[0]);
        Assert.Equal("b", result[1]);
        Assert.Equal("c", result[2]);
        Assert.Equal("d", result[3]);
    }

    [Fact]
    public void GetStrEnumerable_WithCustomSeparator_SplitsCorrectly()
    {
        // Arrange
        var sourceStr = "a|b|c|d";

        // Act
        var result = StringHelper.GetStrEnumerable(sourceStr, '|').ToList();

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal("a", result[0]);
        Assert.Equal("b", result[1]);
        Assert.Equal("c", result[2]);
        Assert.Equal("d", result[3]);
    }

    #endregion 分割测试

    #region 组装测试

    [Fact]
    public void GetListStr_WithValidList_ReturnsCorrectString()
    {
        // Arrange
        var list = new List<string> { "a", "b", "c", "d" };

        // Act
        var result = StringHelper.GetListStr(list);

        // Assert
        Assert.Equal("a,b,c,d", result);
    }

    [Fact]
    public void GetArrayStr_WithValidArray_ReturnsCorrectString()
    {
        // Arrange
        var array = new[] { "a", "b", "c", "d" };

        // Act
        var result = StringHelper.GetArrayStr(array);

        // Assert
        Assert.Equal("a,b,c,d", result);
    }

    [Fact]
    public void GetDictionaryValueStr_WithValidDictionary_ReturnsValuesAsString()
    {
        // Arrange
        var dictionary = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" },
            { "key3", "value3" }
        };

        // Act
        var result = StringHelper.GetDictionaryValueStr(dictionary);

        // Assert
        Assert.Contains("value1", result);
        Assert.Contains("value2", result);
        Assert.Contains("value3", result);
        Assert.Equal(2, result.Count(c => c == ','));
    }

    [Fact]
    public void GetEnumerableStr_WithCustomSeparator_JoinsCorrectly()
    {
        // Arrange
        var enumerable = new List<string> { "a", "b", "c", "d" };

        // Act
        var result = StringHelper.GetEnumerableStr(enumerable, '|');

        // Assert
        Assert.Equal("a|b|c|d", result);
    }

    #endregion 组装测试

    #region 转换为纯字符串测试

    [Fact]
    public void GetCleanStyle_RemovesSplitString()
    {
        // Arrange
        var sourceStr = "123-456-789";
        var splitString = "-";

        // Act
        var result = StringHelper.GetCleanStyle(sourceStr, splitString);

        // Assert
        Assert.Equal("123456789", result);
    }

    [Fact]
    public void GetCleanStyle_WithNullInput_ReturnsEmptyString()
    {
        // Arrange
        string? sourceStr = null;
        var splitString = "-";

        // Act
        var result = StringHelper.GetCleanStyle(sourceStr, splitString);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    #endregion 转换为纯字符串测试

    #region 转换为新样式测试

    [Fact]
    public void GetNewStyle_WithMatchingLengths_FormatsCorrectly()
    {
        // Arrange
        var sourceStr = "123456789";
        var newStyle = "123-456-789";
        var splitString = "-";

        // Act
        var result = StringHelper.GetNewStyle(sourceStr, newStyle, splitString, out var error);

        // Assert
        Assert.Equal("123-456-789", result);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void GetNewStyle_WithNonMatchingLengths_ReturnsErrorMessage()
    {
        // Arrange
        var sourceStr = "12345";
        var newStyle = "123-456-789";
        var splitString = "-";

        // Act
        var result = StringHelper.GetNewStyle(sourceStr, newStyle, splitString, out var error);

        // Assert
        Assert.Equal(string.Empty, result);
        Assert.NotEqual(string.Empty, error);
    }

    [Fact]
    public void GetNewStyle_WithNullInput_ReturnsErrorMessage()
    {
        // Arrange
        string? sourceStr = null;
        var newStyle = "123-456-789";
        var splitString = "-";

        // Act
        var result = StringHelper.GetNewStyle(sourceStr, newStyle, splitString, out var error);

        // Assert
        Assert.Equal(string.Empty, result);
        Assert.NotEqual(string.Empty, error);
    }

    #endregion 转换为新样式测试

    #region 字符串长度和裁剪测试

    [Fact]
    public void GetStrLength_WithAsciiChars_ReturnsCorrectLength()
    {
        // Arrange
        var input = "abcdef";

        // Act
        var result = StringHelper.GetStrLength(input);

        // Assert
        Assert.Equal(6, result);
    }

    [Fact]
    public void GetStrLength_WithMixedChars_CountsChineseAsTwo()
    {
        // Arrange
        var input = "abc中文";

        // Act
        var result = StringHelper.GetStrLength(input);

        // Assert
        Assert.Equal(7, result); // 3 (ascii) + 2*2 (chinese)
    }

    [Fact]
    public void ClipString_WithShortInput_ReturnsOriginalString()
    {
        // Arrange
        var input = "short";
        var length = 10;

        // Act
        var result = StringHelper.ClipString(input, length);

        // Assert
        Assert.Equal(input, result);
    }

    [Fact]
    public void ClipString_WithLongInput_TruncatesAndAddsEllipsis()
    {
        // Arrange
        var input = "This is a very long string that should be truncated";
        var length = 10;

        // Act
        var result = StringHelper.ClipString(input, length);

        // Assert
        Assert.EndsWith("...", result);
        Assert.True(result.Length <= length + 3); // +3 for the ellipsis
    }

    #endregion 字符串长度和裁剪测试

    #region HTML和大小写转换测试

    [Fact]
    public void HtmlToTxt_RemovesHtmlTags()
    {
        // Arrange
        var html = "<p>This is <strong>bold</strong> text</p>";

        // Act
        var result = StringHelper.HtmlToTxt(html);

        // Assert
        Assert.Equal("This is bold text", result);
    }

    [Fact]
    public void FirstToUpper_CapitalizesFirstLetter()
    {
        // Arrange
        var input = "test";

        // Act
        var result = StringHelper.FirstToUpper(input);

        // Assert
        Assert.Equal("Test", result);
    }

    [Fact]
    public void FirstToLower_LowercasesFirstLetter()
    {
        // Arrange
        var input = "Test";

        // Act
        var result = StringHelper.FirstToLower(input);

        // Assert
        Assert.Equal("test", result);
    }

    #endregion HTML和大小写转换测试

    #region 替换和格式化测试

    [Fact]
    public void FormatReplaceStr_ReplacesAllOccurrences()
    {
        // Arrange
        var content = "Hello {name}, how are you {name}?";
        var oldStr = "{name}";
        var newStr = "World";

        // Act
        var result = StringHelper.FormatReplaceStr(content, oldStr, newStr);

        // Assert
        Assert.Equal("Hello World, how are you World?", result);
    }

    #endregion 替换和格式化测试
}
