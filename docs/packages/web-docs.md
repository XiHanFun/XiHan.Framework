# XiHan.Framework.Web.Docs

> API 文档：在动态 API 之上提供 Scalar 与 Swagger UI 两套交互式文档界面，并按 `[DynamicApi]` 分组自动注册多份 OpenAPI 文档。

- **NuGet**：`XiHan.Framework.Web.Docs`
- **模块类**：`XiHanWebDocsModule`
- **所在层**：Web 层
- **关键依赖**：`Scalar.AspNetCore`（现代文档界面）、`Swashbuckle.AspNetCore`（Swagger UI）；构建于框架内置 OpenAPI（`Microsoft.AspNetCore.OpenApi`）之上

## 概述

XiHan.Framework.Web.Docs 让你的 API 可视化、可试调。它基于 [Web.Api](./web-api) 生成的动态 API 与 OpenAPI 文档，接入两套界面：现代化的 **Scalar** 与经典的 **Swagger UI**；同时根据业务服务上 `[DynamicApi]` 特性携带的分组信息，为默认文档和每个额外分组各注册一份独立的 OpenAPI 文档（例如把系统接口与业务接口分开展示），并把原始服务方法上的 XML 注释注入到 OpenAPI 操作里。引用模块、加上 `[DependsOn]`，启动后即可在浏览器打开文档页面。

## 何时使用

- 想要一个交互式 API 文档界面，直接在浏览器里查看接口、参数并发起试调。
- 项目按 `[DynamicApi]` 分了组（如 `Order`），希望文档也按组切换展示。
- 需要在 OpenAPI 中带上 XML 注释（接口摘要、参数说明、返回说明）。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Web.Docs
```

```csharp
[DependsOn(typeof(XiHanWebDocsModule))]
public class MyModule : XiHanModule { }
```

模块 `ConfigureServices` 调用 `AddXiHanWebDocs()`：注册默认 OpenAPI 文档（`v1`）及各动态分组文档，并为每份文档挂上 XML 注释操作转换器。`OnPreApplicationInitialization` 用 `UseSwaggerUI()` 注册 Swagger UI（**早于**认证/授权中间件，保证匿名可访问文档）；`OnApplicationInitialization` 用 `MapScalarApiReference()` 注册 Scalar（`AllowAnonymous()`）。

## 工作原理

1. **分组发现（编译期特性扫描）**：`DynamicApiSwaggerGroupHelper.GetGroupDefinitionsFromAttributes()` 遍历已加载程序集里实现 `IApplicationService` 的类型，合并类级与方法级的 `[DynamicApi]` 特性，收集其 `Group` / `GroupName` / `Order`，跳过 `IsEnabled=false` 或 `VisibleInApiExplorer=false` 的项。系统程序集（`Microsoft.*` / `System.*` / `Serilog*` / `Newtonsoft*` 等）被排除。
2. **多文档注册**：默认文档名 `v1`（标题 `API V1`）始终注册；每个非 `v1` 分组各 `AddOpenApi(group, ...)` 注册一份文档，并用 `ShouldInclude` 按 `ApiDescription.GroupName` 过滤只收本组接口。
3. **两套 UI 挂载**：Scalar 与 Swagger UI 都读取 `/openapi/{documentName}.json`，先渲染默认 `v1`，再逐个追加额外分组端点。
4. **XML 注释注入**：`DynamicApiXmlCommentsOperationTransformer` 从动态 API 生成的控制器动作上的 `OriginalMethodAttribute` 回溯到原始服务方法，读取程序集旁的 `*.xml` 文档，把 `summary` / `remarks` / `param` / `returns` 填进 OpenAPI 操作（仅在对应字段为空时填充，`[DynamicApi]` 自定义描述优先于 XML `summary`）。成员名匹配由 `XmlCommentsNodeNameHelper` 完成，除方法自身外还会依次尝试其**泛型类型定义**上的同名方法与 `GetBaseDefinition()` 得到的**基类/接口原始定义**（及其泛型类型定义），因此继承自泛型基类（如通用 CRUD 基类）的服务方法，只要在基类方法上写了 XML 注释，也能正确回退匹配并注入。

## 核心能力

- **Scalar 文档界面**：`MapScalarApiReference((options, httpContext) => ...)`，标题 `XiHan Framework API`、`ScalarTheme.Purple` 紫色主题、默认 C# `HttpClient` 客户端示例（`ScalarTarget.CSharp` / `ScalarClient.HttpClient`）、OpenAPI 路由模式 `/openapi/{documentName}.json`，默认文档 `v1` 标记 `isDefault: true`，其余分组追加为 `AddDocument`。整个端点 `AllowAnonymous()`。在 BasicApp 中访问路径为 `/scalar`。
- **Swagger UI 文档界面**：`UseSwaggerUI()` 于预初始化阶段注册，逐个 `SwaggerEndpoint("/openapi/{group}.json", 标题)`，先默认 `v1` 后各额外分组。
- **动态 API 分组发现**：`DynamicApiSwaggerGroupHelper` 从 `[DynamicApi]` 特性收集分组定义与默认文档名/标题。
- **XML 注释增强**：`DynamicApiXmlCommentsOperationTransformer` 把原始服务方法的 XML 注释注入 OpenAPI 操作，并借助 `XmlCommentsNodeNameHelper` 兼容泛型基类方法与接口/基类原始定义的注释回退匹配。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanWebDocsModule` | 模块类，注册文档服务并挂载 Scalar / Swagger UI |
| `AddXiHanWebDocs()` | 服务集合扩展；注册默认 `v1` 与各动态分组的 OpenAPI 文档，挂上 XML 注释转换器 |
| `DynamicApiSwaggerGroupHelper` | `internal` 静态类；`GetGroupDefinitionsFromAttributes()` 从 `[DynamicApi]` 扫描分组，常量 `DefaultDocName="v1"` / `DefaultDocTitle="API V1"` |
| `DynamicApiXmlCommentsOperationTransformer` | `IOpenApiOperationTransformer`；经 `OriginalMethodAttribute` 回溯原始方法并注入 XML 注释 |
| `XmlCommentsNodeNameHelper` | `internal` 静态类；生成 XML 文档成员名（`M:`/`T:` 前缀），支持泛型类型定义、`GetBaseDefinition()` 基类/接口原始定义的多候选回退匹配 |

