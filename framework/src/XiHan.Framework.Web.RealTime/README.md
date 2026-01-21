# XiHan.Framework.Web.RealTime

曦寒框架 Web 实时通信模块，基于 SignalR 实现实时双向通信功能。

## 功能特性

- ✅ SignalR Hub 基类封装
- ✅ 连接管理器（支持用户连接映射）
- ✅ 实时通知服务
- ✅ 用户身份验证集成
- ✅ 组播消息支持
- ✅ 在线状态管理
- ✅ 灵活的配置选项

## 快速开始

### 1. 添加模块依赖

在你的模块中添加 `XiHanWebRealTimeModule` 依赖：

```csharp
[DependsOn(typeof(XiHanWebRealTimeModule))]
public class YourModule : XiHanModule
{
}
```

### 2. 配置 SignalR

在 `appsettings.json` 中配置：

```json
{
  "XiHan": {
    "SignalR": {
      "EnableDetailedErrors": false,
      "KeepAliveInterval": "00:00:15",
      "ClientTimeoutInterval": "00:00:30",
      "HandshakeTimeout": "00:00:15",
      "MaximumReceiveMessageSize": 32768,
      "StreamBufferCapacity": 10,
      "MaximumParallelInvocationsPerClient": 1,
      "EnableConnectionMetrics": true
    }
  }
}
```

### 3. 创建自定义 Hub

```csharp
public class ChatHub : XiHanHub
{
    public ChatHub(IConnectionManager connectionManager) 
        : base(connectionManager)
    {
    }

    public async Task SendMessage(string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", UserId, message);
    }
}
```

### 4. 映射 Hub 端点

在 `Program.cs` 或 `Startup.cs` 中：

```csharp
app.MapXiHanHub<ChatHub>("/hubs/chat");
// 或使用配置
app.MapXiHanHub<NotificationHub>("/hubs/notification", options =>
{
    options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
});
```

## 核心组件

### XiHanHub 基类

所有 Hub 应继承自 `XiHanHub`，提供：

- `ConnectionId`: 当前连接 ID
- `UserId`: 当前用户 ID（从 Claims 中获取）
- `UserName`: 当前用户名
- `ConnectionManager`: 连接管理器实例

自动处理连接/断开事件，维护用户连接映射。

### 连接管理器 (IConnectionManager)

管理用户连接状态：

```csharp
// 注入服务
public class MyService
{
    private readonly IConnectionManager _connectionManager;
    
    public MyService(IConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }
    
    public async Task DoSomething()
    {
        // 获取用户的所有连接
        var connections = await _connectionManager.GetConnectionsAsync("userId");
        
        // 检查用户是否在线
        var isOnline = await _connectionManager.IsUserOnlineAsync("userId");
        
        // 获取在线用户列表
        var onlineUsers = await _connectionManager.GetOnlineUsersAsync();
        
        // 获取在线用户数量
        var count = await _connectionManager.GetOnlineUserCountAsync();
    }
}
```

### 实时通知服务 (IRealtimeNotificationService)

发送实时消息：

```csharp
// 注入服务
public class NotificationService
{
    private readonly RealtimeNotificationService<NotificationHub> _notificationService;
    
    public NotificationService(RealtimeNotificationService<NotificationHub> notificationService)
    {
        _notificationService = notificationService;
    }
    
    public async Task SendNotification()
    {
        // 向指定用户发送
        await _notificationService.SendToUserAsync("userId", "ReceiveNotification", 
            new { Title = "标题", Content = "内容" });
        
        // 向多个用户发送
        await _notificationService.SendToUsersAsync(
            new[] { "user1", "user2" }, 
            "ReceiveNotification", 
            new { Title = "标题", Content = "内容" });
        
        // 向所有用户发送
        await _notificationService.SendToAllAsync("ReceiveNotification", 
            new { Title = "标题", Content = "内容" });
        
        // 向组发送
        await _notificationService.SendToGroupAsync("groupName", "ReceiveNotification", 
            new { Title = "标题", Content = "内容" });
        
        // 添加用户到组
        await _notificationService.AddToGroupAsync("userId", "groupName");
        
        // 从组中移除用户
        await _notificationService.RemoveFromGroupAsync("userId", "groupName");
    }
}
```

## 客户端集成

### JavaScript 客户端

