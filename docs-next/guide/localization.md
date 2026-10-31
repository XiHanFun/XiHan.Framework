# 国际化

本章讲 5.0 的文案与多语言：消息键、每种语言一个文件的语言文件、`key.L()` 与调用方区域、错误在 Egress 的翻译、`Accept-Language` 协商，以及把翻译放进数据库。适用于给应用加多语言文案、或给能力包写错误消息的开发者。

完整 API 见 [Localization.Abstractions](../packages/localization-abstractions)、[Localization.InMemory](../packages/localization-inmemory) 与 [Localization.Database](../packages/localization-database) 包页。

## 设计

面向人的文案不写在代码里。代码只引用消息键（如 `security.token.subject_missing`），文案按语言放在 `Localization/<区域>.json`，任何地方用 `key.L()` 取当前语言的文本。`Error` 只携带消息键与参数，由宿主在 Egress 按调用方语言翻译。默认语言是英文 `en-US`，它也是所有语言文件的基准。

调用的语言不靠线程文化传递：5.0 不设置 `CultureInfo.CurrentUICulture`，也不替换 `IStringLocalizerFactory`。区域是调用上下文 Baggage 里的 `xihan.locale`，HTTP 宿主从 `Accept-Language` 协商写入，内核在每次调用入口据此安装一个消息作用域 `MessageScope`，于是同一调用链里任何位置的 `key.L()` 都得到调用方语言，下游服务经 Baggage 拿到同一个区域。

装配本地化的是能力 `localization.resources@1`。`Profiles.Minimal` 与 `Profiles.Standard` 都装配默认实现 `InMemoryLocalizationFeature`；需要运营人员在线改译文时换成 `DatabaseLocalizationFeature`。没有装配本地化时，文案退回键与参数的描述，代码照常运行。

## 内置区域

| 区域 | 语言 |
| --- | --- |
| `en-US` | 英语（默认，语言文件的基准） |
| `zh-CN` | 简体中文 |
| `zh-TW` | 繁体中文 |
| `ja-JP` | 日语 |
| `ko-KR` | 韩语 |
| `de-DE` | 德语 |
| `es-ES` | 西班牙语 |
| `fr-FR` | 法语 |
| `pt-BR` | 葡萄牙语（巴西） |
| `ru-RU` | 俄语 |

这 10 种与 XiHan.UI 相同，常量在 `SupportedLocales`。区域名的文法是 `^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$`，最长 35 个字符，比较不区分大小写；`LocaleName.Normalize` 把它规范成语言小写、地区大写、文字子标签首字母大写（`zh-cn` → `zh-CN`，`ZH-hant-tw` → `zh-Hant-TW`）。

## 消息键

消息键是一种专门的代码，文法：

```text
MessageKey = Domain ("." Segment)+
Domain     = Segment
Segment    = [a-z][a-z0-9_]*
```

即 `^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$`，最长 128 个字符（`MessageTemplate.MaxKeyLength`）。首段是领域：框架包用术语表领域词的小写（`kernel`、`persistence`、`security`），应用用自己的小写标识。操作 ID 的资源段带连字符时，键里把连字符换成下划线：操作 `tenant-members.list` 的说明键是 `tenant_members.list.description`（ADR-0038）。

每个包或应用一个 `<领域>Messages` 静态类，只定义键常量，不含文案；键的含义与占位符名写在 XML 文档里。SimpleApp 示例：

```csharp
namespace SimpleApp;

/// <summary>
/// 示例应用的消息键。
/// </summary>
public static class SampleMessages
{
    /// <summary>
    /// 示例资源不存在。
    /// </summary>
    public const string ResourceMissing = "samples.resource.missing";

    /// <summary>
    /// 下单操作的标题，即 MCP tool 的 title 与 OpenAPI 的 summary。
    /// </summary>
    public const string CreateOrderTitle = "orders.create.title";

    /// <summary>
    /// 订单名称只有空白。
    /// </summary>
    public const string OrderNameBlank = "samples.order.name_blank";
}
```

## 语言文件

每种语言一个文件 `Localization/<区域>.json`，内容是键到模板的平面 JSON 对象：

