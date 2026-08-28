// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Templating.Contexts;

namespace XiHan.Framework.Templating.Tests.Contexts;

/// <summary>
/// <see cref="TemplateContextBuilder"/> 流式装配与全局函数导入的测试
/// </summary>
/// <remarks>
/// 全局函数导入按参数个数映射到 Action/Func 委托，超过四个参数的方法会被静默丢弃，
/// 这条「静默丢弃」的边界最容易在重构中漂移，必须显式锁死。
/// </remarks>
public class TemplateContextBuilderTests
{
    /// <summary>
    /// 每个装配方法都返回自身以支持链式调用
    /// </summary>
    [Fact]
    public void FluentMethods_AllReturnSameBuilderInstance()
    {
        var builder = new TemplateContextBuilder(new TemplateContextFactory());
        Func<int> constant = () => 1;

        Assert.Same(builder, builder.AddVariable("a", 1));
        Assert.Same(builder, builder.AddVariables(new Dictionary<string, object?> { ["b"] = 2 }));
        Assert.Same(builder, builder.AddObject(new BuilderModel()));
        Assert.Same(builder, builder.AddFunction("f", constant));
        Assert.Same(builder, builder.AddGlobalFunctions(typeof(GlobalFunctionHost)));
    }

    /// <summary>
    /// 添加的变量出现在构建结果里
    /// </summary>
    [Fact]
    public void AddVariable_ThenBuild_ContextHasVariable()
    {
        var builder = new TemplateContextBuilder(new TemplateContextFactory());

        var context = builder.AddVariable("name", "曦寒").Build();

        Assert.Equal("曦寒", context.GetVariable("name"));
    }

    /// <summary>
    /// 同名变量重复添加时后添加的生效
    /// </summary>
    [Fact]
    public void AddVariable_SameName_LastWins()
    {
        var builder = new TemplateContextBuilder(new TemplateContextFactory());

        var context = builder
            .AddVariable("name", "旧值")
            .AddVariable("name", "新值")
            .Build();

        Assert.Equal("新值", context.GetVariable("name"));
    }

    /// <summary>
    /// 批量添加字典会逐项合并
    /// </summary>
    [Fact]
    public void AddVariables_MergesAllEntries()
    {
        var builder = new TemplateContextBuilder(new TemplateContextFactory());

        var context = builder
            .AddVariable("a", 1)
            .AddVariables(new Dictionary<string, object?> { ["b"] = 2, ["c"] = 3 })
            .Build();

        Assert.Equal(3, context.GetVariableNames().Count());
        Assert.Equal(2, context.GetVariable("b"));
    }

    /// <summary>
    /// 添加对象时按属性名展开
    /// </summary>
    [Fact]
    public void AddObject_WithoutPrefix_UsesPropertyNames()
    {
        var builder = new TemplateContextBuilder(new TemplateContextFactory());

        var context = builder.AddObject(new BuilderModel { Name = "曦寒", Age = 3 }).Build();

        Assert.Equal("曦寒", context.GetVariable("Name"));
        Assert.Equal(3, context.GetVariable("Age"));
    }

    /// <summary>
    /// 指定前缀时变量名带上「前缀.属性名」
    /// </summary>
    [Fact]
    public void AddObject_WithPrefix_PrefixesPropertyNames()
    {
        var builder = new TemplateContextBuilder(new TemplateContextFactory());

        var context = builder.AddObject(new BuilderModel { Name = "曦寒" }, "user").Build();

        Assert.Equal("曦寒", context.GetVariable("user.Name"));
        Assert.False(context.HasVariable("Name"));
    }

    /// <summary>
    /// 空前缀等价于不带前缀
    /// </summary>
    [Fact]
    public void AddObject_WithEmptyPrefix_BehavesAsNoPrefix()
    {
        var builder = new TemplateContextBuilder(new TemplateContextFactory());

        var context = builder.AddObject(new BuilderModel { Name = "曦寒" }, string.Empty).Build();

        Assert.Equal("曦寒", context.GetVariable("Name"));
    }

    /// <summary>
    /// 对象为 null 时静默跳过而不抛异常
    /// </summary>
    [Fact]
    public void AddObject_WhenObjectIsNull_AddsNothing()
    {
        var builder = new TemplateContextBuilder(new TemplateContextFactory());

        var context = builder.AddObject(null!).Build();

        Assert.Empty(context.GetVariableNames());
    }

    /// <summary>
    /// 添加的函数出现在构建结果里
    /// </summary>
    [Fact]
    public void AddFunction_ThenBuild_ContextHasFunction()
    {
        var builder = new TemplateContextBuilder(new TemplateContextFactory());
        Func<int, int> increase = value => value + 1;

        var context = builder.AddFunction("increase", increase).Build();

        Assert.Same(increase, context.GetFunction("increase"));
    }

