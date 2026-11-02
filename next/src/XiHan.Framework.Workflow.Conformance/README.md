# XiHan.Framework.Workflow.Conformance

工作流一致性套件（工具链，RFC-0021）：Provider 继承三个抽象测试类并实现夹具，通过全部用例。存储是作用域服务，用例在夹具开的调用里使用它们（`WorkflowStoreHost.InPartitionAsync`）；不加入调用方事务的 Provider（内存实现）覆写 `JoinsCallerTransaction => false`，事务相关的用例照实断言。

| 类型 | 用途 |
| --- | --- |
| `WorkflowStoreConformance` + `WorkflowStoreHost` | 三个存储与实例锁：按 ID / 名字 / 版本 / 最新已发布查、列表排序与过滤、批量写与删、级联、快照语义、到期 / 种类与键 / 信号查询、可领取书签按到期时刻限量查询、待办查询的过滤 / 升降序 / 分页 / 计数且随实例状态进出、定义查询的过滤 / 分页 / 计数与关键字按字面且不跨字段匹配、实例列表的关联 ID 集合 / 只取顶层 / 标题关键字、按分区的名字空间、分区互不可见、写入别的分区的记录即抛出、锁的互斥与超时（锁等待上限配置在 1 秒以内） |
| `WorkflowEngineConformance` + `WorkflowEngineHost` | 装配了 Provider 的宿主上跑引擎：顺序流与事件、决策与默认出边、并行与汇合、用户任务 Any / All / Sequential、转办与加签、延时与超时（`PollAsync` 轮询一次）、信号、子流程与 ForEach、重试与继续、挂起 / 恢复 / 取消 / 终止与补偿、定义生命周期与结果处理器、死循环保护、分区隔离（平台与租户对等：任务、信号、定义的名字空间与实例的控制和查询）、并发的拒绝重复启动只建一个实例、业务回滚丢弃引擎写入、办理任务随业务提交或回滚、实例锁持有到调用结束、只读调用在执行活动之前拒绝写入、轮询覆盖平台与每个启用的租户、实例查询的分页与清理的条数越界是校验错误（`WORKFLOW_VALIDATION_002`）、目录的定义查询（摘要的一页与总数、分页越界）、实例查询只取顶层与一组关联 ID（子流程继承发起人与关联 ID、集合越界与空集合）；`AsTenantAsync` 以租户开一次调用（可选回滚），`PublishManyAsync` 在一个分区里批量发布 |
| `WorkflowTaskConformance` + `WorkflowEngineHost` | 用户任务增强与外部任务（`StartAsync` 同 `WorkflowEngineConformance`）：会签阈值（按人数或百分比）、认领与释放、退回与历史、催办事件、条件查询的分页结果与总数（越界是校验错误、挂起的实例的任务不出现）、任务元数据（优先级、到期、表单键）与周期提醒、外部任务的领取 / 完成 / 失败 / 锁、按创建先后限量领取与条数上限 |
| `ConformanceLog`、`RecordActivity`、`RecordingOutcomeHandler` | 套件登记进容器的记录器（兼消息代理：记录送达代理的事件）、活动与结果处理器 |

并发启动用例给实例存储与实例锁套装饰器，把两个启动的交错固定下来：Provider 要以具体类型登记存储与锁（`AddScoped<MyInstanceStore>()`、`AddSingleton<MyInstanceLock>()`），再以 `TryAdd` 把接口转给具体类型，套件先登记的接口才能让位给装饰器（存储的装饰器按作用域）。

事件在代理一侧记录（ADR-0096 第六节）：`Register` 把 `ConformanceLog` 登记成 `IMessageBroker`——消息 Provider 以 `TryAdd` 登记代理与直接发布者（框架的内存、RabbitMQ、Kafka 都是），先登记的记录器不被顶替；没装消息 Provider 的宿主由套件补上 `DirectPublisher`。宿主装了发件箱（`OutboxFeature`）也照常：事件随事务写进发件箱，夹具的 `InScopeAsync`、`AsTenantAsync` 在操作结束之后、`PollAsync` 在轮询之后调 `WorkflowEngineHost.DrainOutboxAsync`，把已提交的事件立即投递给记录器，回滚的不送达；同一信封按 ID 只记一次。所以应用可以在自己装着发件箱的真实宿主上跑这两个套件，不用另起不装发件箱的宿主。记录器只记录、不消费：套件运行时宿主别的消息订阅收不到消息。在夹具之外操作引擎后要读事件，先调 `DrainOutboxAsync`。

租户相关的用例经 `ITenantStore` 取租户：套件以 `TryAdd` 登记一份固定的租户目录（`t1`、`t2`、`t-other` 启用，`t-off` 停用），夹具不必装配多租户。

延时启动与外部任务的锁到期、重试时刻由用例推进时钟判定：`configure` 里登记手动推进的 `TimeProvider`，Provider 的引擎、存储与轮询器须从容器取时钟，不读墙钟。

```csharp
public sealed class InMemoryWorkflowEngineConformanceTests : WorkflowEngineConformance
{
    protected override async Task<WorkflowEngineHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryWorkflowFeature>(), configuration, configure);
        var poller = host.Services.GetRequiredService<DueBookmarkPoller>();
        return new WorkflowEngineHost(host.Services, ct => poller.PollOnceAsync(100, ct), host);
    }
}
```

## 依赖方向

依赖 `XiHan.Framework.Workflow.Abstractions` 与 xUnit v3。被各 Provider 的测试项目引用。

## 状态

5.x P0：可用（Preview）。
