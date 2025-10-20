# XiHan.Framework.Http - 代理请求功能完整指南

## 概述

`XiHan.Framework.Http` 现已集成完整的代理请求功能，支持企业级应用的各种代理场景。

## 功能清单

### ✅ 已实现的核心功能

#### 1. 代理类型支持

- ✅ HTTP 代理
- ✅ HTTPS 代理
- ✅ SOCKS4 代理（.NET 6+ 原生支持）
- ✅ SOCKS4A 代理（.NET 6+ 原生支持）
- ✅ SOCKS5 代理（.NET 6+ 原生支持）

#### 2. 代理配置 (ProxyConfiguration)

- ✅ 主机地址和端口配置
- ✅ 代理类型选择
- ✅ 用户名/密码认证
- ✅ 优先级设置
- ✅ 最大并发连接数
- ✅ 超时配置
- ✅ 绕过地址列表
- ✅ 自定义标签

#### 3. 代理池管理 (ProxyPoolManager)

- ✅ 多代理管理
- ✅ 动态添加/移除代理
- ✅ 代理可用性检测
- ✅ 健康检查机制
- ✅ 代理状态监控

#### 4. 代理选择策略 (ProxySelectionStrategy)

- ✅ 轮询 (RoundRobin)
- ✅ 随机选择 (Random)
- ✅ 最少使用 (LeastUsed)
- ✅ 最快响应 (FastestResponse)
- ✅ 优先级 (Priority)

#### 5. 代理验证 (ProxyValidator)

- ✅ 单个代理验证
- ✅ 批量代理验证
- ✅ 验证超时控制
- ✅ 并发验证控制

#### 6. 代理统计 (ProxyStatistics)

- ✅ 总请求次数
- ✅ 成功/失败次数
- ✅ 成功率统计
- ✅ 平均响应时间
- ✅ 当前连接数
- ✅ 连续失败次数
- ✅ 最后使用时间

#### 7. 健康检查服务

- ✅ 后台定时健康检查
- ✅ 自动标记不可用代理
- ✅ 自动恢复机制
- ✅ 失败阈值控制

#### 8. 请求集成

- ✅ 指定代理请求
- ✅ 代理池自动选择
- ✅ 代理结果记录
- ✅ 代理失败处理

## 架构设计

```
┌─────────────────────────────────────────────────────────┐
│                   IAdvancedHttpService                   │
│  (支持所有HTTP方法，集成代理功能)                           │
└─────────────────────────────────────────────────────────┘
                           │
                           │ 使用
                           ▼
┌─────────────────────────────────────────────────────────┐
│                  IProxyPoolManager                       │
│  • 代理池管理                                             │
│  • 代理选择策略                                           │
│  • 统计信息收集                                           │
└─────────────────────────────────────────────────────────┘
                           │
          ┌────────────────┼────────────────┐
          │                │                │
          ▼                ▼                ▼
  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
  │ProxyValidator│  │ProxyStatistics│ │HealthCheck  │
  │  代理验证    │  │   统计信息    │  │  健康检查   │
  └──────────────┘  └──────────────┘  └──────────────┘
```

## 核心类说明

### 1. ProxyConfiguration

代理配置类，包含代理的所有配置信息。

```csharp
public class ProxyConfiguration
{
    public string Host { get; set; }              // 代理主机
    public int Port { get; set; }                 // 代理端口
    public ProxyType Type { get; set; }           // 代理类型
    public string? Username { get; set; }         // 用户名
    public string? Password { get; set; }         // 密码
    public string? Name { get; set; }             // 代理名称
    public bool Enabled { get; set; }             // 是否启用
    public int Priority { get; set; }             // 优先级
    public int MaxConcurrentConnections { get; set; } // 最大并发
    public int TimeoutSeconds { get; set; }       // 超时时间
    public List<string> BypassList { get; set; }  // 绕过列表
    public Dictionary<string, string> Tags { get; set; } // 标签
}
```

### 2. ProxyPoolOptions

代理池配置选项，控制代理池的行为。

```csharp
public class ProxyPoolOptions
{
    public bool Enabled { get; set; }                       // 是否启用
    public ProxySelectionStrategy SelectionStrategy { get; set; } // 选择策略
    public List<ProxyConfiguration> Proxies { get; set; }   // 代理列表
    public bool EnableHealthCheck { get; set; }             // 启用健康检查
    public int HealthCheckIntervalSeconds { get; set; }     // 检查间隔
    public int FailureThreshold { get; set; }               // 失败阈值
    public int RecoveryTimeSeconds { get; set; }            // 恢复时间
}
```

### 3. IProxyPoolManager

代理池管理器接口，提供代理池的所有管理功能。

```csharp
public interface IProxyPoolManager
{
    ProxyConfiguration? GetNextProxy();                     // 获取下一个代理
    IEnumerable<ProxyConfiguration> GetAvailableProxies();  // 获取可用代理
    Task<bool> AddProxyAsync(ProxyConfiguration proxy);     // 添加代理
    bool RemoveProxy(string proxyAddress);                  // 移除代理
    ProxyStatistics? GetProxyStatistics(string proxyAddress); // 获取统计
    void RecordProxyResult(string proxyAddress, bool success, long responseTime); // 记录结果
    void MarkProxyUnavailable(string proxyAddress);         // 标记不可用
    void MarkProxyAvailable(string proxyAddress);           // 标记可用
    Task StartHealthCheckAsync(CancellationToken cancellationToken); // 启动健康检查
}
```

