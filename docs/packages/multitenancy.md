# XiHan.Framework.MultiTenancy

> 多租户实现：当前租户上下文（AsyncLocal）、租户解析链、租户配置存储、租户级设置隔离与功能开关

- **NuGet**：`XiHan.Framework.MultiTenancy`
- **模块类**：`XiHanMultiTenancyModule`
- **所在层**：应用层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（Abstractions / Settings / Security / Core）

## 概述

本包是多租户能力的**实现层**，为 [XiHan.Framework.MultiTenancy.Abstractions](./multitenancy-abstractions) 定义的接口提供落地实现。它用 `AsyncLocal` 维护当前请求的租户上下文（跨 `async/await` 流转）、提供可扩展的**租户解析链**、把「租户级设置」接入设置体系、并提供基于设置存储的**租户功能开关**。你只需引用它并注入 `ICurrentTenant`，上下文的存取与恢复都在幕后完成。

需要强调的边界：本包只负责**上下文、存储、解析链的贡献者与选项**。真正在 HTTP 管线里「执行解析链、把结果写入当前租户」的中间件 `XiHanTenantResolveMiddleware`，以及基于 Header / QueryString 的解析贡献者，都在 [XiHan.Framework.Web.Api](./web-api) 中——本包只负责在解析链首位插入 `CurrentUserTenantResolveContributor`。

## 何时使用

- 你的系统需要区分多个租户，并按租户隔离数据与配置
- 你需要在请求管线里自动识别当前租户（登录用户、`X-Tenant-Id` 头、`tenantId` 查询串——后两者由 Web.Api 提供贡献者）
- 你需要租户级设置（同一设置项对不同租户取不同值）
- 你需要从存储中按 Id 或名称查询租户配置（连接串、是否激活、版本等）
- 你需要按租户判断某功能是否启用（`ITenantFeatureChecker`）

## 安装与启用

```bash
dotnet add package XiHan.Framework.MultiTenancy
```

```csharp
[DependsOn(typeof(XiHanMultiTenancyModule))]
public class MyModule : XiHanModule { }
```

模块 `[DependsOn]` 了 `XiHanMultiTenancyAbstractionsModule`、`XiHanSettingsModule`、`XiHanSecurityModule`，并在 `ConfigureServices` 里调用 `AddXiHanMultiTenancy(config)`，完成：

- 注册 `ICurrentTenantAccessor` 为 `AsyncLocalCurrentTenantAccessor.Instance`（单例）
- 绑定 `XiHanDefaultTenantStoreOptions`（配置节 `XiHan:MultiTenancy:DefaultStore`），`TryAddSingleton<ITenantStore, DefaultTenantStore>()`
- 向 `XiHanSettingOptions.ValueProviders` 在 `GlobalSettingValueProvider` **之后**插入 `TenantSettingValueProvider`（提供者名 `"T"`）
- 向 `XiHanTenantResolveOptions.TenantResolvers` 的**首位**（`Insert(0, ...)`）插入 `CurrentUserTenantResolveContributor`
- 注册 `ITenantFeatureChecker` → `TenantFeatureChecker`（Transient）

> `CurrentTenant`（`ICurrentTenant` 实现）标记了 `ITransientDependency`，通过框架的按接口约定注册自动生效，无需在扩展方法里手动登记。

## 工作原理

### 当前租户上下文（AsyncLocal）

`AsyncLocalCurrentTenantAccessor` 用 `AsyncLocal<BasicTenantInfo?>` 保存当前租户，注册为进程单例（`Instance`）。`CurrentTenant.Change(id, name)` 的实现：先记住父级 `Current`，写入新的 `BasicTenantInfo`，返回一个 `DisposeAction`——`Dispose` 时把 `Current` 还原为父级。因此 `Change` 支持**嵌套**，且能安全地跨异步边界流转、在 `using` 结束时精确恢复到上一层。`IsAvailable` 等价于 `Id.HasValue`。

### 租户解析链（贡献者 + 中间件）

解析链由 `XiHanTenantResolveOptions.TenantResolvers`（一个有序 `List<ITenantResolveContributor>`）承载。实际执行发生在 Web.Api 的 `XiHanTenantResolveMiddleware`（位于 `UseAuthentication` 之后、`UseAuthorization` 之前）：

```text
请求进入 → 遍历 TenantResolvers（有序）：
  逐个 ResolveAsync(context)，谁把 context.Handled = true 就短路
  ↓
得到 TenantIdOrName（若为空且未 Handled，回退到 FallbackTenant）
  ↓
若仍为空 → 直接 next()（宿主上下文，Id = null）
  ↓
ITenantStore 按 TenantIdOrName 查 TenantConfiguration
  （先按数字 Id 试 FindAsync(long)，再按名称 FindAsync(string)）
  ↓
命中 → using CurrentTenant.Change(cfg.Id, cfg.Name) 包裹 next()
未命中 → 尽力把 TenantIdOrName 解析为 long 后 Change(tenantId, key) 包裹 next()
```

