# RBAC 授予与条件

这一页讲权限怎样落到主体身上：授予 `PermissionGrant` 与检查器的判定顺序、内存与数据库两种授予存储、用 `IPermissionFilter` 收窄与用 `IPermissionGrantSource` 扩展授予，以及与存储无关的条件引擎。读完你能把 SimpleApp 的角色授予搬进数据库、给套餐做权限白名单、让角色继承权限，并写出按 IP 与时间判定的条件。阅读前先读[授权与权限](/security/authorization)。

## 为什么需要

权限码定义了「有哪些事可做」，还得有一张表说「谁能做」。RBAC 把授予挂在用户或角色上，角色来自主体的声明。但真实系统的授予不止一张表：套餐限制要拿走角色里本来有的权限，角色继承与限时委托要加上授予表里没有的权限，风控规则要按 IP、时间、属性再判一次。框架把这些做成可替换的部件：授予存储是 Provider，收窄与来源是两个对称的扩展点，条件引擎独立于存储；检查器按固定顺序把它们串起来，任何一环读不到数据都是失败而不是「不持有」。

## 授予模型

授予是 `PermissionGrant`：主体、权限码、可选租户。

| 成员 | 说明 |
| --- | --- |
| `Subject` | `GrantSubject`，种类只有用户（`GrantSubject.ForUser(id)`，即 `user:<id>`）与角色（`GrantSubject.ForRole(name)`，即 `role:<name>`） |
| `Permission` | 权限码；写 `*`（`PermissionGrant.All`）匹配任何权限 |
| `TenantId` | `null` 的授予对任何租户生效，租户专属授予只对该租户生效 |

主体 ID、角色名与权限码都区分大小写。

`PermissionChecker` 判定「主体是否持有权限」的顺序：

1. 匿名主体直接不持有，不访问存储。
2. 查用户本身的授予（全局授予加当前租户的授予）。
3. 逐个查主体的角色授予，角色取类型为 `XiHan:Security:RoleClaim`（缺省 `role`）的声明值。
4. 存储都没命中时，按登记顺序查询各 `IPermissionGrantSource`。
5. 任一步命中即算授予命中，之后按登记顺序调用全部 `IPermissionFilter`，全部放行才算持有。

存储、来源、过滤器、检查器都返回 `Result`：存储的 `GetManyAsync` 是与入参等长的 `Result<IReadOnlyList<IReadOnlyCollection<string>>>`（检查器把用户与全部角色一次读出），来源的 `GetGrantsAsync` 是 `Result<IReadOnlyCollection<string>>`，`AllowsAsync` 与 `HasPermissionAsync` 是 `Result<bool>`。没有授予是成功的空集合，读不到是失败；检查器遇到第一个失败即原样返回。

::: warning 读不到不是没有
授予存储、来源或过滤器读不到数据时返回 `SECURITY_UNAVAILABLE_001`，调用以 503 结束。不要用空集合或 `false` 代替：那会被判定为不持有，客户端会以为权限被收回，网关也不会重试。自己调用 `HasPermissionAsync` 的地方同样要处理失败，不能把失败当作不持有。
:::

## 最小示例

内存实现从配置装载授予。配置里的授予都是全局的，值是逗号分隔的权限码；下面是 SimpleApp 的授予，主体见[认证](/security/authentication)：

```json
{
  "XiHan": {
    "Security": {
      "InMemory": {
        "Grants": {
          "role": {
            "editor": "orders.read,orders.write",
            "viewer": "orders.read",
            "event-relay": "events.deliver"
          }
        }
      }
    }
  }
}
```

`alice`（角色 `editor`）调用 `orders.create` 通过；`bob`（角色 `viewer`）调用 `orders.create` 得到 `SECURITY_DENIED_004`（HTTP 403）；匿名调用得到 `SECURITY_DENIED_001`（HTTP 401）。

运行时增删授予用授予管理 `IPermissionGrantAdministration`（两个实现都提供）：`GrantAsync` / `RevokeAsync` 批量逐项返回结果，容量满时为 `SECURITY_UNAVAILABLE_002`，撤销不存在的授予为 `SECURITY_NOTFOUND_002`；单条用同名扩展方法。租户专属授予只能这样添加：`await grants.GrantAsync(new PermissionGrant(GrantSubject.ForUser("bob"), "orders.write", "acme"), cancellationToken)`。

