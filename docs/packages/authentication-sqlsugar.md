# XiHan.Framework.Authentication.SqlSugar

> 认证存储的 SqlSugar 持久化提供程序：用户、刷新令牌、第三方登录绑定三个存储落库，替换 [Authentication](./authentication) 的内存实现。

- **NuGet**：`XiHan.Framework.Authentication.SqlSugar`
- **模块类**：`XiHanAuthenticationSqlSugarModule`
- **所在层**：基础设施层
- **关键依赖**：[Authentication](./authentication)（存储契约）、[Data](./data)（SqlSugar 客户端、雪花主键、建表）

## 概述

[Authentication](./authentication) 定义了三个存储契约并各带一个内存实现：`DefaultUserStore` 注册为作用域，每个请求都是空字典；`DefaultRefreshTokenStore` 以令牌明文为键存在进程内；`DefaultExternalLoginStore` 同样在进程内。它们只够跑通示例。本包把三者落到数据库，并补上数据库场景下才需要的安全处理：刷新令牌只存哈希、令牌重用检测、按租户隔离、失败计数原子累加。

## 何时使用

- 应用自己没有用户体系，直接使用框架的 `DefaultAuthenticationService` 做用户名密码登录、双因素、锁定
- 需要刷新令牌在进程重启后仍然有效、在多实例之间共享
- 需要第三方登录（OAuth）的绑定关系落库

已有自己用户表的应用，通常只需要本包的刷新令牌存储；此时可以不依赖本模块，而是在自己的模块里单独 `Replace` 刷新令牌存储。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Authentication.SqlSugar
```

```csharp
[DependsOn(typeof(XiHanAuthenticationSqlSugarModule))]
public class MyModule : XiHanModule { }
```

`XiHanAuthenticationSqlSugarModule.ConfigureServices` 调用 `services.AddXiHanAuthenticationSqlSugar(configuration)`，以 `services.Replace` 顶替主包的注册：

| 契约 | 默认实现 | 本包实现 | 生命周期 |
| --- | --- | --- | --- |
| `IUserStore` | `DefaultUserStore` | `SqlSugarUserStore` | 作用域 |
| `IRefreshTokenStore` | `DefaultRefreshTokenStore` | `SqlSugarRefreshTokenStore` | 单例 |
| `IExternalLoginStore` | `DefaultExternalLoginStore`（仅启用 OAuth 时注册） | `SqlSugarExternalLoginStore` | 作用域 |

建表依赖 [Data](./data) 的两个开关，**默认都是 `false`**：

```json
{
  "XiHan": {
    "Data": {
      "SqlSugarCore": {
        "EnableDbInitialization": true,
        "EnableTableInitialization": true
      }
    }
  }
}
```

## 表结构

**`sys_auth_user`**：唯一索引 `(Tenant_Id, Normalized_User_Name)`。

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Basic_Id` | `bigint` | 雪花主键，即 `UserInfo.UserId` |
| `Tenant_Id` | `bigint` | 0 为平台 |
| `User_Name` / `Normalized_User_Name` | 128 | 原样 / 大写 |
| `Password_Hash` | 512 | 原样存储上层给出的哈希 |
| `Email` / `Phone_Number` | 256 / 32 | 不唯一 |
| `Two_Factor_Enabled` / `Two_Factor_Secret` | `bool` / 256 | 密钥明文 |
| `Recovery_Codes` | 大文本 | 恢复码哈希的 JSON 数组 |
| `Is_Locked` / `Lockout_End` / `Failed_Login_Attempts` | | 锁定与失败计数 |
| `Last_Login_Time` / `Password_Changed_Time` | `datetime` | UTC |
| `Is_Active` / `Additional_Data` | `bool` / 大文本 | |

**`sys_auth_refresh_token`**：唯一索引 `Token_Hash`，普通索引 `(Tenant_Id, Subject)`、`Expires_At`。

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Basic_Id` | `bigint` | 雪花主键 |
| `Tenant_Id` | `bigint` | 签发时的租户 |
| `Token_Hash` | 64 | SHA-256 大写十六进制 |
| `Subject` | 256 | 主体（用户标识） |
| `Expires_At` / `Created_Time` / `Revoked_Time` | `datetime` | UTC；`Revoked_Time` 为空表示未撤销 |

**`sys_auth_external_login`**：唯一索引 `(Tenant_Id, Provider, Provider_Key)`，普通索引 `(User_Id, Provider)`。

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Basic_Id` | `bigint` | 雪花主键 |
| `Tenant_Id` / `User_Id` | `bigint` | |
| `Provider` | 64 | 小写 |
| `Provider_Key` | 256 | 原样，区分大小写 |
| `Display_Name` / `Email` / `Avatar_Url` | 256 / 256 / 2048 | 超长截断 |
| `Created_Time` | `datetime` | UTC |

