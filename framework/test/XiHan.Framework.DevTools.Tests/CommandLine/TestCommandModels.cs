// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DevTools.CommandLine.Attributes;
using XiHan.Framework.DevTools.CommandLine.Commands;

namespace XiHan.Framework.DevTools.Tests.CommandLine;

/// <summary>
/// 用于命令绑定测试的日志级别枚举
/// </summary>
public enum BindingLogLevel
{
    /// <summary>
    /// 调试级别
    /// </summary>
    Debug,

    /// <summary>
    /// 信息级别
    /// </summary>
    Info,

    /// <summary>
    /// 警告级别
    /// </summary>
    Warning,

    /// <summary>
    /// 错误级别
    /// </summary>
    Error
}

/// <summary>
/// 综合绑定测试使用的选项模型
/// </summary>
public class BindingOptions
{
    /// <summary>
    /// 名称选项
    /// </summary>
    [CommandOption("name", "n", Description = "名称")]
    public string Name { get; set; } = "";

    /// <summary>
    /// 详细输出开关
    /// </summary>
    [CommandOption("verbose", "v", Description = "详细输出", IsSwitch = true)]
    public bool Verbose { get; set; }

    /// <summary>
    /// 数量选项（带默认值）
    /// </summary>
    [CommandOption("count", "c", Description = "数量", DefaultValue = 5)]
    public int Count { get; set; }

    /// <summary>
    /// 标签多值选项
    /// </summary>
    [CommandOption("tags", "t", Description = "标签列表", AllowMultiple = true)]
    public string[] Tags { get; set; } = [];

    /// <summary>
    /// 源文件位置参数
    /// </summary>
    [CommandArgument(0, "source", Description = "源路径")]
    public string Source { get; set; } = "";

    /// <summary>
    /// 其余多值位置参数
    /// </summary>
    [CommandArgument(1, "rest", Description = "其余参数", AllowMultiple = true)]
    public string[] Rest { get; set; } = [];
}

/// <summary>
/// 枚举选项绑定测试模型
/// </summary>
public class EnumOptionModel
{
    /// <summary>
    /// 日志级别选项
    /// </summary>
    [CommandOption("level", Description = "日志级别")]
    public BindingLogLevel Level { get; set; }
}

/// <summary>
/// 必填选项绑定测试模型
/// </summary>
public class RequiredOptionModel
{
    /// <summary>
    /// 必填的名称选项
    /// </summary>
    [CommandOption("name", "n", Description = "名称", Required = true)]
    public string Name { get; set; } = "";
}

/// <summary>
/// 必填位置参数绑定测试模型
/// </summary>
public class RequiredArgumentModel
{
    /// <summary>
    /// 必填的目标位置参数
    /// </summary>
    [CommandArgument(0, "target", Description = "目标")]
    public string Target { get; set; } = "";
}

/// <summary>
/// 范围验证绑定测试模型
/// </summary>
public class RangeOptionModel
{
    /// <summary>
    /// 端口号选项（范围 1-65535）
    /// </summary>
    [CommandOption("port", "p", Description = "端口号")]
    [Range(1, 65535)]
    public int Port { get; set; }
}

/// <summary>
/// 多值 List 集合绑定测试模型
/// </summary>
public class MultiValueListModel
{
    /// <summary>
    /// 多值选项
    /// </summary>
    [CommandOption("item", Description = "多值选项", AllowMultiple = true)]
    public List<string> Items { get; set; } = [];
}

/// <summary>
/// 多值数组选项绑定测试模型
/// </summary>
public class MultiValueArrayModel
{
    /// <summary>
    /// 多值数组选项
    /// </summary>
    [CommandOption("tags", "t", Description = "标签列表", AllowMultiple = true)]
    public string[] Tags { get; set; } = [];
}

/// <summary>
/// 布尔值选项绑定测试模型
/// </summary>
public class BoolValueModel
{
    /// <summary>
    /// 启用选项
    /// </summary>
    [CommandOption("enabled", Description = "是否启用")]
    public bool Enabled { get; set; }
}

