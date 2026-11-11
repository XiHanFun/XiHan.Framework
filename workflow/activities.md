来源：https://framework.docs.xihanfun.com/workflow/activities

# 活动与扩展

这一页讲工作流的「零件」与扩展点：19 个内置活动与它们的属性、条件与模板共用的表达式语言、节点失败后的重试 / 继续 / 补偿、故障原因与设计期诊断的本地化展示、怎样写自己的活动、引擎发出的事件、Provider 要实现的存储与实例锁契约、配置与错误码。读完后，我们能把短信、ERP 回写这类业务动作做成节点放进流程里，并知道出了问题去哪里看。前置知识是[定义与发布](/workflow/definitions)与[实例与用户任务](/workflow/instances)；自定义活动要用到[结果与错误处理](/web/errors)与[全球化和本地化](/common/localization)。

## 为什么需要

流程图里的节点不只是审批：要调 HTTP 接口、等信号、并行汇合、循环处理一批子单、失败后重试或回滚。如果每种动作都在业务代码里特判，流程定义就失去了描述能力。框架把动作做成活动：内置活动覆盖控制流、等待、调用与补偿，业务动作实现一个接口即可登记成新的节点类型，设计器通过 `ActivityTypes()` 看到它们的属性说明。失败处理、表达式、事件与存储契约都在引擎里统一，活动只管做事。

## 内置活动

`AddWorkflowEngine` 登记 19 个内置活动（`ActivityTypes`）：

