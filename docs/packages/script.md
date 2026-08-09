# XiHan.Framework.Script

> C# 脚本引擎：基于 Roslyn（`Microsoft.CodeAnalysis.CSharp`）在内存中动态编译并执行 C# 脚本，附带编译缓存、执行超时与编译后静态安全校验。

- **NuGet**：`XiHan.Framework.Script`
- **模块类**：`XiHanScriptModule`（不注册额外服务，仅占位）
- **所在层**：基础设施层
- **关键依赖**：`Microsoft.CodeAnalysis.CSharp`（Roslyn，5.6.0）+ 框架内部依赖（[XiHan.Framework.Core](./core)）。

## 概述

XiHan.Framework.Script 让你在运行时把一段 C# 源码字符串编译成程序集并执行，得到强类型结果。底层用 Roslyn 在内存中编译，支持表达式、语句块、方法、类、完整程序等多种脚本形态，并内置编译缓存、执行超时、安全校验与执行统计，适合规则计算、动态公式、可配置逻辑这类"代码即配置"的场景。

> **仅支持 C#**。源码与依赖里没有 JavaScript / Python 引擎——请勿据此声称支持其它语言。

> **关于「沙箱」的措辞**：本包的安全机制是"编译成程序集后用反射做静态检查（禁止的命名空间/类型、不安全代码、危险方法名）+ 执行超时"，**不是 OS/进程级隔离**。脚本运行在宿主进程内，能通过反射拦截的黑名单是有限集合；不要把它当作运行不可信任意代码的安全边界。

## 何时使用