```json
{
  "orders.create.title": "Place an order",
  "samples.order.name_blank": "The order name must not be blank.",
  "samples.resource.missing": "The sample resource does not exist."
}
```

```json
{
  "orders.create.title": "下单",
  "samples.order.name_blank": "订单名称不能只有空白。",
  "samples.resource.missing": "示例资源不存在。"
}
```

文件以固定的逻辑名嵌入程序集：

```xml
<ItemGroup>
  <EmbeddedResource Include="Localization/*.json" LogicalName="Localization/%(Filename)%(Extension)" />
</ItemGroup>
```

`LogicalName` 不能省。`MessageCatalog.FromEmbeddedResources` 只认逻辑名以 `Localization/` 开头、以 `.json` 结尾的资源，文件名就是区域名；不写 `LogicalName` 时资源名会带上根命名空间（`SimpleApp.Localization.zh-CN.json`），目录装载不到它。

模板语法：

::: v-pre
| 写法 | 含义 |
| --- | --- |
| `{name}` | 按名字取参数 |
| `{0}`、`{1}` | 按位置取参数 |
| `{amount:N2}`、`{when:yyyy-MM-dd}` | 带格式说明符，按命中区域的文化格式化（`{amount:N2}` 在 `de-DE` 得到 `1.234,50`） |
| `{{`、`}}` | 输出单个花括号 |
:::

解析是严格的：模板里任一占位符缺值，整条模板都不采用，退回键与参数的描述，不会输出半截文案。

框架自己的包由架构测试 `LocalizationTests` 保证 10 个文件齐全、各区域的占位符与 `en-US` 一致、键常量与 `en-US.json` 一一对应、`Error` 的调用点只用常量。这些检查只扫描框架源码；应用的语言文件要自己保持同步，缺了某个区域的键时按回退链取到其他区域的文案。

## 注册应用的目录

应用的语言文件装成一个 `MessageCatalog` 注册进容器，SimpleApp 的写法：

```csharp
using SimpleApp;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;

// 应用自己的消息模板（SampleMessages）与译文（嵌入的 Localization/<区域>.json）
builder.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("samples", typeof(SampleMessages)));
```

第一个参数是目录名，同名目录只注册一次（多个 Provider 可能都带同一份抽象包的目录）；第二个参数是目录所在程序集里的任意类型。目录不合法（JSON 错误、键或区域名不合法、程序集里没有任何语言文件）时抛出 `LOCALIZATION_VALIDATION_002`，应用启动失败。

各目录由谁注册：

| 目录 | 注册方 |
| --- | --- |
| 原语层 `primitives`、内核 `kernel` | 本地化特性（`InMemoryLocalizationFeature` 或 `DatabaseLocalizationFeature`）代为注册，它们自己不能读嵌入资源 |
| 子系统抽象包（`objectstorage`、`configuration` 等） | 对应的 Provider 在 `Configure` 里注册 |
| 应用、`Plugins.InProcess` 加载的插件 | 应用注册 |

同一个键出现在多个目录里时，在回退链的同一个区域上先注册的目录优先。

## 取文案

### key.L()

消息键常量上的扩展方法 `L` 按当前 `MessageScope` 解析。下面的 `OrdersMessages` 是应用自己的键常量类，`en-US.json` 里的模板写在注释中：

```csharp
public static class OrdersMessages
{
    public const string NotFound = "orders.order.not_found";   // "Order '{id}' was not found."
    public const string Total = "orders.order.total";          // "{0} orders, {1:N2} in total."
    public const string Shipped = "orders.order.shipped";      // "Order '{id}' has shipped."
}
```

```csharp
using XiHan.Framework.Primitives;

var title = SampleMessages.CreateOrderTitle.L();                       // 无参数
var missing = OrdersMessages.NotFound.L(("id", orderId));              // 按名字对应 {id}
var total = OrdersMessages.Total.L(count, amount);                     // 按位置对应 {0}、{1}
var details = MessageArguments.Of(("id", orderId));
var same = OrdersMessages.NotFound.L(details);                         // 参数字典，也可直接作 Error 的 Details
```

当前作用域按下面的规则确定：