/// <summary>
/// 默认值选项绑定测试模型
/// </summary>
public class DefaultValueModel
{
    /// <summary>
    /// 数量选项（默认 42）
    /// </summary>
    [CommandOption("count", "c", Description = "数量", DefaultValue = 42)]
    public int Count { get; set; }
}

/// <summary>
/// 回显命令（实现 ICommand，用于异步执行测试）
/// </summary>
[Command("echo", Description = "回显消息")]
public class EchoCommand : ICommand
{
    /// <summary>
    /// 要回显的消息位置参数
    /// </summary>
    [CommandArgument(0, "message", Description = "要回显的消息")]
    public string Message { get; set; } = "";

    /// <summary>
    /// 执行命令：将消息写入输出流并返回 0
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>退出代码</returns>
    public Task<int> ExecuteAsync(CommandContext context)
    {
        context.Output.WriteLine(Message);
        return Task.FromResult(0);
    }
}

/// <summary>
/// 问候命令（用于描述符与帮助测试）
/// </summary>
[Command("greet", Aliases = ["hi", "hello"], Description = "问候命令", IsDefault = true, Usage = "greet <name>")]
public class GreetCommand
{
    /// <summary>
    /// 名称选项
    /// </summary>
    [CommandOption("name", "n", Description = "名称", Required = true)]
    public string Name { get; set; } = "";

    /// <summary>
    /// 目标位置参数
    /// </summary>
    [CommandArgument(0, "target", Description = "目标")]
    public string Target { get; set; } = "";

    /// <summary>
    /// 连通性测试子命令
    /// </summary>
    [SubCommand(typeof(PingSubCommand))]
    public PingSubCommand? Ping { get; set; }
}

/// <summary>
/// 连通性测试子命令
/// </summary>
[Command("ping", Description = "连通性测试")]
public class PingSubCommand
{
    /// <summary>
    /// 测试次数选项
    /// </summary>
    [CommandOption("count", "c", Description = "测试次数")]
    public int Count { get; set; }
}

/// <summary>
/// 帮助文本测试命令
/// </summary>
[Command("copy", Description = "复制文件", Usage = "copy source dest")]
public class HelpSampleCommand
{
    /// <summary>
    /// 源文件位置参数
    /// </summary>
    [CommandArgument(0, "source", Description = "源文件")]
    public string Source { get; set; } = "";

    /// <summary>
    /// 目标文件位置参数（可选）
    /// </summary>
    [CommandArgument(1, "destination", Description = "目标文件", Required = false)]
    public string Destination { get; set; } = "";

    /// <summary>
    /// 强制覆盖开关
    /// </summary>
    [CommandOption("force", "f", Description = "强制覆盖", IsSwitch = true)]
    public bool Force { get; set; }

    /// <summary>
    /// 输出目录选项
    /// </summary>
    [CommandOption("output", "o", Description = "输出目录")]
    public string Output { get; set; } = "";
}

/// <summary>
/// 隐藏命令（用于帮助隐藏测试）
/// </summary>
[Command("secret", Description = "隐藏命令", Hidden = true)]
public class HiddenSampleCommand
{
}

/// <summary>
/// 元数据选项测试模型
/// </summary>
[Command("metadata", Description = "元数据选项模型")]
public class MetadataOptionModel
{
    /// <summary>
    /// 布尔开关选项
    /// </summary>
    [CommandOption("switch", "s", Description = "开关")]
    public bool Switch { get; set; }

    /// <summary>
    /// 端口选项
    /// </summary>
    [CommandOption("port", Description = "端口")]
    public int Port { get; set; }

    /// <summary>
    /// 路径选项
    /// </summary>
    [CommandOption("path", Description = "路径")]
    public string Path { get; set; } = "";
}

/// <summary>
/// 未标记命令属性的普通类（用于异常测试）
/// </summary>
public class NotACommand
{
}
