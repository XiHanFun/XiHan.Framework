# 4.4 远程配置与密钥

这一页讲把配置中心与密钥库接进 `IConfiguration` 的两个契约：`IRemoteConfigurationClient` 与 `ISecretStore`，以及对应的配置源 `AddXiHanRemoteConfiguration` / `AddXiHanSecrets`、框架自带的 Nacos Provider、加载失败时的行为与错误码、凭据怎样避免进入日志。读完你能把一份 Nacos 文档挂到配置树上并随变化重载，也能为自己的密钥来源写一个 `ISecretStore`。前置知识是 [4.1 配置模型与选项](/configuration/options)与 [4.3 资源绑定](/configuration/bindings)。

## 为什么需要

集中管理的配置（配置中心）与敏感值（密钥库）都不该写进镜像或 ConfigMap：前者要在不发版的情况下调整，后者要受访问控制并定期轮换。每家配置中心与密钥库的 SDK 都不一样，如果应用直接调用它们，换一家就要改所有读配置的地方。

框架把它们都收敛到 `IConfiguration`：`Configuration.Abstractions` 定义两个很小的契约，并提供把契约实现接进 `IConfigurationBuilder` 的配置源。应用代码照旧读 `IConfiguration` 或 `IOptionsMonitor<T>`，远端文档变化后自动重载；密钥按名字映射到配置键，`XiHan:Bindings:db:value` 这类绑定主值也能来自密钥库。失败语义在契约里写明：首次加载失败默认让启动失败，之后的轮询失败保留最后一份成功的数据。

## 两个契约

| 能力 ID | 契约 | 作用 |
| --- | --- | --- |
| `configuration.remote@1` | `IRemoteConfigurationClient.FetchAsync(namespace, knownVersion, ct)` | 按命名空间取整份带版本的文档 `ConfigurationDocument`，键已展平成 `A:B:C`；`knownVersion` 非空且远端没有变化时返回无值，实现可以长轮询 |
| `configuration.secrets@1` | `ISecretStore.GetAsync(names, ct)` | 按名字批量读取密钥，只读；逐项返回 `SecretLookup(Name, Result<SecretValue>)`，顺序与入参一致 |

两个契约的返回值都是 `Result`：命名空间不存在、密钥没有权限这些可恢复的失败以错误码返回，不抛异常。配置源对应两个扩展方法：

| 扩展方法 | 选项 | 作用 |
| --- | --- | --- |
| `AddXiHanRemoteConfiguration(client 或 clientFactory, RemoteConfigurationOptions)` | 命名空间、前缀、轮询周期 | 把远程文档挂到配置树 |
| `AddXiHanSecrets(store, SecretsConfigurationOptions)` | 配置键到密钥名的映射、重读周期 | 把密钥值写到指定配置键 |

两者都要在 `AddXiHan` 之前调用：框架在 `AddXiHan` 里读取选项与绑定。

## 最小示例

我们把 Nacos 里 `dataId` 为 `orders` 的 JSON 文档挂到 `Orders` 节下，应用用 `IOptionsMonitor<OrderLimits>` 读它。先安装 Provider：

```bash
dotnet add package XiHan.Framework.Configuration.Nacos --prerelease
```

```csharp
// Program.cs
using Microsoft.Extensions.Options;
using XiHan.Framework.Configuration.Abstractions.Remote;
using XiHan.Framework.Configuration.Nacos;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// 必须在 AddXiHan 之前：框架在 AddXiHan 里读取选项与绑定
builder.Configuration.AddXiHanNacos(
    NacosOptions.FromConfiguration(builder.Configuration),
    new RemoteConfigurationOptions("orders")
    {
        Prefix = "Orders",
        PollInterval = TimeSpan.FromSeconds(15),
        // 配置在日志之前构建，OnError 是唯一的报告渠道
        OnError = static error => Console.Error.WriteLine($"Nacos: {error.Code}"),
    });
builder.Services.Configure<OrderLimits>(builder.Configuration.GetSection("Orders"));

builder.AddXiHan(xihan => xihan.UseStandardProfile());

var app = builder.Build();
app.UseXiHan();
app.MapGet("/limits", (IOptionsMonitor<OrderLimits> limits) => limits.CurrentValue.MaxItems);
app.Run();

public sealed class OrderLimits
{
    public int MaxItems { get; set; } = 100;
}
```

`appsettings.json` 里给出 Nacos 的连接设置：

```json
{
  "XiHan": {
    "Configuration": {
      "Nacos": {
        "ServerAddresses": "http://localhost:8848",
        "Group": "DEFAULT_GROUP",
        "Format": "Json"
      }
    }
  }
}
```