链的组成（默认顺序）：

1. `CurrentUserTenantResolveContributor`（本包，插在首位）——从登录用户的 `ICurrentUser.TenantId` 解析；未登录或无租户则跳过
2. `HeaderTenantResolveContributor`（Web.Api 追加）——按 `HeaderKeys` 读请求头
3. `QueryStringTenantResolveContributor`（Web.Api 追加）——按 `QueryStringKeys` 读查询串

要新增/调整解析来源，`Configure<XiHanTenantResolveOptions>` 增删 `TenantResolvers` 即可（见「扩展点」）。

### 租户级设置隔离

`TenantSettingValueProvider`（提供者名 `"T"`）继承设置包的 `SettingValueProvider`，读值时以「当前租户键」作为 ProviderKey 调设置存储。租户键取值优先用 `ICurrentTenant.Name`，为空则回退到 `Id?.ToString()`。它被插入到设置值提供者链的 `GlobalSettingValueProvider`（`"G"`）之后，从而实现「先看全局、再被租户覆盖」的语义。详见 [Settings](./settings)。

### 租户功能开关

`TenantFeatureChecker` 复用设置存储：把功能名加前缀 `Feature:` 组成设置键，以租户键作 ProviderKey、`"T"` 作 ProviderName 读值。`IsEnabledAsync` 把 `1/true/yes/on`（忽略大小写）视为启用，其它/空值返回传入的 `defaultValue`。

## 核心能力

- **当前租户上下文**：`CurrentTenant`（`ICurrentTenant`，Transient）+ `AsyncLocalCurrentTenantAccessor`（单例），`Change(...)` 支持嵌套临时切换并在释放时恢复
- **租户解析链**：`ITenantResolveContributor` 组成有序链，内置 `CurrentUserTenantResolveContributor`；`TenantResolveContributorBase` 是自定义贡献者基类
- **租户配置存储**：`ITenantStore` 按 Id / 名称查询 `TenantConfiguration`，默认实现 `DefaultTenantStore` 从配置节读取
- **租户级设置隔离**：`TenantSettingValueProvider`（`"T"`），从设置存储按当前租户键取值
- **租户功能开关**：`ITenantFeatureChecker` / `TenantFeatureChecker` 基于设置存储判断功能是否启用

## 主要 API / 类型

### 上下文与解析

| 类型 | 说明 |
| --- | --- |
| `CurrentTenant` | `ICurrentTenant` 实现（`ITransientDependency`），委托给 `ICurrentTenantAccessor`；`IsAvailable => Id.HasValue` |
| `AsyncLocalCurrentTenantAccessor` | 基于 `AsyncLocal<BasicTenantInfo?>` 的访问器，单例 `Instance`，私有构造 |
| `TenantResolveContributorBase` | 自定义租户解析贡献者的抽象基类（`abstract string Name` / `abstract Task ResolveAsync(...)`） |
| `CurrentUserTenantResolveContributor` | 内置贡献者，`Name = "CurrentUser"`；从 `ICurrentUser.TenantId` 解析（未认证/无租户则跳过） |
| `XiHanTenantResolveOptions` | 解析选项（配置节 `XiHan:MultiTenancy:Resolve`），见下表 |

### 存储与功能

| 类型 | 说明 |
| --- | --- |
| `TenantConfiguration` | 租户配置模型：`long Id`、`string Name`、`string NormalizedName`、`ConnectionStrings? ConnectionStrings`、`bool IsActive`（默认 `true`）、`Guid? EditionId` |
| `ITenantStore` | 租户存储契约：`Task<TenantConfiguration?> FindAsync(long id, ...)`、`Task<TenantConfiguration?> FindAsync(string name, ...)`、`Task<IReadOnlyList<TenantConfiguration>> GetListAsync(bool includeInactive = true, ...)` |
| `DefaultTenantStore` | 默认实现，从 `XiHanDefaultTenantStoreOptions.Tenants` 读取（`IOptionsMonitor` 热更新）；`FindAsync(string)` 支持纯数字按 Id、否则按 `Name`/`NormalizedName` 不区分大小写匹配 |
| `XiHanDefaultTenantStoreOptions` | 配置节 `XiHan:MultiTenancy:DefaultStore`，字段 `TenantConfiguration[] Tenants`（默认空数组） |
| `TenantSettingValueProvider` | 租户级设置值提供者，`ProviderName = "T"`；租户键优先 `Name`、回退 `Id` |
| `ITenantFeatureChecker` / `TenantFeatureChecker` | `Task<bool> IsEnabledAsync(string featureName, bool defaultValue = false)`、`Task<string?> GetValueOrNullAsync(string featureName)`；功能键前缀 `Feature:` |

## 配置

