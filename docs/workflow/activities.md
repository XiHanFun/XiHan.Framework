# 活动与扩展

这一页讲工作流的「零件」与扩展点：19 个内置活动与它们的属性、条件与模板共用的表达式语言、节点失败后的重试 / 继续 / 补偿、故障原因与设计期诊断的本地化展示、怎样写自己的活动、引擎发出的事件、Provider 要实现的存储与实例锁契约、两种实现的对比与常用配置。读完后，我们能把短信、ERP 回写这类业务动作做成节点放进流程里，并知道出了问题去哪里看。前置知识是[定义与发布](/workflow/definitions)与[实例与用户任务](/workflow/instances)；自定义活动要用到[结果与错误处理](/web/errors)与[全球化和本地化](/common/localization)。

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
| `Delay` | AllMatched | `Duration`（秒或表达式）、`Until`（时刻表达式，结果按下文「表达式」的时间文法读、换算成 UTC，不合文法是 `workflow.activity.property_not_datetime`）、`Cron` + `TimeZone`（文法与调度相同，见[作业与计划](/scheduling/jobs)） |
| `UserTask` | AllMatched | 见[处理用户任务](/workflow/instances#处理用户任务) |
| `PublishEvent` | AllMatched | `EventName`、`Payload`：发布 `workflow.custom` 消息 |
| `WaitSignal` | AllMatched | `SignalName`、`AcceptAnyCorrelation` |
| `EventGateway` | AllMatched | `Signals`、`TimeoutSeconds`、`AcceptAnyCorrelation`：先到的信号胜出，结果标签是信号名或 `timeout`，输出 `signal`、`payload` |
| `SubWorkflow` | AllMatched | `DefinitionName`、`DefinitionVersion`、`Variables`、`VariableExpressions`、`WaitForCompletion`、`ResultVariable`、`FailOnChildFault` |
| `ForEach` | AllMatched | `Items`（求值为数组，最多 `MaxForEachItems` 项）、`ItemVariable`、`Parallel`、`FailFast`，子流程属性同 `SubWorkflow` |
| `Log` | AllMatched | `Message`、`Level` |
| `Fault` | None | `Message`：让实例故障 |
| `Compensate` | AllMatched | `NodeIds`：对指定的已完成节点（缺省为之前全部已完成节点）逆序补偿后继续 |
| `Http` | AllMatched | `Url`、`Method`、`Headers`、`Body`、`ContentType`、`TimeoutSeconds`、`ResultVariable`（缺省 `http`）、`FailOnErrorStatus`（缺省 `true`）；输出 `{ status, headers, body }`，JSON 响应解析为对象 |
| `ExternalTask` | AllMatched | `Topic`、`Payload`；锁的时长由领取方在 `FetchAsync` 里给出 |

框架不提供 `Script` 活动：表达式语言与 `SetVariable` 覆盖了条件、拼接与简单计算，再复杂的逻辑写成自定义活动（有类型、能测试、能注入服务），脚本引擎属于第三方依赖。

### Http 活动

`Http` 经单例 `WorkflowHttpClient` 发请求，超时缺省 `XiHan:Workflow:HttpTimeoutSeconds`（30 秒），定义里的 `TimeoutSeconds` 不能超过 `XiHan:Workflow:MaxHttpTimeoutSeconds`（300 秒）。需要代理或认证处理器时，在装配前注册自己的实例 `new WorkflowHttpClient(httpClient)` 替换它（`HttpClient.Timeout` 设成无限，超时由活动给出）。

地址由流程定义给出，不可信：缺省的客户端过[出站地址策略](/security/outbound)，只能连公网地址，重定向的目标同样检查。目标被拒绝时节点故障 `WORKFLOW_UNEXPECTED_001`，消息键 `workflow.http.address_denied`，参数 `host` 是请求里的主机名。流程要调内网服务时，把服务写进 `XiHan:Security:Outbound:AllowedHosts`。注册自己的 `WorkflowHttpClient` 之后，由它决定能连哪里。

对端同样不可信（ADR-0181）：正文边读边计数，只保留前 4 MiB，再多就停止读取，结果的 `truncated` 为 `true`，`body` 是前 4 MiB 的文本。`TimeoutSeconds` 不是正的秒数或超过上限时，发布定义即校验失败；已发布的定义执行到这里时节点故障、不发请求。活动等响应期间占着事务与实例锁，上限按业务能接受的最长占用来定。

超时由活动自己计时、按令牌归因：`TimeoutSeconds` 按引擎的 `TimeProvider` 落成定时取消，与这次调用的取消令牌链接后交给客户端，时限从发出请求到读完响应体。

| 情形 | 结果 |
| --- | --- |
| 活动的超时令牌已触发（不论调用方是否也已取消，不论客户端抛出取消还是处理器接住取消后交回了响应） | 节点故障 `WORKFLOW_UNEXPECTED_001`，消息键 `workflow.http.timed_out`，响应不写进变量 |
| 只有调用方取消 | 抛出 `OperationCanceledException`：整个操作随调用回滚，不记成节点故障；交回的响应同样不写进变量 |
| 两个都没触发，客户端抛出取消（它自己的超时）或 `TimeoutException` | 同超时 |
| `HttpRequestException` | 同码故障，`details.reason` 只写异常类型的全名，完整异常进日志 |

测试里注入 `FakeTimeProvider`（在装配工作流特性之前登记为 `TimeProvider`），推进时钟即让请求超时，不用真实等待。

### 子流程

子流程的子实例在父实例的分区里，继承关联 ID 与发起人，深度加一，超过 `MaxSubWorkflowDepth`（缺省 16）时返回 `WORKFLOW_VALIDATION_002`；子实例结束时以 `childInstanceId`、`childStatus`、`childVariables`、`childFault`（子实例 `Fault` 的 JSON，没有故障为空）回调父实例。子实例没有正常完成而父节点因此失败时，父节点的故障按子实例的状态各一个消息键：故障 `workflow.child.faulted`、取消 `workflow.child.canceled`、终止 `workflow.child.terminated`，其他状态是通用的 `workflow.child.failed`（参数都是 `id`、`status`，`status` 是枚举名），子实例的故障是它的内层原因。

## 表达式

条件、模板、输出映射、表达式指派都用同一个无依赖的小语言（`IWorkflowExpressionEvaluator`）。它没有赋值、循环与副作用：同一段文本、同一组变量与同一个时刻，在任何实现上求出同一个结果或同一个错误。逐条的规定（词法、文法、每个运算符与函数、模板与错误）写在规范 RFC-0021「表达式」，一致性向量是 `workflow/` 下的五个 `expression-*.json`；这里讲要点。

| 类别 | 内容 |
| --- | --- |
| 运算符 | `\|\|`、`&&`、`==`、`!=`、`<`、`<=`、`>`、`>=`、`+`、`-`、`*`、`/`、`%`、一元 `!` 与 `-` |
| 访问 | 成员 `.x`、索引 `[i]` |
| 字面量 | 数字（十进制定点数，见下）、单双引号字符串、`true`、`false`、`null` |
| 函数（名字不区分 ASCII 字母的大小写） | `len`、`contains`、`startsWith`、`endsWith`、`upper`、`lower`、`trim`、`isNullOrEmpty`、`abs`、`min`、`max`、`round`、`format`、`toNumber`、`toString`、`now`、`date` |

::: v-pre
`+` 任一侧是字符串即拼接，两侧各取值的文本接起来；`==` 两侧都能当数字时按数值比（`1 == '1'`、`'007' == '7'` 为真），否则按值的文本的序数比较，有容器时按结构深比较。标识符是实例变量，输出映射里另有 `outputs`（节点输出）；变量名与成员名一律区分大小写，不随 `JsonObject` 的大小写选项。节点属性里的字符串支持 `{{ 表达式 }}` 模板，引号内的 `}}` 不算结束，渲染出的结果总是字符串。
:::

值与它们的文本：

- 数字是十进制定点数：96 位整数加 0 到 28 位小数（数值上就是 .NET 的 `decimal`），只看数值，`1.50` 与 `1.5` 是同一个数。运算的结果按一半取偶放进数字，放不下是 `workflow.expression.number_out_of_range`（参数 `operator`），`round` 与 `format` 的位数不在 0 到 28 之间同样。
- 数字的文本只取决于数值：不用指数，不带结尾的零，零写 `0`。`19.90` 写 `19.9`，`1.5 + 1.5` 写 `3`；模板、拼接、`toString` 与 `WorkflowValues.AsString` 都是这一种写法。表达式得出的数字写进变量、作为输出时也写这段文本：`1.5 + 1.5` 存成 `3`，`price * 1.10` 不带结尾的零；变量里原来就有的数字照它的原文，不改写。
- `upper` 与 `lower` 逐个码位取 Unicode 的简单大小写映射，与列表关键字的折叠是同一份：不随区域、不看上下文，转换前后长度相同。`lower('İ')` 是 `i`、`upper('ı')` 是 `I`，`ß` 的大写还是 `ß`，`lower('ẞ')` 是 `ß`，平面以外的字母按整个码位转换。结果以实现所带的 Unicode 数据为准，不同版本只在新近收录的字符上可能不同。
- 要固定小数位数的展示（金额）用 `format(x, n)`：先照 `round(x, n)` 舍入（正好一半时远离零），再写成恰好 `n` 位小数的字符串，不足的补零，`n` 为 0 时不写小数点；负数以 `-` 开头，舍入成零的不写负号，没有千分位，不随区域。`format(19.9, 2)` 是 `19.90`，`format(2.5, 0)` 是 `3`，`format(-0.001, 2)` 是 `0.00`。`x` 与 `n` 都要能当数字（数字文本先读成数字），否则 `operands_not_numeric`；`n` 必须是 0 到 28 之间的整数（`2.0` 与 `'2'` 可以，`2.5` 不行），否则 `number_out_of_range`（`operator` 是 `format`）。结果是字符串：`format(1, 2) + 1` 是 `1.001`。
- 字符串当数字用（算术、比较、下标与数字函数）时按一条文法读：首尾的空白只认 U+0009–U+000D 与空格，可选的符号，ASCII 数字与可选的小数点，可选的指数。
- 数组与对象当文本用时是紧凑的 JSON：成员按名字的序数顺序，数字写上面的文本，字符串只转义 `"`、`\` 与 U+0000–U+001F，非 ASCII 字符原样，超过 64 层的容器写 `null`。按文本匹配对象的条件（`contains(obj, '"b":1')`）照这种写法写。
- 时间是字符串。`now()` 与 `date()` 返回 UTC、七位小数、`+00:00` 的定宽文本（`2026-01-02T03:04:05.0000000+00:00`），进程内读这个结果用 `WorkflowValues.TryConvert<DateTimeOffset>`。`date()` 与比较只认 `YYYY-MM-DD`，或再加 `T` / 空格与 `HH:MM`，秒、一到九位小数与偏移可省（没有偏移是 UTC），首尾的空白不裁剪；`01/02/2026`、`Jan 2, 2026`、`12:30` 不是时间，`date()` 报 `workflow.expression.not_date`，比较时按文本。两侧都是时间文本时比较按时刻，比 100 纳秒更细的小数截断。
- 活动自己读的时间用同一条文法：`Delay.Until`、`UserTask.DueAt`、`WorkflowVariables.TryGet<DateTimeOffset>`、`WorkflowValues.TryConvert<DateTimeOffset>` 与 `TryConvert<DateTime>` 只认上面的写法，读出的时刻换算成 UTC，数字、布尔与容器都不是时间；`12:30` 这样只有时间的写法改成在表达式里算出完整的时刻，或改用 `Duration` / `DueAfterSeconds`。

求值的次序：二元运算符与函数先从左到右求值全部操作数，再从左到右核对类型，除法与取余最后才看除数，所以 `'x' / 0` 报 `operands_not_numeric`、`nope / 0` 报 `unknown_variable`；`&&` 与 `||` 短路，右操作数不需要时不求值也不核对；索引先求值被索引的值、再求值下标，下标超出数组是 `index_out_of_range`。

空值的语义：`Evaluate` 与 `ActivityContext.EvaluateProperty` 返回 `Result<Maybe<JsonNode>>`。结果为 null，包括字面量 `null`、缺少的成员或键、值为 null 的变量、没有这个属性，都是成功的无值，不是失败。下游的处理：

- `EvaluateCondition` 遇到无值失败，因为无值不是布尔。
- `Render` 把无值渲染成空串。
- 写回变量、输出与子流程变量时，无值写成 JSON 的 `null`。

表达式最长 4000，嵌套最多 64 层（括号、函数实参、索引、一元运算符都算，`a + b + c` 这样的链不算）。超限在发布时进校验结果，运行时是 `WORKFLOW_VALIDATION_003`（`workflow.expression.too_long`、`workflow.expression.too_deep`）。`+` 拼出的字符串与模板渲染出的文本都不超过 4194304，超过是 `workflow.expression.string_too_long`，渲染结果更长的模板改为分段或在活动里拼。长度与位置都按 UTF-16 码元计；错误里带出的文本（`expression` 参数与 `not_date` 的 `value`）最多 200 个码元，超过的截断后接 `…`，不切开代理对。

一次求值的工作量也有上限：一次 `Evaluate` 或 `EvaluateCondition` 从 0 起累计，一次 `Render` 的全部占位共用一份，超过 16777216 即 `WORKFLOW_VALIDATION_003`（`workflow.expression.budget_exceeded`，参数 `max`）。计数只取决于表达式、值与求值的次序，与机器的快慢无关：

- 每开始求值一个节点（字面量、变量、成员访问、索引、函数调用、一元与二元运算）记 64，括号不算，短路没有求值的右操作数不记。
- 运算在核对操作数之前记它读的操作数：字符串记长度；数字记它的写法的长度，变量与 `outputs` 里的数字按 JSON 原文（`1.50` 记 4、`1e2` 记 3），表达式得出的按数字的文本（字面量 `1.50` 记 3，`1.5 + 1.5` 的结果记 1）；数组与对象记它里面每个值 64 加上全部字符串、数字与成员名的长度；`null` 与布尔记 0。拼接读两侧；算术、比较、一元负、数字函数与数组的下标读其中是字符串或数字的操作数；相等在两侧都是标量时读其中是字符串或数字的、都是容器时读两侧；`len` 读是字符串或数字的实参（容器的长度是个数，不读）；`isNullOrEmpty` 只读到能判定为止，非空的字符串记 1，空串、数字、布尔与容器不读；在数组里 `contains` 读数组，再按元素个数读要找的值；别的字符串函数与 `toString`、`date` 读全部实参；模板的每个占位读结果。

正常的表达式远在上限以内：把四个百万码元的字符串拼到拼接的上限、渲染出 4194304 个码元的模板都在以内。撞上的是把几十万个值的变量整个渲染进模板、在几十万个元素的数组里 `contains`、比两个很大的容器、反复读几十万位以上的数字、反复对百万码元的字符串求 `len`（四百万码元的字符串求四次就到上限）这类写法，改在活动里处理这样的数据，或只取需要的成员。

### 扇出与变量的上限

流程定义由租户用户编辑，引擎限制一个定义能占多少资源（ADR-0182）：

- `ForEach` 的 `Items` 最多 `MaxForEachItems` 项（缺省 1000），超过时节点故障，一个子实例也不启动。
- 实例变量序列化后最多 `MaxVariablesBytes` 字节（缺省 8 MiB）。启动、改变量和跳转时超过是校验错误；节点输出写进变量后超过时节点故障，变量保持写入之前的样子。
- `ForEach` 只在给了 `ResultVariable` 时收集子实例的变量，累计超过变量上限即故障。

取消或终止父实例时，级联处理它的全部子实例。

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
2. `ContinueOnError`：节点记为 `Faulted`，变量 `lastError` 写入这次错误的故障结构（`code`、`message`、`messageKey`、`arguments`、`inner`），以 `outcome = "error"` 流出。表达式按 `lastError.code` 判断（`message` 是按进程默认区域渲染的文本，只适合判空）；展示用 `WorkflowJson.DeserializeFault(instance.Variables[WorkflowReservedNames.LastErrorVariable])?.Localize()`。
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

引擎与内置活动的失败原因都是消息键：节点超时 `workflow.node.timed_out`、活动属性不合法按种类各有键（`workflow.activity.property_*`）、指派规则、表达式的结果不是布尔、HTTP 超时、`Fault` 节点 `workflow.activity.fault_raised`、没有开始节点等。活动抛出时 `reason` 只写异常类型的全名，完整异常进日志；要让调用方看到说明，活动返回失败，不靠抛异常。应用或第三方给的文本（HTTP 的原因短语、外部任务工作者报的错误、`Fault` 节点作者写的消息）原样放在参数里，外层的键照常本地化。表达式在运行期出错（未知变量、除以零、类型不符等）时，故障的消息键就是表达式诊断的键（`workflow.expression.*`，参数带表达式本身）。子流程没有正常完成时父节点的键见上文「子流程」。没有消息键的故障，`Localize()` 返回 `Message` 原文。

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

表达式的语法与求值诊断每种一个键（`workflow.expression.unexpected_character`、`token_expected`、`unexpected_end`、`unknown_variable`、`unknown_function`、`argument_count`、`number_out_of_range` 等，共 25 个，全表在 RFC-0021「表达式」的「错误」），参数都带 `expression`，位置从 1 起、按 UTF-16 码元计；错误码仍是 `WORKFLOW_VALIDATION_003`。`WorkflowJson.DeserializeDefinition` 先按与模型同一份规则核对文档，再构造模型：模型会拒绝的值（缺少成员、名字与活动类型的文法、定义 ID、版本、分类长度、超时、节点与转移 ID、变量名、重试策略）是 `workflow.definition.invalid` 加 `details.errors` 的违规列表，形状与发布时的校验相同（路径如 `$.nodes[0].id`、`$.nodes[0].retryPolicy.maxAttempts`，键如 `workflow.definition.node_id_invalid`、`member_missing`），经 HTTP 同样是字段级 `errors`。JSON 为 `null` 是 `workflow.definition.json_null`，语法错误与值的类型不符是 `json_malformed`（参数 `path`、`line`、`position`）。

### 状态的展示名

状态在模型、查询结果、事件与回调输入里都是枚举值，程序按枚举判断。给人看时用 `WorkflowStatusMessages`：`KeyOf(status)` 给消息键（`workflow.instance_status.*`、`workflow.node_status.*`、`workflow.definition_status.*`），`status.Localize()` 按当前区域渲染。状态不允许操作的错误（`WORKFLOW_CONFLICT_001` / `_002`）按状态各一句（`workflow.instance.is_running` … `is_terminated`、`workflow.definition.is_draft` … `is_archived`），参数 `status` 是枚举名。

## 事件

装配了消息能力时，引擎发布 CloudEvents 信封：`Source` 为 `/workflow`，`Type` 是主题，`Data` 是 `WorkflowEventData` 的 JSON，扩展属性带 `tenant` 与 `correlation`。没有发布者时跳过，发布失败只记日志。应用用 `services.AddMessageDataSchema(WorkflowTopics.InstanceCompleted, "https://schemas.example.com/workflow/instance.json")` 给某个主题（即信封的 `Type`）登记了数据模式时（也可以写在配置节 `XiHan:Messaging:DataSchemas`，ADR-0332），引擎与 `PublishEvent` 活动发布的信封带上这个 `dataschema`，没登记的不带（ADR-0323，见[领域事件](/messaging/domain-events)）。

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

引擎选项在 `XiHan:Workflow`（`WorkflowOptions`），内存与数据库实现读同一节；本页各节提到的上限（波次、子流程深度、`ForEach` 项数、变量字节数、表达式缓存、`Http` 超时）都是这一节的键。上线时最常调的三个：

| 键 | 缺省 | 说明 |
| --- | --- | --- |
| `LockTimeoutSeconds` | 10 | 等实例锁的上限（秒） |
| `RetentionDays` | 0 | 已结束实例的保留期（天），0 为不自动清理 |
| `HousekeepingIntervalSeconds` | 3600 | 自动清理的周期（秒） |

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

完整配置（全部键、取值范围与写错时的启动失败）见 [Workflow.Abstractions](/packages/workflow-abstractions#配置)；Provider 自己的键在各自的子节 `XiHan:Workflow:InMemory`（容量、轮询周期与批量）与 `XiHan:Workflow:Database`（轮询、租约、持有者、租约表连接、健康探针），见 [Workflow.InMemory](/packages/workflow-inmemory#配置) 与 [Workflow.Database](/packages/workflow-database#配置)。

## 常见问题

### 实例多了内存实现会不会撑爆

不会无界增长：内存实现有定义、实例、节点实例、书签四个容量上限，满了写入返回 `WORKFLOW_UNAVAILABLE_002` 而不是淘汰旧数据。配 `RetentionDays` 让轮询器定期清理已结束实例；生产环境多实例部署用数据库实现。

## 相关包与下一步

- [Workflow.Abstractions](/packages/workflow-abstractions)：活动契约、表达式、事件与存储契约
- [Workflow.InMemory](/packages/workflow-inmemory)、[Workflow.Database](/packages/workflow-database)：两种实现的配置与行为
- 错误码：[Workflow.Abstractions 的错误码表](/packages/workflow-abstractions#错误码)
- 相关章节：[CloudEvents](/protocols/cloudevents)（事件信封）、[一致性套件与门禁](/testing/conformance)
- 下一步：[对象存储：读写与列举](/object-storage/basics)
