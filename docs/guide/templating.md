# 模板引擎

把「模板 + 数据」渲染成文本（通知正文、邮件、代码文件）。这一章的重点只有一句话：**`ITemplateService` 的默认引擎是简单占位替换，不是 Scriban**——搞错这一点，模板会被原样输出。

完整 API 与配置项全表见 [Templating 包](../packages/templating)。

## 包里其实有两套引擎

| 引擎 | 类型 | 注册名 | 语法能力 |
| --- | --- | --- | --- |
| `DefaultTemplateEngine` | `ITemplateEngine<string>` | `"String"` | 自研的占位替换 + 单层条件/循环 |
| `ScribanTemplateEngine` | `ITemplateEngine<Template>` | `"Scriban"` | 转发给 Scriban，支持过滤器、函数、成员访问 |

两者都注册进 `ITemplateEngineRegistry`，注册表的键是「**模板类型 + 名称**」——`ITemplateEngine<string>` 和 `ITemplateEngine<Template>` 各有一套默认引擎，互不影响：

```csharp
registry.SetDefaultEngine<string>("String");     // string 模板的默认引擎
registry.SetDefaultEngine<Template>("Scriban");  // Scriban.Template 模板的默认引擎
```

`ITemplateService` 的所有字符串重载内部取的都是 `GetDefaultEngine<string>()`，也就是 `DefaultTemplateEngine`。

::: danger 最容易踩的坑
`TemplatingOptions.DefaultEngine` 的默认值是 `"Scriban"`，但**它不影响任何渲染路径**——`TemplateService` 只认注册表里 `string` 类型的默认引擎。

把 Scriban 语法（<code v-pre>{{ name | string.upcase }}</code>、<code v-pre>{{ user.Name }}</code>）丢给 `ITemplateService`，得到的是原样输出，而且不报错。
:::

## 选型：三条路径怎么选

| 场景 | 用什么 |
| --- | --- |
| 变量都是标量，只需要填空 + 少量条件（通知、短信、消息标题） | 注入 `ITemplateService` |
| 控制台工具、单元测试等不方便走 DI 的地方 | `XiHan.Framework.Templating.Simple` 静态 API |
| 需要过滤器、成员访问、嵌套逻辑（代码生成、复杂报表、富文本邮件） | 原生 Scriban（注入 `ITemplateEngine<Template>` 只用于 `Validate` / `Parse`） |
| 需要多引擎共存 / 接自定义引擎 | `ITemplateEngineRegistry` |

## 安装与启用

```bash
dotnet add package XiHan.Framework.Templating
```

```csharp
[DependsOn(typeof(XiHanTemplatingModule))]
public class MyModule : XiHanModule { }
```

模块做两件事：`ConfigureServices` 调 `AddXiHanTemplating()` 注册全部服务；`OnApplicationInitialization` 把两个引擎注册进注册表并设为各自模板类型的默认引擎。

注册的服务（全部用 `TryAdd*`，可在自己模块里先注册同接口实现来覆盖）：

| 接口 | 生命周期 | 实现 |
| --- | --- | --- |
| `ITemplateEngineRegistry` | Singleton | `TemplateEngineRegistry` |
| `ITemplateContextFactory` | Singleton | `TemplateContextFactory` |
| `ITemplateEngine<string>` / `ITemplateEngine<Template>` | Transient | `DefaultTemplateEngine` / `ScribanTemplateEngine` |
| `ITemplateService` | Scoped | `TemplateService` |
| `ITemplateContextAccessor` | Scoped | `TemplateContextAccessor` |
| `ITemplateInheritanceManager` / `ITemplatePartialManager` | Singleton | 内存字典实现 |
| `ITemplateSecurityAnalyzer` / `ITemplateSecurityChecker` | Singleton | 正则扫描实现 |
| `ITemplateVariableResolver` | Transient | `TemplateVariableResolver` |

::: tip 引擎实例是「事实上的单例」
引擎注册为 Transient，但注册表在应用初始化时各解析一次并长期持有。从注册表拿到的永远是同一个实例，别在引擎实现里放请求级状态。
:::

