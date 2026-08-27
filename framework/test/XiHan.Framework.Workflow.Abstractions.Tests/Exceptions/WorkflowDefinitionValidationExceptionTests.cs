// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Exceptions;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 流程定义校验异常测试
/// </summary>
/// <remarks>
/// 发布定义失败时前端要逐条展示错误，所以异常必须同时提供两样东西：
/// 拼好的整体消息（日志用）与原始错误集合（前端逐条渲染用）。
/// 拼接用的是全角分号，不是英文分号——错误文本本身常含英文标点，用全角才能切得开。
/// </remarks>
public class WorkflowDefinitionValidationExceptionTests
{
    /// <summary>
    /// 多条错误按全角分号拼接进消息
    /// </summary>
    [Fact]
    public void Constructor_WithMultipleErrors_JoinsMessageWithFullWidthSemicolon()
    {
        var exception = new WorkflowDefinitionValidationException(["缺少开始节点", "存在孤立节点"]);

        Assert.Equal("流程定义校验失败：缺少开始节点；存在孤立节点", exception.Message);
    }

    /// <summary>
    /// 单条错误不产生多余分隔符
    /// </summary>
    [Fact]
    public void Constructor_WithSingleError_HasNoTrailingSeparator()
    {
        var exception = new WorkflowDefinitionValidationException(["缺少开始节点"]);

        Assert.Equal("流程定义校验失败：缺少开始节点", exception.Message);
        Assert.DoesNotContain("；", exception.Message);
    }

    /// <summary>
    /// 空错误集合仍能构造，消息只剩前缀
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyErrors_KeepsPrefixOnly()
    {
        var exception = new WorkflowDefinitionValidationException([]);

        Assert.Equal("流程定义校验失败：", exception.Message);
        Assert.Empty(exception.Errors);
    }

    /// <summary>
    /// 原始错误集合按传入顺序原样保留
    /// </summary>
    [Fact]
    public void Errors_PreserveOriginalOrder()
    {
        string[] errors = ["第一条", "第二条", "第三条"];

        var exception = new WorkflowDefinitionValidationException(errors);

        Assert.Equal(3, exception.Errors.Count);
        Assert.Equal("第一条", exception.Errors[0]);
        Assert.Equal("第二条", exception.Errors[1]);
        Assert.Equal("第三条", exception.Errors[2]);
    }

    /// <summary>
    /// 继承自工作流异常，可被统一的协议错误处理捕获
    /// </summary>
    [Fact]
    public void Type_DerivesFromWorkflowException()
    {
        Assert.True(typeof(WorkflowException).IsAssignableFrom(typeof(WorkflowDefinitionValidationException)));
    }

    /// <summary>
    /// 抛出后按基类型捕获仍能取回逐条错误
    /// </summary>
    [Fact]
    public void Throw_CaughtAsWorkflowException_StillExposesErrors()
    {
        WorkflowException? caught = null;

        try
        {
            throw new WorkflowDefinitionValidationException(["缺少结束节点"]);
        }
        catch (WorkflowException exception)
        {
            caught = exception;
        }

        var validation = Assert.IsType<WorkflowDefinitionValidationException>(caught);
        Assert.Single(validation.Errors);
        Assert.Equal("缺少结束节点", validation.Errors[0]);
    }
}
