// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Hosting.CommandLine.Parsing;

namespace XiHan.Framework.Hosting.CommandLine.Tests;

/// <summary>
/// 命令行记号的解析规则。
/// </summary>
public sealed class CommandLineParserTests
{
    [Fact]
    public void Parse_SplitsCommandArgumentsAndOptions()
    {
        var parsed = CommandLineParser.Parse(["orders", "create", "--name", "widget", "--json={\"a\":1}", "--tags", "a", "--tags", "b", "--flag", "--", "--literal", "last"]);

        Assert.Equal("orders", parsed.Command);
        Assert.Equal(["create", "--literal", "last"], parsed.Arguments);
        Assert.Equal("widget", parsed.Option("name").ValueOrThrow());
        Assert.Equal("{\"a\":1}", parsed.Option("json").ValueOrThrow());
        Assert.Equal(["a", "b"], parsed.OptionValues("tags"));
        Assert.Equal("b", parsed.Option("tags").ValueOrThrow());
        Assert.Equal("true", parsed.Option("flag").ValueOrThrow());
        Assert.True(parsed.HasOption("flag"));
        Assert.False(parsed.Option("missing").HasValue);
        Assert.Empty(parsed.OptionValues("missing"));
    }

    [Fact]
    public void Parse_HandlesEdgeCases()
    {
        Assert.Null(CommandLineParser.Parse([]).Command);
        Assert.Null(CommandLineParser.Parse(["--help"]).Command);
        Assert.True(CommandLineParser.Parse(["--help"]).HasOption("help"));

        // 标志后面跟选项或 -- 时仍是标志；负数与单横线记号是值
        var parsed = CommandLineParser.Parse(["invoke", "--verbose", "--page", "-5", "--dash", "-", "--last"]);
        Assert.Equal("true", parsed.Option("verbose").ValueOrThrow());
        Assert.Equal("-5", parsed.Option("page").ValueOrThrow());
        Assert.Equal("-", parsed.Option("dash").ValueOrThrow());
        Assert.Equal("true", parsed.Option("last").ValueOrThrow());
        Assert.Empty(parsed.Arguments);

        // --name= 给出空值；单独的 -- 后面的 --x 是位置参数
        var empty = CommandLineParser.Parse(["run", "--name=", "--", "--x"]);
        Assert.Equal(string.Empty, empty.Option("name").ValueOrThrow());
        Assert.Equal(["--x"], empty.Arguments);
    }
}