## 两种授予存储

| | `Security.InMemory` | `Security.Database` |
| --- | --- | --- |
| 装载 | 启动时读配置，运行时经 `IPermissionGrantAdministration` 增删 | 数据库表，经 `IPermissionGrantAdministration`（`PermissionGrantRecordStore`）批量授予、撤销 |
| 上限 | `XiHan:Security:InMemory:MaxGrants`，缺省 10000 | 无条数上限；缓存 `CacheSize` 缺省 10000 条 |
| 读取 | 进程内查表 | 按（主体，租户）一次查询，结果缓存 `CacheTimeToLiveSeconds`（缺省 30 秒） |
| 适用 | 开发、测试、授予随配置提交 | 授予由管理后台维护、多实例共享的生产应用 |

## 数据库授予：Security.Database

`Security.Database` 只换授予存储：检查器、策略目录与 Authorize 阶段中间件与内存实现是同一套，`IPermissionFilter` 与 `IPermissionGrantSource` 照常生效。它只提供 `security.authorization@1`，不提供认证：与 `Security.InMemory` 同时装配时顶替它的授权部分，内存实现只剩 ApiKey 认证；认证也可以换成 `Security.Jwt` 或 `Security.OpenIdConnect`。

```bash
dotnet add package XiHan.Framework.Security.Database --prerelease
```

本包必须和一个持久化 Provider 一起装配：

```csharp
using SqlSugar;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Security.Database;
using XiHan.Framework.Security.Jwt;

var builder = WebApplication.CreateBuilder(args);

// JWT 认证 + 数据库授予；Standard 组合包里的内存持久化与内存安全都被顶替
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<JwtSecurityFeature>()
    .UseFeature<DatabaseSecurityFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

`Persistence.EntityFrameworkCore` 的应用在自己的 `DbContext` 里 `modelBuilder.Entity<PermissionGrantRecord>()` 登记实体并 `ApplyPersistenceConventions(Database)`，四列的联合唯一索引由实体上的 `[EntityIndex]` 建出，见 [Security.Database](/packages/security-database)。

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Security:Database:CacheSize` | `int` | 10000 | 缓存条目上限，每个（主体，租户）组合一条；满时整体清空 |
| `XiHan:Security:Database:CacheTimeToLiveSeconds` | `double` | 30 | 每条缓存的存活时间；`0` 表示不过期，只靠失效与容量清空 |

读写分成两个类型，与数据库租户目录同一套做法：`DatabasePermissionGrantStore` 是单例 `IPermissionGrantStore`，Authorize 阶段经检查器查它，它在根提供者的新作用域里直接开只读工作单元读仓储（配了从库也读主库），结果进有界的 TTL 缓存；`PermissionGrantRecordStore` 是作用域服务，管理操作在 Transaction 阶段内用调用方的工作单元批量授予、撤销，逐项返回结果，写入后失效涉及主体的缓存：

```csharp
using XiHan.Framework.Security.Abstractions.Permissions;

// 作用域服务，在操作的处理器里使用
public sealed class RoleAdministration(IPermissionGrantAdministration grants)
{
    // 一次授予多项：已存在的返回成功且不重复写
    public ValueTask<IReadOnlyList<Result>> SetUpEditorAsync(string tenantId, CancellationToken cancellationToken)
        => grants.GrantAsync(
        [
            new PermissionGrant(GrantSubject.ForRole("editor"), "orders.read"),
            new PermissionGrant(GrantSubject.ForRole("editor"), "orders.write", tenantId),
        ], cancellationToken);

    // 不存在的授予逐项返回 SECURITY_NOTFOUND_002
    public ValueTask<IReadOnlyList<Result>> RevokeWriteAsync(string tenantId, CancellationToken cancellationToken)
        => grants.RevokeAsync([new PermissionGrant(GrantSubject.ForRole("editor"), "orders.write", tenantId)], cancellationToken);
}
```

`ListAsync(PermissionGrantQuery)` 分页列出授予，返回 `PermissionGrantPage`（`Items` 与总数 `Total`）：按 `Subject` 过滤主体，按 `Tenant` 过滤租户（`GrantTenantFilter.Any` 缺省不过滤、`Global` 只要全局、`Of(tenantId)` 只要该租户），`Skip` / `Take`（最多 1000）越界返回 `SECURITY_VALIDATION_005`。行为要点：

