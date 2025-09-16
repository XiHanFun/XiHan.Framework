#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CommandApp
// Guid:l8m9n013-k5m2-4839-in7l-j9fd84c18145
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/16 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.Utils.CommandLine.Commands;
using XiHan.Framework.Utils.CommandLine.Models;
using XiHan.Framework.Utils.ConsoleTools;

namespace XiHan.Framework.Utils.CommandLine;

/// <summary>
/// 命令行应用程序
/// </summary>
public class CommandApp
{
    private readonly List<CommandDescriptor> _commands = [];
    private readonly ParseOptions _parseOptions;
    private CommandDescriptor? _defaultCommand;

    /// <summary>
    /// 创建命令行应用程序
    /// </summary>
    /// <param name="parseOptions">解析配置</param>
    public CommandApp(ParseOptions? parseOptions = null)
    {
        _parseOptions = parseOptions ?? new ParseOptions();

        // 自动获取程序集信息
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        Name = assembly.GetName().Name;
        Version = assembly.GetName().Version?.ToString();
    }

    /// <summary>
    /// 应用程序名称
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 应用程序描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 应用程序版本
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// 添加命令
    /// </summary>
    /// <typeparam name="T">命令类型</typeparam>
    /// <returns>命令应用程序</returns>
    public CommandApp AddCommand<T>() where T : class
    {
        return AddCommand(typeof(T));
    }

    /// <summary>
    /// 添加命令
    /// </summary>
    /// <param name="commandType">命令类型</param>
    /// <returns>命令应用程序</returns>
    public CommandApp AddCommand(Type commandType)
    {
        var descriptor = new CommandDescriptor(commandType);
        _commands.Add(descriptor);

        if (descriptor.IsDefault)
        {
            _defaultCommand = descriptor;
        }

        return this;
    }

    /// <summary>
    /// 自动发现并添加命令
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns>命令应用程序</returns>
    public CommandApp DiscoverCommands(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();

        var commandTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<Attributes.CommandAttribute>() != null)
            .Where(t => !t.IsAbstract && !t.IsInterface);

        foreach (var commandType in commandTypes)
        {
            AddCommand(commandType);
        }

