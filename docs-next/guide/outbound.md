# 出站地址策略

本章讲框架怎样防服务端请求伪造（SSRF）：向用户给的地址发请求时，缺省只能连公网地址，回环、私网、链路本地这些地址都要在配置里放行。

设计与取舍见 RFC-0008「出站地址策略」和 ADR-0117。

## 为什么需要

流程定义里的 `Http` 节点、租户填的存储端点、机器人的 Webhook 地址，都是别人给服务器的地址。不加限制时，租户管理员能让服务器去访问内网服务，或者取云元数据地址 `169.254.169.254` 上的临时凭据。

只查 URL 挡不住：公网域名可以解析到内网地址，解析结果也可以在检查之后变掉（DNS 重绑定），对方还可以用 3xx 把请求引过去。所以策略放在建立连接的那一刻，按解析出的实际 IP 判断，连接用的就是检查过的地址。

## 缺省行为

| 地址 | 缺省 | 怎么放行 |
| --- | --- | --- |
| 公网地址 | 允许 | 不需要 |
| 回环 `127.0.0.0/8`、`::1` | 拒绝 | `AllowLoopback` |
| 私网 `10.0.0.0/8`、`172.16.0.0/12`、`192.168.0.0/16`、`fc00::/7` | 拒绝 | `AllowPrivateNetwork` |
| 链路本地 `169.254.0.0/16`、`fe80::/10`（多数云的元数据地址在这里） | 拒绝 | 只能写进 `AllowedNetworks` |
| 运营商级 NAT `100.64.0.0/10`（阿里云元数据 `100.100.100.200` 在这里） | 拒绝 | 只能写进 `AllowedNetworks` |
| AWS 的 IPv6 元数据 `fd00:ec2::254` | 拒绝，开了私网也拒绝 | 只能写进 `AllowedNetworks` |
| `0.0.0.0`、`::`、组播、保留、文档与测试地址 | 拒绝 | 只能写进 `AllowedNetworks` |

`::ffff:127.0.0.1`、`64:ff9b::7f00:1`、`2002:7f00:1::` 这些把 IPv4 写进 IPv6 的形式，按里面的 IPv4 判断。`http://2130706433/`、`http://0x7f.0.0.1/` 这些写法解析出来还是 `127.0.0.1`，同样被拒绝。

## 配置

配置节 `XiHan:Security:Outbound`，装配时读一次，改了要重启应用。值不合法时启动失败。

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `AllowLoopback` | bool | `false` | 放行回环地址。本地开发用，生产环境不要打开 |
| `AllowPrivateNetwork` | bool | `false` | 放行私网地址。不含链路本地、运营商级 NAT 与云元数据地址 |
| `AllowedNetworks` | string[] | 空 | 放行的网段，CIDR 写法，主机位必须为零（`10.1.0.0/16`，不能写 `10.1.2.3/16`）。单个地址写成 `/32` 或 `/128` |
| `AllowedHosts` | string[] | 空 | 放行的主机名，不区分大小写，整名相等，不支持通配符。不带协议和端口，国际化域名写 ASCII 形式 |

只需要内网里的几个服务时，优先用 `AllowedHosts` 或 `AllowedNetworks` 收窄范围，不要整个打开 `AllowPrivateNetwork`。

`AllowedHosts` 只看请求里写的主机名，不再看它解析到哪里，所以这个名字的 DNS 要是可信的。

### 本地开发放行 localhost

写进 `appsettings.Development.json`，不要写进会带到生产环境的配置：

```json
{
  "XiHan": {
    "Security": {
      "Outbound": {
        "AllowLoopback": true
      }
    }
  }
}
```

也可以用环境变量：

```bash
XiHan__Security__Outbound__AllowLoopback=true
```

`AllowLoopback` 只放行回环地址。本地的容器服务通常在别的地址上，按服务名放行：

```json
{
  "XiHan": {
    "Security": {
      "Outbound": {
        "AllowLoopback": true,
        "AllowedHosts": ["minio", "host.docker.internal"]
      }
    }
  }
}
```

开着代理软件的 fake-ip 模式（Clash、sing-box 等的 TUN）时，公网域名解析出来是 `198.18.0.0/15` 里的地址，缺省全部被拒绝，表现是连公网也报地址不允许，日志里解析出的地址都在这一段。开发机上这样放行：

```json
{
  "XiHan": {
    "Security": {
      "Outbound": {
        "AllowedNetworks": ["198.18.0.0/15"]
      }
    }
  }
}
```

这段放行之后，策略就只能看到假地址，真实目标由代理决定，所以只用在开发机上。

## 框架的哪些路径过策略

