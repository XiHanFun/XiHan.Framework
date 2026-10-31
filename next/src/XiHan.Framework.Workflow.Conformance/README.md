# XiHan.Framework.Workflow.Conformance

工作流一致性套件（工具链，RFC-0021）：Provider 继承三个抽象测试类并实现夹具，通过全部用例。

| 类型 | 用途 |
| --- | --- |
| `WorkflowStoreConformance` + `WorkflowStoreHost` | 三个存储与实例锁：按 ID / 名字 / 版本 / 最新已发布查、列表排序与过滤、批量写与删、级联、快照语义、到期 / 种类与键 / 信号查询、可领取书签按租户与到期时刻限量查询、定义按名字的查询以租户为名字空间、锁的互斥与超时（锁等待上限配置在 1 秒以内） |
| `WorkflowEngineConformance` + `WorkflowEngineHost` | 装配了 Provider 的宿主上跑引擎：顺序流与事件、决策与默认出边、并行与汇合、用户任务 Any / All / Sequential、转办与加签、延时与超时（`PollAsync` 轮询一次）、信号、子流程与 ForEach、重试与继续、挂起 / 恢复 / 取消 / 终止与补偿、定义生命周期与结果处理器、死循环保护、租户隔离（任务、信号、定义的名字空间与实例的控制和查询）、并发的拒绝重复启动只建一个实例 |
| `WorkflowTaskConformance` + `WorkflowEngineHost` | 用户任务增强与外部任务（`StartAsync` 同 `WorkflowEngineConformance`）：会签阈值（按人数或百分比）、认领与释放、退回与历史、催办事件、条件查询与分页、任务元数据（优先级、到期、表单键）与周期提醒、外部任务的领取 / 完成 / 失败 / 锁、按创建先后限量领取与条数上限 |
| `ConformanceLog`、`RecordActivity`、`RecordingOutcomeHandler` | 套件登记进容器的记录器（兼消息发布者）、活动与结果处理器 |

并发启动用例给实例存储与实例锁套装饰器，把两个启动的交错固定下来：Provider 要以具体类型登记存储与锁（`AddSingleton<MyInstanceStore>()`），再以 `TryAdd` 把接口转给具体类型，套件先登记的接口才能让位给装饰器。

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