        return this;
    }

    /// <summary>
    /// 运行应用程序
    /// </summary>
    /// <param name="args">命令行参数</param>
    /// <returns>退出代码</returns>
    public async Task<int> RunAsync(string[] args)
    {
        try
        {
            // 解析参数
            var parser = new CommandLineParser(_parseOptions);
            var parsedArgs = parser.Parse(args);

            // 检查内置选项
            if (CheckBuiltinOptions(parsedArgs))
            {
                return 0;
            }

            // 查找要执行的命令
            var (command, remainingArgs) = FindCommand(parsedArgs);

            if (command == null)
            {
                if (_defaultCommand != null)
                {
                    command = _defaultCommand;
                }
                else
                {
                    ShowHelp();
                    return 1;
                }
            }

            // 重新解析剩余参数
            if (remainingArgs.Count > 0)
            {
                parsedArgs = parser.Parse([.. remainingArgs]);
            }

            // 创建命令实例
            var commandInstance = command.CreateInstance();

            // 绑定参数到命令对象
            var binder = new CommandLineBinder();
            var boundCommand = binder.Bind(command.CommandType, parsedArgs);

            // 复制绑定的属性值到命令实例
            CopyProperties(boundCommand, commandInstance);

            // 创建执行上下文
            var context = new CommandContext(args, parsedArgs, command);

            // 执行命令
            return await ExecuteCommandAsync(commandInstance, context);
        }
        catch (ArgumentParseException ex)
        {
            ConsoleColorWriter.WriteError($"参数错误: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            ConsoleColorWriter.WriteError($"执行失败: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// 同步运行应用程序
    /// </summary>
    /// <param name="args">命令行参数</param>
    /// <returns>退出代码</returns>
    public int Run(string[] args)
    {
        return RunAsync(args).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 获取所有命令
    /// </summary>
    /// <returns>命令列表</returns>
    public List<CommandDescriptor> GetCommands()
    {
        return [.. _commands];
    }

    /// <summary>
    /// 获取可见命令
    /// </summary>
    /// <returns>可见命令列表</returns>
    public List<CommandDescriptor> GetVisibleCommands()
    {
        return [.. _commands.Where(cmd => !cmd.Hidden)];
    }

    /// <summary>
    /// 执行命令
    /// </summary>
    /// <param name="commandInstance">命令实例</param>
    /// <param name="context">执行上下文</param>
    /// <returns>退出代码</returns>
    private static async Task<int> ExecuteCommandAsync(object commandInstance, CommandContext context)
    {
        return commandInstance switch
        {
            ICommand asyncCommand => await asyncCommand.ExecuteAsync(context),
            ISyncCommand syncCommand => syncCommand.Execute(context),
            _ => throw new InvalidOperationException($"命令类型 {commandInstance.GetType().Name} 必须实现 ICommand 或 ISyncCommand 接口")
        };
    }

    /// <summary>
    /// 复制属性值
    /// </summary>
    /// <param name="source">源对象</param>
    /// <param name="target">目标对象</param>
    private static void CopyProperties(object source, object target)
    {
        var sourceType = source.GetType();
        var targetType = target.GetType();

        if (sourceType != targetType)
        {
            return;
        }

        var properties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);

        foreach (var property in properties)
        {
            var value = property.GetValue(source);
            property.SetValue(target, value);
        }

        var fields = sourceType.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => !f.IsInitOnly);

        foreach (var field in fields)
        {
            var value = field.GetValue(source);
            field.SetValue(target, value);
        }
    }

    /// <summary>
    /// 检查内置选项
    /// </summary>
    /// <param name="parsedArgs">解析结果</param>
    /// <returns>是否处理了内置选项</returns>
    private bool CheckBuiltinOptions(ParsedArguments parsedArgs)
    {
        // 检查帮助选项
        if (_parseOptions.AutoGenerateHelp)
        {
            foreach (var helpOption in _parseOptions.HelpOptions)
            {
                if (parsedArgs.HasOption(helpOption))
                {
                    ShowHelp();
                    return true;
                }
            }
        }

        // 检查版本选项
        if (_parseOptions.AutoGenerateVersion)
        {
            foreach (var versionOption in _parseOptions.VersionOptions)
            {
                if (parsedArgs.HasOption(versionOption))
                {
                    ShowVersion();
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 查找要执行的命令
    /// </summary>
    /// <param name="parsedArgs">解析结果</param>
    /// <returns>命令和剩余参数</returns>
    private (CommandDescriptor? command, List<string> remainingArgs) FindCommand(ParsedArguments parsedArgs)
    {
        var remainingArgs = new List<string>(parsedArgs.Arguments);

        if (remainingArgs.Count == 0)
        {
            return (_defaultCommand, remainingArgs);
        }

        var commandName = remainingArgs[0];
        var command = _commands.FirstOrDefault(cmd => cmd.MatchesName(commandName, !_parseOptions.CaseSensitive));

        if (command != null)
        {
            remainingArgs.RemoveAt(0);

            // 查找子命令
            while (remainingArgs.Count > 0 && command.SubCommands.Count > 0)
            {
                var subCommandName = remainingArgs[0];
                var subCommand = command.FindSubCommand(subCommandName, !_parseOptions.CaseSensitive);

                if (subCommand != null)
                {
                    command = subCommand;
                    remainingArgs.RemoveAt(0);
                }
                else
                {
                    break;
                }
            }
        }

        return (command, remainingArgs);
    }

    /// <summary>
    /// 显示帮助信息
    /// </summary>
    private void ShowHelp()
    {
        var helpText = HelpGenerator.GenerateHelp(this);
        Console.WriteLine(helpText);
    }

    /// <summary>
    /// 显示版本信息
    /// </summary>
    private void ShowVersion()
    {
        if (!string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(Version))
        {
            ConsoleColorWriter.WriteInfo($"{Name} v{Version}");
        }
        else if (!string.IsNullOrEmpty(Version))
        {
            ConsoleColorWriter.WriteInfo($"Version: {Version}");
        }
        else
        {
            ConsoleColorWriter.WriteWarning("版本信息不可用");
        }
    }
}

/// <summary>
/// 命令行绑定器扩展
/// </summary>
public static class CommandLineBinderExtensions
{
    /// <summary>
    /// 绑定到指定类型
    /// </summary>
    /// <param name="binder">绑定器</param>
    /// <param name="type">目标类型</param>
    /// <param name="parsedArgs">解析结果</param>
    /// <returns>绑定后的对象</returns>
    public static object Bind(this CommandLineBinder binder, Type type, ParsedArguments parsedArgs)
    {
        var method = typeof(CommandLineBinder).GetMethod("Bind")!;
        var genericMethod = method.MakeGenericMethod(type);
        return genericMethod.Invoke(binder, [parsedArgs])!;
    }
}