配置节 `XiHan:MultiTenancy:Resolve`（`XiHanTenantResolveOptions.SectionName`）：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `TenantResolvers` | `List<ITenantResolveContributor>` | 空（运行时由各模块插入） | 有序解析链，代码组装、不从配置绑定 |
| `EnableHeaderResolve` | `bool` | `true` | 是否启用 Header 解析（供 Web.Api 的 Header 贡献者判断） |
| `HeaderKeys` | `string[]` | `["X-Tenant-Id", "x-tenant-id", "TenantId"]` | Header 租户键（按优先级） |
| `EnableQueryStringResolve` | `bool` | `true` | 是否启用 QueryString 解析 |
| `QueryStringKeys` | `string[]` | `["tenantId", "tenant"]` | QueryString 租户键（按优先级） |
| `FallbackTenant` | `string?` | `null` | 解析链未命中且未 Handled 时的回退租户标识 |

配置节 `XiHan:MultiTenancy:DefaultStore`（`XiHanDefaultTenantStoreOptions.SectionName`）：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `Tenants` | `TenantConfiguration[]` | `[]` | `DefaultTenantStore` 的租户清单来源 |

示例 appsettings.json：

```json
{
  "XiHan": {
    "MultiTenancy": {
      "Resolve": {
        "EnableHeaderResolve": true,
        "HeaderKeys": [ "X-Tenant-Id" ],
        "EnableQueryStringResolve": true,
        "QueryStringKeys": [ "tenantId" ],
        "FallbackTenant": null
      },
      "DefaultStore": {
        "Tenants": [
          {
            "Id": 1,
            "Name": "acme",
            "NormalizedName": "ACME",
            "IsActive": true
          }
        ]
      }
    }
  }
}
```

## 使用示例

### 注入并读取当前租户

```csharp
public class OrderService(ICurrentTenant currentTenant)
{
    public long? CurrentTenantId => currentTenant.Id; // null = 宿主
}
```

### 自定义租户解析贡献者并加入解析链

```csharp
public class ApiKeyTenantResolveContributor : TenantResolveContributorBase
{
    public override string Name => "ApiKey";

    public override Task ResolveAsync(ITenantResolveContext context)
    {
        // 通过 context.ServiceProvider 取请求级服务，解析出租户标识/名称
        context.TenantIdOrName = "acme";
        context.Handled = true; // 短路后续贡献者
        return Task.CompletedTask;
    }
}

// 在模块 ConfigureServices 里插入（越靠前优先级越高）
context.Services.Configure<XiHanTenantResolveOptions>(options =>
{
    options.TenantResolvers.Insert(0, new ApiKeyTenantResolveContributor());
});
```

### 按租户读取功能开关

```csharp
public class FeatureGate(ITenantFeatureChecker featureChecker)
{
    public Task<bool> CanExportAsync()
        => featureChecker.IsEnabledAsync("Report.Export", defaultValue: false);
}
```

## 扩展点 / 自定义

- **替换租户存储**：`ITenantStore` 用 `TryAddSingleton` 注册，上层可注册自己的实现（如查数据库）覆盖默认的 `DefaultTenantStore`。BasicApp 即用数据库实现替换。
- **扩展解析链**：`Configure<XiHanTenantResolveOptions>` 增删 `TenantResolvers`；`Insert(0, ...)` 提高优先级，`Add(...)` 追加为兜底。
- **自定义功能检查**：`ITenantFeatureChecker` 为 Transient 注册，可按需替换实现。

## 注意事项与最佳实践

- **框架层 `null` = 宿主；`TenantId = 0` 是应用层约定**：框架抽象中 `TenantId` 为 `long?`，`null` 表示宿主/公共数据。**BasicApp 应用层**另用 `TenantId = 0` 表示全局/宿主数据——这是**应用侧约定**，并非框架强制，框架自身不认识「0 号租户」这一特例。
- **解析中间件不在本包**：`XiHanTenantResolveMiddleware` 与 Header/QueryString 贡献者在 [Web.Api](./web-api)。只引用本包不会自动解析请求中的租户，需要 Web.Api 模块接入中间件。
- **中间件在认证之后**：解析链首位的 `CurrentUserTenantResolveContributor` 依赖 `ICurrentUser`，因此中间件排在 `UseAuthentication()` 之后才能拿到已认证用户的 `TenantId`。
- **默认存储是内存快照**：`DefaultTenantStore` 每次查询会克隆配置快照（含连接串）返回，适合小规模静态租户；生产多租户建议替换为数据库版 `ITenantStore`。

## 依赖模块

- [XiHan.Framework.Core](./core)
- [XiHan.Framework.MultiTenancy.Abstractions](./multitenancy-abstractions)（接口与模型来源）
- [XiHan.Framework.Settings](./settings)（把租户级设置提供者接入设置链、承载功能开关）
- [XiHan.Framework.Security](./security)（`ICurrentUser`，用于按当前用户解析租户）

## 相关模块

- [XiHan.Framework.MultiTenancy.Abstractions](./multitenancy-abstractions)
- [XiHan.Framework.Settings](./settings)
- [XiHan.Framework.Web.Api](./web-api)（解析中间件与 Header/QueryString 贡献者）
- [XiHan.Framework.Data](./data)（按租户过滤/路由数据）
