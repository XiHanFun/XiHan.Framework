# 领域事件与发件箱

这一页讲聚合里记录的领域事件怎样变成消息：事件缓冲在聚合里、用 `AddDomainEvent` 登记映射、Transaction 阶段在提交前派发；再讲事务性发件箱 `OutboxFeature` 怎样让「写业务数据」与「发消息」落进同一个事务。读完你能让下单这样的操作在不手写任何发布代码的情况下发出 `orders.placed` 事件，并保证事件只随成功提交的写入发出。前置知识是[领域模型](/persistence/domain)、[工作单元与事务](/persistence/unit-of-work)与[发布与订阅](/messaging/pubsub)。

## 为什么需要

操作里先写数据库再发消息，看上去只是两行代码，却藏着双写问题：写库成功、发消息失败，下游永远不知道这笔订单；发消息成功、随后提交失败，下游收到了一个并不存在的订单。把发布代码塞进每个操作还让领域逻辑与传输细节纠缠在一起。框架把这两件事拆开：聚合只负责记录「发生了什么」（领域事件），特性在装配时声明事件到主题与 CloudEvents 类型的映射，Transaction 阶段在提交前统一派发；装配发件箱后，派发变成向发件箱表写行，与业务写入在同一个事务里提交或回滚，后台循环再把行投递到代理。操作代码里不出现任何 `IMessagePublisher`。

## 概念

| 术语 | 说明 |
| --- | --- |
| `IDomainEvent` | 领域事件接口，只有 `OccurredAt`；事件通常是 `record` |
| `DomainEventBuffer` / `IHasDomainEvents` | 聚合用缓冲收集事件并经 `DomainEvents` 暴露、`ClearDomainEvents()` 清空；缓冲有容量上限（缺省 256） |
| `AddDomainEvent<TEvent>(topic, type, info, subject)` | 登记一种事件的主题、CloudEvents `type`、源生成的 `JsonTypeInfo<TEvent>`（事件序列化成信封的 `Data` 时不走反射、兼容 Native AOT，事件类型要写进 `[JsonSerializable]` 上下文，见[JSON 序列化与 Native AOT](/common/serialization)）与可选的 `subject` 选择器；首次调用时注册 `IDomainEventDispatcher` |
| `IDomainEventDispatcher` | 持久化定义的派发契约，由消息子系统实现，首次 `AddDomainEvent<TEvent>` 时注册 |
| Transaction 阶段 | 处理器成功后、提交前，`UnitOfWorkMiddleware` 把工作单元登记的、带未派发事件的聚合交给派发器 |
| `OutboxFeature` | 事务性发件箱：`IMessagePublisher` 换成 `OutboxPublisher`，后台 `OutboxDispatcher` 轮询 `OutboxMessage` 表投递 |

事件的流转：仓储写入成功后把聚合登记到工作单元，处理器成功后、提交前由 Transaction 阶段的中间件连同工作单元的租户交给派发器（这一段在[工作单元与事务](/persistence/unit-of-work)里讲）；派发器把事件映射成信封，同一主题一次 `PublishManyAsync`，全部成功后清空各聚合的缓冲，再进入提交。

## 最小示例

下面的代码取自 `framework/samples/SimpleApp`：订单聚合在 `Place` 里记一条 `OrderPlaced`；`OrdersFeature` 登记事件映射与处理器；下单操作只写仓储；`Program.cs` 装配发件箱。

```csharp
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Domain.Events;

public sealed record OrderPlaced(Guid OrderId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed class Order : IAggregateRoot<Guid>, IMultiTenant, IHasCreationTime
{
    // 待发布的领域事件缓冲：仓储写入成功后登记到工作单元，提交前交给派发器，发布成功后清空
    private readonly DomainEventBuffer _events = new();

    public Guid Id { get; init; }
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _events.Items;

    // 下单：记录一条 OrderPlaced，事件随订单写入一起在工作单元内发布
    public void Place(DateTimeOffset now) => _events.Add(new OrderPlaced(Id, now));

    public void ClearDomainEvents() => _events.Clear();
}
```

