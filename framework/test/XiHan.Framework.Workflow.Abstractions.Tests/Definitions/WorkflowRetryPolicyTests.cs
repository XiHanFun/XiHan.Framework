// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Definitions;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Definitions;

/// <summary>
/// 节点重试策略模型测试
/// </summary>
/// <remarks>
/// MaxAttempts 默认 1（含首次执行）意味着"配了策略对象但没配次数 = 不重试"，
/// 这是最容易被误改成 0 或 3 的默认值，必须锁死；退避倍率是 double，一并验证往返精度。
/// </remarks>
public class WorkflowRetryPolicyTests
{
    /// <summary>
    /// 新建策略的默认值不产生额外重试
    /// </summary>
    [Fact]
    public void Defaults_OnNewInstance_MeanNoRetry()
    {
        var policy = new WorkflowRetryPolicy();

        Assert.Equal(1, policy.MaxAttempts);
        Assert.Equal(10, policy.FirstDelaySeconds);
        Assert.Equal(2.0, policy.BackoffFactor);
    }

    /// <summary>
    /// 指数退避等待时长按"首次等待 × 倍率^(N-1)"递增
    /// </summary>
    /// <remarks>
    /// 抽象层不提供计算方法，这里只锁定策略字段能表达出文档声明的退避序列，
    /// 防止字段语义被悄悄改成"每次固定间隔"或"倍率作用于总时长"。
    /// </remarks>
    [Theory]
    [InlineData(1, 10d)]
    [InlineData(2, 20d)]
    [InlineData(3, 40d)]
    [InlineData(4, 80d)]
    public void BackoffSequence_FromDefaults_MatchesDocumentedFormula(int attempt, double expectedSeconds)
    {
        var policy = new WorkflowRetryPolicy();

        var delay = policy.FirstDelaySeconds * Math.Pow(policy.BackoffFactor, attempt - 1);

        Assert.Equal(expectedSeconds, delay);
    }

    /// <summary>
    /// 策略 JSON 往返保留三个字段
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesAllFields()
    {
        var policy = new WorkflowRetryPolicy { MaxAttempts = 4, FirstDelaySeconds = 15, BackoffFactor = 1.25 };

        var restored = JsonSerializer.Deserialize<WorkflowRetryPolicy>(JsonSerializer.Serialize(policy));

        Assert.NotNull(restored);
        Assert.Equal(4, restored.MaxAttempts);
        Assert.Equal(15, restored.FirstDelaySeconds);
        Assert.Equal(1.25, restored.BackoffFactor);
    }
}
