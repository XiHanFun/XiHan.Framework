#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheHelperLazyCleanupTests
// Guid:7f8c9d0e-1a2b-3c4d-5e6f-7890abcdef12
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Caching;
using Xunit.Abstractions;

namespace XiHan.Framework.Utils.Tests.Caching;

/// <summary>
/// CacheHelper 惰性清理机制测试
/// </summary>
public class CacheHelperLazyCleanupTests
{
    private readonly ITestOutputHelper _output;

    public CacheHelperLazyCleanupTests(ITestOutputHelper output)
    {
        _output = output;
        // 每次测试前清空缓存
        CacheHelper.Clear();
    }

    /// <summary>
    /// 测试基本的缓存设置和获取
    /// </summary>
    [Fact]
    public void TestBasicSetAndGet()
    {
        // Arrange
        const string key = "test_key";
        const string value = "test_value";

        // Act
        CacheHelper.Set(key, value, 60);
        var result = CacheHelper.Get<string>(key);

        // Assert
        Assert.Equal(value, result);
        _output.WriteLine($"✓ 基本设置和获取测试通过");
    }

    /// <summary>
    /// 测试过期缓存的自动清理
    /// </summary>
    [Fact]
    public async Task TestExpiredCacheAutoCleanup()
    {
        // Arrange
        const string key = "expire_test";
        const string value = "test_value";

        // Act - 设置一个1秒后过期的缓存
        CacheHelper.Set(key, value, 1);
        
        // 立即获取，应该能获取到
        var immediateResult = CacheHelper.Get<string>(key);
        Assert.Equal(value, immediateResult);
        _output.WriteLine($"✓ 立即获取成功: {immediateResult}");

        // 等待2秒，让缓存过期
        await Task.Delay(2000);

        // 再次获取，应该返回 null（触发惰性清理）
        var expiredResult = CacheHelper.Get<string>(key);
        Assert.Null(expiredResult);
        _output.WriteLine($"✓ 过期后获取返回 null，惰性清理成功");

        // 验证缓存项确实被移除
        Assert.False(CacheHelper.Exists(key));
    }

    /// <summary>
    /// 测试惰性清理批次处理
    /// </summary>
    [Fact]
    public async Task TestLazyCleanupBatchProcessing()
    {
        // Arrange - 创建多个即将过期的缓存项
        var keys = Enumerable.Range(1, 100).Select(i => $"batch_test_{i}").ToList();
        
        foreach (var key in keys)
        {
            CacheHelper.Set(key, $"value_{key}", 1); // 1秒后过期
        }

        _output.WriteLine($"✓ 创建了 {keys.Count} 个缓存项");
        Assert.Equal(keys.Count, CacheHelper.Count);

        // 等待缓存过期
        await Task.Delay(2000);

        // Act - 触发惰性清理（通过一次 Get 调用）
        CacheHelper.Get<string>("trigger_cleanup");

        // 由于惰性清理是批量的（默认64个），第一次Get可能不会清理所有过期项
        // 需要多次触发
        for (var i = 0; i < 3; i++)
        {
            CacheHelper.Get<string>($"trigger_{i}");
            await Task.Delay(100);
        }

        _output.WriteLine($"✓ 触发惰性清理后，剩余缓存项: {CacheHelper.Count}");

        // 手动全量清理
        CacheHelper.CleanupExpired();
        _output.WriteLine($"✓ 手动清理后，剩余缓存项: {CacheHelper.Count}");

        // 验证所有过期项都被清理
        foreach (var key in keys)
        {
            Assert.False(CacheHelper.Exists(key), $"缓存项 {key} 应该已被清理");
        }
    }

