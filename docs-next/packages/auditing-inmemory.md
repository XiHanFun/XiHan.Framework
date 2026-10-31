# XiHan.Framework.Auditing.InMemory

审计的默认实现：有界环形缓冲同时充当接收器与查询，每条事件可同时写一条日志。提供 `auditing.trail@1`，适用于开发、测试与单实例应用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Auditing.InMemory` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `auditing.trail@1`（`role: default`）；依赖 `kernel.hosting@1` |
| 特性入口 | `InMemoryAuditingFeature`（清单 `xihan.auditing.inmemory`） |
| 配置节 | `XiHan:Auditing`（中间件选项）、`XiHan:Auditing:InMemory` |
| 依赖 | `XiHan.Framework.Auditing.Abstractions` |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

`InMemoryAuditTrail` 实现 `IAuditSink` 与 `IAuditTrail`：写入在锁内追加到一个先进先出的缓冲，保留最近 `Capacity` 条，满了丢最旧的；每条事件分配一个递增序号，查询按序号升序遍历缓冲，续页令牌就是本页最后一条的序号。最旧的事件被淘汰后序号不回退，已发出的令牌仍指向正确的位置。

特性被能力图谱选中时把 `InMemoryAuditTrail` 绑定为接收器与查询并调用 `AddAuditing()` 挂上审计中间件。应用再装配一个显式 Provider（如 [Auditing.Database](./auditing-database)）时，本包不被选中，既不绑定也不挂中间件。

## 何时使用

开发环境、集成测试、只需近期留痕的单实例服务。数据只在一个进程里，重启即失，多实例之间也不共享；要持久化或跨实例查询时用 [Auditing.Database](./auditing-database)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Auditing.InMemory" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。`Profiles.Full` 已包含本包；用其他组合时：

```csharp
using XiHan.Framework.Auditing.InMemory;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<InMemoryAuditingFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Auditing:InMemory:Capacity` | int，大于 0 | 10000 | 保留的事件数上限，满了丢最旧的 |
| `XiHan:Auditing:InMemory:LogEvents` | bool | `true` | 每条事件以 Information 级写一条日志 |

中间件的 `Selection` 与 `IncludeSuccesses` 在 `XiHan:Auditing`，见 [Auditing.Abstractions](./auditing-abstractions#配置)。

```json
{
  "XiHan": {
    "Auditing": {
      "Selection": "Protected",
      "IncludeSuccesses": true,
      "InMemory": { "Capacity": 10000, "LogEvents": true }
    }
  }
}
```

`Capacity` 不是正整数、`LogEvents` 不是 `true` / `false` 时，`InMemoryAuditingOptions.FromConfiguration` 抛出 `ArgumentException`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `InMemoryAuditingFeature` | 能力包入口 |
| `InMemoryAuditTrail` | 接收器与查询；`Capacity` 是容量，`Count` 是当前保留的事件数 |
| `InMemoryAuditingOptions` | `Capacity`、`LogEvents`；`ConfigurationSection` 为 `XiHan:Auditing:InMemory` |

## 使用示例

在测试里用 `XiHan.Framework.Testing` 的 `TestHost` 写入并读回：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Auditing.Abstractions.Pipeline;
using XiHan.Framework.Auditing.Abstractions.Trail;
using XiHan.Framework.Auditing.InMemory;
using XiHan.Framework.Testing;

public sealed class AuditTrailTests
{
    [Fact]
    public async Task ExplicitAction_IsRecorded()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryAuditingFeature>());

        // 非操作调用没有动作就不记录；显式声明后一定记录
        await host.InvokeAsync(static context =>
        {
            context.SetAuditAction("reports.rebuild");
            return ValueTask.CompletedTask;
        });

        var trail = host.Services.GetRequiredService<IAuditTrail>();
        var page = (await trail.QueryAsync(new AuditQuery { Action = "reports.rebuild" }, CancellationToken.None)).ValueOrThrow();
        Assert.Single(page.Events);
    }
}
```

`IAuditTrail` 是单例，可以在任何地方解析，不需要活动的工作单元。

## 行为与限制

- 有界：最多保留 `Capacity` 条，写入在锁内完成，不等待任何外部资源。
- 续页令牌是十进制序号，不能解析或为负数时返回 `AUDITING_VALIDATION_001`；页大小越界、起止颠倒同样返回该错误码。
- `LogEvents` 的日志模板是 `Audit {Action} {Outcome} by {PrincipalId} in tenant {TenantId} on {Resource} ({TraceId}).`，成功时 `Outcome` 为 `success`，失败时为错误码。
- 单进程、重启即失，多实例之间不共享。
- 记录不按租户分区，查询要显式传 `TenantId`。

## 相关

- [审计](../guide/auditing)
- [Auditing.Abstractions](./auditing-abstractions)
- [Auditing.Database](./auditing-database)
- [Profiles.Full](./profiles-full)
- 规范：RFC-0015《审计》