    /// <summary>
    /// 导入类型的公共静态有返回值方法映射为 Func 委托
    /// </summary>
    [Fact]
    public void AddGlobalFunctions_RegistersPublicStaticFunction()
    {
        var builder = new TemplateContextBuilder(new TemplateContextFactory());

        var context = builder.AddGlobalFunctions(typeof(GlobalFunctionHost)).Build();

        var add = Assert.IsType<Func<int, int, int>>(context.GetFunction("Add"));
        Assert.Equal(3, add(1, 2));
    }

    /// <summary>
    /// 导入类型的公共静态无返回值方法映射为 Action 委托
    /// </summary>
    [Fact]
    public void AddGlobalFunctions_RegistersPublicStaticVoidMethodAsAction()
    {
        var builder = new TemplateContextBuilder(new TemplateContextFactory());

        var context = builder.AddGlobalFunctions(typeof(GlobalFunctionHost)).Build();

        Assert.IsType<Action>(context.GetFunction("NoOp"));
    }

    /// <summary>
    /// 超过四个参数的方法被静默丢弃
    /// </summary>
    [Fact]
    public void AddGlobalFunctions_SkipsMethodWithMoreThanFourParameters()
    {
        var builder = new TemplateContextBuilder(new TemplateContextFactory());

        var context = builder.AddGlobalFunctions(typeof(GlobalFunctionHost)).Build();

        // Func 最多映射到四入参一返回值，第五个参数直接落到「不支持」分支
        Assert.Null(context.GetFunction("TooManyParameters"));
    }

    /// <summary>
    /// 实例方法不会被导入
    /// </summary>
    [Fact]
    public void AddGlobalFunctions_SkipsInstanceMethod()
    {
        var builder = new TemplateContextBuilder(new TemplateContextFactory());

        var context = builder.AddGlobalFunctions(typeof(GlobalFunctionHost)).Build();

        Assert.Null(context.GetFunction("InstanceMethod"));
    }

    /// <summary>
    /// 静态属性的访问器不会被当成函数导入
    /// </summary>
    [Fact]
    public void AddGlobalFunctions_SkipsPropertyAccessors()
    {
        var builder = new TemplateContextBuilder(new TemplateContextFactory());

        var context = builder.AddGlobalFunctions(typeof(GlobalFunctionHost)).Build();

        // 属性访问器是特殊名方法，导入后会出现 get_Version 这种噪音名字
        Assert.Null(context.GetFunction("get_Version"));
        Assert.Null(context.GetFunction("Version"));
    }

    /// <summary>
    /// 重复构建得到内容相同但相互独立的上下文
    /// </summary>
    [Fact]
    public void Build_CalledTwice_ReturnsIndependentContexts()
    {
        var builder = new TemplateContextBuilder(new TemplateContextFactory());
        builder.AddVariable("name", "曦寒");

        var first = builder.Build();
        var second = builder.Build();
        first.SetVariable("name", "改过了");

        Assert.NotSame(first, second);
        Assert.Equal("曦寒", second.GetVariable("name"));
    }

    /// <summary>
    /// 构建器测试用的模型对象
    /// </summary>
    public sealed class BuilderModel
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 年龄
        /// </summary>
        public int Age { get; set; }
    }

    /// <summary>
    /// 全局函数导入测试用的宿主类型
    /// </summary>
    /// <remarks>
    /// 必须是公共类型：导入走 <see cref="Delegate.CreateDelegate(Type, System.Reflection.MethodInfo)"/>，
    /// 跨程序集创建委托会做可访问性检查。
    /// </remarks>
    public sealed class GlobalFunctionHost
    {
        /// <summary>
        /// 静态属性，其访问器不应被导入
        /// </summary>
        public static int Version => 1;

        /// <summary>
        /// 两入参一返回值的静态方法
        /// </summary>
        /// <param name="left">左值</param>
        /// <param name="right">右值</param>
        /// <returns>两数之和</returns>
        public static int Add(int left, int right) => left + right;

        /// <summary>
        /// 无入参无返回值的静态方法
        /// </summary>
        public static void NoOp()
        {
        }

        /// <summary>
        /// 五入参的静态方法，超出可映射的委托形状
        /// </summary>
        /// <param name="first">第一个参数</param>
        /// <param name="second">第二个参数</param>
        /// <param name="third">第三个参数</param>
        /// <param name="fourth">第四个参数</param>
        /// <param name="fifth">第五个参数</param>
        /// <returns>参数之和</returns>
        public static int TooManyParameters(int first, int second, int third, int fourth, int fifth)
            => first + second + third + fourth + fifth;

        /// <summary>
        /// 实例方法，不应被导入
        /// </summary>
        /// <returns>固定值</returns>
        public int InstanceMethod() => 1;
    }
}
