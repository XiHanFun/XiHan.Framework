// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DevTools.CommandLine.Commands;

namespace XiHan.Framework.DevTools.Tests.CommandLine;

/// <summary>
/// 命令描述符与属性元数据读取测试
/// </summary>
public class CommandDescriptorTests
{
    /// <summary>
    /// 描述符应读取 CommandAttribute 的名称、别名、描述与其它元数据
    /// </summary>
    [Fact]
    public void Descriptor_ReadsCommandMetadata()
    {
        var descriptor = new CommandDescriptor(typeof(GreetCommand));

        Assert.Equal("greet", descriptor.Name);
        Assert.Contains("hi", descriptor.Aliases);
        Assert.Contains("hello", descriptor.Aliases);
        Assert.Equal("问候命令", descriptor.Description);
        Assert.True(descriptor.IsDefault);
        Assert.False(descriptor.Hidden);
        Assert.Equal("greet <name>", descriptor.Usage);
        Assert.Equal(typeof(GreetCommand), descriptor.CommandType);
    }

    /// <summary>
    /// 命令名与别名匹配默认忽略大小写，显式关闭后应区分
    /// </summary>
    [Fact]
    public void Descriptor_MatchesName_ByAliasAndCase()
    {
        var descriptor = new CommandDescriptor(typeof(GreetCommand));

        Assert.True(descriptor.MatchesName("greet"));
        Assert.True(descriptor.MatchesName("GREET"));
        Assert.True(descriptor.MatchesName("hi"));
        Assert.False(descriptor.MatchesName("nope"));
        Assert.False(descriptor.MatchesName("GREET", ignoreCase: false));
    }

    /// <summary>
    /// 描述符应读取选项与位置参数的元数据
    /// </summary>
    [Fact]
    public void Descriptor_ReadsOptionsAndArguments()
    {
        var descriptor = new CommandDescriptor(typeof(GreetCommand));

        var option = Assert.Single(descriptor.Options);
        Assert.Equal("name", option.LongName);
        Assert.Equal("n", option.ShortName);
        Assert.True(option.Required);
        Assert.Equal("名称", option.Description);
        Assert.Collection(option.GetNames(),
            v => Assert.Equal("name", v),
            v => Assert.Equal("n", v));

        var arg = Assert.Single(descriptor.Arguments);
        Assert.Equal(0, arg.Position);
        Assert.Equal("target", arg.Name);
        Assert.True(arg.Required);
    }

    /// <summary>
    /// 描述符应读取子命令并拼接完整命令路径
    /// </summary>
    [Fact]
    public void Descriptor_ReadsSubCommandAndFullPath()
    {
        var descriptor = new CommandDescriptor(typeof(GreetCommand));

        var sub = Assert.Single(descriptor.SubCommands);
        Assert.Equal("ping", sub.Name);
        Assert.Equal("greet ping", sub.GetFullPath());
        Assert.Same(sub, descriptor.FindSubCommand("ping"));
        Assert.Same(sub, descriptor.FindSubCommand("PING"));
        Assert.Null(descriptor.FindSubCommand("missing"));
    }

    /// <summary>
    /// 布尔选项应自动识别为开关，数值与字符串选项应生成默认 MetaName
    /// </summary>
    [Fact]
    public void OptionDescriptor_AutoDetectsSwitchAndMetaName()
    {
        var descriptor = new CommandDescriptor(typeof(MetadataOptionModel));

        var sw = descriptor.Options.Single(o => o.LongName == "switch");
        Assert.True(sw.IsSwitch);
        Assert.Equal("", sw.MetaName);

        var port = descriptor.Options.Single(o => o.LongName == "port");
        Assert.False(port.IsSwitch);
        Assert.Equal("NUMBER", port.MetaName);

        var path = descriptor.Options.Single(o => o.LongName == "path");
        Assert.Equal("VALUE", path.MetaName);
    }

    /// <summary>
    /// 描述符应能创建命令类型的实例
    /// </summary>
    [Fact]
    public void Descriptor_CreateInstance_ReturnsCommandInstance()
    {
        var descriptor = new CommandDescriptor(typeof(GreetCommand));

        var instance = descriptor.CreateInstance();

        Assert.IsType<GreetCommand>(instance);
    }

    /// <summary>
    /// 未标记 CommandAttribute 的类型应抛出 ArgumentException
    /// </summary>
    [Fact]
    public void Descriptor_WithoutCommandAttribute_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            new CommandDescriptor(typeof(NotACommand));
        });
    }
}
