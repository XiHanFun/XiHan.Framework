// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Models;

/// <summary>
/// 后台作业配置测试
/// </summary>
/// <remarks>
/// 配置是"作业名 ↔ 处理器类型 ↔ 参数类型"三元组的唯一来源，
/// 作业名必须由参数类型（而非处理器类型）推导，否则换个处理器实现就会让已入库作业失联。
/// </remarks>
public class BackgroundJobConfigurationTests
{
    /// <summary>
    /// 由处理器类型推导出参数类型与作业名
    /// </summary>
    [Fact]
    public void Constructor_DerivesArgsTypeAndJobNameFromJobType()
    {
        var configuration = new BackgroundJobConfiguration(typeof(NamedArgsJob));

        Assert.Equal(typeof(NamedArgsJob), configuration.JobType);
        Assert.Equal(typeof(NamedJobArgs), configuration.ArgsType);
        Assert.Equal("xihan-tests-named-args", configuration.JobName);
    }

    /// <summary>
    /// 参数类型未标注名称时作业名回退为参数类型完整名
    /// </summary>
    [Fact]
    public void Constructor_WhenArgsTypeNotAnnotated_UsesArgsTypeFullName()
    {
        var configuration = new BackgroundJobConfiguration(typeof(UnnamedArgsJob));

        Assert.Equal(typeof(UnnamedJobArgs).FullName, configuration.JobName);
    }

    /// <summary>
    /// 同一参数类型的不同处理器算出同一个作业名
    /// </summary>
    [Fact]
    public void Constructor_WhenDifferentJobTypesShareArgs_ProducesSameJobName()
    {
        var first = new BackgroundJobConfiguration(typeof(NamedArgsJob));
        var second = new BackgroundJobConfiguration(typeof(AlternateNamedArgsJob));

        Assert.Equal(first.JobName, second.JobName);
        Assert.NotEqual(first.JobType, second.JobType);
    }

    /// <summary>
    /// 处理器类型为 null 时抛出空引用参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenJobTypeNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BackgroundJobConfiguration(null!));
    }

    /// <summary>
    /// 处理器类型未实现作业接口时抛出参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenJobTypeIsNotJob_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new BackgroundJobConfiguration(typeof(NotABackgroundJob)));
    }
}