```csharp
using System.Text.Json.Serialization;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

public sealed record PlaceOrderRequest(string Name, int Amount);

public sealed record OrderResponse(Guid Id, string Name, int Amount, DateTimeOffset CreatedAt);

[JsonSerializable(typeof(OrderPlaced))]
[JsonSerializable(typeof(PlaceOrderRequest))]
[JsonSerializable(typeof(OrderResponse))]
internal sealed partial class SampleJsonContext : JsonSerializerContext;

public sealed class OrdersFeature : IFeature
{
    public const string Topic = "orders";

    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.acme.orders",
        Package = "Acme.Orders",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(PersistenceCapabilities.Relational, PersistenceCapabilities.Version),
            new CapabilityRequirement(MessagingCapabilities.Queue, MessagingCapabilities.Version),
        ],
        Entry = typeof(OrdersFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        // 仓储登记由 Persistence.Generators 从实体类生成
        context.Services.AddGeneratedRepositories();
        // 聚合记录的 OrderPlaced 在工作单元提交前发到 orders 主题（类型 orders.placed，subject 是订单）；装配了发件箱时与订单写入同事务
        context.Services.AddDomainEvent<OrderPlaced>(Topic, "orders.placed", SampleJsonContext.Default.OrderPlaced, static placed => $"order/{placed.OrderId:N}");
        context.Services.AddMessageHandler<OrderPlacedHandler>(Topic);
        // 操作登记由 Kernel.Generators 从 [Operation] 生成
        context.AddGeneratedOperations();
    }
}
```

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

[Operation(Id, Stability = Stability.Experimental, Expose = Expose.Http)]
public sealed class CreateOrder(IRepository<Order, Guid> orders, IIdGenerator<Guid> ids, TimeProvider clock) : IOperation<PlaceOrderRequest, OrderResponse>
{
    public const string Id = "orders.create";

    public async ValueTask<Result<OrderResponse>> HandleAsync(PlaceOrderRequest request, InvocationContext context)
    {
        var order = new Order { Id = ids.NewId(), Name = request.Name, Amount = request.Amount };
        order.Place(clock.GetUtcNow());

        // 仓储把订单登记到工作单元，订单上的 OrderPlaced 在提交前由 Transaction 阶段派发；这里不发布任何消息
        var added = await orders.AddAsync(order, context.CancellationToken);
        return added.IsFailure ? added.Error! : new OrderResponse(order.Id, order.Name, order.Amount, order.CreatedAt);
    }
}
```

`Program.cs` 接在[第一个应用](/getting-started/first-app)换成 Standard 组合后的那份上，`AddXiHan` 里把 `GreetingsFeature` 换成发件箱与 `OrdersFeature`：

```csharp
using XiHan.Framework.Messaging.Abstractions.Outbox;

// 发件箱不在任何组合包里，按需装配；它需要 messaging.queue 与 persistence.relational
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<OutboxFeature>()
    .UseFeature<OrdersFeature>());
