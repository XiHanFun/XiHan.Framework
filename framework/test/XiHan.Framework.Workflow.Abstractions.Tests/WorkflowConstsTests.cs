// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 工作流通用常量测试
/// </summary>
/// <remarks>
/// 这些常量分三类，都是跨进程/跨版本的硬协议：
/// 锁资源键前缀（改了等于换锁，滚动发布期间会双写）、
/// 变量名与恢复输入键（改了等于流程定义里的表达式全部失效）、
/// 节点私有状态键（改了等于旧实例的子流程回调隔离信息丢失）。
/// 因此逐条锁死字面值，并锁死"变量名不带下划线前缀、内部键带双下划线前缀"这条命名口径。
/// </remarks>
public class WorkflowConstsTests
{
    /// <summary>
    /// 实例执行锁资源键前缀锁定
    /// </summary>
    [Fact]
    public void InstanceLockKeyPrefix_IsLocked()
    {
        Assert.Equal("default:workflow:lock:instance:", WorkflowConsts.InstanceLockKeyPrefix);
        Assert.EndsWith(":", WorkflowConsts.InstanceLockKeyPrefix);
    }

    /// <summary>
    /// 参与出边条件求值的变量名锁定
    /// </summary>
    [Fact]
    public void ExpressionVariableNames_AreLocked()
    {
        Assert.Equal("outcome", WorkflowConsts.OutcomeVariableName);
        Assert.Equal("lastError", WorkflowConsts.LastErrorVariableName);
    }

    /// <summary>
    /// 子流程回调输入键锁定
    /// </summary>
    [Fact]
    public void ChildCallbackInputKeys_AreLocked()
    {
        Assert.Equal("childInstanceId", WorkflowConsts.ChildInstanceIdInputKey);
        Assert.Equal("childStatus", WorkflowConsts.ChildStatusInputKey);
        Assert.Equal("childVariables", WorkflowConsts.ChildVariablesInputKey);
        Assert.Equal("childFaultMessage", WorkflowConsts.ChildFaultMessageInputKey);
    }

    /// <summary>
    /// 引擎内部标记键锁定并以双下划线开头
    /// </summary>
    /// <remarks>
    /// 双下划线前缀是与业务变量的隔离约定：业务表达式不会写成 __timeout，
    /// 去掉前缀就可能被业务同名变量覆盖，从而伪造超时或篡改子实例隔离集合。
    /// </remarks>
    [Fact]
    public void InternalKeys_AreLockedAndDoubleUnderscorePrefixed()
    {
        Assert.Equal("__timeout", WorkflowConsts.TimeoutInputKey);
        Assert.Equal("__childInstanceIds", WorkflowConsts.ChildInstanceIdsStateKey);
        Assert.StartsWith("__", WorkflowConsts.TimeoutInputKey);
        Assert.StartsWith("__", WorkflowConsts.ChildInstanceIdsStateKey);
    }

    /// <summary>
    /// 面向表达式的变量名不带内部前缀，可被流程定义直接引用
    /// </summary>
    [Fact]
    public void ExpressionFacingNames_DoNotUseInternalPrefix()
    {
        Assert.DoesNotContain("__", WorkflowConsts.OutcomeVariableName);
        Assert.DoesNotContain("__", WorkflowConsts.LastErrorVariableName);
        Assert.DoesNotContain("__", WorkflowConsts.ChildInstanceIdInputKey);
        Assert.DoesNotContain("__", WorkflowConsts.ChildStatusInputKey);
        Assert.DoesNotContain("__", WorkflowConsts.ChildVariablesInputKey);
        Assert.DoesNotContain("__", WorkflowConsts.ChildFaultMessageInputKey);
    }

    /// <summary>
    /// 常量共九个且互不重复
    /// </summary>
    [Fact]
    public void Constants_AreNineAndUnique()
    {
        var values = GetConstantValues();

        Assert.Equal(9, values.Count);
        Assert.Equal(values.Count, values.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// 常量容器是静态类
    /// </summary>
    [Fact]
    public void Type_IsStaticClass()
    {
        var type = typeof(WorkflowConsts);

        Assert.True(type.IsAbstract);
        Assert.True(type.IsSealed);
    }

    /// <summary>
    /// 读取全部公共字符串常量值
    /// </summary>
    /// <returns>常量值列表</returns>
    private static List<string> GetConstantValues()
    {
        return [.. typeof(WorkflowConsts)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)];
    }
}
