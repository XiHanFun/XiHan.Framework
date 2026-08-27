// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Templating.Contexts;

namespace XiHan.Framework.Templating.Tests.Contexts;

/// <summary>
/// <see cref="TemplateContext"/> 变量作用域、函数注册与克隆语义的测试
/// </summary>
/// <remarks>
/// 作用域语义是模板渲染的地基，这里锁死三条契约：写入永远落在当前作用域、
/// 读取自内向外逐层查找（内层遮蔽外层）、作用域释放后外层取值必须原样还原。
/// 作用域标识的具体格式属实现细节，不做断言。
/// </remarks>
public class TemplateContextTests
{
    /// <summary>
    /// 未设置过的变量取值为 null
    /// </summary>
    [Fact]
    public void GetVariable_WhenNotSet_ReturnsNull()
    {
        var context = new TemplateContext();

        Assert.Null(context.GetVariable("missing"));
        Assert.False(context.HasVariable("missing"));
    }

    /// <summary>
    /// 设置后可按名取回原值
    /// </summary>
    [Fact]
    public void SetVariable_ThenGetVariable_ReturnsValue()
    {
        var context = new TemplateContext();

        context.SetVariable("name", "曦寒");

        Assert.Equal("曦寒", context.GetVariable("name"));
        Assert.True(context.HasVariable("name"));
    }

    /// <summary>
    /// 值为 null 的变量仍然算「存在」，与「未设置」区分开
    /// </summary>
    [Fact]
    public void SetVariable_WithNullValue_IsStillPresent()
    {
        var context = new TemplateContext();

        context.SetVariable("name", null);

        // HasVariable 查的是键是否存在，null 值不等于变量缺失，这是渲染时判空的前提
        Assert.True(context.HasVariable("name"));
        Assert.Null(context.GetVariable("name"));
    }

    /// <summary>
    /// 同名变量重复设置时后写覆盖先写
    /// </summary>
    [Fact]
    public void SetVariable_SameNameTwice_LastWriteWins()
    {
        var context = new TemplateContext();

        context.SetVariable("name", "旧值");
        context.SetVariable("name", "新值");

        Assert.Equal("新值", context.GetVariable("name"));
    }

    /// <summary>
    /// 移除当前作用域内的变量返回 true 且变量消失
    /// </summary>
    [Fact]
    public void RemoveVariable_InCurrentScope_RemovesAndReturnsTrue()
    {
        var context = new TemplateContext();
        context.SetVariable("name", "曦寒");

        Assert.True(context.RemoveVariable("name"));
        Assert.False(context.HasVariable("name"));
    }

    /// <summary>
    /// 移除不存在的变量返回 false
    /// </summary>
    [Fact]
    public void RemoveVariable_WhenNotPresent_ReturnsFalse()
    {
        var context = new TemplateContext();

        Assert.False(context.RemoveVariable("missing"));
    }

    /// <summary>
    /// 只存在于外层作用域的变量在内层无法被移除
    /// </summary>
    [Fact]
    public void RemoveVariable_WhenOnlyInOuterScope_ReturnsFalse_AndKeepsOuterValue()
    {
        var context = new TemplateContext();
        context.SetVariable("name", "外层");

        using var scope = context.PushScope();

        // 移除只作用于当前作用域，外层变量不受内层影响，否则局部渲染会污染宿主上下文
        Assert.False(context.RemoveVariable("name"));
        Assert.Equal("外层", context.GetVariable("name"));
    }

    /// <summary>
    /// 内层作用域的同名变量遮蔽外层，作用域释放后还原为外层值
    /// </summary>
    [Fact]
    public void PushScope_InnerVariable_ShadowsOuter_AndRestoresAfterDispose()
    {
        var context = new TemplateContext();
        context.SetVariable("name", "外层");

        var scope = context.PushScope();
        context.SetVariable("name", "内层");
        Assert.Equal("内层", context.GetVariable("name"));

        scope.Dispose();

        Assert.Equal("外层", context.GetVariable("name"));
    }

    /// <summary>
    /// 作用域内新增的变量在释放后消失
    /// </summary>
    [Fact]
    public void PushScope_VariableDefinedInside_DisappearsAfterDispose()
    {
        var context = new TemplateContext();

        var scope = context.PushScope();
        context.SetVariable("temp", "临时");
        Assert.True(context.HasVariable("temp"));

        scope.Dispose();

        Assert.False(context.HasVariable("temp"));
        Assert.Null(context.GetVariable("temp"));
    }

    /// <summary>
    /// 多层嵌套作用域按由内向外的顺序取值
    /// </summary>
    [Fact]
    public void PushScope_Nested_ResolvesInnermostFirst()
    {
        var context = new TemplateContext();
        context.SetVariable("level", "根");

        var outer = context.PushScope();
        context.SetVariable("level", "第一层");

        var inner = context.PushScope();
        context.SetVariable("level", "第二层");
        Assert.Equal("第二层", context.GetVariable("level"));

        inner.Dispose();
        Assert.Equal("第一层", context.GetVariable("level"));

        outer.Dispose();
        Assert.Equal("根", context.GetVariable("level"));
    }

