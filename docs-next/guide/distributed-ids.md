# 标识生成

本页讲标识生成端口 `IIdGenerator<TKey>`、各持久化 Provider 缺省注册的 UUID v7 生成器，以及可选的雪花 `long` 标识（位布局、机器位分配、时钟回拨、错误码与解析），适用于在应用侧为实体分配主键的开发者。完整 API 见 [Persistence.Abstractions 包](../packages/persistence-abstractions)。

## 标识生成端口

`IIdGenerator<TKey>` 只有一个方法 `NewId()`。实体的标识在应用侧生成，写进 `Id` 后再交给仓储新增，不依赖数据库自增：SqlSugar 约定把 `Id` 设为主键但不自增；EF Core 按约定把整数主键当作数据库生成的值，应用自己生成整数标识时在模型里配 `ValueGeneratedNever()`（一致性套件的 `DbContext` 就是这样配的）。

| 生成器 | 标识类型 | 怎样得到 | 说明 |
| --- | --- | --- | --- |
| `GuidV7IdGenerator` | `Guid` | 每个持久化 Provider 都注册为 `IIdGenerator<Guid>` | UUID 版本 7：前 48 位是毫秒时间戳，按时间有序；不需要任何协调 |
| `SnowflakeIdGenerator` | `long` | 应用显式装配 `SnowflakeIdFeature` 后注册为 `IIdGenerator<long>` | 41 位毫秒时间戳 + 10 位机器位 + 12 位序列；需要独占的机器位 |

Provider 与 `SnowflakeIdFeature` 都用 `TryAddSingleton` 注册生成器，应用在 `AddXiHan()` 之前自己注册的 `IIdGenerator<TKey>` 优先。

SimpleApp 的下单操作注入 `IIdGenerator<Guid>`，在创建实体时分配标识：

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace SimpleApp.Orders;

[Operation(Id, Expose = Expose.Http | Expose.Grpc | Expose.Mcp, Permission = "orders.write")]
public sealed class CreateOrder(IRepository<Order, Guid> orders, IIdGenerator<Guid> ids, TimeProvider clock) : IOperation<PlaceOrderRequest, OrderResponse>
{
    public const string Id = "orders.create";

    public async ValueTask<Result<OrderResponse>> HandleAsync(PlaceOrderRequest request, InvocationContext context)
    {
        // 标识在应用侧生成，领域事件里就能带上它
        var order = new Order { Id = ids.NewId(), Name = request.Name, Amount = request.Amount };
        order.Place(clock.GetUtcNow());

        var added = await orders.AddAsync(order, context.CancellationToken);
        return added.IsFailure ? added.Error! : new OrderResponse(order.Id, order.Name, order.Amount, order.CreatedAt);
    }
}
```

`PlaceOrderRequest`、`OrderResponse` 与 `Order` 都来自 SimpleApp。

## 选 Guid 还是 long

缺省用 `Guid`：任何 Provider 都已注册，实例之间不需要协调，前端按字符串处理也没有精度问题。需要 64 位整数主键时用雪花标识，例如表的主键已经是 `bigint`、索引体积敏感，或与其他系统约定了整数标识。

| | `GuidV7IdGenerator` | `SnowflakeIdGenerator` |
| --- | --- | --- |
| 长度 | 128 位 | 64 位 |
| 顺序 | 按毫秒时间有序，同一毫秒内由随机位决定 | 按毫秒时间有序，同一毫秒内按序列递增 |
| 依赖 | 只需 `TimeProvider` | 固定机器位，或一个 `caching.distributed` Provider 提供的 `IDistributedLock` |
| 单实例数量限制 | 没有每毫秒的数量限制 | 每毫秒最多 4096 个，用尽等下一毫秒 |
| 失败 | 不涉及机器位与时钟回拨 | 时钟回拨超过容忍阈值或没有机器位时抛出异常 |
| JSON 与前端 | 字符串 | number，超过 2^53 后在 JavaScript 里丢精度 |

## 启用雪花标识

Profile 不装配雪花标识，应用显式选用：

1. 决定机器位的来源：配置 `XiHan:Persistence:Snowflake:WorkerId` 固定一个值，或装配一个 `caching.distributed` 的 Provider（如 `Caching.Redis`）由分布式锁租约分配。
2. 走租约分配时，缓存 Provider 在 `SnowflakeIdFeature` 之前 `UseFeature`：同层特性按装配顺序启动、逆序停止，雪花特性停止时要能释放租约。
3. `UseFeature<SnowflakeIdFeature>()`。
4. 在需要的地方注入 `IIdGenerator<long>`（或具体类型 `SnowflakeIdGenerator`）。

```csharp
using XiHan.Framework.Caching.Redis;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(x => x
    .UseStandardProfile()
    // Redis 锁跨实例协调机器位；缓存 Provider 装配在雪花特性之前
    .UseFeature<RedisCachingFeature>()
    .UseFeature<SnowflakeIdFeature>());
