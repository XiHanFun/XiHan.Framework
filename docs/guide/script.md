# 脚本引擎

把易变的业务规则（折扣公式、审批条件、字段派生逻辑）写成一段 C# 源码存起来，运行期编译执行，改逻辑不用重新发布。这一章讲怎么把它接进项目、`IScriptEngine` 的契约长什么样，以及它的安全边界到哪为止——**边界比名字听上去窄得多，先看完再决定用不用**。

## 只有 C#

`XiHan.Framework.Script` 的唯一第三方依赖是 `Microsoft.CodeAnalysis.CSharp`（Roslyn 5.6.0）。没有 JavaScript、Python、Lua 引擎，也没有留出接别的语言的抽象层。

脚本不是被解释执行的 DSL，而是**真的 C# 源码**：Roslyn 在内存里把它编译成一个程序集，然后反射调用其中的入口方法。所以脚本拥有和宿主代码同等的能力——这既是它好用的原因，也是安全章节要反复强调的原因。

| 适合 | 不适合 |
| --- | --- |
| 运营/实施人员维护的计算公式、判定条件 | 执行终端用户提交的任意代码 |
| 需要频繁调整、又不值得每次发版的分支逻辑 | 需要真正沙箱隔离的多租户脚本市场 |
| 工作流节点里的一小段胶水代码 | 高频热路径（每次执行都要反射调用 + 载入程序集） |
| 配置化的数据清洗、字段映射 | 依赖大量业务类型的复杂逻辑（引用要一个个手动挂） |

## 安装与启用

```bash
dotnet add package XiHan.Framework.Script
```

```csharp
[DependsOn(typeof(XiHanScriptModule))]
public class MyModule : XiHanModule { }
```

::: warning 模块本身不注册任何服务
`XiHanScriptModule.ConfigureServices` 是空的——依赖它并不会让你能注入 `IScriptEngine`。三条拿到引擎的路子，按场景选：

```csharp
// 1. 静态门面：一次性、脚手架、单元测试
var value = await XiHanScript.EvalAsync<int>("1 + 2 * 3");

// 2. 命名引擎：想按业务域隔离编译缓存
var engine = ScriptEngineFactory.GetOrCreate("discount-rules");

// 3. 自己注册进 DI：想在服务里构造注入（推荐）
context.Services.TryAddSingleton<IScriptEngine>(_ => new ScriptEngine());
```

第三条正是框架内部的做法——`XiHanWorkflowModule` 依赖脚本模块之后，自己补了一句 `TryAddSingleton<IScriptEngine>(_ => new ScriptEngine())` 给脚本活动用。
:::

`ScriptEngine` 实现了 `IDisposable`，`Dispose()` 只是清空它的编译缓存。编译缓存用并发字典存放，可以被并发调用；引擎**当单例长期复用**才有意义——缓存挂在实例上，每次 `new` 一个引擎等于把缓存清零。（并发下 `GetStatistics()` 的缓存命中/未命中计数有轻微漂移，只当观测参考，别拿它做判定。）

## IScriptEngine 契约

| 方法 | 干什么 | 失败时 |
| --- | --- | --- |
| `ExecuteAsync(code, options)` | 编译并执行，返回 `ScriptResult` | 返回 `IsSuccess=false`，**不抛异常** |
| `ExecuteAsync<T>(code, options)` | 同上，结果转 `ScriptResult<T>` | 同上 |
| `ExecuteFileAsync(path, options)` | 读文件后执行 | 文件不存在→失败结果；**加载/文件安全校验失败会抛异常** |
| `CompileAsync(code, options)` | 只编译不执行，返回 `CompilationResult` | 返回失败结果 + 诊断 |
| `CreateInstanceAsync<T>(code, options)` | 执行脚本，把返回值 `as T` | 返回 `null` |
| `EvaluateAsync(expr, options)` | 求值表达式，直接返回值 | 返回 `null`，**错误信息全部丢失** |
| `EvaluateAsync<T>(expr, options)` | 同上，类型不匹配返回 `default` | 同上 |
| `ClearCache()` | 清空该引擎的编译缓存 | — |
| `GetStatistics()` | 取 `EngineStatistics` 快照 | — |

