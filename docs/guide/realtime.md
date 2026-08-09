# 实时通信

服务端要主动把消息推到浏览器——通知、聊天、任务进度、强制下线——走 `XiHan.Framework.Web.RealTime` 提供的 SignalR 集成。本章讲怎么定义 Hub、怎么从业务代码推送、连接是怎么被找到的，以及一条最容易翻车的载荷约定。

完整 API 清单与全部配置项见 [Web.RealTime 包](../packages/web-realtime)。

## 什么时候用

| 需求 | 选择 |
| --- | --- |
| 服务端有事就通知前端，前端不知道什么时候会有 | Hub 推送 |
| 前端定期刷新一份数据，能接受几秒延迟 | 普通 HTTP 轮询，不必上 Hub |
| 一次长耗时操作要报进度 | Hub 推送 `TaskProgress` |
| 同账号多端状态同步（偏好、被踢下线） | Hub 推送，按用户点发 |

Hub 是有状态长连接，会占住连接数、跨实例还要额外考虑路由。能用请求-响应解决的事就别开 Hub。

## 启用

```bash
dotnet add package XiHan.Framework.Web.RealTime
```

```csharp
[DependsOn(typeof(XiHanWebRealTimeModule))]
public class MyAppModule : XiHanModule { }
```

`XiHanWebRealTimeModule` 依赖 `XiHanWebCoreModule` 与 `XiHanAuthenticationModule`，在 `ConfigureServices` 里调用 `AddXiHanSignalRWithJson(config)`，一次性完成：

| 动作 | 说明 |
| --- | --- |
| `AddSignalR().AddJsonProtocol(...)` | JSON 协议：`PropertyNamingPolicy = CamelCase`、`WriteIndented = false` |
| 绑定 `XiHanSignalROptions` | 读配置节 `XiHan:Web:RealTime:SignalR` |
| 桥接到 `HubOptions` | 保活/超时/握手/消息大小等运行时从 DI 取值 |
| 注册 `IConnectionManager` → `ConnectionManager` | `Singleton`，进程内连接表 |
| 注册 `IUserIdProvider` / `IXiHanUserIdProvider` → `XiHanUserIdProvider` | `Singleton` |
| 注册 `IRealtimeNotificationService<>` → `RealtimeNotificationService<>` | `Scoped` |

::: warning 端点不会自动映射
模块只注册服务，**不映射任何 Hub 端点**——包括内置示例 `NotificationHub`。路径由你在应用侧决定。
:::

## 定义 Hub

继承 `XiHanHub`，构造函数接 `IConnectionManager` 传给基类：

```csharp
using XiHan.Framework.Web.RealTime.Attributes;
using XiHan.Framework.Web.RealTime.Hubs;
using XiHan.Framework.Web.RealTime.Services;

[AuthorizeHub]
public class ChatHub : XiHanHub
{
    public ChatHub(IConnectionManager connectionManager) : base(connectionManager)
    {
    }

    // 参数一律用 string 接收，原因见「载荷不走 MVC 的 JSON 管道」
    public Task JoinConversation(string conversationId)
    {
        return Groups.AddToGroupAsync(ConnectionId!, $"conversation:{conversationId}");
    }
}
```

`XiHanHub` 在基类里提供三个只读属性和一个受保护成员：

| 成员 | 来源 |
| --- | --- |
| `ConnectionId` | `Context.ConnectionId` |
| `UserId` | `ClaimTypes.NameIdentifier` claim |
| `UserName` | `ClaimTypes.Name` claim |
| `ConnectionManager`（`protected`） | 注入的 `IConnectionManager` |

`AuthorizeHubAttribute` 继承自 `AuthorizeAttribute`，可打在类或方法上，可重复标注，也可带策略名：`[AuthorizeHub("MyPolicy")]`。

### 映射端点

在模块的 `OnApplicationInitialization` 里映射：

```csharp
public override void OnApplicationInitialization(ApplicationInitializationContext context)
{
    var app = context.GetApplicationBuilder();
    app.UseEndpoints(endpoints =>
    {
        endpoints.MapXiHanHub<ChatHub>("/hubs/chat");
        endpoints.MapXiHanHub<NotificationHub>(SignalRConstants.HubPaths.Notification);
    });
}
```

