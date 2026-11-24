# ADR-0288 其余四个 Stable 位置记录改成具名成员

## 状态

已采纳（内核的 `SecurityContext`、`TenantContext` 与 `Security.Abstractions` 的 `PermissionGrant`、`AuthorizationRequirement` 已改，框架内的构造处跟着改；规范只改 RFC-0008 里一处记法，RFC-0001「数据类型的形状」已经是这条规则）。

## 日期

2026-11-24

## 背景

- ADR-0142 定下「会演进或跨 Provider 边界的数据类型不用位置记录」，规则写进 RFC-0001。ADR-0266 照它改了对象存储的 `BlobSignedUrl`，ADR-0267 改了内核的 `BindingValue` 与 `MiddlewareDescriptor`，并在「影响」里列出还剩四个「会演进、建议在正式版之前照这里的方式改」的 `Stable` 位置记录，留给仓库所有者定。这次定下来：四个都改。
- 四个类型原先的形状：
  - `SecurityContext(Principal Principal, TenantContext? Tenant)`（内核）：每次调用都带着它，认证处理器、宿主、插件协议与测试都会构造。委托（代表谁）、认证强度这类成员加得上去。
  - `TenantContext(string Id)`（内核）：位置参数之外已经追加了 `DisplayName` 一个 `init` 成员，构造与解构只覆盖 `Id`。版本、区域这类成员加得上去。
  - `PermissionGrant(GrantSubject Subject, string Permission, string? TenantId)`（`Security.Abstractions`）：另有一个两参的构造表示平台授予。它跨授予存储的 Provider 边界（`IPermissionGrantStore`、`IPermissionGrantAdministration`），过期时刻、授予来源、条件都是加得上去的成员。
  - `AuthorizationRequirement(string Policy, ResourceName? Resource)`（`Security.Abstractions`）：另有一个单参的构造表示不针对具体资源。
- 位置记录每加一个位置参数，构造与 `Deconstruct` 的签名都要变；不加位置参数、只追加 `init` 成员，解构又看不到新成员。后两个类型还用「多一个构造重载」表达可缺省的末位参数，每加一个可缺省的成员重载数就要翻倍。四个都是 `Stable`，5.0.0 之后同一大版本内不能再破坏（ADR-0131）。
- 四个类型都在构造时校验（主体不为 `null`、租户 ID 与策略名不为空白、授予的主体不是缺省值、权限码不为空白、授予的范围不是空白）。校验写在位置参数对应的只读属性的初始值上，所以这些成员原先没有 `init`，`with` 换不了它们。

## 备选

1. **留到下一个大版本再改**：否决。四个类型现在都没有基线（`PublicAPI.Shipped.txt` 是空的），5.0.0 之前改不需要豁免；正式版之后要等 6.0。
2. **构成身份的成员经构造给出、其余走初始化器**（RFC-0001 留的那条路；ADR-0267 给 `TenantContext(id)` 与 `AuthorizationRequirement(policy)` 提过这种改法，好处是 `new TenantContext(id)` 这类调用点不用改）：否决。
   - `SecurityContext` 与 `PermissionGrant` 没有哪一项是身份：主体与租户是一次调用并列的两个事实，一条授予的身份是主体、权限码与范围三项合起来（存储按三项去重与撤销）。
   - 四个类型一半留构造、一半不留，使用者要逐个记哪个类型的哪一项经构造给。与 `BlobSignedUrl`、`BindingValue`、`MiddlewareDescriptor`、`AuditEvent`、`MessageDelivery` 一样全部走初始化器，调用点每个值都带着成员名。
   - 代价是框架内约 130 处构造要改，`new TenantContext(id)` 变成 `new TenantContext { Id = id }`。
3. **可空的成员也标 `required`**（`SecurityContext.Tenant`、`PermissionGrant.TenantId`、`AuthorizationRequirement.Resource`；`BindingValue.Value` 是这么做的，理由是「没有主值要构造方明说」）：否决。
   - `PermissionGrant.TenantId` 与 `AuthorizationRequirement.Resource` 原先就可以不给：两参与单参的构造分别表示平台授予与不针对具体资源（ADR-0197 把两参构造定为平台授予）。标 `required` 等于顺手改了契约。
   - `SecurityContext.Tenant` 原先必须写出来，但无租户是它的常态：`Anonymous`、单租户应用的每一次调用、Tenant 阶段之前的每一个上下文都没有租户。
   - 留下的风险：换主体时另建上下文而忘了带上已有的租户，得到的是不带租户的上下文，编译器不再提醒。成员的文档写明了这一条，框架自己的两处（`AuthenticationMiddleware`、`InvocationAuthenticationHandler`）都显式带上 `Tenant`。