::: tip 契约上的三个反直觉点
1. **`ExecuteAsync` 从不抛异常**。超时、安全违规、脚本内部异常，全被包成 `ScriptResult.Failure(...)`，原始异常放在 `Exception` 属性里。要判断是哪类失败就 `result.Exception is ScriptSecurityException` 这样取。
2. **`ExecuteFileAsync` 会抛**。文件层的 `ScriptLoadException` / `ScriptSecurityException` 是直接抛出去的，和 `ExecuteAsync` 不对称。想统一成失败结果，用扩展方法 `ExecuteFileSafelyAsync`。
3. **`CreateInstanceAsync<T>` 不 new 任何东西**。它就是执行一次脚本，再把入口方法的返回值 `as T`。想拿到实例，脚本自己得 `return new Xxx();`。
:::

同步版本、批量、基准测试、异常兜底等便捷方法都在 `ScriptEngineExtensions` 扩展里（`Execute` / `ExecuteBatchAsync` / `ExecuteSafelyAsync` / `BenchmarkAsync` …），完整清单见[包文档](../packages/script)。

## 第一件事：选对 ScriptType

这是新手踩坑最集中的地方。引擎会先按 `ScriptType` 把你的源码**包进一个固定壳子**，然后编译，最后去程序集里找 `ScriptClass` 类的 `public static Execute` 方法调用。

| ScriptType | 引擎怎么包 | 你要怎么写 | 返回值从哪来 |
| --- | --- | --- | --- |
| `Statement`（默认） | 包进 `ScriptClass.Execute()`，开头注入 `object result = null;`，结尾 `return result;` | 直接写语句 | 给 `result` 赋值 |
| `Expression` | 包进 `ScriptClass.Execute()`，写成 `return {你的表达式};` | 只写一个表达式，不带分号 | 表达式的值 |
| `Method` | 原样嵌进 `public class ScriptClass { … }` | 自己写 `public static object Execute(...)` | 方法的 `return` |
| `Class` | 原样输出（前面带 `using` 头） | 自己写完整的类，**类名必须是 `ScriptClass`** | 其 `public static Execute` 的 `return` |
| `Program` | 完全不包装，**连 `using` 头都不加** | 自己写全部代码，仍需包含 `ScriptClass.Execute` | 同上 |

::: warning 入口点是硬编码的
`FindEntryPoint` 找的是 `assembly.GetType("ScriptClass")` 上的 `public static Execute`——全局命名空间，类名一个字都不能差。`Class` / `Program` 类型下写了别的类名，会得到"未找到有效的入口点"。
:::

### 表达式与语句

```csharp
// Expression：EvaluateAsync 会自动把 ScriptType 切成 Expression
int sum = await XiHanScript.EvalAsync<int>("1 + 2 * 3");   // 7

// Statement（默认）：给内置的 result 变量赋值就是返回值
var r = await XiHanScript.RunAsync<int>("result = 10 * 20;");
if (r.IsSuccess)
{
    Console.WriteLine(r.Value);       // 200
    Console.WriteLine(r.FromCache);   // 第二次执行同一段代码就是 true
}
```

::: danger `EvalAsync` 会吞掉全部错误
`EvaluateAsync` 在失败时直接 `return null`——编译错误、安全违规、脚本抛异常，你一律只看到 `null` 或 `default`。只在"表达式一定是对的"场景用它；需要错误信息就走 `ExecuteAsync` 拿 `ScriptResult`。
:::

### 方法：唯一能收参数的形态

```csharp
var options = new ScriptOptions()
    .WithScriptType(ScriptType.Method)
    .AddGlobal("factor", 3);

var result = await XiHanScript.RunAsync<int>(
    "public static object Execute(int factor) { return 10 * factor; }",
    options);
// result.Value == 30
```

## 传参进脚本：Globals 走的是方法参数

`Globals` **不是脚本里可以直接访问的字典**。引擎反射调用入口方法时，逐个读方法的参数名，去 `Globals` 里找同名键填成实参；找不到就填 `null`。

推论很直接：

