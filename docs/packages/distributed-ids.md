# XiHan.Framework.DistributedIds

> 分布式唯一标识生成：自研 Snowflake / NanoId / SequentialGuid 生成器 + Sqids 短码编码，统一在 `IDistributedIdGenerator<TKey>` 接口下，零第三方依赖。

- **NuGet**：`XiHan.Framework.DistributedIds`
- **模块类**：`XiHanDistributedIdsModule`
- **所在层**：基础设施层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（[XiHan.Framework.Core](./core)）。所有算法均为框架自研，不引入任何第三方 ID 库。

## 概述

XiHan.Framework.DistributedIds 用于在分布式环境下生成不重复的标识。它自研实现了三种 `IDistributedIdGenerator<TKey>` 生成器——Snowflake（雪花漂移/传统雪花，`long`）、NanoId（随机字符串，但对外仍以 `long` 键接口暴露）、SequentialGuid（时间有序 `Guid`）——并额外提供 Sqids 短码编码方案。所有算法都不依赖外部 NuGet 包，便于版本控制与定制。

设计上把「生成器」与「编码器」区分开：**生成器**实现统一接口 `IDistributedIdGenerator<TKey>`（`TKey` 为 `long` 或 `Guid`），可被 DI 注入；**Sqids** 则是把一个/多个非负整数双向编解码为短字符串的 `SqidsEncoder<T>`，**不实现** `IDistributedIdGenerator<TKey>`。此外还有一个更基础的 **`GuidHelper`** 静态工具类，提供通用 Guid 生成/校验/格式转换能力，同样不实现生成器接口，与 `SequentialGuidGenerator` 相互独立。

## 何时使用

- 需要跨库、跨服务、无中心协调地生成不重复主键或业务编号。
- 高并发下想要有序、可从中提取时间戳/机器信息的 `long` 型 ID（Snowflake）。
- 需要数据库索引友好的时间有序 `Guid`（SequentialGuid）——框架实体主键的默认选择。
- 需要 URL 友好、加密安全随机的短字符串 ID（NanoId）。
- 需要把自增整数编码成不可预测的短码对外展示、隐藏真实顺序/数量（Sqids）。

## 安装与启用

```bash
dotnet add package XiHan.Framework.DistributedIds
```

```csharp
[DependsOn(typeof(XiHanDistributedIdsModule))]
public class MyModule : XiHanModule { }
```

模块在 `ConfigureServices` 中调用 `AddXiHanDistributedIds(config)`，它做两件事：

1. 绑定四个 Options 到配置节（`SnowflakeIdOptions` / `SequentialGuidOptions` / `NanoIdOptions` / `SqidsOptions`）。
2. 以单例注册两个默认生成器：
   - `IDistributedIdGenerator<Guid>` → `SequentialGuidGenerator`（默认 `Default()`，即 `SequentialAtEnd` 末尾形式）
   - `IDistributedIdGenerator<long>` → `SnowflakeIdGenerator`（默认 `HighWorkload`，`SeqBitLength=12`、`WorkerIdBitLength=6`）

> 注意：默认只注册了 `Guid` 与 `long` 两个生成器。NanoId 与 Sqids 不在默认 DI 注册中，需要时通过 `IdGeneratorFactory` / `SqidsEncoder` / 扩展方法自行构造。

## 核心能力

- **多算法生成器**：Snowflake（`long`）、NanoId（字符串，键接口为 `long`）、SequentialGuid（`Guid`），统一 `IDistributedIdGenerator<TKey>` 接口。
- **Sqids 短码编码**：`SqidsEncoder<T>`（`T : INumber<T>`）与 `SqidsExtensions` 扩展方法，可逆、顺序混淆、内置屏蔽词过滤。
- **零第三方依赖**：全部算法自研，便于审计与定制。
- **信息可提取**：接口提供 `ExtractTime` / `ExtractWorkerId` / `ExtractSequence` / `ExtractDataCenterId`（主要对 Snowflake 有意义）。
- **工厂预置场景**：`IdGeneratorFactory` 提供低/中/高并发、短 ID、前缀、经典雪花、多种 NanoId 字符集、多种 SequentialGuid 排序等大量预置构造方法。
- **同步/异步与批量**：接口提供 `NextId()` / `NextIdString()` / `NextIds(count)` 及对应 `...Async` 变体。
- **数据库友好**：`SequentialGuidGenerator` 支持字符串/二进制/末尾三种排序模式以适配不同数据库索引。
- **Guid 工具箱**：`GuidHelper`（静态）提供标准/加密安全/时间有序/确定性（SHA1、MD5）Guid 生成，以及有效性校验、N/D/B/P/X 格式转换、字节数组/Base64 互转、版本位与变体位解析等，独立于生成器接口体系，不参与 DI 注册。

