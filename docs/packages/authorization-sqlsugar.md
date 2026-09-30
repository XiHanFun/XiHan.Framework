# XiHan.Framework.Authorization.SqlSugar

> 授权存储的 SqlSugar 持久化提供程序：权限、角色、策略及其关联落库，并以直接查表的检查器替换默认权限检查器。替换 [Authorization](./authorization) 的内存存储后，RBAC 数据才真正跨请求保留。

- **NuGet**：`XiHan.Framework.Authorization.SqlSugar`
- **模块类**：`XiHanAuthorizationSqlSugarModule`
- **所在层**：基础设施层
- **关键依赖**：[Authorization](./authorization)（存储与检查器契约）、[Data](./data)（SqlSugar 客户端、租户过滤、雪花主键、建表）

## 概述

[Authorization](./authorization) 定义了 `IPermissionStore`、`IRoleStore`、`IPolicyStore` 三个存储契约，默认实现是**作用域生命周期**的内存字典——每个请求拿到一个全新的空存储，授予、角色、策略都留不过一个请求。

本包补上两件事：

- **落库**：三个存储的 SqlSugar 实现，六张表
- **热路径**：默认的 `DefaultPermissionChecker` 对用户的每个启用角色各查一次角色权限，一次判定要 `2 + 角色数` 次查询，判定多个权限时再乘以权限数。本包以 `SqlSugarPermissionChecker` 替换它，一次判定至多 2 条 SQL

## 何时使用

- 需要把 RBAC 数据（权限定义、角色、授予、策略）存进关系型数据库
- 使用框架自带的 `[PermissionAuthorize]` / `IAuthorizationService`，希望授权变更即时生效
- 已在用 [Data](./data)，希望授权数据随租户隔离

应用若已有自己的角色、权限表和检查器（例如在应用层基于授权快照判定），不需要本包。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Authorization.SqlSugar
```

```csharp
[DependsOn(typeof(XiHanAuthorizationSqlSugarModule))]
public class MyModule : XiHanModule { }
```

`XiHanAuthorizationSqlSugarModule.ConfigureServices` 调用 `services.AddXiHanAuthorizationSqlSugar()`，以 `services.Replace` 顶替 [Authorization](./authorization) 用 `TryAddScoped` 注册的默认实现：

| 契约 | 默认实现 | 本包实现 |
| --- | --- | --- |
| `IPermissionStore` | `DefaultPermissionStore` | `SqlSugarPermissionStore`（具体类型另以作用域注册） |
| `IRoleStore` | `DefaultRoleStore` | `SqlSugarRoleStore` |
| `IPolicyStore` | `DefaultPolicyStore` | `SqlSugarPolicyStore` |
| `IPermissionChecker` | `DefaultPermissionChecker` | `SqlSugarPermissionChecker` |

本包**没有自己的配置节**。建表需要打开 [Data](./data) 的两个开关（默认均为 `false`）：

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

不打开就不会建表，首次调用任何一个存储或检查器即报「表不存在」。

## 表结构

| 表 | 内容 | 租户 | 唯一索引 |
| --- | --- | --- | --- |
| `sys_authz_permission` | 权限定义 | 无租户列，随租户布局的主库 | `Permission_Name` |
| `sys_authz_user_permission` | 用户直接授予 | 严格隔离 | `Tenant_Id, User_Id, Permission_Name` |
| `sys_authz_role_permission` | 角色授予 | 严格隔离 | `Tenant_Id, Role_Id, Permission_Name` |
| `sys_authz_role` | 角色 | 严格隔离 | `Tenant_Id, Role_Id`；`Tenant_Id, Role_Name` |
| `sys_authz_user_role` | 用户角色关联 | 严格隔离 | `Tenant_Id, User_Id, Role_Id` |
| `sys_authz_policy` | 策略 | 无租户列，随租户布局的主库 | `Policy_Name` |

| 约定 | 值 |
| --- | --- |
| 表名 | `sys_authz_` 前缀、全小写下划线，不分表；刻意避开 `sys_role`、`sys_permission` 这类常见业务表名 |
| 列名 | Pascal_Snake_Case，每列带简体中文 `ColumnDescription` |
| 主键 | `Basic_Id`，`long`，雪花 ID，非自增 |
| 业务键 | 契约里的角色标识、权限名、策略名、用户标识存为普通列，按租户组成唯一索引 |
| 关联 | 关联表存契约里的字符串标识，不存其他表的 `Basic_Id`；用户角色关联存**角色标识** |
| 集合字段 | 策略的 `RequiredRoles`、`RequiredPermissions`、`RequiredClaims` 与各定义的 `Properties` 存为 JSON 文本 |
| 建表分组 | `[TableInitialization(Group = "Authorization")]`，`OptIn` 模式也会建；`All` 模式可用 `ExcludedGroups` 排除 |

「严格隔离」指每条读写都显式带 `Tenant_Id = ICurrentTenant.Id ?? 0`（平台态为 0），不依赖全局过滤器；实体另外实现 `IStrictMultiTenantEntity`，过滤器开着时结果相同。

## 工作原理

### 一次权限判定

```text
SqlSugarPermissionChecker.IsGrantedAsync(userId, name)
  ① 直接授予
     user_permission ⋈ permission(启用)             WHERE User_Id = ? AND 名称 IN (…)
     全部命中 → 返回
  ② 经角色授予
     user_role ⋈ role(启用) ⋈ role_permission ⋈ permission(启用)
       各跳联表条件都带 Tenant_Id 相等                WHERE User_Id = ? AND 名称 IN (…)
  结果 = ① ∪ ②