4. **加工厂方法顶替删掉的构造**（`PermissionGrant.Platform(subject, permission)`、`TenantContext.Of(id)` 之类）：否决。初始化器已经带着成员名说清了同一件事；工厂是第二条构造途径，也是要守到下个大版本的 `Stable` 签名。已有的、语义明确的静态成员照旧保留：`SecurityContext.Anonymous`、`AuthorizationRequirement.Authenticated` 与 `Permission(code)` / `Permission(code, resource)`。
5. **照 `BindingValue` 不在初始化器里校验**：否决。`BindingValue` 原先就不校验；这四个原先在构造时校验，去掉是行为变化。RFC-0001 的规则是「初始化器在设值时校验，`with` 走同一校验」。
6. **顺手把校验收紧到文法**（`TenantContext.Id` 与 `PermissionGrant.TenantId` 按 RFC-0007 的租户 ID 文法、`PermissionGrant.Permission` 按权限码文法）：这次不做。原先只拒绝空白；文法由 Tenant 阶段与权限登记表各自把关，在数据类型上收紧是行为变化，要另定。
7. **保留 `Deconstruct`**：否决。它正是签名随成员数变的那一半；仓库里没有任何解构与位置模式。
8. **成员的声明次序照 `ToString()` 原来的输出排**（`SecurityContext` 原先先打印 `Tenant`、`AuthorizationRequirement` 原先先打印 `Resource`，因为另一项是在记录体里重新声明的）：否决。那个次序是重新声明带出来的副作用；改后按自然次序声明（`Principal`、`Tenant`；`Policy`、`Resource`）。代价见「影响」的行为变化。

## 决策

1. `SecurityContext` 改成初始化器形状：`Principal` 是 `required` 的 `init` 成员，`Tenant` 是可选的 `init` 成员（缺省 `null`），没有位置构造与 `Deconstruct`。`Anonymous` 不变。
2. `TenantContext` 改成初始化器形状：`Id` 是 `required` 的 `init` 成员，`DisplayName` 照旧是可选的 `init` 成员，没有位置构造与 `Deconstruct`。
3. `PermissionGrant` 改成初始化器形状：`Subject` 与 `Permission` 是 `required` 的 `init` 成员，`TenantId` 是可选的 `init` 成员（缺省 `null`，即平台授予），三参与两参的构造、`Deconstruct` 都删除。常量 `All`、`AnyTenant` 与 `VisibleScopes` 不变。
4. `AuthorizationRequirement` 改成初始化器形状：`Policy` 是 `required` 的 `init` 成员，`Resource` 是可选的 `init` 成员（缺省 `null`），两参与单参的构造、`Deconstruct` 都删除。`Authenticated`、`Permission(code)` 与 `Permission(code, resource)` 不变。
5. 原先构造时的校验原样搬进 `init` 访问器，消息不变，`with` 走同一校验：

   | 成员 | 不合法的值 | 抛出 |
   | --- | --- | --- |
   | `SecurityContext.Principal` | `null` | `ArgumentNullException` |
   | `TenantContext.Id` | `null`、空或空白 | `ArgumentException` |
   | `PermissionGrant.Subject` | 缺省值（`default(GrantSubject)`） | `ArgumentException` |
   | `PermissionGrant.Permission` | `null`、空或空白 | `ArgumentException` |
   | `PermissionGrant.TenantId` | 空或空白（`null` 合法） | `ArgumentException` |
   | `AuthorizationRequirement.Policy` | `null`、空或空白 | `ArgumentException` |