```javascript
// 安装: npm install @microsoft/signalr

import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/notification", {
        accessTokenFactory: () => getAccessToken()
    })
    .withAutomaticReconnect()
    .build();

// 监听消息
connection.on("ReceiveMessage", (userId, message) => {
    console.log(`${userId}: ${message}`);
});

// 启动连接
await connection.start();

// 发送消息
await connection.invoke("SendMessage", "Hello!");
```

### C# 客户端

```csharp
// 安装: Microsoft.AspNetCore.SignalR.Client

var connection = new HubConnectionBuilder()
    .WithUrl("https://localhost:5001/hubs/notification", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult(accessToken);
    })
    .WithAutomaticReconnect()
    .Build();

// 监听消息
connection.On<string, string>("ReceiveMessage", (userId, message) =>
{
    Console.WriteLine($"{userId}: {message}");
});

// 启动连接
await connection.StartAsync();

// 发送消息
await connection.InvokeAsync("SendMessage", "Hello!");
```

## 身份验证

模块自动集成 JWT 身份验证，从 Token 中提取用户信息：

```csharp
// 服务端配置
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && 
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
```

## 高级配置

### 自定义用户 ID 提供器

```csharp
public class CustomUserIdProvider : XiHanUserIdProvider
{
    public override string? GetUserId(HubConnectionContext connection)
    {
        // 自定义用户 ID 获取逻辑
        return connection.User?.FindFirst("custom_claim")?.Value 
               ?? base.GetUserId(connection);
    }
}

// 注册服务
services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();
```

### 扩展 Hub 基类

```csharp
public abstract class MyHubBase : XiHanHub
{
    protected ILogger Logger { get; }
    
    protected MyHubBase(
        IConnectionManager connectionManager,
        ILogger logger) 
        : base(connectionManager)
    {
        Logger = logger;
    }
    
    public override async Task OnConnectedAsync()
    {
        Logger.LogInformation($"User {UserId} connected");
        await base.OnConnectedAsync();
    }
}
```

## 示例场景

### 1. 实时聊天

```csharp
public class ChatHub : XiHanHub
{
    public ChatHub(IConnectionManager connectionManager) 
        : base(connectionManager)
    {
    }

    public async Task SendPrivateMessage(string toUserId, string message)
    {
        var connections = await ConnectionManager.GetConnectionsAsync(toUserId);
        if (connections.Count > 0)
        {
            await Clients.Clients(connections)
                .SendAsync("ReceivePrivateMessage", UserId, message);
        }
    }
}
```

### 2. 实时通知

```csharp
public class NotificationHub : XiHanHub
{
    private readonly RealtimeNotificationService<NotificationHub> _notificationService;
    
    public NotificationHub(
        IConnectionManager connectionManager,
        RealtimeNotificationService<NotificationHub> notificationService) 
        : base(connectionManager)
    {
        _notificationService = notificationService;
    }

    public async Task MarkAsRead(string notificationId)
    {
        // 处理标记为已读的逻辑
        await Clients.Caller.SendAsync("NotificationRead", notificationId);
    }
}
```

### 3. 实时数据推送

```csharp
public class DataHub : XiHanHub
{
    public DataHub(IConnectionManager connectionManager) 
        : base(connectionManager)
    {
    }

    public async Task SubscribeToDataFeed(string feedName)
    {
        await Groups.AddToGroupAsync(ConnectionId!, feedName);
    }

    public async Task UnsubscribeFromDataFeed(string feedName)
    {
        await Groups.RemoveFromGroupAsync(ConnectionId!, feedName);
    }
}

// 后台服务推送数据
public class DataPushService : BackgroundService
{
    private readonly IHubContext<DataHub> _hubContext;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var data = await GetLatestData();
            await _hubContext.Clients.Group("stockPrices")
                .SendAsync("UpdateData", data, stoppingToken);
            await Task.Delay(1000, stoppingToken);
        }
    }
}
```

## 性能优化建议

1. **使用消息包协议**: 比 JSON 更高效
2. **启用压缩**: 减少带宽使用
3. **合理设置超时**: 根据实际需求调整
4. **使用 Redis 扩展**: 支持多服务器部署
5. **限制消息大小**: 防止过大消息影响性能

## 依赖项

- Microsoft.AspNetCore.SignalR.Core
- XiHan.Framework.Web.Core
- XiHan.Framework.Authentication

## 相关资源

- [SignalR 官方文档](https://docs.microsoft.com/aspnet/core/signalr/)
- [曦寒框架文档](https://github.com/zhaifanhua/XiHan.Framework)
