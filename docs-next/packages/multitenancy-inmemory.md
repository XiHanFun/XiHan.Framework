# XiHan.Framework.MultiTenancy.InMemory

多租户的默认实现：启动时从配置装载租户，存在进程内存里，有容量上限，运行时可增删。它提供 `multitenancy.context@1` 与 `multitenancy.sharding@1`，适用于租户数量少、随配置发布的应用，以及开发与测试。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.MultiTenancy.InMemory` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `multitenancy.context@1`、`multitenancy.sharding@1`；依赖 `kernel.hosting@1` |
| 特性入口 | `InMemoryMultiTenancyFeature`（清单 `xihan.multitenancy.inmemory`，`role: default`） |
| 配置节 | `XiHan:MultiTenancy`（含 `Tenants` 与 `InMemory` 子节） |
| 依赖 | `XiHan.Framework.MultiTenancy.Abstractions` |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

`InMemoryTenantStore` 是一张按租户 ID 索引的并发字典，读取无锁，新增与替换在同一把锁里先查容量再写入，并发新增不会越过上限。特性在 `Configure` 里注册：

- `MultiTenancyOptions` 与 `InMemoryMultiTenancyOptions`（都从配置读取）、`InMemoryTenantStore` 与 `ITenantStore`。
- 解析器 `ClaimTenantResolver` → `BaggageTenantResolver`，注册顺序即解析顺序。
- Tenant 阶段的 `TenantResolutionMiddleware`。
- 分片能力被选中时的 `TenantStoreShardResolver`；作用域服务 `TenantShardScope`（与是否选中分片无关）。
- 健康项 `multitenancy.inmemory.capacity`，由存储自己同步计算。

清单角色是 `default`：装配一个显式的多租户 Provider（如 `MultiTenancy.Database`）后本包被顶替，不装配、不启动。

## 何时使用

- 租户随配置发布、数量在千级以内，或开发、演示、自动化测试：用本包。
- 租户由管理后台在运行时增删、多实例共享同一份目录：用 [MultiTenancy.Database](./multitenancy-database)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.MultiTenancy.InMemory" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。`Profiles.Standard` 与 `Profiles.Full` 已包含本包；单独装配时：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.MultiTenancy.InMemory;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseFeature<InMemoryMultiTenancyFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:MultiTenancy:Tenants:<id>:DisplayName` | `string` | 无 | 显示名；节名 `<id>` 就是租户 ID，按租户 ID 文法校验 |
| `XiHan:MultiTenancy:Tenants:<id>:IsEnabled` | `bool` | `true` | 是否启用；只接受 `true` / `false` |
| `XiHan:MultiTenancy:Tenants:<id>:Properties:<key>` | `string` | 无 | 租户属性，分片键见下表 |
| `XiHan:MultiTenancy:InMemory:MaxTenants` | `int` | 1000 | 租户上限，正整数 |

`DefaultTenant`、`RequireTenant`、`ValidateAgainstStore`、`TenantClaim` 四个解析选项也在 `XiHan:MultiTenancy` 下，见 [MultiTenancy.Abstractions](./multitenancy-abstractions#配置)。

```json
{
  "XiHan": {
    "MultiTenancy": {
      "RequireTenant": false,
      "InMemory": { "MaxTenants": 1000 },
      "Tenants": {
        "acme": {
          "DisplayName": "Acme Corp",
          "Properties": {
            "ConnectionString": "Host=localhost;Database=acme",
            "DatabaseProvider": "postgresql"
          }
        },
        "beta": { "DisplayName": "Beta" },
        "globex": { "DisplayName": "Globex", "IsEnabled": false }
      }
    }
  }
}
```

分片能力被选中时，`TenantStoreShardResolver` 从租户属性推出分片：

| 属性键 | 含义 |
| --- | --- |
| `ConnectionString` | 有值即该租户分库 |
| `DatabaseProvider` | 数据库提供者名，可选，转成小写 |
| `Shard.<key>` | 去掉前缀后作为 `<key>` 透传给持久化 Provider |

上例里 `acme` 分库，`beta` 与 `globex` 共库。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `InMemoryMultiTenancyFeature` | 特性入口；`HealthName` = `multitenancy.inmemory.capacity` |
| `InMemoryMultiTenancyOptions` | `MaxTenants`（缺省 1000）、`Tenants`（启动时装载的租户）；`FromConfiguration` 读取上面的配置 |
| `InMemoryTenantStore` | `ITenantStore` 与健康项贡献者：`Set(tenant)` 新增或替换、`Remove(tenantId)` 删除、`Capacity`、`Count` |

## 使用示例

运行时增删租户。`Set` 与 `Remove` 不会自动失效分片缓存，改了租户连接后要调用 `ITenantShardResolver.Invalidate`：

```csharp
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Primitives;

public sealed class TenantAdministration(InMemoryTenantStore store, ITenantShardResolver shards)
{
    public Result MoveToOwnDatabase(string tenantId, string connectionString)
    {
        var updated = store.Set(new TenantInfo
        {
            Id = tenantId,
            Properties = new Dictionary<string, string>(StringComparer.Ordinal) { [TenantShardKeys.ConnectionString] = connectionString },
        });

        // 解析器缓存了旧结果（包括"无分片"），不失效就继续落旧库
        if (updated.IsSuccess)
        {
            shards.Invalidate(tenantId);
        }

        return updated;
    }
}
```

`ITenantShardResolver` 只在分片能力被选中时注册。本包同时提供两个能力，单独装配本包时两者都由它提供。

## 行为与限制

- 容量：新增超过 `MaxTenants` 时 `Set` 返回 `MULTITENANCY_UNAVAILABLE_001`；替换已有租户不占新容量。配置里的租户超过上限时，构造存储即抛出 `FrameworkException`。
- 健康项 `multitenancy.inmemory.capacity`：租户数达到上限即 Degraded（原因 `MULTITENANCY_UNAVAILABLE_001`），否则 Ready。
- ID 区分大小写；`ListAsync` 按 ID 序数排序返回全部租户。
- 数据只在进程内存里：多实例部署时各实例各有一份，运行时的增删不会同步到其他实例，重启后恢复为配置内容。
- 分片缓存容量 `ShardCacheSize` 缺省 10000，配置里没有对应的键。

## 相关

- [多租户](../guide/multi-tenancy)
- [MultiTenancy.Abstractions](./multitenancy-abstractions)
- [MultiTenancy.Database](./multitenancy-database)
- [Profiles.Standard](./profiles-standard)
- 规范：RFC-0007《多租户》；ADR-0018
