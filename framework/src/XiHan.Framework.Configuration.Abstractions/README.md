# XiHan.Framework.Configuration.Abstractions

配置契约（Ring 2，RFC-0014）。应用仍然读 `Microsoft.Extensions.Configuration` 的 `IConfiguration`；本包只定义两个 Provider 契约与把它们接进 `IConfigurationBuilder` 的配置源。默认实现是 .NET 原生的文件与环境变量，不设默认包。

| 能力 | 契约 | 配置源 |
| --- | --- | --- |
| `configuration.remote@1` | `IRemoteConfigurationClient.FetchAsync(namespace, knownVersion)` → `Result<Maybe<ConfigurationDocument>>`（未变化为无值，可长轮询） | `AddXiHanRemoteConfiguration(client, options)`（客户端由调用方持有）或 `AddXiHanRemoteConfiguration(clientFactory, options)`（每个 Provider 按工厂新建一个，随它释放）：首次同步加载（`LoadTimeout`），之后按 `PollInterval` 轮询（计时用 `Clock`），变化时整体替换并触发重载；失败保留最后一份成功的数据并回调 `OnError` |
| `configuration.secrets@1` | `ISecretStore.GetAsync(names[])` 批量逐项 `SecretLookup`；`SecretValue.ToString()` 固定遮蔽 | `AddXiHanSecrets(store, options)`：`Mappings` 把配置键映射到密钥名，构建时一次批量读取，可按 `RefreshInterval` 重读（计时用 `Clock`） |

```csharp
builder.Configuration
    .AddXiHanRemoteConfiguration(() => NacosConfigurationClient.Create(nacosOptions), new RemoteConfigurationOptions("orders") { Prefix = "Orders", PollInterval = TimeSpan.FromSeconds(15) })
    .AddXiHanSecrets(vault, new SecretsConfigurationOptions(new Dictionary<string, SecretName>
    {
        ["ConnectionStrings:Orders"] = "orders/db-connection",
    }));
```

命名空间不存在 → `CONFIGURATION_NOTFOUND_001`；密钥不存在 → `CONFIGURATION_NOTFOUND_002`；不可达 → `CONFIGURATION_UNAVAILABLE_001` / `_002`；无权限 → `CONFIGURATION_DENIED_001`。首次加载的失败（返回失败、超时、客户端或密钥库抛出异常、密钥库返回的条数与请求不一致）都先回调 `OnError`，`Optional` 为假时随后抛出 `FrameworkException` 让启动失败（抛出的异常作为内部异常），为真时为空并照常轮询或重读；重读时条数不一致同样回调并保留旧值。`OnError` 自己抛出的异常被丢弃，不改变加载结果、不终止轮询或重读（ADR-0068）。

## 依赖方向

依赖 `XiHan.Framework.Primitives`、`XiHan.Framework.Localization.Abstractions` 与 `Microsoft.Extensions.Configuration.Abstractions`（重载令牌用其传递依赖 `Microsoft.Extensions.Primitives`）。被 `Configuration.Conformance` 与各 Provider 引用。

## 状态

P3：可用。
