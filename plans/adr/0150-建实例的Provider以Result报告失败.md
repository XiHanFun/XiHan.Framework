# ADR-0150 建实例的 Provider 以 Result 报告失败

## 状态

已采纳（规范 `rfcs/0012-objectstorage.md`、`rfcs/0005-persistence.md` 修订）。

## 日期

2026-11-11

## 背景

`IBlobStoreProvider.CreateAsync`（`Preview`，计划 GA 时与对象存储一起升 `Stable`）用异常表达可恢复的失败：

- 设置不合法抛 `ArgumentException`。
- 后端建不出来抛 `IOException` / `UnauthorizedAccessException`。
- 探测发现不能用抛携带错误码的 `FrameworkException`。

注册表再逐类捕获，翻译成结果。同一接口的 `ProbeAsync` 返回的却是 `Result`。持久化的 `IDataSourceProvider.CreateAsync` 同型：连接串不合法抛 `ArgumentException`，注册表翻译为 `PERSISTENCE_VALIDATION_006`。

这违背框架约定「可恢复的失败返回 `Result` / `Error`」。Provider 的作者也只能从注册表的 catch 块里倒推契约，漏抛一种异常就成了 500。

## 决策

- **对象存储**：`IBlobStoreProvider.CreateAsync` 返回 `ValueTask<Result<IBlobStore>>`。
  - 设置不合法 → `OBJECTSTORAGE_VALIDATION_003`。
  - 后端此刻建不出来 → `OBJECTSTORAGE_UNAVAILABLE_001`，`reason` 只写异常类型，完整异常由 Provider 记日志。
  - 探测发现不能用 → 探测给出的错误码。
  - 失败时 Provider 自己释放已建的资源；只有参数为 `null` 这类编程错误抛出。
- **标准错误的工厂**：新增 `BlobStoreFailures.DefinitionInvalid(definition, reason)` 与 `Unavailable(exception)`，Provider 与注册表共用，错误码和参数一致。
- **注册表**：去掉逐类捕获，Provider 的失败原样交给调用方，记一条带错误码的 Warning，不登记实例。
  - 文件系统 Provider 在初始化目录失败时自己记下完整异常。
  - S3 Provider 不再把探测结果转成异常再抛。
- **持久化同步改形**：`IDataSourceProvider.CreateAsync` 返回 `ValueTask<Result<IDataSource>>`。
  - 新增 `DataSourceFailures.Invalid(definition, reason)`，原因在工厂里统一经 `DataSourceDefinition.Redact` 脱敏，Provider 不会忘。
  - SqlSugar Provider 与注册表随之修改。
- **一致性套件**：用例 2 改为核对未知设置键以 `OBJECTSTORAGE_VALIDATION_003` 结果拒绝，不抛出。

## 影响

- 自己实现 `IBlobStoreProvider` 的存储包：
  - 返回类型改为 `Result<IBlobStore>`。
  - 原来抛出的 `ArgumentException` / IO 异常 / `FrameworkException` 改用 `BlobStoreFailures` 或探测的错误返回。
- 自己实现 `IDataSourceProvider` 的持久化 Provider：返回类型改为 `Result<IDataSource>`，拒绝定义时用 `DataSourceFailures.Invalid`。
- 经注册表（`BlobStores.GetOrAddAsync`、`DataSourceRegistry`）使用的应用不受影响，错误码不变。
- 两个接口都是 `Preview`，GA 前修改，不需要登记豁免（ADR-0131）。
