# 示例说明

框架仓库的 `framework/samples/` 目录下有八个示例。它们不是演示用的玩具：每个都在 CI 里构建、测试或冒烟，文档里的代码片段大多出自这里。这一页说明每个示例演示什么、怎样运行，以及读教程时该在什么时候打开哪一个。

## 一览

| 示例 | 组合包 | 演示什么 | 外部设施 |
| --- | --- | --- | --- |
| `SimpleApp` | Standard | 订单的两个操作契约同时经 HTTP、gRPC 与 MCP 暴露；入站 CloudEvents 交给消息处理器；带租户、ApiKey 认证、权限、事务、领域事件与事务性发件箱；Native AOT 发布冒烟 | 无 |
| `SimpleCli` | Minimal | 同一份操作契约作为命令行命令与 MCP stdio tool | 无 |
| `KubernetesApp` | Full | 探针、优雅停机、ConfigMap 热更新、Lease 领导选举；`deploy/` 是 `xihan pack` 生成的部署产物 | 本地无；集群验证需要 kind 或任意 Kubernetes |
| `SamplePlugin` | 进程内插件 | 用 .NET 写的进程内插件，贡献一个操作 | 无 |
| `EchoPlugin` | 进程外插件 | 用 .NET 写的进程外插件子进程：握手、操作、事件订阅、崩溃 | 无 |
| `EchoGoPlugin` | 进程外插件 | 用 Go SDK 写的进程外插件，在 .NET 宿主里服务一个操作 | Go 工具链 |
| `CacheGoPlugin` | 远程 Provider | 用 Go 实现 `caching.distributed`，经 `Caching.Remote` 适配器通过缓存一致性套件 | Go 工具链 |
| `EchoWasmPlugin` | Wasm 插件 | 用 Rust 写的 Wasm 插件：操作、陷阱、忙等，验证沙箱上限与崩溃重建 | Rust 工具链（产物已提交，运行不需要） |

## SimpleApp

这是教程里引用最多的示例。它用 Standard 组合在零外部设施下运行，一个 `Program.cs` 里能看到框架的全部主线：装配特性、把操作契约映射到三种协议、发布 OpenAPI 与 `.proto`、接收 CloudEvents、在普通端点里读调用上下文。

```bash
dotnet run --project framework/samples/SimpleApp
```

HTTP 在 5000 端口，gRPC（h2c）在 5001 端口。配置里有三个开发用的 ApiKey 主体：`alice`（编辑者，租户 acme）、`bob`（只读，租户 acme）、`relay`（只持有投递事件的权限）。几条能直接感受框架行为的请求：

```bash
curl -s localhost:5000/whoami -H "Authorization: ApiKey dev-alice-key"
curl -s -X POST localhost:5000/orders -H "Authorization: ApiKey dev-alice-key" -H "Content-Type: application/json" -d '{"name":"widget","amount":3}'
curl -s -X POST localhost:5000/orders -H "Authorization: ApiKey dev-bob-key" -H "Content-Type: application/json" -d '{"name":"x","amount":1}'
curl -s localhost:5000/missing -H "Accept-Language: zh-CN"
curl -s localhost:5000/openapi.json
curl -s -X POST localhost:5000/mcp -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

第三条返回 403，错误码 `SECURITY_DENIED_004`：bob 只有 `orders.read`。第四条返回按 `Accept-Language` 翻译的 Problem Details。最后一条列出 `orders_create` 与 `orders_list` 两个 MCP tool，描述取自消息目录。

读教程时打开它的时机：

| 读到 | 看 |
| --- | --- |
| [特性与清单](/startup/features) | `Orders/OrdersFeature.cs`：清单里按能力 ID 声明依赖，`Configure` 里登记仓储、权限、消息处理器与操作 |
| [操作契约](/web/operations) | `Orders/CreateOrder.cs`、`Orders/ListOrders.cs`：一份契约三种协议 |
| [领域模型](/persistence/domain) | `Orders/Order.cs`：接口组合的聚合根，带领域事件缓冲 |
| [领域事件与发件箱](/messaging/domain-events) | `Orders/OrderPlacedHandler.cs` 与 `Program.cs` 里的 `OutboxFeature` |
| [全球化和本地化](/common/localization) | `SampleMessages.cs` 与 `Localization/*.json`：十种区域各一个文件 |
| [JSON 序列化与 Native AOT](/common/serialization) | `SampleJsonContext.cs` 与项目文件里的 `PublishAot` |

## SimpleCli

同一份契约，换一个宿主。它不用 ASP.NET Core，用命令行宿主把操作变成命令，再加上 MCP 的 stdio 传输：

```bash
dotnet run --project framework/samples/SimpleCli -- help
dotnet run --project framework/samples/SimpleCli -- operations
dotnet run --project framework/samples/SimpleCli -- greetings create --name Ada --times 2
dotnet run --project framework/samples/SimpleCli -- invoke greetings.create --json '{"name":"Ada","times":99}'
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | dotnet run --project framework/samples/SimpleCli -- mcp
```

第四条的 `times` 超出契约范围，stderr 输出一行 Problem JSON，退出码为 2。对应章节是[命令行宿主](/protocols/command-line)与 [MCP](/protocols/mcp)。

## KubernetesApp

Full 组合在 Kubernetes 上的落地。本地运行时没有 API Server 环境变量，程序退回进程内领导选举：

```bash
dotnet run --project framework/samples/KubernetesApp
curl -s localhost:5100/leader
curl -s localhost:5100/config
```

集群里的四项验证（探针、优雅停机、热更新、Lease 领导选举）写在示例自己的 README 与[部署到 Kubernetes](/deployment/kubernetes)一章。`deploy/` 目录由 `xihan pack` 从 `xihan.app.json` 生成并提交，测试保证产物与清单不漂移。

## 插件示例

五个插件示例覆盖三种隔离级别和两种语言的 SDK：

- `SamplePlugin` 是进程内插件，宿主用可收集的 `AssemblyLoadContext` 加载它。
- `EchoPlugin` 是 .NET 写的进程外插件子进程，不引用任何框架包，只按 `xihan.plugin.v1` 协议与宿主通信；它故意提供一个会崩溃的操作，用来验证宿主进入 Degraded 而不退出。
- `EchoGoPlugin` 用 [framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go) 写同样的插件，证明规范不是 .NET 形状的。
- `CacheGoPlugin` 更进一步：Go 实现的分布式缓存经 `Caching.Remote` 适配器接进 .NET 应用，并通过与 Redis Provider 相同的一致性套件。
- `EchoWasmPlugin` 是 Rust 写的 Wasm 插件，编译产物 `plugin/echo.wasm` 已提交，运行不需要 Rust。

Go 示例由对应测试项目在运行时 `go build`，本机没有 Go 时测试跳过。读[插件](/plugins/overview)一章时打开它们。

## 从源码运行示例的准备

示例以项目引用的方式引用 `framework/src` 下的包，所以要先克隆仓库并初始化规范子模块：

```bash
git clone --branch dev https://github.com/XiHanFun/XiHan.Framework.git
cd XiHan.Framework
git submodule update --init framework/spec
dotnet restore framework/XiHan.Framework.slnx
```

## 下一步

- [环境与安装](/getting-started/installation)：在自己的项目里引用框架
- [第一个应用](/getting-started/first-app)：十五分钟写出一个可运行的操作
