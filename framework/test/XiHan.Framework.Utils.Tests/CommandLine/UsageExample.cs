#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UsageExample
// Guid:p2q3r457-o9q6-8273-mr1p-n3fd84c18149
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/16 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.CommandLine.Attributes;
using XiHan.Framework.Utils.CommandLine.Commands;
using XiHan.Framework.Utils.ConsoleTools;

namespace XiHan.Framework.Utils.CommandLine.Examples;

/// <summary>
/// 命令行框架使用示例
/// </summary>
public static class UsageExample
{
    /// <summary>
    /// 基本使用示例
    /// </summary>
    /// <param name="args">命令行参数</param>
    /// <returns>退出代码</returns>
    public static async Task<int> BasicUsageAsync(string[] args)
    {
        // 1. 创建命令行应用程序
        var app = new CommandApp
        {
            Name = "MyApp",
            Description = "一个示例命令行应用程序",
            Version = "1.0.0"
        };

        // 2. 添加命令
        app.AddCommand<FileCommand>()
           .AddCommand<ConfigCommand>()
           .AddCommand<BuildCommand>();

        // 3. 自动发现命令（可选）
        // app.DiscoverCommands();

        // 4. 运行应用程序
        return await app.RunAsync(args);
    }

    /// <summary>
    /// 交互式模式示例
    /// </summary>
    /// <returns>退出代码</returns>
    public static async Task<int> InteractiveModeExample()
    {
        var app = new CommandApp
        {
            Name = "InteractiveApp",
            Description = "交互式命令行应用程序"
        };

        app.AddCommand<FileCommand>()
           .AddCommand<ConfigCommand>();

        // 启动交互式模式
        return await app.RunInteractiveAsync("MyApp> ");
    }

    /// <summary>
    /// 直接解析参数示例
    /// </summary>
    /// <param name="args">命令行参数</param>
    public static void DirectParsingExample(string[] args)
    {
        // 方式1：解析为字典
        var parsedArgs = CommandLine.Parse(args);
        
        Console.WriteLine("选项:");
        foreach (var option in parsedArgs.Options)
        {
            Console.WriteLine($"  {option.Key}: {string.Join(", ", option.Value)}");
        }
        
        Console.WriteLine("参数:");
        for (var i = 0; i < parsedArgs.Arguments.Count; i++)
        {
            Console.WriteLine($"  [{i}]: {parsedArgs.Arguments[i]}");
        }

        // 方式2：解析为强类型对象
        var options = CommandLine.Parse<MyOptions>(args);
        Console.WriteLine($"Name: {options.Name}");
        Console.WriteLine($"Verbose: {options.Verbose}");
        Console.WriteLine($"Files: {string.Join(", ", options.Files)}");
    }

    /// <summary>
    /// 自定义验证示例
    /// </summary>
    /// <param name="args">命令行参数</param>
    public static void ValidationExample(string[] args)
    {
        try
        {
            var options = CommandLine.Parse<ValidatedOptions>(args);
            Console.WriteLine("验证通过");
        }
        catch (ArgumentParseException ex)
        {
            ConsoleColorWriter.WriteError($"验证失败: {ex.Message}");
        }
    }
}

/// <summary>
/// 示例选项类
/// </summary>
public class MyOptions
{
    [Option("name", "n", Description = "用户名称", Required = true)]
    public string Name { get; set; } = "";

    [Option("verbose", "v", Description = "详细输出", IsSwitch = true)]
    public bool Verbose { get; set; }

    [Option("files", "f", Description = "文件列表", AllowMultiple = true)]
    public string[] Files { get; set; } = [];

    [Option("count", "c", Description = "数量", DefaultValue = 10)]
    public int Count { get; set; }
}

/// <summary>
/// 带验证的选项类
/// </summary>
public class ValidatedOptions
{
    [Option("port", "p", Description = "端口号")]
    [Range(1, 65535)]
    public int Port { get; set; } = 8080;

    [Option("input", "i", Description = "输入文件")]
    [FileExists]
    public string? InputFile { get; set; }

