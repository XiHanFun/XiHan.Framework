# ADR-0262 测试用的日志收集工厂收进 Testing

## 状态

已采纳（`Testing` 新增 `LogCapture` 与 `LogCaptureEntry`，`Preview`；测试项目里的副本全部删除；规范不变）。

## 日期

2026-11-24

## 背景

- 要断言组件写了哪条日志的测试，各自在项目里放一个把日志收进内存的 `ILoggerFactory`，名字都叫 `LogCapture`。到这一轮共 20 个测试项目各有一份，约 1100 行。
- 这 20 份分成 5 种变体，差别只在条目的形状与接入方式：
  - 条目是四元组（级别、类别、消息、异常），10 份，另有 2 份只差一句注释。
  - 条目是三元组（不带异常），4 份。
  - 条目是三元组，异常另放一张 `Exceptions` 表，2 份。
  - 实现的是 `ILoggerProvider` 而不是 `ILoggerFactory`，条目是三元组，2 份。
- 另有 3 份只实现 `ILogger` 的同类替身 `RecordingLogger`（1 个文件、2 个嵌套类型）。
- 这些副本都用无界的队列或列表收日志，都没有测试。
- `Testing` 已有同类的故障替身 `FaultyCache`、`ScriptedUnitOfWork`（ADR-0259）；应用测自己的组件时同样需要断言日志。

## 备选

1. **留在各测试项目**：否决。同一个东西 23 份，下一个要断言日志的测试项目还会再抄一份。
2. **换成 `Microsoft.Extensions.Diagnostics.Testing` 的 `FakeLogger` / `FakeLogCollector`**：否决。它是现成的，但要给二十来个测试项目各加一个包引用，应用用 `Testing` 时也得另装；它的记录带作用域、结构化状态与时间戳，这里的断言只用级别、类别、消息与异常四样。
3. **收进 `Testing`，公开一个类型**：采纳。
4. **条目用位置记录或元组**：否决。位置记录以后加成员会破坏解构（审计里对 `BlobSignedUrl` 等提过同一个问题），元组没法写文档；用带 `required` 成员的记录。
5. **不设上限**：否决。框架不放无界的内存集合，测试替身也一样：被测组件在循环里刷日志时，收集器不该把测试进程撑大。

## 决策

- `XiHan.Framework.Testing` 新增 `LogCapture`（`Preview`）：
  - 同时实现 `ILoggerFactory` 与 `ILoggerProvider`：登记为日志工厂单例，或经日志构建器的 `AddProvider` 接入，两种都行。
  - 全部级别都启用（`LogLevel.None` 除外），不支持作用域（`BeginScope` 返回空）。
  - `Entries` 是按写入顺序的快照，每次读取一份新副本；可以多线程同时写入与读取。
  - 最多留 `Capacity` 条（缺省 `DefaultCapacity` = 10000，构造时可给，必须为正），满了丢掉最早的。
  - `AddProvider` 抛出 `NotSupportedException`：日志只收在这里，不转发。`Dispose` 没有要释放的资源，释放之后写入的照样记下。
- 新增 `LogCaptureEntry`（`Preview`）：记录，成员 `Level`、`Category`、`Message`（格式化后的文本）、`Exception`（可空）。
- 测试项目里的 20 份 `LogCapture` 与 3 份 `RecordingLogger` 删除，调用点改用它；以 `(级别, 类别, 消息)` 元组作参数或返回值的辅助方法改成 `LogCaptureEntry`，读 `Exceptions` 表的改成在 `Entries` 里找带异常的条目。
- `Kernel.Tests` 与 `Protocols.CloudEvents.Tests` 原先不引用 `Testing`，为此加上项目引用。

## 影响

- 公开面：`Testing` 多两个 `Preview` 类型，包的依赖不变（`Microsoft.Extensions.Logging.Abstractions` 原先就经内核带入）。
- `Kernel.Tests` 从只引用 `Kernel` 变成还引用 `Testing`，间接带进 `Testing` 依赖的几个抽象包。只影响这个测试项目的构建闭包，不影响 `Kernel` 自身的依赖。
- 原先收集不设上限，现在超过 10000 条时丢最早的；仓库里没有哪个测试写得到这个量级。
- 新增 `LogCaptureTests` 10 条：条目内容与顺序、两种接入方式、快照、上限、参数校验、释放之后、并发写入与读取。
