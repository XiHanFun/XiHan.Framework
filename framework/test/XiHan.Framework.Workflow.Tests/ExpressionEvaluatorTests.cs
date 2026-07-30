// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Expressions;

namespace XiHan.Framework.Workflow.Tests;

/// <summary>
/// 表达式求值器测试
/// </summary>
public class ExpressionEvaluatorTests
{
    private readonly WorkflowExpressionEvaluator _evaluator = new(new TestClock());

    private static Dictionary<string, object?> Variables => new()
    {
        ["amount"] = 20000,
        ["name"] = "张三",
        ["approved"] = true,
        ["days"] = 3.5,
        ["tags"] = new List<object?> { "a", "b", "c" },
        ["order"] = new Dictionary<string, object?> { ["total"] = 99, ["customer"] = "李四" },
        ["empty"] = null
    };

    /// <summary>
    /// 算术与比较
    /// </summary>
    [Theory]
    [InlineData("amount > 10000", true)]
    [InlineData("amount <= 10000", false)]
    [InlineData("amount + 5000 == 25000", true)]
    [InlineData("days * 2 == 7", true)]
    [InlineData("amount % 3 == 2", true)]
    [InlineData("-amount < 0", true)]
    public async Task 算术与比较运算正确(string expression, bool expected)
    {
        Assert.Equal(expected, await _evaluator.EvaluateConditionAsync(expression, Variables));
    }

    /// <summary>
    /// 逻辑与字符串
    /// </summary>
    [Theory]
    [InlineData("approved && amount > 0", true)]
    [InlineData("!approved || name == '张三'", true)]
    [InlineData("name != \"李四\"", true)]
    [InlineData("empty == null", true)]
    [InlineData("startsWith(name, '张')", true)]
    [InlineData("contains(tags, 'b')", true)]
    [InlineData("len(tags) == 3", true)]
    [InlineData("isNullOrEmpty(empty)", true)]
    public async Task 逻辑与字符串函数正确(string expression, bool expected)
    {
        Assert.Equal(expected, await _evaluator.EvaluateConditionAsync(expression, Variables));
    }

    /// <summary>
    /// 点号导航与索引
    /// </summary>
    [Fact]
    public async Task 点号导航与索引访问正确()
    {
        Assert.True(await _evaluator.EvaluateConditionAsync("order.total < 100", Variables));
        Assert.Equal("b", await _evaluator.EvaluateAsync<string>("tags[1]", Variables));
        Assert.Equal("李四", await _evaluator.EvaluateAsync<string>("order['customer']", Variables));
    }

    /// <summary>
    /// JsonElement 归一化（模拟持久化往返）
    /// </summary>
    [Fact]
    public async Task JsonElement变量归一化后可参与运算()
    {
        var element = JsonSerializer.SerializeToElement(new { total = 500, items = new[] { 1, 2 } });
        var variables = new Dictionary<string, object?> { ["order"] = element };

        Assert.True(await _evaluator.EvaluateConditionAsync("order.total == 500", variables));
        Assert.True(await _evaluator.EvaluateConditionAsync("len(order.items) == 2", variables));
    }

    /// <summary>
    /// 模板渲染
    /// </summary>
    [Fact]
    public async Task 模板渲染替换占位()
    {
        var result = await _evaluator.RenderTemplateAsync("{{name}}的请假申请，金额 {{amount + 1}} 元", Variables);
        Assert.Equal("张三的请假申请，金额 20001 元", result);
    }

    /// <summary>
    /// fail-closed：非布尔条件、未知变量、非法语法
    /// </summary>
    [Fact]
    public async Task 非法表达式按失败关闭语义抛出()
    {
        await Assert.ThrowsAsync<WorkflowException>(() => _evaluator.EvaluateConditionAsync("amount + 1", Variables));
        await Assert.ThrowsAsync<WorkflowException>(() => _evaluator.EvaluateConditionAsync("unknownVar > 1", Variables));
        await Assert.ThrowsAsync<WorkflowException>(() => _evaluator.EvaluateConditionAsync("amount >", Variables));
        await Assert.ThrowsAsync<WorkflowException>(() => _evaluator.RenderTemplateAsync("{{amount", Variables));
    }
}
