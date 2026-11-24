# ADR-0273 OpenAPI 的 deprecated 取自操作的稳定性

## 状态

已采纳（`Protocols.Http` 的 OpenAPI 生成给 `Stability.Deprecated` 的操作写标准的 `deprecated: true`，稳定性扩展字段照旧；契约、源生成器与其余协议不变；`protocol/http.md` 已补）。

## 日期

2026-11-24

## 背景

- OpenAPI 的操作对象有标准字段 `deprecated`（布尔，缺省 `false`）。文档界面据此把操作标成弃用，客户端生成器据此给生成的方法加弃用标记。框架生成的文档里没有这个字段。
- 操作契约上已经有「弃用」的表达，不需要新加：
  - `OperationContract.Stability`（`[Operation("orders.create", Stability = Stability.Deprecated)]`）。`Stability.Deprecated` 是 RFC-0001 的五个级别之一：已弃用，保留至少两个小版本，下一个大版本移除。
  - 源生成器认 `[Operation]` 的这个成员，手写登记的 `new OperationContract(id) { Stability = … }` 同样带着它；各协议适配器都从 `OperationDescriptor.Contract` 读。
- OpenAPI 生成只把稳定性写进扩展字段：非 `Stable` 的操作带 `x-xihan-stability`，弃用的是 `"x-xihan-stability": "Deprecated"`。扩展字段只有框架自己的管理界面认，标准工具不认。
- 核对过的其他候选都不是操作的弃用表达：
  - `[Obsolete]`：C# 编译期的标记，`[Operation]` 的生成器不读它，运行期的契约里也没有对应字段。
  - `[ApiLevel]`：标公开类型的 API 级别，运行期不读，与操作契约无关。
  - `OperationAttribute` 的其余成员（`Expose`、`ReadOnly`、`Policy`、`Permission`）与弃用无关。

## 备选

1. **认 C# 的 `[Obsolete]`，由源生成器带进操作描述**：否决。
   - 契约已经有 `Stability.Deprecated`，再认 `[Obsolete]` 就是两个来源：只标了其中一个时以谁为准，要再定一条规则。
   - 不经源生成器的登记没有 `[Obsolete]` 可读：手写的 `OperationContract`、插件登记的远程操作、其他语言的 SDK。稳定性是清单与契约里跨语言的字段。
   - 生成的登记代码要引用操作类，生成器没有为它压制 CS0612 / CS0618；给操作类标 `[Obsolete]` 本身就不是现在支持的写法。
2. **契约新增布尔字段 `Deprecated`**：否决。与 `Stability.Deprecated` 重复，同样会出现两处不一致。
3. **只留扩展字段，由使用方去认 `x-xihan-stability`**：否决。标准字段就是为这件事定的，文档界面与客户端生成器不会为一个框架的扩展写适配。
4. **写了 `deprecated` 就不再写 `x-xihan-stability: "Deprecated"`**：否决。规范写的是非 `Stable` 的操作都带这个扩展；读扩展的一方不必为弃用另读一个字段。
5. **不弃用的操作写 `deprecated: false`**：否决。缺省就是 `false`，每个操作多一行没有信息量。
6. **同时在 `description` 里加一句本地化的「已弃用」、在响应上加 `Deprecation` / `Sunset` 头**：不在本 ADR。契约没有「何时移除」「换成哪个操作」的字段，现在能写的只有一个布尔；标准字段已经让界面显示弃用样式。
7. **普通端点（没有操作描述、按元数据收录的路由端点）认端点元数据里的 `ObsoleteAttribute`**：不在本 ADR，见「影响」。
8. **参数与 Schema 属性级别的 `deprecated`**：不在本 ADR。请求与响应的 Schema 由源生成器生成，属性上的弃用没有带进 `OperationSchema`。

## 决策

1. 生成 OpenAPI 文档时，`descriptor.Contract.Stability == Stability.Deprecated` 的操作在操作对象上写 `"deprecated": true`；其余级别不写这个字段。
2. `x-xihan-stability` 照旧：弃用的操作两个字段都有。
3. 全量文档与各分组文档是同一段写法，都带。
4. 只改文档。弃用的操作照常映射路由、照常执行；`Stability.Internal` 的操作照旧不进文档。gRPC、MCP、命令行与 CloudEvents 不动。

## 理由

- 一个来源：弃用只在契约的稳定性上声明一次，五个协议读同一个字段；这次只是让 HTTP 的文档把它翻译成标准写法。
- 改动只在生成器写操作对象的一处，不碰公开 API、契约与规范的操作模型。

## 影响

- 公开 API：没有变化。
- 文档内容：已经声明 `Stability = Stability.Deprecated` 的操作多出 `"deprecated": true`。
  - 文档界面（Scalar、Swagger UI 等）把它显示成弃用。
  - 由文档生成的客户端会给对应的方法加弃用标记；把弃用警告当错误的客户端工程会在重新生成后报出来，这正是弃用要传达的。
  - 没有弃用操作的应用，文档逐字节不变。
- 规范（已补，规范指针 `3d3a139`）：`protocol/http.md`「OpenAPI」里 `paths` 一条，在「非 `Stable` 的操作带 `x-xihan-stability`」之后补「`Deprecated` 的操作另带 OpenAPI 标准的 `deprecated: true`，其余级别不写」。RFC-0011 不用改：稳定性本来就是契约的字段。
- 文档站（已改）：`docs/web/openapi.md` 的字段表加一行 `deprecated`。
- 同一个标记在别的协议上可以怎样体现，这次都没有做：
  - **MCP**：tool 对象没有弃用字段（`annotations` 只有标题与四个行为提示）。可选做法是在 `description` 前加一句本地化的「已弃用」，或放进 `_meta`。
  - **命令行**：`operations` 已经逐行打印稳定性，弃用的显示 `Deprecated`。可选做法是 `invoke` 调到弃用的操作时在标准错误上提示一句。
  - **gRPC**：`ProtoFileBuilder` 生成的 `.proto` 可以给对应的 `rpc` 写 `option deprecated = true;`。
  - **HTTP 运行期**：响应带 `Deprecation` 头（RFC 9745）；要有移除日期才能带 `Sunset`（RFC 8594），契约现在没有这个字段。
- 普通端点没有处理：它们的 `operationId`、摘要、标签都取自端点元数据，弃用可以取元数据里的 `ObsoleteAttribute`（方法组形式的处理器会把方法上的特性带进元数据）。是否加另行决定。