## 主要 API / 类型

### 统一生成器接口

`IDistributedIdGenerator<TKey>` 关键成员（`TKey` 为 `long` 或 `Guid`）：

| 成员 | 说明 |
| --- | --- |
| `TKey NextId()` | 生成下一个唯一标识 |
| `string NextIdString()` | 生成下一个唯一标识（字符串形式，Snowflake 会带上前缀等） |
| `TKey[] NextIds(int count)` | 批量生成 |
| `string[] NextIdStrings(int count)` | 批量生成（字符串形式） |
| `Task<TKey> NextIdAsync()` 等 | 上述方法的异步变体（`NextIdStringAsync` / `NextIdsAsync` / `NextIdStringsAsync`） |
| `DateTime ExtractTime(TKey id)` | 从 ID 提取时间戳 |
| `int ExtractWorkerId(TKey id)` | 从 ID 提取工作机器标识 |
| `int ExtractSequence(TKey id)` | 从 ID 提取序列号 |
| `int ExtractDataCenterId(TKey id)` | 从 ID 提取数据中心标识 |
| `string GetGeneratorType()` | 生成器类型名 |
| `Dictionary<string, object> GetStats()` | 生成器运行状态 |

### 工厂 `IdGeneratorFactory`（静态）

| 方法 | 返回 | 说明 |
| --- | --- | --- |
| `CreateSnowflakeIdGenerator(SnowflakeIdOptions)` | `IDistributedIdGenerator<long>` | 自定义选项 |
| `CreateSnowflakeIdGenerator_LowWorkload(ushort workerId = 1)` | `IDistributedIdGenerator<long>` | 低并发（`SeqBitLength=6`） |
| `CreateSnowflakeIdGenerator_MediumWorkload(ushort workerId = 1)` | `IDistributedIdGenerator<long>` | 中并发（`SeqBitLength=10`） |
| `CreateSnowflakeIdGenerator_HighWorkload(ushort workerId = 1)` | `IDistributedIdGenerator<long>` | 高并发（`SeqBitLength=12`，默认注册用它） |
| `CreateSnowflakeIdGenerator_ShortId(ushort workerId = 1)` | `IDistributedIdGenerator<long>` | 短 ID（`IdLength=10`） |
| `CreateSnowflakeIdGenerator_PrefixedId(string prefix, ushort workerId = 1)` | `IDistributedIdGenerator<long>` | 带前缀 |
| `CreateSnowflakeIdGenerator_Classic(ushort workerId = 1, byte dataCenterId = 1)` | `IDistributedIdGenerator<long>` | 传统 Twitter Snowflake 兼容 |
| `CreateNanoIdGenerator(NanoIdOptions)` | `IDistributedIdGenerator<long>` | 自定义字符集/长度 |
| `CreateNanoIdGenerator_Numeric/_Lowercase/_Uppercase/_UrlSafe/_Safe/_Hex/_Custom(...)` | `IDistributedIdGenerator<long>` | 各预置字符集 |
| `CreateSequentialGuidGenerator(SequentialGuidOptions)` | `IDistributedIdGenerator<Guid>` | 自定义排序模式 |
| `CreateSequentialGuidGenerator_Default/_AsString/_AsBinary/_AtEnd()` | `IDistributedIdGenerator<Guid>` | 各排序模式，`_Default` 即 `_AtEnd` |

> NanoId 生成器虽然产出随机字符串，但接口签名仍是 `IDistributedIdGenerator<long>`；取字符串结果请用 `NextIdString()`，`NextId()` 返回的是内部数值。

### Sqids 编码（非生成器）

