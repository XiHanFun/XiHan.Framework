# XiHan.Framework.Configuration.Conformance

配置一致性套件（RFC-0014）。Provider 的测试项目继承抽象类并实现夹具工厂，全部用例随之执行：

- `RemoteConfigurationClientConformance`：实现 `CreateFixtureAsync()` 返回 `RemoteConfigurationFixture`（被测 `IRemoteConfigurationClient` 与向远端发布文档的 `PublishAsync`）；用例覆盖取整份文档与版本、已知版本未变化返回无值（长轮询实现须在 `UnchangedWait` 内返回）、再次发布后版本变化、未知命名空间 NOTFOUND。
- `SecretStoreConformance`：实现 `CreateFixtureAsync()` 返回 `SecretStoreFixture`（被测 `ISecretStore` 与写入密钥的 `PutAsync`）；用例覆盖批量逐项有序返回、重复项、缺失 NOTFOUND、空集合、遮蔽的文本表示、覆盖后读新值。

## 依赖方向

依赖 `XiHan.Framework.Configuration.Abstractions` 与 xunit 的断言与扩展核心包。

## 状态

P3：可用。