- `Statement` / `Expression` 被包出来的 `Execute()` **没有参数**，所以这两种形态下 `Globals` 里放什么都到不了脚本里。
- 想传值，必须用 `Method`（或自己写 `ScriptClass` 的 `Class` / `Program`），并让参数名和 `Globals` 的键一致。
- 参数类型要和 `Globals` 里的值类型对得上，否则反射调用会失败（被包成执行失败结果）。

框架自己的工作流脚本活动就是这个套路：把实例变量装成一个 `IDictionary` 传进去，脚本读写这个字典，执行成功后再整体合并回实例变量。

```csharp
var vars = new Dictionary<string, object?> { ["amount"] = 100m };

var options = new ScriptOptions()
    .WithScriptType(ScriptType.Method)
    .AddGlobal("variables", vars);

await engine.ExecuteAsync(
    """
    public static object Execute(System.Collections.Generic.IDictionary<string, object> variables)
    {
        variables["discounted"] = (decimal)variables["amount"] * 0.9m;
        return null;
    }
    """, options);
```

::: tip 想让脚本用到你的业务类型
默认只挂了极少几个程序集引用：`object` / `Console` / `Enumerable` 所在的程序集，加上运行时目录下的 `System.Runtime.dll` 与 `System.Collections.dll`。其余（包括 `System.Text.Json`、`System.Net.Http` 和你自己的业务程序集）都要显式加：

```csharp
options.AddReference(typeof(OrderDto))        // 按类型挂程序集
       .AddReference("/app/plugins/Rules.dll")  // 按路径挂
       .AddImport("MyApp.Domain.Orders");        // 加 using
```

注意 `Program` 类型不会自动写 `using` 头，`AddImport` 对它无效，得在脚本里自己写。
:::

## 编译校验：把错误挡在保存那一刻

脚本是配置，配置就该在保存时校验，而不是等业务跑到那一步才炸。两个入口：

| 入口 | 返回 | 用途 |
| --- | --- | --- |
| `CompileAsync(code, options)` | `CompilationResult`（`IsSuccess` / `Diagnostics` / `ErrorMessage` / `CompilationTimeMs`） | 只编译不执行 |
| `ValidateSyntaxAsync(code, options)`（扩展方法） | `SyntaxValidationResult`（`IsValid` / `Errors` / `Warnings` / `FormatErrors()`） | 编译一次并把诊断按严重级别分好 |

```csharp
var syntax = await engine.ValidateSyntaxAsync(WrapRule(body), BuildOptions());
if (!syntax.IsValid)
{
    throw new UserFriendlyException($"规则脚本有语法错误：\n{syntax.FormatErrors()}");
}
```

两点要知道：

- **`CompileAsync` 不走编译缓存**，每次都真编译一遍。校验频率高的话自己节流。
- 编译过程抛出异常（而不是编译报错）时，返回的失败结果 `Diagnostics` 为空、`ErrorMessage` 为空串。看到"编译失败但没有任何错误信息"，说明走的是这条分支。

## 编译缓存：省的是编译，不是加载

缓存挂在引擎实例上，`EnableCache` 默认为 `true`。缓存键取 `CacheKey`（显式指定时优先），否则由这五项算出来：

```text
脚本源码 + ScriptType + Imports + References 的 FullName + ReferencePaths
```

::: warning 缓存键不含编译开关
`AllowUnsafe`、`OptimizationLevel`、`CompilerOptions`、`SecurityOptions`、`Globals` **都不参与缓存键计算**。同一段代码换了这些开关重新执行，会直接复用上一次编译出来的程序集，编译级设置不会重新生效。

好消息是安全校验是在**每次执行**时做的（不是编译期），所以换安全策略仍然有效；受影响的只是 `AllowUnsafe` 和优化级别这类编译期开关。真要区分，用 `WithCacheKey(...)` 手动把它们编进键里。
:::

`ExecuteFileAsync` 会自动用 `file:{路径}:{最后写入时间Ticks}` 作缓存键——改文件自动失效，这点不用操心。

::: danger 命中缓存也不是零成本
即便命中缓存，每次执行仍然会 `Assembly.Load(编译出的字节数组)` 载入一份程序集，而 .NET 里这样载入的程序集**无法卸载**。高频、长期执行会持续累积程序集与元数据内存。

