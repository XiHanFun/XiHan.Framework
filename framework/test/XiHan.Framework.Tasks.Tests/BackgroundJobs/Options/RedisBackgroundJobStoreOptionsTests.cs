// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Options;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Options;

/// <summary>
/// Redis 后台作业存储选项测试
/// </summary>
/// <remarks>
/// 键前缀直接决定 Redis 里的键名，改动等同于换库——升级后旧键会变成孤儿数据。
/// 候选加载倍数则决定"先按到期时间取候选、再在内存按优先级二次排序"能否取到足够样本，
/// 小于 1 会退化成只按时间排序，优先级形同虚设，所以下限也要有断言兜底。
/// </remarks>
public class RedisBackgroundJobStoreOptionsTests
{
    /// <summary>
    /// 默认键前缀锁死
    /// </summary>
    [Fact]
    public void Defaults_KeyPrefixIsStable()
    {
        Assert.Equal("Default:BackgroundJobs", new RedisBackgroundJobStoreOptions().KeyPrefix);
    }

    /// <summary>
    /// 已放弃作业默认保留 7 天，便于事后排查
    /// </summary>
    [Fact]
    public void Defaults_AbandonedRetentionIsOneWeek()
    {
        Assert.Equal(7, new RedisBackgroundJobStoreOptions().AbandonedRetentionDays);
    }

    /// <summary>
    /// 候选加载倍数默认为 4，且必须大于 1 才能让优先级二次排序有意义
    /// </summary>
    [Fact]
    public void Defaults_FetchMultiplierIsGreaterThanOne()
    {
        var options = new RedisBackgroundJobStoreOptions();

        Assert.Equal(4, options.FetchMultiplier);
        Assert.True(options.FetchMultiplier > 1);
    }

    /// <summary>
    /// 所有选项可写，允许应用侧自定义
    /// </summary>
    [Fact]
    public void Properties_AreWritable()
    {
        var options = new RedisBackgroundJobStoreOptions
        {
            KeyPrefix = "App:Jobs",
            AbandonedRetentionDays = 30,
            FetchMultiplier = 2
        };

        Assert.Equal("App:Jobs", options.KeyPrefix);
        Assert.Equal(30, options.AbandonedRetentionDays);
        Assert.Equal(2, options.FetchMultiplier);
    }
}
