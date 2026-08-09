# 配置与选项

框架怎么读配置、选项模式怎么用、配置节怎么命名、什么该放配置什么该写代码。

## 配置来源

标准 .NET 配置栈，优先级从低到高：

```text
appsettings.json
  → appsettings.{Environment}.json
    → 环境变量
      → 命令行参数
```

层级用**双下划线**表示：`XiHan:Data:SqlSugarCore:EnableDiffLog` → `XiHan__Data__SqlSugarCore__EnableDiffLog`。

::: warning 密钥走环境变量
JWT 签名密钥、数据库密码、第三方 AppSecret 一律用环境变量或密钥库注入，不要提交明文。
:::

## 配置节命名约定

框架自身的配置节统一在 **`XiHan:`** 命名空间下，按「领域 → 子领域」分层：

| 配置节 | 归属包 |
| --- | --- |
| `XiHan:Data:SqlSugarCore` | [Data](../packages/data) |
| `XiHan:Caching:Redis` | [Caching](../packages/caching) |
| `XiHan:Authentication:Jwt` / `:PasswordHasher` / `:OAuth` | [Authentication](../packages/authentication) |
| `XiHan:Web:Api:Auth` / `:Cors` / `:OpenApiSecurity` | [Web.Api](../packages/web-api) |
| `XiHan:Web:RealTime:SignalR` | [Web.RealTime](../packages/web-realtime) |
| `XiHan:Tasks:ScheduledJobs` / `XiHan:BackgroundJobs` | [Tasks](../packages/tasks) |
| `XiHan:Workflow` / `XiHan:Workflow:Worker` | [Workflow](../packages/workflow) |
| `XiHan:Localization` | [Localization](../packages/localization) |
| `XiHan:ObjectStorage` | [ObjectStorage](../packages/object-storage) |
| `XiHan:Observability` | [Observability](../packages/observability) |
| `XiHan:DistributedIds:SnowflakeId` | [DistributedIds](../packages/distributed-ids) |
| `XiHan:Upgrade` | [Upgrade](../packages/upgrade) |
| `XiHan:SearchEngines:Elasticsearch` | [SearchEngines.Elasticsearch](../packages/search-engines-elasticsearch) |

你自己的业务包**用自己的顶层命名空间**（如 `MyCompany:Billing`），避免和框架撞节。

## 选项模式

### 定义

把配置节名钉成常量放在 Options 类里，代码里一律引用常量：

```csharp
public class BillingOptions
{
    public const string SectionName = "MyCompany:Billing";

    public int InvoiceRetentionDays { get; set; } = 365;
    public bool EnableAutoArchive { get; set; }
}
```

::: tip 为什么要 `SectionName` 常量
内联 `"MyCompany:Billing"` 字符串会散落在绑定处、测试里、文档里，改名时必漏。常量还能让「这个 Options 对应哪个节」一眼可见。
:::

### 绑定

在模块的 `ConfigureServices` 里：

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    var configuration = context.Services.GetConfiguration();

    // 从配置节绑定
    Configure<BillingOptions>(configuration.GetSection(BillingOptions.SectionName));

    // 或直接赋值
    Configure<BillingOptions>(o => o.InvoiceRetentionDays = 180);
}
```

`Configure<T>` 是 `XiHanModule` 上的便捷方法，等价于 `services.Configure<T>(...)`。

### 消费

```csharp
public class InvoiceService(IOptions<BillingOptions> options) : ITransientDependency
{
    private readonly BillingOptions _options = options.Value;
}
```

| 接口 | 何时用 |
| --- | --- |
| `IOptions<T>` | **默认选择**。单例，应用生命周期内不变 |
| `IOptionsSnapshot<T>` | 每个作用域重新计算（Scoped 服务里用） |
| `IOptionsMonitor<T>` | 需要**热更新**与变更通知（单例服务里用） |

## 三个配置钩子的时序

`XiHanModule` 提供三个层次，对应模块生命周期的三个阶段：

| 方法 | 时机 | 用途 |
| --- | --- | --- |
| `PreConfigure<T>` | `PreConfigureServices` | **先于其他模块预设**，供后续模块读取 |
| `Configure<T>` | `ConfigureServices` | 常规配置 |
| `PostConfigure<T>` | `PostConfigureServices` | **所有模块配置完之后**做最终覆盖 |

典型用法：

```csharp
// 库作者：给使用方一个预埋钩子
public override void PreConfigureServices(ServiceConfigurationContext context)
{
    PreConfigure<BillingOptions>(o => o.EnableAutoArchive = true);
}

// 应用作者：不管中间谁改过，我最后说了算
public override void PostConfigureServices(ServiceConfigurationContext context)
{
    PostConfigure<BillingOptions>(o => o.InvoiceRetentionDays = 90);
}
```

## 不走配置文件的选项

有些选项**刻意不从配置绑定**，只能代码方式设置——因为它们要在装配期就确定，或者值是类型/委托而非标量。

最典型的是[动态 API](./dynamic-api) 的 `DynamicApiOptions`：框架 `TryAddSingleton` 一个实例并交给约定实现持有，你的模块在 `ConfigureServices` 里就地改它：

```csharp
context.Services.ConfigureDynamicApiConventions(conventions =>
{
    conventions.HttpMethodConventions["Import"] = "POST";
});
```

遇到「这个选项怎么配置文件里绑不上」时，先看包文档里它是不是这一类。

## 什么该放配置

| 判断 | 结论 |
| --- | --- |
| 启动期就要用、改了必须重启（连接串、密钥、端口） | **配置文件 / 环境变量** |
| 运行期由管理员调整、要按租户隔离（业务开关、阈值） | **数据库**（自行实现配置源） |
| 装配期确定的类型/委托/约定 | **代码** |

框架的可替换点大多留了「配置源」接口（如 `IAiProviderConfigStore`），业务侧可以 `Replace` 成数据库实现——这就是「配置落库」的标准做法，见 [扩展与二次开发](./extending#配方-b-替换框架的默认实现)。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 配置改了不生效 | 用了 `IOptions<T>`（单例快照）；要热更新改 `IOptionsMonitor<T>` |
| 环境变量没覆盖掉 json | 层级分隔符要用双下划线 `__` |
| 某个选项在 appsettings 里绑不上 | 它可能是代码方式配置的（如 `DynamicApiOptions`），或该字段是第三方库的字段而非属性 |
| `PostConfigure` 没生效 | 检查是否有更晚的模块又改了一次——最后执行的赢 |

## 下一步

- [模块生命周期](./lifecycle)：三个配置钩子分别在什么时候跑
- [依赖注入](./dependency-injection)：选项模式与约定注册
- [扩展与二次开发](./extending)：把配置源换成数据库实现