## 工作原理

### 用户存储

每条 SQL 都带 `Tenant_Id = 当前租户（无租户为 0）`，不依赖全局租户过滤器——全局过滤器对多租户实体放行 `TenantId = 0` 的平台行，用它做隔离会让租户入口能登录平台账号。

`DefaultAuthenticationService` 的几条流程是「先读出用户对象，中途调其他存储方法改库，最后把最初读出的对象交给 `UpdateUserAsync`」。为了让这样写回的对象是最新的，存储在同一个实例（同一请求作用域）内对同一用户返回同一个 `UserInfo` 实例，并在 `UpdatePasswordAsync`、失败计数、锁定方法里同步修改它。`UpdateUserAsync` 不写密码、失败计数与锁定三列，跨请求的并发写回因此不会解除另一个请求加上的锁定。

失败计数以 `SET Failed_Login_Attempts = Failed_Login_Attempts + 1` 在数据库侧累加，并发的失败登录不会丢失计数。

### 刷新令牌

```
登录     Save(T1)                     → 插入 Hash(T1)
刷新     Validate(T1) → Save(T2) → Remove(T1)
                                       → 插入 Hash(T2)，标记 Hash(T1) 已撤销
盗用     Validate(T1)（T1 已撤销）     → 拒绝，并撤销同租户同主体的全部未撤销令牌（含 T2）
```

级联撤销在 `CopyNew()` 开出的独立连接上执行并立即提交。刷新失败时下游通常抛业务异常、回滚工作单元，若级联走同一事务，撤销会被一起回滚。

过期记录的清理在保存时顺带进行：每保存 `RefreshTokenCleanupFrequency` 次，在独立连接上删除 `Expires_At` 早于当前时间的行。已撤销但未过期的行保留，用于重用检测。

存储注册为单例（`JwtTokenService` 是单例并在构造函数中持有它），每次调用经 `IServiceScopeFactory` 新建作用域解析数据库客户端；当前工作单元存于 `AsyncLocal`，新作用域里同样能登记到调用方的事务。

### 第三方登录

`tenantId` 为空时回退到当前租户。查到的绑定再在内存里用序数比较一次 `Provider_Key`，避免 MySQL / SQL Server 默认排序规则下把只差大小写的两个第三方标识当成同一个。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanAuthenticationSqlSugarModule` | 模块类 |
| `SqlSugarUserStore` | `IUserStore` 实现；另有 `Task<string> AddUserAsync(UserInfo user, CancellationToken)` |
| `SqlSugarRefreshTokenStore` | `IRefreshTokenStore` 实现 |
| `SqlSugarExternalLoginStore` | `IExternalLoginStore` 实现 |
| `RefreshTokenHasher` | `string Hash(string refreshToken)` |
| `AuthUserMapper` / `StorageTime` | 用户映射 / UTC 换算 |
| `XiHanAuthenticationSqlSugarOptions` | 配置 |
| `SysAuthUser` / `SysAuthRefreshToken` / `SysAuthExternalLogin` | 实体 |

## 配置

配置节 `XiHan:Authentication:SqlSugar`：

| 键 | 默认值 | 说明 |
| --- | --- | --- |
| `RefreshTokenReuseDetection` | `true` | 重用检测开关 |
| `RefreshTokenReuseGracePeriod` | `00:00:00` | 撤销后在此时长内再次出现只拒绝、不级联 |
| `RefreshTokenCleanupFrequency` | `256` | 每保存多少次清理一次过期记录，≤ 0 不清理 |

## 使用示例

### 1. 创建用户

```csharp
public class UserSeeder(IUserStore userStore, IPasswordHasher passwordHasher)
{
    public async Task SeedAsync()
    {
        if (userStore is SqlSugarUserStore store &&
            await store.GetUserByUsernameAsync("admin") is null)
        {
            await store.AddUserAsync(new UserInfo
            {
                Username = "admin",
                PasswordHash = passwordHasher.HashPassword("Change#Me2026"),
                IsActive = true
            });
        }
    }
}
```

### 2. 放宽多标签页场景的重用检测

```json
{
  "XiHan": {
    "Authentication": {
      "SqlSugar": {
        "RefreshTokenReuseGracePeriod": "00:00:10"
      }
    }
  }
}
```

宽限期内的重复使用依然被拒绝，只是不级联撤销。

### 3. 只使用刷新令牌存储

应用自有用户表、只想让刷新令牌落库时，不依赖 `XiHanAuthenticationSqlSugarModule`，在自己的模块里：

```csharp
services.Configure<XiHanAuthenticationSqlSugarOptions>(
    configuration.GetSection(XiHanAuthenticationSqlSugarOptions.SectionName));
