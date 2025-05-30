# XiHan.Framework.Script

## 简介

XiHan.Framework.Script 是一个基于 Roslyn 编译器的动态 C# 脚本执行库，类似于 CS-Script，提供了强大的脚本动态编译和执行功能。

## 主要特性

- ✅ **动态编译执行** - 支持运行时编译和执行 C# 代码
- ✅ **多种脚本类型** - 支持表达式、语句、方法、类和完整程序
- ✅ **智能缓存机制** - 编译结果缓存，提升重复执行性能
- ✅ **程序集引用管理** - 灵活的程序集和命名空间引用
- ✅ **超时控制** - 防止脚本无限执行
- ✅ **类型安全** - 支持强类型返回值
- ✅ **异步支持** - 完整的异步/await 支持
- ✅ **性能监控** - 详细的执行统计信息
- ✅ **异常处理** - 完善的异常类型和错误信息

## 快速开始

### 基本用法

```csharp
using XiHan.Framework.Script;

// 1. 简单表达式求值
var result = Script.Eval("1 + 2 * 3");
Console.WriteLine(result); // 输出: 7

// 2. 执行语句脚本
var scriptResult = Script.Run(@"
    var name = ""World"";
    result = $""Hello, {name}!"";
");
Console.WriteLine(scriptResult.Value); // 输出: Hello, World!

// 3. 强类型返回值
var intResult = Script.Eval<int>("Math.Max(10, 20)");
Console.WriteLine(intResult); // 输出: 20
```

### 异步执行

```csharp
// 异步执行脚本
var result = await Script.RunAsync(@"
    await Task.Delay(100);
    result = DateTime.Now.ToString();
");

// 异步表达式求值
var value = await Script.EvalAsync<string>("$""Current time: {DateTime.Now}""");
```

### 使用脚本选项

```csharp
using XiHan.Framework.Script.Models;

// 配置脚本选项
var options = ScriptOptions.Default
    .AddReference(typeof(System.Data.DataTable))
    .AddImport("System.Data")
    .AddGlobal("userName", "张三")
    .WithTimeout(5000);

var result = Script.Run(@"
    var table = new DataTable();
    table.Columns.Add(""Name"");
    table.Rows.Add(userName);
    result = table.Rows[0][""Name""];
", options);
```

### 脚本引擎实例

```csharp
using XiHan.Framework.Script.Abstractions;

// 创建专用引擎实例
var engine = ScriptEngineFactory.CreateDefault();

// 执行脚本
var result = await engine.ExecuteAsync("Math.PI * 2");

// 获取统计信息
var stats = engine.GetStatistics();
Console.WriteLine($"总执行次数: {stats.TotalExecutions}");
Console.WriteLine($"缓存命中率: {stats.CacheHitRate:F1}%");
```

### 构建器模式

```csharp
// 使用构建器创建配置化引擎
var engine = Script.CreateBuilder()
    .AddReference(typeof(Newtonsoft.Json.JsonConvert))
    .AddImport("Newtonsoft.Json")
    .WithTimeout(10000)
    .WithOptimization()
    .Build();

var result = await engine.ExecuteAsync(@"
    var obj = new { Name = ""测试"", Value = 123 };
    result = JsonConvert.SerializeObject(obj);
");
```

## 脚本类型支持

### 1. 表达式脚本

```csharp
var options = ScriptOptions.Default.WithScriptType(ScriptType.Expression);
var result = Script.Run("Math.Sqrt(16) + Math.Pow(2, 3)", options);
// 结果: 12
```

### 2. 语句脚本

```csharp
var result = Script.Run(@"
    var numbers = new[] { 1, 2, 3, 4, 5 };
    var sum = numbers.Sum();
    var average = numbers.Average();
    result = new { Sum = sum, Average = average };
");
```

### 3. 类定义脚本

```csharp
var options = ScriptOptions.Default.WithScriptType(ScriptType.Class);
var result = Script.Run(@"
    public class Calculator
    {
        public static int Add(int a, int b) => a + b;
        public static int Multiply(int a, int b) => a * b;
    }

    public static object Execute()
    {
        return Calculator.Add(5, 3) * Calculator.Multiply(2, 4);
    }
", options);
```

### 4. 完整程序

```csharp
var options = ScriptOptions.Default.WithScriptType(ScriptType.Program);
var result = Script.Run(@"
    using System;
    using System.Linq;

    public class Program
    {
        public static object Main()
        {
            var data = Enumerable.Range(1, 10).Where(x => x % 2 == 0);
            return string.Join("", "", data);
        }
    }
", options);
```