所以：脚本适合"低频 + 逻辑可变"，不适合放在每秒成千上万次的热路径上。真要高频，考虑把脚本结果缓存成委托或直接落成代码。
:::

统计信息可以拿来观察缓存效果：

```csharp
var stats = engine.GetStatistics();
// TotalExecutions / SuccessRate / CacheHitRate / AverageCompilationTimeMs / Uptime
```

## 超时：它保护调用方，保护不了进程

::: danger `ScriptOptions.TimeoutMs` 打断不了已经跑起来的脚本
引擎内部是 `Task.Run(委托, cts.Token)` 再 `await task`。`CancellationToken` 传给 `Task.Run` 只在**委托开始执行之前**起作用；一旦委托跑起来，取消令牌就再也影响不到它，`await` 会老老实实等到脚本自己结束。

也就是说：默认路径下 `TimeoutMs`（默认 30000）几乎只在极端调度延迟时才会触发 `ScriptTimeoutException`。**它不能用来兜底死循环。**
:::

想让调用方到点返回，用扩展方法：

```csharp
try
{
    var result = await engine.ExecuteWithTimeoutAsync(code, timeoutMs: 2000, options);
}
catch (ScriptTimeoutException ex)
{
    // 2 秒后这里一定会走到
}
```

`ExecuteWithTimeoutAsync` 内部用的是 `task.WaitAsync(token)`，到点就把控制权还给你。但要认清代价：**脚本线程还在后台跑到底**，线程池线程和它占的内存都收不回来。一个 `while(true)` 会永久占住一个线程池线程直到进程重启。

结论：超时只是给调用方的 SLA 兜底，不是安全机制。真正的兜底是"不执行不可信代码"。

## 安全边界

::: danger 这不是沙箱
脚本编译出的程序集载入宿主进程、以宿主的完整权限运行。这里的"安全机制"是三层**有限的静态检查**，全部可以被绕过。**不要用它执行不可信代码。**
:::

三层检查各自实际做了什么：

### 1. 文件层（只在 `ExecuteFileAsync` 生效）

| 检查 | 依据 |
| --- | --- |
| 扩展名白名单 | `AllowedFileExtensions`，默认 `.cs` / `.csx` / `.txt` |
| 路径里含 `..`、`~`、`$` 直接拒 | 硬编码 |
| 文件大小上限 | `MaxFileSize`，默认 10MB |

违规抛 `ScriptSecurityException`，`ViolationType` 分别是 `InvalidFileExtension` / `DangerousPath` / `FileTooLarge`。

### 2. 程序集层（每次执行都跑）

编译成功后、调用入口方法前，对 `assembly.GetTypes()` 做反射检查：

| 检查 | 命中条件 |
| --- | --- |
| 不安全代码（`AllowUnsafe=false` 时） | 类型名**包含** `Unsafe`，或有指针类型的字段/参数/返回值 |
| 禁止命名空间 | 类型的 `Namespace` 以 `ForbiddenNamespaces` 中任一项开头 |
| 禁止类型 | 类型的 `FullName` **包含** `ForbiddenTypes` 中任一项 |
| 危险方法 | 方法的 `Name` **包含** `DangerousKeywords` 中任一项 |

::: danger 检查的是"脚本定义了什么"，不是"脚本调用了什么"
`GetTypes()` 只能看到脚本自己声明的类型，`ForbiddenTypes` / `ForbiddenNamespaces` 比对的也是这些类型的名字。

所以脚本里写 `System.Diagnostics.Process.Start(...)` **不会**被这层拦下来——它只声明了 `ScriptClass` 一个类型，`FullName` 是 `"ScriptClass"`，不含任何黑名单项。反过来，比对用的是黑名单里的**完整串**：把自己的类命名成 `MyProcessHelper` 同样不会命中 `ForbiddenTypes`，因为默认项是 `System.Diagnostics.Process` 整串。

同理，`DangerousKeywords` 比对的是脚本自己定义的方法名，不是方法体里的调用——真会误伤的是方法名里恰好含黑名单串的情况，比如方法叫 `MarshalRow` 会命中 `Marshal`（大小写敏感）。
:::