- 调用链里（操作、管道中间件、宿主映射到管道里的端点、消息处理器）：内核在 `App.InvokeAsync` 入口按 Baggage `xihan.locale` 安装作用域，得到调用方区域。
- 调用链之外（启动代码、后台线程）：本地化特性启动时设置的进程默认作用域，得到默认区域。
- 都没有，或目录里找不到这个键：返回 `MessageTemplate.Describe(key, arguments)`，形如 `kernel.feature.start_failed (id=…, reason=…)`，键与参数在日志里仍然可见。

需要在调用链之外按指定语言渲染（例如后台任务按收件人语言发邮件）时，临时安装一个作用域，`resolver` 传 `null` 沿用当前的解析器：

```csharp
using (MessageScope.Use(resolver: null, locale: recipientLocale))
{
    var subject = OrdersMessages.Shipped.L(("id", orderId));
}
```

作用域随 `AsyncLocal` 传到子任务，释放时恢复先前的作用域。

### IMessageLocalizer

需要显式区域、或要知道文案实际命中哪个区域时，注入 `IMessageLocalizer`：

| 成员 | 返回 |
| --- | --- |
| `Resolve(key, locale, arguments)` | `Maybe<LocalizedText>`：文案与命中的区域；没有模板或占位符缺值时无值 |
| `Localize(key, locale, arguments)` | 文案；解析不到时是键与参数的描述 |
| `Localize(key, context, arguments)` | 按调用上下文的区域解析 |
| `Resolve(error, locale)` / `Localize(error, locale)` | 翻译 `Error`；自由文本错误没有键，`Resolve` 无值，`Localize` 返回原文 |
| `Find(key, locale)` | 沿回退链取未格式化的模板 |
| `Options` | 当前的 `LocalizationOptions` |

调用上下文上有等价的扩展方法 `context.Localize(key, arguments)` 与 `context.Localize(error)`，没有装配本地化时分别退回描述与 `Error.Message`。

```csharp
using XiHan.Framework.Localization.Abstractions.Messages;

var text = localizer.Resolve(SampleMessages.ResourceMissing, "zh-HK", arguments: null);
if (text.TryGetValue(out var localized))
{
    // zh-HK 不在启用列表里，按同语言回退到 zh-CN
    Console.WriteLine($"{localized.Locale}: {localized.Text}");
}
```

## 回退链

`LocalizationOptions.FallbackChain(locale)` 给出查找顺序，`IMessageLocalizer` 与 `key.L()` 都按它取模板：

1. 请求的区域（规范化；不合法时用默认区域）。
2. 启用列表里同语言的其他区域，按启用列表的顺序。
3. 默认区域。
4. `en-US`：源语言，永远是最后一道回退。

链上去重并保持顺序。默认配置下请求 `zh-HK` 的链是 `zh-HK → zh-CN → zh-TW → en-US`，请求 `it-IT` 的链是 `it-IT → en-US`。占位符按实际命中区域的文化格式化。

## 错误的翻译

`Error` 只携带错误码、消息键与参数，`Details` 同时是模板参数：

```csharp
return Error.NotFound("ORDERS_NOTFOUND_001", OrdersMessages.NotFound, MessageArguments.Of(("id", id)));
```

- `Error.Message` 按进程默认区域渲染，用于日志与异常；没有默认作用域时是键与参数的描述。
- 宿主在 Egress 把失败写成 RFC 9457 Problem Details：`detail` 是 `IMessageLocalizer.Resolve(error, locale)` 的文案，响应头 `Content-Language` 是文案实际命中的区域（可能是回退链上的区域），字段级 `errors` 中带消息键的违规按同一区域翻译。没有装配本地化或错误是自由文本时，`detail` 是 `Error.Message`，不带 `Content-Language`。
- `Error.Literal(code, text)` 没有键、不翻译，只用于线上还原远端错误与把异常转换为错误。`WithCode` 换码时保留键与参数。
- 幂等记录保存消息键与字符串化的参数，回放时仍按回放请求的区域翻译。
- `FrameworkException`（配置与编程错误）的消息是英文，不走消息键。

SimpleApp 的 `/missing` 端点把失败写进调用上下文，由宿主翻译：

