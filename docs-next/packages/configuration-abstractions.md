# XiHan.Framework.Configuration.Abstractions

配置子系统的抽象包：定义远程配置客户端 `IRemoteConfigurationClient` 与密钥库 `ISecretStore` 两个 Provider 契约，以及把它们接进 `IConfigurationBuilder` 的配置源。应用仍然读 `IConfiguration`，需要从配置中心或密钥库取值时引用本包。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Configuration.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 定义 `configuration.remote@1` 与 `configuration.secrets@1`，本包不提供实现 |
| 特性入口 | — |
| 配置节 | —（Provider 的配置节在各自包里，如 `XiHan:Configuration:Nacos`） |
| 依赖 | `XiHan.Framework.Primitives`、`XiHan.Framework.Localization.Abstractions`；`Microsoft.Extensions.Configuration.Abstractions` 10.0.13 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

5.0 不另造配置模型，默认的配置来源就是 .NET 原生的文件、环境变量与命令行，所以配置子系统没有默认实现包。本包只补两类 .NET 没有统一契约的来源：

- 远程配置（`configuration.remote@1`）：按命名空间取整份带版本的文档，键已展平成 `A:B:C`。`AddXiHanRemoteConfiguration(client, options)` 构建配置时同步加载一次，之后按周期带已知版本轮询，版本变化时整体替换并触发重载令牌，失败保留最后一份成功的数据。
- 密钥（`configuration.secrets@1`）：按名字批量读取，只读，逐项返回结果；`SecretValue.ToString()` 固定返回遮蔽文本。`AddXiHanSecrets(store, options)` 把密钥映射到配置键，构建时一次批量读取，可周期重读。

两个配置源都派生自内部的键值表 Provider：数据整体替换，替换时先换上新令牌再取消旧令牌；子键的枚举顺序与 `Microsoft.Extensions.Configuration` 一致（数字段按数值、其余按忽略大小写的序数）。键不区分大小写。

## 何时使用

- 用 Nacos 做配置中心：装 [Configuration.Nacos](./configuration-nacos)，它实现 `IRemoteConfigurationClient`，本包随之引入。
- 接入其他配置中心或密钥库（Apollo、Consul、Vault、Key Vault 等）：5.0 没有提供这些 Provider，应用实现本包的接口，并用一致性套件验证。
- 只用文件与环境变量：不需要本包，见[配置与选项](../guide/configuration)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Configuration.Abstractions" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。`Profiles.Full` 引用了本包。

本包没有特性。配置源加在 `builder.Configuration` 上，并且要在 `AddXiHan` 之前，框架在 `AddXiHan` 里读取各包选项与绑定。应用自己实现的 Provider 要让错误按调用方语言翻译时，注册本包的消息目录：

```csharp
builder.Services.AddMessageCatalog(ConfigurationMessages.Catalog);
```

## 配置

本包没有配置节，两个配置源的选项在代码里构造。

`RemoteConfigurationOptions`：

| 属性 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Namespace` | `string` | 构造参数，不能为空白 | 远程命名空间 |
| `Prefix` | `string?` | `null` | 把文档挂到的配置节，为空时挂在根 |
| `PollInterval` | `TimeSpan?` | 30 秒 | 轮询周期，`null` 关闭，必须为正 |
| `LoadTimeout` | `TimeSpan` | 10 秒 | 首次加载的超时，必须为正 |
| `Optional` | `bool` | `false` | 首次加载失败时继续（该源为空）还是抛出 |
| `OnError` | `Action<Error>?` | `null` | 加载或轮询失败的回调，配置在日志之前构建，这是唯一的报告渠道 |

`SecretsConfigurationOptions`：

| 属性 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Mappings` | `IReadOnlyDictionary<string, SecretName>` | 构造参数，至少一项 | 配置键 → 密钥名 |
| `LoadTimeout` | `TimeSpan` | 10 秒 | 首次读取的超时 |
| `RefreshInterval` | `TimeSpan?` | `null` | 周期重读的间隔，`null` 不重读 |
| `Optional` | `bool` | `false` | 首次读取缺失或失败的项跳过还是抛出 |
| `OnError` | `Action<Error>?` | `null` | 每个失败的项回调一次 |

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `Remote.IRemoteConfigurationClient` | `FetchAsync(namespaceName, knownVersion, cancellationToken)` → `Result<Maybe<ConfigurationDocument>>`；`knownVersion` 为空总是返回文档，非空且未变化返回无值，可长轮询，收到取消返回无值 |
| `Remote.ConfigurationDocument` | 版本（ETag、修订号或内容哈希，非空）与展平的键值；`Empty(version)` 建空文档 |
| `Remote.RemoteConfigurationOptions` | 远程配置源的选项 |
| `Remote.RemoteConfigurationBuilderExtensions` | `AddXiHanRemoteConfiguration(client, options)` |
| `Remote.RemoteConfigurationSource` | 对应的 `IConfigurationSource` |
| `Secrets.ISecretStore` | `GetAsync(names, cancellationToken)` 批量逐项返回 `SecretLookup`，顺序与入参一致 |
| `Secrets.SecretName` | 密钥名，`^[A-Za-z0-9][A-Za-z0-9._/-]{0,255}$`，不含 `..`、`//`，不以 `/` 结尾，大小写敏感；可从 `string` 隐式转换 |
| `Secrets.SecretValue` | 值与可选版本；`ToString()` 返回 `Redacted`（`***`） |
| `Secrets.SecretLookup` | 一项结果：`Name` 与 `Result<SecretValue>` |
| `Secrets.SecretStoreExtensions` | 单条读取 `GetAsync(store, name, cancellationToken)`，转调批量方法 |
| `Secrets.SecretsConfigurationOptions` / `SecretsConfigurationBuilderExtensions` / `SecretsConfigurationSource` | 密钥配置源的选项、`AddXiHanSecrets(store, options)` 与配置源 |
| `ConfigurationCapabilities` | 能力 ID 常量 `Remote`、`Secrets` 与接口版本 `Version` |
| `ConfigurationErrors` | 错误码常量 |
| `ConfigurationMessages` | 消息键常量与本包的消息目录 `Catalog` |

