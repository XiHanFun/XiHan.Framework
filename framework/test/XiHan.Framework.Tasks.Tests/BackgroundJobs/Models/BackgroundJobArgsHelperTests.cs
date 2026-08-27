// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Models;

/// <summary>
/// 后台作业参数类型解析工具测试
/// </summary>
/// <remarks>
/// 自动发现依赖这两个方法把"哪些类型是作业、它的参数类型是什么"判准，
/// 判错的后果是启动时把非作业类型塞进注册表或漏掉真正的作业，因此排除分支要逐条覆盖。
/// </remarks>
public class BackgroundJobArgsHelperTests
{
    /// <summary>
    /// 从直接实现接口的处理器解析出参数类型
    /// </summary>
    [Fact]
    public void GetJobArgsType_WhenInterfaceImplementedDirectly_ReturnsArgsType()
    {
        Assert.Equal(typeof(NamedJobArgs), BackgroundJobArgsHelper.GetJobArgsType(typeof(AlternateNamedArgsJob)));
    }

    /// <summary>
    /// 从继承基类的处理器同样能解析出参数类型
    /// </summary>
    [Fact]
    public void GetJobArgsType_WhenDerivedFromAsyncBackgroundJob_ReturnsArgsType()
    {
        Assert.Equal(typeof(UnnamedJobArgs), BackgroundJobArgsHelper.GetJobArgsType(typeof(UnnamedArgsJob)));
    }

    /// <summary>
    /// 类型未实现作业接口时抛出参数异常
    /// </summary>
    [Fact]
    public void GetJobArgsType_WhenTypeIsNotJob_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => BackgroundJobArgsHelper.GetJobArgsType(typeof(NotABackgroundJob)));

        Assert.Equal("jobType", exception.ParamName);
        Assert.Contains("IAsyncBackgroundJob", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 类型为 null 时抛出空引用参数异常
    /// </summary>
    [Fact]
    public void GetJobArgsType_WhenTypeNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BackgroundJobArgsHelper.GetJobArgsType(null!));
    }

    /// <summary>
    /// 可实例化的作业处理器被认定为后台作业
    /// </summary>
    [Fact]
    public void IsBackgroundJob_WhenConcreteJob_ReturnsTrue()
    {
        Assert.True(BackgroundJobArgsHelper.IsBackgroundJob(typeof(NamedArgsJob)));
        Assert.True(BackgroundJobArgsHelper.IsBackgroundJob(typeof(AlternateNamedArgsJob)));
    }

    /// <summary>
    /// 闭合后的泛型处理器同样可用
    /// </summary>
    [Fact]
    public void IsBackgroundJob_WhenClosedGenericJob_ReturnsTrue()
    {
        Assert.True(BackgroundJobArgsHelper.IsBackgroundJob(typeof(OpenGenericJob<NamedJobArgs>)));
    }

    /// <summary>
    /// 抽象类、接口、开放泛型定义、非作业类型与 null 一律排除
    /// </summary>
    [Fact]
    public void IsBackgroundJob_WhenTypeIsNotInstantiableJob_ReturnsFalse()
    {
        Assert.False(BackgroundJobArgsHelper.IsBackgroundJob(typeof(AbstractSampleJob)));
        Assert.False(BackgroundJobArgsHelper.IsBackgroundJob(typeof(IAsyncBackgroundJob<NamedJobArgs>)));
        Assert.False(BackgroundJobArgsHelper.IsBackgroundJob(typeof(OpenGenericJob<>)));
        Assert.False(BackgroundJobArgsHelper.IsBackgroundJob(typeof(NotABackgroundJob)));
        Assert.False(BackgroundJobArgsHelper.IsBackgroundJob(null!));
    }

    /// <summary>
    /// 仅实现标记接口而未实现泛型作业接口的类型不算作业
    /// </summary>
    [Fact]
    public void IsBackgroundJob_WhenOnlyMarkerInterfaceImplemented_ReturnsFalse()
    {
        Assert.False(BackgroundJobArgsHelper.IsBackgroundJob(typeof(IBackgroundJob)));
    }
}