```csharp
app.MapGet("/missing", (HttpContext http) =>
{
    // 把失败结果写进调用上下文，Egress 由宿主翻译为 RFC 9457 Problem Details
    http.GetXiHanContext().ValueOrThrow().SetOutcome(Error.NotFound("SAMPLES_NOTFOUND_001", SampleMessages.ResourceMissing));
    return Results.Empty;
});
```

```bash
# 在仓库根运行 SimpleApp（dotnet run --project next/samples/SimpleApp）后
curl -si -H "Accept-Language: zh-CN" http://localhost:5000/missing
# 404，响应头 Content-Language: zh-CN，detail 为「示例资源不存在。」
```

错误码的分类与 Problem Details 的结构见[结果与错误](./errors)。

## 区域协商

调用的区域只有一个来源：Baggage `xihan.locale`。各宿主把调用方的语言写进它：

- HTTP 宿主：请求的 `baggage` 头里已有 `xihan.locale` 时不覆盖；否则取 `Accept-Language` 的候选，按权重降序（同权重保持原顺序），跳过 `*`、权重为 0 与格式不合法的项，经 `LocalizationOptions.Negotiate` 协商成启用的区域写入 Baggage。没有装配本地化时取第一个合法候选并规范化，下游服务仍能拿到调用方语言。
- 命令行宿主：`--locale` 选项规范化后写入 Baggage。

`Negotiate` 分两轮：第一轮看任一候选是否精确命中启用列表；第二轮按语言子标签命中启用列表里第一个同语言的区域；都没有时用默认区域。精确命中优先于权重：`Accept-Language: fr-CA, de-DE;q=0.8` 协商出 `de-DE`，因为 `de-DE` 精确命中，而 `fr-CA` 只能按语言命中 `fr-FR`。

Baggage 随调用传播：HTTP 的 `baggage` 头、CloudEvents 的扩展属性 `xihanlocale`，于是跨服务的调用链用同一个区域。宿主尚未就绪返回 503 时还没有调用上下文，Problem Details 直接从 `Accept-Language` 协商。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Localization:DefaultLocale` | `string` | `en-US` | 协商不到时使用的区域，也是进程默认作用域的区域 |
| `XiHan:Localization:Locales` | `string`（逗号分隔） | 内置 10 种 | 启用的区域，按优先级排列；同语言回退按这个顺序 |

```json
{
  "XiHan": {
    "Localization": {
      "DefaultLocale": "zh-CN",
      "Locales": "zh-CN,zh-TW,en-US"
    }
  }
}
```

默认区域必须在启用列表里，区域名必须合法，否则装配本地化特性时抛出 `ArgumentException`，应用启动失败。

## 操作的标题与说明

操作在各协议上的显示文案取自消息目录里的约定键，键名由操作 ID 推出（连字符换成下划线）：

| 约定键 | 用在 |
| --- | --- |
| `<操作 ID>.title` | OpenAPI 操作的 `summary`、MCP tool 的 `title` |
| `<操作 ID>.description` | OpenAPI 操作的 `description`、MCP tool 的 `description`、AI 工具的描述 |
| `permissions.<权限码>.name` / `.description` | OpenAPI 里权限的显示名与说明 |
| `openapi.tags.<资源段>.name` / `.description` | OpenAPI 顶层 `tags` 的显示名与说明 |
| `openapi.groups.<组名>.name` / `.description` | OpenAPI 分组的显示名与说明 |

说明里不写授权要求，适配器按契约里的权限统一呈现。SimpleApp 的 `orders.create.title`、`orders.create.description` 就是这类键，十种语言文件各有一份；没有的键对应的字段不输出。各协议的细节见 [HTTP 与 OpenAPI](./http) 与 [gRPC、MCP 与 CloudEvents](./protocols)。

## 数据库翻译

`Localization.Database` 把翻译存进关系数据库，运营人员改了译文，应用不重新发布就生效。它是 `localization.resources@1` 的显式 Provider，装配后顶替默认的内存实现，依赖 `persistence.relational@1`：

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature(new EntityFrameworkCorePersistenceFeature<AppDbContext>(static (binding, options) => options.UseNpgsql(binding.ValueOrThrow().Value)))
    .UseFeature<DatabaseLocalizationFeature>());
```

