# XiHan.Framework 元数据包

曦寒框架元数据包，提供框架版本信息、基础接口定义、通用枚举、常量定义等，为所有其他包提供基础支持。

## 功能特性

- **框架信息管理**：统一的框架版本、版权、作者等信息
- **常量定义**：框架中使用的各种常量值（按功能分类）
- **枚举定义**：框架中使用的各种枚举类型（按类型分类）
- **接口定义**：框架服务的基础接口（按能力分类）
- **特性定义**：框架中使用的各种特性（按用途分类）

## 包含内容

### 1. 框架信息 (XiHan)

- 框架版本信息
- 作者和组织信息
- 仓库和文档地址
- 支持的框架和平台

### 2. 常量 (Constants)

#### 超时相关常量 (TimeoutConstants)

- 默认超时时间、重试次数、重试间隔
- 会话超时、JWT 过期时间、刷新令牌过期时间
- 验证码过期时间

#### 缓存相关常量 (CacheConstants)

- 默认缓存时间

#### 分页相关常量 (PaginationConstants)

- 默认分页大小、最大分页大小

#### 环境相关常量 (EnvironmentConstants)

- 环境名称（开发、测试、生产）
- 默认时区、语言、区域、货币

#### 格式相关常量 (FormatConstants)

- 日期格式、时间格式、日期时间格式

#### 文件相关常量 (FileConstants)

- 文件上传大小限制、请求大小限制

#### 安全相关常量 (SecurityConstants)

- 密码长度限制、用户名长度限制
- 邮箱长度、手机号长度、验证码长度

#### API 相关常量 (ApiConstants)

- API 版本、前缀、路径
- 健康检查、指标、Swagger 路径

#### 认证授权相关常量 (AuthenticationConstants)

- CORS 策略、认证方案、授权策略
- 角色名称（用户、管理员、系统）

#### 系统相关常量 (SystemConstants)

- 连接字符串名称、日志级别
- 租户 ID、用户 ID、组织 ID

### 3. 枚举 (Enums)

#### 基础枚举

- **LogLevel** - 日志级别（Trace、Debug、Information、Warning、Error、Critical、None）
- **EnvironmentType** - 环境类型（Development、Test、Production）
- **DatabaseType** - 数据库类型（SqlServer、MySql、PostgreSql、Oracle、Sqlite、MongoDb、InMemory）
- **CacheType** - 缓存类型（Memory、Redis、File）

#### 认证授权枚举

- **AuthenticationType** - 认证类型（Jwt、OAuth、OpenId、Windows、Forms）
- **AuthorizationType** - 授权类型（Role、Permission、Policy、Operation）

#### 用户相关枚举

- **UserStatus** - 用户状态（Active、Inactive、Locked、Pending）
- **Gender** - 性别（Unknown、Male、Female）

#### 操作相关枚举

- **OperationType** - 操作类型（Create、Read、Update、Delete、Login、Logout、Authorize）
- **SortDirection** - 排序方向（Ascending、Descending）

#### 查询相关枚举

- **ComparisonOperator** - 比较操作符（Equal、NotEqual、GreaterThan、LessThan 等）
- **LogicalOperator** - 逻辑操作符（And、Or、Not）

#### 消息通知枚举

- **MessageType** - 消息类型（Info、Success、Warning、Error、Debug）
- **NotificationType** - 通知类型（Email、Sms、WeChat、DingTalk）

#### 文件任务枚举

- **FileType** - 文件类型（Image、Document、Video、Audio、Other）
- **TaskStatus** - 任务状态（Pending、Running、Completed、Failed、Paused）
- **TaskPriority** - 任务优先级（Low、Normal、High、Critical）

### 4. 接口 (Interfaces)

#### 基础服务接口

- **IFrameworkService** - 框架服务基础接口（初始化、启动、停止、状态）

#### 能力扩展接口

- **IConfigurableService<T>** - 可配置服务接口
- **IExtensibleService<T>** - 可扩展服务接口
- **IMonitorableService** - 可监控服务接口
- **IAuditableService** - 可审计服务接口
- **ICacheableService** - 可缓存服务接口
- **IValidatableService** - 可验证服务接口
- **ISerializableService** - 可序列化服务接口
- **IEncryptableService** - 可加密服务接口
- **ICompressibleService** - 可压缩服务接口