    /// <summary>
    /// 作用域释放器重复释放不会多弹出一层作用域
    /// </summary>
    [Fact]
    public void PushScope_DisposeTwice_DoesNotPopExtraScope()
    {
        var context = new TemplateContext();
        context.SetVariable("level", "根");

        var outer = context.PushScope();
        context.SetVariable("level", "第一层");

        outer.Dispose();
        outer.Dispose();

        // 第二次释放若再弹一层就会连根作用域一起弹掉，根变量必须仍然可见
        Assert.Equal("根", context.GetVariable("level"));
    }

    /// <summary>
    /// 变量名集合是所有活动作用域的去重并集
    /// </summary>
    [Fact]
    public void GetVariableNames_AcrossScopes_ReturnsDistinctUnion()
    {
        var context = new TemplateContext();
        context.SetVariable("shared", "外层");
        context.SetVariable("outerOnly", "外层");

        using var scope = context.PushScope();
        context.SetVariable("shared", "内层");
        context.SetVariable("innerOnly", "内层");

        var names = context.GetVariableNames().ToList();

        Assert.Equal(3, names.Count);
        Assert.Contains("shared", names);
        Assert.Contains("outerOnly", names);
        Assert.Contains("innerOnly", names);
    }

    /// <summary>
    /// 未注册的函数取值为 null
    /// </summary>
    [Fact]
    public void GetFunction_WhenNotSet_ReturnsNull()
    {
        var context = new TemplateContext();

        Assert.Null(context.GetFunction("missing"));
    }

    /// <summary>
    /// 注册后取回的是同一个委托实例
    /// </summary>
    [Fact]
    public void SetFunction_ThenGetFunction_ReturnsSameDelegate()
    {
        var context = new TemplateContext();
        Func<int, int> increase = value => value + 1;

        context.SetFunction("increase", increase);

        Assert.Same(increase, context.GetFunction("increase"));
    }

    /// <summary>
    /// 同名函数重复注册时后注册的生效
    /// </summary>
    [Fact]
    public void SetFunction_SameName_Overwrites()
    {
        var context = new TemplateContext();
        Func<int, int> first = value => value + 1;
        Func<int, int> second = value => value + 2;

        context.SetFunction("calc", first);
        context.SetFunction("calc", second);

        Assert.Same(second, context.GetFunction("calc"));
    }

    /// <summary>
    /// 函数不参与作用域，作用域释放后依然可用
    /// </summary>
    [Fact]
    public void SetFunction_InsideScope_SurvivesScopeDispose()
    {
        var context = new TemplateContext();
        Func<int, int> increase = value => value + 1;

        var scope = context.PushScope();
        context.SetFunction("increase", increase);
        scope.Dispose();

        // 函数表与作用域栈是两套存储，模板片段注册的辅助函数不应随局部作用域消失
        Assert.Same(increase, context.GetFunction("increase"));
    }

    /// <summary>
    /// 克隆会带上当前作用域的变量与全部函数
    /// </summary>
    [Fact]
    public void Clone_CopiesCurrentScopeVariablesAndFunctions()
    {
        var context = new TemplateContext();
        context.SetVariable("name", "曦寒");
        Func<int, int> increase = value => value + 1;
        context.SetFunction("increase", increase);

        var cloned = context.Clone();

        Assert.NotSame(context, cloned);
        Assert.Equal("曦寒", cloned.GetVariable("name"));
        Assert.Same(increase, cloned.GetFunction("increase"));
    }

    /// <summary>
    /// 克隆体与源上下文互不影响
    /// </summary>
    [Fact]
    public void Clone_IsIndependentFromSource()
    {
        var context = new TemplateContext();
        context.SetVariable("name", "原始");

        var cloned = context.Clone();
        cloned.SetVariable("name", "克隆");
        cloned.SetVariable("cloneOnly", "只在克隆体");

        Assert.Equal("原始", context.GetVariable("name"));
        Assert.False(context.HasVariable("cloneOnly"));
        Assert.Equal("克隆", cloned.GetVariable("name"));
    }

    /// <summary>
    /// 多线程并发写入不同变量不会丢值
    /// </summary>
    [Fact]
    public void SetVariable_FromMultipleThreads_KeepsAllValues()
    {
        var context = new TemplateContext();
        const int count = 200;

        Parallel.For(0, count, index => context.SetVariable($"key{index}", index));

        // 作用域内部用并发字典承载，声称可并发写；丢值意味着承诺不成立
        Assert.Equal(count, context.GetVariableNames().Count());
        for (var index = 0; index < count; index++)
        {
            Assert.True(context.HasVariable($"key{index}"));
        }
    }
}
