// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DevTools.CommandLine;
using XiHan.Framework.DevTools.CommandLine.Arguments;
using XiHan.Framework.DevTools.CommandLine.Commands;

namespace XiHan.Framework.DevTools.Tests.CommandLine;

/// <summary>
/// CommandLineBinder 与 CommandLineParserFactory 参数绑定测试
/// </summary>
public class CommandLineBinderTests
{
    /// <summary>
    /// 综合绑定：选项、开关、默认值、位置参数与多值参数应正确映射
    /// </summary>
    [Fact]
    public void Bind_FullOptionsAndPositionalArguments_MapsAllMembers()
    {
        var parsed = CommandLineParserFactory.Parse(["--name", "hello", "input.txt", "x", "y", "--verbose"]);

        var model = CommandLineBinder.Bind<BindingOptions>(parsed);

        Assert.Equal("hello", model.Name);
        Assert.True(model.Verbose);
        Assert.Equal(5, model.Count);
        Assert.Empty(model.Tags);
        Assert.Equal("input.txt", model.Source);
        Assert.Equal(2, model.Rest.Length);
        Assert.Equal("x", model.Rest[0]);
        Assert.Equal("y", model.Rest[1]);
    }

    /// <summary>
    /// 枚举选项值应被转换为对应枚举成员
    /// </summary>
    [Fact]
    public void Bind_EnumOptionValue_ConvertsToEnum()
    {
        var parsed = CommandLineParserFactory.Parse(["--level", "Warning"]);

        var model = CommandLineBinder.Bind<EnumOptionModel>(parsed);

        Assert.Equal(BindingLogLevel.Warning, model.Level);
    }

    /// <summary>
    /// 缺少必填选项时应抛出 ArgumentParseException
    /// </summary>
    [Fact]
    public void Bind_MissingRequiredOption_ThrowsArgumentParseException()
    {
        var parsed = CommandLineParserFactory.Parse([]);

        var ex = Assert.Throws<ArgumentParseException>(() =>
        {
            CommandLineBinder.Bind<RequiredOptionModel>(parsed);
        });

        Assert.Equal("name", ex.ArgumentName);
    }

    /// <summary>
    /// 缺少必填位置参数时应抛出 ArgumentParseException
    /// </summary>
    [Fact]
    public void Bind_MissingRequiredArgument_ThrowsArgumentParseException()
    {
        var parsed = CommandLineParserFactory.Parse([]);

        Assert.Throws<ArgumentParseException>(() =>
        {
            CommandLineBinder.Bind<RequiredArgumentModel>(parsed);
        });
    }

    /// <summary>
    /// 超出范围验证的选项应抛出 ArgumentParseException
    /// </summary>
    [Fact]
    public void Bind_RangeValidationViolation_ThrowsArgumentParseException()
    {
        var parsed = CommandLineParserFactory.Parse(["--port", "0"]);

        Assert.Throws<ArgumentParseException>(() =>
        {
            CommandLineBinder.Bind<RangeOptionModel>(parsed);
        });
    }

    /// <summary>
    /// 位于范围内的选项值应通过范围验证
    /// </summary>
    [Fact]
    public void Bind_RangeValidationInRange_Passes()
    {
        var parsed = CommandLineParserFactory.Parse(["--port", "8080"]);

        var model = CommandLineBinder.Bind<RangeOptionModel>(parsed);

        Assert.Equal(8080, model.Port);
    }

    /// <summary>
    /// 多值选项应绑定为 List 集合
    /// </summary>
    [Fact]
    public void Bind_MultiValueOption_ToGenericList()
    {
        var parsed = CommandLineParserFactory.Parse(["--item", "a", "--item", "b", "--item", "c"]);

        var model = CommandLineBinder.Bind<MultiValueListModel>(parsed);

        Assert.Collection(model.Items,
            v => Assert.Equal("a", v),
            v => Assert.Equal("b", v),
            v => Assert.Equal("c", v));
    }

    /// <summary>
    /// 多值选项用逗号分隔时应拆分为数组
    /// </summary>
    [Fact]
    public void Bind_MultiValueArray_WithSeparator_SplitsValues()
    {
        var parsed = CommandLineParserFactory.Parse(["--tags", "a,b,c"]);

        var model = CommandLineBinder.Bind<MultiValueArrayModel>(parsed);

        Assert.Equal(3, model.Tags.Length);
        Assert.Equal("a", model.Tags[0]);
        Assert.Equal("b", model.Tags[1]);
        Assert.Equal("c", model.Tags[2]);
    }

    /// <summary>
    /// 布尔选项值应按常见字面量转换
    /// </summary>
    /// <param name="input">输入值</param>
    /// <param name="expected">期望的布尔结果</param>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    public void Bind_BooleanValue_IsConverted(string input, bool expected)
    {
        var parsed = CommandLineParserFactory.Parse(["--enabled", input]);

        var model = CommandLineBinder.Bind<BoolValueModel>(parsed);

        Assert.Equal(expected, model.Enabled);
    }

    /// <summary>
    /// 未提供选项值时应应用默认值
    /// </summary>
    [Fact]
    public void Bind_OptionWithoutValue_AppliesDefault()
    {
        var parsed = CommandLineParserFactory.Parse([]);

        var model = CommandLineBinder.Bind<DefaultValueModel>(parsed);

        Assert.Equal(42, model.Count);
    }

    /// <summary>
    /// 工厂的强类型 Parse 应直接返回绑定后的对象
    /// </summary>
    [Fact]
    public void Factory_ParseTyped_ReturnsBoundObject()
    {
        var model = CommandLineParserFactory.Parse<DefaultValueModel>(["--count", "7"]);

        Assert.Equal(7, model.Count);
    }

    /// <summary>
    /// 工厂的强类型 TryParse 成功时应返回 true 与绑定对象
    /// </summary>
    [Fact]
    public void Factory_TryParseTyped_ReturnsTrueAndObject()
    {
        var ok = CommandLineParserFactory.TryParse<DefaultValueModel>(["--count", "7"], out var model);

        Assert.True(ok);
        Assert.Equal(7, model.Count);
    }

    /// <summary>
    /// 通过 Type 重载绑定到命令实例并读取位置参数
    /// </summary>
    [Fact]
    public void Binder_BindByType_ToCommandInstance()
    {
        var parsed = CommandLineParserFactory.Parse(["hello world"]);

        var instance = (EchoCommand)new CommandLineBinder().Bind(typeof(EchoCommand), parsed);

        Assert.Equal("hello world", instance.Message);
    }

    /// <summary>
    /// 命令的异步执行应写入消息并返回退出代码
    /// </summary>
    [Fact]
    public async Task Command_ExecuteAsync_WritesMessageAndReturnsCode()
    {
        var parsed = CommandLineParserFactory.Parse(["你好"]);
        var command = CommandLineBinder.Bind<EchoCommand>(parsed);
        var descriptor = new CommandDescriptor(typeof(EchoCommand));
        using var writer = new StringWriter();
        var context = new CommandContext(["你好"], parsed, descriptor, output: writer, cancellationToken: TestContext.Current.CancellationToken);

        var exitCode = await command.ExecuteAsync(context);

        Assert.Equal(0, exitCode);
        Assert.Contains("你好", writer.ToString());
    }
}