| 类型 | 说明 |
| --- | --- |
| `SqidsEncoder<T> where T : INumber<T>` | 泛型编码器。`string Encode(params T[] numbers)` 编码、`T[] Decode(string id)` 解码。构造可传 `SqidsOptions` |
| `SqidsEncoder`（非泛型，`: SqidsEncoder<int>`） | 面向 `int` 的便捷子类 |
| `SqidsExtensions`（静态扩展） | `int/uint/long/ulong` 的 `ToSqid()`，以及 `string.FromSqidToInt32/…Int32Array/…Int64/…Int64Array/…UInt32/…UInt64()` |

### Guid 通用工具 `GuidHelper`（静态，非生成器）

`XiHan.Framework.DistributedIds.Guids.GuidHelper` 是独立于 `IDistributedIdGenerator<TKey>` 体系之外的 Guid 工具类，覆盖生成、校验、格式转换与位域解析：

| 分类 | 方法 | 说明 |
| --- | --- | --- |
| 生成 | `NewGuid()` | 标准随机 Guid（等价 `Guid.NewGuid()`） |
| 生成 | `NewCryptoGuid()` | 使用 `RandomNumberGenerator` 生成的加密安全随机 Guid，版本位设为 4 |
| 生成 | `NewTimeBasedGuid()` | 基于 `DateTime.UtcNow.Ticks` + 随机字节拼装的时间有序 Guid，版本位设为 1 |
| 生成 | `NewDeterministicGuid(string input, Guid? namespaceGuid = null)` | SHA1 命名空间确定性 Guid（版本 5），相同输入必得相同输出 |
| 生成 | `NewDeterministicGuidMd5(string input, Guid? namespaceGuid = null)` | MD5 命名空间确定性 Guid（版本 3） |
| 生成 | `GenerateMultiple(int count, bool useCrypto = false)` | 批量生成 `List<Guid>` |
| 校验 | `IsValidGuid` / `IsValidStandardGuid`（带连字符）/ `IsValidGuidNoDash`（无连字符） | 格式校验 |
| 解析 | `TryParse` / `Parse` | 包装 `Guid.TryParse` / `Guid.Parse` |
| 格式化 | `ToString(Guid, format = "D")` / `ToUpperString` / `ToLowerString` | 支持 N/D/B/P/X 格式 |
| 格式转换 | `ToStringNoDash` / `ToStandardFormat` / `ToNoDashFormat` | 标准格式与无连字符格式互转 |
| 字节/Base64 | `ToByteArray` / `FromByteArray` / `ToBase64String` / `FromBase64String` | 与字节数组、Base64 字符串互转 |
| 位域解析 | `GetVersion(Guid)` / `GetVariant(Guid)` | 提取 RFC 4122 版本号（1-5）与变体位 |
| 判空/比较 | `IsEmpty` / `IsNotEmpty` / `AreEqual` / `GetHashCode(Guid)` | 基础判断 |
| 诊断 | `GetFormatExamples(Guid? guid = null)` | 返回 N/D/B/P/X/Base64 六种格式示例的字典 |

> `GuidHelper` 与 `SequentialGuidGenerator` 相互独立：前者是通用 Guid 工具箱（静态方法，不实现 `IDistributedIdGenerator<TKey>`，不参与 DI 注册），后者是专门产出「末尾/字符串/二进制」三种排序模式有序 Guid 的生成器实例。`GuidHelper.NewTimeBasedGuid()` 虽然也时间有序，但字节布局与算法完全不同于 `SequentialGuidGenerator`，两者生成的 Guid 不可互换、不可用同一套 `ExtractTime` 逻辑解析。

## 配置

四个 Options 各自绑定独立配置节，前缀均为 `XiHan:DistributedIds:*`。

| Options | 配置节（`SectionName`） |
| --- | --- |
| `SnowflakeIdOptions` | `XiHan:DistributedIds:SnowflakeId` |
| `SequentialGuidOptions` | `XiHan:DistributedIds:SequentialGuid` |
| `NanoIdOptions` | `XiHan:DistributedIds:NanoId` |
| `SqidsOptions` | `XiHan:DistributedIds:Sqids` |

