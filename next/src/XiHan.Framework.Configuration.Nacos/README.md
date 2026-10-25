# XiHan.Framework.Configuration.Nacos

`configuration.remote@1` 的 Nacos Provider（Ring 3，`role: provider`，RFC-0014「Nacos Provider」）。直接用 Nacos 的 HTTP 配置接口（v1，Nacos 2.x 兼容），不依赖 SDK。

```csharp
// 配置构建期：把 dataId "orders" 挂到 Orders 节下，每 15 秒以已知版本长轮询一次
builder.Configuration.AddXiHanNacos(
    new NacosOptions { ServerAddresses = ["http://localhost:8848"], Username = "nacos", Password = "nacos" },
    new RemoteConfigurationOptions("orders") { Prefix = "Orders", PollInterval = TimeSpan.FromSeconds(15) });

// 运行期：登记 IRemoteConfigurationClient 供其他组件取用
builder.AddXiHan(x => x.UseFeature<NacosConfigurationFeature>());
```

```json
{ "XiHan": { "Configuration": { "Nacos": { "ServerAddresses": "http://nacos-a:8848,http://nacos-b:8848", "Group": "DEFAULT_GROUP", "Tenant": "", "Username": "nacos", "Password": "nacos", "Format": "Json", "LongPollTimeoutSeconds": 30, "RequestTimeoutSeconds": 10 } } } }
```

服务地址来自 `XiHan:Configuration:Nacos:ServerAddresses`，或绑定 `nacos`（`configuration:remote`）的值。

## 语义

- 命名空间参数即 Nacos 的 `dataId`；`Group`（默认 `DEFAULT_GROUP`）与 `Tenant`（Nacos 命名空间 ID，默认公共）来自选项。
- 版本是内容的 MD5（小写十六进制，取响应头 `Content-MD5`，没有就自己算），与 Nacos 监听接口的约定一致。`knownVersion` 非空时先以该 MD5 长轮询（`LongPollTimeout`，默认 30 秒）：服务端报告变化才取整份文档，窗口到期或取消返回无值。
- 格式 `Format`：`Json`（默认，根须是对象，嵌套对象按 `A:B`、数组按下标展平，`null` 保留为空值）或 `Properties`（`a.b=c` → `a:b`，`#` / `!` 行是注释）；不合法返回 `CONFIGURATION_VALIDATION_001`。
- 配置了 `Username` / `Password` 时先登录取 `accessToken`，到期前一分钟自动续；多个地址按序尝试，连接失败或超时换下一个。
- `dataId` 不存在 → `CONFIGURATION_NOTFOUND_001`；不可达、超时、非 2xx → `CONFIGURATION_UNAVAILABLE_001`。

## 测试

一致性套件需要真实 Nacos：设置环境变量 `XIHAN_TEST_NACOS`（如 `http://localhost:8848`）后运行，开启了鉴权的服务再设 `XIHAN_TEST_NACOS_USERNAME` / `XIHAN_TEST_NACOS_PASSWORD`；未设置时跳过。本地 `docker run -d -p 8848:8848 -e MODE=standalone nacos/nacos-server:v2.5.1` 即可（3.x 镜像默认开启鉴权且 v1 接口不再可用，未验证）。

## 依赖方向

依赖 `Configuration.Abstractions`、`Kernel`（绑定与清单）与 `Microsoft.Extensions.Http`。

## 状态

P5：可用，通过 `Configuration.Conformance` 套件（Nacos 2.5.1）。
