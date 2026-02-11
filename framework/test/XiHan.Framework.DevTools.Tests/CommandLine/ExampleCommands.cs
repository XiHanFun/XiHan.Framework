#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExampleCommands
// Guid:78b523ae-4f3a-4192-a4e6-9541936d9644
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/16 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.DevTools.CommandLine;
using XiHan.Framework.DevTools.CommandLine.Attributes;
using XiHan.Framework.DevTools.CommandLine.Commands;
using XiHan.Framework.Utils.ConsoleTools;

namespace XiHan.Framework.DevTools.Tests.CommandLine;

/// <summary>
/// 示例：文件操作命令
/// </summary>
[Command("file", Description = "文件操作命令", Usage = "file copy source.txt dest.txt\nfile delete temp.txt")]
public class FileCommand : ICommand
{
    [SubCommand(typeof(CopyCommand))]
    public CopyCommand? Copy { get; set; }

    [SubCommand(typeof(DeleteCommand))]
    public DeleteCommand? Delete { get; set; }

    [SubCommand(typeof(ListCommand))]
    public ListCommand? List { get; set; }

    public async Task<int> ExecuteAsync(CommandContext context)
    {
        // 如果没有指定子命令，显示帮助
        await Task.CompletedTask; // 占位符，避免编译警告
        var helpGen = new HelpGenerator();
        HelpGenerator.ShowColoredCommandHelp(context.CommandDescriptor);
        return 0;
    }
}

/// <summary>
/// 示例：文件拷贝子命令
/// </summary>
[Command("copy", Description = "复制文件")]
public class CopyCommand : ICommand
{
    [CommandArgument(0, "source", Description = "源文件路径")]
    [FileExists]
    public string Source { get; set; } = "";

    [CommandArgument(1, "destination", Description = "目标文件路径")]
    public string Destination { get; set; } = "";

    [CommandOption("force", "f", Description = "强制覆盖", IsSwitch = true)]
    public bool Force { get; set; }

    [CommandOption("verbose", "v", Description = "详细输出", IsSwitch = true)]
    public bool Verbose { get; set; }

    public async Task<int> ExecuteAsync(CommandContext context)
    {
        try
        {
            if (Verbose)
            {
                ConsoleColorWriter.WriteInfo($"复制文件: {Source} -> {Destination}");
            }

            // 检查目标文件是否存在
            if (File.Exists(Destination) && !Force)
            {
                var confirm = ConsolePrompt.Confirm($"目标文件 '{Destination}' 已存在，是否覆盖？");
                if (!confirm)
                {
                    ConsoleColorWriter.WriteWarn("操作已取消");
                    return 1;
                }
            }

            // 使用进度条显示复制进度
            var fileInfo = new FileInfo(Source);
            using var progress = new ConsoleProgressBar(fileInfo.Length, 40);

            const int BufferSize = 81920; // 80KB buffer
            var buffer = new byte[BufferSize];
            long totalRead = 0;

            using var sourceStream = File.OpenRead(Source);
            using var destStream = File.Create(Destination);

            int bytesRead;
            while ((bytesRead = await sourceStream.ReadAsync(buffer)) > 0)
            {
                await destStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalRead += bytesRead;
                progress.Update(totalRead, $"已复制 {totalRead / 1024:N0} KB");
            }

            progress.Complete("复制完成");
            ConsoleColorWriter.WriteSuccess($"文件复制成功: {Source} -> {Destination}");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleColorWriter.WriteError($"复制失败: {ex.Message}");
            return 1;
        }
    }
}

/// <summary>
/// 示例：文件删除子命令
/// </summary>
[Command("delete", Aliases = ["del", "rm"], Description = "删除文件")]
public class DeleteCommand : ICommand
{
    [CommandArgument(0, "files", Description = "要删除的文件", AllowMultiple = true)]
    public string[] Files { get; set; } = [];

    [CommandOption("recursive", "r", Description = "递归删除目录", IsSwitch = true)]
    public bool Recursive { get; set; }

    [CommandOption("force", "f", Description = "强制删除，不提示确认", IsSwitch = true)]
    public bool Force { get; set; }

