// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DevTools.CommandLine;
using XiHan.Framework.DevTools.CommandLine.Commands;

namespace XiHan.Framework.DevTools.Tests.CommandLine;

/// <summary>
/// HelpGenerator 帮助文本生成测试
/// </summary>
public class HelpGeneratorTests
{
    /// <summary>
    /// 应用级帮助应包含名称、版本、描述与可见命令
    /// </summary>
    [Fact]
    public void GenerateHelp_ContainsAppInfoAndCommands()
    {
        var app = new CommandApp
        {
            Name = "tool",
            Description = "工具集",
            Version = "1.0.0"
        };
        app.AddCommand<GreetCommand>();

        var help = HelpGenerator.GenerateHelp(app);

        Assert.Contains("tool", help);
        Assert.Contains("版本: 1.0.0", help);
        Assert.Contains("工具集", help);
        Assert.Contains("greet", help);
        Assert.Contains("问候命令", help);
    }

    /// <summary>
    /// 命令级帮助应包含命令路径、参数、选项及其描述
    /// </summary>
    [Fact]
    public void GenerateCommandHelp_ContainsArgumentsAndOptions()
    {
        var descriptor = new CommandDescriptor(typeof(HelpSampleCommand));

        var help = HelpGenerator.GenerateCommandHelp(descriptor, "app");

        Assert.Contains("命令: copy", help);
        Assert.Contains("复制文件", help);
        Assert.Contains("<source>", help);
        Assert.Contains("源文件", help);
        Assert.Contains("[destination]", help);
        Assert.Contains("目标文件", help);
        Assert.Contains("-f, --force", help);
        Assert.Contains("强制覆盖", help);
        Assert.Contains("-o, --output <VALUE>", help);
        Assert.Contains("输出目录", help);
    }

    /// <summary>
    /// 隐藏命令不应出现在应用帮助的命令列表中
    /// </summary>
    [Fact]
    public void GenerateHelp_HiddenCommand_IsExcluded()
    {
        var app = new CommandApp { Name = "tool" };
        app.AddCommand<HiddenSampleCommand>();

        var help = HelpGenerator.GenerateHelp(app);

        Assert.DoesNotContain("secret", help);
        Assert.DoesNotContain("隐藏命令", help);
    }
}
