# 架构决策记录（ADR）

每条决策一个文件，编号递增，状态只增不改（已采纳 → 已替代）。模板：状态、日期、背景、备选、决策、理由、影响。

| 编号 | 决策 | 状态 |
| --- | --- | --- |
| [0001](0001-版本线.md) | 下一代所有包共享 5.x 版本线 | 已采纳 |
| [0002](0002-原语层与内核分包.md) | `Primitives` 与 `Kernel` 分包，原语层单独冻结 | 已采纳 |
| [0003](0003-内核单包.md) | 内核为单包，职责用子命名空间表达 | 已采纳 |
| [0004](0004-第二语言.md) | 第二语言 Go 先行 | 已采纳 |
| [0005](0005-数据Provider顺序.md) | 持久化 Provider 以 EntityFrameworkCore 优先 | 已采纳 |
| [0006](0006-进程外插件协议.md) | 进程外 gRPC 插件协议进 5.0，Wasm 标 Experimental | 已采纳 |
| [0007](0007-Spec仓库位置.md) | Spec 先留在 `next/spec/`，P4 公开时拆出 | 已采纳 |
| [0008](0008-旧版本维护期.md) | 4.x 在 5.0 GA 后维护 24 个月 | 已采纳 |
| [0009](0009-Wasm插件ABI.md) | Wasm 插件 ABI v1 取核心模块 + 线性内存 + JSON，组件模型留给 v2 | 已采纳 |
| [0010](0010-规范与SDK仓库命名.md) | 规范与各语言 SDK 分仓库，按 `framework-*` 命名，`next/spec` 为 submodule | 已采纳 |
| [0011](0011-远程Provider.md) | 远程 Provider = 宿主侧 `<领域>.Remote` 适配器 + 插件实现规范的操作集，适配器不走管道直接执行代理操作 | 已采纳 |
| [0012](0012-批量优先.md) | 批量优先：抽象层加集合入参的批量方法，逐项结果，Provider 原生批量，不提供按查询条件的批量更新 / 删除 | 已采纳 |
| [0013](0013-SqlSugar-Provider.md) | SqlSugar Provider：依赖 `SqlSugarCore`，客户端由特性创建或应用注册，实体只实现协议接口由约定映射，并发用框架并发戳 | 已采纳 |
| [0014](0014-取消V4垫片.md) | 取消 `Compat.V4` 运行期垫片：模块骨架由 `xihan migrate v4` 生成为原生特性，投入放在代码修复与迁移指南 | 已采纳 |
| [0015](0015-集合级自定义动词路由.md) | 自定义动词按请求是否声明 `id` 分实例级 `/{resource}/{id}:{verb}` 与集合级 `/{resource}:{verb}` HTTP 路由；操作描述公开请求 / 响应的 `JsonTypeInfo` | 已采纳 |
| [0016](0016-工作流子系统.md) | 工作流子系统：引擎与内置活动进 `Workflow.Abstractions`，Provider 只实现存储、实例锁与轮询；JSON 值模型；活动经管道执行；事件走 `workflow.*` 消息 | 已采纳 |
| [0017](0017-AI子系统.md) | AI 子系统：以 `Microsoft.Extensions.AI` 为线格式；`Expose.Ai` 的操作契约经完整管道成为模型工具；知识库定义 `IKnowledgeStore`，`AI.VectorData` 适配 `VectorStore`；只有 `ai.tools` 有默认实现 | 已采纳 |
