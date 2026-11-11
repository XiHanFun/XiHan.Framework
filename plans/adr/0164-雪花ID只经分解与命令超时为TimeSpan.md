# ADR-0164 雪花 ID 只经分解得到，命令超时改为 TimeSpan

## 状态

已采纳（RFC-0005）。

## 日期

2026-11-11

## 背景

API 审查第三节「持久化」一组：

- **`SnowflakeId` 能构造出自相矛盾的实例**：它是位置记录 `SnowflakeId(Value, Timestamp, WorkerId, Sequence)`，四个成员各自给出。`new SnowflakeId(42, now, 7, 0)` 照样编译，值与分量对不上，读到它的代码信哪一个都可能错。
- **命令超时的类型与同类不一**：`SqlSugarPersistenceOptions.CommandTimeoutSeconds` 是 `int`，同一个选项类型里的 `HealthProbeInterval` 等时长都是 `TimeSpan`。

## 决策

1. **`SnowflakeId` 只经分解得到**：
   - 去掉位置参数与公开构造，四个成员只读，由私有构造填写。
   - 唯一的入口是 `SnowflakeId.Parse(value, epoch)`：从 64 位值分解出时刻、节点与序号，值与分量总是一致。
   - 组合仍用 `Compose(timestamp, workerId, sequence)` 得到值，再按需 `Parse`。
2. **命令超时是 `TimeSpan`**：
   - `CommandTimeoutSeconds` 改为 `TimeSpan CommandTimeout`，至少 1 秒，按整秒向上取整交给 ADO.NET。
   - 缺省常量改为 `StandardCommandTimeout`（300 秒）。
   - 配置键仍是 `XiHan:Persistence:SqlSugar:CommandTimeoutSeconds`（秒），与其他 `…Seconds` 键的写法一致。

## 影响

- 源码迁移：
  - `new SnowflakeId(value, …)` → `SnowflakeId.Parse(value, epoch)`；按位置解构的代码改为读属性。
  - `new SqlSugarPersistenceOptions { CommandTimeoutSeconds = 60 }` → `{ CommandTimeout = TimeSpan.FromSeconds(60) }`。
  - `StandardCommandTimeoutSeconds` → `StandardCommandTimeout`。
- 配置文件不用改。
- 两个类型都是 `Preview`；GA 前修改，不需要登记豁免（ADR-0131）。