### 5. 特性 (Attributes)

#### 服务模块特性

- **FrameworkServiceAttribute** - 框架服务特性（服务名称、版本、单例、初始化顺序）
- **FrameworkModuleAttribute** - 框架模块特性（模块名称、依赖、是否必需）

#### 配置验证特性

- **FrameworkConfigurationAttribute** - 框架配置特性（配置键、默认值、是否必需）
- **FrameworkValidationAttribute** - 框架验证特性（验证规则、错误消息）

#### 功能增强特性

- **FrameworkCacheAttribute** - 框架缓存特性（缓存键、时间、策略）
- **FrameworkLogAttribute** - 框架日志特性（日志级别、消息、参数记录）
- **FrameworkAuditAttribute** - 框架审计特性（操作类型、描述、详细信息）
- **FrameworkPerformanceAttribute** - 框架性能特性（性能阈值、性能记录）

#### 安全版本特性

- **FrameworkSecurityAttribute** - 框架安全特性（安全级别、权限、角色）
- **FrameworkVersionAttribute** - 框架版本特性（版本号、弃用状态）
- **FrameworkDocumentationAttribute** - 框架文档特性（标题、描述、标签、示例代码）

## 使用示例

```csharp
// 获取框架信息
var version = XiHan.Version;
var description = XiHan.Description;
var summary = XiHan.GetSummary();

// 使用常量
var timeout = TimeoutConstants.DefaultTimeoutSeconds;
var pageSize = PaginationConstants.DefaultPageSize;
var environment = EnvironmentConstants.DefaultEnvironment;

// 使用枚举
var logLevel = LogLevel.Information;
var dbType = DatabaseType.SqlServer;
var userStatus = UserStatus.Active;

// 使用接口
[FrameworkService("MyService", "我的服务", "1.0.0")]
[FrameworkModule("MyModule", "我的模块")]
public class MyService : IFrameworkService, IConfigurableService<MyConfig>
{
    public string ServiceName => "MyService";
    public string ServiceVersion => "1.0.0";
    public bool IsInitialized { get; private set; }

    [FrameworkConfiguration("MyConfig", "我的配置")]
    public MyConfig Configuration { get; private set; } = new();

    public void Configure(MyConfig configuration)
    {
        Configuration = configuration;
    }

    public MyConfig GetConfiguration() => Configuration;

    public async Task<bool> InitializeAsync()
    {
        // 初始化逻辑
        IsInitialized = true;
        return true;
    }

    public async Task<bool> StartAsync() => true;
    public async Task<bool> StopAsync() => true;
    public async Task<string> GetStatusAsync() => "Running";
}

// 使用特性
[FrameworkService("UserService", "用户服务", "1.0.0")]
[FrameworkModule("UserModule", "用户模块")]
public class UserService
{
    [FrameworkConfiguration("UserConfig", "用户配置")]
    public UserConfig Config { get; set; } = new();

    [FrameworkCache("UserCache", 60)]
    [FrameworkLog(LogLevel.Information, "获取用户信息")]
    [FrameworkAudit(OperationType.Read, "查询用户")]
    [FrameworkPerformance(1000)]
    [FrameworkSecurity("Normal", ["User.Read"], ["User"])]
    public async Task<User> GetUserAsync(int userId)
    {
        return new User { Id = userId, Name = "张三" };
    }
}

// 配置类
public class MyConfig
{
    [FrameworkValidation("Required", "配置项不能为空")]
    public string ConnectionString { get; set; } = string.Empty;

    public int Timeout { get; set; } = TimeoutConstants.DefaultTimeoutSeconds;
}

public class UserConfig
{
    public int MaxLoginAttempts { get; set; } = 3;
    public int SessionTimeout { get; set; } = TimeoutConstants.DefaultSessionTimeoutMinutes;
}
```

## 文件结构