- 授予是平台级数据：`PermissionGrantRecord` 不实现 `IMultiTenant`，不受当前调用租户过滤。全局授予以空字符串（`PermissionGrantRecord.GlobalTenantId`）落库，契约边界仍以 `PermissionGrant.TenantId == null` 表示全局；空白租户是编程错误，在访问数据库前抛出 `ArgumentException`。
- 授予已存在的返回成功且不重复写；两个调用同时授予同一项时后插入的那一批返回 `PERSISTENCE_CONFLICT_003`、调用回滚，重试即成功。撤销不存在的授予返回 `SECURITY_NOTFOUND_002`。
- 缓存条目按 `CacheTimeToLive` 过期，条目数达到 `CacheSize` 时整体清空；写入在事务提交后才对其他调用可见，失效与提交之间的窗口以及其他实例的缓存都靠存活时间收敛。
- 数据库故障返回 `SECURITY_UNAVAILABLE_001`，调用以 503 结束，不会落到 403；失败不进缓存，恢复后下一次判定立即重新查库。
- 表结构由应用管理，`InitTables` 只用于开发与测试；唯一索引 `(SubjectKind, SubjectId, Permission, TenantId)` 由 `[EntityIndex]` 建出。被选为授权能力时同样登记启动核对 `PolicyCatalog.EnforceOn`。

## 收窄：IPermissionFilter

`IPermissionFilter` 在授予命中之后再判定一次，返回成功的 `false` 即视为不持有。它只能拿走权限、不能给出权限，用于套餐白名单、显式拒绝、委托到期这类授予之外的约束。被收窄的权限在权限策略上同样返回 `SECURITY_DENIED_004`，调用方不需要区分「没授予」与「被收窄」。

```csharp
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Permissions;

public sealed class EditionPermissionFilter : IPermissionFilter
{
    // 基础版套餐只开放查询；真实应用按租户从套餐表读取并缓存
    private static readonly HashSet<string> _basicEdition = new(StringComparer.Ordinal) { "orders.read" };

    public ValueTask<Result<bool>> AllowsAsync(SecurityContext security, string permission, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success(security.Tenant is null || _basicEdition.Contains(permission)));
}
```

在特性的 `Configure` 里按单例登记：`context.Services.AddSingleton<IPermissionFilter, EditionPermissionFilter>()`。

- 检查器按登记顺序逐个调用，任一返回 `false` 即不持有，后面的不再调用；匿名主体与授予未命中时不调用过滤器。
- 过滤器收到的是具体权限码；主体持有 `*` 时同样会被收窄。
- 套餐表读不到时返回失败（`Error.Unavailable(SecurityErrors.StoreUnavailable, SecurityMessages.GrantStoreUnavailable)`），调用以 503 结束；跳过它等于放行，返回 `false` 又会变成 403。

## 授予来源：IPermissionGrantSource

`IPermissionGrantSource` 与过滤器对称：给出主体在授予存储之外的额外授予（权限码或 `*`），用于角色继承、限时委托这类不落在主体自己授予行上的授予。来源只能加，给出的授予（包括 `*`）同样经过全部过滤器收窄。

```csharp
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Permissions;

public sealed class ManagerInheritsEditorGrantSource(SecurityOptions options) : IPermissionGrantSource
{
    // manager 角色继承 editor 的权限；继承关系通常来自应用的角色表
    private static readonly IReadOnlyCollection<string> _editorPermissions = ["orders.read", "orders.write"];

    public ValueTask<Result<IReadOnlyCollection<string>>> GetGrantsAsync(SecurityContext security, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success(security.Principal.HasClaim(options.RoleClaim, "manager") ? _editorPermissions : []));
}
```

同样按单例登记：`context.Services.AddSingleton<IPermissionGrantSource, ManagerInheritsEditorGrantSource>()`。检查器只在用户与角色的存储授予都没命中时才查询来源，任一来源命中即止；匿名主体不查询。继承关系读不到时返回失败，不要返回空集合。来源拿到整个安全上下文，继承关系一改就生效，不依赖令牌刷新。应用替换 `IPermissionChecker` 时，必须同样并上全部授予来源、并在授予命中之后施加全部过滤器（规范 RFC-0008）。

## 条件引擎

