# XiHan.Framework.Web.RealTime

> 实时通信：基于 SignalR 的服务端集成，提供 Hub 基类、连接管理、按用户/组/全体的泛型通知服务与统一 JSON 协议。

- **NuGet**：`XiHan.Framework.Web.RealTime`
- **模块类**：`XiHanWebRealTimeModule`
- **所在层**：Web 层
- **关键依赖**：ASP.NET Core 内置 SignalR（`Microsoft.AspNetCore.SignalR`，随 Web SDK / `FrameworkReference` 提供，无独立第三方包引用）

## 概述

XiHan.Framework.Web.RealTime 把 ASP.NET Core SignalR 接入框架，用于服务器主动向客户端推送消息（通知、聊天、任务进度、在线状态、多端设置同步等）。它提供：一个 Hub 基类 `XiHanHub`（在连接/断开时自动维护用户与连接 ID 的映射）、一个进程内连接管理器 `IConnectionManager`、一个泛型实时通知服务 `` IRealtimeNotificationService<THub> ``（支持发给单个用户、用户列表、指定组或全体），以及一个自定义用户标识提供器；同时统一配置 SignalR 的 JSON 协议与保活/超时等 `HubOptions`。

## 何时使用

- 需要服务端向前端**主动推送**：站内通知、消息中心、聊天、任务进度、实时状态、强制下线。
- 需要按「用户 / 用户列表 / 组 / 全体」定向推送，并管理用户与连接的映射关系。
- 需要在 SignalR 场景下用 JWT 鉴权（WebSocket/SSE 无法带 `Authorization` 头，token 经 query string 的 `access_token` 传入，由 [Web.Api](./web-api) 侧支持）。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Web.RealTime
```

```csharp
[DependsOn(typeof(XiHanWebRealTimeModule))]
public class MyModule : XiHanModule { }
```

模块 `ConfigureServices` 调用 `AddXiHanSignalRWithJson(config)`：注册 SignalR（带 JSON 协议）、连接管理器、用户 ID 提供器与泛型实时通知服务，并从配置节 `XiHan:Web:RealTime:SignalR` 绑定选项、桥接到 `HubOptions`。Hub 端点映射需在应用侧用 `MapXiHanHub<THub>(pattern)` 完成。

## 工作原理

- **连接映射自动维护**：`XiHanHub.OnConnectedAsync`/`OnDisconnectedAsync` 在有 `ConnectionId` 与 `UserId`（取自 `ClaimTypes.NameIdentifier`）时，调用 `IConnectionManager` 登记/注销「用户 ID → 连接 ID 集合」。
- **默认连接管理器为进程内**：`ConnectionManager` 用 `ConcurrentDictionary<string, HashSet<string>>` + 锁保存映射，用户无连接时自动移除条目。**单进程内存实现**，多实例/横向扩展场景需自行替换（见扩展点）。
- **通知路由**：`` RealtimeNotificationService<THub> `` 经 `IHubContext<THub>` 推送——发用户时先由连接管理器查出该用户的所有连接再 `Clients.Clients(...)`，发组/全体直接走 SignalR 的 `Group` / `All`，均用 `SendCoreAsync(method, args)`。
- **选项桥接**：`AddXiHanSignalRWithJson` 用 `AddOptions<HubOptions>().Configure<IOptions<XiHanSignalROptions>>(...)` 延迟把 `XiHanSignalROptions` 的值映射进 SignalR 的 `HubOptions`（运行时从 DI 读取）。

## 核心能力

- **Hub 基类**：`XiHanHub`（`abstract`，实现 `IXiHanHub`）在连接/断开自动登记连接，暴露 `ConnectionId`/`UserId`/`UserName`（分别取自 `Context.ConnectionId`、`NameIdentifier`、`Name` Claim），并暴露受保护的 `ConnectionManager`。
- **连接管理**：`IConnectionManager` 维护用户到连接映射并提供在线查询；`IXiHanUserIdProvider`（实现 SignalR 的 `IUserIdProvider`）以 `NameIdentifier`（回退 `Name`）作为 SignalR 用户标识。
- **实时通知服务**：`` IRealtimeNotificationService<THub> `` 提供用户/列表/组/全体推送与分组增删（`Scoped` 生命周期）。
- **JSON 协议配置**：`AddJsonProtocol` 设 `PayloadSerializerOptions` 为 CamelCase 命名、非缩进输出。
- **Hub 选项桥接**：`XiHanSignalROptions`（保活/超时/握手/最大消息大小/并发调用数等）映射到 `HubOptions`。
- **鉴权与异常过滤**：`AuthorizeHubAttribute`（继承 `AuthorizeAttribute`）用于 Hub 授权；`HubExceptionFilter`（`IHubFilter`）统一记录方法调用/连接/断开异常，方法异常对客户端抛出脱敏后的 `HubException`。
- **内置示例与常量**：`NotificationHub`（示例 Hub）、`NotificationMessage`（消息模型）、`SignalRConstants`（客户端/服务端方法名、组名、Hub 路径约定）。

## 序列化约定（重要）

SignalR 的 JSON 协议**仅**配置了 CamelCase 命名（且 `WriteIndented=false`），**未注册** `long → string`、枚举等自动转换器。因此：

- 推送的载荷需在**应用侧手动投影**为客户端可安全消费的形状——例如把 `long` ID 显式转成 `string`，把枚举转成约定字符串，避免 JS 端大整数精度丢失与枚举歧义。
- Hub 方法参数也应以 `string` 接收（内置 `NotificationHub` 的示例方法即全部收 `string`）。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanHub` / `IXiHanHub` | Hub 抽象基类与接口，自动维护连接映射，暴露 `ConnectionId`/`UserId`/`UserName` |
| `IConnectionManager` / `ConnectionManager` | 用户与连接映射管理（默认进程内内存实现，`Singleton`） |
| `` IRealtimeNotificationService<THub> `` | 泛型通知服务：`SendToUserAsync` / `SendToUsersAsync` / `SendToGroupAsync` / `SendToAllAsync` / `AddToGroupAsync` / `RemoveFromGroupAsync`（`Scoped`） |
| `IXiHanUserIdProvider` / `XiHanUserIdProvider` | SignalR 用户标识提供器（`NameIdentifier` 回退 `Name`，`Singleton`） |
| `XiHanSignalROptions` | SignalR 选项（配置节 `XiHan:Web:RealTime:SignalR`） |
| `AuthorizeHubAttribute` | Hub 授权特性（继承 `AuthorizeAttribute`，类/方法可用，可带策略名） |
| `HubExceptionFilter` | `IHubFilter`，统一记录并脱敏 Hub 异常 |
| `NotificationHub` / `NotificationMessage` | 内置示例 Hub 与通知消息模型 |
| `SignalRConstants` | 客户端方法名（`ReceiveMessage`/`ReceiveNotification`/`TaskProgress`/`UserSettingChanged`/`ForceLogout`…）、服务端方法名、组名、Hub 路径常量 |
| `MapXiHanHub<THub>(pattern[, configure])` | 端点路由扩展，映射继承 `XiHanHub` 的 Hub |
| `AddXiHanSignalR(...)` / `AddXiHanSignalRWithJson(...)` | 服务集合扩展（后者带 CamelCase JSON 协议，为模块默认使用） |

