# 动态 API

动态 API 是 XiHan.Framework 暴露接口的方式：**你写应用服务，框架把它自动变成 REST 接口**，不需要为每个服务再写一个 Controller。

## 传统做法 vs 动态 API

```text
传统 ASP.NET Core                    XiHan 动态 API
┌───────────────────┐               ┌───────────────────┐
│ OrderController   │  样板代码      │ OrderAppService   │  没有 Controller
│  ├ [HttpGet]      │  ───────►      │  ├ GetAsync()     │  方法即接口
│  ├ [HttpPost]     │               │  ├ CreateAsync()  │
│  └ 手动调用 Service│               │  └ ...            │
└───────────────────┘               └───────────────────┘
        │                                     │
   Service（真正逻辑）                   [DynamicApi] 自动生成路由
```

## 写一个动态 API

两步：**继承应用服务基类** + **打上 `[DynamicApi]`**。

```csharp
using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Services;

[DynamicApi]
public class OrderAppService : ApplicationServiceBase
{
    public Task<OrderDto> GetAsync(long id) { /* ... */ }
    public Task<OrderDto> CreateAsync(CreateOrderDto input) { /* ... */ }
}
```

框架启动时，动态 API 约定会扫描到这个类，为它的每个公共方法生成对应的 HTTP 端点，并纳入 [Web.Docs](../packages/web-docs) 的 Scalar / Swagger 文档。

> `ApplicationServiceBase` 实现了 `IApplicationService` 与 `ITransientDependency`，所以应用服务**同时**被自动注册进 DI 并被动态 API 识别。

## CRUD 基类：一套增删改查开箱即用

对标准的实体增删改查，框架提供了 `CrudApplicationServiceBase`，泛型指定实体、DTO、主键类型即可获得完整 CRUD：

```csharp
[DynamicApi]
public class ProductAppService
    : CrudApplicationServiceBase<Product, ProductDto, long, CreateProductDto, UpdateProductDto, ProductPageRequestDto>
{
    public ProductAppService(IRepositoryBase<Product, long> repository)
        : base(repository) { }
}
```

它内置了 `GetByIdAsync`、分页查询、创建、更新、删除等方法，全部自动暴露为接口，还负责实体与 DTO 之间的映射。需要自定义时重写对应方法即可。

## 路由与文档的定制

`[DynamicApi]` 提供了丰富的定制项（类级与方法级都可标注，方法级优先）：

| 属性 | 作用 |
| --- | --- |
| `IsEnabled` | 是否启用（任一层级为 `false` 即禁用） |
| `RouteTemplate` | 自定义路由模板 |
| `Name` | API 名称 |
| `Version` | API 版本（可叠加多个，写纯数字不带 `v` 前缀——路由拼接时框架会自动加上） |
| `Group` / `GroupName` | 文档分组键 / 分组显示名 |
| `Tag` | API 标签（可叠加） |
| `Description` | API 描述 |
| `UsePascalCaseRoutes` / `UseLowercaseRoute` | 路由大小写风格 |
| `PreserveRoutePredicate` | 是否保留 Get/Create/Delete 等动词前缀 |
| `VisibleInApiExplorer` | 是否在 API 浏览器/文档中显示（任一层级为 `false` 即隐藏） |
| `Order` | 同层级多个特性的合并顺序（数值越大越晚应用、优先级越高） |
| `CustomProperties` | 自定义键值属性，`key=value` 形式，可叠加 |

```csharp
[DynamicApi(Group = "order", GroupName = "订单管理", Version = "1")]
public class OrderAppService : ApplicationServiceBase { }
```

**合并优先级**：全局配置 < 类级特性 < 方法级特性；同一层级按 `Order` 升序合并（后写入覆盖先写入）。

## 它跑在标准 MVC 管道上

动态 API 生成的是**真正的 MVC 控制器**，因此完全兼容 ASP.NET Core 的一切：过滤器、模型绑定、`[Authorize]`、认证/授权中间件等。请求会经过 [Web.Api 的完整中间件管道](./lifecycle#真实示例-web-api-的中间件管道)（TraceId → 请求文化 → 认证 → 租户解析 → 授权 → 端点）。

> 如果某个场景确实需要手写传统 Controller，也完全可以——`Web.Api` 的 `MapControllers()` 会一并识别特性路由的控制器。两种方式可以共存。

## 小结

- 写**应用服务**，不写 Controller
- `ApplicationServiceBase` + `[DynamicApi]` = 自动注册 + 自动暴露 + 自动进文档
- 标准 CRUD 用 `CrudApplicationServiceBase` 一步到位
- 路由、分组、版本通过 `[DynamicApi]` 的属性灵活定制

## 下一步

- [Web.Api 模块](../packages/web-api)：动态 API 的实现细节与中间件管道
- [Web.Docs 模块](../packages/web-docs)：Scalar / Swagger 文档
- [Application 模块](../packages/application)：应用服务与 CRUD 基类