```

```json
{ "XiHan": { "Messaging": { "Outbox": { "PollIntervalSeconds": 0.5, "BatchSize": 100 } } } }
```

`POST /orders` 之后：订单写入与一行 `OutboxMessage` 在同一个事务里提交；最多半秒后 `OutboxDispatcher` 把这行发到 `orders` 主题，`OrderPlacedHandler` 收到类型为 `orders.placed`、`subject` 为 `order/<ID>` 的消息。把 `UseFeature<OutboxFeature>()` 去掉，事件改为在提交前直接发给代理，处理器几乎立刻收到，但事件与写入不再是原子的。示例应用里的请求类型还带 DataAnnotations 校验、操作还声明了权限码，这里为聚焦事件把它们省去了；`OrderPlacedHandler` 的写法见[发布与订阅](/messaging/pubsub)，操作契约见[操作契约](/web/operations)，聚合与仓储见[领域模型](/persistence/domain)与[仓储读写](/persistence/repositories)。

## 事件到信封的映射

| 信封属性 | 取值 |
| --- | --- |
| `Id` | 新的 UUID v7（`N` 格式） |
| `Source` | `/<topic>` |
| `Type` | 登记的类型 |
| `Subject` | 选择器的结果，没有选择器或选择器返回 `null` 时省略；选择器返回空串时派发抛 `ArgumentException`（信封不收空的 `Subject`，ADR-0332），没有对象时返回 `null` |
| `Time` | `IDomainEvent.OccurredAt` |
| `Data` | 用登记的 `JsonTypeInfo` 序列化的事件 |
| `DataSchema` | 应用为这个 `Type` 登记了数据模式时带上，没登记的省略 |
| `xihantenant` | 工作单元有租户时带上 |
| 其余扩展 | `traceparent`、`xihanprincipal`、`xihancorrelation`、`xihanlocale` 由发布者从当前调用补齐 |

数据模式按 CloudEvents `type` 登记，领域事件、调度与工作流发布的信封共用这张表（`MessageDataSchemas`，Preview，ADR-0323）：

```csharp
context.Services.AddMessageDataSchema("orders.placed", "https://schemas.example.com/orders/placed.json");
```

部署时只改配置也能登记：配置节 `XiHan:Messaging:DataSchemas` 是数组，每项 `Type` 与 `DataSchema`（`MessagingOptions.DataSchemas`，ADR-0332），由消息 Provider 的特性装配时登记进同一张表。`type` 不进键名，带点号也写得进环境变量：

```json
{ "XiHan": { "Messaging": { "DataSchemas": [ { "Type": "orders.placed", "DataSchema": "https://schemas.example.com/orders/placed.json" } ] } } }
```

```bash
XiHan__Messaging__DataSchemas__0__Type=orders.placed
XiHan__Messaging__DataSchemas__0__DataSchema=https://schemas.example.com/orders/placed.json
```

`type` 与数据模式都不能为空、只有空白或有孤立的代理项，数据模式不核对 URI 语法；同一个 `type` 再登记同一个值只记一次，另一个值抛 `ArgumentException`，最多 10000 个 `type`（配置的与代码的合计），满了再登记新的抛 `InvalidOperationException`。配置项缺 `Type` 或 `DataSchema`、为空或有孤立的代理项时启动失败，消息带配置键；配置与代码登记了同一个 `type` 而值不同时同样失败，不论谁先登记，配置不会悄悄顶掉代码里的登记。表只作用于框架自己构造的信封，应用自己构造的信封照写 `DataSchema`；不登记与以前相同。

同一主题的事件一次 `PublishManyAsync`，主题按首次出现的顺序；任一项失败即派发失败，工作单元回滚，聚合的缓冲保留；全部成功后清空每个聚合的缓冲再提交。

| 情况 | 结果 |
| --- | --- |
| 事件类型没有登记 | 调用以 `MESSAGING_VALIDATION_003` 结束并回滚，一条也不发 |
| 同一事件类型登记两次，或登记表满（10000 种） | 装配时抛 `FrameworkException`（`MESSAGING_VALIDATION_004`） |
| 有事件但没有任何 `AddDomainEvent`（没有派发器） | 调用以 `PERSISTENCE_VALIDATION_003` 结束并回滚 |

## 有没有发件箱的区别

派发发生在提交之前，事件去向取决于是否装配了发件箱：

| 装配 | 事件的去向 | 与业务写入的关系 |
| --- | --- | --- |
| `OutboxFeature` | 写进发件箱表，与业务写入在同一事务；提交后由投递循环发到代理 | 一起提交、一起回滚 |
| 没有发件箱 | 直接发布到代理 | 发布失败回滚业务写入；发布成功而随后提交失败时，事件已经发出 |

开发与单进程测试不装发件箱也能工作；要让事件只随成功的写入发出，装配发件箱。

## 事务性发件箱

`OutboxFeature` 在 `Messaging.Abstractions` 里，清单声明依赖 `messaging.queue@1` 与 `persistence.relational@1`，不在任何组合包里。装配后 `IMessagePublisher` 被替换成 `OutboxPublisher`：

1. 有活动工作单元时（操作与消息处理器的 Transaction 阶段都开着），发布写成 `OutboxMessage` 行（主题、CloudEvents 结构化 JSON、创建时间，`NextAttemptAt` 等于创建时间），批量发布用仓储的 `AddRangeAsync` 一次写入，随业务事务提交或回滚。没有活动工作单元时直接发布到代理。
2. 后台的 `OutboxDispatcher` 每隔 `PollInterval` 跑一轮，每轮一个工作单元：按创建时间取最多 `BatchSize` 条到期的消息（未投递、未进死信、`NextAttemptAt` 为空或已到），首次投递的按主题分组各一次 `PublishManyAsync`，重试的（`Attempts` 不为 0）逐条各一次，最后一次批量写回。工作单元只覆盖发件箱行，代理不在事务里。
3. 删掉超过保留期的已投递行，每轮最多 `BatchSize` 行，与投递同一工作单元。
4. 存储失败（读不出、写不回、提交失败）让本轮回滚，记日志后继续下一轮，只有停止才退出循环。

每条发布过的消息计一次尝试（`Attempts`），结果写回行上：

| 结果 | 行的变化 |
| --- | --- |
| 发布成功 | 记 `DispatchedAt`，清掉 `LastError`；`DispatchedRetention` 为 0 时当场删除 |
| 发布失败，次数未用尽 | 记 `LastError`，`NextAttemptAt` 推后 `min(RetryDelay × 2^(Attempts − 1), MaxRetryDelay)`：翻倍到上限之后一直是上限，对任何尝试次数都是 |
| 发布失败，次数达到 `MaxAttempts` | 记 `DeadLetteredAt` 进死信，日志 Error 级别 |
| 载荷无法解码 | 直接进死信（重试也解不开），`LastError` 是 `MESSAGING_VALIDATION_002` |
| 代理抛出（不可达，或某条消息让整批失败） | 这次调用里的每条按「发布失败」处理，`LastError` 是 `MESSAGING_UNAVAILABLE_001`；记一条 Warning；本轮停止发布，余下的不计尝试、留到下一轮 |
| 宿主停止（调用方取消） | 本轮回滚，不计尝试、不进死信，本轮已发出的下一轮重发 |

退避中与死信的消息不占批次，几条永久失败的消息不会挡住后面的消息。重试逐条发布：代理整批抛出时分不出是哪一条，单独重试后让整批失败的那条只拖累它自己，同批的别的消息随即发出。代理不可达时一轮只有一次调用抛出，积压的消息累计尝试比逐条失败慢；要更长的停机容忍度，调大 `MaxAttempts` 或 `MaxRetryDelaySeconds`（如 15 次、3600 秒约 5.4 小时）。缺省参数下一条消息要连续失败约 20 分钟才进死信，代理短暂拒收不会把消息成批送进死信。

投递是至少一次：一轮发布之后、提交之前崩溃，下一轮会重发。死信留在表里不再投递，供排查；死信行不自动删除，由应用按 `DeadLetteredAt` 清理，或在排除原因后重投，见下一节。处理器侧的死信（代理的死信队列、主题或流）由代理管理，见[队列与流](/messaging/streams)。

## 积压、死信与重投

`OutboxFeature` 带一条健康项 `messaging.outbox.backlog`，经健康端点（见[生命周期与健康](/startup/lifecycle)）看得到发件箱此刻的积压与死信：按 `HealthProbeIntervalSeconds`（缺省 15 秒）在默认库与每个分片库上各数一遍，条目的数据是未投递的行数 `pending`、最老一条等了多久 `oldestPendingSeconds`、死信行数 `deadLettered` 与数过的库数 `databases`。未投递的行数超过 `HealthMaxPending`（缺省 1000）或最老一条等过 `HealthMaxPendingAgeSeconds`（缺省 300）时条目以积压降级，死信行数超过 `HealthMaxDeadLettered`（缺省 0，有死信就降级）时以死信降级；应用随之报告 Degraded，照常接收调用。任何一个库读不出时条目同样降级，不报告不完整的数字。四个阈值键见 [Messaging.Abstractions](/packages/messaging-abstractions#配置)，对应的错误码见它的[错误码](/packages/messaging-abstractions#错误码)。

缺省阈值下发件箱表里只要有死信，条目就是 Degraded：重投或删除死信，或者调大 `HealthMaxDeadLettered`。用 SqlSugar 加租户分片时把 `IncludeShards` 设为 `false`，否则默认库的行按库数重复计数。

死信的原因排除之后（代理恢复、主题建好），用 `OutboxDeadLetters` 批量重投。它由 `OutboxFeature` 登记为单例，自己在每个库上开工作单元，不必在调用里：

```csharp
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.Primitives;