## 配置

配置节 `XiHan:Web:RealTime:SignalR`（`XiHanSignalROptions.SectionName`）：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `EnableDetailedErrors` | `bool` | `false` | 是否向客户端返回详细错误 |
| `KeepAliveInterval` | `TimeSpan` | `15s` | 保活心跳间隔 |
| `ClientTimeoutInterval` | `TimeSpan` | `30s` | 客户端超时（应约为保活间隔的 2 倍） |
| `HandshakeTimeout` | `TimeSpan` | `15s` | 握手超时 |
| `MaximumReceiveMessageSize` | `long?` | `32768`（32KB） | 最大接收消息大小（字节） |
| `StreamBufferCapacity` | `int` | `10` | 流缓冲容量 |
| `MaximumParallelInvocationsPerClient` | `int` | `1` | 每客户端最大并行调用数 |
| `EnableConnectionMetrics` | `bool` | `true` | 是否启用连接指标（选项位） |

```json
{
  "XiHan": {
    "Web": {
      "RealTime": {
        "SignalR": {
          "EnableDetailedErrors": false,
          "KeepAliveInterval": "00:00:15",
          "ClientTimeoutInterval": "00:00:30",
          "HandshakeTimeout": "00:00:15",
          "MaximumReceiveMessageSize": 32768,
          "MaximumParallelInvocationsPerClient": 1
        }
      }
    }
  }
}
```

