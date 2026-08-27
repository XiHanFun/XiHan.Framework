// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 流程变量容器测试
/// </summary>
/// <remarks>
/// 容器直接持有底层字典的引用而非副本，这是活动写变量能被引擎持久化的前提，必须显式验证；
/// 读取路径统一经值转换器归一化，使活动不必区分"内存里的原生值"和"持久化往返后的 JsonElement"。
/// </remarks>
public class WorkflowVariablesTests
{
    /// <summary>
    /// 容器写入直接反映到构造时传入的字典
    /// </summary>
    [Fact]
    public void Set_WritesThroughToUnderlyingDictionary()
    {
        var source = new Dictionary<string, object?>();
        var variables = new WorkflowVariables(source);

        variables.Set("amount", 100);

        Assert.Single(source);
        Assert.Equal(100, source["amount"]);
    }

    /// <summary>
    /// 外部直接改字典时容器立刻可见
    /// </summary>
    [Fact]
    public void Get_AfterExternalDictionaryMutation_SeesNewValue()
    {
        var source = new Dictionary<string, object?> { ["name"] = "张三" };
        var variables = new WorkflowVariables(source);

        source["name"] = "李四";

        Assert.Equal("李四", variables.Get("name"));
    }

    /// <summary>
    /// 只读视图与底层字典是同一实例
    /// </summary>
    [Fact]
    public void AsReadOnly_ReturnsLiveViewOfUnderlyingDictionary()
    {
        var source = new Dictionary<string, object?> { ["a"] = 1 };
        var variables = new WorkflowVariables(source);

        Assert.Same(source, variables.AsReadOnly);

        variables.Set("b", 2);
        Assert.Equal(2, variables.AsReadOnly.Count);
    }

    /// <summary>
    /// 变量名集合反映当前键集合
    /// </summary>
    [Fact]
    public void Names_ReflectsCurrentKeys()
    {
        var variables = new WorkflowVariables(new Dictionary<string, object?>());

        variables.Set("a", 1);
        variables.Set("b", 2);

        Assert.Equal(2, variables.Names.Count);
        Assert.Contains("a", variables.Names);
        Assert.Contains("b", variables.Names);
    }

    /// <summary>
    /// 包含判断按键存在而非值非空
    /// </summary>
    [Fact]
    public void Contains_WithExplicitNullValue_ReturnsTrue()
    {
        var variables = new WorkflowVariables(new Dictionary<string, object?> { ["a"] = null });

        Assert.True(variables.Contains("a"));
        Assert.False(variables.Contains("missing"));
    }

    /// <summary>
    /// 读取不存在的变量返回空
    /// </summary>
    [Fact]
    public void Get_WithMissingName_ReturnsNull()
    {
        var variables = new WorkflowVariables(new Dictionary<string, object?>());

        Assert.Null(variables.Get("missing"));
    }

    /// <summary>
    /// 读取原始值时对持久化往返后的 JsonElement 做归一化
    /// </summary>
    [Fact]
    public void Get_WithJsonElementValue_ReturnsNormalizedNativeValue()
    {
        var variables = new WorkflowVariables(new Dictionary<string, object?>
        {
            ["amount"] = JsonSerializer.Deserialize<JsonElement>("2000"),
            ["name"] = JsonSerializer.Deserialize<JsonElement>("\"张三\""),
            ["order"] = JsonSerializer.Deserialize<JsonElement>("{\"total\":99}")
        });

        Assert.Equal(2000m, variables.Get("amount"));
        Assert.Equal("张三", variables.Get("name"));
        var order = Assert.IsType<Dictionary<string, object?>>(variables.Get("order"));
        Assert.Equal(99m, order["total"]);
    }

    /// <summary>
    /// 泛型读取按目标类型转换
    /// </summary>
    [Fact]
    public void GetOfT_WithConvertibleValue_ConvertsToTargetType()
    {
        var variables = new WorkflowVariables(new Dictionary<string, object?>
        {
            ["amount"] = "2000",
            ["status"] = "Completed"
        });

        Assert.Equal(2000, variables.Get<int>("amount"));
        Assert.Equal(WorkflowInstanceStatus.Completed, variables.Get<WorkflowInstanceStatus>("status"));
    }

    /// <summary>
    /// 泛型读取不存在的变量返回目标类型默认值
    /// </summary>
    [Fact]
    public void GetOfT_WithMissingName_ReturnsDefault()
    {
        var variables = new WorkflowVariables(new Dictionary<string, object?>());

        Assert.Equal(0, variables.Get<int>("missing"));
        Assert.Null(variables.Get<string>("missing"));
        Assert.Null(variables.Get<int?>("missing"));
    }

    /// <summary>
    /// 尝试读取存在的变量返回真并给出转换结果
    /// </summary>
    [Fact]
    public void TryGet_WithExistingName_ReturnsTrueAndConvertedValue()
    {
        var variables = new WorkflowVariables(new Dictionary<string, object?> { ["amount"] = "2000" });

        Assert.True(variables.TryGet<int>("amount", out var amount));
        Assert.Equal(2000, amount);
    }

    /// <summary>
    /// 尝试读取不存在的变量返回假且输出默认值
    /// </summary>
    [Fact]
    public void TryGet_WithMissingName_ReturnsFalseAndDefault()
    {
        var variables = new WorkflowVariables(new Dictionary<string, object?>());

        Assert.False(variables.TryGet<string>("missing", out var text));
        Assert.Null(text);
    }

    /// <summary>
    /// 批量合并同时覆盖已有键与新增键
    /// </summary>
    [Fact]
    public void Merge_OverwritesExistingAndAddsNewKeys()
    {
        var variables = new WorkflowVariables(new Dictionary<string, object?>
        {
            ["a"] = 1,
            ["b"] = 2
        });

        variables.Merge(new Dictionary<string, object?>
        {
            ["b"] = 20,
            ["c"] = 30
        });

        Assert.Equal(3, variables.Names.Count);
        Assert.Equal(1, variables.Get("a"));
        Assert.Equal(20, variables.Get("b"));
        Assert.Equal(30, variables.Get("c"));
    }

    /// <summary>
    /// 合并空集合不改变现有变量
    /// </summary>
    [Fact]
    public void Merge_WithEmptySequence_KeepsVariablesUnchanged()
    {
        var variables = new WorkflowVariables(new Dictionary<string, object?> { ["a"] = 1 });

        variables.Merge(Array.Empty<KeyValuePair<string, object?>>());

        Assert.Single(variables.Names);
        Assert.Equal(1, variables.Get("a"));
    }

    /// <summary>
    /// 移除存在的变量返回真，移除不存在的返回假
    /// </summary>
    [Fact]
    public void Remove_ReturnsWhetherVariableExisted()
    {
        var source = new Dictionary<string, object?> { ["a"] = 1 };
        var variables = new WorkflowVariables(source);

        Assert.True(variables.Remove("a"));
        Assert.False(variables.Remove("a"));
        Assert.Empty(source);
    }
}
