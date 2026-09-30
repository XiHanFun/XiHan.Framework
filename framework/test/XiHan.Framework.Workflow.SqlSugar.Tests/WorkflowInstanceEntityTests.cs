// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using System.Reflection;
using XiHan.Framework.Workflow.SqlSugar.Entities;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 流程实例与节点实例实体测试
/// </summary>
public class WorkflowInstanceEntityTests
{
    /// <summary>
    /// 表名固定
    /// </summary>
    [Fact]
    public void 表名固定()
    {
        Assert.Equal("sys_workflow_instance", typeof(SysWorkflowInstance).GetCustomAttribute<SugarTable>()?.TableName);
        Assert.Equal("sys_workflow_node_instance", typeof(SysWorkflowNodeInstance).GetCustomAttribute<SugarTable>()?.TableName);
    }

    /// <summary>
    /// 实例有四个查询索引
    /// </summary>
    [Fact]
    public void 实例有四个查询索引()
    {
        var names = typeof(SysWorkflowInstance)
            .GetCustomAttributes<SugarIndexAttribute>()
            .Select(item => item.IndexName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["idx_{table}_correlation", "idx_{table}_definition_code", "idx_{table}_parent", "idx_{table}_status"],
            names);
    }

    /// <summary>
    /// 节点实例索引覆盖执行历史排序
    /// </summary>
    [Fact]
    public void 节点实例索引覆盖执行历史排序()
    {
        var index = Assert.Single(typeof(SysWorkflowNodeInstance).GetCustomAttributes<SugarIndexAttribute>());

        Assert.Equal(
            [nameof(SysWorkflowNodeInstance.InstanceId), nameof(SysWorkflowNodeInstance.StartTime), nameof(SysWorkflowNodeInstance.Sequence)],
            index.IndexFields.Keys);
        Assert.False(index.IsUnique);
    }
}