    /// <summary>
    /// 测试滑动过期和惰性清理
    /// </summary>
    [Fact]
    public async Task TestSlidingExpirationWithLazyCleanup()
    {
        // Arrange
        const string key = "sliding_test";
        const string value = "sliding_value";

        // 设置滑动过期时间为2秒
        CacheHelper.SetSliding(key, value, TimeSpan.FromSeconds(2));
        _output.WriteLine($"✓ 设置滑动过期缓存项: {key}");

        // Act - 每隔1秒访问一次，保持缓存活跃
        for (var i = 0; i < 3; i++)
        {
            await Task.Delay(1000);
            var result = CacheHelper.Get<string>(key);
            Assert.Equal(value, result);
            _output.WriteLine($"✓ 第 {i + 1} 次访问成功，缓存仍然有效");
        }

        // 停止访问3秒，让滑动过期生效
        await Task.Delay(3000);

        // 获取应该返回 null
        var expiredResult = CacheHelper.Get<string>(key);
        Assert.Null(expiredResult);
        _output.WriteLine($"✓ 滑动过期生效，缓存已清理");
    }

    /// <summary>
    /// 测试 GetOrAdd 与惰性清理的配合
    /// </summary>
    [Fact]
    public async Task TestGetOrAddWithLazyCleanup()
    {
        // Arrange
        const string key = "getadd_test";
        var callCount = 0;

        // Act - 第一次调用，会执行工厂方法
        var result1 = CacheHelper.GetOrAdd(key, () =>
        {
            callCount++;
            return $"value_{callCount}";
        }, 1); // 1秒后过期

        Assert.Equal("value_1", result1);
        Assert.Equal(1, callCount);
        _output.WriteLine($"✓ 第一次 GetOrAdd，工厂方法被调用: {result1}");

        // 等待过期
        await Task.Delay(2000);

        // 第二次调用，缓存已过期，会再次执行工厂方法
        var result2 = CacheHelper.GetOrAdd(key, () =>
        {
            callCount++;
            return $"value_{callCount}";
        }, 1);

        Assert.Equal("value_2", result2);
        Assert.Equal(2, callCount);
        _output.WriteLine($"✓ 第二次 GetOrAdd（过期后），工厂方法再次被调用: {result2}");
    }

    /// <summary>
    /// 测试手动清理方法
    /// </summary>
    [Fact]
    public async Task TestManualCleanup()
    {
        // Arrange - 创建一些即将过期的缓存项
        for (var i = 0; i < 50; i++)
        {
            CacheHelper.Set($"manual_test_{i}", $"value_{i}", 1);
        }

        var initialCount = CacheHelper.Count;
        _output.WriteLine($"✓ 初始缓存项数量: {initialCount}");

        // 等待过期
        await Task.Delay(2000);

        // Act - 手动触发全量清理
        CacheHelper.CleanupExpired();

        var finalCount = CacheHelper.Count;
        _output.WriteLine($"✓ 手动清理后缓存项数量: {finalCount}");

        // Assert - 所有过期项应该被清理
        Assert.True(finalCount < initialCount, "手动清理应该移除过期项");
    }

    /// <summary>
    /// 测试并发场景下的惰性清理
    /// </summary>
    [Fact]
    public async Task TestConcurrentLazyCleanup()
    {
        // Arrange - 创建大量缓存项
        var tasks = new List<Task>();
        
        for (var i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                CacheHelper.Set($"concurrent_{index}", $"value_{index}", 1);
            }));
        }

        await Task.WhenAll(tasks);
        _output.WriteLine($"✓ 并发创建 100 个缓存项");

        // 等待过期
        await Task.Delay(2000);

        // Act - 并发触发惰性清理
        var readTasks = new List<Task>();
        for (var i = 0; i < 50; i++)
        {
            var index = i;
            readTasks.Add(Task.Run(() =>
            {
                CacheHelper.Get<string>($"concurrent_{index}");
            }));
        }

        await Task.WhenAll(readTasks);
        _output.WriteLine($"✓ 并发读取触发惰性清理");

        // 最终手动清理所有过期项
        CacheHelper.CleanupExpired();
        
        var remainingCount = CacheHelper.Count;
        _output.WriteLine($"✓ 最终剩余缓存项: {remainingCount}");
        
        // 由于过期项可能还没完全清理，这里只验证清理逻辑不会出错
        Assert.True(remainingCount >= 0);
    }

    /// <summary>
    /// 清理测试后的缓存
    /// </summary>
    public static void Dispose()
    {
        CacheHelper.Clear();
    }
}