`MapXiHanHub<THub>` 有两个重载，第二个接 `Action<HttpConnectionDispatcherOptions>`，用来单独调传输方式、缓冲区等。泛型约束是 `where THub : XiHanHub`——直接继承 `Hub` 的类型用不了这个扩展。

`SignalRConstants.HubPaths` 里预置了三个路径常量：`Notification`（`/hubs/notification`）、`Chat`（`/hubs/chat`）、`Data`（`/hubs/data`）。

## 从业务代码推送

业务侧不碰 Hub 实例，注入按 Hub 泛型化的 `IRealtimeNotificationService<THub>`：

```csharp
public class OrderNotifier(IRealtimeNotificationService<ChatHub> realtime)
{
    public Task NotifyCreatedAsync(long userId, long orderId)
    {
        // long 显式转 string，见下节
        return realtime.SendToUserAsync(
            userId.ToString(),
            SignalRConstants.ClientMethods.ReceiveNotification,
            new { orderId = orderId.ToString(), title = "订单已创建" });
    }
}
```

泛型参数决定推到哪条 Hub，推错 Hub 是编译期错误。六个方法：

| 方法 | 目标 |
| --- | --- |
| `SendToUserAsync(userId, method, args)` | 单个用户的全部连接 |
| `SendToUsersAsync(userIds, method, args)` | 一批用户的全部连接 |
| `SendToGroupAsync(groupName, method, args)` | SignalR 组 |
| `SendToAllAsync(method, args)` | 全体连接 |
| `AddToGroupAsync(userId, groupName)` | 把该用户当前的所有连接加进组 |
| `RemoveFromGroupAsync(userId, groupName)` | 反向操作 |

::: warning `args` 是 `params object[]`，没有 `CancellationToken` 重载
把 `CancellationToken` 顺手当最后一个参数传进去，它会被当成一条业务载荷序列化发给客户端。要传就只传真正的载荷。
:::

::: tip 服务是 `Scoped`
从 `BackgroundService`、`IHostedService` 这类单例里用它，需要先 `CreateScope()`；或者直接注入 `IHubContext<THub>` 自己发。
:::

## 关键机制

### 连接是怎么被登记的

`XiHanHub.OnConnectedAsync` / `OnDisconnectedAsync` 已经重写好：**当 `ConnectionId` 与 `UserId` 都非空时**，调 `IConnectionManager` 登记 / 注销「用户 ID → 连接 ID 集合」。

::: danger 匿名连接推不到
`UserId` 取的是 `NameIdentifier` claim。匿名连上来的客户端拿不到这个 claim，**不会进连接表**，后续 `SendToUserAsync` 永远找不到它。要点对点推送，Hub 就得打 `[AuthorizeHub]`，且令牌里必须有 `NameIdentifier`。
:::

如果你在派生 Hub 里重写了这两个方法，**必须调 `base.OnConnectedAsync()` / `base.OnDisconnectedAsync(exception)`**，否则登记逻辑被整条跳过。

### 哪些方法依赖连接表

这是多实例部署时唯一需要记住的分界：

| 方法 | 是否查 `IConnectionManager` |
| --- | --- |
| `SendToUserAsync` / `SendToUsersAsync` | 是 |
| `AddToGroupAsync` / `RemoveFromGroupAsync` | 是 |
| `SendToGroupAsync` / `SendToAllAsync` | 否，直接走 SignalR 的 `Group` / `All` |

默认的 `ConnectionManager` 是**进程内**实现（`ConcurrentDictionary` + 锁，用户连接清空后自动删条目）。多实例下：

- 用户连在 A 实例，B 实例上的 `SendToUserAsync` 查不到连接，静默什么都不发；
- 接了 SignalR backplane 之后，`SendToGroupAsync` / `SendToAllAsync` 能跨实例，`SendToUserAsync` 仍然不行——它依赖的是本进程的连接表。

两条出路：

1. 直接注入 `IHubContext<THub>` 用 `Clients.User(userId)`——它走 `IUserIdProvider`，而 `XiHanUserIdProvider` 取的正是 `NameIdentifier`（缺失时回退 `Name`），与 `SendToUserAsync` 的用户键一致；
2. 用共享存储实现 `IConnectionManager`，在模块注册之后覆盖那条 `Singleton` 注册。