public sealed class OutboxMaintenance(OutboxDeadLetters deadLetters)
{
    // 逐项结果与入参同序：成功、MESSAGING_NOTFOUND_001（没有这一行）或 MESSAGING_CONFLICT_001（不在死信里）
    public ValueTask<IReadOnlyList<Result>> RedriveAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
        => deadLetters.RedriveManyAsync(ids, cancellationToken);
}
```

要重投的标识是 `OutboxMessage.Id`，在操作里用 `IRepository<OutboxMessage, Guid>` 按 `DeadLetteredAt` 不为空查出。重投的行回到待投递，尝试次数从头算，由投递循环在下一轮与首次投递的消息一样按主题成批发出；`LastError` 留到下一次投递的结果覆盖它。不在死信里的行（待投递、退避中或已投递）不改动。某个库的存储失败不挡其他库，还有标识没有着落时才抛出；已经在别的库里重投的行保持已重投，重试时它们是 `MESSAGING_CONFLICT_001`。逐项语义的细则见 [Messaging.Abstractions](/packages/messaging-abstractions#行为与限制)。

## 发件箱表

`OutboxMessage` 是普通实体：`Topic` 长 128，`Payload` 是字节，`LastError` 为长文本，`NextAttemptAt` 与 `DeadLetteredAt` 可空（加在已有数据的表上时旧行为空，视为到期、未进死信）。实体上的 `[EntityIndex(DispatchedAt, DeadLetteredAt, CreatedAt)]` 声明了同时服务取到期消息与保留期清理的索引。用 EF Core 时 `OutboxMessage` 要像其他实体一样进应用 `DbContext` 的模型（`modelBuilder.Entity<OutboxMessage>()`），之后调用 `ApplyPersistenceConventions(Database)` 即得到索引，见 [EF Core Provider](/persistence/entity-framework-core) 与[建表约定与索引](/persistence/schema)。

租户分库时，发件箱行与业务写入同一个事务，落在哪个库由持久化 Provider 决定：EF Core Provider 在带分片的调用里整个 `DbContext` 连到租户库，发件箱行也在租户库；SqlSugar Provider 把发件箱这类平台表留在默认库。投递循环每轮先投递默认库，再逐个投递分片库（按连接去重，每个库各自一个工作单元），分片库的清单从租户目录读出、每 `ShardRefreshIntervalSeconds` 重读一次；用 SqlSugar 时可以把 `IncludeShards` 设为 `false` 省去多余的查询。EF Core 下带分片的调用要发消息，租户库就要建发件箱表；没有建的库每轮投递失败、记日志，不影响别的库。

## 升级前写进的空 subject

信封不再收空的 `Subject`（ADR-0332）。升级之前由返回空串的 `subject` 选择器写进发件箱的行，`Payload` 里带着 `"subject":""`，升级后读不出：按「载荷无法解码」直接进死信，`LastError` 是 `MESSAGING_VALIDATION_002`，原因带 `The subject must not be empty.`。最稳妥的做法是先在旧版本上让选择器在没有对象时返回 `null`，等这类未投递的行投递完再升级。

来不及的，升级后在数据库里把这个成员删掉。`Payload` 是二进制列，存的是 CloudEvents 结构化 JSON 的 UTF-8 字节；`subject` 是它的顶层成员，写在 `datacontenttype` 之前，事件数据 `data` 在 `datacontenttype` 之后。所以只处理未投递的行（`DispatchedAt IS NULL`，进了死信的也算），删掉 `datacontenttype` 之前第一处 `,"subject":""`（13 个字节），`data` 里同样的文字不受影响。表名以应用的模型为准：EF Core 取 `DbContext` 里给 `OutboxMessage` 配的表名，下面按缺省的 `OutboxMessage` 写。

动手之前先数一下受影响的行，WHERE 与下面的 UPDATE 相同：

```sql
-- SQLite
SELECT COUNT(*) FROM "OutboxMessage"
WHERE "DispatchedAt" IS NULL
  AND instr("Payload", CAST(',"subject":""' AS BLOB)) > 0
  AND instr("Payload", CAST(',"subject":""' AS BLOB)) < instr("Payload", CAST(',"datacontenttype":' AS BLOB));

