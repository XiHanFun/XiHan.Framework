# 分布式 ID

主键、消息标识、对外短码各需要不同形状的唯一标识。这一章讲四种方案怎么选，以及雪花算法里那几个**上线后改不动**的配置。

完整 API 与全部配置项见 [DistributedIds 包](../packages/distributed-ids)。

## 四种方案

| 方案 | 类型 | 产物 | 能否反解 | 需要节点配置 |
| --- | --- | --- | --- | --- |
| Snowflake | `SnowflakeIdGenerator` | `long`，时间有序 | 可提取时间 / 机器码 / 序列号 | **必须**，`WorkerId` 全局唯一 |
| SequentialGuid | `SequentialGuidGenerator` | `Guid`，毫秒级有序 | 可提取时间 | 不需要 |
| NanoId | `NanoIdGenerator` | 随机字符串 | 不可 | 不需要 |
| Sqids | `SqidsEncoder<T>` | 数字的短码 | 可解回原数字（同一实例内） | 不需要 |

前三个都实现 `IDistributedIdGenerator<TKey>`（`TKey` 是 `long` 或 `Guid`），可以注入。Sqids 是**编码器不是生成器**——它不产生新 ID，只把你已有的数字换个样子。

选型走这条链：

| 问题 | 选择 |
| --- | --- |
| 主键是 `long`，想按时间排序、想从 ID 反查生成时间 | Snowflake |
| 主键是 `Guid`，只想让索引写入友好 | SequentialGuid |
| 要一段猜不出来的短串（邀请码、分享链接、外部令牌串），不需要还原 | NanoId |
| 已有自增/雪花数字 ID，只想对外藏住真实数值和数据量 | Sqids |

::: warning 只有两个生成器在 DI 里
`AddXiHanDistributedIds` 只注册 `IDistributedIdGenerator<Guid>`（SequentialGuid）和 `IDistributedIdGenerator<long>`（Snowflake）。

NanoId 和 Sqids 的 Options 虽然也绑定了配置节，但**没有任何服务消费它们**——要用得自己经 `IdGeneratorFactory` / `new SqidsEncoder<T>(...)` 构造。
:::

## 安装与启用

```bash
dotnet add package XiHan.Framework.DistributedIds
```

```csharp
[DependsOn(typeof(XiHanDistributedIdsModule))]
public class MyModule : XiHanModule { }
```

多数应用不用手写这行——`XiHanApplicationModule`、`XiHanDataModule`、`XiHanEventBusModule`、`XiHanWorkflowModule` 都已经 `DependsOn` 了它。

启用后拿到的两个默认生成器：

| 注入类型 | 实现 | 选项来源 |
| --- | --- | --- |
| `IDistributedIdGenerator<Guid>` | `SequentialGuidGenerator` | `XiHan:DistributedIds:SequentialGuid`，未配置时用 `SequentialAtEnd` |
| `IDistributedIdGenerator<long>` | `SnowflakeIdGenerator` | 基线（`WorkerIdBitLength=6`、`SeqBitLength=12`、`WorkerId=1`）再由 `XiHan:DistributedIds:SnowflakeId` 覆盖 |

基线只是「配置没写这个键时的取值」，写了的键真正生效。

## 核心用法

### 注入生成器

```csharp
public class OrderService(
    IDistributedIdGenerator<long> idGenerator,
    IDistributedIdGenerator<Guid> guidGenerator)
{
    public long NewOrderNo() => idGenerator.NextId();

    public Guid NewRowId() => guidGenerator.NextId();

    public Task<long[]> PreallocateAsync(int count) => idGenerator.NextIdsAsync(count);

    // 从雪花 ID 反查生成时间
    public DateTime WhenCreated(long orderNo) => idGenerator.ExtractTime(orderNo);
}
```

同步方法之外还有 `NextIdAsync` / `NextIdStringAsync` / `NextIdsAsync` / `NextIdStringsAsync`，实现都是 `Task.FromResult` 包装同步逻辑——它们是为了签名统一，不会带来并发收益。

### 实体主键自动填充

数据层已经接好了雪花生成器：SqlSugar 的 `DataExecuting` 事件在插入时调用 `TrySetSnowflakeId`，**四个条件同时满足**才会写入。

| 条件 | 不满足时 |
| --- | --- |
| 该列是主键 | 跳过 |
| 该列不是数据库自增（`IsIdentity`） | 跳过，交给数据库 |
| 属性类型是 `long` 或 `long?` | 跳过，`Guid` 主键不由这里填 |
| 当前值是默认值（0 / null） | 跳过，尊重你手动赋的值 |

所以 `long` 主键实体直接 `InsertAsync` 就有 ID，不用手动 `NextId()`；`Guid` 主键需要自己注入 `IDistributedIdGenerator<Guid>` 赋值。

