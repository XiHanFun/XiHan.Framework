#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConsoleTableUsageExample
// Guid:e1f45c21-d35f-4a29-84b7-28fd84218125
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/15 13:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.ConsoleTools;
using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Utils.Tests.ConsoleTools;

/// <summary>
/// 控制台表格使用示例
/// </summary>
public static class ConsoleTableUsageExample
{
    /// <summary>
    /// 基本表格示例
    /// </summary>
    public static void BasicTableExample()
    {
        Console.WriteLine("=== 基本表格示例 ===");

        var table = new ConsoleTable("姓名", "年龄", "城市")
            .AddRow("张三", 25, "北京")
            .AddRow("李四", 30, "上海")
            .AddRow("王五", 28, "广州");

        table.Print();
        Console.WriteLine();
    }

    /// <summary>
    /// 不同边框样式示例
    /// </summary>
    public static void BorderStylesExample()
    {
        var data = new[]
        {
            new object[] { "产品A", 100, "$9.99" },
            ["产品B", 50, "$19.99"],
            ["产品C", 200, "$5.99"]
        };

        var styles = new[]
        {
            (ConsoleTableBorderStyle.Simple, "简单边框"),
            (ConsoleTableBorderStyle.Rounded, "圆角边框"),
            (ConsoleTableBorderStyle.Double, "双线边框"),
            (ConsoleTableBorderStyle.Bold, "粗体边框"),
            (ConsoleTableBorderStyle.Markdown, "Markdown 样式"),
            (ConsoleTableBorderStyle.None, "无边框")
        };

        foreach (var (style, description) in styles)
        {
            Console.WriteLine($"=== {description} ===");

            var table = new ConsoleTable("产品", "库存", "价格")
            {
                BorderStyle = style
            };

            table.AddRows(data);
            table.Print();
            Console.WriteLine();
        }
    }

    /// <summary>
    /// 颜色支持示例
    /// </summary>
    public static void ColorSupportExample()
    {
        Console.WriteLine("=== 颜色支持示例 ===");

        var table = new ConsoleTable()
        {
            BorderStyle = ConsoleTableBorderStyle.Double,
            DefaultHeaderColor = ConsoleColor.Cyan,
            DefaultTextColor = ConsoleColor.White
        };

        // 设置带颜色的表头
        table.SetHeaders(
            ("任务名称", ConsoleColor.Yellow),
            ("状态", ConsoleColor.Green),
            ("优先级", ConsoleColor.Red),
            ("完成度", ConsoleColor.Blue)
        );

        // 添加带颜色的行数据
        table.AddRow(
            ("系统设计", ConsoleColor.White),
            ("完成", ConsoleColor.Green),
            ("高", ConsoleColor.Red),
            ("100%", ConsoleColor.Green)
        );

        table.AddRow(
            ("代码开发", ConsoleColor.White),
            ("进行中", ConsoleColor.Yellow),
            ("高", ConsoleColor.Red),
            ("75%", ConsoleColor.Yellow)
        );

        table.AddRow(
            ("测试验证", ConsoleColor.White),
            ("等待", ConsoleColor.Gray),
            ("中", ConsoleColor.Yellow),
            ("0%", ConsoleColor.Red)
        );

        table.Print();
        Console.WriteLine();
    }

    /// <summary>
    /// 自适应宽度示例
    /// </summary>
    public static void AutoSizingExample()
    {
        Console.WriteLine("=== 自适应宽度示例 ===");

        var table = new ConsoleTable("短列", "中等长度的列名", "很长的列名测试自适应")
        {
            BorderStyle = ConsoleTableBorderStyle.Rounded,
            MaxColumnWidth = 25, // 限制最大列宽
            Padding = 1
        };

        table.AddRow("A", "数据B", "这是一段很长的文本内容，用来测试表格的自动换行和宽度自适应功能。当文本超过最大列宽时会自动换行显示。")
             .AddRow("短数据", "中等长度的数据内容", "另一段较长的文本用来测试")
             .AddRow("X", "Y", "简短内容");

        table.Print();
        Console.WriteLine();
    }