顺带一个真实的误伤：`AllowUnsafe=false`（默认）时，**类名里带 `Unsafe` 字样就会被拒**，哪怕它完全安全。

### 3. 源码预检（可选，纯字符串匹配）

`ValidateSecurityAsync` 扩展方法是**独立于执行期检查的另一套东西**：先编译一次验语法，再在源码文本里 `Contains` 一批关键字，给出 `SecurityRiskLevel`（`Low` / `Medium` / `High`）和问题清单。

```csharp
var check = await engine.ValidateSecurityAsync(code);
if (check.RiskLevel >= SecurityRiskLevel.Medium)
{
    logger.LogWarning("脚本包含敏感操作：{Issues}", check.FormatIssues());
}
```

匹配的关键字分三档：危险（`unsafe` / `DllImport` / `Process.Start` / `Assembly.Load` / `Environment.Exit` …）判 `High`，网络（`HttpClient` / `Socket` / `TcpClient` …）和文件（`File.` / `Directory.` / `FileStream` …）判 `Medium`。字符串拼接就能绕开，**只适合当提交时的提醒，不能当门禁**。

### `SecurityOptions` 里哪些开关真的有用

::: warning 一半开关目前只是数据
| 字段 | 是否被消费 |
| --- | --- |
| `EnableSecurityChecks` | ✅ 总开关，关掉则整层跳过 |
| `AllowedFileExtensions` / `MaxFileSize` | ✅ 仅 `ExecuteFileAsync` 路径 |
| `ForbiddenNamespaces` / `ForbiddenTypes` / `DangerousKeywords` | ✅ 程序集层反射检查 |
| `EnableStrictMode` | ❌ 引擎从不读取 |
| `AllowFileSystemAccess` / `AllowNetworkAccess` / `AllowReflectionAccess` | ❌ 引擎从不读取 |
| `AllowProcessOperations` / `AllowRegistryAccess` / `AllowEnvironmentAccess` | ❌ 引擎从不读取 |

因此 `ScriptOptions.WithStrictSecurity()` 实际只做了一件有效的事：把 `AllowUnsafe` 设为 `false`（而它本来就是 `false`）。它设置的另外四个标志目前不影响任何行为。

要真正收紧，请改**有效的那三个列表**：

```csharp
var options = new ScriptOptions().WithSecurity(s =>
{
    s.ForbiddenNamespaces.Add("MyApp.Infrastructure");
    s.DangerousKeywords.Add("Sql");
});
```

`SecurityOptions.Strict()` 预设相比默认值，真正生效的差异是把 `MaxFileSize` 收到 1MB、扩展名白名单收到 `.cs` / `.csx`——只影响文件执行路径。
:::

### 实际能守住的底线

- 脚本必须由**你自己或受信的运维/实施人员**编写，并走评审或审批流程。
- 存脚本的表要有权限控制和变更审计，改动可追溯到人。
- 用 `ForbiddenNamespaces` 把内部基础设施命名空间挡掉，减少手滑范围。
- 要跑真正不可信的代码，请上进程或容器级隔离——本包提供不了这个层级的保证。

## 配置

::: warning 脚本选项不走 appsettings
`ScriptOptions` 没有配置节名，不绑定 `IConfiguration`，也不经 DI Options 系统。它是**每次调用时作为参数传进去的普通对象**，不传就用 `ScriptOptions.Default`（每次返回一个新实例）。
:::

常用项（完整表格见[包文档](../packages/script)）：

| 字段 | 默认值 | 说明 |
| --- | --- | --- |
| `ScriptType` | `Statement` | 脚本形态，决定包装方式 |
| `TimeoutMs` | `30000` | 见上文「超时」一节的实际效果 |
| `EnableCache` / `CacheKey` | `true` / `null` | 编译缓存开关与自定义键 |
| `Imports` | System / Collections.Generic / Linq / Text / Threading.Tasks | 自动写入的 `using` |
| `References` / `ReferencePaths` | 空 | 附加程序集引用 |
| `Globals` | 空 | 按入口方法参数名匹配注入 |
| `AllowUnsafe` | `false` | 允许不安全代码 |
| `OptimizationLevel` | `Debug` | 长期跑的脚本建议 `WithOptimization()` 切 `Release` |
| `SecurityOptions` | 见上 | 安全约束 |

