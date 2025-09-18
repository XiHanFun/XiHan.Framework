#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Examples
// Guid:g2h3i567-f9h6-8273-cg1f-d3fd84c18138
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/16 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.ConsoleTools;

namespace XiHan.Framework.Utils.Tests.ConsoleTools;

/// <summary>
/// 控制台工具使用示例
/// </summary>
public static class Examples
{
    /// <summary>
    /// 进度条示例
    /// </summary>
    public static async Task ProgressBarExample()
    {
        ConsoleColorWriter.WriteInfo("=== 进度条示例 ===");

        // 单个进度条
        Console.WriteLine("1. 单个进度条:");
        using var progress = new ConsoleProgressBar(100, 40);
        for (var i = 0; i <= 100; i += 5)
        {
            progress.Update(i, $"处理项目 {i}");
            await Task.Delay(100);
        }
        progress.Complete("处理完成!");

        await Task.Delay(1000);

        // 多任务进度条
        Console.WriteLine("\n2. 多任务进度条:");
        using var multiProgress = new ConsoleMultiProgressBar();

        multiProgress.AddTask("task1", 50, "下载文件");
        multiProgress.AddTask("task2", 30, "解压文件");
        multiProgress.AddTask("task3", 20, "安装程序");

        var tasks = new[]
        {
            SimulateTask("task1", 50, multiProgress),
            SimulateTask("task2", 30, multiProgress),
            SimulateTask("task3", 20, multiProgress)
        };

        await Task.WhenAll(tasks);
        Console.WriteLine("\n所有任务完成!");
    }

    /// <summary>
    /// 旋转器示例
    /// </summary>
    public static async Task SpinnerExample()
    {
        ConsoleColorWriter.WriteInfo("=== 旋转器示例 ===");

        // 基本旋转器
        Console.WriteLine("1. 基本旋转器:");
        using (var spinner = new ConsoleSpinner("加载数据中..."))
        {
            await Task.Delay(3000);
            spinner.Stop("数据加载完成!");
        }

        await Task.Delay(1000);

        // 不同样式的旋转器
        Console.WriteLine("\n2. 不同样式:");
        var styles = new[]
        {
            ("点式", ConsoleSpinner.Styles.Dots),
            ("箭头", ConsoleSpinner.Styles.Arrow),
            ("方块", ConsoleSpinner.Styles.Block)
        };

        foreach (var (name, style) in styles)
        {
            Console.WriteLine($"\n{name}旋转器:");
            using var spinner = new ConsoleSpinner($"使用{name}样式", style, 150);
            await Task.Delay(2000);
            spinner.Stop($"{name}样式演示完成");
        }

        // 使用 LoadingIndicator
        Console.WriteLine("\n3. LoadingIndicator 包装:");
        var result = await LoadingIndicator.ShowAsync(
            SimulateAsyncWork(),
            "执行异步任务",
            ConsoleSpinner.Styles.Clock);

        ConsoleColorWriter.WriteSuccess($"任务结果: {result}");
    }

    /// <summary>
    /// 彩色输出示例
    /// </summary>
    public static void ColorWriterExample()
    {
        ConsoleColorWriter.WriteInfo("=== 彩色输出示例 ===");

        // 基本彩色输出
        Console.WriteLine("1. 基本彩色输出:");
        ConsoleColorWriter.WriteSuccess("这是成功消息");
        ConsoleColorWriter.WriteError("这是错误消息");
        ConsoleColorWriter.WriteWarn("这是警告消息");
        ConsoleColorWriter.WriteInfo("这是信息消息");

        // 多彩消息
        Console.WriteLine("\n2. 多彩消息:");
        ConsoleColorWriter.WriteMultiColorMessage(
            "{red:错误:} 找不到文件 {yellow:/path/to/file.txt}，{green:已使用默认文件}");

        // 关键字高亮
        Console.WriteLine("\n3. 关键字高亮:");
        var logMessage = "2024-01-01 ERROR: Failed to connect to database at localhost:5432, retrying...";
        ConsoleColorWriter.WriteWithHighlightMessage(
            logMessage,
            new[] { "ERROR", "database", "localhost" },
            ConsoleColor.Red);

        // 标签消息
        Console.WriteLine("\n5. 标签消息:");
        ConsoleColorWriter.WriteTaggedMessage("API", "接口调用成功", ConsoleColor.Blue, ConsoleColor.Green);
        ConsoleColorWriter.WriteTaggedMessage("DB", "数据库连接失败", ConsoleColor.Magenta, ConsoleColor.Red);
    }

