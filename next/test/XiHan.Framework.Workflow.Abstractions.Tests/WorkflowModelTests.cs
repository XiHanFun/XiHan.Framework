// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 标识、重试策略、查询、选项、故障与消息目录等小模型的约束。
/// </summary>
public sealed class WorkflowModelTests
{
    [Fact]
    public void Identifiers_FollowTheirGrammars()
    {
        Assert.Equal("order.approval_v2", new WorkflowName("order.approval_v2").ToString());
        Assert.Equal(new WorkflowName("a"), (WorkflowName)"a");
        Assert.ThrowsAny<ArgumentException>(() => new WorkflowName("Upper"));
        Assert.ThrowsAny<ArgumentException>(() => new WorkflowName("-dash"));
        Assert.ThrowsAny<ArgumentException>(() => new WorkflowName(new string('a', 129)));
        Assert.Equal(128, new WorkflowName(new string('a', 128)).Value.Length);

        Assert.Equal("Custom.Type-1", new ActivityType("Custom.Type-1").Value);
        Assert.ThrowsAny<ArgumentException>(() => new ActivityType("1starts-with-digit"));
        Assert.ThrowsAny<ArgumentException>(() => new ActivityType("has space"));

        Assert.True(WorkflowIdentifiers.IsValidElementId("a") && WorkflowIdentifiers.IsValidElementId("A-1_b.c") && WorkflowIdentifiers.IsValidElementId(new string('x', 64)));
        Assert.False(WorkflowIdentifiers.IsValidElementId(null) || WorkflowIdentifiers.IsValidElementId("") || WorkflowIdentifiers.IsValidElementId("-x") || WorkflowIdentifiers.IsValidElementId("a b") || WorkflowIdentifiers.IsValidElementId(new string('x', 65)));
        var id = WorkflowIdentifiers.NewId();
        Assert.Equal(32, id.Length);
        Assert.True(id.All(Uri.IsHexDigit));
        Assert.NotEqual(id, WorkflowIdentifiers.NewId());
    }

    [Fact]
    public void RetryPolicy_ComputesBackoff()
    {
        var policy = new WorkflowRetryPolicy { MaxAttempts = 3, FirstDelay = TimeSpan.FromSeconds(10), BackoffFactor = 2 };
        Assert.True(policy.CanRetry(1) && policy.CanRetry(2) && !policy.CanRetry(3));
        Assert.Equal(TimeSpan.FromSeconds(10), policy.DelayAfter(1));
        Assert.Equal(TimeSpan.FromSeconds(20), policy.DelayAfter(2));
        Assert.Equal(TimeSpan.FromSeconds(40), policy.DelayAfter(3));
        Assert.Equal(TimeSpan.MaxValue, (policy with { BackoffFactor = 1000 }).DelayAfter(10));
        Assert.Equal(TimeSpan.FromSeconds(10), (policy with { BackoffFactor = 1 }).DelayAfter(7));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowRetryPolicy { MaxAttempts = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowRetryPolicy { BackoffFactor = 0.5 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowRetryPolicy { FirstDelay = TimeSpan.FromSeconds(-1) });
    }

    [Fact]
    public void QueryOptionsAndFaults_EnforceBounds()
    {
        Assert.Equal(100, new WorkflowInstanceQuery().Limit);
        Assert.Equal(WorkflowInstanceQuery.MaxLimit, new WorkflowInstanceQuery { Limit = WorkflowInstanceQuery.MaxLimit }.Limit);
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowInstanceQuery { Limit = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowInstanceQuery { Limit = WorkflowInstanceQuery.MaxLimit + 1 });

        var options = new WorkflowOptions();
        Assert.Equal((1000, 16, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5), 10_000), (options.MaxNodeExecutionsPerBurst, options.MaxSubWorkflowDepth, options.LockTimeout, options.NotResumableBackoff, options.ExpressionCacheSize));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowOptions { MaxNodeExecutionsPerBurst = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowOptions { MaxSubWorkflowDepth = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowOptions { ExpressionCacheSize = 0 });

        var error = Error.NotFound(WorkflowErrors.InstanceNotFound, WorkflowMessages.InstanceNotFound, MessageArguments.Of(("id", "i1")));
        var fault = WorkflowFault.From(error);
        Assert.Equal(WorkflowErrors.InstanceNotFound, fault.Code);
        Assert.Equal("NOTFOUND", fault.ToError().Kind);
        Assert.Equal(fault.Code, fault.ToError().Code);

        var instance = new WorkflowInstance { Id = "i", DefinitionId = "d", DefinitionName = new WorkflowName("n") };
        Assert.False(instance.IsFinal);
        foreach (var status in new[] { WorkflowInstanceStatus.Completed, WorkflowInstanceStatus.Faulted, WorkflowInstanceStatus.Canceled, WorkflowInstanceStatus.Terminated })
        {
            instance.Status = status;
            Assert.True(instance.IsFinal);
        }

        instance.Status = WorkflowInstanceStatus.Suspended;
        Assert.False(instance.IsFinal);
    }

    [Fact]
    public void Messages_AreCompleteForEveryBuiltInLocale()
    {
        var catalog = WorkflowMessages.Catalog;
        Assert.Equal(WorkflowMessages.CatalogName, catalog.Name);
        Assert.Equal(10, catalog.Locales.Count);
        foreach (var locale in new[] { "en-US", "zh-CN", "zh-TW", "ja-JP", "ko-KR", "de-DE", "es-ES", "fr-FR", "pt-BR", "ru-RU" })
        {
            Assert.Contains(locale, catalog.Locales);
            Assert.Contains("{id}", catalog.Find(WorkflowMessages.InstanceNotFound, locale).ValueOrThrow(), StringComparison.Ordinal);
        }
    }
}