-- PostgreSQL
SELECT COUNT(*) FROM "OutboxMessage"
WHERE "DispatchedAt" IS NULL
  AND position(',"subject":""'::bytea in "Payload") > 0
  AND position(',"subject":""'::bytea in "Payload") < position(',"datacontenttype":'::bytea in "Payload");
```

再删掉这一成员：

```sql
-- SQLite
UPDATE "OutboxMessage"
SET "Payload" = CAST(substr("Payload", 1, instr("Payload", CAST(',"subject":""' AS BLOB)) - 1) || substr("Payload", instr("Payload", CAST(',"subject":""' AS BLOB)) + 13) AS BLOB)
WHERE "DispatchedAt" IS NULL
  AND instr("Payload", CAST(',"subject":""' AS BLOB)) > 0
  AND instr("Payload", CAST(',"subject":""' AS BLOB)) < instr("Payload", CAST(',"datacontenttype":' AS BLOB));

-- PostgreSQL
UPDATE "OutboxMessage"
SET "Payload" = overlay("Payload" placing ''::bytea from position(',"subject":""'::bytea in "Payload") for 13)
WHERE "DispatchedAt" IS NULL
  AND position(',"subject":""'::bytea in "Payload") > 0
  AND position(',"subject":""'::bytea in "Payload") < position(',"datacontenttype":'::bytea in "Payload");