### 组只对「调用那一刻已存在的连接」生效

`AddToGroupAsync(userId, groupName)` 会遍历该用户当时的连接逐个入组。用户之后新开的标签页不会自动进组。需要「用户级常驻组」就在 `OnConnectedAsync` 里补入组。

### 载荷不走 MVC 的 JSON 管道

::: danger 这是最容易踩的一条
SignalR 的 JSON 协议在框架里**只**配了两件事：camelCase 命名、不缩进。MVC 那套 `long` → 字符串、枚举 → 成员名、按 `X-Timezone` 换算时间的转换器，全都挂在 MVC 的 JSON 选项上，**对 Hub 载荷不生效**。

后果：直接推一个含雪花 ID 的对象，前端拿到的是会丢精度的 Number；推枚举拿到的是数字。
:::

两条约定：

1. **推送前手动投影**——ID `ToString()`，枚举转成约定字符串，时间自己决定格式；
2. **Hub 方法参数用 `string` 接收 ID**，服务端自行解析。内置示例 `NotificationHub` 的方法参数全是 `string`，就是这个原因。

写新推送时先问一句「这个 payload 里有 `long` 或枚举吗」，有就先投影。

框架提供的 `NotificationMessage` 模型（`Id` / `SenderId` / `ReceiverId` / `Type` / `Title` / `Content` / `Data` / `CreatedTime` / `IsRead`）是个现成的载荷形状，但它的 `Data` 是 `object?`——用它也一样要自己投影里面的内容。

### 方法名用常量

客户端方法名是字符串，`SignalRConstants` 里集中了内置的一批：

- `ClientMethods`：`ReceiveMessage`、`ReceiveNotification`、`UserJoined`、`UserLeft`、`Connected`、`Disconnected`、`Error`、`ForceLogout`、`TaskProgress`、`UserSettingChanged`
- `ServerMethods`：`SendMessage`、`SendMessageToUser`、`SendMessageToAll`、`JoinGroup`、`LeaveGroup`、`SendMessageToGroup`、`GetOnlineUserCount`、`IsUserOnline`
- `Groups`：`Admin`、`Users`、`Notifications`

其中 `TaskProgress` 与 `UserSettingChanged` 在源码里带载荷约定注释：前者是 `taskId` / `label` / `detail` / `state`（`loading` \| `success` \| `error` \| `info`）/ `progress`（0-100）/ `link`；后者是 `scene` / `settingKey` / `settingValue` / `sourceClientId`。业务自己新增的方法名，也建一个常量类，别内联字符串。

### 在线状态

`IConnectionManager` 顺带就是在线状态的数据源：

```csharp
await connectionManager.IsUserOnlineAsync(userId);     // 该用户是否有活跃连接
await connectionManager.GetOnlineUserCountAsync();     // 在线用户数（不是连接数）
await connectionManager.GetOnlineUsersAsync();         // 在线用户 ID 列表
await connectionManager.GetConnectionsAsync(userId);   // 该用户的连接 ID 列表
```

注意 `GetOnlineUserCountAsync` 数的是**用户**，一个用户开三个标签页仍然只算一个。同样受进程内实现的限制。

## Hub 路径前缀

Hub 路径挂在哪里不是随便定的，Web.Api 侧有两处按前缀判断的逻辑，默认都是 `/hubs`：

| 配置键 | 默认 | 作用 |
| --- | --- | --- |
| `XiHan:Web:Api:Auth:SignalRHubPathPrefix` | `/hubs` | 命中该前缀时，才从 query string 的 `access_token` 取 JWT |
| `XiHan:Web:SessionState:SignalRHubPathPrefix` | `/hubs` | 命中该前缀时，会话闸门中间件整体跳过 |

前一条是必需的：WebSocket / SSE 握手带不了 `Authorization` 头，令牌只能走 query，连接地址形如 `/hubs/chat?access_token=<jwt>`。后一条是为了避免长连接被 401 / 423 直接切断——客户端会陷入重连死循环，而且这条连接本身还要用来接收「你被踢下线了」的推送；Hub 的方法级拦截交给 `IHubFilter` 做。

::: warning Hub 挂到前缀之外要两处一起改
把 Hub 映射到 `/realtime/chat` 这种路径，上面两个配置都得跟着改，否则表现是「连不上（401）」或者「连上了但过一会被闸门切断」。
:::