| 路径 | 是否接入 | 原因 |
| --- | --- | --- |
| 工作流 `Http` 活动的缺省客户端 | 接入 | 地址来自流程定义，租户管理员可编辑 |
| CloudEvents Webhook | 接入 | 地址是运维配的，但接收方是第三方，它的 3xx 能把请求引到内网 |
| S3 命名实例 | 接入 | 定义可能来自应用的库，端点可能是租户填的 |
| S3 缺省实例 | 不接入 | 端点是运维配的 |
| OpenAI Provider | 不接入 | 框架的端点是运维配的，本地模型多在回环或内网 |
| OpenID Connect 发现与 JWKS | 不接入 | 签发者是运维配的，它本身就是令牌的信任根 |
| Nacos、Kubernetes API Server、OTLP 导出 | 不接入 | 运维配的内网地址 |
| 数据库、缓存、消息代理 | 不接入 | 不是 HTTP，策略管不到 |

地址被拒绝时：

| 路径 | 结果 |
| --- | --- |
| `Http` 活动 | 节点故障 `WORKFLOW_UNEXPECTED_001`，消息键 `workflow.http.address_denied`，参数 `host` |
| CloudEvents Webhook | `PROTOCOLS_UNAVAILABLE_002`，`reason` 是 `OutboundAddressDeniedException` 的类型全名 |
| S3 命名实例 | 与连不上同样处理：建实例时的探测只算尚未确认，读写返回 `OBJECTSTORAGE_UNAVAILABLE_001` |

错误里只有请求里写的主机名，没有解析出的地址，这是内网信息。排查看日志：类别 `OutboundAddressPolicy` 有一条 Warning，带主机、端口和解析出的地址。

## 接到自己的 HttpClient

应用向用户给的地址发请求时，用同一个策略。它是宿主登记的单例，用 `Apply` 接到 `SocketsHttpHandler` 上。

命名客户端：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Security;

builder.Services.AddHttpClient("bot-webhook")
    .UseSocketsHttpHandler(static (handler, services) => services.GetRequiredService<OutboundAddressPolicy>().Apply(handler));
```

手工建的客户端：

```csharp
var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) };
addresses.Apply(handler);
var client = new HttpClient(handler);
```

OpenAI Provider 的端点来自用户时，给它的命名客户端接上：

```csharp
builder.Services.AddHttpClient(OpenAIFeature.HttpClientName)
    .UseSocketsHttpHandler(static (handler, services) => services.GetRequiredService<OutboundAddressPolicy>().Apply(handler));
```

被拒绝时 `HttpClient` 抛 `HttpRequestException`，`InnerException` 是 `OutboundAddressDeniedException`：

```csharp
try
{
    using var response = await client.PostAsync(url, content, cancellationToken);
    // …
}
catch (HttpRequestException exception) when (exception.InnerException is OutboundAddressDeniedException)
{
    // 告诉用户这个地址不允许，不要把解析出的地址写给他
}
```

要在保存配置时就提示，可以用 `IsAllowed(IPAddress)` 判断一个 IP。它只是提前给出提示，连接时还会再查，不能代替连接时的检查。

## 不是 HTTP 的连接

地址同样来自用户、但不走 HTTP 时（租户自己填的 SMTP 服务器），用 `ConnectAsync`（ADR-0126）。规则与 HTTP 路径相同，连的就是检查过的地址：

```csharp
// 连好的套接字由调用方释放；协议库接过去之后自己做 STARTTLS
using var socket = await addresses.ConnectAsync(host, port, cancellationToken);
await smtp.ConnectAsync(socket, host, port, SecureSocketOptions.Auto, cancellationToken);
```

全部地址被拒绝时抛 `OutboundAddressDeniedException`，连不上时抛 `SocketException`。不要先解析再用 `IsAllowed` 判断、再自己连接：两次解析之间就是 DNS 重绑定的窗口。

自己连接的驱动（数据库、消息代理等）不经过这里。

## 限制

- **代理**：`Apply` 会关掉 `UseProxy`。经代理时连接回调看到的是代理的地址，查不到真正的目标。之后再打开代理就绕过了策略，要用代理就在代理上做这层限制。
- **已有连接回调**：处理器已经有别的 `ConnectCallback` 时，`Apply` 抛 `InvalidOperationException`。两者只能有一个。
- **替换主处理器**：`ConfigurePrimaryHttpMessageHandler` 或自己注册 `WorkflowHttpClient` 之后，由替换后的处理器决定能连哪里，测试替身就是这样接入的。
- **HTTP/3**：走 QUIC，不经过连接回调。请求缺省是 HTTP/1.1，把 `Version` 设成 3.0 的请求不受保护。
- **解析结果一部分被拒绝**：只连其余的，记一条 Debug。全部被拒绝才算拒绝。

## 相关

- [配置与选项](./configuration)
- [工作流](./workflow)
- [对象存储](./storage)
- [gRPC、MCP 与 CloudEvents](./protocols)
- [Kernel 包](../packages/kernel)
