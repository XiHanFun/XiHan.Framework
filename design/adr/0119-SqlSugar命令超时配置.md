# ADR-0119 SqlSugar 命令超时配置

## 状态

已采纳（`spec/rfcs/0005-persistence.md` 随之修订）。

## 日期

2026-11-04

本 ADR 记录第三十批回灌（`aj-batch-backlog.md`，N5 矩阵发现）的第二项。用户定的矩阵清单里有「命令超时配置」，在框架实现。

## 背景

`Ado.CommandTimeOut` 在特性建的客户端上，配置回调拿到的只是 `ConnectionConfig`，碰不到它，`ConnMoreSettings` 里也没有这一项，所以 BasicApp 接不了。

## 决策

1. 键 `XiHan:Persistence:SqlSugar:CommandTimeoutSeconds`。缺省 300，与 SqlSugar 自己的缺省值相同（`AdoProvider` 构造里设的，测试锁住了），不配时行为不变。
2. 设到特性建的客户端的每个连接上。`Ado` 每个连接一个：默认连接、命名连接与分片连接都要设。多连接时要把各连接的上下文都建出来，所以缺省值时跳过这一步，不配的应用没有变化。
3. 不是正整数即启动失败。0 与负值不当作「不覆盖」：写错了悄悄按没配处理，应用会以为超时已经生效。0 也不能当作无限等待，ADO.NET 虽然是这个语义，但整个应用都没有超时不该是一个配置项的结果。
4. 应用自己注册 `ISqlSugarClient`（方式二）时这个键碰不到它，配了与缺省不同的值即启动失败，与 `Connections` 同理。配成 300 查不出来，但那和不配没有区别。

## 备选

1. **给配置回调加一个参数，让应用自己设**：回调拿到的是配置，客户端还没建，要改回调签名，每个应用都要写同样的代码。否决。
2. **`SqlSugarClient(configs, configAction)` 的回调里设**：它只在建客户端时对根连接跑一次，命名连接与分片连接照样没设上。否决。
3. **同时设到运行时数据源**：运行时数据源连的是应用记录里的外部库，由应用管理，不是这个配置节的范围。否决。

## 影响

- 不配的应用没有变化。
- 公开 API 新增 `SqlSugarPersistenceOptions.CommandTimeoutSeconds`、`SqlSugarPersistenceOptions.StandardCommandTimeoutSeconds`。
- 仓储判断冲突时另开的连接、库结构读取用 `CopyNew` 建，保持缺省的 300 秒：语句很短，超时只会比配置的长，不会误杀。

### 已知限制

- 只管 SqlSugar。EF Core Provider 的命令超时由应用在 `DbContext` 上配。
