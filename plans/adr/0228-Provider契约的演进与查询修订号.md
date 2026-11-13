# ADR-0228 Provider 契约的演进与查询修订号

## 状态

已采纳（RFC-0001「稳定性级别」新增「Provider 契约的演进」；错误码登记 `KERNEL_VALIDATION_007`）。

## 日期

2026-11-13

## 背景

5.0 API 审查的 R2（`plans/5.0-API审查.md`）：Provider 接口在 11 月里多次加成员（`IJobStore`、三个工作流存储、`IRepository`、`IBlobStore`、`IAuditTrail`）；存储直接接收查询类型，给查询加过滤字段时，旧 Provider 会静默忽略。

- GA 之后，同一大版本里抽象包与 Provider 包可以不同步升级。应用升级了抽象包、Provider 还是旧的时：
  - 已有接口上加的成员让旧 Provider 在运行时缺方法。
  - 查询上加的过滤项旧 Provider 读不到。比如审计查询加一个按资源前缀过滤，旧的数据库实现照旧返回全部事件，调用方以为已经过滤过。
- 旧 Provider 是按旧版查询类型编译的，运行时它根本不知道新字段存在，只靠它自己察觉不了。
- 「给 Provider 要实现的接口加抽象成员算破坏」已写在 RFC-0001，但没说新能力该怎么加。

## 备选

新能力：

1. **可选接口**：采纳。先例已有 `ICacheStoreInspector`、`IBlobMultipartStore`、`IPermissionGrantAdministration`、`ITenantAdministration`，调用方按类型判断，旧 Provider 不受影响。
2. 带默认实现的接口成员：否决。默认实现不是静默降级就是抛出，前者掩盖缺失，后者把编译期能发现的缺失推到运行期。
3. 照旧往已有接口加成员、登记豁免：只留给每个 Provider 都必须有、缺了契约就不成立的能力（如 ADR-0215 的比较后重设到期）。

查询的新条件：

1. **查询带修订号常量，Provider 以编译时取到的常量核对**：采纳。
   - .NET 的 `const` 在编译时写进调用方程序集，旧 Provider 带着的就是旧值。
   - 核对的代码在新版抽象包里，知道全部条件。
   - 没用到新条件的查询所需修订号不变，旧 Provider 照常服务，只有真用到新条件时才拒绝。
2. 查询列出用到的条件名，Provider 声明认识的条件名：否决。API 更大，Provider 容易把抽象包里的「全部条件」直接当作自己认识的，等于没核对。
3. 抽象包给出接口版本号、Provider 声明实现到哪个版本：否决。粒度在接口而不在条件，新增任何条件都让旧 Provider 整体失效。
4. 只写规则、不给机制：否决。没有机制，Provider 无从察觉不认识的条件，规则无法落实。

## 决策

1. **规则**（RFC-0001「Provider 契约的演进」）：
   - 新能力走可选接口；不在已有的 Provider 接口上加成员；不用带默认实现的接口成员补能力。
   - 交给 Provider 的查询带修订号，Provider 在入口核对。
   - 选项记录新增的字段，被旧 Provider 忽略时必须仍然安全；做不到的走可选接口。
2. **查询类型**：12 个交给 Provider 的查询各加 `const int Revision = 1`（5.0 的全部条件属于修订 1）与 `Validate(int supportedRevision)`。
   - 这 12 个是 `AuditQuery`、`CacheKeyQuery`、`TenantQuery`、`BlobQuery`、`EntityQuery<T>`、`TableListQuery`、`JobRunQuery`、`PermissionGrantQuery`、`WorkflowDefinitionQuery`、`WorkflowInstanceQuery`、`UserTaskQuery`、`KnowledgeSearch`。
   - 原有的 `Validate()` 改为这个签名，先核对修订号，再做原来的校验。
   - 所需修订号由私有方法给出，5.0 恒为 1。以后新增条件时改为按设置了的条件取最高的修订号；不直接返回 `Revision`，否则常量一升，不用新条件的查询也会被旧 Provider 拒绝。
3. **内核**：`QueryRevision.Check(query, required, supported)`，所需修订号更高时返回 `KERNEL_VALIDATION_007`（参数 `query`、`required`、`supported`）。消息键 `kernel.query.revision_unsupported`。
4. **Provider**：框架里全部实现在入口传入编译时的常量。
   - 返回 `Result` 的返回校验错误。
   - 仓储的 `ListAsync` / `CountAsync` 不返回 `Result`，以同一错误码抛出 `FrameworkException`，版本不配套是部署错误。
5. **门禁**：架构测试 `QueryRevisionTests`。
   - 每个带修订号的查询，`Validate(Revision - 1)` 都返回 `KERNEL_VALIDATION_007`。
   - 实现了接收查询的框架接口方法的类型，源文件里引用该查询 `Revision` 的次数不少于它实现的这类方法数。
   - 豁免两类实现：一是与查询同一程序集的，它们总是和查询一起编译，多是转给存储的服务；二是工具链包（一致性套件、测试支持）里的，随框架一起编译，理由同 ADR-0157。
   - 门禁在故意少核对一处时失败，已验证。
6. **远程 Provider**：跨进程时请求里带所需修订号，由插件核对。现有远程操作集都不传查询，暂不涉及线上格式；以后加带查询的远程操作时一并加这个字段。

## 影响

- 破坏性变更（5.0 尚未正式发布，均在 Unshipped）：
  - 已有 `Validate()` 的 7 个查询改为 `Validate(int supportedRevision)`。
  - 自己实现存储接口的 Provider 要在入口加一行核对：`query.Validate(XxxQuery.Revision)`；仓储写成 `query.Validate(EntityQuery<TEntity>.Revision).ThrowIfFailure()`。
- 5.0 里核对总是通过；它的作用在 GA 之后：小版本给查询加条件时，没升级的 Provider 会明确失败，不会给出放宽了的结果。
- 以后给查询加条件的步骤：常量加 1，在所需修订号的计算里写明新条件属于新修订号，各框架 Provider 实现新条件。
- 内核公开类型加 1（`QueryRevision`）。
