// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Templating.Contexts;

namespace XiHan.Framework.Templating.Tests.Contexts;

/// <summary>
/// <see cref="TemplateContextFactory"/> 三种上下文构造入口的测试
/// </summary>
/// <remarks>
/// 工厂的契约是「模型对象只取公共实例可读属性」，字段、私有属性、只写属性都不进上下文。
/// 这条边界决定了模板里能引用哪些名字，必须锁死。
/// </remarks>
public class TemplateContextFactoryTests
{
    /// <summary>
    /// 无参创建得到空上下文
    /// </summary>
    [Fact]
    public void CreateContext_WithoutArguments_ReturnsEmptyContext()
    {
        var factory = new TemplateContextFactory();

        var context = factory.CreateContext();

        Assert.NotNull(context);
        Assert.Empty(context.GetVariableNames());
    }

    /// <summary>
    /// 每次创建返回相互独立的实例
    /// </summary>
    [Fact]
    public void CreateContext_CalledTwice_ReturnsIndependentInstances()
    {
        var factory = new TemplateContextFactory();

        var first = factory.CreateContext();
        var second = factory.CreateContext();
        first.SetVariable("name", "曦寒");

        Assert.NotSame(first, second);
        Assert.False(second.HasVariable("name"));
    }

    /// <summary>
    /// 用变量字典创建时逐项写入上下文
    /// </summary>
    [Fact]
    public void CreateContext_WithVariables_CopiesAllEntries()
    {
        var factory = new TemplateContextFactory();
        var variables = new Dictionary<string, object?>
        {
            ["name"] = "曦寒",
            ["count"] = 3,
            ["empty"] = null
        };

        var context = factory.CreateContext(variables);

        Assert.Equal("曦寒", context.GetVariable("name"));
        Assert.Equal(3, context.GetVariable("count"));
        Assert.True(context.HasVariable("empty"));
        Assert.Equal(3, context.GetVariableNames().Count());
    }

    /// <summary>
    /// 用空字典创建得到空上下文
    /// </summary>
    [Fact]
    public void CreateContext_WithEmptyVariables_ReturnsEmptyContext()
    {
        var factory = new TemplateContextFactory();

        var context = factory.CreateContext(new Dictionary<string, object?>());

        Assert.Empty(context.GetVariableNames());
    }

    /// <summary>
    /// 用模型对象创建时只取公共实例可读属性
    /// </summary>
    [Fact]
    public void CreateContext_WithModel_CopiesPublicReadablePropertiesOnly()
    {
        var factory = new TemplateContextFactory();
        var model = new ContextModel { Name = "曦寒", Age = 3, PublicField = "字段" };

        var context = factory.CreateContext(model);

        Assert.Equal("曦寒", context.GetVariable("Name"));
        Assert.Equal(3, context.GetVariable("Age"));
        // 字段、私有属性、只写属性都不是「公共实例可读属性」，不应出现在上下文里
        Assert.False(context.HasVariable("PublicField"));
        Assert.False(context.HasVariable("Hidden"));
        Assert.False(context.HasVariable("WriteOnly"));
    }

    /// <summary>
    /// 模型对象为 null 时得到空上下文而不是抛异常
    /// </summary>
    [Fact]
    public void CreateContext_WithNullModel_ReturnsEmptyContext()
    {
        var factory = new TemplateContextFactory();

        var context = factory.CreateContext((object)null!);

        Assert.Empty(context.GetVariableNames());
    }

    /// <summary>
    /// 构建器由工厂创建，构建结果落到同一套上下文实现上
    /// </summary>
    [Fact]
    public void CreateBuilder_ReturnsBuilder_ThatBuildsContext()
    {
        var factory = new TemplateContextFactory();

        var builder = factory.CreateBuilder();
        var context = builder.AddVariable("name", "曦寒").Build();

        Assert.NotNull(builder);
        Assert.Equal("曦寒", context.GetVariable("name"));
    }

    /// <summary>
    /// 上下文工厂测试用的模型对象
    /// </summary>
    public sealed class ContextModel
    {
        /// <summary>
        /// 公共可读属性
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 公共可读属性
        /// </summary>
        public int Age { get; set; }

        /// <summary>
        /// 只写属性，不应被提取
        /// </summary>
        public string WriteOnly
        {
            set => _writeOnly = value;
        }

        /// <summary>
        /// 公共字段，不应被提取
        /// </summary>
        public string PublicField = string.Empty;

        /// <summary>
        /// 私有属性，不应被提取
        /// </summary>
        private string Hidden { get; set; } = string.Empty;

        private string _writeOnly = string.Empty;

        /// <summary>
        /// 供内部字段被读取一次，避免编译器认为字段从未使用
        /// </summary>
        /// <returns>只写属性写入的值</returns>
        public string GetWriteOnlyValue() => _writeOnly + Hidden;
    }
}
