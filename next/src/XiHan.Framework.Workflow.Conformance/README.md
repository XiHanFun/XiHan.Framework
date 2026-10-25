# XiHan.Framework.Workflow.Conformance

工作流一致性套件（工具链，RFC-0021）：Provider 继承两个抽象测试类并实现夹具，通过全部用例。

| 类型 | 用途 |
| --- | --- |
| `WorkflowStoreConformance` + `WorkflowStoreHost` | 三个存储与实例锁：按 ID / 名字 / 版本 / 最新已发布查、列表排序与过滤、批量写与删、级联、快照语义、到期 / 种类与键 / 信号查询、锁的互斥与超时（锁等待上限配置在 1 秒以内） |
| `WorkflowEngineConformance` + `WorkflowEngineHost` | 装配了 Provider 的宿主上跑引擎：顺序流与事件、决策与默认出边、并行与汇合、用户任务 Any / All / Sequential、转办与加签、延时与超时（`PollAsync` 轮询一次）、信号、子流程与 ForEach、重试与继续、挂起 / 恢复 / 取消 / 终止与补偿、定义生命周期与结果处理器、死循环保护、租户隔离 |
| `ConformanceLog`、`RecordActivity`、`RecordingOutcomeHandler` | 套件登记进容器的记录器（兼消息发布者）、活动与结果处理器 |

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