在 Nacos 里发布 `dataId=orders`、内容为 `{ "MaxItems": 500 }` 的配置后启动应用，`curl localhost:5000/limits` 返回 `500`。在 Nacos 控制台把值改成 `800`，长轮询感知到变化，整份文档替换并触发重载，几秒内端点返回 `800`。停掉 Nacos 再启动应用，首次加载超时，`OnError` 收到 `CONFIGURATION_UNAVAILABLE_001`，随后 `FrameworkException` 让启动失败：`Optional` 缺省为 `false`，缺了配置中心不会带着默认值运行。

## 远程配置的加载与轮询

`AddXiHanRemoteConfiguration` 的行为分四步：

1. 构建配置时同步加载一次。`IConfigurationProvider.Load` 是同步契约，取值在线程池上执行并等待，受 `LoadTimeout`（缺省 10 秒）约束。
2. 首次加载失败（客户端返回失败结果、超过 `LoadTimeout`，或客户端抛出异常，后两者都归 `CONFIGURATION_UNAVAILABLE_001`）时先回调 `OnError`；`Optional` 为 `false`（缺省）时随后抛出带错误码的 `FrameworkException`（客户端抛出的异常是它的内部异常），启动失败；为 `true` 时该源为空。
3. 不论首次加载成败，之后都按 `PollInterval`（缺省 30 秒，`null` 关闭轮询）带已知版本轮询；首次失败时已知版本为空，第一次成功的轮询装入整份文档。拿到新版本就整体替换这个源的数据并触发重载令牌，`IOptionsMonitor<T>` 随之更新。
4. 轮询失败只回调 `OnError`，保留最后一份成功的数据。

| 选项 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Namespace` | `string` | 构造参数 | 远程命名空间，Nacos 里是 `dataId` |
| `Prefix` | `string?` | `null` | 挂载的配置节，为空时挂在根 |
| `PollInterval` | `TimeSpan?` | 30 秒 | 轮询周期，`null` 关闭，必须为正 |
| `Clock` | `TimeProvider` | 系统时钟 | 轮询计时用的时钟；测试换成可推进的时钟，轮询由用例触发 |
| `LoadTimeout` | `TimeSpan` | 10 秒 | 首次加载的超时，必须为正 |
| `Optional` | `bool` | `false` | 首次加载失败时是否继续启动（该源为空，照常轮询） |
| `OnError` | `Action<Error>?` | `null` | 加载或轮询失败时的回调；它抛出的异常被丢弃，不改变结果也不终止轮询 |

客户端的归属看用哪个重载：`AddXiHanRemoteConfiguration(client, options)` 传入的客户端由调用方持有，配置根释放时不释放它；`AddXiHanRemoteConfiguration(clientFactory, options)` 每次构建 Provider 按工厂新建一个客户端，配置根释放时随之释放（实现 `IDisposable` 时）。配置构建期自建、没有别人持有的客户端用工厂重载。

## Nacos Provider

`Configuration.Nacos` 直接调用 Nacos 的 HTTP 配置接口（v1，与 Nacos 2.x 兼容），不依赖 SDK：命名空间参数就是 `dataId`，版本是服务端给出的内容 MD5，`knownVersion` 非空时先长轮询监听，服务端报告变化才取整份文档。支持 `Json` 与 `Properties` 两种格式、用户名密码登录、多个服务地址按序尝试。

| 成员 | 说明 |
| --- | --- |
| `AddXiHanNacos(options, remote)` | 经工厂重载自建客户端（自带 HTTP 客户端），随配置根释放 |
| `NacosOptions.FromConfiguration(configuration, binding)` | 从 `XiHan:Configuration:Nacos` 读取；传入绑定 `nacos` 时主值覆盖 `ServerAddresses`，属性 `username`、`password` 覆盖 `Username`、`Password` |
| `NacosOptions` | `ServerAddresses`、`Group`、`Tenant`（Nacos 命名空间）、`Format`、`Username`、`Password`、`RequestTimeout`、`LongPollTimeout` |
| `NacosConfigurationFeature` | 装配后运行期容器里登记 `IRemoteConfigurationClient`，供其他组件按需取文档；配置源本身不需要它 |

## 密钥

`ISecretStore.GetAsync(names, ct)` 的约定：

- 批量读取，逐项返回 `SecretLookup(Name, Result<SecretValue>)`，顺序与入参一致，重复项各自作答，空集合返回空。
- 只读。写入与轮换属于运维工具，不在应用契约里。
- 密钥名 `SecretName` 的文法是 `^[A-Za-z0-9][A-Za-z0-9._/-]{0,255}$`，不含 `..` 与 `//`，不以 `/` 结尾，大小写敏感；不合法在构造时抛出 `ArgumentException`。`string` 可隐式转换成 `SecretName`。
- `SecretValue.ToString()` 固定返回 `***`（常量 `SecretValue.Redacted`），值不会经字符串插值进入日志或异常消息；读值用 `Value`，可选的 `Version` 记录密钥版本。
- 单条读取用扩展方法 `store.GetAsync(name, cancellationToken)`，它转调批量方法。

