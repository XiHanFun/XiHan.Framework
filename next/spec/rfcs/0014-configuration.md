# RFC-0014 远程配置与密钥

> 状态：已采纳 | 日期：2026-10-23 | 对应 ADR：— | 参考实现：`XiHan.Framework.Configuration.Abstractions` | 一致性：`XiHan.Framework.Configuration.Conformance`

## 摘要

配置子系统不另造配置模型：应用仍然读 `Microsoft.Extensions.Configuration` 的 `IConfiguration`。本 RFC 只定义两个 Provider 契约与把它们接进 `IConfigurationBuilder` 的适配器：远程配置客户端（Nacos、Apollo、Consul 等）与密钥库（Vault、Key Vault、KMS 等）。默认实现是 .NET 原生的文件与环境变量，不设默认包。

## 能力

| 能力 | 含义 | 契约 |
| --- | --- | --- |
| `configuration.remote@1` | 远程配置：按命名空间取整份文档，带版本，可轮询或长轮询 | `IRemoteConfigurationClient` |
| `configuration.secrets@1` | 密钥：按名字批量读取，只读 | `ISecretStore` |

## 远程配置

- 文档 `ConfigurationDocument(Version, Values)`：`Values` 的键已是 `IConfiguration` 的键形式（`A:B:C`），Provider 负责把自己的格式（JSON、YAML、properties）展平；`Version` 非空，每次变化都不同（ETag、修订号或内容哈希）。
- `FetchAsync(namespace, knownVersion)`：`knownVersion` 为空时总是返回文档；非空且远端未变化时返回无值。实现可以长轮询：阻塞到变化或自己的窗口到期，收到取消时返回无值而不是抛出。命名空间不存在 → `CONFIGURATION_NOTFOUND_001`；不可达 → `CONFIGURATION_UNAVAILABLE_001`。
- 适配器 `AddXiHanRemoteConfiguration(client, options)`：构建配置时同步加载一次（`IConfigurationProvider.Load` 是同步契约，受 `LoadTimeout` 约束）；失败时 `Optional` 为假则抛出让启动失败，为真则为空并回调 `OnError`。之后按 `PollInterval`（默认 30 秒，`null` 关闭）带已知版本轮询，拿到新版本就替换整份数据并触发重载令牌；轮询失败保留最后一份成功的数据并回调 `OnError`。`Prefix` 把文档挂到某个节下。

## 密钥

- 名字 `SecretName`：`^[A-Za-z0-9][A-Za-z0-9._/-]{0,255}$`，不含 `..`、`//`，不以 `/` 结尾。
- `GetAsync(names[])` 批量逐项返回 `SecretLookup(Name, Result<SecretValue>)`，顺序与入参一致，重复项各自作答，空集合返回空；不存在 → `CONFIGURATION_NOTFOUND_002`，无权限 → `CONFIGURATION_DENIED_001`，不可达 → `CONFIGURATION_UNAVAILABLE_002`。
- `SecretValue.ToString()` 固定返回遮蔽文本，防止进入日志。
- 适配器 `AddXiHanSecrets(store, options)`：`Mappings` 把配置键映射到密钥名，构建时一次批量读取；缺失或失败时 `Optional` 为假则抛出，为真则跳过并回调 `OnError`；`RefreshInterval` 非空时周期性重读，失败的项保留旧值。

## 一致性

Provider 继承 `RemoteConfigurationClientConformance` 或 `SecretStoreConformance`，实现夹具（客户端或密钥库加写入钩子）并通过全部用例。

## 错误码

见 `conventions/error-codes.md` 中 `CONFIGURATION_*`。
