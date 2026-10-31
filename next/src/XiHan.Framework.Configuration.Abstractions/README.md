# XiHan.Framework.Configuration.Abstractions

配置契约（Ring 2，RFC-0014）。应用仍然读 `Microsoft.Extensions.Configuration` 的 `IConfiguration`；本包只定义两个 Provider 契约与把它们接进 `IConfigurationBuilder` 的配置源。默认实现是 .NET 原生的文件与环境变量，不设默认包。

| 能力 | 契约 | 配置源 |
| --- | --- | --- |
| `configuration.remote@1` | `IRemoteConfigurationClient.FetchAsync(namespace, knownVersion)` → `Result<Maybe<ConfigurationDocument>>`（未变化为无值，可长轮询） | `AddXiHanRemoteConfiguration(client, options)`：首次同步加载（`LoadTimeout`），之后按 `PollInterval` 轮询（计时用 `Clock`），变化时整体替换并触发重载；失败保留最后一份成功的数据并回调 `OnError` |
| `configuration.secrets@1` | `ISecretStore.GetAsync(names[])` 批量逐项 `SecretLookup`；`SecretValue.ToString()` 固定遮蔽 | `AddXiHanSecrets(store, options)`：`Mappings` 把配置键映射到密钥名，构建时一次批量读取，可按 `RefreshInterval` 重读（计时用 `Clock`） |

```csharp
builder.Configuration
    .AddXiHanRemoteConfiguration(nacos, new RemoteConfigurationOptions("orders") { Prefix = "Orders", PollInterval = TimeSpan.FromSeconds(15) })
    .AddXiHanSecrets(vault, new SecretsConfigurationOptions(new Dictionary<string, SecretName>
    {
        ["ConnectionStrings:Orders"] = "orders/db-connection",
    }));
```

命名空间不存在 → `CONFIGURATION_NOTFOUND_001`；密钥不存在 → `CONFIGURATION_NOTFOUND_002`；不可达 → `CONFIGURATION_UNAVAILABLE_001` / `_002`；无权限 → `CONFIGURATION_DENIED_001`。首次加载失败且 `Optional` 为假时抛出 `FrameworkException` 让启动失败。

## 依赖方向

依赖 `XiHan.Framework.Primitives`、`XiHan.Framework.Localization.Abstractions` 与 `Microsoft.Extensions.Configuration.Abstractions`（重载令牌用其传递依赖 `Microsoft.Extensions.Primitives`）。被 `Configuration.Conformance` 与各 Provider 引用。

## 状态

P3：可用。