## 使用示例

**定义 Hub 并映射端点**（端点映射在应用侧）：

```csharp
public class ChatHub : XiHanHub
{
    public ChatHub(IConnectionManager connectionManager) : base(connectionManager) { }

    // 参数以 string 接收，避免大整数/枚举歧义
    public Task Send(string toUserId, string message) => ...;
}

// 应用侧端点映射
app.MapXiHanHub<ChatHub>("/hubs/chat");
```

**从业务代码推送**（注入泛型通知服务，载荷已手动投影为 string）：

```csharp
public class OrderNotifier(IRealtimeNotificationService<ChatHub> notifier)
{
    public Task NotifyAsync(long userId, long orderId) =>
        // long 显式转 string，防止前端精度丢失
        notifier.SendToUserAsync(
            userId.ToString(),
            SignalRConstants.ClientMethods.ReceiveNotification,
            new { orderId = orderId.ToString(), title = "订单已创建" });
}
```

**前端连接携带 token**（SignalR 走 query string 的 `access_token`）：连接 `/hubs/chat?access_token={jwt}`，由 [Web.Api](./web-api) 的 JWT `OnMessageReceived` 从 query 提取 token（仅对 `SignalRHubPathPrefix`，默认 `/hubs` 前缀生效）。

## 扩展点 / 自定义

- **替换连接管理器**：默认 `ConnectionManager` 是**单进程内存**实现。多实例/横向扩展时，应改用外部共享存储（如 Redis backplane / 分布式映射）并注册自定义 `IConnectionManager`（在模块注册之后覆盖 `Singleton` 注册）。
- **自定义用户标识**：继承 `XiHanUserIdProvider`（`GetUserId` 为 `virtual`）或另实现 `IXiHanUserIdProvider`，改用其它 Claim 作为 SignalR 用户键。
- **Hub 异常过滤**：`HubExceptionFilter` 是 `IHubFilter`，可全局或按 Hub 注册以统一异常记录与脱敏。

## 注意事项与最佳实践

- **载荷/参数手动 string 化**：无自动 `long→string`/枚举转换器，务必在应用侧投影载荷、Hub 参数收 `string`（详见「序列化约定」）。
- **连接管理器不跨进程**：默认实现仅进程内有效，负载均衡多实例下的在线状态/定向推送需要替换为分布式实现。
- **`ClientTimeoutInterval` 与 `KeepAliveInterval` 的比例**：客户端超时通常设为保活间隔的约 2 倍，避免误判掉线。
- **鉴权路径前缀**：token 从 query string 提取仅对 `XiHanWebAuthOptions.SignalRHubPathPrefix`（默认 `/hubs`）下的路径生效，Hub 路由应挂在该前缀下。

## 依赖模块

- 内部依赖：[XiHan.Framework.Web.Core](./web-core)、[XiHan.Framework.Authentication](./authentication)。
- 第三方核心：ASP.NET Core 内置 SignalR（`Microsoft.AspNetCore.SignalR`）。

## 相关模块

- [XiHan.Framework.Web.Api](./web-api) — 提供 JWT 认证，并支持从 query string 的 `access_token` 取 token（SignalR 场景）。
- [XiHan.Framework.Authentication](./authentication) — 身份认证与 Claim 来源。
