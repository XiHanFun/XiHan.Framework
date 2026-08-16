// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DevTools.CommandLine;
using XiHan.Framework.DevTools.CommandLine.Arguments;

namespace XiHan.Framework.DevTools.Tests.CommandLine;

/// <summary>
/// CommandLineParser 命令行参数解析测试
/// </summary>
public class CommandLineParserTests
{
    /// <summary>
    /// 解析纯位置参数时应按顺序存入 Arguments 且不产生任何选项
    /// </summary>
    [Fact]
    public void Parse_WithPositionalArguments_StoresArgumentsInOrder()
    {
        var parser = new CommandLineParser();

        var result = parser.Parse(["alpha", "beta", "gamma"]);

        Assert.Collection(result.Arguments,
            v => Assert.Equal("alpha", v),
            v => Assert.Equal("beta", v),
            v => Assert.Equal("gamma", v));
        Assert.Empty(result.Options);
    }

    /// <summary>
    /// 长选项后跟值时，值应绑定到该选项
    /// </summary>
    [Fact]
    public void Parse_LongOptionWithValue_BindsValue()
    {
        var parser = new CommandLineParser();

        var result = parser.Parse(["--name", "test"]);

        Assert.Equal("test", result.GetOption("name"));
    }

    /// <summary>
    /// 长选项无值时应作为布尔开关存在且值为空
    /// </summary>
    [Fact]
    public void Parse_LongOptionWithoutValue_IsBooleanSwitch()
    {
        var parser = new CommandLineParser();

        var result = parser.Parse(["--verbose"]);

        Assert.True(result.HasOption("verbose"));
        Assert.Null(result.GetOption("verbose"));
    }

    /// <summary>
    /// 短选项后跟值时，值应绑定到该选项
    /// </summary>
    [Fact]
    public void Parse_ShortOptionWithValue_BindsValue()
    {
        var parser = new CommandLineParser();

        var result = parser.Parse(["-n", "test"]);

        Assert.Equal("test", result.GetOption("n"));
    }

    /// <summary>
    /// 键值对格式（=、:，含长/短前缀）应正确提取选项与值
    /// </summary>
    /// <param name="arg">待解析参数</param>
    /// <param name="optionName">期望的选项名</param>
    /// <param name="expected">期望的值</param>
    [Theory]
    [InlineData("--name=test", "name", "test")]
    [InlineData("--name:test", "name", "test")]
    [InlineData("-n=test", "n", "test")]
    [InlineData("-n:test", "n", "test")]
    public void Parse_KeyValueFormats_ExtractsOption(string arg, string optionName, string expected)
    {
        var parser = new CommandLineParser();

        var result = parser.Parse([arg]);

        Assert.Equal(expected, result.GetOption(optionName));
    }

    /// <summary>
    /// 组合短选项应被拆分为多个独立的布尔选项
    /// </summary>
    [Fact]
    public void Parse_CombinedShortOptions_SplitsIntoBooleanOptions()
    {
        var parser = new CommandLineParser();

        var result = parser.Parse(["-abc"]);

        Assert.True(result.HasOption("a"));
        Assert.True(result.HasOption("b"));
        Assert.True(result.HasOption("c"));
    }

    /// <summary>
    /// 停止解析标记后的参数应进入 Remaining 而不是 Arguments 或 Options
    /// </summary>
    [Fact]
    public void Parse_StopParsingMarker_MovesRestToRemaining()
    {
        var parser = new CommandLineParser();

        var result = parser.Parse(["--", "foo", "bar"]);

        Assert.Empty(result.Arguments);
        Assert.Empty(result.Options);
        Assert.Collection(result.Remaining,
            v => Assert.Equal("foo", v),
            v => Assert.Equal("bar", v));
    }

    /// <summary>
    /// 同一选项重复出现时应累积全部值
    /// </summary>
    [Fact]
    public void Parse_RepeatedOption_AccumulatesValues()
    {
        var parser = new CommandLineParser();

        var result = parser.Parse(["--file", "a.txt", "--file", "b.txt"]);

        Assert.Collection(result.GetOptions("file"),
            v => Assert.Equal("a.txt", v),
            v => Assert.Equal("b.txt", v));
    }

    /// <summary>
    /// 关闭 POSIX 风格后，-abc 应作为单一短选项处理
    /// </summary>
    [Fact]
    public void Parse_PosixStyleDisabled_TreatsCombinedAsSingleOption()
    {
        var options = new ParseOptions { EnablePosixStyle = false };
        var parser = new CommandLineParser(options);

        var result = parser.Parse(["-abc"]);

        Assert.True(result.HasOption("abc"));
        Assert.False(result.HasOption("a"));
    }

    /// <summary>
    /// 选项名查找应不区分大小写
    /// </summary>
    [Fact]
    public void Parse_OptionLookup_IsCaseInsensitive()
    {
        var parser = new CommandLineParser();

        var result = parser.Parse(["--Name", "test"]);

        Assert.Equal("test", result.GetOption("name"));
        Assert.Equal("test", result.GetOption("NAME"));
    }

    /// <summary>
    /// 传入 null 参数时 TryParse 应返回 false 且产出空结果
    /// </summary>
    [Fact]
    public void TryParse_WithNullArgs_ReturnsFalse()
    {
        var parser = new CommandLineParser();

        var ok = parser.TryParse(null!, out var result);

        Assert.False(ok);
        Assert.Empty(result.Arguments);
        Assert.Empty(result.Options);
    }

    /// <summary>
    /// 空参数数组应产出空的解析结果
    /// </summary>
    [Fact]
    public void Parse_EmptyArgs_ReturnsEmptyResult()
    {
        var parser = new CommandLineParser();

        var result = parser.Parse([]);

        Assert.Empty(result.Arguments);
        Assert.Empty(result.Options);
        Assert.Empty(result.Remaining);
        Assert.Null(result.Command);
        Assert.Null(result.SubCommand);
    }
}