条件是访问策略、权限条件与约束规则共用的属性判定，引擎在 `XiHan.Framework.Security.Abstractions.Conditions`，与存储无关：应用提供属性来源与条件的存储，引擎负责登记、编译、求值。框架没有把条件接进 Authorize 阶段，应用在自己的策略、过滤器或中间件里调用编译器与求值器。

条件集 `ConditionSet` 由组 `ConditionGroup` 构成，组由子句 `ConditionClause`（属性名、运算符、条件值原文、是否取反、标识）构成；组内是「且」，组间是「或」。没有非空组的条件集求值恒为命中，需要拒绝空条件的场景在保存时拦下。运算符与值种类的整数值是持久化契约，不会调整：

| 运算符（值） | 可用于 |
| --- | --- |
| `Equals`（0）、`NotEquals`（1）、`In`（8）、`NotIn`（9）、`IsNull`（13）、`IsNotNull`（14） | 任何属性 |
| `GreaterThan`（2）、`GreaterThanOrEquals`（3）、`LessThan`（4）、`LessThanOrEquals`（5）、`Between`（10） | 数字、日期时间、日期、时刻 |
| `Contains`（6）、`NotContains`（7） | 多值属性与字符串（单值字符串按子串） |
| `StartsWith`（11）、`EndsWith`（12） | 字符串 |

| 值种类（值） | 比较与写法 |
| --- | --- |
| `String`（0） | 不区分大小写的序数比较 |
| `Number`（1）、`Boolean`（2） | 十进制；`true` / `false` / `1` / `0` |
| `DateTime`（3）、`Date`（4）、`TimeOfDay`（5） | ISO 8601，不带偏移的按编译时区的墙钟解释；`HH:mm[:ss]`，区间下限大于上限表示跨零点 |
| `IpAddress`（6） | IPv4 只收四段点分，IPv4 映射的 IPv6 还原为 IPv4；集合可写 CIDR，主机位必须为零 |

集合写 JSON 数组，或用逗号、全角逗号、换行分隔；区间写 `下限,上限`、`[下限,上限]` 或 `{"min":下限,"max":上限}`。`ConditionOperatorRules.AllowedFor(descriptor)` 给出某个属性能用的运算符，编辑界面据此列出选项。

属性用 `services.AddConditionAttributes(...)` 登记，它同时确保 `IConditionAttributeRegistry`、`IConditionCompiler`、`IConditionEvaluator` 三项单例已登记。登记名不区分大小写，可带别名；以 `.*` 结尾的是一族属性（如 `resource.route.*`），按最长前缀匹配，精确登记优先；重名与别名冲突在登记表首次解析时抛出。

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Security.Abstractions.Conditions;

public static class OfficeHoursCondition
{
    public static void Register(IServiceCollection services)
        => services.AddConditionAttributes(
            new ConditionAttributeDescriptor("environment.ip", ConditionValueKind.IpAddress),
            new ConditionAttributeDescriptor("environment.time", ConditionValueKind.TimeOfDay),
            new ConditionAttributeDescriptor("subject.roleCodes", ConditionValueKind.String, IsMultiValued: true));

