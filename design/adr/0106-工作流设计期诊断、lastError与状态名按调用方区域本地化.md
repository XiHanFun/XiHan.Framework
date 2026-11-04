# ADR-0106 工作流设计期诊断、lastError 与状态名按调用方区域本地化

## 状态

已采纳（`spec/rfcs/0021-workflow.md` 的「校验」「目录」「故障与重试」「故障原因的本地化」「子流程」随之修订，`spec/conventions/error-codes.md` 的 `WORKFLOW_VALIDATION_001` 一行随之修订）。修订 ADR-0105：它列为待定的三处（定义校验与表达式诊断、`lastError`、子流程的状态名）也用消息键与参数；`workflow.child.failed` 只留给意外的状态。

## 日期

2026-11-03

本 ADR 记录第十五批回灌（`s-batch-backlog.md`，用户定，经 BasicApp 设计会话转来）的一项决定。

## 背景

ADR-0105 让运行期的失败原因按调用方区域本地化，留了三处英文：

1. **设计期诊断**：BasicApp 的流程设计器把发布失败的逐条问题展示给管理员，这些问题是英文。
2. **`lastError` 变量**。
3. **子流程的状态名**：如 `Faulted`。

盘点（只读查看当前检出）：

| 出处 | 原来 | 读取方 |
| --- | --- | --- |
| `WorkflowDefinitionValidator.Errors` / `Validate` | 英文句子的列表；`Validate` 把它们用 `; ` 拼成 `workflow.definition.invalid` 的参数 `errors` | 目录发布、设计器 |
| `AssigneeResolver.Validate` | 英文句子的列表（未登记的类型，以及 Provider 错误的 `Message`） | 定义校验 |
| 表达式（`ExpressionCompiler` 的 19 处 `ExpressionException`） | 英文异常消息，经 `workflow.expression.invalid` 的 `reason` 返回 | 定义校验，以及运行期的实例故障（条件、属性、输出映射） |
| `WorkflowJson.DeserializeDefinition` | 文本 `null` 或异常消息，放进 `errors` | 设计器保存草稿（BasicApp 的 `WorkflowDefinitions.Parse`） |
| `ActivityTypes()` 的描述符 | 显示名、说明、输入说明是代码里的英文 | BasicApp 设计器的节点面板（`WorkflowActivityTypeResponse.From`） |
| 变量 `lastError` | `{code, message}`，`message` 按进程默认区域渲染 | 定义里的表达式（如 `lastError.code == '…'`） |
| 子流程失败 | `workflow.child.failed`，参数 `status` 是枚举名，文案里出现 `Faulted` | 父实例的故障 |
| 状态不允许操作 | `workflow.instance.status` / `workflow.definition.status`，文案里出现状态的枚举名 | 控制、待办、外部任务与目录操作的返回 |

状态在别处都是给程序用的枚举值，不进文案：回调输入 `childStatus`、事件、查询结果、`SubWorkflow` 的结果标签（状态名小写）。

## 备选

**逐条诊断的形状：**

1. 新定义一个工作流自己的诊断类型（节点 ID、转移 ID、键、参数）。宿主不认识它，经 HTTP 时要另写映射。
2. 沿用内核的 `SchemaViolation(Path, Reason, MessageKey, Arguments)`（采纳）。这样 `details.errors` 就是违规列表：
   - 宿主现成会把它写成 Problem Details 的字段级 `errors`，并按调用方区域翻译，BasicApp 不必另写映射。
   - 位置用定义 JSON 里的路径（`$.nodes[2].properties.Cron`）。路径用下标而不用节点 ID，因为 ID 可能重复、可能含 `.`；设计器提交的就是这份 JSON，按下标定位。

**问题文案里的节点上下文：**

1. 外层套一个「节点 'x'：{message}」的键，内层文案在创建时渲染进参数。这又回到存渲染好的文本，参数的语言由创建时的作用域决定。
2. 每条问题用自己的键，节点或转移 ID 作参数，位置由 `Path` 给出（采纳）。属性问题沿用 ADR-0105 的 `workflow.activity.property_*`，文案里不带节点，靠 `Path` 定位。

**`lastError`：**

1. 另加一个本地化文本字段。写入时只能按一种区域渲染，读的人各自的区域不同。
2. 改成 `WorkflowFault` 的结构（采纳）：`code`、`message` 不变，表达式照旧可用；多出 `messageKey`、`arguments`、`inner`，展示时按读取方区域渲染。和 `childFault` 是同一形状。

**状态名：**

1. 文案里放状态的展示名，在创建时渲染进参数。问题同上。
2. 按状态各写一句（采纳）：子流程的故障、取消、终止；状态不允许操作的 6 + 4 种。另给状态的展示名一组键，供界面翻译枚举。

## 决策

### 定义校验

- **入口**：`WorkflowDefinitionValidator.Errors(definition) → IReadOnlyList<string>` 改为 `Diagnose(definition) → IReadOnlyList<SchemaViolation>`。
- **`Validate` 的错误**：
  - 仍是 `WORKFLOW_VALIDATION_001`。
  - 消息键 `workflow.definition.invalid` 的参数由 `errors`（拼接文本）改为 `count`。
  - `details.errors` 是违规列表。
