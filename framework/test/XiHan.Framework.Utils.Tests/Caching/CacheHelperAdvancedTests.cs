#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheHelperAdvancedTests
// Guid:8c7d6e5f-4d3c-2b1a-0f9e-8d7c6b5a4b3c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Caching;
using Xunit.Abstractions;

namespace XiHan.Framework.Utils.Tests.Caching;

/// <summary>
/// CacheHelper 高级功能测试
/// </summary>
public class CacheHelperAdvancedTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public CacheHelperAdvancedTests(ITestOutputHelper output)
    {
        _output = output;
        CacheHelper.Clear();
        CacheHelper.ResetStatistics();
    }

    /// <summary>
    /// 测试缓存统计功能
    /// </summary>
    [Fact]
    public void TestCacheStatistics()
    {
        // Arrange - 启用统计
        CacheHelper.Configure(opt =>
        {
            opt.EnableStatistics = true;
        });

        // Act - 执行一些缓存操作
        CacheHelper.Set("key1", "value1", 60);
        CacheHelper.Set("key2", "value2", 60);

        var hit1 = CacheHelper.Get<string>("key1"); // 命中
        var hit2 = CacheHelper.Get<string>("key2"); // 命中
        var miss = CacheHelper.Get<string>("key3"); // 未命中

        // Assert
        var stats = CacheHelper.GetStatistics();
        _output.WriteLine($"统计信息: {stats.GetSummary()}");

        Assert.Equal(2, stats.HitCount);
        Assert.Equal(1, stats.MissCount);
        Assert.Equal(3, stats.TotalRequests);
        Assert.True(stats.HitRate > 0);

        _output.WriteLine($"✓ 缓存统计测试通过 - 命中率: {stats.HitRate:F2}%");
    }

    /// <summary>
    /// 测试 LRU 淘汰策略
    /// </summary>
    [Fact]
    public void TestLruEvictionPolicy()
    {
        // Arrange - 配置最大容量和 LRU 策略
        CacheHelper.Clear();
        CacheHelper.Configure(opt =>
        {
            opt.MaxCacheSize = 10;
            opt.EvictionPolicy = CacheEvictionPolicy.Lru;
            opt.EnableStatistics = true;
        });

        // Act - 添加超过容量的缓存项
        for (var i = 0; i < 15; i++)
        {
            CacheHelper.Set($"key{i}", $"value{i}", 3600);
        }

        var count = CacheHelper.Count;
        _output.WriteLine($"缓存项数量: {count}");

        // Assert - 应该触发淘汰
        Assert.True(count <= 10, $"缓存项数量应该不超过10，实际: {count}");

        var stats = CacheHelper.GetStatistics();
        _output.WriteLine($"淘汰次数: {stats.EvictionCount}");
        Assert.True(stats.EvictionCount > 0, "应该有淘汰记录");

        _output.WriteLine($"✓ LRU 淘汰策略测试通过");
    }

    /// <summary>
    /// 测试缓存事件通知
    /// </summary>
    [Fact]
    public void TestCacheEvents()
    {
        // Arrange
        CacheHelper.Clear();
        CacheHelper.Configure(opt =>
        {
            opt.EnableEvents = true;
        });

        var events = new List<CacheEventArgs>();
        CacheHelper.CacheEvent += (sender, args) =>
        {
            events.Add(args);
            _output.WriteLine($"事件: {args.EventType} - 键: {args.Key}");
        };

        // Act
        CacheHelper.Set("test_key", "test_value", 60); // Added
        CacheHelper.Set("test_key", "test_value2", 60); // Updated
        CacheHelper.Remove("test_key"); // Removed

        // Assert
        Assert.True(events.Count >= 2, $"应该至少有2个事件，实际: {events.Count}");
        Assert.Contains(events, e => e.EventType == CacheEventType.Added);
        Assert.Contains(events, e => e.EventType == CacheEventType.Removed);

        _output.WriteLine($"✓ 缓存事件通知测试通过，共 {events.Count} 个事件");
    }

    /// <summary>
    /// 测试模式匹配查询
    /// </summary>
    [Fact]
    public void TestPatternMatching()
    {
        // Arrange
        CacheHelper.Clear();
        CacheHelper.Set("user:1", "user1", 60);
        CacheHelper.Set("user:2", "user2", 60);
        CacheHelper.Set("order:1", "order1", 60);
        CacheHelper.Set("order:2", "order2", 60);

        // Act
        var userKeys = CacheHelper.GetKeysByPrefix("user:");
        var orderKeys = CacheHelper.GetKeysByPrefix("order:");
        var allUserKeys = CacheHelper.GetKeysByPattern("user:*");

        // Assert
        Assert.Equal(2, userKeys.Count());
        Assert.Equal(2, orderKeys.Count());
        Assert.Equal(2, allUserKeys.Count());

        _output.WriteLine($"✓ 模式匹配查询测试通过");
        _output.WriteLine($"  - User 键: {string.Join(", ", userKeys)}");
        _output.WriteLine($"  - Order 键: {string.Join(", ", orderKeys)}");
    }

    /// <summary>
    /// 测试前缀删除
    /// </summary>
    [Fact]
    public void TestRemoveByPrefix()
    {
        // Arrange
        CacheHelper.Clear();
        for (var i = 0; i < 5; i++)
        {
            CacheHelper.Set($"session:{i}", $"session{i}", 60);
        }
        for (var i = 0; i < 3; i++)
        {
            CacheHelper.Set($"temp:{i}", $"temp{i}", 60);
        }

        var initialCount = CacheHelper.Count;
        _output.WriteLine($"初始缓存项数量: {initialCount}");

        // Act
        var removed = CacheHelper.RemoveByPrefix("session:");
        var finalCount = CacheHelper.Count;

        // Assert
        Assert.Equal(5, removed);
        Assert.Equal(3, finalCount);
        Assert.False(CacheHelper.Exists("session:0"));
        Assert.True(CacheHelper.Exists("temp:0"));

        _output.WriteLine($"✓ 前缀删除测试通过 - 删除了 {removed} 个缓存项");
    }

    /// <summary>
    /// 测试 FIFO 淘汰策略
    /// </summary>
    [Fact]
    public async Task TestFifoEvictionPolicy()
    {
        // Arrange
        CacheHelper.Clear();
        CacheHelper.Configure(opt =>
        {
            opt.MaxCacheSize = 5;
            opt.EvictionPolicy = CacheEvictionPolicy.Fifo;
        });

        // Act - 按顺序添加缓存项
        for (var i = 0; i < 8; i++)
        {
            CacheHelper.Set($"key{i}", $"value{i}", 3600);
            await Task.Delay(10); // 确保创建时间有差异
        }

        // Assert - 先添加的应该被淘汰
        Assert.True(CacheHelper.Count <= 5);
        Assert.False(CacheHelper.Exists("key0")); // 第一个应该被淘汰
        Assert.True(CacheHelper.Exists("key7")); // 最后一个应该存在

        _output.WriteLine($"✓ FIFO 淘汰策略测试通过");
    }

    /// <summary>
    /// 测试 LFU 淘汰策略
    /// </summary>
    [Fact]
    public void TestLfuEvictionPolicy()
    {
        // Arrange
        CacheHelper.Clear();
        CacheHelper.Configure(opt =>
        {
            opt.MaxCacheSize = 5;
            opt.EvictionPolicy = CacheEvictionPolicy.Lfu;
        });

        // Act - 添加缓存项并访问不同次数
        CacheHelper.Set("frequent", "value", 3600);
        for (var i = 0; i < 10; i++)
        {
            CacheHelper.Get<string>("frequent"); // 频繁访问
        }

        for (var i = 0; i < 7; i++)
        {
            CacheHelper.Set($"key{i}", $"value{i}", 3600);
        }

        // Assert - 访问频率高的应该被保留
        Assert.True(CacheHelper.Count <= 5);
        Assert.True(CacheHelper.Exists("frequent"), "频繁访问的缓存项应该被保留");

        _output.WriteLine($"✓ LFU 淘汰策略测试通过");
    }

    /// <summary>
    /// 测试统计信息重置
    /// </summary>
    [Fact]
    public void TestStatisticsReset()
    {
        // Arrange
        CacheHelper.Configure(opt => opt.EnableStatistics = true);

        CacheHelper.Set("key1", "value1", 60);
        CacheHelper.Get<string>("key1");
        CacheHelper.Get<string>("key2");

        var statsBefore = CacheHelper.GetStatistics();
        _output.WriteLine($"重置前: {statsBefore.GetSummary()}");

        // Act
        CacheHelper.ResetStatistics();

        // Assert
        var statsAfter = CacheHelper.GetStatistics();
        _output.WriteLine($"重置后: {statsAfter.GetSummary()}");

        Assert.Equal(0, statsAfter.HitCount);
        Assert.Equal(0, statsAfter.MissCount);
        Assert.Equal(0, statsAfter.EvictionCount);

        _output.WriteLine($"✓ 统计信息重置测试通过");
    }

    /// <summary>
    /// 测试配置更改
    /// </summary>
    [Fact]
    public void TestConfigurationUpdate()
    {
        // Arrange & Act
        CacheHelper.Configure(opt =>
        {
            opt.MaxCacheSize = 100;
            opt.EnableStatistics = true;
            opt.EnableEvents = true;
            opt.CleanupBatchSize = 32;
            opt.EvictionPolicy = CacheEvictionPolicy.Lru;
        });

        // Assert - 通过实际使用来验证配置生效
        for (var i = 0; i < 150; i++)
        {
            CacheHelper.Set($"key{i}", $"value{i}", 60);
        }

        Assert.True(CacheHelper.Count <= 100, "应该遵循最大容量限制");

        var stats = CacheHelper.GetStatistics();
        Assert.True(stats.EvictionCount > 0, "应该有淘汰记录（说明统计功能开启）");

        _output.WriteLine($"✓ 配置更改测试通过 - 缓存项: {CacheHelper.Count}, 淘汰: {stats.EvictionCount}");
    }

    public void Dispose()
    {
        CacheHelper.Clear();
        CacheHelper.ResetStatistics();
        // 重置配置
        CacheHelper.Configure(opt =>
        {
            opt.MaxCacheSize = 10000;
            opt.EnableStatistics = false;
            opt.EnableEvents = false;
            opt.CleanupBatchSize = 64;
            opt.EvictionPolicy = CacheEvictionPolicy.Lru;
        });
    }
}