6. 以后给这四个类型加成员只加可选的初始化成员。
7. 框架内的构造处机械改写，共 129 处（含目标类型推断的 `new(...)` 16 处），另有类型自己的静态成员 3 处。没有解构与位置模式。`with` 的用法（`SetTenant` 里的 `with { Tenant = tenant }`）不受影响。
   - 正式包 28 处：`Security.Conformance` 14、`Hosting.AspNetCore` 2、`MultiTenancy.Abstractions` 2、`Security.Abstractions` 2、`Security.InMemory` 2、`Workflow.Conformance` 2，`Persistence.Conformance`、`Plugins.Protocol`、`Security.Database`、`Workflow.Abstractions` 各 1。
   - 测试 101 处。
   - 按类型（正式包与测试合计）：`PermissionGrant` 64、`SecurityContext` 35、`TenantContext` 23、`AuthorizationRequirement` 7。
   - 原先写 `new SecurityContext(principal, null)` 的改成不写 `Tenant`；`PermissionGrant` 第三个参数显式写 `null` 的保留成 `TenantId = null`（作者是有意同两参的写法区分开的）。
   - 示例、基准、源生成器与分析器都不构造也不识别这四个类型，没有要改的。

## 理由

- 安全上下文是每次调用都带着的类型，授予是每个授予存储的 Provider 都要收发的类型；它们的构造签名一变，破坏面是全部认证处理器与全部授予存储。
- 四个类型的形状原先各不相同（纯位置、位置加 `init`、位置加构造重载），改后同一种：必需的 `required`、可缺省的可选，都经初始化器。
- 校验留在设值的地方，`with` 得到的值与初始化器得到的值受同样的约束；原先只读的成员改后可以经 `with` 换，换出不合法的值同样抛出。
- 都在 5.0.0 之前改，不需要豁免登记（ADR-0131）。

## 影响

- **公开 API 与迁移**（`PublicAPI.Unshipped.txt` 已同步；尚无基线，不需要 `CompatibilitySuppressions.xml`）。四个类型的位置构造、构造重载与 `Deconstruct` 删除，各新增无参构造；原先只读的 `SecurityContext.Principal`、`TenantContext.Id`、`PermissionGrant.Subject` / `Permission` / `TenantId`、`AuthorizationRequirement.Policy` 各新增 `init`。逐个类型的对照：

  | 原先 | 改成 |
  | --- | --- |
  | `new SecurityContext(p, t)` | `new SecurityContext { Principal = p, Tenant = t }` |
  | `new SecurityContext(p, null)` | `new SecurityContext { Principal = p }` |
  | `new TenantContext(id)` | `new TenantContext { Id = id }` |
  | `new TenantContext(id) { DisplayName = name }` | `new TenantContext { Id = id, DisplayName = name }` |
  | `new PermissionGrant(subject, permission)` | `new PermissionGrant { Subject = subject, Permission = permission }` |
  | `new PermissionGrant(subject, permission, tenantId)` | `new PermissionGrant { Subject = subject, Permission = permission, TenantId = tenantId }` |
  | `new AuthorizationRequirement(policy)` | `new AuthorizationRequirement { Policy = policy }` |
  | `new AuthorizationRequirement(policy, resource)` | `new AuthorizationRequirement { Policy = policy, Resource = resource }` |

  - 目标类型推断的写法同理：`PermissionGrant grant = new(subject, permission)` 改成 `new() { Subject = subject, Permission = permission }`。按大写参数名传参的（`Principal: …`）同样改成初始化器。
  - 位置解构与位置模式改成读属性与属性模式：`var (principal, tenant) = security` 改成读 `security.Principal`、`security.Tenant`；`grant is PermissionGrant(var subject, _, null)` 改成 `grant is { Subject: var subject, TenantId: null }`。
  - 不受影响：`SecurityContext.Anonymous`、`AuthorizationRequirement.Authenticated` / `Permission(...)`、`context.SetTenant(...)`、`with` 的写法，以及只读这些类型的代码（策略、权限检查器、审计）。
  - 二进制不兼容：按旧签名编译的程序集要重新编译。
- **行为变化**：
  - 原先只读的成员可以经 `with` 换了（`security with { Principal = other }`、`grant with { TenantId = "acme" }`），换成不合法的值抛出同构造时一样的异常。
  - 校验抛出的异常，`ParamName` 由成员名（`Principal`、`Id`、`Subject`、`Permission`、`TenantId`、`Policy`）变成 `value`（`init` 访问器的参数，同仓库里其余在初始化器里校验的类型）。异常类型与消息不变。
  - `ToString()` 的成员次序跟着声明次序变了两个：`SecurityContext { Principal = …, Tenant = … }`（原先 `Tenant` 在前）、`AuthorizationRequirement { Policy = …, Resource = … }`（原先 `Resource` 在前）。`TenantContext` 与 `PermissionGrant` 的输出不变。
  - 相等比较与哈希不变（仍按全部成员）。
