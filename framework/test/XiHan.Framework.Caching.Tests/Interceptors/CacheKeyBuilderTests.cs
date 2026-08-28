// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Caching.Interceptors;
using XiHan.Framework.Caching.Tests.Fakes;

namespace XiHan.Framework.Caching.Tests.Interceptors;

/// <summary>
/// 缓存键构建器测试
/// </summary>
/// <remarks>
/// 占位符按形参名匹配，缺位与空值统一落成字面量 null；
/// 这一步出错会让两个本该不同的调用共用同一个缓存键，直接串数据，所以边界都要覆盖。
/// </remarks>
public class CacheKeyBuilderTests
{
    /// <summary>
    /// 占位符被替换为对应形参的实参值
    /// </summary>
    [Fact]
    public void Build_ReplacesPlaceholdersWithArgumentValues()
    {
        var method = GetMethod(nameof(Target.Query));

        var key = CacheKeyBuilder.Build("config:{tenantId}:{key}", method, [1024L, "theme"]);

        Assert.Equal("config:1024:theme", key);
    }

    /// <summary>
    /// 同一占位符出现多次时全部被替换
    /// </summary>
    [Fact]
    public void Build_ReplacesEveryOccurrenceOfSamePlaceholder()
    {
        var method = GetMethod(nameof(Target.Query));

        var key = CacheKeyBuilder.Build("{tenantId}-{tenantId}", method, [7L, "theme"]);

        Assert.Equal("7-7", key);
    }

    /// <summary>
    /// 实参为空值时替换为字面量 null
    /// </summary>
    [Fact]
    public void Build_WithNullArgument_UsesNullLiteral()
    {
        var method = GetMethod(nameof(Target.Query));

        var key = CacheKeyBuilder.Build("config:{tenantId}:{key}", method, [1L, null]);

        Assert.Equal("config:1:null", key);
    }

    /// <summary>
    /// 实参个数少于形参时缺位按字面量 null 处理
    /// </summary>
    [Fact]
    public void Build_WithFewerArgumentsThanParameters_UsesNullLiteralForMissing()
    {
        var method = GetMethod(nameof(Target.Query));

        var key = CacheKeyBuilder.Build("config:{tenantId}:{key}", method, [1L]);

        Assert.Equal("config:1:null", key);
    }

    /// <summary>
    /// 模板里没有占位符时原样返回
    /// </summary>
    [Fact]
    public void Build_WithoutPlaceholders_ReturnsTemplateUnchanged()
    {
        var method = GetMethod(nameof(Target.Query));

        Assert.Equal("config:all", CacheKeyBuilder.Build("config:all", method, [1L, "theme"]));
    }

    /// <summary>
    /// 模板里出现非形参名的占位符时保持原样
    /// </summary>
    [Fact]
    public void Build_WithUnknownPlaceholder_LeavesItInPlace()
    {
        var method = GetMethod(nameof(Target.Query));

        var key = CacheKeyBuilder.Build("config:{unknown}", method, [1L, "theme"]);

        Assert.Equal("config:{unknown}", key);
    }

    /// <summary>
    /// 无参方法的模板不做任何替换
    /// </summary>
    [Fact]
    public void Build_ForParameterlessMethod_ReturnsTemplateUnchanged()
    {
        var method = GetMethod(nameof(Target.NoArguments));

        Assert.Equal("config:{tenantId}", CacheKeyBuilder.Build("config:{tenantId}", method, []));
    }

    /// <summary>
    /// 方法为空时拒绝构建
    /// </summary>
    [Fact]
    public void Build_WithNullMethod_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => CacheKeyBuilder.Build("config:{tenantId}", null!, [1L]));
    }

    /// <summary>
    /// 实参集合为空时拒绝构建
    /// </summary>
    [Fact]
    public void Build_WithNullArguments_Throws()
    {
        var method = GetMethod(nameof(Target.Query));

        Assert.Throws<ArgumentNullException>(() => CacheKeyBuilder.Build("config:{tenantId}", method, null!));
    }

    /// <summary>
    /// 通过方法调用上下文构建的键与直接传方法与实参一致
    /// </summary>
    [Fact]
    public void Build_FromInvocation_MatchesDirectOverload()
    {
        var method = GetMethod(nameof(Target.Query));
        var invocation = new FakeMethodInvocation(method, [1024L, "theme"]);

        Assert.Equal(
            CacheKeyBuilder.Build("config:{tenantId}:{key}", method, [1024L, "theme"]),
            CacheKeyBuilder.Build("config:{tenantId}:{key}", invocation));
    }

    /// <summary>
    /// 合法占位符被识别
    /// </summary>
    [Theory]
    [InlineData("{tenantId}")]
    [InlineData("config:{key}")]
    [InlineData("{_private}")]
    [InlineData("{a1}")]
    public void HasPlaceholders_ForValidPlaceholder_ReturnsTrue(string template)
    {
        Assert.True(CacheKeyBuilder.HasPlaceholders(template));
    }

    /// <summary>
    /// 非法或缺失的占位符不被识别
    /// </summary>
    /// <remarks>
    /// 占位符必须以字母或下划线开头，数字开头的花括号内容不是形参名，不能被当成待替换项。
    /// </remarks>
    [Theory]
    [InlineData("config:all")]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{1abc}")]
    [InlineData("{a-b}")]
    public void HasPlaceholders_ForInvalidPlaceholder_ReturnsFalse(string template)
    {
        Assert.False(CacheKeyBuilder.HasPlaceholders(template));
    }

    /// <summary>
    /// 取目标类型上的方法
    /// </summary>
    /// <param name="name">方法名</param>
    /// <returns>方法信息</returns>
    private static MethodInfo GetMethod(string name)
    {
        return typeof(Target).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)!;
    }

    /// <summary>
    /// 提供形参名的目标类型
    /// </summary>
    private sealed class Target
    {
        /// <summary>
        /// 带两个形参的方法
        /// </summary>
        /// <param name="tenantId">租户标识</param>
        /// <param name="key">配置键</param>
        /// <returns>占位值</returns>
        public string Query(long tenantId, string key)
        {
            return $"{tenantId}:{key}";
        }

        /// <summary>
        /// 无形参的方法
        /// </summary>
        /// <returns>占位值</returns>
        public string NoArguments()
        {
            return string.Empty;
        }
    }
}