    /// <summary>
    /// 交互式提示示例
    /// </summary>
    public static void PromptExample()
    {
        ConsoleColorWriter.WriteInfo("=== 交互式提示示例 ===");

        // 文本输入
        var name = ConsolePrompt.Input("请输入您的姓名", "张三", true);
        ConsoleColorWriter.WriteSuccess($"您好, {name}!");

        // 数字输入
        var age = ConsolePrompt.Number("请输入您的年龄", 25, 0, 120);
        ConsoleColorWriter.WriteInfo($"您的年龄是: {age}");

        // 确认提示
        var confirmed = ConsolePrompt.Confirm("是否继续?", true);
        if (!confirmed)
        {
            ConsoleColorWriter.WriteWarn("操作已取消");
            return;
        }

        // 单选
        var colors = new[] { "红色", "绿色", "蓝色", "黄色" };
        var colorIndex = ConsolePrompt.Choose("选择您喜欢的颜色:", colors, 0);
        ConsoleColorWriter.WriteSuccess($"您选择了: {colors[colorIndex]}");

        // 多选
        var hobbies = new[] { "阅读", "运动", "旅行", "音乐", "电影", "游戏" };
        var hobbyIndices = ConsolePrompt.MultiChoose("选择您的爱好 (可多选):", hobbies, null, 1, 3);
        var selectedHobbies = hobbyIndices.Select(i => hobbies[i]);
        ConsoleColorWriter.WriteSuccess($"您的爱好: {string.Join(", ", selectedHobbies)}");

        // 密码输入
        var password = ConsolePrompt.Password("请输入密码", '*', true, 6);
        ConsoleColorWriter.WriteSuccess("密码设置成功!");
    }

    /// <summary>
    /// 交互式菜单示例
    /// </summary>
    public static void MenuExample()
    {
        ConsoleColorWriter.WriteInfo("=== 交互式菜单示例 ===");

        // 创建主菜单
        var mainMenu = new ConsoleMenu
        {
            Title = "主菜单 - 系统管理",
            SelectionColor = ConsoleColor.Green
        };

        // 添加菜单项
        mainMenu.AddItem("用户管理", UserManagementMenu)
              .AddItem("系统设置", () => ConsoleColorWriter.WriteInfo("打开系统设置..."))
              .AddSeparator("─── 工具 ───")
              .AddItem("数据备份", () => BackupData())
              .AddItem("日志查看", () => ConsoleColorWriter.WriteInfo("查看系统日志..."))
              .AddItem("关于系统", ShowAbout);

        // 显示菜单
        mainMenu.ShowAndExecute();
    }

    /// <summary>
    /// 快速菜单示例
    /// </summary>
    public static void QuickMenuExample()
    {
        ConsoleColorWriter.WriteInfo("=== 快速菜单示例 ===");

        // 简单选择
        var choice = QuickMenu.Choose("选择操作", "新建", "打开", "保存", "另存为");
        if (choice >= 0)
        {
            var actionNames = new[] { "新建", "打开", "保存", "另存为" };
            ConsoleColorWriter.WriteSuccess($"您选择了: {actionNames[choice]}");
        }

        // 确认对话框
        var deleteConfirmed = QuickMenu.Confirm("确定要删除所有数据吗？");
        if (deleteConfirmed)
        {
            ConsoleColorWriter.WriteError("数据已删除!");
        }
        else
        {
            ConsoleColorWriter.WriteInfo("操作已取消");
        }

        // 动作菜单
        var actions = new Dictionary<string, Action>
        {
            ["查看状态"] = () => ConsoleColorWriter.WriteInfo("系统状态: 正常"),
            ["重启服务"] = () => ConsoleColorWriter.WriteWarn("服务正在重启..."),
            ["清理缓存"] = () => ConsoleColorWriter.WriteSuccess("缓存已清理"),
            ["检查更新"] = () => ConsoleColorWriter.WriteInfo("检查更新中...")
        };

        QuickMenu.Execute("系统工具", actions);
    }

    /// <summary>
    /// 用户管理子菜单
    /// </summary>
    private static void UserManagementMenu()
    {
        var userMenu = new ConsoleMenu
        {
            Title = "用户管理",
            TitleColor = ConsoleColor.Magenta
        };

        userMenu.AddItem("添加用户", () => ConsoleColorWriter.WriteSuccess("添加用户功能"))
               .AddItem("删除用户", () => ConsoleColorWriter.WriteWarn("删除用户功能"))
               .AddItem("修改权限", () => ConsoleColorWriter.WriteInfo("修改权限功能"))
               .AddItem("用户列表", () => ShowUserList());

        userMenu.ShowAndExecute();
    }

    #region 辅助方法

    private static async Task SimulateTask(string taskId, int total, ConsoleMultiProgressBar progressBar)
    {
        var random = new Random();
        for (var i = 0; i <= total; i++)
        {
            progressBar.UpdateTask(taskId, i, $"处理 {i}/{total}");
            await Task.Delay(random.Next(50, 200));
        }
        progressBar.CompleteTask(taskId);
    }

    private static async Task<string> SimulateAsyncWork()
    {
        await Task.Delay(2000);
        return "异步任务完成";
    }

    private static void BackupData()
    {
        using var progress = new ConsoleProgressBar(100, 30);
        for (var i = 0; i <= 100; i += 10)
        {
            progress.Update(i, $"备份进度 {i}%");
            Thread.Sleep(200);
        }
        progress.Complete("备份完成!");
    }

    private static void ShowAbout()
    {
        Console.WriteLine();
        ConsoleColorWriter.WriteColoredMessage("XiHan Framework Console Tools", ConsoleColor.Cyan);
        Console.WriteLine("版本: 1.0.0");
        Console.WriteLine("作者: zhaifanhua");
        Console.WriteLine("功能: 提供丰富的控制台交互工具");
    }

    private static void ShowUserList()
    {
        var users = new[]
        {
            "admin - 管理员",
            "user1 - 普通用户",
            "user2 - 普通用户",
            "guest - 访客"
        };

        Console.WriteLine("\n用户列表:");
        foreach (var user in users)
        {
            Console.WriteLine($"  • {user}");
        }
    }

    #endregion
}
