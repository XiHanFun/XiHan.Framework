// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Builders;

namespace XiHan.Framework.Workflow.Tests;

/// <summary>
/// 定义管理与 JSON 序列化测试
/// </summary>
public class DefinitionManagerTests : IDisposable
{
    private readonly WorkflowTestHost _host = new();

    private static WorkflowDefinitionBuilder ValidBuilder(string code = "vacation")
    {
        return WorkflowDefinitionBuilder.Create(code, "请假流程")
            .AddStart()
            .AddEnd()
            .AddTransition("start", "end");
    }

    /// <summary>
    /// 创建-发布-新版本-停用的完整生命周期
    /// </summary>
    [Fact]
    public async Task 定义生命周期与版本管理()
    {
        var created = await _host.DefinitionManager.CreateAsync(ValidBuilder().Build());
        Assert.Equal(1, created.Version);
        Assert.Equal(WorkflowDefinitionStatus.Draft, created.Status);

        var published = await _host.DefinitionManager.PublishAsync(created.Id);
        Assert.Equal(WorkflowDefinitionStatus.Published, published.Status);
        Assert.NotNull(published.PublishTime);

        // 已发布不可更新
        await Assert.ThrowsAsync<WorkflowException>(() => _host.DefinitionManager.UpdateDraftAsync(published));

        var v2 = await _host.DefinitionManager.CreateNewVersionAsync("vacation");
        Assert.Equal(2, v2.Version);
        Assert.Equal(WorkflowDefinitionStatus.Draft, v2.Status);
        Assert.NotEqual(published.Id, v2.Id);

        await _host.DefinitionManager.PublishAsync(v2.Id);
        var latest = await _host.DefinitionManager.GetPublishedAsync("vacation");
        Assert.Equal(2, latest.Version);

        // 指定版本仍可获取
        var v1 = await _host.DefinitionManager.GetPublishedAsync("vacation", 1);
        Assert.Equal(1, v1.Version);

        var disabled = await _host.DefinitionManager.DisableAsync(latest.Id);
        Assert.Equal(WorkflowDefinitionStatus.Disabled, disabled.Status);
        var fallback = await _host.DefinitionManager.GetPublishedAsync("vacation");
        Assert.Equal(1, fallback.Version);
    }

    /// <summary>
    /// 发布前结构校验拦截非法定义
    /// </summary>
    [Fact]
    public async Task 发布拦截非法定义()
    {
        var invalid = WorkflowDefinitionBuilder.Create("invalid", "非法流程")
            .AddStart()
            .AddNode("ghost-target", WorkflowActivityTypes.SetVariable, "目标")
            .AddNode("orphan", WorkflowActivityTypes.SetVariable, "孤岛")
            .AddTransition("start", "missing-node")
            .AddTransition("start", "ghost-target", "amount >")
            .Build();
        var created = await _host.DefinitionManager.CreateAsync(invalid);

        var exception = await Assert.ThrowsAsync<WorkflowDefinitionValidationException>(() =>
            _host.DefinitionManager.PublishAsync(created.Id));

        Assert.Contains(exception.Errors, error => error.Contains("missing-node"));
        Assert.Contains(exception.Errors, error => error.Contains("条件表达式非法"));
        Assert.Contains(exception.Errors, error => error.Contains("orphan"));
    }

    /// <summary>
    /// JSON 序列化往返保持结构
    /// </summary>
    [Fact]
    public void JSON序列化往返保持结构()
    {
        var definition = ValidBuilder("json-roundtrip")
            .WithDescription("往返测试")
            .AddVariable("amount", required: true, description: "金额")
            .Build();
        definition.Nodes[0].Properties["custom"] = new Dictionary<string, object?> { ["nested"] = 5 };

        var json = WorkflowDefinitionJsonSerializer.Serialize(definition);
        var restored = WorkflowDefinitionJsonSerializer.Deserialize(json);

        Assert.Equal(definition.Code, restored.Code);
        Assert.Equal(definition.Nodes.Count, restored.Nodes.Count);
        Assert.Equal(definition.Transitions.Count, restored.Transitions.Count);
        Assert.Equal(definition.Variables.Single().Name, restored.Variables.Single().Name);
        Assert.True(restored.Variables.Single().Required);

        var custom = XiHan.Framework.Workflow.Abstractions.Runtime.WorkflowValueConverter
            .ConvertTo<Dictionary<string, object?>>(restored.Nodes[0].Properties["custom"]);
        Assert.NotNull(custom);
        Assert.Equal(5m, XiHan.Framework.Workflow.Abstractions.Runtime.WorkflowValueConverter.ConvertTo<decimal>(custom["nested"]));

        Assert.Throws<WorkflowException>(() => WorkflowDefinitionJsonSerializer.Deserialize("{ not json"));
    }

    /// <summary>
    /// 仅草稿可删除
    /// </summary>
    [Fact]
    public async Task 仅草稿可删除()
    {
        var created = await _host.DefinitionManager.CreateAsync(ValidBuilder("deletable").Build());
        await _host.DefinitionManager.DeleteAsync(created.Id);
        await Assert.ThrowsAsync<WorkflowException>(() => _host.DefinitionManager.GetAsync(created.Id));

        var published = await _host.PublishAsync(ValidBuilder("undeletable").Build());
        await Assert.ThrowsAsync<WorkflowException>(() => _host.DefinitionManager.DeleteAsync(published.Id));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _host.Dispose();
    }
}