## 异常过滤器

`HubExceptionFilter` 实现 `IHubFilter`，统一记录方法调用 / 连接 / 断开三处的异常，并把方法异常替换成脱敏后的 `HubException`（只回 `调用方法 {方法名} 时发生错误`，不外泄堆栈）。

它**不是自动生效的**，需要自己挂：

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    // 全局挂到所有 Hub
    context.Services.Configure<HubOptions>(options => options.AddFilter<HubExceptionFilter>());
}
```

## 配置

配置节 `XiHan:Web:RealTime:SignalR`（常量 `XiHanSignalROptions.SectionName`），值会被桥接进 SignalR 的 `HubOptions`：

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
          "StreamBufferCapacity": 10,
          "MaximumParallelInvocationsPerClient": 1
        }
      }
    }
  }
}
```

几个要拿捏的：

| 键 | 默认 | 拿捏点 |
| --- | --- | --- |
| `EnableDetailedErrors` | `false` | 打开会把服务端异常原文发给客户端，**生产别开** |
| `KeepAliveInterval` | `15s` | 服务端心跳间隔 |
| `ClientTimeoutInterval` | `30s` | 保持在保活间隔的约 **2 倍**，调小容易误判掉线 |
| `MaximumReceiveMessageSize` | `32768`（32KB） | 客户端→服务端单条消息上限，富文本/附件元数据可能顶到 |
| `MaximumParallelInvocationsPerClient` | `1` | 默认串行，同一连接上的慢方法会阻塞后续调用；调大前先确认 Hub 方法是并发安全的 |

`XiHanSignalROptions` 里还有一个 `EnableConnectionMetrics`（默认 `true`），当前只是一个选项位，没有代码读取它。

完整字段表见 [Web.RealTime 包](../packages/web-realtime)。

## 常见问题

| 现象 | 原因 | 处理 |
| --- | --- | --- |
| 握手直接 401 | Hub 路径不在 `SignalRHubPathPrefix` 下，或客户端没带 `access_token` query | 把 Hub 挂到 `/hubs` 下，或同步改两处前缀配置 |
| 连上了，但 `SendToUserAsync` 收不到 | 连接是匿名的，没进连接表 | Hub 打 `[AuthorizeHub]`，确认令牌带 `NameIdentifier` |
| 重写了 `OnConnectedAsync` 后点对点推送全失效 | 漏调 `base.OnConnectedAsync()` | 补上基类调用 |
| 前端拿到的 ID 与后端对不上 | 载荷里的 `long` 没投影，JS Number 溢出 | 推送前 `ToString()` |
| 前端拿到的枚举是数字 | Hub 载荷不走 MVC 的枚举转换器 | 推送前转成约定字符串 |
| 多实例部署后点对点推送时灵时不灵 | 连接表是进程内的 | 改用 `Clients.User(userId)`，或替换 `IConnectionManager` |
| 同一份消息收到两遍 | `SendToUsersAsync` 的入参里有重复 `userId`，连接会被重复收集 | 调用前对 `userIds` 去重 |
| `AddToGroupAsync` 之后新开的标签页收不到组消息 | 只对调用时已存在的连接生效 | 在 `OnConnectedAsync` 里补入组 |
| Hub 异常没有统一日志 | `HubExceptionFilter` 没注册 | 用 `HubOptions.AddFilter` 挂上 |
| 消息能到但延迟高、CPU 偏高 | 反向代理没放行 `Upgrade` / `Connection`，退回 SSE / LongPolling | 改反代配置，确认协商结果是 WebSockets |
| 客户端反复掉线重连 | `ClientTimeoutInterval` 相对 `KeepAliveInterval` 太小 | 恢复到约 2 倍关系 |

## 下一步

- [Web 应用开发](./web)：中间件管道，以及 MVC 侧的 JSON 序列化约定（与 Hub 的差异就在这）
- [认证与授权](./authentication)：JWT 与 claim 来源，Hub 的 `UserId` 从这里来
- [配置](./configuration)：配置节总览与绑定方式
- [Web.RealTime 包](../packages/web-realtime)：完整 API 与全部配置项
- [Web.Api 包](../packages/web-api)：`SignalRHubPathPrefix` 与会话闸门所在