::: danger `ScriptOptions` 是可变对象，别共享
所有 `WithXxx` / `AddXxx` 都是在**当前实例上原地修改**再 `return this`，不是复制。后果：

- 把一个共享的 options 传给 `EvaluateAsync`，它的 `ScriptType` 会被永久改成 `Expression`。
- 传给 `ExecuteFileAsync`，它会被写上一个 `CacheKey`。
- 传给 `ExecuteWithTimeoutAsync`，它的 `TimeoutMs` 会被改掉。

要么每次调用前新建，要么用工厂方法产出：

```csharp
private static ScriptOptions BuildOptions() => new ScriptOptions()
    .WithScriptType(ScriptType.Method)
    .WithTimeout(2000);
```
:::

::: danger 构建器和工厂的配置不会传给引擎
`ScriptEngineBuilder` 的 `AddReference` / `AddImport` / `AddGlobal` / `WithTimeout` / `WithOptimization` / `WithUnsafe` / `DisableCache` 只写进构建器内部持有的一份 `ScriptOptions`，而 `Build()` 只是 `new ScriptEngine()` 后执行 `Configure(Action<IScriptEngine>)` 注册的委托——那份 options 根本不会交给引擎。`ScriptEngineFactory.Create(configure)` 同理，`configure` 参数当前被忽略。

所以：**一切执行期配置只能通过 `Execute*` / `Evaluate*` 的 `options` 参数传**。链式调完构建器就以为配好了，是最难查的一类"设置没生效"。
:::

`CompilerOptions` 里同样有一部分只是数据：引擎实际读取 `LanguageVersion`（默认 `Latest`）、`WarningLevel`（4）、`TreatWarningsAsErrors`、`GenerateDebugInfo`、`DebugInformationFormat`；而 `PreprocessorSymbols`、`WarningsAsErrors`、`WarningsNotAsErrors`、`DisabledWarnings` 目前不参与编译。

## 一个完整的接法：规则存库，运行期执行

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Script;
using XiHan.Framework.Script.Core;
using XiHan.Framework.Script.Enums;
using XiHan.Framework.Script.Extensions;
using XiHan.Framework.Script.Options;

public class DiscountRuleEvaluator(IScriptEngine engine)
{
    /// <summary>保存规则前校验</summary>
    public async Task<string?> ValidateAsync(string ruleBody)
    {
        var syntax = await engine.ValidateSyntaxAsync(Wrap(ruleBody), BuildOptions());
        return syntax.IsValid ? null : syntax.FormatErrors();
    }

    /// <summary>执行规则，失败时回退到原价</summary>
    public async Task<decimal> EvaluateAsync(string ruleBody, decimal amount, int level)
    {
        var options = BuildOptions()
            .AddGlobal("amount", amount)
            .AddGlobal("level", level);

        var result = await engine.ExecuteAsync<decimal>(Wrap(ruleBody), options);
        return result.IsSuccess ? result.Value : amount;
    }

    // 规则体只写方法体，壳子由这里补，运营就不用关心签名
    private static string Wrap(string ruleBody)
    {
        return "public static object Execute(decimal amount, int level)\n{\n" + ruleBody + "\n}";
    }

    private static ScriptOptions BuildOptions()
    {
        return new ScriptOptions()
            .WithScriptType(ScriptType.Method)
            .WithTimeout(2000)
            .WithSecurity(s => s.ForbiddenNamespaces.Add("MyApp.Infrastructure"));
    }
}
```

注册（引擎做成单例，编译缓存才能跨请求复用）：

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    context.Services.TryAddSingleton<IScriptEngine>(_ => new ScriptEngine());
    context.Services.AddTransient<DiscountRuleEvaluator>();
}
```

运营写的规则体长这样：

```csharp
if (level >= 3) return amount * 0.8m;
if (amount > 1000m) return amount * 0.9m;
return amount;
```

## 和工作流的联动

