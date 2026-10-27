# ADR-0035 SqlSugar 规约里可空值类型与 null 的比较改写成 `HasValue` 判定

## 状态

已采纳（`spec/rfcs/0005-persistence.md` 修订）。

## 日期

2026-10-28

## 背景

BasicApp 6.0 的 N1 第一批发现：规约 `s.RevokedAt == null`（`DateTimeOffset?`）经 `Persistence.SqlSugar` 生成 `WHERE (revoked_at = null)`——永假、静默返回空集；`string?` 的 `== null` 与 `!s.RevokedAt.HasValue` 则正确生成 `IS NULL` / `NOT (… IS NOT NULL)`。SQLite 复现，PostgreSQL 同理。原因在表达式树：C# 把可空值类型与 null 的 `==` 编译成带 `op_Equality` 方法的提升比较（`Equal(RevokedAt, Constant(null), method: DateTimeOffset.op_Equality)`），SqlSugar 按运算符方法翻译成 `=`；引用类型的 `== null` 没有运算符方法，走的是它的空值分支。这是静默错数据，比抛错更危险，而且应用无法从 `IRepository` 的契约看出区别。

## 备选

1. 应用一律写 `HasValue`，README 提醒：契约靠约定，漏写一处就静默空集，一致性套件也测不到。
2. `Persistence.SqlSugar` 在交给 SqlSugar 前用 `ExpressionVisitor` 把可空值类型与 null 的 `==` / `!=` 改写成 `!HasValue` / `HasValue`：对应用透明，InMemory 与 EF Core Provider 本来就按存在性判定，三者一致。
3. 改写放在 `Domain.Specification<T>`（所有 Provider 共享）：把 Provider 的缺陷塞进领域层，EF Core 与内存实现不需要它。
4. 给 SqlSugar 提 issue 等上游修：不受控，期间仍错数据。

## 决策

采用 2，附带上游反馈不在本 ADR 范围。

- `NullableComparisonRewriter`（`Persistence.SqlSugar` 内部）：`VisitBinary` 对 `Equal` / `NotEqual`，一侧去掉 `Convert` 包装后是 `Nullable<T>`、另一侧求值为 null 时，改写成 `Not(Property(HasValue))` / `Property(HasValue)`；左右互换都认。null 一侧的求值不编译：常量直接取、闭包字段与静态成员沿链取值、`Convert` 透传；方法调用、参数或宿主为 null 的实例成员不求值（交给 SqlSugar 自己翻译）。值非 null 时不动。
- `SqlSugarRepository<TEntity, TKey>.Filtered` 对 `EntityQuery.Filter.Criteria` 先改写再 `Where`；框架自己拼的 `IdEquals` / `NotDeleted` / `TenantIs` 不涉及可空值类型，不经改写。
- 一致性套件新增 `List_NullableValueTypeComparedWithNull_FiltersByPresence`：字面量 null、闭包里的 null 变量、`null == x`、`!x.HasValue`、与 `||` / `&&` 组合，InMemory、SqlSugar（SQLite）、EF Core 三个 Provider 都通过；PostgreSQL 按同一套件在有服务器时跑。
- RFC-0005 的 `ISpecification<T>` 行与一致性段落写明：可空值类型与 null 的比较按存在性翻译，Provider 不得译成 `= NULL`。

## 理由

- 规约是领域层写的，`IRepository` 的契约不应要求调用方知道某个 Provider 的翻译缺陷；改写在 Provider 内部正是适配层的职责。
- 不编译求值：规约每次查询都会经过改写，`Expression.Compile` 的代价不该落在每条查询上；闭包与常量覆盖了实际会出现的 null 来源。
- 放一致性套件而不是只放 SqlSugar 测试：三个 Provider 对同一规约给出同一结果才是契约，EF Core 与内存实现也要被这条约束钉住。

## 影响

- 行为变化只在原先错的地方：`DateTimeOffset?` / `int?` / `Guid?` 等与 null 的比较从永假（`== null`）或永真（`!= null`）变成按存在性过滤。
- 公开 API 不变；`NullableComparisonRewriter` 是内部类型。
- BasicApp 侧原先改成 `HasValue` 的规约可以保留，两种写法现在等价。
