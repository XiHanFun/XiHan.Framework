#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HelpGenerator
// Guid:15e2fad3-e76c-40f8-a560-f57ce3241e0c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/16 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using XiHan.Framework.DevTools.CommandLine.Commands;
using XiHan.Framework.Utils.ConsoleTools;

namespace XiHan.Framework.DevTools.CommandLine;

/// <summary>
/// 帮助文档生成器
/// </summary>
public class HelpGenerator
{
    /// <summary>
    /// 生成应用程序帮助
    /// </summary>
    /// <param name="app">命令应用程序</param>
    /// <returns>帮助文档</returns>
    public static string GenerateHelp(CommandApp app)
    {
        var sb = new StringBuilder();

        // 应用程序标题
        if (!string.IsNullOrEmpty(app.Name))
        {
            sb.AppendLine($"{app.Name}");
            if (!string.IsNullOrEmpty(app.Version))
            {
                sb.AppendLine($"版本: {app.Version}");
            }
            sb.AppendLine();
        }

        // 应用程序描述
        if (!string.IsNullOrEmpty(app.Description))
        {
            sb.AppendLine(app.Description);
            sb.AppendLine();
        }

        // 使用方法
        sb.AppendLine("使用方法:");
        var appName = app.Name ?? "app";
        sb.AppendLine($"  {appName} [command] [options]");
        sb.AppendLine();

        // 命令列表
        var visibleCommands = app.GetVisibleCommands();
        if (visibleCommands.Count > 0)
        {
            sb.AppendLine("命令:");

            var maxNameLength = visibleCommands.Max(cmd => cmd.Name.Length);
            foreach (var command in visibleCommands.OrderBy(cmd => cmd.Name))
            {
                var name = command.Name.PadRight(maxNameLength);
                var description = command.Description ?? "";
                sb.AppendLine($"  {name}  {description}");
            }
            sb.AppendLine();
        }

        // 全局选项
        sb.AppendLine("选项:");
        sb.AppendLine("  -h, --help     显示帮助信息");
        sb.AppendLine("  -v, --version  显示版本信息");
        sb.AppendLine();

        // 获取更多帮助
        sb.AppendLine("使用 '--help' 获取特定命令的帮助信息。");

        return sb.ToString();
    }

