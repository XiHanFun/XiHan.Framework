# iHan.Framework.DevTools.CommandLine

强大且易用的 .NET 命令行解析框架，支持现代命令行应用程序开发的各种需求。

## ✨ 特性

### 🎯 核心功能

- **多格式参数解析** - 支持 `--option value`、`-o value`、`key=value` 等格式
- **布尔开关** - 支持 `--verbose` 等开关参数
- **多值参数** - 支持 `--files a.txt b.txt` 等多值选项
- **位置参数** - 支持有序位置参数和验证
- **默认值和必填校验** - 灵活的参数配置

### 🏗️ 命令系统

- **命令与子命令** - 类似 Git 的命令结构 (`git commit -m "msg"`)
- **自动对象绑定** - 参数自动绑定到 POCO 类
- **类型转换** - 自动类型转换和集合支持
- **自定义验证** - 支持自定义验证器

### 📖 帮助系统

- **自动帮助生成** - 根据属性自动生成美观的帮助文档
- **彩色表格显示** - 使用 ConsoleTable 优化显示效果
- **版本信息** - 自动版本信息显示

### 🚀 高级功能

- **交互式模式** - REPL 风格的交互式命令行
- **进度显示** - 集成进度条和加载指示器
- **插件发现** - 自动扫描和注册命令类
- **彩色输出** - 丰富的彩色输出支持

## 🚀 快速开始

### 1. 基本用法

```csharp
using iHan.Framework.DevTools.CommandLine;

// 创建应用程序
var app = new CommandApp
{
    Name = "MyApp",
    Description = "我的命令行应用程序",
    Version = "1.0.0"
};

// 添加命令
app.AddCommand<FileCommand>();

// 运行
await app.RunAsync(args);
```

### 2. 定义命令

```csharp
[Command("copy", Description = "复制文件")]
public class CopyCommand : ICommand
{
    [CommandArgument(0, "source", Description = "源文件")]
    [FileExists]
    public string Source { get; set; } = "";

    [CommandArgument(1, "destination", Description = "目标文件")]
    public string Destination { get; set; } = "";

    [CommandOption("force", "f", Description = "强制覆盖", IsSwitch = true)]
    public bool Force { get; set; }

    [CommandOption("verbose", "v", Description = "详细输出", IsSwitch = true)]
    public bool Verbose { get; set; }

    public async Task<int> ExecuteAsync(CommandContext context)
    {
        // 命令实现
        if (Verbose)
        {
            Console.WriteLine($"复制: {Source} -> {Destination}");
        }

        File.Copy(Source, Destination, Force);
        return 0;
    }
}
```

### 3. 子命令支持

```csharp
[Command("git", Description = "Git 命令示例")]
public class GitCommand : ICommand
{
    [SubCommand(typeof(CommitCommand))]
    public CommitCommand? Commit { get; set; }

    [SubCommand(typeof(PushCommand))]
    public PushCommand? Push { get; set; }

    public async Task<int> ExecuteAsync(CommandContext context)
    {
        // 显示帮助或执行默认操作
        return 0;
    }
}

[Command("commit", Description = "提交更改")]
public class CommitCommand : ICommand
{
    [CommandOption("message", "m", Description = "提交消息", Required = true)]
    public string Message { get; set; } = "";

    public async Task<int> ExecuteAsync(CommandContext context)
    {
        Console.WriteLine($"提交消息: {Message}");
        return 0;
    }
}
```

### 4. 直接参数解析

```csharp
// 解析为字典
var parsed = CommandLine.Parse(args);
var verboseMode = parsed.HasOption("verbose");

// 解析为强类型对象
public class Options
{
    [CommandOption("name", "n", Required = true)]
    public string Name { get; set; } = "";

    [CommandOption("count", "c", DefaultValue = 10)]
    public int Count { get; set; }

    [CommandOption("files", AllowMultiple = true)]
    public string[] Files { get; set; } = [];
}

var options = CommandLine.Parse<Options>(args);
```

### 5. 交互式模式

```csharp
var app = new CommandApp { Name = "InteractiveApp" };
app.AddCommand<FileCommand>();

// 启动交互式 REPL
await app.RunInteractiveAsync("MyApp> ");
```

## 📋 属性参考

### Option 属性

```csharp
[CommandOption("name", "n",
    Description = "选项描述",
    Required = true,
    DefaultValue = "默认值",
    IsSwitch = false,
    AllowMultiple = false,
    MetaName = "VALUE")]
```