- 需要在运行期执行配置/用户提供的 C# 表达式或脚本（动态规则、计算公式）。
- 想把易变的业务逻辑外置为脚本，改逻辑无需重新发布。
- 需要对脚本执行做超时、命名空间/类型黑名单等约束。
- 需要缓存已编译脚本、复用编译结果以提升重复执行性能。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Script
```

```csharp
[DependsOn(typeof(XiHanScriptModule))]
public class MyModule : XiHanModule { }
```

`XiHanScriptModule.ConfigureServices` **当前不注册任何服务**。使用不依赖 DI——直接用静态门面 `XiHanScript`、工厂 `ScriptEngineFactory` 或构建器 `ScriptEngineBuilder` 即可。

## 工作原理

1. **包装**：按 `ScriptOptions.ScriptType` 把源码包进一个 `ScriptClass`。`Statement` 会注入 `object result = null;`，末尾 `return result;`——所以语句脚本里给 `result` 赋值即为返回值；`Expression` 直接 `return {表达式};`；`Method` / `Class` 原样嵌入；`Program` 不包装。
2. **编译**：Roslyn 用 `CompilerOptions`（语言版本、优化级别、调试信息等）编译到内存程序集；失败返回带诊断的 `CompilationResult`。
3. **缓存**：按脚本内容与选项生成缓存键，命中则复用已编译程序集（结果 `FromCache=true`）。
4. **安全校验**：若 `SecurityOptions.EnableSecurityChecks`，对编译出的程序集 `assembly.GetTypes()` 做反射检查——命中禁止命名空间/类型/不安全代码/危险方法名则抛 `ScriptSecurityException`。
5. **执行 + 超时**：反射调用入口方法，用 `CancellationTokenSource(TimeoutMs)` 控制时长，超时抛 `ScriptTimeoutException`；结果封装为 `ScriptResult` / `ScriptResult<T>`（含执行/编译耗时、内存占用、诊断）。

## 核心能力

- **动态执行**：`ExecuteAsync` / `EvaluateAsync` 执行脚本或求值表达式，`ExecuteFileAsync` 执行脚本文件，`CompileAsync` 只编译，`CreateInstanceAsync<T>` 创建脚本类实例。
- **多种脚本形态**：`ScriptType` = `Statement`（默认）/ `Expression` / `Method` / `Class` / `Program`。
- **强类型结果**：`ScriptResult<T>`（`IsSuccess` / `Value` / `ErrorMessage` / `Exception` / `Diagnostics` / `ExecutionTimeMs` / `CompilationTimeMs` / `FromCache`）。
- **编译缓存**：内容+选项键，命中复用，`ClearCache()` 清理，`GetStatistics()` 取命中率等。
- **超时控制**：`TimeoutMs`（默认 30000ms）→ `ScriptTimeoutException`。
- **静态安全校验**：黑名单命名空间/类型 + 危险关键字 + 不安全代码检测；`SecurityOptions` 提供 `Strict` / `Permissive` / `Disabled` 预设。
- **构建与工厂**：`ScriptEngineBuilder` 提供 `AddReference/AddImport/AddGlobal/WithTimeout/WithOptimization/WithUnsafe/DisableCache` 链式方法；`ScriptEngineFactory` 创建默认/命名引擎并管理生命周期与统计。**已知限制**见下方「使用示例」。
- **扩展方法**（`ScriptEngineExtensions`）：同步门面 `Execute`/`Execute<T>`/`ExecuteFile`/`ExecuteFile<T>`/`Compile`/`CreateInstance`/`Evaluate`/`Evaluate<T>`；失败即抛异常的 `ExecuteOrThrowAsync`/`ExecuteOrThrowAsync<T>`/`CompileOrThrowAsync`；批量执行 `ExecuteBatchAsync`（可选并行）；基准测试 `BenchmarkAsync`（预热 5 次后统计，返回 `PerformanceStatistics`）；`ExecuteWithTimeoutAsync`；全异常兜底 `ExecuteSafelyAsync`/`ExecuteFileSafelyAsync`（把各类 `ScriptException` 转换成失败态 `ScriptResult`，不再抛出）；启发式安全扫描 `ValidateSecurityAsync`（按危险/网络/文件关键字字符串匹配打分，返回 `SecurityValidationResult` + `SecurityRiskLevel`，与执行期反射安全校验是两套独立机制，仅供预检参考）；纯编译语法检查 `ValidateSyntaxAsync`（返回 `SyntaxValidationResult`）。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `IScriptEngine` / `ScriptEngine` | 引擎接口与 Roslyn 实现。`ExecuteAsync`/`ExecuteAsync<T>`、`ExecuteFileAsync`/`ExecuteFileAsync<T>`、`CompileAsync`、`CreateInstanceAsync<T>`、`EvaluateAsync`/`EvaluateAsync<T>`、`ClearCache`、`GetStatistics` |
| `XiHanScript`（静态门面） | `RunAsync`/`Run`、`RunAsync<T>`/`Run<T>`、`EvalAsync`/`Eval`、`EvalAsync<T>`/`Eval<T>`、`RunFileAsync`/`RunFile`、`CreateBuilder()`；`Engine` 属性为惰性默认引擎 |
| `ScriptEngineFactory`（静态） | `CreateDefault()`、`Create(Action<ScriptOptions>?)`、`GetOrCreate(name, ...)`、`Release/ReleaseAll`、`GetStatistics/GetAllStatistics`、`ClearAllCaches/ClearCache` |
| `ScriptEngineBuilder` | 流式：`AddReference(...)`、`AddImport(...)`、`AddGlobal(...)`、`WithTimeout(...)`、`WithOptimization()`、`WithUnsafe()`、`DisableCache()`、`Configure(...)`、`Build()`。**注意**：目前只有 `Configure(Action<IScriptEngine>)` 会真正作用于 `Build()` 出的引擎，其余链式方法收集的配置不会自动生效，见下方示例 |
| `ScriptEngineExtensions`（静态扩展方法） | 同步门面 + `ExecuteOrThrowAsync`/`CompileOrThrowAsync` + `ExecuteBatchAsync` + `BenchmarkAsync` + `ExecuteWithTimeoutAsync` + `ExecuteSafelyAsync`/`ExecuteFileSafelyAsync` + `ValidateSecurityAsync`/`ValidateSyntaxAsync` |
| `ScriptOptions` | 执行选项（见下） |
| `CompilerOptions` | 编译选项：`LanguageVersion`（默认 Latest）、`WarningLevel`（4）、`TreatWarningsAsErrors`、`GenerateDebugInfo`、`DebugInformationFormat` 等 |
| `SecurityOptions` | 安全选项（见下） |
| `ScriptResult` / `ScriptResult<T>` / `CompilationResult` | 执行结果与编译结果 |
| `ScriptType`（enum） | `Statement` / `Expression` / `Class` / `Method` / `Program` |
| `ScriptMonitor` + `ScriptMonitorOptions` | 独立的执行日志/性能监控组件（见「扩展点 / 自定义」） |
| `ScriptTemplateManager` + `ScriptTemplate` | 参数化脚本模板管理（见「扩展点 / 自定义」） |
| `ScriptException` 及子类 | `ScriptCompilationException` / `ScriptExecutionException`（`ScriptTimeoutException` 继承自它）/ `ScriptSecurityException` / `ScriptLoadException` |

## 配置

`ScriptOptions` 需要显式构造（或用 `ScriptOptions.Default` + 链式方法）后作为参数传入每次 `Execute*/Evaluate*` 调用，**不绑定 appsettings 配置节**（无 `SectionName`）；参见上方「`ScriptEngineBuilder` 的已知限制」，链式配置不能只经构建器组装就指望生效。

### `ScriptOptions` 关键字段

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `ScriptType` | `ScriptType` | `Statement` | 脚本形态 |
| `Imports` | `List<string>` | System / Collections.Generic / Linq / Text / Threading.Tasks | 默认导入的命名空间 |
| `References` / `ReferencePaths` | `List<Assembly>` / `List<string>` | 空 | 附加程序集引用 |
| `Globals` | `Dictionary<string, object?>` | 空 | 全局变量——仅在反射调用入口方法时按**参数名**匹配注入实参，需配合 `ScriptType.Method` 声明同名参数；`Statement`/`Expression`/`Class` 脚本内不能以 `Globals[...]` 形式直接访问，详见下方示例 |
| `EnableCache` / `CacheKey` | `bool` / `string?` | `true` / `null` | 编译缓存开关与自定义键 |
| `TimeoutMs` | `int` | `30000` | 执行超时（毫秒）|
| `AllowUnsafe` | `bool` | `false` | 允许不安全代码 |
| `OptimizationLevel` | `OptimizationLevel` | `Debug` | 优化级别 |
| `CompilerOptions` | `CompilerOptions` | 见上 | 编译器细项 |
| `SecurityOptions` | `SecurityOptions` | 见下 | 安全约束 |

链式方法：`AddReference/AddImport/AddGlobal`、`WithScriptType/WithTimeout/WithCacheKey`、`DisableCache/WithOptimization/WithUnsafe`、`WithSecurity/WithStrictSecurity/DisableSecurity`。

### `SecurityOptions` 关键字段

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `EnableSecurityChecks` | `bool` | `true` | 总开关，关闭则跳过所有校验 |
| `EnableStrictMode` | `bool` | `false` | 严格模式 |
| `AllowFileSystemAccess` / `AllowNetworkAccess` / `AllowReflectionAccess` | `bool` | `true` | 各类访问许可 |
| `AllowProcessOperations` / `AllowRegistryAccess` | `bool` | `false` | 进程/注册表操作 |
| `AllowEnvironmentAccess` | `bool` | `true` | 环境变量访问 |
| `MaxFileSize` | `long` | 10MB | 脚本文件大小上限 |
| `AllowedFileExtensions` | `List<string>` | `.cs/.csx/.txt` | 允许的脚本文件扩展名 |
| `ForbiddenNamespaces` | `List<string>` | `Reflection.Emit` / `InteropServices` / `Security.Permissions` / `Diagnostics.Process` | 禁止命名空间 |
| `ForbiddenTypes` | `List<string>` | `Process` / `Registry` / `Environment` | 禁止类型 |
| `DangerousKeywords` | `List<string>` | `unsafe`/`fixed`/`stackalloc`/`DllImport`/`Marshal`/`Assembly.Load`/`Activator.CreateInstance`/`Process.Start`/`Environment.Exit` | 危险关键字/方法名 |

预设：`SecurityOptions.Strict()`（全面收紧）、`Permissive()`（放开）、`Disabled()`（关闭检查）。

## 使用示例

### 求值表达式与执行语句

```csharp
// 求值表达式（自动切到 Expression 类型）
int sum = await XiHanScript.EvalAsync<int>("1 + 2 * 3"); // 7

