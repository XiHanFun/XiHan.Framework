// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using System.Reflection;
using XiHan.Framework.Workflow.SqlSugar.Entities;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 流程定义实体测试
/// </summary>
public class WorkflowDefinitionEntityTests
{
    /// <summary>
    /// 表名固定
    /// </summary>
    [Fact]
    public void 表名固定()
    {
        var attribute = typeof(SysWorkflowDefinition).GetCustomAttribute<SugarTable>();

        Assert.NotNull(attribute);
        Assert.Equal("sys_workflow_definition", attribute.TableName);
    }

    /// <summary>
    /// 编码与版本唯一
    /// </summary>
    [Fact]
    public void 编码与版本唯一()
    {
        var index = Assert.Single(typeof(SysWorkflowDefinition).GetCustomAttributes<SugarIndexAttribute>());

        Assert.True(index.IsUnique);
        Assert.Equal([nameof(SysWorkflowDefinition.Code), nameof(SysWorkflowDefinition.Version)], index.IndexFields.Keys);
    }

    /// <summary>
    /// 主键经构造函数传入
    /// </summary>
    [Fact]
    public void 主键经构造函数传入()
    {
        var entity = new SysWorkflowDefinition("1001");

        Assert.Equal("1001", entity.BasicId);
    }
}