```

`IsAnyGrantedAsync`、`IsAllGrantedAsync` 把全部候选权限放进同一对查询；`GetGrantedPermissionsAsync` 不带候选条件。每条查询的每一跳都有首列等值的索引。

判定语义与 `DefaultPermissionChecker` 一致：直接授予只看权限是否启用；经角色授予要求角色与权限都启用；`PermissionExistsAsync` 只看定义是否存在。

### 删除角色

```text
按 Role_Id 读出角色行
在同一事务内：
  DELETE user_role       WHERE Tenant_Id = 该行 AND Role_Id = 该行
  DELETE role_permission WHERE Tenant_Id = 该行 AND Role_Id = 该行
  DELETE role            WHERE Basic_Id = 该行
```

当前已处于事务型工作单元时并入该事务，由工作单元统一提交或回滚；否则自开事务，失败时整体回滚并把异常抛给调用方。

### 事务与客户端

所有读写经 `ISqlSugarClientResolver.GetCurrentClient()`，存在事务型工作单元时自动并入——「创建用户 + 分配角色」可以在同一个事务里完成。六张表都在当前租户布局的主库，不按实体分库：共享库部署下即平台主库，租户独立库部署下是各租户自己的主库。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `SqlSugarPermissionStore` | `IPermissionStore` 实现；另有 `AddOrUpdatePermissionAsync`、`AddPermissionsAsync`、`RemovePermissionAsync` 维护权限定义 |
| `SqlSugarRoleStore` | `IRoleStore` 实现 |
| `SqlSugarPolicyStore` | `IPolicyStore` 实现 |
| `SqlSugarPermissionChecker` | `IPermissionChecker` 实现 |
| `SysAuthzPermission` 等 6 个实体 | 表实体，各带无参与 `(long basicId)` 两个公开构造函数 |
| `PermissionMapper` / `RoleMapper` / `PolicyMapper` | 定义与实体的静态映射器 |
| `XiHanAuthorizationSqlSugarServiceCollectionExtensions` | `AddXiHanAuthorizationSqlSugar()`：以 `Replace` 注册上述实现 |
| `XiHanAuthorizationSqlSugarModule` | 模块类，`[DependsOn(XiHanAuthorizationModule, XiHanDataModule)]`，只做装配 |

## 使用示例

### 1. 播种权限定义

权限定义不在 `IPermissionStore` 契约里，通过具体类型写入：

```csharp
using var scope = serviceProvider.CreateScope();
var permissionStore = scope.ServiceProvider.GetRequiredService<SqlSugarPermissionStore>();

