# XiHan.Framework.MultiTenancy.Abstractions

> 多租户抽象：当前租户上下文、多租户实体标记、租户解析链契约与 URL 提供者接口

- **NuGet**：`XiHan.Framework.MultiTenancy.Abstractions`
- **模块类**：`XiHanMultiTenancyAbstractionsModule`
- **所在层**：应用层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（`XiHan.Framework.Core`）

## 概述

本包是多租户能力的**抽象层**，只定义接口与基础模型，不含任何实现逻辑。它约定了三件核心的事：「当前请求属于哪个租户」（`ICurrentTenant`）、「哪些实体归属租户」（`IMultiTenant`）、「如何从请求中解析出租户」（`ITenantResolveContributor` / `ITenantResolveContext`）。上层领域/应用代码依赖这些接口而非具体实现，从而与落地方式解耦。真正的实现由 [XiHan.Framework.MultiTenancy](./multitenancy) 提供，二者通过依赖倒置组合。

模块类 `XiHanMultiTenancyAbstractionsModule` 本身是**空实现**——它不注册任何服务，只作为模块依赖锚点存在，让实现包与上层模块通过 `[DependsOn]` 统一引入抽象契约。

## 何时使用

- 你的领域/应用代码想读取当前租户信息，但不想耦合具体实现——注入 `ICurrentTenant` 即可
- 你要定义支持租户隔离的实体——实现 `IMultiTenant`（暴露 `long? TenantId`）
- 你要编写自定义的租户解析逻辑——实现 `ITenantResolveContributor`
- 你需要标记某段代码/实体忽略多租户过滤——`IgnoreMultiTenancyAttribute`
- 你需要按租户生成/解析 URL——实现或依赖 `IMultiTenantUrlProvider`

通常无需单独引用本包：引用实现包 [XiHan.Framework.MultiTenancy](./multitenancy) 时会自动带上这些抽象。

## 安装与启用

```bash
dotnet add package XiHan.Framework.MultiTenancy.Abstractions
```

```csharp
[DependsOn(typeof(XiHanMultiTenancyAbstractionsModule))]
public class MyModule : XiHanModule { }
```

模块类不注册任何服务；真正的服务注册（当前租户访问器、租户存储、解析链等）在实现包 [XiHan.Framework.MultiTenancy](./multitenancy) 中完成。

## 核心能力

- **当前租户契约**：`ICurrentTenant` 提供 `Id`（`long?`）、`Name`、`IsAvailable`，以及用 `using` 临时切换租户的 `Change(...)`
- **租户信息载体与访问器**：`BasicTenantInfo` 承载租户 Id/Name，`ICurrentTenantAccessor` 读写「当前」`BasicTenantInfo`
- **多租户实体标记**：`IMultiTenant` 暴露 `long? TenantId`，`null` 表示宿主（Host）/公共数据
- **租户解析链契约**：`ITenantResolveContributor` + `ITenantResolveContext`，把请求中的标识/名称解析成租户
- **忽略多租户**：`IgnoreMultiTenancyAttribute`
- **租户 URL 契约**：`IMultiTenantUrlProvider` 按租户模板生成 URL

## 主要 API / 类型

### 当前租户上下文

| 类型 | 说明 |
| --- | --- |
| `ICurrentTenant` | 当前租户上下文。属性 `bool IsAvailable`、`long? Id`、`string? Name`；方法 `IDisposable Change(long? id, string? name = null)`（临时切换，`Dispose` 时恢复上一层） |
| `ICurrentTenantAccessor` | 当前租户信息读写访问器。属性 `BasicTenantInfo? Current { get; set; }` |
| `BasicTenantInfo` | 只读租户信息载体。构造 `BasicTenantInfo(long? tenantId, string? name = null)`；属性 `long? TenantId`、`string? Name` |

### 多租户实体与解析链

| 类型 | 说明 |
| --- | --- |
| `IMultiTenant` | 多租户实体接口，暴露 `long? TenantId { get; }`（`null` = 宿主/公共数据） |
| `ITenantResolveContributor` | 租户解析贡献者。属性 `string Name`；方法 `Task ResolveAsync(ITenantResolveContext context)` |
| `ITenantResolveContext` | 解析上下文（继承 `IServiceProviderAccessor`）。属性 `string? TenantIdOrName { get; set; }`、`bool Handled { get; set; }` |
| `IMultiTenantUrlProvider` | 按租户生成 URL 的契约。方法 `Task<string> GetUrlAsync(string templateUrl)` |
| `IgnoreMultiTenancyAttribute` | 标记忽略多租户过滤（`AttributeUsage = AttributeTargets.All`，无成员） |

> `ITenantResolveContext` 继承自 `XiHan.Framework.Core.DependencyInjection.IServiceProviderAccessor`，因此解析贡献者可通过 `context.ServiceProvider` 取到当前请求的服务（如 `IHttpContextAccessor`、`ICurrentUser`）来完成解析。

## 使用示例

### 读取与临时切换当前租户

```csharp
public class ReportService
{
    private readonly ICurrentTenant _currentTenant;

    public ReportService(ICurrentTenant currentTenant)
    {
        _currentTenant = currentTenant;
    }

    public void Run(long tenantId)
    {
        // 临时切换到指定租户，using 结束后自动恢复到上一层
        using (_currentTenant.Change(tenantId, "Acme"))
        {
            var id = _currentTenant.Id;          // 当前作用域内为 tenantId
            var available = _currentTenant.IsAvailable; // true
        }
    }
}
```

### 定义多租户实体

```csharp
public class Order : IMultiTenant
{
    public long Id { get; set; }

    // null 表示宿主/公共数据；非 null 表示归属某租户
    public long? TenantId { get; set; }
}
```

## 扩展点 / 自定义

- **自定义租户解析贡献者**：实现 `ITenantResolveContributor`（或继承实现包提供的 `TenantResolveContributorBase`），在 `ResolveAsync` 里把解析出的标识写入 `context.TenantIdOrName` 并置 `context.Handled = true` 短路后续贡献者。
- **自定义 URL 策略**：实现 `IMultiTenantUrlProvider`，按租户把模板 URL 渲染成最终地址（如 `{tenant}.example.com`）。

## 注意事项与最佳实践

- **`null` = 宿主/公共**：`ICurrentTenant.Id` 与 `IMultiTenant.TenantId` 均为 `long?`，框架层约定 `null` 表示宿主（Host）数据，而非某个特殊数字。BasicApp 应用层另有 `TenantId = 0` 表宿主的约定，那是**应用侧约定**，见 [MultiTenancy](./multitenancy)。
- **`IsAvailable` 语义**：实现包中 `IsAvailable` 等价于 `Id.HasValue`；宿主上下文（`Id == null`）下 `IsAvailable` 为 `false`。
- **抽象包无副作用**：本包只有接口/模型，模块类不注册服务；不要指望单独引用它就能获得当前租户，必须引用实现包。

## 依赖模块

- [XiHan.Framework.Core](./core)（模块化与 DI 抽象，`IServiceProviderAccessor` 来源）

仅依赖 Core 与 BCL，不引入任何第三方库。

## 相关模块

- [XiHan.Framework.MultiTenancy](./multitenancy)（本抽象的实现包）
- [XiHan.Framework.Settings](./settings)（租户级设置来源）
- [XiHan.Framework.Data](./data)（按租户过滤/路由数据）