### `SnowflakeIdOptions` 关键字段

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `WorkerId` | `ushort` | `0` | 机器码，需全局唯一，上限受 `WorkerIdBitLength` 约束 |
| `WorkerIdBitLength` | `byte` | `6` | 机器码位长（1–15），与 `SeqBitLength` 之和 ≤ 22 |
| `SeqBitLength` | `byte` | `6` | 序列号位长（3–21），与 `WorkerIdBitLength` 之和 ≤ 22 |
| `SnowflakeIdType` | `SnowflakeIdTypes` | `SnowFlakeMethod` | `SnowFlakeMethod`(=1 雪花漂移) / `ClassicSnowFlakeMethod`(=2 传统雪花) |
| `TimestampType` | `TimestampTypes` | `Milliseconds` | `Seconds` / `Milliseconds` |
| `DataCenterId` / `DataCenterIdBitLength` | `byte` | `0` / `5` | 传统雪花的数据中心码及位长（1–10） |
| `BaseTime` | `DateTime` | `2026-01-01 UTC` | 纪元起点，不能超过当前系统时间 |
| `UseCustomEpoch` | `bool` | `true` | 是否用 `BaseTime` 作为时间起点 |
| `MaxSeqNumber` / `MinSeqNumber` | `int` | `63` / `5` | 最大/最小序列号 |
| `TopOverCostCount` | `int` | `2000` | 最大漂移次数（0–10000） |
| `LoopedSequence` | `bool` | `false` | 序列号到顶时循环回最小值而非等待下一时间片 |
| `MaxBackwardToleranceMs` | `int` | `10000` | 时钟回拨最大容忍毫秒（0–60000） |
| `IdLength` | `byte` | `0` | ID 输出长度（0=默认，或 10–20），会截断/填充 |
| `IdPrefix` | `string` | 空 | ID 字符串前缀 |
| `GeneratorId` | `string` | 随机 GUID(N) | 生成器实例标识 |

> `SnowflakeIdOptions` 另提供静态工厂 `LowWorkload/MediumWorkload/HighWorkload/ShortId/PrefixedId/Classic` 及 `FromJson/ToJson`。多数属性带范围校验，越界会抛 `ArgumentException`。

### `NanoIdOptions` 关键字段

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `Size` | `int` | `21` | ID 长度（1–128） |
| `Alphabet` | `string` | 62 字符 URL 安全集 | 字符集，须 ≥2 且字符唯一 |
| `StartTime` | `DateTime` | `2020-01-01 UTC` | 时间提取起点 |
| `TimestampType` | `TimestampTypes` | `Milliseconds` | 时间戳粒度 |

内置字符集常量：`DefaultAlphabet` / `NumbersAlphabet` / `LowercaseAlphabet` / `UppercaseAlphabet` / `UrlSafeAlphabet` / `SafeAlphabet`（无相似字符）/ `HexAlphabet`；对应静态工厂 `OnlyNumbers/OnlyLowercase/OnlyUppercase/UrlSafe/Safe/Hex`。

### `SequentialGuidOptions`

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `DefaultSequentialGuidType` | `SequentialGuidType?` | `null`（回退 `SequentialAtEnd`） | 排序模式 |

`SequentialGuidType` 取值：`SequentialAsString`（字符串比较排序）、`SequentialAsBinary`（二进制排序）、`SequentialAtEnd`（末尾形式，推荐用于 SQL Server 聚集索引，也是框架默认）。静态工厂：`AsString/AsBinary/AtEnd/Default`。

### `SqidsOptions`

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `Alphabet` | `string` | 62 字符集 | 编码字母表，须 ≥3 且字符唯一 |
| `MinLength` | `int` | `5` | 生成短码最小长度 |
| `BlockList` | `HashSet<string>` | 内置屏蔽词表 | 命中则重排字母表重试，避免生成不雅词 |

### 示例 appsettings.json

```json
{
  "XiHan": {
    "DistributedIds": {
      "SnowflakeId": {
        "WorkerId": 1,
        "SeqBitLength": 12,
        "WorkerIdBitLength": 6,
        "TimestampType": "Milliseconds"
      },
      "SequentialGuid": {
        "DefaultSequentialGuidType": "SequentialAtEnd"
      },
      "NanoId": {
        "Size": 21
      },
      "Sqids": {
        "MinLength": 8
      }
    }
  }
}
```

## 使用示例

### 依赖注入（默认注册的生成器）