// 执行语句块：给内置的 result 变量赋值即为返回值
var result = await XiHanScript.RunAsync<int>("result = 10 * 20;");
if (result.IsSuccess)
{
    Console.WriteLine(result.Value); // 200
}
```

### 用 `ScriptOptions` 定制超时等执行参数

> **`ScriptEngineBuilder` 的已知限制**：`AddReference`/`AddImport`/`AddGlobal`/`WithTimeout`/`WithOptimization`/`WithUnsafe`/`DisableCache` 只是把配置写进构建器内部持有的一份 `ScriptOptions`，但 `Build()` 目前**不会**把这份 `ScriptOptions` 传给新建的引擎——`Build()` 内部就是 `new ScriptEngine()` 后执行通过 `Configure(Action<IScriptEngine>)` 注册的委托（而 `IScriptEngine` 本身也没有暴露可设置引用/导入/全局变量的成员）。也就是说，链式调用这些方法目前**不会**对后续的 `Execute*/Evaluate*` 生效。要让引用、导入、全局变量、超时真正生效，请直接构造/复用 `ScriptOptions` 并在每次调用时传入：

```csharp
var options = new ScriptOptions().WithTimeout(5000);

var engine = XiHanScript.CreateBuilder().Build(); // 等价于 ScriptEngineFactory.CreateDefault()
var r = await engine.ExecuteAsync<int>("result = 10 * 2;", options);
```

### 用 `ScriptType.Method` 接收全局变量

`Globals` 不是脚本内可直接访问的字典，而是执行引擎在反射调用入口方法时按**参数名**匹配、逐个填充成实参。要让全局变量真正传入脚本，脚本必须是 `ScriptType.Method`，并自己声明一个名为 `Execute` 的 `public static` 方法、用参数名对应 `Globals` 的键：

```csharp
var options = new ScriptOptions()
    .WithScriptType(ScriptType.Method)
    .AddGlobal("factor", 3);