`AddXiHanSecrets(store, options)` 把密钥映射到配置键：`Mappings` 是配置键到密钥名的字典，构建配置时一次批量读取。失败分两类：逐项失败（不存在、无权限等）每项回调一次 `OnError`；整体失败（超过 `LoadTimeout`、密钥库抛出异常、返回的条数与请求不一致）回调一次 `CONFIGURATION_UNAVAILABLE_002`。首次读取有失败时，`Optional` 为 `false`（缺省）随后抛出 `FrameworkException`（逐项失败列出失败的配置键，密钥库抛出的异常是它的内部异常），为 `true` 时跳过失败的项，整体失败时该源为空。`RefreshInterval` 非空时周期性重读：逐项失败的项保留旧值，整体失败保留全部旧值并回调。

| 选项 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Mappings` | `IReadOnlyDictionary<string, SecretName>` | 构造参数 | 配置键 → 密钥名，至少一项 |
| `LoadTimeout` | `TimeSpan` | 10 秒 | 首次读取的超时 |
| `RefreshInterval` | `TimeSpan?` | `null` | 周期重读的间隔，`null` 不重读 |
| `Clock` | `TimeProvider` | 系统时钟 | 重读计时用的时钟 |
| `Optional` | `bool` | `false` | 首次读取缺失或失败的项是否跳过 |
| `OnError` | `Action<Error>?` | `null` | 每个失败的项回调一次，整体失败回调一次；它抛出的异常被丢弃 |

框架没有提供密钥库 Provider（Vault、Key Vault 等），应用按自己的密钥来源实现 `ISecretStore`，并让测试项目继承 `Configuration.Conformance` 的 `SecretStoreConformance` 验证语义。下面的实现从一个目录读文件，每个密钥一个文件，适合容器平台把 Secret 挂成文件的场景：

```csharp
using XiHan.Framework.Configuration.Abstractions;
using XiHan.Framework.Configuration.Abstractions.Secrets;
using XiHan.Framework.Primitives;

namespace Shop.Secrets;

public sealed class DirectorySecretStore(string root) : ISecretStore
{
    public async ValueTask<IReadOnlyList<SecretLookup>> GetAsync(IReadOnlyList<SecretName> names, CancellationToken cancellationToken)
    {
        var results = new List<SecretLookup>(names.Count);
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // SecretName 的文法已排除 ".." 与开头的 "/"，拼出的路径不会跳出根目录
            var path = Path.Combine(root, name.Value);
            Result<SecretValue> result;
            try
            {
                result = File.Exists(path)
                    ? new SecretValue((await File.ReadAllTextAsync(path, cancellationToken)).Trim())
                    : Error.NotFound(ConfigurationErrors.SecretNotFound, ConfigurationMessages.SecretNotFound, MessageArguments.Of(("name", name.Value)));
            }
            catch (IOException exception)
            {
                result = Error.Unavailable(ConfigurationErrors.SecretsUnavailable, ConfigurationMessages.SecretsUnavailable, MessageArguments.Of(("reason", exception.Message)));
            }

            results.Add(new SecretLookup(name, result));
        }

        return results;
    }
}
```

把数据库绑定的主值交给它：

```csharp
// Program.cs：密钥源同样要在 AddXiHan 之前加入，绑定 db 的主值才能取到它
builder.Configuration.AddXiHanSecrets(
    new DirectorySecretStore("/run/secrets"),
    new SecretsConfigurationOptions(new Dictionary<string, SecretName>
    {
        ["XiHan:Bindings:db:value"] = "orders/db-connection",
    })
    {
        RefreshInterval = TimeSpan.FromMinutes(5),
    });