    /// <summary>
    /// 多行文本示例
    /// </summary>
    public static void MultiLineTextExample()
    {
        Console.WriteLine("=== 多行文本示例（无行分隔线）===");

        var table = new ConsoleTable("功能模块", "详细描述", "当前状态")
        {
            BorderStyle = ConsoleTableBorderStyle.Bold,
            MaxColumnWidth = 20,
            ShowRowSeparators = false
        };

        table.AddRow("用户认证", "支持多种认证方式：\n- 用户名密码认证\n- OAuth2.0 授权\n- JWT Token 验证", "已完成")
             .AddRow("数据缓存", "多层缓存架构：\n- Redis 分布式缓存\n- 本地内存缓存", "开发中")
             .AddRow("日志系统", "完整日志方案：\n- 结构化日志记录\n- 自动文件轮转\n- 远程日志推送", "规划中");

        table.Print();
        Console.WriteLine();

        Console.WriteLine("=== 多行文本示例（带行分隔线）===");

        var tableWithSeparators = new ConsoleTable("功能模块", "详细描述", "当前状态")
        {
            BorderStyle = ConsoleTableBorderStyle.Bold,
            MaxColumnWidth = 20,
            ShowRowSeparators = true  // 启用行分隔线
        };

        tableWithSeparators.AddRow("用户认证", "支持多种认证方式：\n- 用户名密码认证\n- OAuth2.0 授权\n- JWT Token 验证", "已完成")
                           .AddRow("数据缓存", "多层缓存架构：\n- Redis 分布式缓存\n- 本地内存缓存", "开发中")
                           .AddRow("日志系统", "完整日志方案：\n- 结构化日志记录\n- 自动文件轮转\n- 远程日志推送", "规划中");

        tableWithSeparators.Print();
        Console.WriteLine();
    }

    /// <summary>
    /// 与日志系统集成示例
    /// </summary>
    public static void LoggerIntegrationExample()
    {
        Console.WriteLine("=== 与日志系统集成示例 ===");

        // 创建系统状态表格
        var statusTable = new ConsoleTable("服务", "状态", "响应时间", "错误率")
        {
            BorderStyle = ConsoleTableBorderStyle.Simple
        };

        statusTable.AddRow("数据库", "正常", "15ms", "0%")
                   .AddRow("Redis", "正常", "5ms", "0%")
                   .AddRow("API网关", "异常", "500ms", "15%");

        // 使用 LogHelper 输出
        LogHelper.InfoTable(statusTable);

        // 创建错误统计表格
        var errorTable = new ConsoleTable("错误类型", "次数", "最后发生时间")
        {
            BorderStyle = ConsoleTableBorderStyle.Double
        };

        errorTable.AddRow("数据库连接超时", 5, DateTime.Now.AddMinutes(-10))
                  .AddRow("API调用失败", 12, DateTime.Now.AddMinutes(-2))
                  .AddRow("内存不足", 1, DateTime.Now.AddHours(-1));

        LogHelper.ErrorTable(errorTable);
    }

    /// <summary>
    /// 性能数据示例
    /// </summary>
    public static void PerformanceDataExample()
    {
        Console.WriteLine("=== 性能数据示例 ===");

        var perfTable = new ConsoleTable()
        {
            BorderStyle = ConsoleTableBorderStyle.Rounded,
            DefaultHeaderColor = ConsoleColor.Cyan
        };

        perfTable.SetHeaders("指标", "当前值", "阈值", "状态");

        // 使用不同颜色表示不同的性能状态
        perfTable.AddRow(
            ("CPU使用率", ConsoleColor.White),
            ("45%", ConsoleColor.Green),
            ("80%", ConsoleColor.Gray),
            ("正常", ConsoleColor.Green)
        );

        perfTable.AddRow(
            ("内存使用率", ConsoleColor.White),
            ("85%", ConsoleColor.Yellow),
            ("90%", ConsoleColor.Gray),
            ("警告", ConsoleColor.Yellow)
        );

        perfTable.AddRow(
            ("磁盘使用率", ConsoleColor.White),
            ("95%", ConsoleColor.Red),
            ("90%", ConsoleColor.Gray),
            ("危险", ConsoleColor.Red)
        );

        perfTable.Print();
        Console.WriteLine();
    }

    /// <summary>
    /// 运行所有示例
    /// </summary>
    public static void RunAllExamples()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Clear();

        Console.WriteLine("🎨 控制台表格功能演示");
        Console.WriteLine("====================================");
        Console.WriteLine();

        BasicTableExample();
        BorderStylesExample();
        ColorSupportExample();
        AutoSizingExample();
        MultiLineTextExample();
        LoggerIntegrationExample();
        PerformanceDataExample();

        Console.WriteLine("演示完成！按任意键退出...");
        Console.ReadKey();
    }
}