```
XiHan.Framework/
├── XiHan.cs              # 框架信息
├── Constants/                     # 常量定义
│   ├── TimeoutConstants.cs       # 超时相关常量
│   ├── CacheConstants.cs         # 缓存相关常量
│   ├── PaginationConstants.cs    # 分页相关常量
│   ├── EnvironmentConstants.cs   # 环境相关常量
│   ├── FormatConstants.cs        # 格式相关常量
│   ├── FileConstants.cs          # 文件相关常量
│   ├── SecurityConstants.cs      # 安全相关常量
│   ├── ApiConstants.cs           # API相关常量
│   ├── AuthenticationConstants.cs # 认证授权相关常量
│   └── SystemConstants.cs        # 系统相关常量
├── Enums/                        # 枚举定义
│   ├── LogLevel.cs               # 日志级别
│   ├── EnvironmentType.cs        # 环境类型
│   ├── DatabaseType.cs           # 数据库类型
│   ├── CacheType.cs              # 缓存类型
│   ├── AuthenticationType.cs     # 认证类型
│   ├── AuthorizationType.cs      # 授权类型
│   ├── UserStatus.cs             # 用户状态
│   ├── Gender.cs                 # 性别
│   ├── OperationType.cs          # 操作类型
│   ├── SortDirection.cs          # 排序方向
│   ├── ComparisonOperator.cs     # 比较操作符
│   ├── LogicalOperator.cs        # 逻辑操作符
│   ├── MessageType.cs            # 消息类型
│   ├── NotificationType.cs       # 通知类型
│   ├── FileType.cs               # 文件类型
│   ├── TaskStatus.cs             # 任务状态
│   └── TaskPriority.cs           # 任务优先级
├── Interfaces/                   # 接口定义
│   ├── IFrameworkService.cs      # 框架服务基础接口
│   ├── IConfigurableService.cs   # 可配置服务接口
│   ├── IExtensibleService.cs     # 可扩展服务接口
│   ├── IMonitorableService.cs    # 可监控服务接口
│   ├── IAuditableService.cs      # 可审计服务接口
│   ├── ICacheableService.cs      # 可缓存服务接口
│   ├── IValidatableService.cs    # 可验证服务接口
│   ├── ISerializableService.cs   # 可序列化服务接口
│   ├── IEncryptableService.cs    # 可加密服务接口
│   └── ICompressibleService.cs   # 可压缩服务接口
└── Attributes/                   # 特性定义
    ├── FrameworkServiceAttribute.cs      # 框架服务特性
    ├── FrameworkModuleAttribute.cs       # 框架模块特性
    ├── FrameworkConfigurationAttribute.cs # 框架配置特性
    ├── FrameworkValidationAttribute.cs   # 框架验证特性
    ├── FrameworkCacheAttribute.cs        # 框架缓存特性
    ├── FrameworkLogAttribute.cs          # 框架日志特性
    ├── FrameworkAuditAttribute.cs        # 框架审计特性
    ├── FrameworkPerformanceAttribute.cs  # 框架性能特性
    ├── FrameworkSecurityAttribute.cs     # 框架安全特性
    ├── FrameworkVersionAttribute.cs      # 框架版本特性
    └── FrameworkDocumentationAttribute.cs # 框架文档特性
```

## 设计原则

### 单一职责原则

- 每个文件只负责一个特定的功能领域
- 常量、枚举、接口、特性按功能严格分离
- 便于维护、测试和扩展

### 模块化设计

- 支持按需引用，减少不必要的依赖
- 清晰的命名规范和代码风格
- 符合 SOLID 原则

### 类型安全

- 使用强类型枚举替代字符串常量
- 提供类型安全的接口定义
- 减少运行时错误

### 可扩展性

- 接口设计支持多种实现方式
- 特性系统支持灵活的功能扩展
- 便于版本管理和发布

## 依赖关系

- 无外部依赖
- 基于 .NET 9
- 使用隐式全局 using
- 支持 AOT 编译

## 版本信息

- **目标框架**: .NET 9.0
- **许可证**: MIT

## 贡献

欢迎提交 Issue 和 Pull Request 来改进这个包。

## 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](../../../LICENSE) 文件了解详情。
