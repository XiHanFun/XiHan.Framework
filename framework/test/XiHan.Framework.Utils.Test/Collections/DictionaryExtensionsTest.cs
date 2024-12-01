using JetBrains.Annotations;
using System.Collections.Concurrent;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Utils.Test.Collections;

[TestSubject(typeof(DictionaryExtensions))]
public class DictionaryExtensionsTest
{
    [Fact]
    public void TryGetValue_ReturnsTrueAndValue_WhenKeyExists()
    {
        var dictionary = new Dictionary<string, object> { { "key", 42 } };
        var result = dictionary.TryGetValue("key", out var value);
        Assert.True(result);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetValue_ReturnsFalseAndDefault_WhenKeyDoesNotExist()
    {
        var dictionary = new Dictionary<string, object>();
        var result = dictionary.TryGetValue("key", out var value);
        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public void GetOrDefault_ReturnsValue_WhenKeyExists()
    {
        var dictionary = new Dictionary<string, int> { { "key", 42 } };
        var result = dictionary.GetOrDefault("key");
        Assert.Equal(42, result);
    }

    [Fact]
    public void GetOrDefault_ReturnsDefault_WhenKeyDoesNotExist()
    {
        var dictionary = new Dictionary<string, int>();
        var result = dictionary.GetOrDefault("key");
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetOrAdd_AddsAndReturnsValue_WhenKeyDoesNotExist()
    {
        var dictionary = new Dictionary<string, int>();
        var result = dictionary.GetOrAdd("key", () => 42);
        Assert.Equal(42, result);
        Assert.Equal(42, dictionary["key"]);
    }

    [Fact]
    public void GetOrAdd_ReturnsExistingValue_WhenKeyExists()
    {
        var dictionary = new Dictionary<string, int> { { "key", 42 } };
        var result = dictionary.GetOrAdd("key", () => 100);
        Assert.Equal(42, result);
    }

    [Fact]
    public void ConvertToDynamicObject_ReturnsExpandoObject_WithSameValues()
    {
        var dictionary = new Dictionary<string, object> { { "key", 42 } };
        dynamic expando = dictionary.ConvertToDynamicObject();
        Assert.Equal(42, expando.key);
    }

    [Fact]
    public void GetOrDefault_ConcurrentDictionary_ReturnsValue_WhenKeyExists()
    {
        var dictionary = new ConcurrentDictionary<string, int>();
        dictionary["key"] = 42;
        var result = dictionary.GetOrDefault("key");
        Assert.Equal(42, result);
    }

    [Fact]
    public void GetOrDefault_ConcurrentDictionary_ReturnsDefault_WhenKeyDoesNotExist()
    {
        var dictionary = new ConcurrentDictionary<string, int>();
        var result = dictionary.GetOrDefault("key");
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetOrAdd_ConcurrentDictionary_AddsAndReturnsValue_WhenKeyDoesNotExist()
    {
        var dictionary = new ConcurrentDictionary<string, int>();
        var result = dictionary.GetOrAdd("key", () => 42);
        Assert.Equal(42, result);
        Assert.Equal(42, dictionary["key"]);
    }

    [Fact]
    public void GetOrAdd_ConcurrentDictionary_ReturnsExistingValue_WhenKeyExists()
    {
        var dictionary = new ConcurrentDictionary<string, int>();
        dictionary["key"] = 42;
        var result = dictionary.GetOrAdd("key", () => 100);
        Assert.Equal(42, result);
    }
}