    // 内网或 auditor 角色，且在 09:00–18:00 之间：组内为且，组间为或
    public static readonly ConditionSet Conditions = new(
    [
        new ConditionGroup([
            new ConditionClause("environment.ip", ConditionOperator.In, "10.0.0.0/8, 192.168.0.0/16"),
            new ConditionClause("environment.time", ConditionOperator.Between, "09:00,18:00"),
        ]),
        new ConditionGroup([
            new ConditionClause("subject.roleCodes", ConditionOperator.Contains, "auditor"),
            new ConditionClause("environment.time", ConditionOperator.Between, "09:00,18:00"),
        ]),
    ]);
}
```

编译与求值各一步：`compiler.Compile(conditions, new ConditionCompileOptions { TimeZone = TimeZoneInfo.Local })` 返回 `CompiledConditionSet`，`Errors` 非空就在保存前拒绝（每条错误带 `SECURITY_VALIDATION_002` 与说明原因的消息键）；`await evaluator.EvaluateAsync(compiled, source, explain: false, cancellationToken)` 返回 `ConditionEvaluationResult`，看 `IsMatched`。`DictionaryConditionAttributeSource` 用于试算与测试；运行时应用实现 `IConditionAttributeSource`，按规范名返回 `ConditionAttributeValue.Of(value)`、`ConditionAttributeValue.Missing`（取不到）或 `ConditionAttributeValue.Deferred`（暂不可得），`MemoizingConditionAttributeSource` 让同一次求值里每个属性只取一次。求值只向来源取被引用的属性（`CompiledConditionSet.ReferencedAttributes`）。

| 结果 | 子句 | 组 | 条件集 |
| --- | --- | --- | --- |
| `Matched` 命中 | 判定成立 | 全部子句命中 | 任一组命中 |
| `NotMatched` 不命中 | 判定不成立 | 任一子句不命中或无法判定 | 没有组命中也没有组待定 |
| `Indeterminate` 无法判定 | 属性取不到（`SECURITY_NOTFOUND_003`）或值不是登记的种类（`SECURITY_VALIDATION_003`）；按不命中计，取反不翻转 | — | — |
| `Pending` 待定 | 属性暂不可得（`SECURITY_UNAVAILABLE_004`） | 没有不命中、有待定 | 没有组命中、有组待定 |

`IsNull` / `IsNotNull` 把取不到、空列表、全是空白的字符串当空，可以取反。多值属性按任一元素判定。编译出错的子句保留在编译结果里、求值恒为无法判定，库里的脏数据不会让整个条件集失效。`explain: true` 时逐组逐子句给出结果与原因，用于编辑界面的试算。

## 错误码

| 错误码 | 含义 | HTTP |
| --- | --- | --- |
| `SECURITY_DENIED_004` | 缺少权限，含被 `IPermissionFilter` 收窄 | 403 |
| `SECURITY_NOTFOUND_002` | 要撤销的授予不存在（`Security.Database` 的逐项结果） | — |
| `SECURITY_UNAVAILABLE_001` | 授予存储、授予来源或收窄过滤器读不到数据，不当作不持有 | 503 |
| `SECURITY_UNAVAILABLE_002` | 内存授予存储容量已满 | — |
| `SECURITY_VALIDATION_002` / `SECURITY_VALIDATION_003` | 条件无效（属性未登记、运算符与值种类不搭配、值解析不了）/ 属性值不是登记的种类 | — |
| `SECURITY_NOTFOUND_003` / `SECURITY_UNAVAILABLE_004` | 取不到条件属性的值 / 属性暂不可得 | — |
| `PERSISTENCE_CONFLICT_003` | 两个调用同时写入同一条数据库授予，后者回滚，重试即成功 | 409 |

条件相关的四个错误码出现在编译结果与说明里，不直接成为调用结果。

## 常见问题

### 配置里能写租户专属的授予吗

不能，`XiHan:Security:InMemory:Grants` 里的授予都是全局的。租户专属授予在两个实现里都经 `IPermissionGrantAdministration.GrantAsync` 带 `tenantId` 写入。用户授予、角色授予、来源任一命中即命中，没有「冲突」；要拿走权限用 `IPermissionFilter`。

### 条件引擎怎样接进授权

框架没有把它接进 Authorize 阶段，因为属性来源（当前 IP、时间、资源属性）由应用定义。常见做法是写一个自定义 `IPolicy` 或 `IPermissionFilter`：从调用来源取 IP（见 [IP 归属地](/security/ip-region)）、从主体取角色、从时钟取时间，组成 `IConditionAttributeSource`，用 `IConditionEvaluator` 求值，`Matched` 放行、其他拒绝；`Pending` 要么当拒绝，要么补齐属性后再求值一次。

### 条件保存时怎样提示用户写错了

先 `IConditionCompiler.Compile`，`Errors` 非空就拒绝保存，每条错误带 `SECURITY_VALIDATION_002` 与具体原因的消息键，翻译后展示，见[全球化和本地化](/common/localization)。

## 相关包与下一步

- 包参考：[Security.Abstractions](/packages/security-abstractions)、[Security.InMemory](/packages/security-inmemory)、[Security.Database](/packages/security-database)、[Persistence.SqlSugar](/packages/persistence-sqlsugar)、[Persistence.EntityFrameworkCore](/packages/persistence-entityframeworkcore)
- 规范：RFC-0008《认证与授权》
- 相关章节：[授权与权限](/security/authorization)、[数据库租户目录](/multi-tenancy/database)、[仓储读写](/persistence/repositories)
- 下一步：[IP 归属地](/security/ip-region)
