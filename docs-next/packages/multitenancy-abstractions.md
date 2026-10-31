# XiHan.Framework.MultiTenancy.Abstractions

多租户的抽象包：定义租户信息、租户存储、租户解析器、Tenant 阶段中间件与租户分片契约，能力 `multitenancy.context@1` 与 `multitenancy.sharding@1` 由多租户 Provider 提供。实现自己的租户存储或分片解析器时引用它。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.MultiTenancy.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 定义 `multitenancy.context@1`、`multitenancy.sharding@1`（`MultiTenancyCapabilities`），本包不提供 |
| 特性入口 | — |
| 配置节 | `XiHan:MultiTenancy`（`MultiTenancyOptions`，由 Provider 读取） |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`、`Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.13 |
| AOT | 兼容 |
| 稳定性 | Stable；分片相关类型（`Sharding` 命名空间）为 Preview |

## 概述

租户是调用上下文的一部分。`TenantResolutionMiddleware` 在 Tenant 阶段按注册顺序调用 `ITenantResolver`，取第一个结果（没有时用默认租户），校验文法，按选项核对 `ITenantStore`（存在且启用），然后写进安全上下文 `SecurityContext.Tenant`；容器里有 `ITenantShardResolver` 时再解析分片写进调用上下文。失败以结果返回，不抛异常。没有环境态的租户访问器，读租户用 `context.GetSecurity().Tenant`。

本包还带两个解析器（主体声明、Baggage）与从租户属性推出分片的缺省解析器 `TenantStoreShardResolver`，多租户 Provider 直接注册它们，只实现存储。持久化 Provider 经 `TenantShardBinding.Resolve` 读分片选连接，管道外的作用域经 `TenantShardScope` 显式绑定分片（ADR-0057）。

## 何时使用

- 写自己的租户存储（`ITenantStore`）或分片解析器（`ITenantShardResolver`）时引用本包，并用 `MultiTenancy.Conformance` 验证。
- 业务代码要读分片、在管道外绑定分片、校验租户 ID 时引用本包。
- 只是启用多租户时直接装 [MultiTenancy.InMemory](./multitenancy-inmemory) 或 [MultiTenancy.Database](./multitenancy-database)，它们会带上本包。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.MultiTenancy.Abstractions" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。本包没有特性入口，`Profiles.Standard` 与 `Profiles.Full` 已经引用它。

## 配置

`MultiTenancyOptions.FromConfiguration` 读取 `XiHan:MultiTenancy` 下的四个键：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `DefaultTenant` | `string` | 无 | 解析器都没给出值时使用，同样经过文法与存储校验；空字符串表示清除 |
| `RequireTenant` | `bool` | `false` | 没有租户的调用返回 `MULTITENANCY_VALIDATION_002` |
| `ValidateAgainstStore` | `bool` | `true` | 租户必须在存储中存在且启用；关闭后只校验文法 |
| `TenantClaim` | `string` | `tenant` | `ClaimTenantResolver` 读取的声明类型 |

`ShardCacheSize`（`TenantStoreShardResolver` 的缓存容量，缺省 10000，最小 1）只能在代码里构造选项时设置，没有配置键。

```json
{
  "XiHan": {
    "MultiTenancy": {
      "DefaultTenant": "acme",
      "RequireTenant": false,
      "ValidateAgainstStore": true,
      "TenantClaim": "tenant"
    }
  }
}
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `Tenants.TenantInfo` | 租户：`Id`、`DisplayName`、`IsEnabled`（缺省 `true`）、`Properties`（键区分大小写） |
| `Tenants.TenantIdentifier` | 租户 ID 文法 `^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$`：`IsValid`、`Validate`、`MaxLength`（64） |
| `Tenants.ITenantStore` | 租户存储：`FindAsync`、`ListAsync`；ID 区分大小写，基础设施失败抛异常 |
| `Resolution.ITenantResolver` | 从调用上下文解析租户 ID 候选，只解析不校验 |
| `Resolution.ClaimTenantResolver` | 读已认证主体的租户声明（第一个值）；未认证主体跳过 |
| `Resolution.BaggageTenantResolver` | 读 Baggage `xihan.tenant`，即调用方自报的租户 |
| `Resolution.TenantResolutionMiddleware` | Tenant 阶段的中间件 |
| `MultiTenancyOptions` | 选项，见上表 |
| `Sharding.ITenantShardResolver` | 分片解析器：`ResolveAsync` → `Result<Maybe<TenantShard>>`、`Invalidate(tenantId?)` |
| `Sharding.TenantShard` | 分片：`TenantId`、`ConnectionString`、`Provider`（转小写，可空）、`Properties` |
| `Sharding.TenantStoreShardResolver` | 从租户属性推出分片的缺省解析器，按租户有界缓存；`FromProperties(tenant)` 不带缓存地推出单个租户的分片 |
| `Sharding.TenantShardKeys` | 租户属性键 `ConnectionString`、`DatabaseProvider`、前缀 `Shard.`，绑定属性键 `tenant`、`provider` |
| `Sharding.TenantShardInvocationExtensions` | `GetTenantShard()` / `SetTenantShard(shard)`：调用上下文里的分片 |
| `Sharding.TenantShardBinding` | `Resolve(scopedServices, bindingName, bindingType, fallback)`：持久化 Provider 创建客户端时取分片连接绑定 |
| `Sharding.TenantShardScope` | 作用域服务，管道外显式绑定分片：`BindTenantAsync`、`Bind`、`Shard`、`IsConnected` |
| `MultiTenancyCapabilities` | 能力 ID 常量 `Context`、`Sharding` 与 `Version`（1） |
| `MultiTenancyErrors` / `MultiTenancyMessages` | 错误码与消息键常量，消息目录 `MultiTenancyMessages.Catalog` |

## 使用示例

自定义租户存储，例如从应用已有的组织服务读租户：

```csharp
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;