var result = await XiHanScript.RunAsync<int>(
    "public static object Execute(int factor) { return 10 * factor; }", options);
// result.Value == 30
```

### 收紧安全策略

```csharp
var options = ScriptOptions.Default
    .WithStrictSecurity()   // 禁文件/网络/反射，禁不安全代码
    .WithTimeout(2000);

var res = await XiHanScript.RunAsync("result = System.Environment.MachineName;", options);
// 命中 ForbiddenTypes(Environment) → res.IsSuccess=false，抛/记 ScriptSecurityException
```

## 扩展点 / 自定义

- **命名引擎复用**：`ScriptEngineFactory.GetOrCreate("rules")` 复用同名引擎（含其编译缓存），`Release/ReleaseAll` 释放。
- **自定义安全策略**：构造 `SecurityOptions` 或用 `Strict/Permissive/Disabled` 预设，再经 `ScriptOptions.WithSecurity(...)` 调整黑名单。
- **附加引用/导入**：需要脚本访问业务类型时用 `ScriptOptions` 的 `AddReference(typeof(X))` / `AddImport("Your.Namespace")`（注意上面「已知限制」，不要经 `ScriptEngineBuilder` 配置后误以为已生效）。
- **脚本模板**：`ScriptTemplateManager` 构造时自动加载 3 个内置系统模板（`HelloWorld`/`MathCalculator`/`DataProcessor`，`IsSystem=true`，不可删除/覆盖保存），并从模板目录（默认 `AppDomain.CurrentDomain.BaseDirectory/Templates`，可在构造函数传入）加载 `*.json` 自定义模板；`ScriptTemplate.Code` 用 `#{参数名}` 占位符，`GenerateCode(parameters)` 做纯字符串替换（不是 Scriban/正则模板引擎），`ValidateParameters` 按 `TemplateParameter`（`Required`/`MinValue`/`MaxValue`/`Pattern`/`Options` 等）做必填、数值范围、正则、枚举取值校验。
- **执行监控**：`ScriptMonitor`（配 `ScriptMonitorOptions`，预设 `Default`/`HighPerformance`/`Verbose`）是独立组件，**不会**被 `ScriptEngine`/`XiHanScript` 自动调用——需要调用方自己在执行后调 `monitor.LogExecution(result, scriptCode, scriptPath)` 记录；随后可用 `GetExecutionLogs`/`GetStatistics()`（`ScriptExecutionStatistics`，含成功率、缓存命中率、最近一小时执行数、最慢脚本等）/`GetPerformanceInfo()`、订阅 `ScriptExecuted`/`PerformanceWarning` 事件，或 `ExportLogsAsync` 导出 JSON/CSV/XML（`LogExportFormat`）。
- **调试选项（预留，未接入引擎）**：`DebugOptions`（含 `Verbose()`/`Production()` 预设）、`Breakpoint`、`DebugLevel`、`HitCountCondition` 是独立的公开数据模型，当前未被 `ScriptOptions`/`ScriptEngine` 引用或消费——即设置断点/调试级别目前不会对脚本执行产生任何实际效果，不要据此设计依赖真实调试能力的功能。

## 注意事项与最佳实践

- **不是安全沙箱**：静态黑名单可被绕过（如经字符串拼接/间接调用），且脚本在宿主进程内运行。**不要用它执行不可信的任意代码**；确需运行不可信代码请用进程/容器级隔离。
- **`Statement` 脚本的返回值**：给内置 `result` 赋值；`Expression` 则直接是表达式的值。
- **超时是软控制**：`ScriptTimeoutException` 依赖协作式取消，纯 CPU 死循环不一定能被及时中断。
- **首次编译有开销**：重复执行相同脚本请保持 `EnableCache=true` 并复用引擎实例，以命中编译缓存。
- **模块不注册服务**：不能注入 `IScriptEngine`；通过 `XiHanScript` / `ScriptEngineFactory` 使用。

## 依赖模块

- [XiHan.Framework.Core](./core) — 唯一的框架内部依赖，提供模块化与依赖注入基座。
- 第三方核心依赖：`Microsoft.CodeAnalysis.CSharp`（Roslyn，5.6.0）。

## 相关模块

- [XiHan.Framework.Templating](./templating) — 模板渲染，与脚本可配合做动态内容生成。
- [XiHan.Framework.Tasks](./tasks) — 任务调度，常见于承载动态脚本化的后台逻辑。