## 高级功能

### 脚本文件执行

```csharp
// 创建脚本文件 script.cs
await File.WriteAllTextAsync("script.cs", @"
    var message = $""脚本执行时间: {DateTime.Now}"";
    result = message;
");

// 执行脚本文件
var result = await Script.RunFileAsync("script.cs");
Console.WriteLine(result.Value);
```

### 批量执行

```csharp
var scripts = new[]
{
    "1 + 1",
    "2 * 3",
    "Math.PI.ToString()"
};

var results = await engine.ExecuteBatchAsync(scripts, parallel: true);
foreach (var result in results)
{
    Console.WriteLine($"结果: {result.Value}, 耗时: {result.ExecutionTimeMs}ms");
}
```

### 性能基准测试

```csharp
var stats = await engine.BenchmarkAsync("Math.Sin(Math.PI / 4)", iterations: 1000);

Console.WriteLine($"总执行次数: {stats.TotalIterations}");
Console.WriteLine($"平均耗时: {stats.AverageExecutionTimeMs:F2}ms");
Console.WriteLine($"每秒执行数: {stats.ExecutionsPerSecond:F0}");
Console.WriteLine($"缓存命中率: {stats.CacheHitRate:F1}%");
```

### 语法验证

```csharp
var validation = await engine.ValidateSyntaxAsync(@"
    var x = 10;
    var y = 20
    // 缺少分号，会产生编译错误
");

if (!validation.IsValid)
{
    Console.WriteLine("语法错误:");
    Console.WriteLine(validation.FormatErrors());
}
```

### 超时控制

```csharp
try
{
    var result = await engine.ExecuteWithTimeoutAsync(@"
        // 模拟长时间运行的脚本
        Thread.Sleep(10000);
        result = ""完成"";
    ", timeoutMs: 1000);
}
catch (ScriptTimeoutException ex)
{
    Console.WriteLine($"脚本执行超时: {ex.TimeoutMs}ms");
}
```

## 错误处理

```csharp
var result = await Script.RunAsync(@"
    var x = 10;
    var y = 0;
    result = x / y; // 除零错误
");

if (!result.IsSuccess)
{
    Console.WriteLine($"执行失败: {result.ErrorMessage}");
    if (result.Exception != null)
    {
        Console.WriteLine($"异常详情: {result.Exception}");
    }
}

// 或者使用异常模式
try
{
    var value = await engine.ExecuteOrThrowAsync("throw new Exception(""测试异常"");");
}
catch (ScriptExecutionException ex)
{
    Console.WriteLine($"脚本执行异常: {ex.Message}");
}
```

## 引擎管理

```csharp
// 创建命名引擎
var calculatorEngine = ScriptEngineFactory.GetOrCreate("calculator", options =>
{
    options.AddImport("System.Math");
    options.WithOptimization();
});

var mathEngine = ScriptEngineFactory.GetOrCreate("math", options =>
{
    options.AddReference(typeof(System.Numerics.BigInteger));
    options.AddImport("System.Numerics");
});

// 获取所有引擎统计信息
var allStats = ScriptEngineFactory.GetAllStatistics();
foreach (var kvp in allStats)
{
    Console.WriteLine($"引擎 {kvp.Key}: 执行次数 {kvp.Value.TotalExecutions}");
}

// 清理资源
ScriptEngineFactory.ReleaseAll();
```

## 最佳实践

1. **重用引擎实例** - 创建引擎有一定开销，建议重用实例
2. **合理使用缓存** - 启用缓存可大幅提升重复脚本的执行性能
3. **设置超时时间** - 防止恶意或错误脚本无限执行
4. **异常处理** - 始终处理脚本执行可能的异常
5. **资源清理** - 及时释放不再使用的引擎实例

## 性能优化

- 编译结果自动缓存，重复执行同一脚本性能大幅提升
- 支持预编译模式，可预先编译常用脚本
- 内存池化技术，减少垃圾回收压力
- 异步执行，不阻塞主线程

## 注意事项

- 脚本执行具有完全的系统权限，请谨慎处理不可信的脚本代码
- 复杂脚本的首次编译可能需要较长时间
- 内存使用量与脚本复杂度和缓存大小相关
- 建议在生产环境中设置合理的超时时间和资源限制
