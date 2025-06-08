using XiHan.Framework.Utils.Caching;

namespace XiHan.Framework.Utils.Test.Caching;

public class CacheManagerTest
{
    [Fact]
    public void Instance_Should_Return_Singleton_Instance()
    {
        // Act
        var instance1 = CacheManager.Instance;
        var instance2 = CacheManager.Instance;

        // Assert
        Assert.NotNull(instance1);
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void DefaultCache_Should_Not_Be_Null()
    {
        // Act
        var defaultCache = CacheManager.Instance.DefaultCache;

        // Assert
        Assert.NotNull(defaultCache);
    }

    [Fact]
    public void GetOrAdd_Should_Return_Same_Instance_For_Same_Name()
    {
        // Arrange
        var cacheName = "TestCache";

        // Act
        var cache1 = CacheManager.Instance.GetOrAdd(cacheName);
        var cache2 = CacheManager.Instance.GetOrAdd(cacheName);

        // Assert
        Assert.NotNull(cache1);
        Assert.NotNull(cache2);
        Assert.Same(cache1, cache2);
    }

    [Fact]
    public void GetOrAdd_Should_Return_Different_Instances_For_Different_Names()
    {
        // Arrange
        var cacheName1 = "TestCache1";
        var cacheName2 = "TestCache2";

        // Act
        var cache1 = CacheManager.Instance.GetOrAdd(cacheName1);
        var cache2 = CacheManager.Instance.GetOrAdd(cacheName2);

        // Assert
        Assert.NotNull(cache1);
        Assert.NotNull(cache2);
        Assert.NotSame(cache1, cache2);
    }

    [Fact]
    public void GetOrAdd_With_Empty_Name_Should_Return_DefaultCache()
    {
        // Act
        var cache = CacheManager.Instance.GetOrAdd("");

        // Assert
        Assert.Same(CacheManager.Instance.DefaultCache, cache);
    }

    [Fact]
    public void GetOrAdd_With_Null_Name_Should_Return_DefaultCache()
    {
        // Act
        var cache = CacheManager.Instance.GetOrAdd(null!);

        // Assert
        Assert.Same(CacheManager.Instance.DefaultCache, cache);
    }

    [Fact]
    public void RemoveCache_Should_Remove_Cache()
    {
        // Arrange
        var cacheName = "RemoveTestCache";
        _ = CacheManager.Instance.GetOrAdd(cacheName);

        // Act
        var removeResult = CacheManager.Instance.RemoveCache(cacheName);
        var cacheExists = CacheManager.Instance.CacheExists(cacheName);

        // Assert
        Assert.True(removeResult);
        Assert.False(cacheExists);
    }

    [Fact]
    public void RemoveCache_On_DefaultCache_Should_Return_False()
    {
        // Act
        var removeResult = CacheManager.Instance.RemoveCache(CacheManager.DefaultCacheName);

        // Assert
        Assert.False(removeResult);
        Assert.True(CacheManager.Instance.CacheExists(CacheManager.DefaultCacheName));
    }

    [Fact]
    public void ClearAllCaches_Should_Clear_All_Caches()
    {
        // Arrange
        var defaultCache = CacheManager.Instance.DefaultCache;
        var testCache = CacheManager.Instance.GetOrAdd("ClearTestCache");

        defaultCache.Set("key1", "value1");
        testCache.Set("key2", "value2");

        // Act
        CacheManager.Instance.ClearAllCaches();

        // Assert
        Assert.Equal(0, defaultCache.Count());
        Assert.Equal(0, testCache.Count());
    }

    [Fact]
    public void GetCacheNames_Should_Return_All_Cache_Names()
    {
        // Arrange
        CacheManager.Instance.ClearAllCaches();
        _ = CacheManager.Instance.GetOrAdd("NameTestCache1");
        _ = CacheManager.Instance.GetOrAdd("NameTestCache2");

        // Act
        var cacheNames = CacheManager.Instance.GetCacheNames().ToList();

        // Assert
        Assert.Equal(3, cacheNames.Count); // Default + 2 custom caches
        Assert.Contains(CacheManager.DefaultCacheName, cacheNames);
        Assert.Contains("NameTestCache1", cacheNames);
        Assert.Contains("NameTestCache2", cacheNames);
    }

    [Fact]
    public void GetOrAddCount_Should_Return_Correct_Count()
    {
        // Arrange
        CacheManager.Instance.ClearAllCaches();
        _ = CacheManager.Instance.GetOrAdd("CountTestCache1");
        _ = CacheManager.Instance.GetOrAdd("CountTestCache2");

        // Act
        var count = CacheManager.Instance.GetCacheCount();

        // Assert
        Assert.Equal(3, count); // Default + 2 custom caches
    }

    [Fact]
    public void CacheExists_Should_Return_True_When_Cache_Exists()
    {
        // Arrange
        var cacheName = "ExistsTestCache";
        _ = CacheManager.Instance.GetOrAdd(cacheName);

        // Act
        var exists = CacheManager.Instance.CacheExists(cacheName);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public void CacheExists_Should_Return_False_When_Cache_Not_Exists()
    {
        // Act
        var exists = CacheManager.Instance.CacheExists("NonExistentCache");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public void CacheExists_With_Empty_Or_Null_Name_Should_Return_False()
    {
        // Act
        var existsWithEmpty = CacheManager.Instance.CacheExists("");
        var existsWithNull = CacheManager.Instance.CacheExists(null!);

        // Assert
        Assert.False(existsWithEmpty);
        Assert.False(existsWithNull);
    }
}
