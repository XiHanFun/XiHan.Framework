# ADR-0035 SqlSugar 规约里可空值类型与 null 的比较改写成 `HasValue` 判定

## 状态

已采纳（`spec/rfcs/0005-persistence.md` 修订）。2026-11-01 修订：判空改写成 `SqlFunc.EqualsNull`，逻辑非下推到叶子，见文末「修订」。

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

## 修订（2026-11-01）：判空与取反嵌套在与 / 或条件里

### 背景

消息分区给发件箱加到期判定时发现：规约 `m => m.DispatchedAt == null && m.DeadLetteredAt == null && (m.NextAttemptAt == null || m.NextAttemptAt <= now)` 经 `Persistence.SqlSugar` 译成丢了 `OR` 的 SQL，SQLite 报 `near "(": syntax error`。发件箱改成先写或条件的等价形式绕了过去（ADR-0064）。

在 SQLite 上逐个形态打印 SqlSugar 生成的 SQL，最小复现是 `o => o.Amount == 1 && (!o.ModifiedAt.HasValue || o.Amount == 3)`，译成 `(( Amount = 1 ) AND (NOT(ModifiedAt IS NOT NULL )( Amount = 3 )))`。规律：

- 漏写的是一元取反作 `&&` / `||` 左操作数时它后面的运算符。`!x.HasValue`、`!(a == b)`、`!(p && q)` 都会漏。
- 条件是这个与 / 或条件处在另一个与 / 或条件的右侧，且外层的左操作数是比较或布尔字段，如 `A && (!… || B)`、`A || (!… && B)`。
- `!o.ModifiedAt.HasValue && (!o.DeletedAt.HasValue || B)` 外层左操作数也是取反，反而译对。
- 布尔字段取反（`!o.IsDeleted`）与方法调用取反（`!ids.Contains(o.Id)`）不受影响。
- 本 ADR 的改写把 `x == null` 变成 `!x.HasValue`，正好落进这个形态，所以发件箱的判定出错。手写 `!x.HasValue` 同样出错，属于 SqlSugar 自身的翻译缺陷。

根因在 SqlSugar 的 `BinaryExpressionResolve.Left`（5.1.4.220 源码）。外层左操作数是二元表达式时，`Right` 先在结果里写一个运算符占位符，外层的右侧翻译完才替换。内层左操作数是一元表达式时，`Left` 只在「整段结果里没有任何占位符」时才补写本层运算符。外层占位符尚未替换，于是被当成本层已经写过，运算符就丢了。布尔字段与方法调用的取反由成员 / 方法解析自己写占位符，所以不受影响。

### 备选

1. 等上游修：不受控，期间是语法错误。
2. 要求调用方先写或条件：契约靠约定，发件箱已经踩到。
3. 改写器交换与 / 或的左右操作数，把取反挪到右侧：两侧都是取反时（`!p || !q`）仍然出错，还要叠加德摩根变换。
4. 改写器把判空改写成方法调用 `SqlFunc.EqualsNull(x, null)`，并把逻辑非下推到叶子。

### 决策

采用 4。

- `x == null`（可空值类型，null 一侧的认定同前）改写成 `SqlFunc.EqualsNull((object)x, null)`，译成 `(x is null)`。`x != null` 仍改写成 `x.HasValue`，它是成员访问，在任何位置都译对。
- `VisitUnary` 对布尔逻辑非做否定下推：
  - `!!p` → `p`。
  - `!(p && q)` → `!p || !q`，`!(p || q)` → `!p && !q`。
  - `!(a == b)` → `a != b`，`!(a < b)` → `a >= b`，其余比较同理。运算符方法按操作数类型重新解析，构造不出来（自定义类型只定义了一半运算符）时保留取反。
  - `!x.HasValue` → `SqlFunc.EqualsNull(x, null)`。
  - 其余叶子（布尔字段、方法调用）保留取反。
- `SqlFunc.EqualsNull` 的方法信息从一个 lambda 的表达式体里取，不用反射查找。
- 一致性套件新增 `List_NullChecksAndNegationsNestedInAndOr_FilterAsWritten`，三个 Provider 继承：发件箱的原形态、先写或条件的形态、判空 / 取反作或条件左操作数、取反整个与 / 或条件。SqlSugar 测试另加带 `DateTimeOffset` 时间比较的同类形态，覆盖取反的时间比较与双重否定。

### 理由

- `SqlFunc.EqualsNull` 是 SqlSugar 自己的方法，各数据库都译成 `(x is null)`，没有数据库专属覆写。它是方法调用而不是一元表达式，SQLite 上逐个验证了作与 / 或条件左右操作数、嵌在右侧、两侧都是判空等位置都译对。
- 否定下推在 SQL 的三值逻辑下等价：`NOT (a < b)` 与 `a >= b` 在含 NULL 时同为未知，德摩根律在三值逻辑下成立，双重否定消去也成立。改写只用于翻译，内存实现仍按原规约判定，三值逻辑与 C# 的差异与改写前相同（`!(x <= t)` 在 x 为 null 时 SQL 过滤掉、C# 保留，这是 SQL Provider 一向的语义）。
- 下推后剩下的取反只在布尔字段与方法调用上，SqlSugar 对它们自己写占位符，不再落进缺陷形态。

### 影响

- 发件箱为绕开缺陷而先写或条件的写法不再需要，直观写法照常翻译。
- 生成的 SQL 形态变化：判空从 `NOT(x IS NOT NULL )` 变成 `(x is null)`，取反的比较变成反运算。查询结果不变。
- 公开 API 不变；`NullableComparisonRewriter` 是内部类型。