- **每条违规**：
  - `Path` 例：`$.nodes[i]`、`$.nodes[i].activityType`、`$.nodes[i].properties.X`、`$.nodes[i].outputMappings.v`、`$.transitions[i]`、`.sourceNodeId`、`.targetNodeId`、`.condition`；定义级是 `$.nodes`。
  - `MessageKey` 与 `Arguments` 是这条问题的键与参数。
  - `Reason` 是按进程默认区域渲染的文本，日志用。
- **结构问题的新键**：13 个 `workflow.definition.*`，即 `no_nodes`、`node_duplicate`、`activity_unknown`、`start_duplicate`、`mapping_variable_empty`、`transition_duplicate`、`transition_source_missing`、`transition_target_missing`、`start_incoming`、`default_duplicate`、`node_unreachable`、`user_task_no_assignees`、`fallback_rules_missing`。没有开始节点沿用 `start_missing`。
- **属性与指派规则**：属性问题沿用 `workflow.activity.property_*`。`AssigneeResolver.Validate` 改为返回 `IReadOnlyList<Error>`：未登记的类型报 `workflow.assignee.rule_unknown`，Provider 拒绝的参数原样返回 Provider 给的错误。

### 表达式

- `ExpressionException` 带消息键与参数，在求值器边界经 `ToError(expression)` 转成 `WORKFLOW_VALIDATION_003`，消息键就是诊断的键。
- 19 个键：`empty`、`unexpected_character`、`token_expected`、`member_expected`、`unexpected_end`、`unterminated_string`、`unknown_variable`、`unknown_function`、`argument_count`、`argument_range`、`member_of_non_object`、`index_out_of_range`、`index_of_scalar`、`operands_not_scalar`、`operands_not_boolean`、`operands_not_numeric`、`not_number`、`not_date`、`division_by_zero`。
- 参数都带 `expression`；位置从 1 起，原来从 0 起。
- `workflow.expression.invalid` 删除。
- 运行期的表达式故障（实例与节点的 `Fault`）随之是这些键。
- 架构测试 `LocalizationTests` 的「线上还原与异常转换」名单加上 `ExpressionException.cs`：它是异常转换，消息键来自异常，不是常量。

### 定义 JSON

`DeserializeDefinition` 的错误改为下面三个键：

| 情形 | 消息键与参数 |
| --- | --- |
| JSON 为 `null` | `workflow.definition.json_null` |
| 语法错误、类型不符、名字或活动类型不合法 | `workflow.definition.json_malformed`，参数 `path`、`line`、`position`，取自 `JsonException`，从 1 起 |
| 模型的 `init` 拒绝的值 | `workflow.definition.json_value_invalid`，参数 `reason` |

最后一种的 `reason` 是模型的英文说明，属于编程错误，按 AGENTS 约定不本地化；外层的句子本地化。

### 活动描述符

- **新成员**：`ActivityDescriptor` 新增 `DisplayNameKey`、`DescriptionKey`、`CategoryKey`、`CategoryName` 与 `Localize()`。`ActivityInputDescriptor` 新增 `DescriptionKey` 与 `Localize()`。`DisplayName`、`Description`、`Inputs` 改为可 `init`。
- **内置活动**：只给消息键，键有 19 × 2 个显示名与说明、4 个分类、5 个输入说明。代码里不再有英文。
- **`ActivityTypes()`**：返回按当前消息作用域渲染过的副本。
- **回退**：没有键、没有解析器，或目录里没有该区域的文案时，保留回退值——显示名回退到类型名，分类显示名回退到分类。
- **`Category`**：仍是分类标识（`Control` 等），不变。

### lastError

- 值是 `WorkflowJson.SerializeFault(WorkflowFault.From(error))`。
- `WorkflowJson.SerializeFault` / `DeserializeFault` 改为公开。它们由 ADR-0105 的内部 `ToNode` / `ReadFault` 改名而来。

### 状态名

- **子流程**：父节点的故障按子实例的状态取键——`workflow.child.faulted` / `canceled` / `terminated`，参数 `id`、`status`。`workflow.child.failed` 只给手工恢复书签时给了别的状态。
- **状态不允许操作**：错误码不变，按状态取键——`workflow.instance.is_running` … `is_terminated`、`workflow.definition.is_draft` … `is_archived`，参数 `id`、`status`。`workflow.instance.status` 与 `workflow.definition.status` 删除。
- **展示名**：新增公开静态类 `WorkflowStatusMessages`：`KeyOf(status)` 返回键，`status.Localize()` 返回文案。键是 `workflow.instance_status.*`、`workflow.node_status.*`、`workflow.definition_status.*`。

## 理由

- **复用宿主现成的翻译**：设计器经 HTTP 拿到的就是 Problem Details 的字段级 `errors`，宿主现成按调用方区域翻译，不需要新的约定。进程内用 `Diagnose` 取同一组问题。
- **不在创建时渲染**：每条问题、每个状态都是独立的键，参数里没有渲染好的文案，读的人是什么区域就是什么语言。
- **`lastError` 只加不改**：只增加成员、不改 `code` 与 `message`，已有定义里的表达式（`lastError.code == '…'`、`lastError != null`）行为不变。

