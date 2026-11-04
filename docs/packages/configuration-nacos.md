# XiHan.Framework.Configuration.Nacos

`configuration.remote@1` 的 Nacos Provider：直接调用 Nacos 的 HTTP 配置接口取整份文档，按内容 MD5 长轮询，不依赖 Nacos SDK。用 Nacos 做配置中心的应用引用本包。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Configuration.Nacos` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `configuration.remote@1`（`role: provider`）；依赖 `kernel.hosting@1`；可选绑定 `nacos`（`configuration:remote`） |
| 特性入口 | `NacosConfigurationFeature` |
| 配置节 | `XiHan:Configuration:Nacos` |
| 依赖 | `XiHan.Framework.Configuration.Abstractions`、`XiHan.Framework.Kernel`；`Microsoft.Extensions.Http` |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

本包有两个入口，用在两个时机：

- 配置构建期：`builder.Configuration.AddXiHanNacos(nacosOptions, remoteOptions)` 按选项自建客户端（`NacosConfigurationClient.Create`），经工厂重载交给 `AddXiHanRemoteConfiguration`，于是 Nacos 里的文档成为一个配置源，首次同步加载、之后轮询重载；自建的客户端连同它的 HTTP 客户端归这个配置源，配置根释放时一并释放。这一步发生在容器建立之前，绝大多数应用只需要它。
- 运行期：`UseFeature<NacosConfigurationFeature>()` 从 `XiHan:Configuration:Nacos` 读选项，经 `IHttpClientFactory` 建客户端，并在本包被选为 `configuration.remote@1` 的提供者时登记为 `IRemoteConfigurationClient`，供其他组件按需取文档。

客户端用 Nacos 的 v1 HTTP 配置接口（与 Nacos 2.x 兼容）。命名空间参数是 Nacos 的 `dataId`，`Group` 与 `Tenant` 来自选项。版本是内容的 MD5，取服务端在响应头 `Content-MD5` 里给出的值，与 Nacos 监听接口的约定一致；`knownVersion` 非空时先以该 MD5 调监听接口长轮询，服务端报告变化才取整份文档，窗口到期或调用方取消都返回无值。

## 何时使用

配置放在 Nacos 里、希望改了以后应用不重启就生效时使用。配置在 Kubernetes ConfigMap 里时用宿主自带的挂载目录（`XIHAN_CONFIG_PATH`）即可，见[配置与选项](../guide/configuration)。Apollo、Consul 等其他配置中心没有提供 Provider，需要应用实现 `IRemoteConfigurationClient`。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Configuration.Nacos --prerelease
```

框架尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

```csharp
using XiHan.Framework.Configuration.Abstractions.Remote;
using XiHan.Framework.Configuration.Nacos;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// 配置构建期：dataId "orders" 挂到 Orders 节下，每 15 秒以已知版本长轮询
builder.Configuration.AddXiHanNacos(
    NacosOptions.FromConfiguration(builder.Configuration),
    new RemoteConfigurationOptions("orders") { Prefix = "Orders", PollInterval = TimeSpan.FromSeconds(15) });

// 运行期：登记 IRemoteConfigurationClient（只在需要按需取文档时装）
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<NacosConfigurationFeature>());
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `ServerAddresses` | `string` | 无，必填 | 逗号或分号分隔的绝对 http / https 地址，按序尝试；地址的路径是 Nacos 的上下文路径，没有路径时用 `/nacos`（见下文）。有绑定 `nacos` 时被绑定的主值覆盖，两者都没有时抛出 `ArgumentException` |
| `Group` | `string` | `DEFAULT_GROUP` | 配置分组 |
| `Tenant` | `string` | 空（公共命名空间） | Nacos 命名空间 ID |
| `Username` | `string` | — | 与 `Password` 都给出时先登录取 `accessToken` |
| `Password` | `string` | — | 登录密码 |
| `Format` | `Json` / `Properties` | `Json` | 文档格式，不区分大小写 |
| `LongPollTimeoutSeconds` | 正数 | 30 | 长轮询窗口 |
| `RequestTimeoutSeconds` | 正数 | 10 | 单次请求（取文档、登录）的超时 |

```json
{
  "XiHan": {
    "Configuration": {
      "Nacos": {
        "ServerAddresses": "http://nacos-a:8848,http://nacos-b:8848",
        "Group": "DEFAULT_GROUP",
        "Tenant": "",
        "Username": "dev",
        "Password": "dev-password",
        "Format": "Json",
        "LongPollTimeoutSeconds": 30,
        "RequestTimeoutSeconds": 10
      }
    }
  }
}
```

服务地址与凭据也可以用绑定提供：绑定的主值覆盖配置里的 `ServerAddresses`，属性 `username` / `password` 各自覆盖 `Username` / `Password`（空白不算给出），分组、格式等其余键照读配置节：

```json
{ "XiHan": { "Bindings": { "nacos": { "type": "configuration:remote", "value": "http://localhost:8848", "username": "nacos", "password": "<来自密钥源>" } } } }
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `NacosConfigurationFeature` | 特性入口；常量 `ConnectionBinding` = `nacos` |
| `NacosConfigurationBuilderExtensions` | `AddXiHanNacos(options, remote)`：配置构建期的入口，自建的客户端随配置根释放 |
| `NacosConfigurationClient` | `IRemoteConfigurationClient` 实现；`Create(options, clock)` 自建 HTTP 客户端并在释放时关闭，公开构造函数接收外部的 `HttpClient`（超时应为无限，由本类按请求控制）；常量 `HttpClientName` |
| `NacosOptions` | 选项；`FromConfiguration(configuration, binding)` 从配置与绑定读取；常量 `ConfigurationSection` |
| `NacosConfigFormat` | `Json`、`Properties` |
| `NacosDocumentParser` | `Parse(content, format)` 把文档展平成配置键值 |

