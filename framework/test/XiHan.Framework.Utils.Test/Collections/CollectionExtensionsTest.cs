using JetBrains.Annotations;
using XiHan.Framework.Utils.Collections;
using CollectionExtensions = XiHan.Framework.Utils.Collections.CollectionExtensions;

namespace XiHan.Framework.Utils.Test.Collections;

[TestSubject(typeof(CollectionExtensions))]
public class CollectionExtensionsTest
{
    [Fact]
    public void IsNullOrEmpty_ReturnsTrue_WhenCollectionIsNull()
    {
        ICollection<int>? collection = null;
        var result = collection.IsNullOrEmpty();
        Assert.True(result);
    }

    [Fact]
    public void IsNullOrEmpty_ReturnsTrue_WhenCollectionIsEmpty()
    {
        var collection = new List<int>();
        var result = collection.IsNullOrEmpty();
        Assert.True(result);
    }

    [Fact]
    public void IsNullOrEmpty_ReturnsFalse_WhenCollectionIsNotEmpty()
    {
        var collection = new List<int>
        {
            1
        };
        var result = collection.IsNullOrEmpty();
        Assert.False(result);
    }

    [Fact]
    public void AddIf_AddsItem_WhenConditionIsTrue()
    {
        var collection = new List<int>();
        collection.AddIf(1, true);
        Assert.Contains(1, collection);
    }

    [Fact]
    public void AddIf_DoesNotAddItem_WhenConditionIsFalse()
    {
        var collection = new List<int>();
        collection.AddIf(1, false);
        Assert.DoesNotContain(1, collection);
    }

    [Fact]
    public void AddIfWithFunc_AddsItem_WhenConditionIsTrue()
    {
        var collection = new List<int>();
        collection.AddIf(1, () => true);
        Assert.Contains(1, collection);
    }

    [Fact]
    public void AddIfWithFunc_DoesNotAddItem_WhenConditionIsFalse()
    {
        var collection = new List<int>();
        collection.AddIf(1, () => false);
        Assert.DoesNotContain(1, collection);
    }

    [Fact]
    public void AddIfNotNull_AddsItem_WhenItemIsNotNull()
    {
        var collection = new List<int?>();
        collection.AddIfNotNull(1);
        Assert.Contains(1, collection);
    }

    [Fact]
    public void AddIfNotNull_DoesNotAddItem_WhenItemIsNull()
    {
        var collection = new List<int?>();
        collection.AddIfNotNull(null);
        Assert.DoesNotContain(null, collection);
    }

    [Fact]
    public void AddIfNotContains_AddsItem_WhenItemDoesNotExist()
    {
        var collection = new List<int>
        {
            1
        };
        var result = collection.AddIfNotContains(2);
        Assert.True(result);
        Assert.Contains(2, collection);
    }

    [Fact]
    public void AddIfNotContains_DoesNotAddItem_WhenItemExists()
    {
        var collection = new List<int>
        {
            1
        };
        var result = collection.AddIfNotContains(1);
        Assert.False(result);
    }

    [Fact]
    public void AddIfNotContainsWithItems_AddsItems_WhenItemsDoNotExist()
    {
        var collection = new List<int>
        {
            1
        };
        var itemsToAdd = new List<int>
        {
            2, 3
        };
        var result = collection.AddIfNotContains(itemsToAdd);
        Assert.Contains(2, collection);
        Assert.Contains(3, collection);
        Assert.Equal(itemsToAdd, result);
    }

    [Fact]
    public void AddIfNotContainsWithPredicate_AddsItem_WhenPredicateDoesNotMatch()
    {
        var collection = new List<int>
        {
            1
        };
        var result = collection.AddIfNotContains(x => x == 2, () => 2);
        Assert.True(result);
        Assert.Contains(2, collection);
    }

    [Fact]
    public void AddIfNotContainsWithPredicate_DoesNotAddItem_WhenPredicateMatches()
    {
        var collection = new List<int>
        {
            1
        };
        var result = collection.AddIfNotContains(x => x == 1, () => 2);
        Assert.False(result);
    }

    [Fact]
    public void RemoveAllWhere_RemovesItems_WhenPredicateMatches()
    {
        var collection = new List<int>
        {
            1, 2, 3
        };
        var result = collection.RemoveAllWhere(x => x > 1);
        Assert.Equal([2, 3], result);
        Assert.DoesNotContain(2, collection);
        Assert.DoesNotContain(3, collection);
    }

    [Fact]
    public void RemoveAll_RemovesSpecifiedItems()
    {
        var collection = new List<int>
        {
            1, 2, 3
        };
        var itemsToRemove = new List<int>
        {
            2, 3
        };
        collection.RemoveAll(itemsToRemove);
        Assert.DoesNotContain(2, collection);
        Assert.DoesNotContain(3, collection);
    }
}