    public async Task<int> ExecuteAsync(CommandContext context)
    {
        await Task.CompletedTask; // 占位符，避免编译警告

        if (Files.Length == 0)
        {
            ConsoleColorWriter.WriteError("请指定要删除的文件");
            return 1;
        }

        var successCount = 0;
        var failCount = 0;

        foreach (var file in Files)
        {
            try
            {
                if (Directory.Exists(file))
                {
                    if (!Recursive)
                    {
                        ConsoleColorWriter.WriteError($"'{file}' 是目录，请使用 -r 选项递归删除");
                        failCount++;
                        continue;
                    }

                    if (!Force)
                    {
                        var confirm = ConsolePrompt.Confirm($"确定要删除目录 '{file}' 及其所有内容吗？");
                        if (!confirm)
                        {
                            ConsoleColorWriter.WriteWarn($"跳过删除: {file}");
                            continue;
                        }
                    }

                    Directory.Delete(file, true);
                    ConsoleColorWriter.WriteSuccess($"已删除目录: {file}");
                }
                else if (File.Exists(file))
                {
                    if (!Force)
                    {
                        var confirm = ConsolePrompt.Confirm($"确定要删除文件 '{file}' 吗？");
                        if (!confirm)
                        {
                            ConsoleColorWriter.WriteWarn($"跳过删除: {file}");
                            continue;
                        }
                    }

                    File.Delete(file);
                    ConsoleColorWriter.WriteSuccess($"已删除文件: {file}");
                }
                else
                {
                    ConsoleColorWriter.WriteWarn($"文件或目录不存在: {file}");
                    failCount++;
                    continue;
                }

                successCount++;
            }
            catch (Exception ex)
            {
                ConsoleColorWriter.WriteError($"删除 '{file}' 失败: {ex.Message}");
                failCount++;
            }
        }

        ConsoleColorWriter.WriteInfo($"删除完成: 成功 {successCount}, 失败 {failCount}");
        return failCount > 0 ? 1 : 0;
    }
}

/// <summary>
/// 示例：文件列表子命令
/// </summary>
[Command("list", Aliases = ["ls", "dir"], Description = "列出文件和目录")]
public class ListCommand : ICommand
{
    [CommandArgument(0, "path", Description = "要列出的路径", Required = false, DefaultValue = ".")]
    public string FilePath { get; set; } = ".";

    [CommandOption("all", "a", Description = "显示隐藏文件", IsSwitch = true)]
    public bool ShowHidden { get; set; }

    [CommandOption("long", "l", Description = "详细格式", IsSwitch = true)]
    public bool LongFormat { get; set; }

    [CommandOption("recursive", "r", Description = "递归列出子目录", IsSwitch = true)]
    public bool Recursive { get; set; }

    [CommandOption("filter", Description = "文件过滤器 (*.txt, *.cs 等)")]
    public string? Filter { get; set; }