```

## 失败语义与错误码

| 错误码 | 含义 | 出现在 |
| --- | --- | --- |
| `CONFIGURATION_NOTFOUND_001` | 远程配置的命名空间不存在 | `IRemoteConfigurationClient.FetchAsync` |
| `CONFIGURATION_NOTFOUND_002` | 密钥不存在 | `ISecretStore.GetAsync` 的逐项结果 |
| `CONFIGURATION_UNAVAILABLE_001` | 远程配置不可达或超时（首次加载超时、客户端抛出异常也归此类） | `FetchAsync`、首次加载 |
| `CONFIGURATION_UNAVAILABLE_002` | 密钥库不可达或超时（密钥库抛出异常、返回条数与请求不一致也归此类） | `GetAsync`、首次读取 |
| `CONFIGURATION_DENIED_001` | 没有读取该密钥的权限 | `GetAsync` 的逐项结果 |
| `CONFIGURATION_VALIDATION_001` | 远程文档不是声明的格式 | `FetchAsync` |

这些码的常量在 `ConfigurationErrors`，对应的消息键在 `ConfigurationMessages`。首次加载失败且不可选时，`FrameworkException` 的错误码就是上表里对应的码；轮询与重读的失败只经 `OnError` 报告。选项值不合法、密钥名或命名空间为空属于编程错误，构造时抛出 `ArgumentException`。

## 选项打印时遮蔽凭据

框架的选项与携带凭据的记录在 `ToString()` 里把凭据打印成 `***`（`Redaction.Mask`），值为 `null` 时打印为空：看得出有没有配置，看不到值。访问密钥 ID（S3 的 `AccessKey`）是访问密钥的一半，同样遮蔽；与口令同在一个记录里的登录用户名（Redis 的 `User`、RabbitMQ 的 `UserName`、Nacos 的 `Username`）是登录凭据的另一半，也遮蔽；端点与令牌文件路径照常打印，排查连接问题要用到它们。连接串整串遮蔽。字典与列表照记录的缺省只打印类型名，不展开内容。

| 记录 | 遮蔽的成员 |
| --- | --- |
| `RedisCachingOptions` | `Configuration`（连接字符串）、`User`、`Password` |
| `RabbitMQMessagingOptions` | `Configuration`（AMQP URI）、`UserName`、`Password` |
| `NacosOptions` | `Username`、`Password` |
| `S3ObjectStorageOptions` | `AccessKey`、`SecretKey` |
| `AIProviderOptions` | `ApiKey` |
| `KubernetesSchedulingOptions` | `Token` |
| `JwtOptions` | `Secret` |
| `HostedBlobUrlOptions` | `Secret` |
| `PrincipalRecord`（`Security.InMemory`） | `ApiKey` |
| `TenantShard` | `ConnectionString` |
| `BindingValue` | `Value`（绑定的主值） |

应用自己的选项若是记录且带凭据，照样手写 `PrintMembers` 并用 `Redaction.Redact`，参考上表各类型的写法。

::: warning 进入 IConfiguration 的密钥是明文
遮蔽只作用于 `SecretValue` 对象与上表记录的 `ToString()`。映射进配置之后，`configuration["XiHan:Bindings:db:value"]` 返回的是原始字符串；不要把整个配置节序列化进日志或诊断端点。
:::

## 常见问题

### 为什么远程配置要写在 AddXiHan 之前

`AddXiHan` 在调用时就读取各包的选项与绑定。配置源在它之后加入，框架看不到远端的值，只有应用自己的 `IOptionsMonitor<T>` 能读到。

### 配置中心挂了，应用还能启动吗

缺省不能：首次加载失败抛出 `FrameworkException`。把 `Optional` 设为 `true` 则该源为空、照常启动并继续轮询，第一次成功的轮询装入整份文档。是否可选取决于缺了这份配置应用还能不能正确工作。

### 轮询期间配置中心不可达，会丢配置吗

不会。轮询失败只回调 `OnError`，最后一份成功的数据保留；恢复后下一次轮询拿到新版本再整体替换。

### 框架为什么不提供 Vault 或 Key Vault 的 Provider

密钥库的接入方式与认证机制因平台而异，框架只定义 `ISecretStore` 契约与一致性套件。实现一个只有一个方法，通过 `SecretStoreConformance` 就能保证与框架的失败语义一致。

### 密钥能不经配置直接在代码里取吗

能。`NacosConfigurationFeature` 这类特性会把客户端登记进容器；密钥库则把自己的 `ISecretStore` 注册成单例，用 `store.GetAsync(name, ct)` 取 `Result<SecretValue>`。记住 `SecretValue.ToString()` 是 `***`，读值要用 `Value`。

## 相关包与下一步

- [Configuration.Abstractions](/packages/configuration-abstractions)：两个契约与两个配置源
- [Configuration.Nacos](/packages/configuration-nacos)：Nacos Provider 的连接选项、长轮询与登录细节
- [Primitives](/packages/primitives)：`Result`、`Error` 与 `Redaction`
- 下一章：[5.1 ASP.NET Core 宿主](/web/hosting)