> 说明：`DynamicApiSwaggerGroupHelper`、`XmlCommentsNodeNameHelper` 均为 `internal`，分组发现与注释匹配由模块内部驱动，无需应用侧显式调用；应用侧只需在业务服务上正确标注 `[DynamicApi(Group=..., GroupName=...)]` 即可自动多文档分组。

## 使用示例

在业务服务上标注分组，文档会自动生成对应的分组文档：

```csharp
[DynamicApi(Group = "Order", GroupName = "订单模块")]
public class OrderAppService : IApplicationService
{
    /// <summary>创建订单</summary>
    /// <param name="input">下单参数</param>
    /// <returns>订单编号</returns>
    public Task<long> CreateAsync(CreateOrderDto input) => ...;
}
```

启动后：Scalar 页面（BasicApp 为 `/scalar`）默认展示 `API V1`，并可切换到「订单模块」；上面方法的 XML `summary`/`param`/`returns` 会显示为接口摘要、参数与返回说明。

## 注意事项与最佳实践

- **文档端点匿名可访问**：Swagger UI 在预初始化阶段注册（先于鉴权），Scalar 端点 `AllowAnonymous()`；生产环境如需保护文档，应在网关/反向代理层控制访问，或把 `/scalar`、`/swagger`、`/openapi` 加入相应的匿名/忽略前缀（BasicApp 已在 `IgnoredPathPrefixes` 中包含这些路径）。
- **XML 注释需开启文档文件生成**：转换器从 `AppContext.BaseDirectory` 下的 `*.xml` 读取注释；承载业务服务的程序集需 `<GenerateDocumentationFile>true</GenerateDocumentationFile>` 才有注释可注入。注意本包自身 csproj 关闭了文档生成（`GenerateDocumentationFile=false`），这只影响本包，不影响你的业务程序集。
- **注释填充是「不覆盖」语义**：仅当 OpenAPI 操作对应字段为空时才填充；`[DynamicApi]` 的自定义描述优先级高于 XML `summary`。
- **分组名大小写不敏感**：分组去重与匹配用 `OrdinalIgnoreCase`；`Order` 值影响文档排序。

## 依赖模块

- 内部依赖：[XiHan.Framework.Web.Api](./web-api)（动态 API 与 OpenAPI 文档的来源，`[DependsOn(typeof(XiHanWebApiModule))]`）。
- 第三方核心：`Scalar.AspNetCore`、`Swashbuckle.AspNetCore`。

## 相关模块

- [XiHan.Framework.Web.Api](./web-api) — 生成本包所展示的 OpenAPI 文档与动态 API 分组。
- [动态 API](../guide/dynamic-api) — 动态 API 与 `[DynamicApi]` 分组约定。
