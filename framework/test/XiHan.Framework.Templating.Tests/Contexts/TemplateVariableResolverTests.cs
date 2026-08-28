// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Templating.Contexts;

namespace XiHan.Framework.Templating.Tests.Contexts;

/// <summary>
/// <see cref="TemplateVariableResolver"/> 变量表达式解析的测试
/// </summary>
/// <remarks>
/// 解析器对「取不到」统一返回 null（缺失变量、缺失属性、越界索引都不抛），
/// 只有表达式本身写错（索引不是整数）才抛异常。这条「缺失容忍、语法不容忍」的分界是核心契约。
/// </remarks>
public class TemplateVariableResolverTests
{
    /// <summary>
    /// 空表达式或空白表达式解析为 null
    /// </summary>
    /// <param name="expression">待解析的表达式</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveVariable_WhenExpressionBlank_ReturnsNull(string expression)
    {
        var resolver = new TemplateVariableResolver();
        var context = new TemplateContext();

        Assert.Null(resolver.ResolveVariable(expression, context));
    }

    /// <summary>
    /// 简单变量名直接从上下文取值
    /// </summary>
    [Fact]
    public void ResolveVariable_SimpleName_ReadsFromContext()
    {
        var resolver = new TemplateVariableResolver();
        var context = new TemplateContext();
        context.SetVariable("name", "曦寒");

        Assert.Equal("曦寒", resolver.ResolveVariable("name", context));
    }

    /// <summary>
    /// 上下文里没有的简单变量解析为 null
    /// </summary>
    [Fact]
    public void ResolveVariable_WhenVariableMissing_ReturnsNull()
    {
        var resolver = new TemplateVariableResolver();
        var context = new TemplateContext();

        Assert.Null(resolver.ResolveVariable("missing", context));
    }

    /// <summary>
    /// 多级属性路径逐层取值
    /// </summary>
    [Fact]
    public void ResolveVariable_NestedPath_WalksEachSegment()
    {
        var resolver = new TemplateVariableResolver();
        var context = new TemplateContext();
        context.SetVariable("user", new UserModel { Profile = new ProfileModel { Name = "曦寒", Age = 3 } });

        Assert.Equal("曦寒", resolver.ResolveVariable("user.Profile.Name", context));
        Assert.Equal(3, resolver.ResolveVariable("user.Profile.Age", context));
    }

    /// <summary>
    /// 路径中间取不到时整体返回 null 而不抛异常
    /// </summary>
    [Fact]
    public void ResolveVariable_WhenSegmentMissing_ReturnsNull()
    {
        var resolver = new TemplateVariableResolver();
        var context = new TemplateContext();
        context.SetVariable("user", new UserModel());

        Assert.Null(resolver.ResolveVariable("user.NotExist", context));
        Assert.Null(resolver.ResolveVariable("user.NotExist.Deeper", context));
        Assert.Null(resolver.ResolveVariable("noSuchRoot.Profile", context));
    }

    /// <summary>
    /// 数组下标表达式取到对应元素
    /// </summary>
    [Fact]
    public void ResolveVariable_ArrayIndex_ReturnsElement()
    {
        var resolver = new TemplateVariableResolver();
        var context = new TemplateContext();
        context.SetVariable("tags", new[] { "甲", "乙", "丙" });

        Assert.Equal("乙", resolver.ResolveVariable("tags[1]", context));
    }

    /// <summary>
    /// 列表下标表达式取到对应元素
    /// </summary>
    [Fact]
    public void ResolveVariable_ListIndex_ReturnsElement()
    {
        var resolver = new TemplateVariableResolver();
        var context = new TemplateContext();
        context.SetVariable("tags", new List<string> { "甲", "乙" });

        Assert.Equal("甲", resolver.ResolveVariable("tags[0]", context));
    }

    /// <summary>
    /// 下标越界或为负时返回 null
    /// </summary>
    /// <param name="expression">带下标的表达式</param>
    [Theory]
    [InlineData("tags[9]")]
    [InlineData("tags[-1]")]
    public void ResolveVariable_WhenIndexOutOfRange_ReturnsNull(string expression)
    {
        var resolver = new TemplateVariableResolver();
        var context = new TemplateContext();
        context.SetVariable("tags", new[] { "甲", "乙" });

        Assert.Null(resolver.ResolveVariable(expression, context));
    }