services.TryAddSingleton<TimeProvider>(TimeProvider.System);
services.Replace(ServiceDescriptor.Singleton<IRefreshTokenStore, SqlSugarRefreshTokenStore>());
```

## 扩展点 / 自定义

- **换任一存储**：实现主包接口并 `Replace`。`IUserStore` 与 `IExternalLoginStore` 须为作用域，`IRefreshTokenStore` 须为单例
- **换时间源**：在本模块之前注册自己的 `TimeProvider`，本包以 `TryAdd` 注册系统时钟

## 注意事项与最佳实践

- **`TryAdd` 不生效**。主包已占位，覆盖必须用 `Replace`
- **双因素密钥明文落库**。契约与框架没有字段级加密抽象，数据库泄漏即泄漏全部 TOTP 密钥
- **`UpdateUserAsync` 不写密码、失败计数与锁定**。它们只能经各自的专用方法修改；解锁用户须调 `SetLockoutEndAsync(username, null)` 与 `ResetFailedLoginAttemptsAsync`
- **用户名不区分大小写**。从大小写敏感的旧数据迁入时，仅大小写不同的重名会让唯一索引建不起来
- **重用检测会让用户所有设备下线**。契约没有令牌家族，以主体近似；持有任一已撤销令牌者可在其过期前反复触发
- **多标签页同时刷新**会触发重用检测，前端应对刷新请求加互斥，或设置宽限期
- **同一令牌的并发刷新只有一方成功**：落败方得到 `null`；对同一令牌第二次调用 `Remove` 会抛 `InvalidOperationException`
- **会话没有绝对上限**，持续刷新即不过期
- **刷新令牌存储同步访问数据库**，契约是同步的
- **第三方登录的 `tenantId` 为空时用当前租户**，与默认实现的 0 不同；已绑定其他用户时拒绝改绑
- **`OptIn` 建表模式**下本包的表不会自动创建
- **失败计数与锁定参与当前工作单元**：`IncrementFailedLoginAttemptsAsync` / `SetLockoutEndAsync` 与业务写入同属当前工作单元。下游在登录失败时若抛出异常导致事务回滚，失败计数与锁定会一起回滚，账户锁定即失效；登录失败应以返回值表示，不要抛异常，或在独立的工作单元中记录失败
- **`RowVersion` 不递增**：实体带 `RowVersion` 版本验证列，但本包的存储以 `SetColumns` 更新，不递增版本
- **唯一索引冲突按契约转抛**：`AddUserAsync` 与第三方登录 `CreateAsync` 先查后插，并发插入撞唯一索引时会重查；同名用户或其他用户的绑定已存在则抛 `InvalidOperationException`，查不到则原样抛出；重查本身失败时抛出 `AggregateException`，同时包含原插入异常与重查异常
- **PostgreSQL 事务内的插入竞态**：若 `AddUserAsync` 或第三方登录 `CreateAsync` 在事务型工作单元内执行，PostgreSQL 上唯一冲突会使整个事务中止，随后的重查也会失败，此时抛出 `AggregateException`，同时包含原插入异常与重查异常。在事务外调用即可避免

## 依赖模块

- [XiHan.Framework.Authentication](./authentication)（三个存储契约、`JwtTokenService`、`DefaultAuthenticationService`）
- [XiHan.Framework.Data](./data)（`ISqlSugarClientResolver`、`DbInitializer`、SqlSugar 传递依赖）

`IDistributedIdGenerator<long>` 经 `XiHanDataModule → XiHanDistributedIdsModule` 间接获得。

## 相关模块

- [XiHan.Framework.Security](./security)（`IPasswordHasher`）
- [XiHan.Framework.MultiTenancy](./multitenancy)（`ICurrentTenant`）
- [XiHan.Framework.Uow](./uow)（工作单元与事务）
- [XiHan.Framework.DistributedIds](./distributed-ids)（雪花主键）