### 按场景构造 NanoId

```csharp
// URL 安全字符集，长度 21
var nano = IdGeneratorFactory.CreateNanoIdGenerator_UrlSafe(21);
string inviteCode = nano.NextIdString();

// 去掉易混字符（1/l/I、0/O/o 等），适合人工抄写
var readable = IdGeneratorFactory.CreateNanoIdGenerator_Safe(12);
string pickupCode = readable.NextIdString();
```

工厂里还有 `_Numeric` / `_Lowercase` / `_Uppercase` / `_Hex` / `_Custom(alphabet, size)`。

::: danger NanoId 的 `NextId()` 不是分布式唯一
`NextIdString()` 才是 NanoId——加密安全随机串。

`NextId()` 返回的 `long` 是 `(时间戳 << 22) | 进程内序列`，**不含任何节点标识**，多实例部署下不同进程会算出相同的值。别拿它当分布式主键。
:::

### Sqids 短码

```csharp
var encoder = new SqidsEncoder<long>(new SqidsOptions { MinLength = 8 });

string code = encoder.Encode(orderId);      // 对外展示
long[] back = encoder.Decode(code);         // 解回原值，失败返回空数组

// 一次编多个数字（例如 租户 + 主键）
string packed = encoder.Encode(tenantId, orderId);
```

也有便捷扩展方法 `1234L.ToSqid()` / `code.FromSqidToInt64()`，但它们走的是内部静态编码器（默认选项），**不读 `XiHan:DistributedIds:Sqids` 配置节**。

## 关键机制

### 雪花的位布局

64 位从高到低：时间戳 → 数据中心码 → 机器码 → 序列号。

| 段 | 位长来源 | 说明 |
| --- | --- | --- |
| 时间戳 | `TimestampType` 决定：`Milliseconds` 41 位（约 69 年）、`Seconds` 32 位（约 136 年） | 存的是「当前时间 − `BaseTime`」的偏移量 |
| 数据中心码 | `DataCenterIdBitLength`，默认 5 | 传统雪花模式才参与反解 |
| 机器码 | `WorkerIdBitLength`，默认 6（即 `WorkerId` 取值 0–63） | 必须全局唯一 |
| 序列号 | `SeqBitLength`，默认基线 12 | 同一时间片内的自增计数 |

硬约束：`WorkerIdBitLength + SeqBitLength ≤ 22`，越界抛 `ArgumentException`。`WorkerIdBitLength` 范围 1–15，`SeqBitLength` 范围 3–21。

### 单时间片的实际上限由 MaxSeqNumber 决定

这是最容易误判吞吐的地方。`SeqBitLength=12` 看上去每毫秒能发 4096 个，但生成器真正用的上限是 `MaxSeqNumber`（**默认 63**），只有把它显式配成 `0` 才会回落成 `2^SeqBitLength - 1`（负数会被选项校验拒绝）。

序列从 `MinSeqNumber`（默认 5）起步，到 `MaxSeqNumber` 为止——默认配置下单节点每毫秒约 59 个 ID。要放开就显式配 `MaxSeqNumber`，取值上限是 `2^SeqBitLength - 1`。

序列耗尽后的行为看 `LoopedSequence`：

| 取值 | 行为 |
| --- | --- |
| `false`（默认） | 自旋等待下一个时间片，吞吐被限住但 ID 不重复 |
| `true` | 序列直接绕回 `MinSeqNumber`，**时间片不变** |

::: danger LoopedSequence 会发出重复 ID
回绕时时间戳、机器码都没变，序列号又回到起点——同一毫秒内前后两个 ID 完全相同。除非你清楚自己在做什么，否则保持默认 `false`。
:::

### 时钟回拨

| 算法 | `SnowflakeIdType` | 回拨时的行为 |
| --- | --- | --- |
| 雪花漂移（默认） | `SnowFlakeMethod` | `Thread.Sleep(5)` 后重试；回拨幅度超过 `MaxBackwardToleranceMs`（默认 10000）抛异常；重试次数超过 `TopOverCostCount`（默认 2000）抛异常 |
| 传统雪花 | `ClassicSnowFlakeMethod` | 直接抛异常，不等待 |

两种模式都不会静默发出可能重复的 ID，代价是回拨期间调用方会阻塞或收到异常。生产环境把 NTP 校时配成缓步调整（slew），别让系统时间跳变。

### 反解的适用范围

`ExtractTime` / `ExtractWorkerId` / `ExtractSequence` / `ExtractDataCenterId` 是接口的统一签名，但不是每个实现都有意义：

| 生成器 | ExtractTime | ExtractWorkerId | ExtractSequence | ExtractDataCenterId |
| --- | --- | --- | --- | --- |
| Snowflake（漂移） | 有效 | 有效 | 有效 | **恒返回 0** |
| Snowflake（传统） | 有效 | 有效 | 有效 | 有效 |
| SequentialGuid | 有效 | 恒 0 | 恒 0 | 恒 0 |
| NanoId | 对 `NextId()` 的 `long` 有效 | 恒 0 | 有效 | 恒 0 |

