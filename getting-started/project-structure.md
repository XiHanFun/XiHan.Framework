来源：https://framework.docs.xihanfun.com/getting-started/project-structure

# 项目结构建议

这一页给出一个中等规模应用的解决方案布局建议：宿主、能力包、共享契约各放哪里，一个能力包内部怎样组织目录，本地化文件与序列化上下文放在哪。它是建议而不是约束，框架不扫描目录，也不靠文件位置推断任何东西。

## 为什么需要约定

框架的装配是显式的：每个特性都要 `UseFeature`，每个操作都由生成器登记。这意味着目录结构不影响运行，但会影响人：一个新同事打开仓库，应该一眼看出哪里是宿主、哪里是业务、哪里是契约。框架自己的仓库用「目录即子命名空间」的规则组织，应用沿用同一条规则最省心。

## 解决方案布局

```text
Shop/
├─ Shop.slnx
├─ Directory.Build.props              共享的编译属性：Nullable、警告即错误、版本
├─ Directory.Packages.props           中央包版本
├─ src/
│  ├─ Shop.Host/                      ASP.NET Core 宿主：Program.cs、appsettings.json、xihan.app.json
│  ├─ Shop.Cli/                       可选：命令行宿主，同一组特性换一个宿主
│  ├─ Shop.Orders/                    能力包：订单
│  ├─ Shop.Catalog/                   能力包：商品
│  └─ Shop.Shared/                    跨能力包共享的值对象、权限码常量、消息键
└─ test/
   ├─ Shop.Orders.Tests/
   └─ Shop.Host.Tests/
```

几条原则：

- 宿主只做装配与映射。`Program.cs` 里是 `AddXiHan`、一串 `UseFeature`、`UseXiHan` 与几个 `Map*`，不写业务。
- 一个能力包一个项目，对应一个清单 ID（`xihan.shop.orders`），只依赖抽象包与 `Shop.Shared`，不直接依赖 Provider 包。Provider 由宿主选。
- 能力包之间不互相引用。需要跨包协作时，经消息（领域事件、发布订阅）或经操作调用器在进程内调用对方的操作。
- 共享项目只放契约级的东西：值对象、权限码常量、主题名、消息键。不放服务实现。

## 一个能力包的内部

以订单能力包为例，目录即子命名空间，最多一层：

```text
Shop.Orders/
├─ Shop.Orders.csproj
├─ OrdersFeature.cs                   特性：清单 + Configure
├─ OrdersMessages.cs                  消息键常量
├─ OrdersJsonContext.cs               源生成的序列化上下文
├─ Localization/
│  ├─ en-US.json                      十种区域各一个文件
│  ├─ zh-CN.json
│  └─ ...
├─ Entities/
│  ├─ Order.cs                        聚合根，接口组合
│  └─ OrderLine.cs
├─ Operations/
│  ├─ CreateOrder.cs                  一个操作一个文件
│  ├─ GetOrder.cs
│  ├─ ListOrders.cs
│  └─ PlaceOrderRequest.cs            请求与响应类型
├─ Events/
│  ├─ OrderPlaced.cs                  领域事件
│  └─ OrderPlacedHandler.cs           消息处理器
└─ Jobs/
   └─ ExpireUnpaidOrders.cs           作业
```

项目文件要点：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Shop.Orders</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="XiHan.Framework.Persistence.Abstractions" />
    <PackageReference Include="XiHan.Framework.Messaging.Abstractions" />
    <PackageReference Include="XiHan.Framework.Security.Abstractions" />
    <PackageReference Include="XiHan.Framework.Kernel.Generators" PrivateAssets="all" />
    <PackageReference Include="XiHan.Framework.Persistence.Generators" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <!-- 本地化文件以固定逻辑名嵌入，与框架包同一格式 -->
    <EmbeddedResource Include="Localization/*.json" LogicalName="Localization/%(Filename)%(Extension)" />
  </ItemGroup>