    [Option("output", "o", Description = "输出目录")]
    [DirectoryExists]
    public string? OutputDirectory { get; set; }

    [Option("email", Description = "邮箱地址")]
    [Validation(typeof(EmailValidator))]
    public string? Email { get; set; }
}

/// <summary>
/// 示例构建命令
/// </summary>
[Command("build", Description = "构建项目", Usage = "build --configuration Release --output ./bin")]
public class BuildCommand : ICommand
{
    [Option("configuration", "c", Description = "构建配置", DefaultValue = "Debug")]
    public string Configuration { get; set; } = "Debug";

    [Option("output", "o", Description = "输出目录", DefaultValue = "./bin")]
    public string OutputDirectory { get; set; } = "./bin";

    [Option("clean", Description = "构建前清理", IsSwitch = true)]
    public bool Clean { get; set; }

    [Option("parallel", "j", Description = "并行构建数", DefaultValue = 1)]
    [Range(1, Environment.ProcessorCount)]
    public int ParallelCount { get; set; } = 1;

    [Option("defines", "D", Description = "预处理器定义", AllowMultiple = true)]
    public string[] Defines { get; set; } = [];

    public async Task<int> ExecuteAsync(CommandContext context)
    {
        ConsoleColorWriter.WriteInfo($"开始构建项目...");
        ConsoleColorWriter.WriteInfo($"配置: {Configuration}");
        ConsoleColorWriter.WriteInfo($"输出目录: {OutputDirectory}");

        if (Clean)
        {
            ConsoleColorWriter.WriteInfo("清理旧文件...");
            using var cleanSpinner = new ConsoleSpinner("清理中...");
            await Task.Delay(1000); // 模拟清理过程
            cleanSpinner.Stop("清理完成");
        }

        if (Defines.Length > 0)
        {
            ConsoleColorWriter.WriteInfo($"预处理器定义: {string.Join(", ", Defines)}");
        }

        // 模拟构建过程
        ConsoleColorWriter.WriteInfo($"使用 {ParallelCount} 个并行任务构建...");
        
        using var multiProgress = new ConsoleMultiProgressBar();
        var tasks = new List<Task>();

        for (var i = 0; i < ParallelCount; i++)
        {
            var taskId = $"task{i + 1}";
            var taskName = $"构建任务 {i + 1}";
            multiProgress.AddTask(taskId, 100, taskName);
            
            tasks.Add(SimulateBuildTask(taskId, multiProgress));
        }

        await Task.WhenAll(tasks);
        
        ConsoleColorWriter.WriteSuccess("构建完成！");
        return 0;
    }

    private static async Task SimulateBuildTask(string taskId, ConsoleMultiProgressBar progress)
    {
        var random = new Random();
        for (var i = 0; i <= 100; i += random.Next(1, 5))
        {
            progress.UpdateTask(taskId, i, $"处理 {i}%");
            await Task.Delay(random.Next(50, 150));
        }
        progress.CompleteTask(taskId, "完成");
    }
}

/// <summary>
/// 邮箱验证器
/// </summary>
public class EmailValidator : IValidator
{
    public ValidationResult Validate(object? value, object[]? parameters = null)
    {
        if (value == null) return ValidationResult.Success;
        
        var email = value.ToString();
        if (string.IsNullOrWhiteSpace(email))
            return ValidationResult.Success;

        // 简单的邮箱格式验证
        if (!email.Contains('@') || !email.Contains('.'))
            return ValidationResult.Error("邮箱格式无效");

        var parts = email.Split('@');
        if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
            return ValidationResult.Error("邮箱格式无效");

        return ValidationResult.Success;
    }
}

/// <summary>
/// 程序入口点示例
/// </summary>
public class ProgramExample
{
    public static async Task<int> Main(string[] args)
    {
        // 检查是否启动交互模式
        if (args.Length == 1 && args[0] == "--interactive")
        {
            return await UsageExample.InteractiveModeExample();
        }

        // 正常命令行模式
        return await UsageExample.BasicUsageAsync(args);
    }
}
