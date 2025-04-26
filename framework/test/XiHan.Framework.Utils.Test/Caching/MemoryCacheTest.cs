using XiHan.Framework.Utils.Caching;

namespace XiHan.Framework.Utils.Test.Caching;

public class MemoryCacheTest
{
    [Fact]
    public void Set_And_TryGet_Should_Work()
    {
        // Arrange
        var cache = new MemoryCache();
        var key = "testKey";
        var value = "testValue";

        // Act
        var setResult = cache.Set(key, value);
        var getResult = cache.TryGet(key, out string? retrievedValue);

        // Assert
        Assert.True(setResult);
        Assert.True(getResult);
        Assert.Equal(value, retrievedValue);
    }

    [Fact]
    public void TryGet_With_NonExistent_Key_Should_Return_False()
    {
        // Arrange
        var cache = new MemoryCache();
        var key = "nonExistentKey";

        // Act
        var result = cache.TryGet<string>(key, out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Set_With_Absolute_Expiration_Should_Expire()
    {
        // Arrange
        var cache = new MemoryCache();
        var key = "expiringKey";
        var value = "expiringValue";
        var expiration = DateTime.Now.AddMilliseconds(100);

        // Act
        cache.Set(key, value, expiration);

        // Wait for expiration
        Thread.Sleep(200);

        var result = cache.TryGet(key, out string? _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Set_With_Sliding_Expiration_Should_Expire()
    {
        // Arrange
        var cache = new MemoryCache();
        var key = "slidingKey";
        var value = "slidingValue";
        var expiration = TimeSpan.FromMilliseconds(100);

        // Act
        cache.Set(key, value, expiration);

        // Wait for expiration
        Thread.Sleep(200);

        var result = cache.TryGet(key, out string? _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetOrAdd_Should_Add_When_Key_Not_Exists()
    {
        // Arrange
        var cache = new MemoryCache();
        var key = "newKey";
        var value = "newValue";

        // Act
        var result = cache.GetOrAdd(key, () => value);

        // Assert
        Assert.Equal(value, result);
        Assert.True(cache.TryGet(key, out string? storedValue));
        Assert.Equal(value, storedValue);
    }

    [Fact]
    public void GetOrAdd_Should_Return_Existing_When_Key_Exists()
    {
        // Arrange
        var cache = new MemoryCache();
        var key = "existingKey";
        var existingValue = "existingValue";
        var newValue = "newValue";

        cache.Set(key, existingValue);

        // Act
        var result = cache.GetOrAdd(key, () => newValue);

        // Assert
        Assert.Equal(existingValue, result);
    }

    [Fact]
    public async Task GetOrAddAsync_Should_Add_When_Key_Not_Exists()
    {
        // Arrange
        var cache = new MemoryCache();
        var key = "asyncKey";
        var value = "asyncValue";

        // Act
        var result = await cache.GetOrAddAsync(key, () => Task.FromResult(value));

        // Assert
        Assert.Equal(value, result);
        Assert.True(cache.TryGet(key, out string? storedValue));
        Assert.Equal(value, storedValue);
    }

    [Fact]
    public void Remove_Should_Remove_Item()
    {
        // Arrange
        var cache = new MemoryCache();
        var key = "removeKey";
        var value = "removeValue";

        cache.Set(key, value);

        // Act
        var removeResult = cache.Remove(key);
        var getResult = cache.TryGet(key, out string? _);

        // Assert
        Assert.True(removeResult);
        Assert.False(getResult);
    }

    [Fact]
    public void Clear_Should_Remove_All_Items()
    {
        // Arrange
        var cache = new MemoryCache();
        cache.Set("key1", "value1");
        cache.Set("key2", "value2");
        cache.Set("key3", "value3");

        // Act
        cache.Clear();
        var count = cache.Count();

        // Assert
        Assert.Equal(0, count);
        Assert.False(cache.TryGet("key1", out string? _));
        Assert.False(cache.TryGet("key2", out string? _));
        Assert.False(cache.TryGet("key3", out string? _));
    }

    [Fact]
    public void Contains_Should_Return_True_When_Key_Exists()
    {
        // Arrange
        var cache = new MemoryCache();
        var key = "containsKey";
        var value = "containsValue";

        cache.Set(key, value);

        // Act
        var result = cache.Contains(key);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Contains_Should_Return_False_When_Key_Not_Exists()
    {
        // Arrange
        var cache = new MemoryCache();
        var key = "nonExistingKey";

        // Act
        var result = cache.Contains(key);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ForEach_Should_Execute_Action_For_Each_Item()
    {
        // Arrange
        var cache = new MemoryCache();
        cache.Set("key1", "value1");
        cache.Set("key2", "value2");
        cache.Set("key3", "value3");

        var keys = new List<string>();
        var values = new List<string>();

        // Act
        cache.ForEach<string>((key, value) =>
        {
            keys.Add(key);
            values.Add(value);
        });

        // Assert
        Assert.Equal(3, keys.Count);
        Assert.Equal(3, values.Count);
        Assert.Contains("key1", keys);
        Assert.Contains("key2", keys);
        Assert.Contains("key3", keys);
        Assert.Contains("value1", values);
        Assert.Contains("value2", values);
        Assert.Contains("value3", values);
    }

    [Fact]
    public void Count_Should_Return_Correct_Item_Count()
    {
        // Arrange
        var cache = new MemoryCache();

        // Act & Assert - Empty cache
        Assert.Equal(0, cache.Count());

        // Add items
        cache.Set("key1", "value1");
        cache.Set("key2", "value2");

        // Act & Assert - Two items
        Assert.Equal(2, cache.Count());

        // Add one more
        cache.Set("key3", "value3");

        // Act & Assert - Three items
        Assert.Equal(3, cache.Count());

        // Remove one
        cache.Remove("key2");

        // Act & Assert - Two items again
        Assert.Equal(2, cache.Count());
    }
}
