# 控制台表格功能使用指南

## 概述

`XiHan.Framework.Utils.Logging` 现在支持强大的表格打印功能，可以自适应列宽和行高，支持多种边框样式和颜色。

## 主要特性

✅ **自适应列宽和行高** - 自动计算最佳列宽，支持文本换行  
✅ **多种边框样式** - 6 种不同的边框样式可选  
✅ **颜色支持** - 支持表头和数据的颜色自定义  
✅ **中文字符支持** - 正确处理中文字符的显示宽度  
✅ **集成日志系统** - 无缝集成现有的日志功能  
✅ **链式调用** - 支持流式 API，使用便捷

## 基本用法

### 1. 创建简单表格

```csharp
using XiHan.Framework.Utils.Logging;

// 创建表格
var table = new ConsoleTable("姓名", "年龄", "城市")
    .AddRow("张三", 25, "北京")
    .AddRow("李四", 30, "上海")
    .AddRow("王五", 28, "广州");

// 输出到控制台
table.Print();
```

### 2. 设置边框样式

```csharp
var table = new ConsoleTable("ID", "Name", "Status")
{
    BorderStyle = TableBorderStyle.Rounded  // 圆角边框
};

table.AddRow(1, "Task 1", "完成")
     .AddRow(2, "Task 2", "进行中")
     .AddRow(3, "Task 3", "等待");

table.Print();
```

### 3. 使用颜色

```csharp
var table = new ConsoleTable()
{
    DefaultHeaderColor = ConsoleColor.Cyan,
    DefaultTextColor = ConsoleColor.White
};

// 设置带颜色的表头
table.SetHeaders(
    ("Name", ConsoleColor.Yellow),
    ("Status", ConsoleColor.Green),
    ("Priority", ConsoleColor.Red)
);

// 添加带颜色的行
table.AddRow(
    ("项目A", ConsoleColor.White),
    ("完成", ConsoleColor.Green),
    ("高", ConsoleColor.Red)
);

table.Print(); // 输出带颜色的表格到控制台
```

**注意**：颜色功能仅在 `Print()` 和 `PrintLine()` 方法中生效。`ToString()` 方法返回纯文本格式，不包含颜色信息。

## 边框样式

### TableBorderStyle.None - 无边框

```
姓名   年龄  城市
张三   25    北京
李四   30    上海
```

### TableBorderStyle.Simple - 简单边框（默认）

```
+------+----+------+
| 姓名 | 年龄 | 城市 |
+------+----+------+
| 张三 | 25 | 北京 |
| 李四 | 30 | 上海 |
+------+----+------+
```

### TableBorderStyle.Rounded - 圆角边框

```
╭------┬----┬------╮
│ 姓名 │ 年龄 │ 城市 │
├------┼----┼------┤
│ 张三 │ 25 │ 北京 │
│ 李四 │ 30 │ 上海 │
╰------┴----┴------╯
```

### TableBorderStyle.Double - 双线边框

```
╔══════╦════╦══════╗
║ 姓名 ║ 年龄 ║ 城市 ║
╠══════╬════╬══════╣
║ 张三 ║ 25 ║ 北京 ║
║ 李四 ║ 30 ║ 上海 ║
╚══════╩════╩══════╝
```

### TableBorderStyle.Bold - 粗体边框

```
┏━━━━━━┳━━━━┳━━━━━━┓
┃ 姓名 ┃ 年龄 ┃ 城市 ┃
┣━━━━━━╋━━━━╋━━━━━━┫
┃ 张三 ┃ 25 ┃ 北京 ┃
┃ 李四 ┃ 30 ┃ 上海 ┃
┗━━━━━━┻━━━━┻━━━━━━┛
```

### TableBorderStyle.Markdown - Markdown 样式

```
| 姓名 | 年龄 | 城市 |
|------|------|------|
| 张三 | 25   | 北京 |
| 李四 | 30   | 上海 |
```

## 与日志系统集成

### 使用 ConsoleLogger

```csharp
using XiHan.Framework.Utils.Logging;

// 创建表格
var table = new ConsoleTable("时间", "事件", "状态")
    .AddRow(DateTime.Now, "系统启动", "成功")
    .AddRow(DateTime.Now, "数据库连接", "成功");

// 输出为不同级别的日志
ConsoleLoggerTableExtensions.InfoTable(table);
ConsoleLoggerTableExtensions.WarnTable(table, ConsoleColor.Yellow);
ConsoleLoggerTableExtensions.ErrorTable(table, ConsoleColor.Red);

// 快速表格
ConsoleLoggerTableExtensions.QuickTable(
    ["用户", "操作", "结果"],
    [
        ["admin", "登录", "成功"],
        ["user1", "查询", "成功"]
    ],
    TableBorderStyle.Double
);
```

### 使用 ConsoleFileLogger

```csharp
// 输出表格到文件
var table = new ConsoleTable("错误代码", "错误信息", "时间")
    .AddRow("E001", "数据库连接失败", DateTime.Now)
    .AddRow("E002", "网络超时", DateTime.Now);

// 输出到不同的日志文件
ConsoleFileLoggerTableExtensions.ErrorTable(table, "error_log");
ConsoleFileLoggerTableExtensions.InfoTable(table, "info_log");

// 键值对表格
var config = new Dictionary<string, object>
{
    ["数据库地址"] = "localhost:5432",
    ["连接池大小"] = 10,
    ["超时时间"] = "30秒"
};

ConsoleFileLoggerTableExtensions.KeyValueTable(config, fileName: "config_log");
```

## 高级功能