`XiHanWorkflowModule` 依赖 `XiHanScriptModule`，内置了一个 C# 脚本活动：节点上配 `Code` 属性（方法体代码），通过 `variables` 字典读写流程实例变量，配 `ResultVariable` 则把 `return` 的值写进指定变量。脚本对 `variables` 的修改会在执行成功后合并回实例变量。

想在工作流里嵌一小段动态逻辑，用现成的脚本活动比自己接引擎省事。细节见[工作流包文档](../packages/workflow)。

## 包里还有什么，以及它们的成色

| 组件 | 当前状态 |
| --- | --- |
| `ScriptTemplateManager` / `ScriptTemplate` | 可用。参数化脚本模板，`Code` 里用 `#{参数名}` 占位，`GenerateCode(...)` 做纯字符串替换（不是模板引擎），`ValidateParameters` 支持必填/范围/正则/枚举校验 |
| `ScriptMonitor` / `ScriptMonitorOptions` | 可用但**不自动接线**。引擎不会调用它，需要你在每次执行后自己 `monitor.LogExecution(result, code, path)`；之后才能取统计、订阅事件、导出日志 |
| `DebugOptions` / `Breakpoint` / `DebugLevel` / `HitCountCondition` | **未接入**。是公开的数据模型，但 `ScriptOptions` 和 `ScriptEngine` 都不引用它们——设断点、调调试级别当前对执行没有任何影响，不要基于它们设计功能 |

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 「未找到有效的入口点」 | `Class` / `Program` 脚本的类名不是 `ScriptClass`，或没有 `public static Execute` |
| `Globals` 传的值脚本里拿不到 | 用了 `Statement` / `Expression`（包出来的 `Execute()` 无参）；改用 `Method` 并让参数名对上键名 |
| `Program` 脚本报找不到 `List` 之类 | `Program` 不会自动写 `using` 头，`Imports` / `AddImport` 对它无效，脚本里自己写 |
| 编译报找不到业务类型 | 默认只挂了几个基础程序集，要 `AddReference(typeof(X))` + `AddImport("命名空间")` |
| `EvalAsync` 一直返回 `null` | 它失败时就返回 `null`，看不到原因；换 `ExecuteAsync` 读 `ErrorMessage` / `Diagnostics` |
| `ScriptResult<T>.Value` 是 `default` 但 `IsSuccess` 是 `true` | 脚本返回的类型和 `T` 对不上，取值时静默降级为 `default` |
| 死循环脚本没有被超时中断 | `TimeoutMs` 打断不了运行中的委托，用 `ExecuteWithTimeoutAsync` 让调用方返回（脚本线程仍在跑） |
| 类名带 `Unsafe` 被判违规 | `AllowUnsafe=false` 时按类型名字符串匹配，改类名 |
| 脚本调 `Process.Start` 没被拦住 | 安全检查只看脚本**声明**的类型/方法名，不看调用；这是设计上的边界，不是配置问题 |
| 改了 `AllowUnsafe` / 优化级别没生效 | 这两项不参与缓存键，命中缓存直接复用旧程序集；`ClearCache()` 或用 `WithCacheKey` 区分 |
| 构建器上配的引用/超时全部不生效 | `Build()` 不传那份 options，配置要通过每次调用的 `options` 参数给 |
| 长跑服务内存缓慢上涨 | 每次执行都 `Assembly.Load` 且程序集不可卸载，降低执行频率或改用编译后的委托 |
| 「编译失败」但没有任何错误信息 | 编译过程本身抛了异常，此时诊断为空、`ErrorMessage` 为空串 |
| 注入不到 `IScriptEngine` | `XiHanScriptModule` 不注册服务，自己 `TryAddSingleton<IScriptEngine>(_ => new ScriptEngine())` |

## 下一步

- [脚本引擎包文档](../packages/script)：完整 API 清单与全部配置项
- [模块化](./modularity)：模块依赖与 `DependsOn`
- [依赖注入](./dependency-injection)：把引擎注册成单例的几种写法
- [工作流包文档](../packages/workflow)：内置 C# 脚本活动
- [常见问题](./faq)：框架级排查清单