## 使用示例

把远程配置挂到 `Orders` 节下，并把一个密钥映射成绑定的连接串。`nacos` 可以换成任何 `IRemoteConfigurationClient` 实现，`vault` 是应用自己的 `ISecretStore`：

```csharp
using XiHan.Framework.Configuration.Abstractions.Remote;
using XiHan.Framework.Configuration.Abstractions.Secrets;
using XiHan.Framework.Configuration.Nacos;

var builder = WebApplication.CreateBuilder(args);
var nacos = NacosConfigurationClient.Create(NacosOptions.FromConfiguration(builder.Configuration));
var vault = new DirectorySecretStore("/run/secrets");

builder.Configuration
    .AddXiHanRemoteConfiguration(nacos, new RemoteConfigurationOptions("orders") { Prefix = "Orders", PollInterval = TimeSpan.FromSeconds(15) })
    .AddXiHanSecrets(vault, new SecretsConfigurationOptions(new Dictionary<string, SecretName>
    {
        ["XiHan:Bindings:db:value"] = "orders/db-connection",
    }));
```

`DirectorySecretStore` 的完整实现见[配置与选项](../guide/configuration#密钥)。自己实现的密钥库用一致性套件验证，测试项目引用 `XiHan.Framework.Configuration.Conformance`：

```csharp
using XiHan.Framework.Configuration.Abstractions.Secrets;
using XiHan.Framework.Configuration.Conformance.Secrets;

namespace Orders.Secrets.Tests;

public sealed class DirectorySecretStoreConformanceTests : SecretStoreConformance
{
    protected override Task<SecretStoreFixture> CreateFixtureAsync() => Task.FromResult<SecretStoreFixture>(new Fixture());

    private sealed class Fixture : SecretStoreFixture
    {
        private readonly string _root = Directory.CreateTempSubdirectory().FullName;

        public override ISecretStore Store => new DirectorySecretStore(_root);

        // 套件经它写入密钥，再从 Store 读回比对
        public override async Task PutAsync(SecretName name, string value, CancellationToken cancellationToken)
        {
            var path = Path.Combine(_root, name.Value);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, value, cancellationToken);
        }
    }
}
```

单条读取用扩展方法，失败时 `Error.Code` 是下表里的错误码：

```csharp
var secret = await vault.GetAsync("orders/api-key", cancellationToken);
if (secret.TryGetValue(out var value))
{
    // 插值或日志里 value 显示为 "***"，真正的值只从 Value 取
    var apiKey = value.Value;
}
```

## 行为与限制

- 首次加载走同步的 `IConfigurationProvider.Load`：取值在线程池上执行并等待 `LoadTimeout`。超时归为 `CONFIGURATION_UNAVAILABLE_001`（远程配置）或 `CONFIGURATION_UNAVAILABLE_002`（密钥）。
- 远程配置首次加载失败且 `Optional = false` 时抛出 `FrameworkException`，错误码是失败结果的码。轮询中客户端返回失败或抛出异常，都只回调 `OnError`，保留最后一份成功的数据；拿到的文档版本与当前相同时不触发重载。
- 密钥首次读取中任一项失败且 `Optional = false` 时抛出 `FrameworkException`，消息里列出失败的配置键；重读时失败的项保留旧值。密钥库返回的结果条数与请求不一致时首次加载抛出 `CONFIGURATION_UNAVAILABLE_002`。
- 遮蔽只作用于 `SecretValue`：映射进 `IConfiguration` 的值是原始字符串。
- 配置源随配置根释放：释放时停止轮询与重读。

| 错误码 | 常量 | 含义 |
| --- | --- | --- |
| `CONFIGURATION_NOTFOUND_001` | `NamespaceNotFound` | 远程配置的命名空间不存在 |
| `CONFIGURATION_NOTFOUND_002` | `SecretNotFound` | 密钥不存在 |
| `CONFIGURATION_UNAVAILABLE_001` | `RemoteUnavailable` | 远程配置不可达或超时 |
| `CONFIGURATION_UNAVAILABLE_002` | `SecretsUnavailable` | 密钥库不可达或超时 |
| `CONFIGURATION_DENIED_001` | `SecretDenied` | 没有读取该密钥的权限 |
| `CONFIGURATION_VALIDATION_001` | `DocumentInvalid` | 远程文档不是声明的格式 |

选项值越界、密钥名不合法、命名空间为空属于编程错误，构造时抛出 `ArgumentException` 或 `ArgumentOutOfRangeException`。

## 相关

- [配置与选项](../guide/configuration)
- [Configuration.Nacos](./configuration-nacos)
- [Conformance 一致性套件](./conformance)
- 规范：RFC-0014《远程配置与密钥》