## 简单占位渲染

```csharp
public class NotificationBuilder(ITemplateService templates)
{
    public Task<string> BuildAsync(string userName, bool isVip) =>
        templates.RenderAsync(
            "你好 {{Name}}！{{if Vip}}感谢你的持续支持。{{endif}}",
            new { Name = userName, Vip = isVip });
}
```

传对象时，变量名 = **公开可读属性名**（`TemplateContextFactory.CreateContext(object)` 反射取的），只取顶层一层。也可以直接传字典：

```csharp
var text = await templates.RenderAsync(
    "共 {{Total}} 条：\n{{for item in Items}}- {{item}}\n{{endfor}}",
    new Dictionary<string, object?>
    {
        ["Total"] = 2,
        ["Items"] = new[] { "订单已创建", "订单已支付" }
    });
```

`DefaultTemplateEngine` 支持且**仅**支持这三种语法：

| 语法 | 说明 |
| --- | --- |
| <code v-pre>{{变量名}}</code> | 字符串精确替换，值取 `ToString()`，null 变空串 |
| <code v-pre>{{if 条件}}…{{else}}…{{endif}}</code> | 条件仅支持 `==`、`!=` 和「变量真值判断」 |
| <code v-pre>{{for 项 in 集合}}…{{endfor}}</code> | 集合必须是 `IEnumerable` |

渲染顺序固定为：**先条件 → 再循环 → 最后变量替换**。

::: warning 占位符里不能有空格
替换用的占位串是把键名直接夹在两对花括号之间拼出来的，<code v-pre>{{ Name }}</code>（带空格）匹配不上，会原样留在输出里。这是「模板没生效」最常见的原因。
:::

其它硬限制，都源于它是正则/字符串替换实现：

- **没有成员访问**：<code v-pre>{{user.Name}}</code> 不会解析对象，除非变量键名字面上就叫 `user.Name`（见下面上下文构建器的写法）。
- **循环项只能整体输出**：循环体里 <code v-pre>{{item}}</code> 输出的是元素的 `ToString()`，写 <code v-pre>{{item.Title}}</code> 拿不到属性。
- **不支持嵌套**：`if` / `for` 的匹配是非贪婪正则，内层的 <code v-pre>{{endif}}</code> / <code v-pre>{{endfor}}</code> 会被外层先吃掉，嵌套写法结果错乱。
- **条件是字符串比较**：`==` / `!=` 两边先解析字面量（引号字符串、整数、小数、布尔），再按 `ToString()` 做序数比较，没有大小比较。
- **集合缺失不报错**：`for` 的集合变量不存在或不是 `IEnumerable` 时，整段循环体渲染为空串。

## 上下文与作用域

变量、函数、作用域都挂在 `ITemplateContext` 上。用构建器可以把对象铺平成带前缀的键——这也是让简单引擎「看起来支持成员访问」的唯一办法：

```csharp
public class ReportBuilder(ITemplateContextFactory contexts, ITemplateEngine<string> engine)
{
    public string Render(User user)
    {
        var context = contexts.CreateBuilder()
            .AddVariable("Title", "月度报表")
            .AddObject(user, "user")   // 生成 user.Name、user.Email 等键
            .Build();

        return engine.Render("{{Title}} — {{user.Name}}", context);
    }
}
```

`PushScope()` 返回 `IDisposable`，作用域内 `SetVariable` 写到栈顶，`GetVariable` 自栈顶向下逐层查找，`Dispose` 后栈顶变量失效。

::: warning 函数在两个引擎里基本都用不上
`SetFunction` / `AddFunction` 注册的委托：`DefaultTemplateEngine` 完全不读；`ScribanTemplateEngine` 是遍历**变量名**去取同名函数，因此只有存在同名变量时函数才会被拷进 Scriban 上下文。

要在模板里调函数，请自建 Scriban 上下文（见下一节）。
:::