```

```json
{
  "XiHan": {
    "Bindings": {
      "cache": "localhost:6379"
    }
  }
}
```

固定机器位时不需要缓存 Provider，只配置 `WorkerId`：

```json
{
  "XiHan": {
    "Persistence": {
      "Snowflake": {
        "WorkerId": 7
      }
    }
  }
}
```

::: danger 多实例部署不能用进程内的锁分配机器位
`Profiles.Standard` 里的 `InMemoryCachingFeature` 也提供 `caching.distributed`，但它的锁只在一个进程内有效。只装配它时，每个实例都会租到机器位 0，不同实例生成的标识会重复。多实例部署时装配跨进程的缓存 Provider（如 `Caching.Redis`），或为每个实例配置互不相同的 `WorkerId`（如 Kubernetes StatefulSet 的序号）。
:::

用 `long` 作主键的实体：

```csharp
using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;

namespace SimpleApp.Billing;

public sealed class Invoice : IEntity<long>, IHasCreationTime
{
    public long Id { get; init; }

    [StringLength(64)]
    public string Number { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
```

EF Core 的 `DbContext` 里为它配置 `modelBuilder.Entity<Invoice>(static e => e.Property(static i => i.Id).ValueGeneratedNever());`，SqlSugar 不需要额外配置。

## 位布局

| 位 | 含义 | 范围 |
| --- | --- | --- |
| 63 | 符号位，恒为 0 | — |
| 62–22（41 位） | 自 epoch 起的毫秒数 | 约 69.7 年 |
| 21–12（10 位） | 机器位 | 0–1023 |
| 11–0（12 位） | 同一毫秒内的序列 | 0–4095 |

这些位宽与上限是 `SnowflakeId` 上的常量：`TimestampBits`、`WorkerIdBits`、`SequenceBits`、`MaxWorkerId`、`MaxSequence`、`MaxTimestamp`。缺省 epoch 是 `2026-01-01T00:00:00Z`，41 位毫秒可用到 2095 年前后；用尽时 `NewId()` 抛出 `InvalidOperationException`。

## 配置

配置节 `XiHan:Persistence:Snowflake`，逐键读取：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Epoch` | ISO 8601 日期时间 | `2026-01-01T00:00:00Z` | 时间戳起点，没有时区按 UTC；同一套数据一经写入不得再改，否则已有标识解析出的时间会错位 |
| `WorkerId` | 0–1023 的整数 | 无 | 指定即固定机器位，运维保证集群内不重复；不指定则启动时经分布式锁租约分配 |
| `ClockDriftToleranceMilliseconds` | 非负整数 | `100` | 时钟回拨不超过它时等待时钟追平，超过时拒绝生成 |
| `LeaseTimeToLiveSeconds` | 正整数 | `30` | 租约分配时每个机器位租约的存活时间，每三分之一续约一次 |

非法值（`WorkerId` 越界或带符号、`Epoch` 不是日期时间）在配置阶段抛出 `ArgumentException`，启动失败。epoch 晚于当前时钟也是配置错误：特性在启动时构造生成器，构造即抛出 `ArgumentException`，不会等到第一次生成。

## 机器位分配

雪花标识的唯一性完全依赖机器位独占，分配的每一步都选择拒绝生成而不是冒重复的风险。

- 固定机器位（`FixedWorkerIdSource`）：取配置的 `WorkerId`，不登记健康项。
- 租约分配（`LeasedWorkerIdSource`）：启动时从 0 起逐个尝试锁资源 `persistence:snowflake:worker:<n>`，取第一个空位；1024 个都被占用时启动失败。之后每三分之一存活时间（缺省 10 秒）续约一次；分布式锁本身不续约，续约就是释放再获取同一机器位。
- 续约时空位被别的实例拿走，或锁存储不可达，都按租约丢失处理，下一轮重新分配，新机器位可能与原来不同。
- 本地按获取时刻加存活时间判定租约有效。获取时刻取在请求发出之前，本地到期不晚于存储侧到期；到期还没续上，生成器就没有机器位，`NewId()` 抛出 `PERSISTENCE_UNAVAILABLE_005`。
- 应用停止时释放租约。
- 租约状态以健康项 `persistence.snowflake.worker` 上报：持有为 Ready，没有为 Degraded（`PERSISTENCE_UNAVAILABLE_005`）。

容器里没有 `IDistributedLock`、又没有配置 `WorkerId` 时，启动以 `PERSISTENCE_UNAVAILABLE_005` 中断，异常消息提示配置 `WorkerId` 或装配 `caching.distributed` 的 Provider。框架不做随机机器位。

## 序列与时钟回拨

生成器线程安全，内部用一把锁保护上一个时间戳与序列：

- 同一毫秒内序列从 0 递增到 4095；用尽时自旋等待下一毫秒。
- 时钟回拨不超过 `ClockDriftToleranceMilliseconds`（缺省 100 毫秒）时，在锁内等待时钟追上上一个时间戳再生成；其余线程排队。
- 回拨超过阈值（或等待期间再次回拨超过阈值）时抛出 `PERSISTENCE_UNAVAILABLE_004`，不发出可能重复的标识；时钟追上之后自动恢复。

缺省 100 毫秒的依据是 NTP 对小偏差做渐变调整，只有步进校正才让时钟倒走，通常超过 128 毫秒；100 毫秒以内的回拨视为抖动（ADR-0033）。

## 失败与错误码

`NewId()` 返回 `long`，没有 `Result` 可走，失败以携带错误码的 `FrameworkException` 抛出。异常从处理器里冒出时，Observe 阶段把它转为 `KERNEL_UNEXPECTED_001` 结果，原错误码作为内层错误保留，HTTP 上是 500；要按 UNAVAILABLE（503）回给调用方的应用，自己捕获 `FrameworkException` 并按 `Code` 转成结果。

| 错误码 | 何时出现 | 怎样恢复 |
| --- | --- | --- |
| `PERSISTENCE_UNAVAILABLE_004` | 时钟回拨超过容忍阈值 | 时钟追上上一个时间戳后自动恢复 |
| `PERSISTENCE_UNAVAILABLE_005` | 没有机器位：未配置 `WorkerId` 且没有分布式锁、1024 个机器位都被占用（启动时）；租约尚未取得或已丢失（生成时） | 启动时的情况修正装配后重启；租约丢失由续约循环重新分配 |

## 解析标识

排障时把标识还原成生成时刻、机器位与序列。进程内用生成器自己的 epoch，离线用配置里的 epoch：

```csharp
using XiHan.Framework.Persistence.Abstractions.Identifiers;

// 生成器按自己的 epoch 解析
var parsed = generator.Parse(id);
Console.WriteLine($"{parsed.Timestamp:O} worker={parsed.WorkerId} sequence={parsed.Sequence}");

// 没有生成器实例时显式给出 epoch，必须与生成时一致
var offline = SnowflakeId.Parse(id, SnowflakeOptions.DefaultEpoch);
```

`generator` 是注入的 `SnowflakeIdGenerator`，`id` 是一个已有的雪花标识；负数不是雪花标识，`Parse` 抛出 `ArgumentOutOfRangeException`。`SnowflakeId.Compose(timestamp, workerId, sequence)` 按同一布局拼出标识，任一段越界抛出 `ArgumentOutOfRangeException`。

## 前端精度

标识在 JSON 里保持 number，框架不加字符串转换器。超过 2^53（约 9.007 × 10^15）的整数在 JavaScript 的 `number` 里会丢精度，而雪花标识从 epoch 之后约 25 天起就全部超过 2^53：缺省 epoch 下，现在生成的每一个标识都超过。

::: warning 前端必须按大整数处理雪花标识
前端用能保留大整数的方式解析响应（`JSON.parse` 的 BigInt reviver，或按字符串读取原文），以字符串或 `BigInt` 保存与回传，不能用 `Number()` 换算。
:::

## 自定义生成器

应用自己的 `IIdGenerator<TKey>` 应通过一致性套件 `IdGeneratorConformance<TKey>`（`TKey : IComparable<TKey>`）：顺序生成 10000 个与 8 线程并发生成不重复、时钟前进后生成的标识更大。框架的两个生成器都通过同一套用例：

```csharp
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Conformance;

namespace SimpleApp.Tests;

public sealed class SnowflakeConformanceTests : IdGeneratorConformance<long>
{
    // 套件注入自己的时钟，用来验证时钟前进后标识更大
    protected override IIdGenerator<long> CreateGenerator(TimeProvider clock) =>
        new SnowflakeIdGenerator(new SnowflakeOptions(), new FixedWorkerIdSource(3), clock);
}
```

一致性套件的引用方式见[测试与一致性套件](./testing)。

## 与其他能力的关系

- 分布式锁与缓存 Provider：[缓存与分布式锁](./caching)。
- 实体协议与主键：[领域模型](./domain#实体协议)。
- 健康项的汇总：[生命周期与健康](./lifecycle)。

## 相关

- [Persistence.Abstractions 包](../packages/persistence-abstractions)
- [Caching.Abstractions 包](../packages/caching-abstractions)
- [Caching.Redis 包](../packages/caching-redis)
- [Conformance 一致性套件](../packages/conformance)
- 规范：RFC-0005《领域协议与持久化》的「标识生成」一节
- 决策：ADR-0033
