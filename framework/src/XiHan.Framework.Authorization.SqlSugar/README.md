# XiHan.Framework.Authorization.SqlSugar

## 概述

`XiHan.Framework.Authorization` 的授权存储 SqlSugar 持久化提供程序。主包的 `IPermissionStore`、`IRoleStore`、`IPolicyStore` 默认实现是作用域生命周期的内存字典，每个请求拿到的都是空存储；本包把权限、角色、策略及其关联落到数据库，并以直接查表的实现替换 `IPermissionChecker`。

## 核心能力

- 六张表：`sys_authz_permission`、`sys_authz_user_permission`、`sys_authz_role_permission`、`sys_authz_role`、`sys_authz_user_role`、`sys_authz_policy`
- `SqlSugarPermissionStore`、`SqlSugarRoleStore`、`SqlSugarPolicyStore` 以 `Replace` 顶替主包的三个内存存储
- `SqlSugarPermissionChecker` 顶替 `IPermissionChecker`：一次权限判定至多执行 2 条 SQL（直接授予命中时 1 条），与用户的角色数、一次判定的权限数无关
- 删除角色时在同一事务内级联删除其用户关联与角色权限；已处于事务型工作单元中时并入该事务
- 角色与各类授予按租户严格隔离；权限定义与策略存放在当前租户布局的主库，不带租户列
- 表结构由 `DbInitializer` 在应用启动时创建，**必须开启** `XiHan:Data:SqlSugarCore` 下的 `EnableDbInitialization` 与 `EnableTableInitialization`（二者默认均为 `false`）

## 依赖关系

依赖 `XiHan.Framework.Authorization`（存储与检查器契约）与 `XiHan.Framework.Data`（SqlSugar 数据访问、租户过滤、雪花主键）。

## 配置与约定

本包没有自己的配置节。

表名 `sys_authz_` 前缀、全小写下划线，不分表；列名 Pascal_Snake_Case；主键 `Basic_Id` 为雪花 ID，非自增。契约里的角色标识、权限名、策略名、用户标识存为普通列，按租户组成唯一索引。表名刻意不用 `sys_role`、`sys_permission`，以免与应用自有的同名业务表冲突。

未开启上述两个建表开关时不会建表，首次调用任何一个存储或检查器即报「表不存在」。实体标注了 `[TableInitialization(Group = "Authorization")]`：`TableInitialization.Mode` 为 `OptIn` 时同样会建这六张表；`All` 模式下可用 `ExcludedGroups: ["Authorization"]` 跳过它们。

角色、用户角色关联、用户权限、角色权限四张表按租户隔离：每条读写都显式带 `Tenant_Id = ICurrentTenant.Id ?? 0`，插入时写入同一个值，结果只来自当前租户，与 `EnableTenantFilter` 开关无关；四个实体同时实现 `IStrictMultiTenantEntity`，全局过滤器开着时与显式条件结果相同。平台态（无租户上下文）读写租户 0 的数据，看不到业务租户的角色与授予；要管理某个租户的授权，先用 `ICurrentTenant.Change` 切到该租户。权限定义与策略两张表不带租户列，落在当前租户布局的主库：共享库部署下各租户共用平台播种的同一份数据，租户态调用它们的写方法会改到这份共用数据，应只在平台态开放；租户独立库部署下每个租户库各有一份，须在每个租户库内播种，平台态写入只影响平台库。

所有读写经 `ISqlSugarClientResolver.GetCurrentClient()`，存在事务型工作单元时自动并入。

与主包默认实现的差异：

- 用户角色关联保存角色标识而非名称，角色改名后成员关系保持
- 删除角色会一并删除其角色权限，以同一标识重建的角色不继承旧权限
- 含 `CustomRequirements` 的策略无法落库，`CreatePolicyAsync` / `UpdatePolicyAsync` 抛 `NotSupportedException`；这类策略需由应用自行实现的 `IPolicyStore` 提供

已知边界：

- `DefaultPolicyEvaluator` 对策略的 `RequiredPermissions` 仍逐个判定，每个权限 1 至 2 条 SQL
- 应用自己 `Replace` 的 `IPermissionChecker` 优先于本包的实现，此时判定次数由应用的实现决定
- 名称比较由数据库排序规则决定：MySQL、SQL Server 默认不区分大小写并忽略尾随空格，PostgreSQL、SQLite 区分；检查器取回权限名后按序数再核对一次，因此 `IsInRoleAsync` 等按名称的查询与权限判定口径不完全一致（唯一索引保证不构成越权）
- `GrantPermissionToRoleAsync` 不校验角色是否存在，给尚未创建的角色标识授权会留下孤儿授予，之后以该标识创建的角色直接继承它（与默认实现一致）
- 策略的任一要求集合为 `null` 时写入抛 `ArgumentException`：没有要求请传空集合
- 查重与插入之间不加锁，并发写入同一条授权或同名角色 / 策略时，后到者撞唯一索引抛出数据库异常
- 删除权限定义不删除已有的授予行，定义补回后授予立即重新生效
- 静态角色（`IsStatic`）可以删除，`IsInRoleAsync` 不检查角色是否启用，均与默认实现一致
- 权限名超过 256 字符、用户或角色标识超过 128 字符时，严格模式的数据库直接报错，本包不截断
- `Properties` 以 System.Text.Json 存取，读回的字典值为 `JsonElement`
- SqlSugar 的异步方法把取消令牌写进 `Ado.CancellationToken` 且执行后不清除，同一作用域内后续不带令牌的调用会继承它

## 使用方式

在应用启动模块上声明依赖：

```csharp
[DependsOn(typeof(XiHanAuthorizationSqlSugarModule))]
public class YourAppModule : XiHanModule
{
}
```

开启建表：

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

权限定义不在 `IPermissionStore` 契约里，通过具体类型写入（例如在数据种子中）：

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

## 扩展点

三个存储与检查器都以 `services.Replace` 注册。应用侧要再次替换，同样使用 `Replace`——`TryAdd` 不会生效。

本包不缓存判定结果。需要缓存时，在 `IPermissionChecker` 外套一层装饰器，并在应用自己的授权写路径上作废缓存。

## 目录结构

```text
XiHan.Framework.Authorization.SqlSugar/
  Entities/
    SysAuthzPermission.cs
    SysAuthzPolicy.cs
    SysAuthzRole.cs
    SysAuthzRolePermission.cs
    SysAuthzUserPermission.cs
    SysAuthzUserRole.cs
  Extensions/
    DependencyInjection/
      XiHanAuthorizationSqlSugarServiceCollectionExtensions.cs
  Mapping/
    JsonColumn.cs
    PermissionMapper.cs
    PolicyMapper.cs
    RoleMapper.cs
  Permissions/
    SqlSugarPermissionChecker.cs
    SqlSugarPermissionStore.cs
  Policies/
    SqlSugarPolicyStore.cs
  Roles/
    SqlSugarRoleStore.cs
  README.md
  XiHanAuthorizationSqlSugarModule.cs
```