`ITemplateVariableResolver` 能解析 `user.profile.name`、`items[0].name` 这类路径表达式，但两个内置引擎都不调用它——它是给你自己写引擎/自定义解析时用的工具。

## 需要完整语法时：直接用 Scriban

当模板里出现过滤器、成员访问、嵌套循环时，就该离开 `ITemplateService`。推荐直接用原生 Scriban，控制权最完整：

```csharp
using Scriban;
using Scriban.Runtime;

var template = Template.Parse(templateSource);
if (template.HasErrors)
{
    var message = string.Join("; ", template.Messages.Select(item => item.Message));
    throw new InvalidOperationException($"模板解析失败：{message}");
}

var script = new ScriptObject();
script.SetValue("ClassName", "SysUser", true);
script.SetValue("Columns", columns, true);

// 关闭成员重命名，模板里就能用 PascalCase 访问成员
var context = new Scriban.TemplateContext { MemberRenamer = member => member.Name };
context.PushGlobal(script);

var result = await template.RenderAsync(context);
```

::: tip 两个 `TemplateContext` 同名
`Scriban.TemplateContext` 和 `XiHan.Framework.Templating.Contexts.TemplateContext` 类名相同，同时 `using` 两个命名空间必须写全名或 using 别名。`ScribanTemplateEngine` 里的 `using TemplateContext = XiHan.Framework.Templating.Contexts.TemplateContext;` 别名指向的是框架自己的那个类型——这正是下面那条告警的成因。
:::

::: danger 封装的 Scriban 引擎收不到 `ITemplateContext` 里的变量
`ScribanTemplateEngine` 渲染前新建的是框架自己的 `Contexts.TemplateContext`，把它交给 `Template.RenderAsync(...)` 时命中的是 `RenderAsync(object model, …)` 这个重载——整个上下文对象被当成 Scriban 的**模型对象**反射展开，`SetVariable` 写进去的变量在模板里一个都取不到，结果是空值而不是报错。
:::

所以注入 `ITemplateEngine<Template>` 只适合拿来做 `Validate` / `Parse`，渲染要自己把模型交给 Scriban：

```csharp
public class ScribanRenderer(ITemplateEngine<Template> scriban)
{
    public async Task<string> RenderAsync(string source, object model)
    {
        var validation = scriban.Validate(source);   // Parse 本身不检查 HasErrors，先自己校验
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.ErrorMessage);
        }

        return await scriban.Parse(source).RenderAsync(model);
    }
}
```

这样传模型没有设置 `MemberRenamer`，对象成员在模板里按 Scriban 的默认命名规则（PascalCase 转 snake_case）访问；要在模板里保留 PascalCase 就用上面的原生写法。

## 语法校验

| 调用 | 实际校验什么 |
| --- | --- |
| `ITemplateService.ValidateTemplate(source)` | 走 String 引擎：只数 <code v-pre>{{</code> / <code v-pre>}}</code>、`if`/`endif`、`for`/`endfor` 是否成对 |
| `ITemplateEngine<Template>.Validate(source)` | 走 Scriban：真正解析，失败时带 `ErrorLine` / `ErrorColumn` |

两者都返回 `TemplateValidationResult`（`IsValid` / `ErrorMessage` / `ErrorLine` / `ErrorColumn`）。

::: warning 校验缓存没有上限
`DefaultTemplateEngine` 把校验结果缓存在一个以**整段模板源码**为键的字典里，没有淘汰策略。对动态拼接出来的模板反复调用 `ValidateTemplate`，会让这份缓存持续增长——用户可编辑的模板请只在保存时校验一次。
:::

## 免 DI 的静态 API

`XiHan.Framework.Templating.Simple` 是一套完全独立的静态实现，不经过 DI、注册表和 `ITemplateService`，语法与简单引擎一致：