反解用的是**当前生成器实例的配置**去拆位。配置和生成时不一致，解出来就是错的时间，而且不会报错。

### SequentialGuid 的三种排序模式

生成的 Guid = 6 字节毫秒时间戳 + 10 字节加密安全随机数，区别只在时间戳放哪儿、字节序怎么排。

| `SequentialGuidType` | 时间戳位置 | 适用 |
| --- | --- | --- |
| `SequentialAsString` | 前 6 字节，小端系统上前 4 字节与随后 2 字节各自反转 | 按字符串比较排序的场景 |
| `SequentialAsBinary` | 前 6 字节，不反转 | 按二进制排序的数据库 |
| `SequentialAtEnd` | 后 6 字节 | SQL Server 聚集索引，**框架默认** |

::: warning 有序性只到毫秒
同一毫秒内生成的多个 Guid 之间没有单调计数器，先后顺序由随机部分决定。它保证的是「批量插入落在相邻页」，不是「严格递增」。

另外 `ExtractTime` 按当前 `DefaultSequentialGuidType` 拆字节——排序模式改过之后，历史 Guid 的时间解不出来。
:::

### Sqids 的短码不跨进程稳定

`SqidsEncoder` 构造时会用 `_options.GetHashCode()` 当种子洗牌字母表。`SqidsOptions` 没有重写 `GetHashCode`，拿到的是**对象引用哈希**——每个 encoder 实例、每次进程启动都不一样。

| 场景 | 是否可靠 |
| --- | --- |
| 同一个 encoder 实例内 `Encode` → `Decode` 往返 | 可靠 |
| 进程内经扩展方法 `ToSqid()` / `FromSqidToInt64()` 往返（走同一个静态实例） | 可靠 |
| 应用重启后解码重启前发出的短码 | **不可靠** |
| 两个不同的 encoder 实例互相解码 | **不可靠** |

::: danger 不要把 Sqids 短码落库或写进长期链接
短码只适合「本次会话内生成、立刻使用」的一次性展示。需要长期稳定的对外标识，用 NanoId 生成一个真实存在的列，别指望从数字算出来。
:::

另外两点：`Encode` 不接受负数（抛 `ArgumentException`）；`Decode` 遇到字母表以外的字符**静默返回空数组**，扩展方法则返回 `0`——解码结果要自己判空。

## 配置

四个配置节，各自独立：

| Options | 配置节 |
| --- | --- |
| `SnowflakeIdOptions` | `XiHan:DistributedIds:SnowflakeId` |
| `SequentialGuidOptions` | `XiHan:DistributedIds:SequentialGuid` |
| `NanoIdOptions` | `XiHan:DistributedIds:NanoId` |
| `SqidsOptions` | `XiHan:DistributedIds:Sqids` |

上线前必须想清楚的雪花键（其余键见[包文档](../packages/distributed-ids)）：

| 键 | 默认 | 上线后可改吗 |
| --- | --- | --- |
| `WorkerId` | 基线 1 | 可改，但每个实例必须互不相同 |
| `WorkerIdBitLength` | 6 | **不可改**，改了位布局就变了 |
| `SeqBitLength` | 基线 12 | **不可改**，同上 |
| `BaseTime` | `2026-01-01 UTC` | **不可改** |
| `TimestampType` | `Milliseconds` | **不可改**，41 位与 32 位布局不同 |
| `SnowflakeIdType` | `SnowFlakeMethod` | **不可改**，两种算法位段划分不同 |
| `MaxSeqNumber` | 63 | 可改，决定单时间片吞吐 |
| `MaxBackwardToleranceMs` | 10000 | 可改 |

```json
{
  "XiHan": {
    "DistributedIds": {
      "SnowflakeId": {
        "WorkerId": 1,
        "WorkerIdBitLength": 6,
        "SeqBitLength": 12,
        "MaxSeqNumber": 4095,
        "TimestampType": "Milliseconds"
      },
      "SequentialGuid": {
        "DefaultSequentialGuidType": "SequentialAtEnd"
      }
    }
  }
}
```

### WorkerId 必须逐实例分配

::: danger 这是最常见的重复 ID 来源
不配置 `WorkerId` 时，所有实例都拿基线值 `1`。两个副本在同一毫秒生成，序列号各自从 5 开始——**直接撞主键**。

容器/多副本部署必须给每个实例注入不同的值，例如环境变量：

```bash
XiHan__DistributedIds__SnowflakeId__WorkerId=3
```

有状态副本（StatefulSet 序号）、注册中心分配、或按主机名映射都可以，关键是同一时刻集群内不重号，且实例重建后不要立刻把号让给别人。
:::

