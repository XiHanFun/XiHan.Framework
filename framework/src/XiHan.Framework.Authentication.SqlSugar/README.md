# XiHan.Framework.Authentication.SqlSugar

## 概述

`XiHan.Framework.Authentication` 的认证存储 SqlSugar 持久化提供程序。主包的用户、刷新令牌、第三方登录三个存储都是内存实现（用户存储还是作用域内的空字典），进程重启即丢、多实例之间不共享；本包把它们落到数据库。

## 核心能力

- `IUserStore` → `SqlSugarUserStore`（表 `sys_auth_user`）：用户名查找与唯一约束不区分大小写、按当前租户隔离、登录失败次数原子累加、契约外提供 `AddUserAsync`
- `IRefreshTokenStore` → `SqlSugarRefreshTokenStore`（表 `sys_auth_refresh_token`）：只存令牌的 SHA-256 哈希、撤销时标记而不删行、已撤销令牌再次出现时撤销同一主体的全部令牌、保存时顺带清理过期记录
- `IExternalLoginStore` → `SqlSugarExternalLoginStore`（表 `sys_auth_external_login`）：未指定租户时按当前租户、拒绝把第三方账号改绑到其他用户
- 三者均以 `Replace` 顶替主包的默认实现：用户与第三方登录为作用域，刷新令牌为单例
- 表结构由 `DbInitializer` 在应用启动时创建，**必须开启** `XiHan:Data:SqlSugarCore` 下的 `EnableDbInitialization` 与 `EnableTableInitialization`（二者默认均为 `false`）

## 依赖关系

依赖 `XiHan.Framework.Authentication`（存储契约）与 `XiHan.Framework.Data`（SqlSugar 数据访问、雪花主键）。

## 配置与约定

表名 `sys_auth_` 前缀、全小写下划线，不分表；列名 Pascal_Snake_Case；主键 `Basic_Id` 为雪花 ID，非自增。未开启自动建表时，首次读写即抛「表不存在」；`TableInitialization.Mode` 为 `OptIn` 时本包的表不会自动创建。

配置节 `XiHan:Authentication:SqlSugar`：

| 键 | 默认值 | 说明 |
| --- | --- | --- |
| `RefreshTokenReuseDetection` | `true` | 已撤销的刷新令牌再次被校验时，撤销同一租户下同一主体的全部未撤销令牌 |
| `RefreshTokenReuseGracePeriod` | `00:00:00` | 令牌撤销后在此时长内再次出现只拒绝、不级联；为 0 时任何重复使用都级联 |
| `RefreshTokenCleanupFrequency` | `256` | 每保存多少次刷新令牌清理一次已过期记录；小于等于 0 时不清理 |

**用户**：`UserInfo.UserId` 是 `Basic_Id` 的十进制字符串，非正整数的标识视为不存在。用户名另存 `Normalized_User_Name`（`ToUpperInvariant()`），`Alice` 与 `alice` 是同一个用户——与主包大小写敏感的默认实现不同。所有读写都带当前租户条件（无租户为 0），租户与平台之间互不可见。存储层对密码哈希、恢复码、双因素密钥原样存取；**双因素密钥以明文落库**。`UpdateUserAsync` **不写密码哈希、失败计数与锁定状态**，它们只能经各自的专用方法修改。同一请求作用域内对同一用户的多次读取返回同一个 `UserInfo` 实例。

**刷新令牌**：数据库与日志里都没有令牌原文。`JwtTokenService` 每次刷新签发新令牌、撤销旧令牌；旧令牌若再次出现，视为被盗用，同一租户下同一主体的全部令牌被撤销，用户**所有设备**都需要重新登录。契约没有令牌家族的概念，这是以主体近似家族的代价：持有该用户任一已撤销令牌的人，在该令牌过期前可以反复触发这一撤销。单页应用多标签页同时刷新也会触发，前端应对刷新请求加互斥，或调大 `RefreshTokenReuseGracePeriod`。级联撤销与过期清理在独立连接上执行、自动提交，**不随业务事务回滚**。同一令牌的并发刷新只有一方成功：`Remove` 是条件更新，落败方的 `Remove` 抛出、`JwtTokenService` 返回 `null`；因此对同一令牌第二次调用 `Remove` 会抛 `InvalidOperationException`。不在事务里时，落败方已插入的新令牌留在库里但从未返回给任何人，按过期清理。持续刷新的会话没有绝对上限。三个方法同步访问数据库。重复保存同一令牌抛唯一约束异常，不覆盖。使用租户独立库时，刷新请求必须解析到与登录时相同的租户。

**第三方登录**：`tenantId` 为空时使用当前租户，而不是主包默认实现的 0。同一第三方账号已绑定其他用户时 `CreateAsync` 抛 `InvalidOperationException`，改绑须先 `RemoveAsync`。提供商名称统一小写、不区分大小写；提供商用户标识区分大小写。`RemoveAsync` 不看租户。显示名称、邮箱、头像地址超长时截断。无论是否启用 OAuth，本包都会注册第三方登录存储。

**事务与并发**：`IncrementFailedLoginAttemptsAsync` / `SetLockoutEndAsync` 参与当前工作单元；下游在登录失败时若抛出异常导致事务回滚，失败计数与锁定会一起回滚，因此登录失败应以返回值表示，不要抛异常，或在独立的工作单元中记录失败。`Remove` 的条件更新未命中时按令牌是否存在判定，存在即视为已撤销并抛出，不受 MySQL 可重复读快照影响。`AddUserAsync` 与第三方登录 `CreateAsync` 先查后插，插入撞唯一索引时会重查：已存在则抛 `InvalidOperationException`，否则原样抛出；重查本身失败时抛出 `AggregateException`，同时包含原插入异常与重查异常。若这两个方法在事务型工作单元内执行，PostgreSQL 上唯一冲突会使整个事务中止，随后的重查也会失败，此时抛出 `AggregateException`，同时包含原插入异常与重查异常；在事务外调用即可避免。实体带 `RowVersion` 版本验证列，但本包的存储以 `SetColumns` 更新，不递增版本。

时间一律以 UTC 存储。下游若自己也 `Replace` 了这些契约，以模块装配顺序靠后者为准。

## 使用方式

在应用启动模块上声明依赖：

```csharp
[DependsOn(typeof(XiHanAuthenticationSqlSugarModule))]
public class YourAppModule : XiHanModule
{
}
```

开启自动建表：

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

创建用户（密码须先经 `IPasswordHasher` 哈希）：

```csharp
var userId = await userStore.AddUserAsync(new UserInfo
{
    Username = "alice",
    PasswordHash = passwordHasher.HashPassword(password),
    IsActive = true
});
```

## 扩展点

需要自定义任一存储时，实现主包对应的接口并以 `services.Replace(...)` 替换：`IUserStore` 与 `IExternalLoginStore` 须注册为作用域，`IRefreshTokenStore` 须注册为单例（`JwtTokenService` 是单例并在构造函数中持有它）。

## 目录结构

```
Entities/                        用户、刷新令牌、第三方登录实体
Mapping/                         用户映射与 UTC 换算
Options/                         配置
Users/                           用户存储
RefreshTokens/                   刷新令牌存储与令牌哈希
ExternalLogins/                  第三方登录存储
Extensions/DependencyInjection/  服务注册扩展
```