```csharp
using XiHan.Framework.Templating.Simple;

var text = TemplateEngine.Render("Hello {{name}}!", new { name = "xihan" });

var list = TemplateEngine.RenderAdvanced(          // Render 只替换变量，条件/循环要用 RenderAdvanced
    "{{for item in items}}- {{item}}\n{{endfor}}",
    new Dictionary<string, object?> { ["items"] = new[] { "A", "B" } });

var html = FileTemplateHelper.RenderFile("template.html", new { title = "首页" });
await FileTemplateHelper.RenderToFileAsync("in.tpl", "out.html", values);
```

`TemplateCache` 是进程级 `static` 字典，**没有过期、没有容量上限**，与 `TemplatingOptions` 的缓存字段无关，清理要自己调 `RemoveTemplate` / `ClearTemplates`。

::: warning 三套 `RenderTemplate` 扩展方法并存
`Simple.TemplateExtensions` 与 `Engines.DefaultTemplateEngineExtensions` 都定义了签名相同的 `RenderTemplate(this string, …)`，同一个文件里 `using` 两个命名空间会直接编译报 CS0121 二义性。挑一个用。
:::

## 布局与片段

`ITemplateInheritanceManager` / `ITemplatePartialManager` 各自维护一份内存字典，注册进去的布局和片段进程重启即失效：

```csharp
inheritance.RegisterLayout("main", "<html><body>{{- block content -}}{{- endblock -}}</body></html>");

var html = await inheritance.RenderInheritedTemplateAsync(
    """{{ extends "main" }}{{ block content }}你好 {{Name}}{{ endblock }}""",
    context);
```

机制要点：

- 子模板的 `extends` / `block` 用宽松正则解析，写 <code v-pre>{{ block x }}</code> 或 <code v-pre>{{- block x -}}</code> 都认。
- **布局里的块标记必须精确写成 <code v-pre>{{- block 名字 -}}</code> 和 <code v-pre>{{- endblock -}}</code>**（带短横线），合并时是按这两个字面串查找的，写成 <code v-pre>{{ block x }}</code> 不会被替换。
- 子模板没覆盖的布局块，标记会原样留在输出里，不会被清掉。
- 合并后的模板交给 **String 引擎**渲染，所以布局里写 Scriban 语法同样无效。
- `ITemplatePartialManager.PrecompileAllPartialsAsync()` 目前是空实现，直接返回完成的任务；模板里写 `include` 也不会被自动展开，片段要靠 `RenderPartialAsync` 显式渲染后自己拼接。

## 安全检查

`ITemplateSecurityChecker` / `ITemplateSecurityAnalyzer` 已注册，但**没有任何渲染路径会自动调用它们**，需要自己在保存/渲染前调：

```csharp
var result = checker.CheckSecurity(templateSource, TemplateSecurityOptions.Strict);
if (!result.IsSecure)
{
    throw new InvalidOperationException(
        string.Join("; ", result.Threats.Select(item => item.Description)));
}
```

`TemplateSecurityOptions` 提供 `Default` / `Strict` / `Relaxed` 三个预设，控制模板大小上限、表达式深度、循环次数、是否允许文件访问等。

::: danger 它不是沙箱
检查逻辑是对模板文本做正则扫描（匹配 `System.IO.`、`System.Reflection.`、`Process.` 这类片段）并统计规模，不做语义分析、也不干预渲染。不要靠它来隔离不受信任的模板；用户可编辑的模板应当限定在简单引擎的三种语法内。
:::

## 配置

`AddXiHanTemplating()` 用代码 `AddOptions<TemplatingOptions>().Configure(...)` 写死默认值，**没有绑定任何配置节**（`TemplatingOptions` 也没有 `SectionName`）。要覆盖就在应用侧再 `Configure<TemplatingOptions>`。

::: warning `TemplatingOptions` 目前基本是声明位
全仓只有 `TemplateService` 的构造函数接收了这个选项对象并存为字段，**没有任何引擎或服务读取它的字段**。`EnableCaching`、`RenderTimeout`、`MaxTemplateSize`、`EnableSecurityChecks`、`EnablePrecompilation`、`TemplateRootDirectory` 等改了都不会改变运行时行为——真正的超时、缓存上限、自动安全检查尚未接线。