### 4. IProxyValidator

代理验证器接口，负责验证代理的可用性。

```csharp
public interface IProxyValidator
{
    Task<ProxyValidationResult> ValidateAsync(
        ProxyConfiguration proxy,
        string testUrl,
        int timeoutSeconds,
        CancellationToken cancellationToken);

    Task<IEnumerable<ProxyValidationResult>> ValidateBatchAsync(
        IEnumerable<ProxyConfiguration> proxies,
        string testUrl,
        int timeoutSeconds,
        int maxConcurrency,
        CancellationToken cancellationToken);
}
```

## 使用场景

### 场景 1: Web 爬虫

使用代理池轮询请求，避免 IP 被封禁。

```csharp
var options = new XiHanHttpRequestOptions().EnableProxyPool();
var result = await httpService.GetAsync<string>(url, options);
```

### 场景 2: API 调用

通过指定代理访问受地理位置限制的 API。

```csharp
var proxy = new ProxyConfiguration { Host = "us-proxy.com", Port = 8080 };
var options = new XiHanHttpRequestOptions().SetProxy(proxy);
var result = await httpService.GetAsync<Data>(url, options);
```

### 场景 3: 负载均衡

使用多个代理分散请求负载。

```csharp
// 配置多个代理，使用轮询策略
// 在 appsettings.json 中配置
// SelectionStrategy = "RoundRobin"
```

### 场景 4: 高可用性

代理失败自动切换到其他可用代理。

```csharp
// 代理池会自动检测失败并切换
// FailureThreshold = 5  // 连续5次失败后切换
```

## 配置示例

### 基础配置

```json
{
  "XiHan": {
    "Http:ProxyPool": {
      "Enabled": true,
      "SelectionStrategy": "RoundRobin"
    }
  }
}
```

### 完整配置

参见 `appsettings.example.json` 文件

## 性能考虑

1. **代理池大小**: 建议 5-20 个代理
2. **健康检查间隔**: 建议 60-300 秒
3. **失败阈值**: 建议 3-10 次
4. **恢复时间**: 建议 300-600 秒
5. **并发连接**: 根据代理性能调整

## 最佳实践

1. ✅ 使用代理池而不是单个代理
2. ✅ 启用健康检查以确保代理可用性
3. ✅ 合理设置失败阈值和恢复时间
4. ✅ 监控代理统计信息
5. ✅ 使用合适的选择策略
6. ✅ 避免在日志中记录代理认证信息
7. ✅ 定期更新代理列表
8. ✅ 实现请求失败重试机制

## 技术细节

### SOCKS 代理原生支持

从 .NET 6 开始，`SocketsHttpHandler` 和 `HttpClientHandler` 原生支持 SOCKS 代理：

```csharp
// 直接使用 SOCKS5 代理
var client = new HttpClient(new SocketsHttpHandler()
{
    Proxy = new WebProxy("socks5://127.0.0.1:9050")
});
```

本框架已经完全集成了这个功能，所有代理类型（HTTP/HTTPS/SOCKS4/SOCKS5）都是原生支持的，**无需任何第三方库**！

参考: [HttpToSocks5Proxy GitHub](https://github.com/MihaZupan/HttpToSocks5Proxy) - 该第三方库已废弃，因为 .NET 6+ 已原生支持。

## 注意事项

1. ✅ SOCKS 代理已原生支持，无需第三方库
2. ⚠️ 代理认证信息请妥善保管
3. ⚠️ 健康检查会产生额外流量
4. ⚠️ 代理池大小影响内存使用
5. ⚠️ 某些网站可能检测并阻止代理访问

## 扩展建议

### 未来可以添加的功能：

- 🔲 代理池持久化（保存到数据库）
- 🔲 代理自动发现和更新
- 🔲 地理位置感知的代理选择
- 🔲 基于负载的智能代理分配
- 🔲 代理性能基准测试
- 🔲 代理成本跟踪
- 🔲 WebSocket 代理支持
- 🔲 代理链支持（多级代理）

## 故障排除

### 问题 1: 代理连接超时

**解决方案**: 增加 `TimeoutSeconds` 或检查代理服务器状态

### 问题 2: 所有代理都不可用

**解决方案**:

- 检查网络连接
- 验证代理配置
- 降低 `FailureThreshold`
- 减少 `RecoveryTimeSeconds`

### 问题 3: 代理认证失败

**解决方案**:

- 验证用户名和密码
- 检查代理是否需要认证

### 问题 4: 健康检查 URL 无法访问

**解决方案**: 更换 `HealthCheckUrl` 为可访问的 URL

## 技术支持

如有问题或建议，请联系：

- Email: me@zhaifanhua.com
- GitHub: [Issues](https://github.com/XiHanFun/XiHan.Framework/issues)

## 版本历史

- **v1.4.1** - 添加完整的代理请求功能
  - 代理池管理
  - 多种选择策略
  - 健康检查机制
  - 统计信息收集
  - 代理验证器

## 许可证

MIT License - Copyright ©2021-Present ZhaiFanhua