::: warning 超过 64 个节点时别只靠配置绑定
`WorkerId` 的合法上限由**赋值那一刻**已生效的 `WorkerIdBitLength` 决定，而选项是「先落基线位长 6，再套配置」。所以在同一配置节里同时写大 `WorkerIdBitLength` 和大于 63 的 `WorkerId`，可能在绑定阶段就抛「工作机器唯一标识必须在 0-63 之间」。

需要更多节点时，用 `IdGeneratorFactory.CreateSnowflakeIdGenerator(options)` 自己构造选项对象（顺序自己控制）并 `Replace` 掉默认注册。
:::

### BaseTime 与位长为什么不能改

雪花 ID 里存的不是绝对时间，是相对 `BaseTime` 的偏移量；每一段占多少位由 `WorkerIdBitLength` / `SeqBitLength` / `TimestampType` 决定。

| 改了什么 | 后果 |
| --- | --- |
| `BaseTime` 往后调 | 偏移量整体变小，新 ID 落回历史 ID 已用过的数值区间 |
| `BaseTime` 往前调 | 新 ID 变大，暂时不撞，但所有历史 ID 的 `ExtractTime` 全部解错 |
| 任一位长 / `TimestampType` / `SnowflakeIdType` | 位段边界移动，新旧 ID 之间既不保证有序也不保证不撞，反解全错 |

这几个键属于「建库时定好，之后当成常量」。生成器构造时只会校验 `BaseTime` 不晚于当前系统时间，不会替你发现「和历史数据对不上」。

::: tip UseCustomEpoch 目前不起作用
`SnowflakeIdOptions.UseCustomEpoch` 这个属性存在，但生成器没有读取它——纪元起点始终取 `BaseTime`。配它没有效果。
:::

### 替换默认生成器

模块用 `AddSingleton` 的工厂委托注册（不是 `TryAdd`），要换实现在自己模块里 `Replace`：

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    context.Services.Replace(ServiceDescriptor.Singleton(
        IdGeneratorFactory.CreateSnowflakeIdGenerator_Classic(workerId: 3, dataCenterId: 1)));
}
```

自定义算法实现 `IDistributedIdGenerator<TKey>` 后同样注册即可，接口与内置实现之间没有耦合。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 多实例部署出现重复主键 | `WorkerId` 没有逐实例分配，全都是基线值 1 |
| 单节点吞吐上不去、插入偶发变慢 | `MaxSeqNumber` 默认 63，序列耗尽后自旋等下一毫秒；调大 `SeqBitLength` 不会自动放开这个上限 |
| 开了 `LoopedSequence` 之后出现重复 ID | 序列回绕但不换时间片，见上文 |
| 抛「时钟回拨太多」 | 回拨幅度超过 `MaxBackwardToleranceMs`；检查 NTP 是否在跳变式校时 |
| 传统雪花模式下回拨直接抛异常 | `ClassicSnowFlakeMethod` 不做等待重试，这是预期行为 |
| `ExtractDataCenterId` 恒为 0 | 漂移模式不反解数据中心，只有 `ClassicSnowFlakeMethod` 有意义 |
| `ExtractTime` 解出的时间不对 | 当前配置的 `BaseTime` / 位长 / `TimestampType` 与生成时不一致 |
| SequentialGuid 的 `ExtractTime` 解不出来 | `DefaultSequentialGuidType` 改过，字节位置对不上 |
| 同一毫秒的 Guid 顺序不严格递增 | 毫秒内由随机部分决定，设计如此 |
| NanoId 的 `NextId()` 跨实例撞了 | 那个 `long` 不含节点标识，只有 `NextIdString()` 是 NanoId |
| 应用重启后旧的 Sqids 短码解不回来 | 字母表种子随 encoder 实例变化 |
| 改了 `XiHan:DistributedIds:Sqids` 但 `ToSqid()` 没变化 | 扩展方法用内部静态编码器，走默认选项 |
| `Decode` 返回空数组 / `FromSqidTo*` 返回 0 | 短码含字母表以外的字符，静默失败不抛异常 |
| 注入 `IDistributedIdGenerator<long>` 报未注册 | 模块没有 `DependsOn(typeof(XiHanDistributedIdsModule))` |
| 注入 NanoId / Sqids 失败 | 它们不在 DI 里，只能自己构造 |

## 下一步

- [数据访问](./data)：`long` 主键的自动填充发生在哪一层
- [配置与选项](./configuration)：配置节绑定与环境变量覆盖规则
- [事件总线](../packages/eventbus)：消息标识用的就是 `IDistributedIdGenerator<Guid>`
- [DistributedIds 包](../packages/distributed-ids)：完整 API 清单、全部配置项、`GuidHelper` 工具箱
