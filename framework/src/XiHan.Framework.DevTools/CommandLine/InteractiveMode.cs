#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InteractiveMode
// Guid:a41f71ef-fce1-49e4-bd79-0bf0223dd7fb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/16 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.ConsoleTools;

namespace XiHan.Framework.DevTools.CommandLine;

/// <summary>
/// 交互式命令行模式
/// </summary>
public class InteractiveMode
{
    private readonly CommandApp _app;
    private readonly string _prompt;
    private bool _isRunning;

    /// <summary>
    /// 创建交互式模式
    /// </summary>
    /// <param name="app">命令应用程序</param>
    /// <param name="prompt">提示符</param>
    public InteractiveMode(CommandApp app, string prompt = "> ")
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _prompt = prompt;
    }

    /// <summary>
    /// 启动交互式模式
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>退出代码</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        _isRunning = true;

        ShowWelcome();

        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 显示提示符并读取输入
                Console.Write(_prompt);
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                // 处理内置命令
                if (HandleBuiltinCommands(input.Trim()))
                {
                    continue;
                }

                // 解析并执行用户命令
                var args = ParseInput(input);
                if (args.Length > 0)
                {
                    var exitCode = await _app.RunAsync(args);
                    if (exitCode != 0)
                    {
                        ConsoleColorWriter.WriteError($"命令执行失败，退出代码: {exitCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleColorWriter.WriteError($"执行错误: {ex.Message}");
            }

            Console.WriteLine(); // 空行分隔
        }

        ShowGoodbye();
        return 0;
    }

    /// <summary>
    /// 解析输入为参数数组
    /// </summary>
    /// <param name="input">输入字符串</param>
    /// <returns>参数数组</returns>
    private static string[] ParseInput(string input)
    {
        var args = new List<string>();
        var currentArg = new List<char>();
        var inQuotes = false;
        var escapeNext = false;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (escapeNext)
            {
                currentArg.Add(c);
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                escapeNext = true;
                continue;
            }

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (currentArg.Count > 0)
                {
                    args.Add(new string([.. currentArg]));
                    currentArg.Clear();
                }
                continue;
            }

            currentArg.Add(c);
        }

        if (currentArg.Count > 0)
        {
            args.Add(new string([.. currentArg]));
        }

        return [.. args];
    }

    /// <summary>
    /// 显示欢迎消息
    /// </summary>
    private static void ShowWelcome()
    {
        ConsoleColorWriter.WriteInfo("=== 交互式命令行模式 ===");
        ConsoleColorWriter.WriteSuccess("输入命令开始使用，输入 'help' 查看帮助，输入 'exit' 退出。");
        Console.WriteLine();
    }

    /// <summary>
    /// 显示再见消息
    /// </summary>
    private static void ShowGoodbye()
    {
        ConsoleColorWriter.WriteInfo("再见！");
    }

    /// <summary>
    /// 处理清屏命令
    /// </summary>
    /// <returns>true</returns>
    private static bool HandleClear()
    {
        Console.Clear();
        return true;
    }

    /// <summary>
    /// 处理内置命令
    /// </summary>
    /// <param name="input">输入</param>
    /// <returns>是否处理了内置命令</returns>
    private bool HandleBuiltinCommands(string input)
    {
        return input.ToLowerInvariant() switch
        {
            "exit" or "quit" or "q" => HandleExit(),
            "help" or "?" => HandleHelp(),
            "clear" or "cls" => HandleClear(),
            "commands" => HandleListCommands(),
            _ => false
        };
    }

    /// <summary>
    /// 处理退出命令
    /// </summary>
    /// <returns>true</returns>
    private bool HandleExit()
    {
        _isRunning = false;
        return true;
    }

    /// <summary>
    /// 处理帮助命令
    /// </summary>
    /// <returns>true</returns>
    private bool HandleHelp()
    {
        ConsoleColorWriter.WriteInfo("内置命令:");

        var table = new ConsoleTable("命令", "描述");
        table.AddRow("help, ?", "显示此帮助信息");
        table.AddRow("commands", "列出所有可用命令");
        table.AddRow("clear, cls", "清屏");
        table.AddRow("exit, quit, q", "退出交互模式");
        table.Print();

        Console.WriteLine();
        ConsoleColorWriter.WriteInfo("可用应用命令:");

        HelpGenerator.ShowColoredHelp(_app);

        return true;
    }

    /// <summary>
    /// 处理列出命令
    /// </summary>
    /// <returns>true</returns>
    private bool HandleListCommands()
    {
        var commands = _app.GetVisibleCommands();
        if (commands.Count == 0)
        {
            ConsoleColorWriter.WriteWarn("没有可用的命令。");
            return true;
        }

        ConsoleColorWriter.WriteInfo("可用命令:");

        var table = new ConsoleTable("命令", "描述");
        foreach (var command in commands.OrderBy(c => c.Name))
        {
            table.AddRow(command.Name, command.Description ?? "");
        }
        table.Print();

        return true;
    }
}