public sealed class FixedTenantStore : ITenantStore
{
    private readonly Dictionary<string, TenantInfo> _tenants = new(StringComparer.Ordinal)
    {
        ["acme"] = new TenantInfo { Id = "acme", DisplayName = "Acme Corp" },
        ["globex"] = new TenantInfo { Id = "globex", DisplayName = "Globex", IsEnabled = false },
    };

    public ValueTask<Maybe<TenantInfo>> FindAsync(string tenantId, CancellationToken cancellationToken)
        => ValueTask.FromResult(_tenants.TryGetValue(tenantId, out var tenant) ? Maybe.Some(tenant) : Maybe.None<TenantInfo>());

    public ValueTask<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult<IReadOnlyList<TenantInfo>>([.. _tenants.Values.OrderBy(static t => t.Id, StringComparer.Ordinal)]);
}
```

多租户 Provider 以 `TryAddSingleton<ITenantStore>` 注册自己的存储，应用在装配前登记 `services.AddSingleton<ITenantStore, FixedTenantStore>()` 即可顶替；解析器、中间件与分片解析器沿用 Provider 注册的。

在处理器里读分片，例如按租户库决定是否执行某项迁移：

```csharp
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.Primitives;

public static class ShardInfo
{
    // 共库租户与无租户的调用没有分片
    public static string Describe(InvocationContext context)
        => context.GetTenantShard().TryGetValue(out var shard)
            ? $"{shard.TenantId} -> {shard.Provider ?? "default"}"
            : "shared";
}
```

管道外的作用域绑定分片的完整示例见[多租户 · 管道外绑定分片](../guide/multi-tenancy#管道外绑定分片)。

## 行为与限制

- 解析顺序就是 `ITenantResolver` 的注册顺序。Provider 先注册 `ClaimTenantResolver` 再注册 `BaggageTenantResolver`，已认证主体所属的租户优先于调用方自报的租户；主体没有租户声明时 Baggage 生效，中间件只核对文法与存储，不判断主体是否属于该租户。
- 默认租户同样经过文法与存储校验；关闭存储核对后安全上下文里没有显示名。
- 分片解析失败（租户存储不可达）返回 `MULTITENANCY_UNAVAILABLE_002`，调用失败，不降级为共库。
- `TenantStoreShardResolver` 缓存"无分片"与未知租户的结果；条目数达到 `ShardCacheSize` 时整体清空，不逐条淘汰；租户连接变更后调用 `Invalidate`。
- `TenantShardScope` 必须在作用域首次创建持久化客户端之前绑定；之后再绑定或改绑另一分片抛出 `InvalidOperationException`，同一分片重复绑定无副作用。`BindTenantAsync` 不看租户是否启用、不受应用状态影响；没有分片解析器时成功且不绑定。

| 错误码 | 含义 |
| --- | --- |
| `MULTITENANCY_VALIDATION_001` | 租户 ID 不合法 |
| `MULTITENANCY_VALIDATION_002` | 调用必须带租户但没有解析到 |
| `MULTITENANCY_NOTFOUND_001` | 租户不存在 |
| `MULTITENANCY_DENIED_001` | 租户已禁用 |
| `MULTITENANCY_UNAVAILABLE_001` | 租户存储容量已满（内存实现） |
| `MULTITENANCY_UNAVAILABLE_002` | 租户分片无法解析（租户存储不可达） |

六个消息键（`multitenancy.tenant.*`、`multitenancy.store.capacity_exceeded`、`multitenancy.shard.unavailable`）在本包的 `Localization/<区域>.json` 里有 10 种区域的文案，Provider 装配时注册 `MultiTenancyMessages.Catalog`。

## 相关

- [多租户](../guide/multi-tenancy)
- [MultiTenancy.InMemory](./multitenancy-inmemory)
- [MultiTenancy.Database](./multitenancy-database)
- [Conformance 一致性套件](./conformance)
- 规范：RFC-0007《多租户》；ADR-0018、ADR-0057