### 1. 对象属性表格

```csharp
public class User
{
    public string Name { get; set; } = "张三";
    public int Age { get; set; } = 25;
    public string Email { get; set; } = "zhangsan@example.com";
}

var user = new User();

// 自动生成对象属性表格
ConsoleLoggerTableExtensions.ObjectTable(user, style: TableBorderStyle.Rounded);
```

### 2. 自定义列宽和换行

```csharp
var table = new ConsoleTable("短列", "长文本列")
{
    MaxColumnWidth = 20,  // 最大列宽20字符
    MinColumnWidth = 5,   // 最小列宽5字符
    Padding = 2          // 内边距2字符
};

table.AddRow("A", "这是一段很长的文本，会自动换行显示在表格中，确保格式整齐");
table.Print();
```

### 3. 多行数据

```csharp
var table = new ConsoleTable("名称", "描述")
    .AddRow("功能1", "这是第一行\n这是第二行\n这是第三行")
    .AddRow("功能2", "单行描述");

table.Print();
```

### 4. 批量添加数据

```csharp
var data = new[]
{
    new object[] { "产品A", 100, 9.99 },
    new object[] { "产品B", 200, 19.99 },
    new object[] { "产品C", 50, 5.99 }
};

var table = new ConsoleTable("产品", "库存", "价格")
    .AddRows(data);

table.Print();
```

## 配置选项

```csharp
var table = new ConsoleTable()
{
    BorderStyle = TableBorderStyle.Double,    // 边框样式
    ShowHeaders = true,                       // 是否显示表头
    ShowRowSeparators = false,                // 是否显示行分隔线
    MinColumnWidth = 5,                       // 最小列宽
    MaxColumnWidth = 50,                      // 最大列宽（0=无限制）
    Padding = 1,                             // 内边距
    DefaultTextColor = ConsoleColor.White,    // 默认文本颜色
    DefaultHeaderColor = ConsoleColor.Yellow  // 默认表头颜色
};
```

### 行分隔线功能

对于包含多行内容的表格，可以启用行分隔线来提高可读性：

```csharp
var table = new ConsoleTable("功能", "描述", "状态")
{
    BorderStyle = TableBorderStyle.Bold,
    ShowRowSeparators = true,  // 启用行分隔线
    MaxColumnWidth = 20
};

table.AddRow("用户认证", "支持多种认证方式：\n- 用户名密码\n- OAuth2.0\n- JWT Token", "完成")
     .AddRow("数据缓存", "多层缓存架构：\n- Redis 缓存\n- 内存缓存", "开发中");

table.Print();
```

**输出效果对比：**

无行分隔线：

```
┏━━━━━━━━━━┳━━━━━━━━━━━━━━━━━━━┳━━━━━━━━━━━┓
┃ 功能     ┃ 描述              ┃ 状态      ┃
┣━━━━━━━━━━╋━━━━━━━━━━━━━━━━━━━╋━━━━━━━━━━━┫
┃ 用户认证 ┃ 支持多种认证方式： ┃ 完成      ┃
┃          ┃ - 用户名密码      ┃           ┃
┃          ┃ - OAuth2.0        ┃           ┃
┃          ┃ - JWT Token       ┃           ┃
┃ 数据缓存 ┃ 多层缓存架构：    ┃ 开发中    ┃
┃          ┃ - Redis 缓存      ┃           ┃
┃          ┃ - 内存缓存        ┃           ┃
┗━━━━━━━━━━┻━━━━━━━━━━━━━━━━━━━┻━━━━━━━━━━━┛
```

带行分隔线：

```
┏━━━━━━━━━━┳━━━━━━━━━━━━━━━━━━━┳━━━━━━━━━━━┓
┃ 功能     ┃ 描述              ┃ 状态      ┃
┣━━━━━━━━━━╋━━━━━━━━━━━━━━━━━━━╋━━━━━━━━━━━┫
┃ 用户认证 ┃ 支持多种认证方式： ┃ 完成      ┃
┃          ┃ - 用户名密码      ┃           ┃
┃          ┃ - OAuth2.0        ┃           ┃
┃          ┃ - JWT Token       ┃           ┃
┣━━━━━━━━━━╋━━━━━━━━━━━━━━━━━━━╋━━━━━━━━━━━┫
┃ 数据缓存 ┃ 多层缓存架构：    ┃ 开发中    ┃
┃          ┃ - Redis 缓存      ┃           ┃
┃          ┃ - 内存缓存        ┃           ┃
┗━━━━━━━━━━┻━━━━━━━━━━━━━━━━━━━┻━━━━━━━━━━━┛
```

## 性能建议

1. **大数据集**：对于超过 1000 行的数据，建议分批显示
2. **文件输出**：大表格建议输出到文件而不是控制台
3. **列宽限制**：设置合理的 `MaxColumnWidth` 避免过宽的表格
4. **颜色使用**：在不支持颜色的终端中，颜色设置会被忽略

## 常见问题

### Q: 中文字符显示不对齐怎么办？

A: 确保控制台字体支持中文，推荐使用等宽字体如 "Consolas" 或 "Microsoft YaHei Mono"。

### Q: 表格太宽超出控制台怎么办？

A: 设置 `MaxColumnWidth` 属性限制列宽，长文本会自动换行。

### Q: 如何在表格中显示复杂对象？

A: 使用 `ObjectTable` 方法或自定义 `ToString()` 方法。

### Q: 能否导出为其他格式？

A: 目前支持纯文本格式，可以选择 `TableBorderStyle.Markdown` 生成 Markdown 表格。