### Argument 属性

```csharp
[CommandArgument(0, "filename",
    Description = "文件名参数",
    Required = true,
    DefaultValue = null,
    AllowMultiple = false)]
```

### Command 属性

```csharp
[Command("commandname",
    Aliases = new[] { "alias1", "alias2" },
    Description = "命令描述",
    Usage = "command [CommandOptions] <args>",
    IsDefault = false,
    Hidden = false)]
```

### 验证属性

```csharp
// 内置验证器
[Range(1, 100)]              // 数值范围
[FileExists]                 // 文件存在
[DirectoryExists]            // 目录存在

// 自定义验证器
[Validation(typeof(EmailValidator))]
```

## 🎨 进度和 UI 功能

### 进度条

```csharp
using var progress = new ConsoleProgressBar(total: 100);
for (int i = 0; i <= 100; i++)
{
    progress.Update(i, $"处理 {i}%");
    await Task.Delay(50);
}
progress.Complete("完成!");
```

### 多任务进度条

```csharp
using var multiProgress = new ConsoleMultiProgressBar();
multiProgress.AddTask("task1", 100, "下载文件");
multiProgress.AddTask("task2", 50, "解压文件");

// 在不同任务中更新进度
multiProgress.UpdateTask("task1", 50);
multiProgress.UpdateTask("task2", 25);
```

### 加载指示器

```csharp
// 包装异步操作
var result = await LoadingIndicator.ShowAsync(
    SomeAsyncOperation(),
    "加载中...",
    ConsoleSpinner.Styles.Dots);

// 包装同步操作
LoadingIndicator.Show(() => {
    // 长时间运行的操作
}, "处理中...");
```

### 彩色输出

```csharp
ConsoleColorWriter.WriteSuccess("操作成功!");
ConsoleColorWriter.WriteError("发生错误!");
ConsoleColorWriter.WriteWarning("注意事项");
ConsoleColorWriter.WriteInfo("提示信息");

// 关键字高亮
ConsoleColorWriter.WriteWithHighlight(
    "Error: File not found at /path/to/file",
    new[] { "Error", "not found" },
    ConsoleColor.Red);
```

### 交互式提示

```csharp
// 文本输入
var name = ConsolePrompt.Input("请输入姓名:", "默认值", required: true);

// 确认对话框
var confirmed = ConsolePrompt.Confirm("确定要删除吗?", defaultValue: false);

// 单选
var choice = ConsolePrompt.Choose("选择操作:",
    new[] { "创建", "修改", "删除" });

// 多选
var selections = ConsolePrompt.MultiChoose("选择功能:",
    new[] { "功能A", "功能B", "功能C" },
    minSelections: 1);

// 密码输入
var password = ConsolePrompt.Password("请输入密码:", maskChar: '*');
```

## 🔧 高级配置

### 解析选项

```csharp
var options = new ParseOptions
{
    CaseSensitive = false,
    EnablePosixStyle = true,
    AllowUnknownOptions = false,
    AutoGenerateHelp = true,
    AutoGenerateVersion = true
};

var app = new CommandApp(options);
```

### 自定义验证器

```csharp
public class EmailValidator : IValidator
{
    public ValidationResult Validate(object? value, object[]? parameters = null)
    {
        var email = value?.ToString();
        if (string.IsNullOrEmpty(email))
            return ValidationResult.Success;

        if (!email.Contains('@'))
            return ValidationResult.Error("无效的邮箱格式");

        return ValidationResult.Success;
    }
}

// 使用
[CommandOption("email")]
[Validation(typeof(EmailValidator))]
public string Email { get; set; } = "";
```

## 📖 示例应用

框架包含完整的示例应用程序，展示了：

- **文件操作命令** - copy, delete, list 子命令
- **配置管理** - get/set 配置项
- **构建系统** - 并行构建任务演示
- **交互式模式** - REPL 界面

运行示例：

```bash
# 查看帮助
dotnet run -- --help

# 文件操作
dotnet run -- file copy source.txt dest.txt --verbose
dotnet run -- file list --long --recursive

# 配置管理
dotnet run -- config --set key1=value1 --set key2=value2
dotnet run -- config --get key1

# 构建项目
dotnet run -- build --configuration Release --parallel 4

# 交互式模式
dotnet run -- --interactive
```

## 🤝 贡献

欢迎提交 Issue 和 Pull Request 来改进这个框架！

## 📄 许可证

本项目采用 MIT 许可证。