- **序列化**：框架不序列化这四个类型，改动不碰任何线上或落盘的形态。
  - 没有哪个 `JsonSerializerContext` 直接或间接带着它们（已在全部正式包、测试、示例与基准的程序集里核对过源生成的类型信息）。
  - 授予落库走 `PermissionGrantRecord`、进缓存走 `GrantSetEntry`；主体与租户跨进程走 Baggage 的 `xihan.principal` / `xihan.tenant`；审计事件带的是 `PrincipalId` 与 `TenantId` 两个字符串。
  - 应用自己拿 `System.Text.Json` 序列化它们的（临时程序实测，反射与源生成两种方式结果相同）：
    - 成员名不变。`PermissionGrant` 与 `TenantContext` 的成员次序不变；`SecurityContext` 与 `AuthorizationRequirement` 的成员次序同 `ToString()` 一样变成声明次序。
    - 读回时缺了 `required` 的成员抛出 `JsonException`，不会得到 `null`；给了 `null` 或空白的由 `init` 的校验抛出 `ArgumentException`；可选成员缺了就是 `null`。
    - `PermissionGrant` 与 `AuthorizationRequirement` 原先各有两个公开构造，`System.Text.Json` 读不回来（`NotSupportedException`），改后读得回来。
- **规范**：RFC-0008「授权」第 1 条把 `AuthorizationRequirement(policy, resource?)` 的记法改成 `AuthorizationRequirement { Policy, Resource? }`（做法同 ADR-0151 改 `UserTaskPage { Items, Total }`），不改规范性内容。RFC-0001「数据类型的形状」已经是这条规则；RFC-0007 与 RFC-0015 只写了 `SecurityContext.Tenant`、`TenantContext.Id` 这些成员名，不涉及形状。
- **没有做的**：
  - 其余的 `Stable` 位置记录还有 6 个，都是 ADR-0267 列为「封闭的小值、建议不动」的那几个，这次没有改，改不改由仓库所有者定：
    - `Primitives`：`TypedId<TTag, TValue>(TValue Value)`（规范点名）。
    - `Kernel`：`ResourceName(string Partition, string Service, string? Tenant, string Type, string Id)`（规范点名，XRN 的五段是文法定死的）。
    - `Security.Abstractions`：`GrantSubject(string Kind, string Id)`（主体的种类与标识，两段式的键）。
    - `ObjectStorage.Abstractions`：`BlobContentRange(long Offset, long Length, long TotalLength)`（HTTP `Content-Range` 的三个数）。
    - `Localization.Abstractions`：`LocalizedText(string Text, string Locale)`（译文与命中的区域）。
    - `Persistence.Abstractions`：`Ordering<T>(Expression<Func<T, object?>> KeySelector, bool Descending)`（排序键与方向）。
  - `Preview` 的位置记录在全部正式包里有 49 个（内核与抽象包 26 个：ADR-0267 记的 25 个，加上之后新增的内核 `JsonTextProblem`），`Experimental` 的 12 个（`Caching.Remote` 的线上请求与响应），在各自的稳定性期间可以带豁免修改，不在这次的范围。
  - 可空成员是否标 `required`（备选 3）与校验是否收紧到文法（备选 6）。
- **测试**：
  - 内核：`SecurityContext` 以初始化器构造、租户不给即无租户、`with` 换主体留住租户、主体为 `null` 时初始化器与 `with` 都抛出；`TenantContext` 以初始化器构造、`with` 换显示名与 ID、ID 为 `null` 或空白时初始化器与 `with` 都抛出。
  - `Security.Abstractions`：`PermissionGrant` 范围不给即平台授予（与显式写 `null` 相等）、`with` 换范围、三个成员不合法时初始化器与 `with` 都抛出；`AuthorizationRequirement` 资源不给即不针对具体资源、三个快捷写法与初始化器给出相等的值、策略名不合法时初始化器与 `with` 都抛出。
  - 四个类型只有无参构造、没有 `Deconstruct`，标了 `required` 的成员正好是上面定的那几项。
- **文档**：包 README 里没有这四个类型的构造写法，不用改。文档站 `security/grants`、`security/authorization`、`packages/security-inmemory`、`extending/provider` 里 `new PermissionGrant(...)` 的 7 处写法与「`AuthorizationRequirement` 的构造参数」一句、更新日志待改。
