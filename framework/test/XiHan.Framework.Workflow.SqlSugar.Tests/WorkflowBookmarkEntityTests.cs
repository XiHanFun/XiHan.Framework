// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using System.Reflection;
using XiHan.Framework.Workflow.SqlSugar.Entities;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 流程书签实体测试
/// </summary>
public class WorkflowBookmarkEntityTests
{
    /// <summary>
    /// 表名固定
    /// </summary>
    [Fact]
    public void 表名固定()
    {
        Assert.Equal("sys_workflow_bookmark", typeof(SysWorkflowBookmark).GetCustomAttribute<SugarTable>()?.TableName);
    }

    /// <summary>
    /// 四个索引覆盖全部查询
    /// </summary>
    [Fact]
    public void 四个索引覆盖全部查询()
    {
        var indexes = typeof(SysWorkflowBookmark)
            .GetCustomAttributes<SugarIndexAttribute>()
            .ToDictionary(item => item.IndexName, item => item.IndexFields.Keys.ToArray());

        Assert.Equal(4, indexes.Count);
        Assert.Equal([nameof(SysWorkflowBookmark.InstanceId), nameof(SysWorkflowBookmark.CreationTime)], indexes["idx_{table}_instance"]);
        Assert.Equal([nameof(SysWorkflowBookmark.NodeInstanceId)], indexes["idx_{table}_node_instance"]);
        Assert.Equal([nameof(SysWorkflowBookmark.DueTime)], indexes["idx_{table}_due"]);
        Assert.Equal(
            [nameof(SysWorkflowBookmark.Kind), nameof(SysWorkflowBookmark.BookmarkKey), nameof(SysWorkflowBookmark.CreationTime)],
            indexes["idx_{table}_kind_key"]);
    }

    /// <summary>
    /// 索引键列不使用保留字
    /// </summary>
    [Fact]
    public void 索引键列不使用保留字()
    {
        var column = typeof(SysWorkflowBookmark)
            .GetProperty(nameof(SysWorkflowBookmark.BookmarkKey))?
            .GetCustomAttribute<SugarColumn>();

        Assert.Equal("Bookmark_Key", column?.ColumnName);
    }
}