```csharp
public class OrderService(
    IDistributedIdGenerator<long> longGenerator,
    IDistributedIdGenerator<Guid> guidGenerator)
{
    public long NewOrderNo() => longGenerator.NextId();   // Snowflake long
    public Guid NewRowId() => guidGenerator.NextId();     // SequentialGuid（末尾形式）
    public async Task<long[]> BatchAsync() => await longGenerator.NextIdsAsync(100);
}
```

### 工厂按场景构造（NanoId / 前缀 Snowflake）

```csharp
// 高并发 Snowflake
var sf = IdGeneratorFactory.CreateSnowflakeIdGenerator_HighWorkload(workerId: 1);
long id = sf.NextId();

// URL 安全的 NanoId（21 位），取字符串结果
var nano = IdGeneratorFactory.CreateNanoIdGenerator_UrlSafe(21);
string shortId = nano.NextIdString();
```

### Sqids 短码编解码（编码方案，非生成器）

```csharp
// 扩展方法（最简）
string code = 1234L.ToSqid();          // 例如 "xY7pQ"
long back = code.FromSqidToInt64();    // 1234

// 编码器（可传自定义 SqidsOptions，支持多值）
var encoder = new SqidsEncoder<long>(new SqidsOptions { MinLength = 8 });
string packed = encoder.Encode(10, 20, 30);
long[] nums = encoder.Decode(packed);  // [10, 20, 30]
```

> Sqids 不能编码负数（会抛 `ArgumentException`），且相同选项下编码是确定的（字母表按选项 hash 洗牌）。

### Guid 通用工具（`GuidHelper`，静态，非生成器）

```csharp
Guid id = GuidHelper.NewCryptoGuid();                 // 加密安全随机 Guid
Guid ordered = GuidHelper.NewTimeBasedGuid();          // 时间有序（与 SequentialGuidGenerator 算法不同）
Guid stable = GuidHelper.NewDeterministicGuid("order-1024"); // 相同输入恒定输出

bool ok = GuidHelper.IsValidGuid("not-a-guid");        // false
string noDash = GuidHelper.ToStringNoDash(id);         // 32 位无连字符
int version = GuidHelper.GetVersion(id);               // 4
```

## 扩展点 / 自定义

- **替换默认生成器**：模块以 `AddSingleton` 直接注册具体实例（非 `TryAdd`）。若要换成自定义生成器，在你的模块 `ConfigureServices` 里用 `services.Replace(...)` 覆盖对应的 `IDistributedIdGenerator<Guid>` / `IDistributedIdGenerator<long>` 注册。
- **自定义算法**：实现 `IDistributedIdGenerator<TKey>` 后注册即可，接口本身与内置实现解耦。
- **自定义字符集/字母表**：NanoId 用 `NanoIdOptions.Alphabet`、Sqids 用 `SqidsOptions.Alphabet`；均要求字符唯一。

## 注意事项与最佳实践

- **Snowflake `WorkerId` 必须全局唯一**：同一 `WorkerId` 在多实例部署下会产生重复风险；容器/多副本环境需为每个实例分配不同 `WorkerId`（如通过配置或注册中心）。
- **`BaseTime` 不能晚于当前时间**：设置越界会抛异常。默认纪元为 `2026-01-01 UTC`。
- **位长约束**：`WorkerIdBitLength + SeqBitLength ≤ 22`，越界抛 `ArgumentException`。
- **NanoId 键类型是 `long`**：需要字符串短码时用 `NextIdString()`，别误用 `NextId()`。
- **`ExtractXxx` 主要面向 Snowflake**：对 NanoId/SequentialGuid 的语义有限，按需使用。
- **Sqids ≠ 加密**：它只是混淆顺序、生成短码，不提供机密性；也不能编码负数。
- **默认只注册 `Guid`/`long`**：NanoId、Sqids 需自行构造，不能直接注入。

## 依赖模块

- [XiHan.Framework.Core](./core) — 唯一的框架内部依赖，提供模块化与依赖注入基座。
- 第三方核心依赖：无（所有算法自研）。

## 相关模块

- [XiHan.Framework.Domain](./domain) — 实体主键常用分布式 ID 生成。
- [XiHan.Framework.Data](./data) — 数据层可结合时间有序 `Guid` / Snowflake 优化主键与索引。