| 活动 | 出边 | 主要属性 |
| --- | --- | --- |
| `Start` | AllMatched | `SignalName`、`Cron`、`TimeZone` |
| `End` | None | `Outcome`：先调用全部 `IWorkflowOutcomeHandler`，再结束实例 |
| `Terminate` | None | `Reason` |
| `SetVariable` | AllMatched | `Values`（字面量对象）、`Expressions`（变量名 → 表达式） |
| `Decision` | Exclusive | — |
| `Parallel` | All | — |
| `Join` | AllMatched | `Mode`：`WaitAll`（缺省，入边全部到达才执行）/ `WaitAny`（首个到达执行） |
| `Delay` | AllMatched | `Duration`（秒或表达式）、`Until`（时刻表达式）、`Cron` + `TimeZone` |
| `UserTask` | AllMatched | 见[处理用户任务](/workflow/instances#处理用户任务) |
| `PublishEvent` | AllMatched | `EventName`、`Payload`：发布 `workflow.custom` 消息 |
| `WaitSignal` | AllMatched | `SignalName`、`AcceptAnyCorrelation` |
| `EventGateway` | AllMatched | `Signals`、`TimeoutSeconds`、`AcceptAnyCorrelation`：先到的信号胜出，结果标签是信号名或 `timeout`，输出 `signal`、`payload` |
| `SubWorkflow` | AllMatched | `DefinitionName`、`DefinitionVersion`、`Variables`、`VariableExpressions`、`WaitForCompletion`、`ResultVariable`、`FailOnChildFault` |
| `ForEach` | AllMatched | `Items`（求值为数组）、`ItemVariable`、`Parallel`、`FailFast`，子流程属性同 `SubWorkflow` |
| `Log` | AllMatched | `Message`、`Level` |
| `Fault` | None | `Message`：让实例故障 |
| `Compensate` | AllMatched | `NodeIds`：对指定的已完成节点（缺省为之前全部已完成节点）逆序补偿后继续 |
| `Http` | AllMatched | `Url`、`Method`、`Headers`、`Body`、`ContentType`、`TimeoutSeconds`、`ResultVariable`（缺省 `http`）、`FailOnErrorStatus`（缺省 `true`）；输出 `{ status, headers, body }`，JSON 响应解析为对象 |
| `ExternalTask` | AllMatched | `Topic`、`Payload`；锁的时长由领取方在 `FetchAsync` 里给出 |

框架不提供 `Script` 活动：表达式语言与 `SetVariable` 覆盖了常见场景，脚本引擎属于第三方依赖。

### Http 活动

`Http` 经单例 `WorkflowHttpClient` 发请求，超时缺省 `XiHan:Workflow:HttpTimeoutSeconds`（30 秒）。需要代理或认证处理器时，在装配前注册自己的实例 `new WorkflowHttpClient(httpClient)` 替换它（`HttpClient.Timeout` 设成无限，超时由活动给出）。

地址由流程定义给出，不可信：缺省的客户端过[出站地址策略](/security/outbound)，只能连公网地址，重定向的目标同样检查。目标被拒绝时节点故障 `WORKFLOW_UNEXPECTED_001`，消息键 `workflow.http.address_denied`，参数 `host` 是请求里的主机名。流程要调内网服务时，把服务写进 `XiHan:Security:Outbound:AllowedHosts`。注册自己的 `WorkflowHttpClient` 之后，由它决定能连哪里。

超时由活动自己计时、按令牌归因：`TimeoutSeconds` 按引擎的 `TimeProvider` 落成定时取消，与这次调用的取消令牌链接后交给客户端，时限从发出请求到读完响应体。

| 情形 | 结果 |
| --- | --- |
| 活动的超时令牌已触发（不论调用方是否也已取消，不论客户端抛出取消还是处理器接住取消后交回了响应） | 节点故障 `WORKFLOW_UNEXPECTED_001`，消息键 `workflow.http.timed_out`，响应不写进变量 |
| 只有调用方取消 | 抛出 `OperationCanceledException`：整个操作随调用回滚，不记成节点故障；交回的响应同样不写进变量 |
| 两个都没触发，客户端抛出取消（它自己的超时）或 `TimeoutException` | 同超时 |
| `HttpRequestException` | 同码故障，`details.reason` 只写异常类型的全名，完整异常进日志 |

测试里注入 `FakeTimeProvider`（在装配工作流特性之前登记为 `TimeProvider`），推进时钟即让请求超时，不用真实等待。

### 子流程

子流程的子实例在父实例的分区里，继承关联 ID 与发起人，深度加一，超过 `MaxSubWorkflowDepth`（缺省 16）时返回 `WORKFLOW_VALIDATION_002`；子实例结束时以 `childInstanceId`、`childStatus`、`childVariables`、`childFault`（子实例 `Fault` 的 JSON，没有故障为空）回调父实例。子实例没有正常完成而父节点因此失败时，父节点的故障是 `workflow.child.failed`（参数 `id`、`status`），子实例的故障是它的内层原因。

## 表达式

条件、模板、输出映射、表达式指派都用同一个无依赖的小语言（`IWorkflowExpressionEvaluator`）：

| 类别 | 内容 |
| --- | --- |
| 运算符 | `\|\|`、`&&`、`==`、`!=`、`<`、`<=`、`>`、`>=`、`+`、`-`、`*`、`/`、`%`、一元 `!` 与 `-` |
| 访问 | 成员 `.x`、索引 `[i]` |
| 字面量 | 数字（decimal）、单双引号字符串、`true`、`false`、`null` |
| 函数（不区分大小写） | `len`、`contains`、`startsWith`、`endsWith`、`upper`、`lower`、`trim`、`isNullOrEmpty`、`abs`、`min`、`max`、`round`、`toNumber`、`toString`、`now`、`date` |

::: v-pre
`+` 任一侧是字符串即拼接；`==` 跨数字类型宽松比较，字符串按序数比较。标识符是实例变量，输出映射里另有 `outputs`（节点输出）。节点属性里的字符串支持 `{{ 表达式 }}` 模板，引号内的 `}}` 不算结束。
:::

空值的语义：`Evaluate` 与 `ActivityContext.EvaluateProperty` 返回 `Result<Maybe<JsonNode>>`。结果为 null，包括字面量 `null`、缺少的成员或键、值为 null 的变量、没有这个属性，都是成功的无值，不是失败。下游的处理：

- `EvaluateCondition` 遇到无值失败，因为无值不是布尔。
- `Render` 把无值渲染成空串。
- 写回变量、输出与子流程变量时，无值写成 JSON 的 `null`。

表达式最长 4000 个字符，嵌套最多 64 层（括号、函数实参、索引、一元运算符都算，`a + b + c` 这样的链不算）。超限在发布时进校验结果，运行时是 `WORKFLOW_VALIDATION_003`（`workflow.expression.too_long`、`workflow.expression.too_deep`）。

**未知的根变量是错误，不是无值**：`amount > 100` 在没有 `amount` 变量时返回 `WORKFLOW_VALIDATION_003`，实例故障；判断可选变量用在启动时给默认值，或者把它放进对象里按成员访问。已解析的表达式按文本缓存，上限 `ExpressionCacheSize`（缺省 10000），满了整体清空重来。

## 最小示例

把「发短信」做成一个活动。活动实现 `IActivity`（需要书签恢复的再实现 `IResumableActivity`，需要补偿的实现 `ICompensableActivity`），是作用域服务：

```csharp
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;

public interface ISmsSender
{
    ValueTask<Result> SendAsync(string phone, string text, CancellationToken cancellationToken);
}

public sealed class SmsActivity(ISmsSender sms) : IActivity
{
    public const string TypeName = "Sms";

    public async ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        // RenderProperty 渲染 {{ }} 模板，缺少的变量渲染成空串
        var phone = context.RenderProperty("Phone");
        var text = context.RenderProperty("Text");
        if (!phone.TryGetValue(out var to) || !text.TryGetValue(out var body))
        {
            return Result.Failure<ActivityOutcome>((phone.Error ?? text.Error)!);
        }

        var sent = await sms.SendAsync(to, body, cancellationToken);
        if (sent.IsFailure)
        {
            // 失败即节点故障，按节点的重试策略处理
            return Result.Failure<ActivityOutcome>(sent.Error!);
        }

        return Result.Success(ActivityOutcome.Complete(new JsonObject { ["smsSent"] = true }));
    }
}
```

::: v-pre
`builder.Services.AddActivity<SmsActivity>(new ActivityType(SmsActivity.TypeName))` 登记后，定义里写 `AddNode("notify", new ActivityType(SmsActivity.TypeName), "短信通知", node => node.WithProperty("Phone", "{{ phone }}").WithProperty("Text", "申请已 {{ outcome }}").WithRetry(3, TimeSpan.FromSeconds(10), 2))`。`AddActivity` 的第三个参数是 `ActivityOutgoing`，第四个是给设计器的 `ActivityDescriptor`。运行后：流程走到 `notify` 时在当前调用的作用域里解析 `SmsActivity`，发送成功则变量里多出 `smsSent: true` 并沿出边继续；发送失败则节点记为 `Faulted`、10 秒后重试，三次都失败实例 `Faulted`，`FaultNodeId` 是 `notify`。`ActivityTypes()` 返回的描述符里也能看到 `Sms`。
:::

活动的返回：

| `ActivityOutcome` | 含义 |
| --- | --- |
| `Complete(outputs, label)` | 完成，输出合并进实例变量，`label` 是结果标签 |
| `Suspend(bookmarks)` / `SuspendWithOutputs(outputs, bookmarks)` | 以书签挂起 |
| `EndInstance(outputs, label)` | 结束整个实例 |
| `TerminateInstance(reason)` | 终止实例 |
| `CompleteWithChildren` / `SuspendWithChildren` | 同时启动子实例 |

`ActivityContext` 提供 `Instance`、`Node`、`NodeInstance`、`Variables`、`State`（节点私有状态）、`Services`、`Invocation`（本次执行的调用上下文）与 `Clock`。`Instance` 与 `NodeInstance` 是只读视图（`WorkflowInstanceView`、`WorkflowNodeInstanceView`），跟随引擎的修改；状态只经输出与结果改变，私有状态经 `State` 读写，结果处理器与指派规则拿到的实例也是同一种视图。`End` 节点带 `Outcome` 时，引擎先调用全部 `IWorkflowOutcomeHandler`（`AddWorkflowOutcomeHandler<T>()` 登记）再结束实例，审批结果回写业务表适合放在这里。

## 故障、重试与补偿

节点失败（活动返回失败的 `Result`，或抛出异常并被翻译为 `WORKFLOW_UNEXPECTED_001`）时按顺序处理：

1. 有重试策略且未用尽：节点记为 `Faulted`，插入 `Retry` 书签，第 n 次尝试失败后等 `FirstDelay × BackoffFactor^(n−1)`，实例保持运行。`WorkflowRetryPolicy.MaxAttempts` 含首次（至少 1，1 为不重试），`FirstDelay` 缺省 10 秒，`BackoffFactor` 至少 1、缺省 2。
2. `ContinueOnError`：节点记为 `Faulted`，变量 `lastError` 写入这次错误的故障结构（`code`、`message`、`messageKey`、`arguments`、`inner`），以 `outcome = "error"` 流出。表达式按 `lastError.code` 判断（`message` 是按进程默认区域渲染的文本，只适合判空）；展示用 `WorkflowJson.DeserializeFault(instance.Variables[WorkflowEngine.LastErrorVariable])?.Localize()`。
3. 否则实例 `Faulted`，记录 `Fault`、`FaultNodeId`、`FaultNodeInstanceId`；`CompensateOnFault` 时执行补偿。之后可以 `RetryAsync` 从故障节点重来，或 `JumpAsync` 改走别的节点。

补偿按完成顺序的逆序调用已完成节点的 `ICompensableActivity.CompensateAsync`，节点状态改为 `Compensated`。三个入口：`EnableCompensation` 的定义被取消时、`CompensateOnFault` 的定义故障时、流程走到 `Compensate` 节点时（BPMN 的补偿抛出事件）。

一个波次执行的节点数超过 `MaxNodeExecutionsPerBurst`（缺省 1000）时实例故障，防止条件写错导致的死循环；波次结束时仍有凑不齐的汇合而又没有书签，实例同样故障。

### 故障原因的展示

实例与节点实例上的 `Fault`（`WorkflowFault`）以及故障事件里的 `Fault` 存错误码 `Code`、消息键 `MessageKey`、参数 `Arguments`（值按不变文化转成字符串）与内层原因 `Inner`，面向人的文案不存进库（规范 RFC-0010）。`Message` 是写入时按进程默认区域渲染的文本，只供日志用，不要直接展示给用户。

| 场景 | 写法 |
| --- | --- |
| 在一次调用里展示（接口返回的视图、时间线） | `instance.Fault?.Localize()`：按当前消息作用域渲染，调用链里就是调用方的区域；有内层原因时以 `workflow.fault.caused_by` 接上 |
| 把故障作为错误返回给调用方 | `instance.Fault.ToError()`：带键、参数与内层的错误，宿主在 Egress 按调用方的区域翻译 |
| 在调用之外按收件人的区域渲染（通知、邮件） | `using (MessageScope.Use(null, recipientLocale)) { text = fault.Localize(); }` |

引擎与内置活动的失败原因都是消息键：节点超时 `workflow.node.timed_out`、活动属性不合法按种类各有键（`workflow.activity.property_*`）、指派规则、表达式的结果不是布尔、HTTP 超时、`Fault` 节点 `workflow.activity.fault_raised`、没有开始节点等。活动抛出时 `reason` 只写异常类型的全名，完整异常进日志；要让调用方看到说明，活动返回失败，不靠抛异常。应用或第三方给的文本（HTTP 的原因短语、外部任务工作者报的错误、`Fault` 节点作者写的消息）原样放在参数里，外层的键照常本地化。表达式在运行期出错（未知变量、除以零、类型不符等）时，故障的消息键就是表达式诊断的键（`workflow.expression.*`，参数带表达式本身）。子流程没有正常完成时，父节点的故障按子实例的状态各一个键（`workflow.child.faulted`、`canceled`、`terminated`），参数 `status` 是枚举名。没有消息键的故障，`Localize()` 返回 `Message` 原文。

### 设计期诊断

发布时的校验结果给设计器逐条展示。`WORKFLOW_VALIDATION_001` 的消息键是 `workflow.definition.invalid`，只带问题条数 `count`；`details.errors` 是 `SchemaViolation` 的列表，每条：

| 成员 | 内容 |
| --- | --- |
| `Path` | 定义 JSON 里的位置：`$.nodes[2]`、`$.nodes[2].activityType`、`$.nodes[2].properties.Cron`、`$.nodes[2].outputMappings.total`、`$.transitions[0].condition`、`$.transitions[0].sourceNodeId`；定义级的问题（没有节点、没有开始节点）是 `$.nodes` |
| `MessageKey`、`Arguments` | 这条问题的消息键与参数：结构问题 `workflow.definition.*`（参数 `node`、`transition` 等），属性问题沿用 `workflow.activity.property_*`，表达式问题是 `workflow.expression.*`，指派规则问题是 Provider 给的错误 |
| `Reason` | 按进程默认区域渲染的文本，日志用 |

接法：

- **经 HTTP**：宿主把 `details.errors` 写成 Problem Details 的字段级 `errors`，键是去掉 `$.` 的路径（如 `nodes[2].properties.Cron`），值是按调用方区域翻译好的文案数组；`detail` 是汇总。设计器按键里的下标定位节点或转移。
- **进程内**：`validator.Diagnose(definition)` 返回同一组问题；在调用里用 `issue.MessageKey.L(issue.Arguments)` 即得调用方语言。

表达式的语法与求值诊断每种一个键（`workflow.expression.unexpected_character`、`token_expected`、`unexpected_end`、`unknown_variable`、`unknown_function`、`argument_count` 等），参数都带 `expression`，位置从 1 起；错误码仍是 `WORKFLOW_VALIDATION_003`。`WorkflowJson.DeserializeDefinition` 先按与模型同一份规则核对文档，再构造模型：模型会拒绝的值（缺少成员、名字与活动类型的文法、定义 ID、版本、分类长度、超时、节点与转移 ID、变量名、重试策略）是 `workflow.definition.invalid` 加 `details.errors` 的违规列表，形状与发布时的校验相同（路径如 `$.nodes[0].id`、`$.nodes[0].retryPolicy.maxAttempts`，键如 `workflow.definition.node_id_invalid`、`member_missing`），经 HTTP 同样是字段级 `errors`。JSON 为 `null` 是 `workflow.definition.json_null`，语法错误与值的类型不符是 `json_malformed`（参数 `path`、`line`、`position`）。

### 状态的展示名

状态在模型、查询结果、事件与回调输入里都是枚举值，程序按枚举判断。给人看时用 `WorkflowStatusMessages`：`KeyOf(status)` 给消息键（`workflow.instance_status.*`、`workflow.node_status.*`、`workflow.definition_status.*`），`status.Localize()` 按当前区域渲染。状态不允许操作的错误（`WORKFLOW_CONFLICT_001` / `_002`）按状态各一句（`workflow.instance.is_running` … `is_terminated`、`workflow.definition.is_draft` … `is_archived`），参数 `status` 是枚举名。

## 事件

装配了消息能力时，引擎发布 CloudEvents 信封：`Source` 为 `/workflow`，`Type` 是主题，`Data` 是 `WorkflowEventData` 的 JSON，扩展属性带 `tenant` 与 `correlation`。没有发布者时跳过，发布失败只记日志。

| 主题前缀 | 主题 |
| --- | --- |
| `workflow.instance.*` | `started`、`completed`、`faulted`、`canceled`、`terminated`、`suspended`、`resumed`、`jumped`、`migrated`、`timedout` |
| `workflow.node.*` | `executing`、`executed`、`faulted`（`WillRetry` 标出是否会重试） |
| `workflow.task.*` | `created`、`completed`、`transferred`、`claimed`、`released`、`returned`、`urged`、`reminder` |
| `workflow.external.*` | `created`、`completed`、`failed` |
| `workflow.custom` | `PublishEvent` 活动发出，`Subject` 是事件名 |

待办通知、催办短信、审批结果同步都订阅这些主题来做，见[发布与订阅](/messaging/pubsub)。消息代理是 Kafka 且集群关了自动建主题（`auto.create.topics.enable=false`）时，这 25 个主题（带 `TopicPrefix`）要预先建好，没人订阅的也要建，否则发布失败、只记日志，见 [Messaging.Kafka](/packages/messaging-kafka#主题)。

## 存储与实例锁

Provider 实现五个契约：`IWorkflowDefinitionStore`、`IWorkflowInstanceStore`、`IWorkflowBookmarkStore`、`IWorkflowTransaction`、`IWorkflowInstanceLock`。三个存储是作用域服务，每个方法的第一个参数是分区（`tenantId`，空为平台），只读写这个分区，写入的记录的 `TenantId` 必须等于它（不等抛 `ArgumentException`）；都按批量写入、不做乐观并发：写入由实例锁串行化。`IWorkflowTransaction` 回答所在作用域能不能加入调用方事务（`IsActive`）、加入的事务能不能写（`EnsureWritable`）。`IWorkflowInstanceLock.AcquireAsync(instanceId)` 等待超过 `LockTimeout`（缺省 10 秒）返回 `WORKFLOW_TIMEOUT_001`，锁不可重入。存储不可达 → `WORKFLOW_UNAVAILABLE_001`，内存实现容量已满 → `WORKFLOW_UNAVAILABLE_002`。自写 Provider 要通过 `Workflow.Conformance` 的一致性套件，见[一致性套件与门禁](/testing/conformance)。

| 方面 | Workflow.InMemory | Workflow.Database |
| --- | --- | --- |
| 存储 | 有界内存表，按分区存放，保存与返回深拷贝；定义 1000、实例 10000、节点实例 100000、书签 100000 | 查询列加整份模型的 JSON 文档，经仓储读写；租户的记录实现 `IMultiTenant`、随租户分库，平台的记录在平台表 |
| 事务 | 无：写入立即生效，调用回滚不撤销；只读调用里拒绝写入（看操作契约） | 在调用方的工作单元里读写，随业务提交或回滚；只读工作单元里拒绝写入 |
| 实例锁 | 每个实例一个进程内信号量 | 租约行：插入即拿到，过期按并发戳接管，持有期间每三分之一租约续约（`LockLeaseSeconds` 缺省 60） |
| 轮询 | 本进程轮询，逐个分区 | 只在持有 `workflow.poller` 租约的节点上轮询，逐个分区（平台与租户目录里启用的租户） |
| 重启 | 实例全部丢失 | 实例与书签保留，重启后接着推进 |
| 多实例 | 不支持 | 多个节点共用一个库 |

## 配置

引擎选项在 `XiHan:Workflow`（`WorkflowOptions`），内存与数据库实现读同一节：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `MaxNodeExecutionsPerBurst` | 整数 | 1000 | 一个波次最多执行的节点数 |
| `MaxSubWorkflowDepth` | 整数 | 16 | 子流程嵌套深度上限 |
| `LockTimeoutSeconds` | 秒 | 10 | 等实例锁的上限 |
| `NotResumableBackoffSeconds` | 秒 | 300 | 不可恢复的到期书签后推多久 |
| `ExpressionCacheSize` | 整数 | 10000 | 表达式缓存条目上限 |
| `RetentionDays` | 天 | 0 | 已结束实例的保留期，0 为不自动清理 |
| `HousekeepingIntervalSeconds` | 秒 | 3600 | 自动清理的周期 |
| `HttpTimeoutSeconds` | 秒 | 30 | `Http` 活动的缺省超时 |

Provider 自己的键在各自的子节：内存实现 `XiHan:Workflow:InMemory`（`MaxDefinitions`、`MaxInstances`、`MaxNodeInstances`、`MaxBookmarks`、`PollIntervalSeconds`、`BatchSize`），数据库实现 `XiHan:Workflow:Database`（`PollIntervalSeconds`、`BatchSize`、`LockLeaseSeconds`、`HolderId`、`LockConnection`、`HealthProbeIntervalSeconds`），缺省值见 [Workflow.InMemory](/packages/workflow-inmemory#配置) 与 [Workflow.Database](/packages/workflow-database#配置)。

```json
{
  "XiHan": {
    "Workflow": {
      "LockTimeoutSeconds": 10,
      "RetentionDays": 90,
      "InMemory": { "PollIntervalSeconds": 1, "BatchSize": 100 }
    }
  }
}
```

配置值写错即启动失败，不按缺省处理：解析不了的值（如把数字写成文字、写成 `NaN`）抛 `ArgumentException`，能解析但超出取值范围的值（如 `LockTimeoutSeconds` 为 0）抛 `ArgumentOutOfRangeException`，消息都带出完整的配置键与原值。

## 错误码

| 错误码 | 含义 |
| --- | --- |
| `WORKFLOW_VALIDATION_001` | 定义不合法，`details.errors` 列出全部问题 |
| `WORKFLOW_VALIDATION_002` | 输入不合法：缺必填变量、子流程深度超限、任务结果不允许、跳转目标不存在、迁移目标不兼容、任务查询缺条件或分页越界、实例查询的分页、关联 ID 集合或清理的条数越界、定义查询的分页越界、定义与实例查询的排序项不合法 |
| `WORKFLOW_VALIDATION_003` | 表达式语法不合法或求值失败 |
| `WORKFLOW_NOTFOUND_001` | 定义不存在、没有已发布版本或在别的分区 |
| `WORKFLOW_NOTFOUND_002` | 实例不存在或在别的分区 |
| `WORKFLOW_NOTFOUND_003` | 书签（用户任务、外部任务）不存在或在别的分区 |
| `WORKFLOW_NOTFOUND_004` | 活动类型或指派规则类型未登记 |
| `WORKFLOW_CONFLICT_001` | 实例状态不允许该操作 |
| `WORKFLOW_CONFLICT_002` | 定义状态不允许该操作 |
| `WORKFLOW_CONFLICT_003` | 书签不可恢复：实例不在运行中、键不匹配，或用户任务与外部任务的书签直接经引擎恢复（要经任务服务与外部任务服务） |
| `WORKFLOW_CONFLICT_004` | 指派人已在任务上，或任务已被认领 |
| `WORKFLOW_CONFLICT_005` | 同一关联 ID 下已有未结束的实例（`RejectActive`） |
| `WORKFLOW_DENIED_001` | 操作者不是任务的指派人 |
| `WORKFLOW_DENIED_002` | 工作者没有持有外部任务的锁，或锁已过期 |
| `WORKFLOW_TIMEOUT_001` | 等实例锁超时 |
| `WORKFLOW_UNAVAILABLE_001` | 工作流存储不可达；或引擎在调用之外被调用时应用不在服务状态 |
| `WORKFLOW_UNAVAILABLE_002` | 工作流存储容量已满 |
| `WORKFLOW_UNEXPECTED_001` | 活动抛出未处理异常，或引擎内部故障（波次上限、死汇合） |
| `PERSISTENCE_UNEXPECTED_001` | 只读调用里调用了写入操作（持久化的错误码，操作实现的缺陷，HTTP 500） |

消息模板在消息目录 `workflow` 里，十种区域都有译文。

## 与其他能力的关系

- `Delay` 的 `Cron` 与 `Start` 的定时启动复用调度子系统的 `CronExpression`，文法见[作业与计划](/scheduling/jobs)；工作流自己的到期书签由轮询器推进，不占用调度器的作业。
- 引擎在调用里加入调用方的事务，在调用之外自己经完整管道开调用，租户、授权、审计与追踪的行为与[执行管道](/startup/pipeline)一致；分区与分库见[租户分片与分库](/multi-tenancy/sharding)。
- 数据库实现经 `persistence.relational@1` 的仓储读写，见[持久化与 Provider](/persistence/overview)。
- `Http` 活动的目标地址受[出站地址策略](/security/outbound)约束。

## 常见问题

### 为什么没有脚本活动

表达式语言加 `SetVariable` 覆盖了条件、拼接与简单计算；再复杂的逻辑写成自定义活动，有类型、能测试、能注入服务。脚本引擎是第三方依赖，框架不内置。

### 活动里抛异常和返回失败有什么区别

两者都让节点失败并走重试 / 继续 / 故障的处理，但抛异常被翻译成 `WORKFLOW_UNEXPECTED_001`，`reason` 只有异常类型名，调用方看不到说明；返回带消息键的失败 `Result`，故障能按调用方区域本地化展示。可预期的失败一律返回 `Result`。

### 节点重试了几次还失败，实例故障了，还能救吗

能。`RetryAsync` 清掉故障从故障节点再执行一次；问题出在流程设计上时用 `JumpAsync` 跳到别的节点；要回滚已做的事，定义上开 `WithCompensateOnFault`，或在流程里放 `Compensate` 节点。

### Http 活动连不上内网服务

缺省的客户端经出站地址策略，只放行公网地址。把内网主机写进 `XiHan:Security:Outbound:AllowedHosts`，或者注册自己的 `WorkflowHttpClient`。被拒绝时节点故障的消息键是 `workflow.http.address_denied`。

### 实例多了内存实现会不会撑爆

不会无界增长：内存实现有定义、实例、节点实例、书签四个容量上限，满了写入返回 `WORKFLOW_UNAVAILABLE_002` 而不是淘汰旧数据。配 `RetentionDays` 让轮询器定期清理已结束实例；生产环境多实例部署用数据库实现。

## 相关包与下一步

- [Workflow.Abstractions](/packages/workflow-abstractions)：活动契约、表达式、事件与存储契约
- [Workflow.InMemory](/packages/workflow-inmemory)、[Workflow.Database](/packages/workflow-database)：两种实现的配置与行为
- 相关章节：[CloudEvents](/protocols/cloudevents)（事件信封）、[一致性套件与门禁](/testing/conformance)
- 下一步：[对象存储：读写与列举](/object-storage/basics)