await permissionStore.AddPermissionsAsync(
[
    new PermissionDefinition("User.Create", "创建用户"),
    new PermissionDefinition("User.Delete", "删除用户")
]);
```

`AddPermissionsAsync` 逐条写入，不保证原子性：中途失败时，已写入的定义不会回滚。

### 2. 建角色并授权

```csharp
await roleStore.CreateRoleAsync(new RoleDefinition("editor", "editor", "编辑"));
await permissionStore.GrantPermissionToRoleAsync("editor", "User.Create");
await roleStore.AddUserToRoleAsync(userId, "editor");
```

`GrantPermissionToRoleAsync` 的第一个参数是**角色标识**，`AddUserToRoleAsync` 的第二个参数是**角色名称**——这是契约本身的约定。

### 3. 判定

```csharp
var granted = await permissionChecker.IsGrantedAsync(userId, "User.Create");
```

## 扩展点 / 自定义

- **再次替换**：三个存储与检查器都以 `services.Replace` 注册，应用侧也要用 `Replace`；`TryAdd` 不生效
- **缓存判定**：本包不缓存。需要时在 `IPermissionChecker` 外套装饰器，并在应用自己的授权写路径上作废
- **自定义要求**：含 `CustomRequirements` 的策略无法落库，由应用自行实现 `IPolicyStore` 提供

## 注意事项与最佳实践

- **建表开关默认关闭**。`EnableDbInitialization` 与 `EnableTableInitialization` 都要打开
- **租户隔离不依赖过滤器**。角色、用户角色关联与两张授予表的每条 SQL 显式带当前租户条件，联表另带 `Tenant_Id` 相等；`EnableTenantFilter` 关掉时结果也只来自当前租户。平台态读写租户 0，管理某个租户的授权须先切换到该租户
- **权限定义与策略存放在当前租户布局的主库**。这两张表不带租户列：共享库部署下各租户共用平台播种的数据，租户态调用写方法会改到这份共用数据，应只在平台态开放；租户独立库部署下每个租户库各有一份，须在每个租户库内播种，平台态写入只影响平台库
- **含自定义要求的策略写入即抛 `NotSupportedException`**。这是为了不让一条要求在落库时悄悄消失
- **策略评估仍逐权限判定**。`DefaultPolicyEvaluator` 对 `RequiredPermissions` 逐个调 `IsGrantedAsync`，p 个权限约 2p 条 SQL
- **应用自己的检查器优先**。应用 `Replace` 了 `IPermissionChecker` 时，本包的检查器不生效
- **名称比较交给数据库**。MySQL、SQL Server 默认不区分大小写并忽略尾随空格，PostgreSQL、SQLite 区分；检查器按序数核对权限名，与按名称查角色的口径不完全一致
- **预先授予不存在的角色**会留下孤儿授予，之后以该标识创建的角色直接继承它
- **策略的要求集合不能为 `null`**。写入时抛 `ArgumentException`，没有要求请传空集合
- **并发重复写入**。查重与插入之间不加锁，后到者撞唯一索引抛数据库异常
- **删除权限定义不删授予**。定义补回后授予立即重新生效
- **与默认实现的差异**：用户角色关联存角色标识，改名后成员关系保持；删除角色会删除其角色权限
- **与默认实现一致的行为**：静态角色可以删除；`IsInRoleAsync` 不检查角色是否启用
- **`Properties` 读回为 `JsonElement`**，不是写入时的原始类型

## 依赖模块

- [XiHan.Framework.Authorization](./authorization)（存储与检查器契约）
- [XiHan.Framework.Data](./data)（`ISqlSugarClientResolver`、租户过滤器、`DbInitializer`、SqlSugar 传递依赖）

`IDistributedIdGenerator<long>` 经 `XiHanDataModule → XiHanDistributedIdsModule` 间接获得，本包不额外声明依赖。

## 相关模块

- [XiHan.Framework.MultiTenancy](./multitenancy)（租户上下文）