```csharp
// AppDbContext.OnModelCreating：同一个键与区域只能有一行
modelBuilder.Entity<MessageTranslation>(static t => t.HasIndex(static x => new { x.Key, x.Locale }).IsUnique());
```

查找规则：沿回退链的每个区域，先查数据库快照，再查嵌入的目录。数据库里没有的键或区域照常走嵌入目录与回退链。

快照的加载：

1. 宿主进入服务状态后，`MessageTranslationLoader` 经完整管道首次加载：按 ID 分页读取（每页 1000 条），最多 `MaxTranslations` 条，超出的不加载并记警告；读完后整份替换快照。宿主就绪之前只有嵌入目录的文案。
2. 之后按 `RefreshIntervalSeconds` 周期刷新，`0` 关闭周期刷新。
3. 加载失败时保留上一份快照，失败记到 `LastError`，健康项 `localization.database.translations` 报 Degraded，应用仍在服务。

写入用作用域服务 `MessageTranslationStore`，它经仓储读写，只能在调用的 Transaction 阶段内使用，所以放在操作里：

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Localization.Database.Translations;
using XiHan.Framework.Primitives;

namespace Admin.Translations;

public sealed record TranslationItem(string Key, string Locale, string Template);

public sealed record SetTranslationsRequest(IReadOnlyList<TranslationItem> Items);

public sealed record SetTranslationsResponse(int Written, int Failed);

[Operation(Id, Permission = "translations.write")]
public sealed class SetTranslations(MessageTranslationStore store) : IOperation<SetTranslationsRequest, SetTranslationsResponse>
{
    public const string Id = "translations.set";

    public async ValueTask<Result<SetTranslationsResponse>> HandleAsync(SetTranslationsRequest request, InvocationContext context)
    {
        // 一次批量写入；同一批里重复的键与区域后者覆盖前者，逐项给出结果
        var entries = request.Items.Select(static i => new MessageTranslationEntry(i.Key, i.Locale, i.Template)).ToList();
        var results = await store.UpsertAsync(entries, context.CancellationToken);
        var written = results.Count(static r => r.Outcome.IsSuccess);
        return new SetTranslationsResponse(written, results.Count - written);
    }
}
```

操作的登记、请求类型的序列化上下文与权限定义见[操作契约](./operations)。`MessageTranslationEntry` 在构造时校验：键必须符合消息键文法，区域规范化大小写，模板不能为空白，不合法抛出 `ArgumentException`。`RemoveAsync` 按键与区域批量删除，不存在的项返回 `LOCALIZATION_NOTFOUND_001`；`ListAsync(locale, skip, take)` 按键排序分页列出。

写入要等这次调用的事务提交后才落库。新译文在下一次周期刷新时生效；要立即生效，在写入的调用成功返回之后调用 `MessageTranslationLoader.ReloadAsync`，不要在同一个操作里调用，那时事务还没提交。

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Localization:Database:RefreshIntervalSeconds` | 非负数 | 300 | 周期刷新的间隔，`0` 只在启动后加载一次 |
| `XiHan:Localization:Database:MaxTranslations` | 正整数 | 100000 | 快照的条数上限 |

## 错误码

| 错误码 | 含义 |
| --- | --- |
| `LOCALIZATION_VALIDATION_002` | 消息目录不合法（JSON、键或区域名错误、程序集里没有语言文件），装载目录时抛出 |
| `LOCALIZATION_NOTFOUND_001` | 数据库里没有这条翻译（`MessageTranslationStore.RemoveAsync` 的逐项结果） |

区域名、消息键不合法属于编程错误，在构造处抛出 `ArgumentException`。

## 相关

- [结果与错误](./errors)
- [HTTP 与 OpenAPI](./http)
- [持久化](./persistence)
- [Localization.Abstractions](../packages/localization-abstractions)
- [Localization.InMemory](../packages/localization-inmemory)
- [Localization.Database](../packages/localization-database)
- 规范：RFC-0010《本地化》、ADR-0038