需要这些能力，目前只能自己在调用侧实现（例如自己读 `RenderTimeout` 包一层 `CancellationTokenSource`）。
:::

字段全表见 [Templating 包 → 配置](../packages/templating#配置)。

## 还没有实现的部分

写扩展前先知道哪些是空壳，避免照着接口名去找实现：

| 命名空间 / 类型 | 现状 |
| --- | --- |
| `Compilers`（`ITemplateCompiler<T>` / `ITemplatePrecompiler` / `IExpressionTreeCompiler`） | 只有接口和 DTO，**没有任何实现类**，也未注册进 DI |
| `Parsers`（`ITemplateParser` / `ITemplateAstBuilder` / `ITemplateNode` 等） | 同上，纯预留抽象 |
| `IPartialTemplateProvider` / `IFileSystemPartialProvider` / `ILayoutTemplateResolver` / `IPartialTemplateRegistry` | 未注册，内置的继承/片段管理器也不会调用它们；`MemoryPartialProvider` 是唯一实现，同样未注册 |
| `ITemplateBlockParser` / `ITemplateInheritanceValidator` | 只有接口定义 |

要接文件系统存储模板、或做预编译，需要自己实现并接线。

## 接自定义引擎

```csharp
public class MyEngine : ITemplateEngine<string> { /* Render / RenderAsync / Parse / Validate */ }

// 在模块的 OnApplicationInitialization 里
registry.RegisterEngine("My", new MyEngine());
registry.SetDefaultEngine<string>("My");   // 之后 ITemplateService 就走你的引擎了
```

`SetDefaultEngine<string>` 是改变 `ITemplateService` 行为的**唯一开关**——想让整个应用的字符串渲染切到别的实现，改这里，而不是改 `TemplatingOptions.DefaultEngine`。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 模板原样输出，占位符没被替换 | 占位符里带了空格；简单引擎只匹配严格的 <code v-pre>{{键}}</code> |
| Scriban 过滤器 / 管道写法不生效 | `ITemplateService` 走的是 String 引擎，不解析 Scriban |
| <code v-pre>{{user.Name}}</code> 没被替换 | 简单引擎无成员访问；用 `AddObject(obj, "user")` 造出 `user.Name` 键，或改用 Scriban |
| 整段循环消失了 | 集合变量不存在或不是 `IEnumerable`，按空串处理 |
| 嵌套的 if / for 结果错乱 | 非贪婪正则把内层结束标记当成了外层的 |
| 校验通过但渲染结果不对 | `ValidateTemplate` 只数配对，不做语义校验 |
| `SetFunction` 的函数在模板里取不到 | 封装的 Scriban 引擎按变量名找同名函数，无同名变量就不会拷过去 |
| 注入 `ITemplateEngine<Template>` 渲染出一片空白 | 封装引擎把框架上下文当成 Scriban 的模型对象传，上下文变量取不到；渲染改走原生 Scriban |
| 改 `TemplatingOptions` 毫无变化 | 除注册时写入的默认值外，字段未被任何实现读取 |
| 布局的 block 没被替换 | 布局里必须写成 <code v-pre>{{- block x -}}</code> / <code v-pre>{{- endblock -}}</code> |
| `using` 后 `RenderTemplate` 报 CS0121 | 同时引入了 `Simple` 与 `Engines` 两套同签名扩展方法 |
| 从文件渲染抛 `FileNotFoundException` | `RenderFileAsync` / `FileTemplateHelper` 找不到文件即抛，不做回退 |

## 下一步

- [配置系统](./configuration)：`Configure<TemplatingOptions>` 的覆盖时机
- [依赖注入](./dependency-injection)：`TryAdd*` 与覆盖内置实现
- [扩展与二次开发](./extending)：替换框架内置服务的通用做法
- [Templating 包](../packages/templating)：完整 API 清单与配置项全表
- [Bot 包](../packages/bot)：框架内实际消费 `ITemplateService` 的例子
