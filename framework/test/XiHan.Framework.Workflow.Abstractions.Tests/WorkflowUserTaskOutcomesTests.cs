// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 人工任务标准结果常量测试
/// </summary>
/// <remarks>
/// 这三个值会作为 outcome 变量参与出边条件求值，流程定义里写死成 <c>outcome == 'approved'</c>，
/// 所以必须锁死为全小写字面值——改成 Approved 会让所有存量定义的审批分支静默走不通。
/// </remarks>
public class WorkflowUserTaskOutcomesTests
{
    /// <summary>
    /// 标准结果字面值锁定为全小写
    /// </summary>
    [Fact]
    public void Values_AreLockedLowercase()
    {
        Assert.Equal("approved", WorkflowUserTaskOutcomes.Approved);
        Assert.Equal("rejected", WorkflowUserTaskOutcomes.Rejected);
        Assert.Equal("timeout", WorkflowUserTaskOutcomes.Timeout);
    }

    /// <summary>
    /// 标准结果共三个且互不重复
    /// </summary>
    [Fact]
    public void Constants_AreThreeAndUnique()
    {
        var values = GetConstantValues();

        Assert.Equal(3, values.Count);
        Assert.Equal(values.Count, values.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// 全部标准结果均为小写，与流程定义里的字面量写法一致
    /// </summary>
    [Fact]
    public void Constants_AreAllLowercase()
    {
        foreach (var value in GetConstantValues())
        {
            Assert.Equal(value.ToLowerInvariant(), value);
        }
    }

    /// <summary>
    /// 允许业务自定义结果，标准值不构成封闭集合
    /// </summary>
    /// <remarks>
    /// outcome 是普通字符串变量而非枚举，这里用一个自定义值验证它不会被标准集合限制住。
    /// </remarks>
    [Fact]
    public void CustomOutcome_IsNotRestrictedByStandardValues()
    {
        const string custom = "returned";

        Assert.DoesNotContain(custom, GetConstantValues());
        Assert.NotEqual(WorkflowUserTaskOutcomes.Approved, custom);
        Assert.NotEqual(WorkflowUserTaskOutcomes.Rejected, custom);
    }

    /// <summary>
    /// 超时结果与节点超时书签种类配套使用但不同名
    /// </summary>
    /// <remarks>
    /// 书签种类是 NodeTimeout（引擎侧标识），办理结果是 timeout（表达式侧字面量），两者语义相关但取值不同，
    /// 不能互相替换。
    /// </remarks>
    [Fact]
    public void Timeout_DiffersFromNodeTimeoutBookmarkKind()
    {
        Assert.NotEqual(WorkflowBookmarkKinds.NodeTimeout, WorkflowUserTaskOutcomes.Timeout);
        Assert.Equal("timeout", WorkflowUserTaskOutcomes.Timeout);
        Assert.Equal("NodeTimeout", WorkflowBookmarkKinds.NodeTimeout);
    }

    /// <summary>
    /// 常量容器是静态类
    /// </summary>
    [Fact]
    public void Type_IsStaticClass()
    {
        var type = typeof(WorkflowUserTaskOutcomes);

        Assert.True(type.IsAbstract);
        Assert.True(type.IsSealed);
    }

    /// <summary>
    /// 读取全部公共字符串常量值
    /// </summary>
    /// <returns>常量值列表</returns>
    private static List<string> GetConstantValues()
    {
        return [.. typeof(WorkflowUserTaskOutcomes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)];
    }
}
