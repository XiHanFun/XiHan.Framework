// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Scheduler;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Scheduler;

/// <summary>
/// JobTriggerManager 触发状态管理测试
/// </summary>
/// <remarks>
/// 触发状态是调度器判断"该不该触发"的唯一依据：暂停标记、下次触发时间、已触发次数三者
/// 分别由不同入口写入，这里逐一验证它们互不覆盖。全部用例使用固定基准时间。
/// </remarks>
public class JobTriggerManagerTests
{
    /// <summary>
    /// 固定基准时间，避免依赖真实时钟
    /// </summary>
    private static readonly DateTimeOffset BaseTime = new(2024, 6, 12, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 未记录过的任务查不到触发状态
    /// </summary>
    [Fact]
    public void GetTriggerState_WhenJobUnknown_ReturnsNull()
    {
        var manager = new JobTriggerManager();

        Assert.Null(manager.GetTriggerState("unknown"));
    }

    /// <summary>
    /// 首次记录触发时创建状态，触发次数从 1 起算
    /// </summary>
    [Fact]
    public void RecordTrigger_FirstTime_CreatesStateWithCountOne()
    {
        var manager = new JobTriggerManager();

        manager.RecordTrigger("job-a", BaseTime);

        var state = manager.GetTriggerState("job-a");
        Assert.NotNull(state);
        Assert.Equal("job-a", state!.JobName);
        Assert.Equal(BaseTime, state.LastFireTime);
        Assert.Null(state.NextFireTime);
        Assert.Equal(1L, state.TriggerCount);
        Assert.False(state.IsPaused);
    }

    /// <summary>
    /// 重复记录触发时累加次数并刷新最后触发时间
    /// </summary>
    [Fact]
    public void RecordTrigger_Repeatedly_AccumulatesCountAndRefreshesLastFireTime()
    {
        var manager = new JobTriggerManager();

        manager.RecordTrigger("job-a", BaseTime);
        manager.RecordTrigger("job-a", BaseTime.AddMinutes(5));
        manager.RecordTrigger("job-a", BaseTime.AddMinutes(10));

        var state = manager.GetTriggerState("job-a");
        Assert.NotNull(state);
        Assert.Equal(3L, state!.TriggerCount);
        Assert.Equal(BaseTime.AddMinutes(10), state.LastFireTime);
    }

    /// <summary>
    /// 更新下次触发时间时若状态不存在则创建，且不伪造触发次数
    /// </summary>
    [Fact]
    public void UpdateNextFireTime_WhenStateMissing_CreatesStateWithZeroCount()
    {
        var manager = new JobTriggerManager();

        manager.UpdateNextFireTime("job-a", BaseTime.AddHours(1));

        var state = manager.GetTriggerState("job-a");
        Assert.NotNull(state);
        Assert.Equal(BaseTime.AddHours(1), state!.NextFireTime);
        Assert.Null(state.LastFireTime);
        Assert.Equal(0L, state.TriggerCount);
        Assert.False(state.IsPaused);
    }

    /// <summary>
    /// 更新下次触发时间不会重置已累计的触发次数与暂停标记
    /// </summary>
    [Fact]
    public void UpdateNextFireTime_WhenStateExists_PreservesCountAndPauseFlag()
    {
        var manager = new JobTriggerManager();
        manager.RecordTrigger("job-a", BaseTime);
        manager.PauseJob("job-a");

        manager.UpdateNextFireTime("job-a", BaseTime.AddHours(1));

        var state = manager.GetTriggerState("job-a");
        Assert.NotNull(state);
        Assert.Equal(1L, state!.TriggerCount);
        Assert.True(state.IsPaused);
        Assert.Equal(BaseTime, state.LastFireTime);
        Assert.Equal(BaseTime.AddHours(1), state.NextFireTime);
    }

    /// <summary>
    /// 把下次触发时间置空表示不再排期
    /// </summary>
    [Fact]
    public void UpdateNextFireTime_WithNull_ClearsSchedule()
    {
        var manager = new JobTriggerManager();
        manager.UpdateNextFireTime("job-a", BaseTime.AddHours(1));

        manager.UpdateNextFireTime("job-a", null);

        Assert.Null(manager.GetTriggerState("job-a")!.NextFireTime);
    }

    /// <summary>
    /// 记录触发不会覆盖已排好的下次触发时间
    /// </summary>
    [Fact]
    public void RecordTrigger_WhenStateExists_KeepsNextFireTime()
    {
        var manager = new JobTriggerManager();
        manager.UpdateNextFireTime("job-a", BaseTime.AddHours(1));

        manager.RecordTrigger("job-a", BaseTime);

        var state = manager.GetTriggerState("job-a");
        Assert.NotNull(state);
        Assert.Equal(BaseTime.AddHours(1), state!.NextFireTime);
        Assert.Equal(1L, state.TriggerCount);
    }

    /// <summary>
    /// 暂停与恢复来回切换暂停标记
    /// </summary>
    [Fact]
    public void PauseJob_ThenResumeJob_TogglesPausedFlag()
    {
        var manager = new JobTriggerManager();
        manager.UpdateNextFireTime("job-a", BaseTime);

        manager.PauseJob("job-a");
        Assert.True(manager.GetTriggerState("job-a")!.IsPaused);

        manager.ResumeJob("job-a");
        Assert.False(manager.GetTriggerState("job-a")!.IsPaused);
    }

    /// <summary>
    /// 重复暂停保持幂等
    /// </summary>
    [Fact]
    public void PauseJob_CalledTwice_IsIdempotent()
    {
        var manager = new JobTriggerManager();
        manager.UpdateNextFireTime("job-a", BaseTime);

        manager.PauseJob("job-a");
        manager.PauseJob("job-a");

        Assert.True(manager.GetTriggerState("job-a")!.IsPaused);
    }

    /// <summary>
    /// 对不存在的任务暂停或恢复是空操作，不会凭空创建状态
    /// </summary>
    [Fact]
    public void PauseJob_WhenStateMissing_DoesNotCreateState()
    {
        var manager = new JobTriggerManager();

        manager.PauseJob("unknown");
        manager.ResumeJob("unknown");

        Assert.Null(manager.GetTriggerState("unknown"));
    }

    /// <summary>
    /// 移除触发状态后查不到，且对不存在的任务移除是空操作
    /// </summary>
    [Fact]
    public void RemoveTriggerState_RemovesEntryAndToleratesMissingJob()
    {
        var manager = new JobTriggerManager();
        manager.RecordTrigger("job-a", BaseTime);

        manager.RemoveTriggerState("job-a");
        manager.RemoveTriggerState("job-a");

        Assert.Null(manager.GetTriggerState("job-a"));
        Assert.Empty(manager.GetAllTriggerStates());
    }

    /// <summary>
    /// 移除后重新记录触发会重新从 1 起算
    /// </summary>
    [Fact]
    public void RecordTrigger_AfterRemove_RestartsCountFromOne()
    {
        var manager = new JobTriggerManager();
        manager.RecordTrigger("job-a", BaseTime);
        manager.RecordTrigger("job-a", BaseTime);

        manager.RemoveTriggerState("job-a");
        manager.RecordTrigger("job-a", BaseTime);

        Assert.Equal(1L, manager.GetTriggerState("job-a")!.TriggerCount);
    }

    /// <summary>
    /// 全量状态按任务名索引，互不干扰
    /// </summary>
    [Fact]
    public void GetAllTriggerStates_ContainsEveryTrackedJob()
    {
        var manager = new JobTriggerManager();
        manager.RecordTrigger("job-a", BaseTime);
        manager.UpdateNextFireTime("job-b", BaseTime.AddHours(1));

        var states = manager.GetAllTriggerStates();

        Assert.Equal(2, states.Count);
        Assert.Equal(1L, states["job-a"].TriggerCount);
        Assert.Equal(BaseTime.AddHours(1), states["job-b"].NextFireTime);
        Assert.Null(states["job-a"].NextFireTime);
    }

    /// <summary>
    /// 新建的触发状态对象采用安全默认值
    /// </summary>
    [Fact]
    public void JobTriggerState_Default_UsesSafeInitialValues()
    {
        var state = new JobTriggerState();

        Assert.Equal(string.Empty, state.JobName);
        Assert.Null(state.LastFireTime);
        Assert.Null(state.NextFireTime);
        Assert.Equal(0L, state.TriggerCount);
        Assert.False(state.IsPaused);
    }
}