## 影响

- **公开 API**（Workflow.Abstractions，Unshipped）：
  - 替换：`WorkflowDefinitionValidator.Errors` → `Diagnose`（返回类型改为违规列表）；`AssigneeResolver.Validate` 的返回类型改为 `IReadOnlyList<Error>`。
  - 新增：
    - `ActivityDescriptor` / `ActivityInputDescriptor` 的成员（见上）；
    - `WorkflowStatusMessages`；
    - `WorkflowJson.SerializeFault`、`DeserializeFault`；
    - `WorkflowMessages` 新增 111 个常量。
  - 删除：`ExpressionInvalid`、`InstanceStatus`、`DefinitionStatus` 三个消息键常量（错误码 `WorkflowErrors.*` 不变）。
  - 改参数：`workflow.definition.invalid` 的参数由 `errors` 改为 `count`。
  - Workflow.Conformance 新增用例方法一个。
- **持久化**：不改表。
  - `lastError` 在实例的变量 JSON 里，形状变为故障结构，旧实例里的 `{code, message}` 照读。
  - 故障在实例与节点实例的 JSON 文档里，事件同样是 JSON。
  - 没有列、索引与回填，`InitTables` 不受影响。
- **行为**：
  - 同一错误码下消息键变了：表达式诊断、定义 JSON 的错误、状态不允许操作、子流程失败。
  - 表达式诊断的位置从 1 起。
  - `details.errors` 由字符串变为违规列表。
- **测试**：
  - 引擎用例 38 的子流程故障改为 `workflow.child.faulted`；用例 12 的发布失败按违规的路径与键断言。
  - 新增引擎用例 40，覆盖：
    - 发布时的诊断，以及 zh-CN / en-US 的精确文本；
    - 描述符的显示名、分类名、说明与输入说明；
    - `lastError` 的表达式判断与展示；
    - 条件里未知变量的运行期故障；
    - 子流程被取消；
    - 状态不允许操作，以及状态的展示名。
  - Abstractions 测试：
    - `WorkflowDefinitionValidatorTests` 改为按路径与键断言，并补上 zh-CN / en-US 文案；
    - 新增 `WorkflowLocalizationTests`：表达式诊断、描述符的渲染与回退、全部状态都有键、`lastError` 的新旧形状。
  - 用例 10 里 `lastError.code == 'WORKFLOW_UNEXPECTED_001'` 的判断照旧通过。

> 修订（2026-11-03，第十六批）：「定义 JSON」一节里模型 `init` 拒绝的值不再报 `json_value_invalid`（该键删除）。`WorkflowJson.DeserializeDefinition` 在构造模型之前按与模型同一份规则核对文档，问题以 `workflow.definition.invalid`（参数 `count`）与 `details.errors` 的违规列表返回，形状同发布时的校验：
>
> - 规则只在一处：值的规则是内部的 `WorkflowDefinitionRules`（定义 ID、版本、分类长度、超时、变量名、重试策略的三项），加上已有的 `WorkflowName.IsValid`、`ActivityType.IsValid`、`WorkflowIdentifiers.IsValidElementId`；模型的 `init` 与读入的核对（内部的 `WorkflowDefinitionPrecheck`）都调用它们。必填成员取自序列化器元数据里的 `IsRequired`（即模型的 `required`），成员名按序列化器同一命名策略由属性名得出。
> - 不在场的 `init` 属性会被源生成的序列化器设成类型默认值（盖掉模型的初值）：默认值过不了规则的（`version`、重试策略的 `maxAttempts`、`backoffFactor`）报缺少成员 `workflow.definition.member_missing`。
> - 新键 13 个：`member_missing`、`id_empty`、`name_invalid`、`version_invalid`、`category_too_long`、`timeout_invalid`、`node_id_invalid`、`activity_type_invalid`、`transition_id_invalid`、`variable_name_empty`、`retry_attempts_invalid`、`retry_delay_negative`、`retry_backoff_invalid`（都在 `workflow.definition.` 下），10 种语言。
> - 核对通过的文档构造模型不再抛出；模型自己的英文异常保持原样，只兜住编程错误（AGENTS「其余运行时英文」），读入不再捕获 `ArgumentException`。语法错误与值的类型不符仍是 `json_malformed`。
> - 一致性：Abstractions 的 `WorkflowDefinitionJsonRulesTests` 对每条规则三处对照：读入报出的路径、消息键与 zh-CN / en-US 精确文案，同一个值直接构造模型抛出，边界上合法的文档读入通过、构造不抛出。

> 修订（2026-11-03，第十七批）：上一条修订里「不在场的 `init` 属性被设成类型默认值、默认值过不了规则的报缺少成员」由 ADR-0107 取消：定义 JSON 里没写的成员取模型的初值（`version` 1、重试策略 `maxAttempts` 1、`firstDelay` 10 秒、`backoffFactor` 2），`member_missing` 只针对必填成员。
