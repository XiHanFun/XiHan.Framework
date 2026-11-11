# ADR-0151 Preview 数据类型按初始化器形状改写

## 状态

已采纳（规则见 ADR-0142 与 `rfcs/0001-primitives.md`「稳定性级别」）。

## 日期

2026-11-11

## 背景

ADR-0142 定下规则：会演进或跨 Provider 边界的数据类型不用位置记录，并改写了内核、消息与宿主的 `Stable` 类型。API 审查第三节还列出了一批 `Preview` 的位置记录，其中几个已经在位置参数之外追加过字段：

- 工作流的 13 个请求类型。
- 对象存储的元数据与分页。
- MCP 的 tool 与行为提示。
- 插件状态、命令行解析结果、OpenAPI 的服务地址与文档输入。
- 持久化的列映射、SqlSugar 差异日志数据。

`Preview` 可以在小版本经豁免破坏（ADR-0131），但 GA 前改不需要豁免，代价最低。

## 决策

按 ADR-0142 的规则改写 25 个类型：

- **构成身份的成员经构造，其余走初始化器**：
  - 用户任务的 7 个请求（`UserTaskCompletion`、`UserTaskTransfer`、`UserTaskAddition`、`UserTaskClaim`、`UserTaskRelease`、`UserTaskReturn`、`UserTaskUrge`）保留 `(taskId, actorId)` 构造。
  - 外部任务的 3 个请求（`ExternalTaskCompletion`、`ExternalTaskFailure`、`ExternalTaskLockExtension`）保留 `(taskId, workerId)` 构造。
  - `WorkflowJump(instanceId, targetNodeId)`、`WorkflowMigration(instanceId, targetDefinitionId)`、`WorkflowVariableUpdate(instanceId)`。
  - `OpenApiServer(url)`、`OpenApiDocumentSource(registry, options)`。
  - 原来没有缺省值的其余成员标 `required`，如 `Outcome`、`TargetAssigneeId`、`AssigneeIds`、`Error`、`LockDuration`。
- **全部走初始化器**：
  - `UserTaskPage`、`BlobMetadata`、`BlobPage`、`EntityColumnMapping`。
  - `OutOfProcessPluginStatus`、`WasmPluginStatus`。
  - `McpTool`、`McpToolAnnotations`：三个提示缺省为假，以后补 MCP 规范的 `title`、`openWorldHint` 只加属性。
  - `ParsedCommandLine`、`SqlSugarDiffLogData`。
- **框架内的构造机械改写**：约 120 处，值为缺省的可选实参（`null`、`false`）直接省掉。
- **文档按新写法更新**：
  - XML 文档从 `<param>` 移到各属性。
  - 带「；」的说明按源码规范拆进 `<remarks>`。

## 影响

- 源码迁移，示例：
  - `new UserTaskCompletion(taskId, userId, outcome, comment)` → `new UserTaskCompletion(taskId, userId) { Outcome = outcome, Comment = comment }`。
  - `new WorkflowJump(id, "manager", Reason: "…", ActorId: admin)` → `new WorkflowJump(id, "manager") { Reason = "…", ActorId = admin }`。
  - `new OpenApiServer(url, "生产")` → `new OpenApiServer(url) { Description = "生产" }`。
  - `new BlobMetadata(key, length, …)` → `new BlobMetadata { Key = key, Length = length, … }`。
- 这些类型都不再能解构。
- JSON 形状不变：`BlobMetadata` 的元数据文件按属性名读写，旧文件照常读出。
- 这些类型都是 `Preview` 或 `Experimental`，GA 前修改，不需要登记豁免（ADR-0131）。
