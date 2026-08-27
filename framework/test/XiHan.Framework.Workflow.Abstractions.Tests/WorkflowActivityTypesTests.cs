// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 内置活动类型编码常量测试
/// </summary>
/// <remarks>
/// 这些编码写在流程定义节点里并落库，改一个字母就会让所有存量定义解析不到活动，
/// 因此逐条锁死字面值，并额外锁死"编码互不重复"与常量总数——新增编码必须显式改测试。
/// </remarks>
public class WorkflowActivityTypesTests
{
    /// <summary>
    /// 各活动类型编码字面值锁定
    /// </summary>
    [Fact]
    public void Values_AreLocked()
    {
        Assert.Equal("Start", WorkflowActivityTypes.Start);
        Assert.Equal("End", WorkflowActivityTypes.End);
        Assert.Equal("Terminate", WorkflowActivityTypes.Terminate);
        Assert.Equal("SetVariable", WorkflowActivityTypes.SetVariable);
        Assert.Equal("Decision", WorkflowActivityTypes.Decision);
        Assert.Equal("Parallel", WorkflowActivityTypes.Parallel);
        Assert.Equal("Join", WorkflowActivityTypes.Join);
        Assert.Equal("Delay", WorkflowActivityTypes.Delay);
        Assert.Equal("UserTask", WorkflowActivityTypes.UserTask);
        Assert.Equal("Http", WorkflowActivityTypes.Http);
        Assert.Equal("Script", WorkflowActivityTypes.Script);
        Assert.Equal("PublishEvent", WorkflowActivityTypes.PublishEvent);
        Assert.Equal("WaitSignal", WorkflowActivityTypes.WaitSignal);
        Assert.Equal("SubWorkflow", WorkflowActivityTypes.SubWorkflow);
        Assert.Equal("ForEach", WorkflowActivityTypes.ForEach);
        Assert.Equal("Log", WorkflowActivityTypes.Log);
        Assert.Equal("Fault", WorkflowActivityTypes.Fault);
    }

    /// <summary>
    /// 内置活动编码共十七个且互不重复
    /// </summary>
    [Fact]
    public void Constants_AreSeventeenAndUnique()
    {
        var values = GetConstantValues();

        Assert.Equal(17, values.Count);
        Assert.Equal(values.Count, values.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// 编码区分大小写，不允许出现仅大小写不同的近似编码
    /// </summary>
    /// <remarks>
    /// 节点解析按序数比较，若同时存在 Http 与 HTTP 会形成两个无法互相兜底的活动，属于设计事故。
    /// </remarks>
    [Fact]
    public void Constants_HaveNoCaseInsensitiveCollision()
    {
        var values = GetConstantValues();

        Assert.Equal(values.Count, values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// 全部编码都是非空且不含空白的标识符
    /// </summary>
    [Fact]
    public void Constants_AreNonEmptyWithoutWhitespace()
    {
        foreach (var value in GetConstantValues())
        {
            Assert.False(string.IsNullOrWhiteSpace(value));
            Assert.DoesNotContain(" ", value);
        }
    }

    /// <summary>
    /// 常量容器是静态类，不可被实例化或继承
    /// </summary>
    [Fact]
    public void Type_IsStaticClass()
    {
        var type = typeof(WorkflowActivityTypes);

        Assert.True(type.IsAbstract);
        Assert.True(type.IsSealed);
    }

    /// <summary>
    /// 读取全部公共字符串常量值
    /// </summary>
    /// <returns>常量值列表</returns>
    private static List<string> GetConstantValues()
    {
        return typeof(WorkflowActivityTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();
    }
}