## 使用示例

properties 格式的文档挂在根上，并在 Nacos 不可达时继续启动：

```csharp
builder.Configuration.AddXiHanNacos(
    new NacosOptions { ServerAddresses = ["http://localhost:8848"], Format = NacosConfigFormat.Properties },
    new RemoteConfigurationOptions("orders.properties")
    {
        Optional = true,
        OnError = static error => Console.Error.WriteLine($"Nacos unavailable: {error.Code}"),
    });
```

`orders.properties` 的内容 `orders.limits.max-items=50` 展平后是配置键 `orders:limits:max-items`。

运行期按需取一份文档（装了 `NacosConfigurationFeature`）：

```csharp
app.MapGet("/config/orders", async (IRemoteConfigurationClient client, CancellationToken ct) =>
{
    var fetched = await client.FetchAsync("orders", knownVersion: null, ct);
    return fetched.TryGetValue(out var document) && document.TryGetValue(out var value)
        ? Results.Ok(value.Version)
        : Results.NotFound();
});
```

## 行为与限制

- 格式：`Json` 要求根是对象，嵌套对象按 `A:B` 展平，数组按下标，`null` 保留为空值；`Properties` 把 `a.b=c` 展平成 `a:b`，`#` 与 `!` 开头的行是注释。文档不合法返回 `CONFIGURATION_VALIDATION_001`。
- 登录：配置了 `Username` 与 `Password` 时调用上下文路径下的 `/v1/auth/login`（缺省即 `/nacos/v1/auth/login`）取 `accessToken`，在到期前一分钟续期；响应没有 `tokenTtl` 时按 18000 秒计。
- 多地址：每次请求按 `ServerAddresses` 的顺序尝试，连接失败或超时换下一个，全部失败时返回不可达。
- 版本：服务端没有给出 `Content-MD5`（例如中间的代理去掉了这个头）时，客户端按 Nacos 的算法自己算，即内容按 UTF-8 编码的 MD5（小写十六进制），监听接口照样认得这个版本，长轮询不受影响。
- 上下文路径：地址的路径就是 Nacos 的上下文路径（`server.servlet.contextPath`），登录、取配置、监听的接口路径（`/v1/auth/login`、`/v1/cs/configs`、`/v1/cs/configs/listener`）接在它后面；地址没有路径（或只有 `/`）时用 Nacos 的缺省值 `/nacos`，所以 `http://nacos:8848` 与 `http://nacos:8848/nacos` 等价。Nacos 改了上下文路径时写 `http://nacos:8848/config`；经网关挂在前缀下时把网关前缀连同上下文路径写全，例如网关把 `/config-center/` 转发到 Nacos 的根上，就写 `http://gateway/config-center/nacos`。上下文路径设成根（`/`）的 Nacos 无法用地址表达，不受支持。
- 一致性：通过 `Configuration.Conformance` 的 `RemoteConfigurationClientConformance`（Nacos 2.5.1）。测试需要真实服务，设置 `XIHAN_TEST_NACOS`（如 `http://localhost:8848`）后运行，开启鉴权的服务再设 `XIHAN_TEST_NACOS_USERNAME` / `XIHAN_TEST_NACOS_PASSWORD`。Nacos 3.x 默认开启鉴权且 v1 接口不再可用，未验证。

| 错误码 | 含义 |
| --- | --- |
| `CONFIGURATION_NOTFOUND_001` | `dataId` 不存在（Nacos 返回 404） |
| `CONFIGURATION_UNAVAILABLE_001` | 不可达、超时、登录失败或非 2xx 响应 |
| `CONFIGURATION_VALIDATION_001` | 文档不是声明的格式 |

## 相关

- [配置与选项](../guide/configuration)
- [Configuration.Abstractions](./configuration-abstractions)
- 规范：RFC-0014《远程配置与密钥》、ADR-0019