```

其他数据库照同样的办法：在 `DispatchedAt IS NULL` 的行里删掉 `datacontenttype` 之前第一处 `,"subject":""`（13 个字节）。

清理之后，还没进死信的行由投递循环照常发出；已经进了死信的行（`DeadLetteredAt` 不为空）用 `OutboxDeadLetters.RedriveManyAsync` 重投，见[积压、死信与重投](#积压、死信与重投)。RabbitMQ、Kafka、Redis 里升级前写进的这类消息见[队列与流](/messaging/streams#升级前写进的空-subject)。

## 管道外派发

领域事件只由 Transaction 阶段的中间件派发。单例服务、后台循环这些不经管道的代码写入带事件的聚合时，要在提交前自己取出 `unitOfWork.DomainEvents.Pending()` 交给 `IDomainEventDispatcher.DispatchAsync`，否则事件留在聚合的缓冲里不会发出。代码与在管道外开工作单元（`UnitOfWorkScope`）的写法见[工作单元与事务](/persistence/unit-of-work#在管道外开工作单元)。

## 配置

示例只设了 `XiHan:Messaging:Outbox:PollIntervalSeconds`（投递循环的轮询间隔，缺省 1）与 `BatchSize`（每轮最多投递条数，缺省 100）。尝试上限、退避间隔、已投递行的保留期、分片库与健康阈值的其余十个键，连同类型与取值范围，见 [Messaging.Abstractions](/packages/messaging-abstractions#配置)。

## 相关包与下一步

- 包参考：[Messaging.Abstractions](/packages/messaging-abstractions)、[Messaging.InMemory](/packages/messaging-inmemory)、[Persistence.Abstractions](/packages/persistence-abstractions)
- 聚合、事件缓冲与仓储见[领域模型](/persistence/domain)；Transaction 阶段与管道外的工作单元见[工作单元与事务](/persistence/unit-of-work)
- 下一步：[队列与流](/messaging/streams)，在多实例部署下选 RabbitMQ 或 Kafka
