// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.BackgroundJobs.Options;
using XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Options;

/// <summary>
/// 后台作业注册表选项测试
/// </summary>
/// <remarks>
/// 注册表同时按"参数类型"和"作业名"两个维度建索引：入队走参数类型、执行走作业名。
/// 两份索引必须始终指向同一份配置，否则会出现"入队成功但执行时找不到配置直接放弃"的静默丢作业。
/// </remarks>
public class BackgroundJobOptionsTests
{
    /// <summary>
    /// 按处理器类型泛型登记后，两个维度都能查到
    /// </summary>
    [Fact]
    public void AddJob_Generic_IndexesByBothNameAndArgsType()
    {
        var options = new BackgroundJobOptions();

        options.AddJob<NamedArgsJob>();

        var byName = options.GetJobOrNull("xihan-tests-named-args");
        var byArgs = options.GetJobByArgsOrNull(typeof(NamedJobArgs));

        Assert.NotNull(byName);
        Assert.NotNull(byArgs);
        Assert.Same(byName, byArgs);
        Assert.Equal(typeof(NamedArgsJob), byName.JobType);
    }

    /// <summary>
    /// 按处理器类型登记与泛型登记等价
    /// </summary>
    [Fact]
    public void AddJob_ByType_IsEquivalentToGeneric()
    {
        var options = new BackgroundJobOptions();

        options.AddJob(typeof(UnnamedArgsJob));

        var configuration = options.GetJobByArgsOrNull(typeof(UnnamedJobArgs));

        Assert.NotNull(configuration);
        Assert.Equal(typeof(UnnamedJobArgs).FullName, configuration.JobName);
    }

    /// <summary>
    /// 直接登记配置对象
    /// </summary>
    [Fact]
    public void AddJob_ByConfiguration_IsRegistered()
    {
        var options = new BackgroundJobOptions();
        var configuration = new BackgroundJobConfiguration(typeof(NamedArgsJob));

        options.AddJob(configuration);

        Assert.Same(configuration, options.GetJobOrNull(configuration.JobName));
    }

    /// <summary>
    /// 配置为 null 时抛出空引用参数异常
    /// </summary>
    [Fact]
    public void AddJob_WhenConfigurationNull_ThrowsArgumentNullException()
    {
        var options = new BackgroundJobOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddJob((BackgroundJobConfiguration)null!));
    }

    /// <summary>
    /// 同一参数类型再次登记时后者覆盖前者，不会留下两份互相冲突的映射
    /// </summary>
    [Fact]
    public void AddJob_WhenSameArgsTypeRegisteredTwice_LastOneWins()
    {
        var options = new BackgroundJobOptions();

        options.AddJob<NamedArgsJob>();
        options.AddJob<AlternateNamedArgsJob>();

        Assert.Single(options.GetJobs());
        Assert.Equal(typeof(AlternateNamedArgsJob), options.GetJobByArgsOrNull(typeof(NamedJobArgs))!.JobType);
        Assert.Equal(typeof(AlternateNamedArgsJob), options.GetJobOrNull("xihan-tests-named-args")!.JobType);
    }

    /// <summary>
    /// 未登记时两个查询都返回 null 而不是抛异常
    /// </summary>
    [Fact]
    public void Get_WhenNotRegistered_ReturnsNull()
    {
        var options = new BackgroundJobOptions();

        Assert.Null(options.GetJobOrNull("missing-job"));
        Assert.Null(options.GetJobByArgsOrNull(typeof(NamedJobArgs)));
    }

    /// <summary>
    /// 新建注册表为空
    /// </summary>
    [Fact]
    public void GetJobs_WhenNothingRegistered_IsEmpty()
    {
        Assert.Empty(new BackgroundJobOptions().GetJobs());
    }

    /// <summary>
    /// 登记多个不同参数类型的作业时全部保留
    /// </summary>
    [Fact]
    public void GetJobs_ReturnsAllRegisteredConfigurations()
    {
        var options = new BackgroundJobOptions();

        options.AddJob<NamedArgsJob>();
        options.AddJob<UnnamedArgsJob>();

        var jobTypes = options.GetJobs().Select(x => x.JobType).ToList();

        Assert.Equal(2, jobTypes.Count);
        Assert.Contains(typeof(NamedArgsJob), jobTypes);
        Assert.Contains(typeof(UnnamedArgsJob), jobTypes);
    }

    /// <summary>
    /// 返回的列表是快照，调用方拿到后再登记不会影响已取回的结果
    /// </summary>
    [Fact]
    public void GetJobs_ReturnsSnapshot()
    {
        var options = new BackgroundJobOptions();
        options.AddJob<NamedArgsJob>();

        var snapshot = options.GetJobs();
        options.AddJob<UnnamedArgsJob>();

        Assert.Single(snapshot);
        Assert.Equal(2, options.GetJobs().Count);
    }
}
