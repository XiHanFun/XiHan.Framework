// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;

namespace XiHan.Framework.Scheduling.Abstractions.Tests;

/// <summary>
/// 回放 <c>job-names.json</c>：调度里名字的文法与长度上限
/// </summary>
/// <remarks>
/// 规范 RFC-0013「作业与触发器」：<c>^[a-z0-9][a-z0-9._-]{0,127}$</c>，按整串匹配；作业名、触发器名与领导选举的资源名都用它（ADR-0277）
/// </remarks>
public sealed class JobNameVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly JobNameVectors _vectors = ConformanceVectors.Load<JobNameVectors>("job-names.json");

    [Fact]
    public void MaxLength_IsTheOneInTheVectors()
        => Assert.Equal(JobName.MaxLength, _vectors.MaxLength);

    [Fact]
    public void IsValid_AcceptsTheValidNames()
    {
        Assert.NotEmpty(_vectors.Valid);
        foreach (var name in _vectors.Valid)
        {
            Assert.True(JobName.IsValid(name), name);
            Assert.Equal(name, JobName.Validate(name));
            Assert.Equal(name, new JobName(name).Value);

            // 触发器名是同一条文法
            Assert.Equal(name, new TriggerKey(new JobName(name), name).Trigger);
        }
    }

    [Fact]
    public void IsValid_RejectsTheInvalidNames()
    {
        Assert.NotEmpty(_vectors.Invalid);
        var job = new JobName(_vectors.Valid[0]);
        foreach (var name in _vectors.Invalid)
        {
            Assert.False(JobName.IsValid(name), name);
            Assert.Throws<ArgumentException>(() => JobName.Validate(name));
            Assert.Throws<ArgumentException>(() => new JobName(name));
            Assert.Throws<ArgumentException>(() => new TriggerKey(job, name));
        }
    }

    [Fact]
    public void TheNamesTheFrameworkGenerates_AreAmongTheValidNames()
    {
        // 向量点了名的三个：便利构造生成的触发器名、手动入队的一次性触发器名的形状、引擎缺省的租约资源名
        Assert.Contains(JobTriggerDefinition.DefaultName, _vectors.Valid);
        Assert.Contains(_vectors.Valid, static name => name.StartsWith(JobSchedulerEngine.ManualTriggerPrefix, StringComparison.Ordinal) && name.Length > JobSchedulerEngine.ManualTriggerPrefix.Length);
        Assert.Contains(new SchedulerOptions().LeaseResource.Value, _vectors.Valid);
    }

    [Fact]
    public void TheVectors_CoverBothSidesOfTheLimit()
    {
        // 防止向量换了写法后上限一条都没核对还照样通过：正好到上限的要收，只因超长而不合法的要拒
        Assert.Contains(_vectors.Valid, name => name.Length == _vectors.MaxLength);
        Assert.Contains(_vectors.Invalid, name => name.Length == _vectors.MaxLength + 1 && name.All(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9')));
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxLength">长度上限</param>
    /// <param name="Valid">合法的名字</param>
    /// <param name="Invalid">不合法的名字</param>
    private sealed record JobNameVectors(int MaxLength, string[] Valid, string[] Invalid);
}