    public async Task<int> ExecuteAsync(CommandContext context)
    {
        try
        {
            if (!Directory.Exists(FilePath))
            {
                ConsoleColorWriter.WriteError($"目录不存在: {FilePath}");
                return 1;
            }

            await ListDirectoryAsync(FilePath, 0);
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleColorWriter.WriteError($"列出文件失败: {ex.Message}");
            return 1;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)bytes;
        var suffixIndex = 0;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F1} {suffixes[suffixIndex]}";
    }

    private async Task ListDirectoryAsync(string path, int depth)
    {
        var indent = new string(' ', depth * 2);

        if (depth == 0)
        {
            ConsoleColorWriter.WriteInfo($"目录: {Path.GetFullPath(path)}");
            Console.WriteLine();
        }

        try
        {
            var entries = Directory.GetFileSystemEntries(path, Filter ?? "*")
                .Where(entry => ShowHidden || !Path.GetFileName(entry).StartsWith('.'))
                .OrderBy(entry => Directory.Exists(entry) ? 0 : 1) // 目录优先
                .ThenBy(entry => Path.GetFileName(entry));

            if (LongFormat)
            {
                var table = new ConsoleTable("类型", "名称", "大小", "修改时间");

                foreach (var entry in entries)
                {
                    var name = Path.GetFileName(entry);
                    var fullPath = Path.Combine(path, name);

                    if (Directory.Exists(fullPath))
                    {
                        var dirInfo = new DirectoryInfo(fullPath);
                        table.AddRow("目录", indent + name + "/", "-", dirInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
                    }
                    else
                    {
                        var fileInfo = new FileInfo(fullPath);
                        var size = FormatFileSize(fileInfo.Length);
                        table.AddRow("文件", indent + name, size, fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
                    }
                }

                table.Print();
            }
            else
            {
                foreach (var entry in entries)
                {
                    var name = Path.GetFileName(entry);
                    var fullPath = Path.Combine(path, name);

                    if (Directory.Exists(fullPath))
                    {
                        ConsoleColorWriter.WriteColoredMessage($"{indent}{name}/", ConsoleColor.Blue);
                    }
                    else
                    {
                        Console.WriteLine($"{indent}{name}");
                    }
                }
            }

            // 递归处理子目录
            if (Recursive)
            {
                var subDirs = Directory.GetDirectories(path)
                    .Where(dir => ShowHidden || !Path.GetFileName(dir).StartsWith('.'));

                foreach (var subDir in subDirs)
                {
                    Console.WriteLine();
                    await ListDirectoryAsync(subDir, depth + 1);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            ConsoleColorWriter.WriteWarn($"{indent}权限不足，无法访问目录");
        }
    }
}

/// <summary>
/// 示例：应用程序配置命令
/// </summary>
[Command("config", Description = "应用程序配置管理")]
public class ConfigCommand : ICommand
{
    [CommandOption("set", Description = "设置配置项 key=value", AllowMultiple = true)]
    public string[] SetValues { get; set; } = [];

    [CommandOption("get", Description = "获取配置项")]
    public string? GetKey { get; set; }

    [CommandOption("list", "l", Description = "列出所有配置", IsSwitch = true)]
    public bool ListAll { get; set; }

    [CommandOption("file", Description = "配置文件路径", DefaultValue = "app.config")]
    public string ConfigFile { get; set; } = "app.config";

    public async Task<int> ExecuteAsync(CommandContext context)
    {
        try
        {
            var config = await LoadConfigAsync();

            if (ListAll)
            {
                ShowAllConfig(config);
            }
            else if (!string.IsNullOrEmpty(GetKey))
            {
                ShowConfigValue(config, GetKey);
            }
            else if (SetValues.Length > 0)
            {
                SetConfigValues(config, SetValues);
                await SaveConfigAsync(config);
                ConsoleColorWriter.WriteSuccess("配置已保存");
            }
            else
            {
                ShowAllConfig(config);
            }

            return 0;
        }
        catch (Exception ex)
        {
            ConsoleColorWriter.WriteError($"配置操作失败: {ex.Message}");
            return 1;
        }
    }

    private static void ShowConfigValue(Dictionary<string, string> config, string key)
    {
        if (config.TryGetValue(key, out var value))
        {
            ConsoleColorWriter.WriteSuccess($"{key} = {value}");
        }
        else
        {
            ConsoleColorWriter.WriteWarn($"配置项 '{key}' 不存在");
        }
    }

    private static void SetConfigValues(Dictionary<string, string> config, string[] setValues)
    {
        foreach (var setValue in setValues)
        {
            var parts = setValue.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim();
                config[key] = value;
                ConsoleColorWriter.WriteInfo($"设置: {key} = {value}");
            }
            else
            {
                ConsoleColorWriter.WriteWarn($"无效的配置格式: {setValue}");
            }
        }
    }

    private async Task<Dictionary<string, string>> LoadConfigAsync()
    {
        var config = new Dictionary<string, string>();

        if (File.Exists(ConfigFile))
        {
            var lines = await File.ReadAllLinesAsync(ConfigFile);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    config[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        return config;
    }

    private async Task SaveConfigAsync(Dictionary<string, string> config)
    {
        var lines = new List<string>
        {
            "# Application Configuration",
            $"# Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            ""
        };

        foreach (var kvp in config.OrderBy(x => x.Key))
        {
            lines.Add($"{kvp.Key}={kvp.Value}");
        }

        await File.WriteAllLinesAsync(ConfigFile, lines);
    }

    private void ShowAllConfig(Dictionary<string, string> config)
    {
        if (config.Count == 0)
        {
            ConsoleColorWriter.WriteWarn("没有配置项");
            return;
        }

        ConsoleColorWriter.WriteInfo($"配置文件: {ConfigFile}");
        var table = new ConsoleTable("键", "值");

        foreach (var kvp in config.OrderBy(x => x.Key))
        {
            table.AddRow(kvp.Key, kvp.Value);
        }

        table.Print();
    }
}