    /// <summary>
    /// 下标元素后可以继续走属性路径
    /// </summary>
    [Fact]
    public void ResolveVariable_IndexThenProperty_ResolvesBoth()
    {
        var resolver = new TemplateVariableResolver();
        var context = new TemplateContext();
        context.SetVariable("users", new[]
        {
            new UserModel { Profile = new ProfileModel { Name = "第一个" } },
            new UserModel { Profile = new ProfileModel { Name = "第二个" } }
        });

        Assert.Equal("第二个", resolver.ResolveVariable("users[1].Profile.Name", context));
    }

    /// <summary>
    /// 下标不是整数时抛出无效表达式异常
    /// </summary>
    [Fact]
    public void ResolveVariable_WhenIndexNotInteger_ThrowsInvalidOperationException()
    {
        var resolver = new TemplateVariableResolver();
        var context = new TemplateContext();
        context.SetVariable("tags", new[] { "甲" });

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            resolver.ResolveVariable("tags[abc]", context);
        });

        // 语法写错必须炸出来，而不是和「取不到」一样被吞成 null
        Assert.Contains("无效的数组索引", exception.Message);
    }

    /// <summary>
    /// 目标对象为 null 或路径为空时属性路径解析返回 null
    /// </summary>
    [Fact]
    public void ResolvePropertyPath_WhenTargetOrPathBlank_ReturnsNull()
    {
        var resolver = new TemplateVariableResolver();

        Assert.Null(resolver.ResolvePropertyPath(null!, "Name"));
        Assert.Null(resolver.ResolvePropertyPath(new ProfileModel(), string.Empty));
        Assert.Null(resolver.ResolvePropertyPath(new ProfileModel(), "   "));
    }

    /// <summary>
    /// 属性路径可以读到公共字段
    /// </summary>
    [Fact]
    public void ResolvePropertyPath_ReadsPublicField()
    {
        var resolver = new TemplateVariableResolver();
        var model = new ProfileModel { Tag = "标签" };

        Assert.Equal("标签", resolver.ResolvePropertyPath(model, "Tag"));
    }

    /// <summary>
    /// 属性路径可以读到字典键
    /// </summary>
    [Fact]
    public void ResolvePropertyPath_ReadsDictionaryEntry()
    {
        var resolver = new TemplateVariableResolver();
        var dictionary = new Dictionary<string, object?> { ["Nick"] = "小曦" };

        Assert.Equal("小曦", resolver.ResolvePropertyPath(dictionary, "Nick"));
        Assert.Null(resolver.ResolvePropertyPath(dictionary, "NotExist"));
    }

    /// <summary>
    /// 设置嵌套属性会写到最内层对象上
    /// </summary>
    [Fact]
    public void SetPropertyValue_NestedPath_WritesToLeafObject()
    {
        var resolver = new TemplateVariableResolver();
        var user = new UserModel { Profile = new ProfileModel { Name = "旧名" } };

        resolver.SetPropertyValue(user, "Profile.Name", "新名");

        Assert.Equal("新名", user.Profile.Name);
    }

    /// <summary>
    /// 设置属性时按目标类型做转换
    /// </summary>
    [Fact]
    public void SetPropertyValue_ConvertsValueToTargetType()
    {
        var resolver = new TemplateVariableResolver();
        var profile = new ProfileModel();

        resolver.SetPropertyValue(profile, "Age", "42");

        Assert.Equal(42, profile.Age);
    }

    /// <summary>
    /// 设置不存在的路径不抛异常也不产生副作用
    /// </summary>
    [Fact]
    public void SetPropertyValue_WhenPathMissing_DoesNothing()
    {
        var resolver = new TemplateVariableResolver();
        var profile = new ProfileModel { Name = "原值" };

        resolver.SetPropertyValue(profile, "NotExist", "无所谓");
        resolver.SetPropertyValue(profile, "NotExist.Deeper", "无所谓");
        resolver.SetPropertyValue(null!, "Name", "无所谓");
        resolver.SetPropertyValue(profile, string.Empty, "无所谓");

        Assert.Equal("原值", profile.Name);
    }

    /// <summary>
    /// 设置字典键会写入字典
    /// </summary>
    [Fact]
    public void SetPropertyValue_OnDictionary_WritesEntry()
    {
        var resolver = new TemplateVariableResolver();
        var dictionary = new Dictionary<string, object?>();

        resolver.SetPropertyValue(dictionary, "Nick", "小曦");

        Assert.Equal("小曦", dictionary["Nick"]);
    }

    /// <summary>
    /// 调用存在的方法返回其结果
    /// </summary>
    [Fact]
    public void InvokeMethod_WithMatchingArguments_ReturnsResult()
    {
        var resolver = new TemplateVariableResolver();
        var model = new ProfileModel();

        Assert.Equal("你好，曦寒", resolver.InvokeMethod(model, "Greet", "曦寒"));
    }

    /// <summary>
    /// 调用无参方法返回其结果
    /// </summary>
    [Fact]
    public void InvokeMethod_WithoutArguments_ReturnsResult()
    {
        var resolver = new TemplateVariableResolver();
        var model = new ProfileModel { Name = "曦寒" };

        Assert.Equal("曦寒", resolver.InvokeMethod(model, "Describe"));
    }

    /// <summary>
    /// 参数类型不精确匹配时走兼容匹配并做类型转换
    /// </summary>
    [Fact]
    public void InvokeMethod_WithConvertibleArgument_ConvertsAndInvokes()
    {
        var resolver = new TemplateVariableResolver();
        var model = new ProfileModel();

        // 模板里的字面量常常是字符串，兼容匹配是让 {{ repeat("3") }} 这类写法能跑通的关键
        Assert.Equal("***", resolver.InvokeMethod(model, "Repeat", "3"));
    }

    /// <summary>
    /// 方法名不存在或参数个数对不上时返回 null
    /// </summary>
    [Fact]
    public void InvokeMethod_WhenMethodNotFound_ReturnsNull()
    {
        var resolver = new TemplateVariableResolver();
        var model = new ProfileModel();

        Assert.Null(resolver.InvokeMethod(model, "NotExist"));
        Assert.Null(resolver.InvokeMethod(model, "Greet", "甲", "乙"));
        Assert.Null(resolver.InvokeMethod(null!, "Greet", "甲"));
        Assert.Null(resolver.InvokeMethod(model, string.Empty));
    }

    /// <summary>
    /// 被调用方法抛异常时包装为无效操作异常并带上方法名
    /// </summary>
    [Fact]
    public void InvokeMethod_WhenTargetThrows_WrapsInInvalidOperationException()
    {
        var resolver = new TemplateVariableResolver();
        var model = new ProfileModel();

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            resolver.InvokeMethod(model, "Boom");
        });

        Assert.Contains("Boom", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    /// <summary>
    /// 变量解析测试用的档案模型
    /// </summary>
    public sealed class ProfileModel
    {
        /// <summary>
        /// 公共字段
        /// </summary>
        public string Tag = string.Empty;

        /// <summary>
        /// 姓名
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 年龄
        /// </summary>
        public int Age { get; set; }

        /// <summary>
        /// 只读属性
        /// </summary>
        public string ReadOnlyName => Name;

        /// <summary>
        /// 带参数的方法
        /// </summary>
        /// <param name="who">对谁问好</param>
        /// <returns>问候语</returns>
        public string Greet(string who) => $"你好，{who}";

        /// <summary>
        /// 无参方法
        /// </summary>
        /// <returns>姓名</returns>
        public string Describe() => Name;

        /// <summary>
        /// 需要整型参数的方法
        /// </summary>
        /// <param name="count">重复次数</param>
        /// <returns>重复后的字符串</returns>
        public string Repeat(int count) => new('*', count);

        /// <summary>
        /// 必定抛异常的方法
        /// </summary>
        /// <returns>永远不会返回</returns>
        public string Boom() => throw new InvalidOperationException("内部炸了");
    }

    /// <summary>
    /// 变量解析测试用的用户模型
    /// </summary>
    public sealed class UserModel
    {
        /// <summary>
        /// 档案
        /// </summary>
        public ProfileModel Profile { get; set; } = new();
    }
}
