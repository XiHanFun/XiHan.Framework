// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.SqlSugar.Stores;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 书签匹配在真实数据库排序规则下的测试
/// </summary>
/// <remarks>
/// 地址取环境变量 <c>XIHAN_TEST_MYSQL</c>，未设置时跳过。
/// </remarks>
public class WorkflowBookmarkMatchingMySqlTests
{
    private const string SkipReason = "未设置 XIHAN_TEST_MYSQL，跳过真实数据库测试。";

    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable("XIHAN_TEST_MYSQL");

    /// <summary>
    /// 书签匹配区分大小写
    /// </summary>
    [Fact]
    public async Task 书签匹配区分大小写()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), SkipReason);

        using var database = WorkflowTestDatabase.CreateMySql(ConnectionString!);
        var store = new SqlSugarWorkflowBookmarkStore(database.Executor);
        var instanceId = Guid.NewGuid().ToString("N");
        var suffix = Guid.NewGuid().ToString("N");

        await store.InsertAsync(NewBookmark(instanceId, WorkflowBookmarkKinds.UserTask, "Alice-" + suffix));
        await store.InsertAsync(NewBookmark(instanceId, WorkflowBookmarkKinds.Signal, "Order-Paid-" + suffix));

        try
        {
            Assert.Empty(await store.GetByKindAndKeyAsync(WorkflowBookmarkKinds.UserTask, "alice-" + suffix));
            Assert.Empty(await store.GetBySignalAsync("order-paid-" + suffix, null));

            Assert.Single(await store.GetByKindAndKeyAsync(WorkflowBookmarkKinds.UserTask, "Alice-" + suffix));
            Assert.Single(await store.GetBySignalAsync("Order-Paid-" + suffix, null));
        }
        finally
        {
            await store.DeleteByInstanceAsync(instanceId);
        }
    }

    private static WorkflowBookmark NewBookmark(string instanceId, string kind, string key)
    {
        return new WorkflowBookmark
        {
            Id = Guid.NewGuid().ToString("N"),
            InstanceId = instanceId,
            NodeId = "node",
            NodeInstanceId = Guid.NewGuid().ToString("N"),
            Kind = kind,
            Key = key,
            CreationTime = new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc)
        };
    }
}