</Project>
```

`RootNamespace` 决定生成器把 `AddGeneratedOperations` 与 `AddGeneratedRepositories` 放在哪个命名空间下；每个能力包各自一份序列化上下文，宿主用 `ConfigureHttpJsonOptions` 把它们插进解析链。

## 宿主的 Program.cs

```csharp
using Shop.Catalog;
using Shop.Orders;
using XiHan.Framework.Caching.Redis;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Persistence.EntityFrameworkCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.OpenApi;
using XiHan.Framework.Protocols.Http.Routing;
using XiHan.Framework.Security.Jwt;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, OrdersJsonContext.Default);
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, CatalogJsonContext.Default);
});

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    // EF Core Provider：应用自有的 DbContext，连接串来自绑定 db
    .UseFeature(new EntityFrameworkCorePersistenceFeature<ShopDbContext>((binding, options) =>
        options.UseNpgsql(binding.ValueOrThrow().Value)))
    .UseFeature<RedisCachingFeature>()
    .UseFeature<JwtSecurityFeature>()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<OrdersFeature>()
    .UseFeature<CatalogFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
app.MapXiHanOperations();
app.MapXiHanOpenApi();
app.Run();
```

Provider 的选择只出现在宿主里。换数据库、换缓存，改的是这个文件与配置，能力包不动。各 Provider 特性的类名以它的包页为准，见[包参考](/packages/)。

## 配置文件

`appsettings.json` 按配置节组织，键名是 `XiHan:<领域>`，应用自己的选项也放在 `XiHan` 下或单独的根节：

```json
{
  "XiHan": {
    "Kernel": { "StartTimeoutSeconds": 30, "StopTimeoutSeconds": 30 },
    "Bindings": {
      "db": { "type": "connection:relational", "value": "Host=localhost;Database=shop;Username=dev;Password=dev-password" }
    },
    "Caching": { "Redis": { "Configuration": "localhost:6379" } },
    "Security": { "Jwt": { "Issuer": "https://auth.example.com" } }
  },
  "Shop": {
    "Orders": { "MaxLines": 100 }
  }
}
```

数据库连接串放在资源绑定 `XiHan:Bindings:db` 下，Provider 从绑定取值，见[资源绑定](/configuration/bindings)。各配置节的键见[配置模型与选项](/configuration/options)与各包页。部署时用环境变量覆盖，分隔符是双下划线：`XiHan__Bindings__db__value`、`XiHan__Caching__Redis__Configuration`。

## 多个宿主共用特性

同一组能力包可以装进不同的宿主：Web 宿主对外服务，命令行宿主做运维与 MCP stdio。两个宿主的 `AddXiHan` 里是同一串 `UseFeature`，只是协议特性不同：

```csharp
// Shop.Cli/Program.cs
var builder = Host.CreateApplicationBuilder(args);
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<EntityFrameworkCorePersistenceFeature>()
    .UseFeature<McpProtocolFeature>()
    .UseFeature<OrdersFeature>()
    .UseFeature<CatalogFeature>());
builder.Services.AddXiHanCommandLine();
return await builder.Build().RunXiHanCommandLineAsync(args);
```

`graph`、`pipeline`、`operations`、`invoke` 四个命令直接可用，见[命令行宿主](/protocols/command-line)。

## 常见问题

### 能力包要不要再拆成 Domain、Application、Infrastructure 三层

不建议。操作就是应用层，实体协议就是领域层，Provider 由框架提供。再拆三个项目只会多出一堆只有接口的程序集。一个能力包内部用目录分区足够。

### 请求与响应类型放在哪

放在操作旁边，或者放在 `Operations/` 下的独立文件里。它们要登记到本能力包的序列化上下文。跨能力包共享的请求类型是坏味道，说明两个能力包的边界画错了。

### 本地化文件每个能力包一份还是全应用一份

每个能力包一份，消息键以能力包的领域词开头（`shop.orders.not_found`），互不冲突。宿主用 `AddMessageCatalog` 登记每个能力包的目录。

## 下一步

- [装配入口](/startup/bootstrap)：`AddXiHan` 的两种模式与 `AppBuilder`
- [特性与清单](/startup/features)：清单的全部字段