    /// <summary>
    /// 生成命令帮助
    /// </summary>
    /// <param name="command">命令描述符</param>
    /// <param name="appName">应用程序名称</param>
    /// <returns>帮助文档</returns>
    public static string GenerateCommandHelp(CommandDescriptor command, string? appName = null)
    {
        var sb = new StringBuilder();
        appName ??= "app";

        // 命令标题
        sb.AppendLine($"命令: {command.GetFullPath()}");
        if (!string.IsNullOrEmpty(command.Description))
        {
            sb.AppendLine(command.Description);
        }
        sb.AppendLine();

        // 使用方法
        sb.AppendLine("使用方法:");
        var usage = GenerateUsageLine(command, appName);
        sb.AppendLine($"  {usage}");
        sb.AppendLine();

        // 参数
        if (command.Arguments.Count > 0)
        {
            sb.AppendLine("参数:");
            foreach (var arg in command.Arguments.OrderBy(a => a.Position))
            {
                var argName = arg.Name;
                if (!arg.Required)
                {
                    argName = $"[{argName}]";
                }
                if (arg.AllowMultiple)
                {
                    argName += "...";
                }

                var description = arg.Description ?? "";
                sb.AppendLine($"  {argName.PadRight(20)}  {description}");
            }
            sb.AppendLine();
        }

        // 选项
        if (command.Options.Count > 0)
        {
            sb.AppendLine("选项:");

            var optionLines = new List<string>();
            foreach (var option in command.Options.OrderBy(o => o.LongName))
            {
                var names = new List<string>();

                if (!string.IsNullOrEmpty(option.ShortName))
                {
                    names.Add($"-{option.ShortName}");
                }
                names.Add($"--{option.LongName}");

                var nameStr = string.Join(", ", names);
                if (!option.IsSwitch && !string.IsNullOrEmpty(option.MetaName))
                {
                    nameStr += $" <{option.MetaName}>";
                }

                var description = option.Description ?? "";
                if (option.Required)
                {
                    description += " (必填)";
                }
                if (option.DefaultValue != null)
                {
                    description += $" (默认: {option.DefaultValue})";
                }

                optionLines.Add($"  {nameStr,-25}  {description}");
            }

            foreach (var line in optionLines)
            {
                sb.AppendLine(line);
            }
            sb.AppendLine();
        }

        // 子命令
        var visibleSubCommands = command.GetVisibleSubCommands();
        if (visibleSubCommands.Count > 0)
        {
            sb.AppendLine("子命令:");

            var maxNameLength = visibleSubCommands.Max(cmd => cmd.Name.Length);
            foreach (var subCommand in visibleSubCommands.OrderBy(cmd => cmd.Name))
            {
                var name = subCommand.Name.PadRight(maxNameLength);
                var description = subCommand.Description ?? "";
                sb.AppendLine($"  {name}  {description}");
            }
            sb.AppendLine();
        }

        // 示例
        if (!string.IsNullOrEmpty(command.Usage))
        {
            sb.AppendLine("示例:");
            var examples = command.Usage.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var example in examples)
            {
                sb.AppendLine($"  {appName} {example.Trim()}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 生成彩色帮助文档
    /// </summary>
    /// <param name="app">命令应用程序</param>
    public static void ShowColoredHelp(CommandApp app)
    {
        // 应用程序标题
        if (!string.IsNullOrEmpty(app.Name))
        {
            ConsoleColorWriter.WriteColoredMessage(app.Name, ConsoleColor.Cyan);
            if (!string.IsNullOrEmpty(app.Version))
            {
                ConsoleColorWriter.WriteColoredMessage($"版本: {app.Version}", ConsoleColor.Gray);
            }
            Console.WriteLine();
        }

        // 应用程序描述
        if (!string.IsNullOrEmpty(app.Description))
        {
            Console.WriteLine(app.Description);
            Console.WriteLine();
        }

        // 使用方法
        ConsoleColorWriter.WriteColoredMessage("使用方法:", ConsoleColor.Yellow);
        var appName = app.Name ?? "app";
        Console.WriteLine($"  {appName} [command] [options]");
        Console.WriteLine();

        // 命令列表
        var visibleCommands = app.GetVisibleCommands();
        if (visibleCommands.Count > 0)
        {
            ConsoleColorWriter.WriteColoredMessage("命令:", ConsoleColor.Yellow);

            // 使用 ConsoleTable 显示命令
            var table = new ConsoleTable("命令", "描述");
            foreach (var command in visibleCommands.OrderBy(cmd => cmd.Name))
            {
                table.AddRow(command.Name, command.Description ?? "");
            }
            table.Print();
            Console.WriteLine();
        }

        // 全局选项
        ConsoleColorWriter.WriteColoredMessage("选项:", ConsoleColor.Yellow);
        var optionTable = new ConsoleTable("选项", "描述");
        optionTable.AddRow("-h, --help", "显示帮助信息");
        optionTable.AddRow("-v, --version", "显示版本信息");
        optionTable.Print();
        Console.WriteLine();

        // 获取更多帮助
        ConsoleColorWriter.WriteInfo("使用 '--help' 获取特定命令的帮助信息。");
    }

    /// <summary>
    /// 显示彩色命令帮助
    /// </summary>
    /// <param name="command">命令描述符</param>
    /// <param name="appName">应用程序名称</param>
    public static void ShowColoredCommandHelp(CommandDescriptor command, string? appName = null)
    {
        appName ??= "app";

        // 命令标题
        ConsoleColorWriter.WriteColoredMessage($"命令: {command.GetFullPath()}", ConsoleColor.Cyan);
        if (!string.IsNullOrEmpty(command.Description))
        {
            Console.WriteLine(command.Description);
        }
        Console.WriteLine();

        // 使用方法
        ConsoleColorWriter.WriteColoredMessage("使用方法:", ConsoleColor.Yellow);
        var usage = GenerateUsageLine(command, appName);
        Console.WriteLine($"  {usage}");
        Console.WriteLine();

        // 参数
        if (command.Arguments.Count > 0)
        {
            ConsoleColorWriter.WriteColoredMessage("参数:", ConsoleColor.Yellow);
            var argTable = new ConsoleTable("参数", "描述");

            foreach (var arg in command.Arguments.OrderBy(a => a.Position))
            {
                var argName = arg.Name;
                if (!arg.Required)
                {
                    argName = $"[{argName}]";
                }
                if (arg.AllowMultiple)
                {
                    argName += "...";
                }

                var description = arg.Description ?? "";
                if (arg.Required)
                {
                    description += " (必填)";
                }

                argTable.AddRow(argName, description);
            }

            argTable.Print();
            Console.WriteLine();
        }

        // 选项
        if (command.Options.Count > 0)
        {
            ConsoleColorWriter.WriteColoredMessage("选项:", ConsoleColor.Yellow);
            var optionTable = new ConsoleTable("选项", "描述");

            foreach (var option in command.Options.OrderBy(o => o.LongName))
            {
                var names = new List<string>();

                if (!string.IsNullOrEmpty(option.ShortName))
                {
                    names.Add($"-{option.ShortName}");
                }
                names.Add($"--{option.LongName}");

                var nameStr = string.Join(", ", names);
                if (!option.IsSwitch && !string.IsNullOrEmpty(option.MetaName))
                {
                    nameStr += $" <{option.MetaName}>";
                }

                var description = option.Description ?? "";
                if (option.Required)
                {
                    description += " (必填)";
                }
                if (option.DefaultValue != null)
                {
                    description += $" (默认: {option.DefaultValue})";
                }

                optionTable.AddRow(nameStr, description);
            }

            optionTable.Print();
            Console.WriteLine();
        }

        // 子命令
        var visibleSubCommands = command.GetVisibleSubCommands();
        if (visibleSubCommands.Count > 0)
        {
            ConsoleColorWriter.WriteColoredMessage("子命令:", ConsoleColor.Yellow);
            var subCommandTable = new ConsoleTable("子命令", "描述");

            foreach (var subCommand in visibleSubCommands.OrderBy(cmd => cmd.Name))
            {
                subCommandTable.AddRow(subCommand.Name, subCommand.Description ?? "");
            }

            subCommandTable.Print();
            Console.WriteLine();
        }

        // 示例
        if (!string.IsNullOrEmpty(command.Usage))
        {
            ConsoleColorWriter.WriteColoredMessage("示例:", ConsoleColor.Yellow);
            var examples = command.Usage.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var example in examples)
            {
                ConsoleColorWriter.WriteColoredMessage($"  {appName} ", ConsoleColor.Gray, false);
                ConsoleColorWriter.WriteColoredMessage(example.Trim(), ConsoleColor.White);
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// 生成使用行
    /// </summary>
    /// <param name="command">命令描述符</param>
    /// <param name="appName">应用程序名称</param>
    /// <returns>使用行</returns>
    private static string GenerateUsageLine(CommandDescriptor command, string appName)
    {
        var parts = new List<string> { appName };

        // 添加命令路径
        var commandPath = command.GetFullPath();
        if (!string.IsNullOrEmpty(commandPath) && commandPath != appName)
        {
            parts.Add(commandPath);
        }

        // 添加参数
        foreach (var arg in command.Arguments.OrderBy(a => a.Position))
        {
            var argStr = arg.Required ? $"<{arg.Name}>" : $"[{arg.Name}]";
            if (arg.AllowMultiple)
            {
                argStr += "...";
            }
            parts.Add(argStr);
        }

        // 添加选项占位符
        if (command.Options.Count > 0)
        {
            parts.Add("[options]");
        }

        // 添加子命令占位符
        if (command.SubCommands.Count > 0)
        {
            parts.Add("[subcommand]");
        }

        return string.Join(" ", parts);
    }
}
