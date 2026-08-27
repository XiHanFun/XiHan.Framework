// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 流程启动请求模型测试
/// </summary>
/// <remarks>
/// 请求上"定义编码 + 版本"与"定义标识"是二选一的两条寻址路径，且 Depth 默认 0 表示顶层实例——
/// 子流程递归深度上限就靠这个默认值起算，改成 1 会让整棵子流程树的深度判断整体偏移。
/// </remarks>
public class WorkflowStartRequestTests
{
    /// <summary>
    /// 新建请求的默认值语义
    /// </summary>
    [Fact]
    public void Defaults_OnNewInstance_AreTopLevelAndUnbound()
    {
        var request = new WorkflowStartRequest();

        Assert.Null(request.InstanceId);
        Assert.Equal(0, request.Depth);
        Assert.Null(request.DefinitionCode);
        Assert.Null(request.DefinitionVersion);
        Assert.Null(request.DefinitionId);
        Assert.Null(request.Name);
        Assert.Empty(request.Variables);
        Assert.Null(request.CorrelationId);
        Assert.Null(request.StarterId);
        Assert.Null(request.ParentInstanceId);
        Assert.Null(request.ParentNodeInstanceId);
    }

    /// <summary>
    /// 不同请求实例的启动变量互相独立
    /// </summary>
    [Fact]
    public void Variables_OnDistinctInstances_AreNotShared()
    {
        var first = new WorkflowStartRequest();
        var second = new WorkflowStartRequest();

        first.Variables["days"] = 3;

        Assert.Empty(second.Variables);
        Assert.NotSame(first.Variables, second.Variables);
    }

    /// <summary>
    /// 按编码启动时版本为空表示取最新已发布版本
    /// </summary>
    [Fact]
    public void ByCode_WithoutVersion_MeansLatestPublished()
    {
        var request = new WorkflowStartRequest { DefinitionCode = "leave" };

        Assert.Equal("leave", request.DefinitionCode);
        Assert.Null(request.DefinitionVersion);
        Assert.Null(request.DefinitionId);
    }

    /// <summary>
    /// 按标识启动时不需要编码与版本
    /// </summary>
    [Fact]
    public void ById_DoesNotRequireCodeOrVersion()
    {
        var request = new WorkflowStartRequest { DefinitionId = "def-1" };

        Assert.Equal("def-1", request.DefinitionId);
        Assert.Null(request.DefinitionCode);
        Assert.Null(request.DefinitionVersion);
    }

    /// <summary>
    /// 子流程启动请求携带父实例归属与递增深度
    /// </summary>
    [Fact]
    public void ChildRequest_CarriesParentLinkageAndIncreasedDepth()
    {
        var request = new WorkflowStartRequest
        {
            InstanceId = "ins-2",
            DefinitionCode = "sub",
            Depth = 1,
            ParentInstanceId = "ins-1",
            ParentNodeInstanceId = "ni-1"
        };

        Assert.Equal("ins-2", request.InstanceId);
        Assert.Equal(1, request.Depth);
        Assert.Equal("ins-1", request.ParentInstanceId);
        Assert.Equal("ni-1", request.ParentNodeInstanceId);
    }

    /// <summary>
    /// 请求 JSON 往返保留寻址字段与启动变量
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesAddressingFieldsAndVariables()
    {
        var request = new WorkflowStartRequest
        {
            InstanceId = "ins-2",
            Depth = 2,
            DefinitionCode = "leave",
            DefinitionVersion = 3,
            DefinitionId = "def-1",
            Name = "张三的请假",
            CorrelationId = "biz-1",
            StarterId = "u-1",
            ParentInstanceId = "ins-1",
            ParentNodeInstanceId = "ni-1",
            Variables = { ["days"] = 3, ["reason"] = "年假" }
        };

        var restored = JsonSerializer.Deserialize<WorkflowStartRequest>(JsonSerializer.Serialize(request));

        Assert.NotNull(restored);
        Assert.Equal("ins-2", restored.InstanceId);
        Assert.Equal(2, restored.Depth);
        Assert.Equal("leave", restored.DefinitionCode);
        Assert.Equal(3, restored.DefinitionVersion);
        Assert.Equal("def-1", restored.DefinitionId);
        Assert.Equal("张三的请假", restored.Name);
        Assert.Equal("biz-1", restored.CorrelationId);
        Assert.Equal("u-1", restored.StarterId);
        Assert.Equal("ins-1", restored.ParentInstanceId);
        Assert.Equal("ni-1", restored.ParentNodeInstanceId);
        Assert.Equal(3m, WorkflowValueConverter.Normalize(restored.Variables["days"]));
        Assert.Equal("年假", WorkflowValueConverter.Normalize(restored.Variables["reason"]));
    }
}
