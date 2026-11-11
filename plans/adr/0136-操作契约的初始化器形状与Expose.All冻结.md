# ADR-0136 操作契约的初始化器形状与 Expose.All 冻结

## 状态

已采纳（规范 `rfcs/0011-operations.md` 修订）。

## 日期

2026-11-11

## 背景

P5-05 的 API 审查发现，有三处形状由 `Kernel.Generators` 写进下游程序集，下游一编译就被冻住：

1. **`OperationContract` 的构造函数**：签名是 `(id, stability = Stable, expose = All, policy = null, permission = null, readOnly = false)`。生成器在每个下游程序集里调用它。ADR-0041 已经用「末尾追加可选参数」扩展过一次（`readOnly`）。GA 之后再这样扩展，已编译的程序集找不到旧签名，会抛 `MissingMethodException`。
2. **`Expose.All`**：它是五个协议位之和，生成器把它硬编码为 `31`，再以 `(Expose)31` 写进生成代码。枚举常量本来就在编译时写进调用方。哪天框架加了第六个协议，「All」的含义就取决于调用方是在哪个版本上编译的：新编译的操作会自动暴露到新协议上，旧程序集里的不会。
3. **生成的 `GeneratedOperations` 是 `public`**：它进入下游库的公开面和包验证基线；生成器以后改它的形状，就会破坏下游库自己的使用者。

## 备选

- 契约：
  - 保留旧构造函数，另加一个只收 ID 的构造函数：同一件事有两种写法，旧构造函数照样冻结。否决。
  - 构造函数只收 ID，其余字段做成 `init` 属性：采纳。
- `Expose.All`：
  - 改成通配（例如取值 `-1`，由 `IsExposedTo` 识别），以后的协议自动包含：框架加了协议适配器，已发布的操作就会被静默暴露出去，这是安全面的扩大，且不经应用同意。否决。
  - 冻结为 5.0 的五个协议：采纳。
- 生成的类：
  - `GeneratedOperations` 改为 `internal`：采纳。它只在特性所在的程序集里调用。
  - `Persistence.Generators` 的 `GeneratedEntities` 保持 `public`：EF Core 的 DbContext 常放在实体之外的项目里，要跨程序集调用 `ApplyGeneratedEntities()`。

## 决策

1. **`OperationContract`**
   - 构造函数是 `OperationContract(string id)`。`Stability`、`Expose`、`Policy`、`Permission`、`IsReadOnly` 改为 `init` 属性，缺省值分别是 `Stable`、`All`、无、无、按动词推导。
   - 校验在赋值时完成，违反就抛 `ArgumentException`：
     - 策略与权限码不能为空白。
     - `Policy` 与 `Permission` 二选一。两个属性的赋值都会检查对方，所以初始化器无论先写哪个，冲突都会在后写的那个上被发现。
     - `create` / `update` / `delete` 不能声明只读。
   - 以后加字段只加 `init` 属性，构造函数的参数表不变。
2. **`Expose.All` 冻结**：值固定为 `Http | Grpc | Mcp | CommandLine | Ai`（31）。框架以后新增的协议不并入 `All`，要在新协议上发布，必须在契约里显式写上这个协议。内核测试 `ExposeAll_IsFrozenToTheFiveProtocolsOf50` 守护这个值。
3. **生成器的输出**
   - 契约写成 `new OperationContract(id) { … }`，只写非缺省的字段。
   - 枚举写成员名：`Stability.Experimental`；`Expose.Http | Expose.Mcp`，五个协议齐全时写 `Expose.All`。
   - 生成器不认识的位写成强转，作为兜底。
   - `GeneratedOperations` 改为 `internal`。要让别的程序集登记这些操作，在本项目里包一个公开的扩展方法转调 `AddGeneratedOperations()`。
4. **改写调用方**：框架内手写的 104 处多参数构造，机械改写成初始化器。插件握手（`HandshakeValidator`）从插件声明构造契约，赋值时抛出的 `ArgumentException` 照旧转成握手不匹配。

## 影响

- **手写契约**：`new OperationContract(id, stability, expose, policy, permission, readOnly)` 改为 `new OperationContract(id) { Stability = …, Expose = …, Policy = …, Permission = …, IsReadOnly = … }`。目标类型写法 `new(id, permission: …)` 同样要改。
- **用生成器的项目**：重新编译即可。生成的 `GeneratedOperations` 变成 `internal`，有跨程序集调用它的（框架与 BasicApp 里都没有），要包一层公开方法。
- **`Expose.All`**：今天的行为不变；以后加协议时，现有操作不会被自动暴露。
- `Kernel` 的 `OperationContract` 与 `Expose` 是 `Stable`，GA 前修改，不需要登记豁免（ADR-0131）。
