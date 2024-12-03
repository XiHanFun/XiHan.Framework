using JetBrains.Annotations;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Utils.Test.Collections;

[TestSubject(typeof(ListExtensions))]
public class ListExtensionsTest
{
    [Fact]
    public void InsertRange_InsertsItemsAtSpecifiedIndex()
    {
        var list = new List<int>
        {
            1, 2, 5
        };
        list.InsertRange(2, [3, 4]);
        Assert.Equal([1, 2, 3, 4, 5], list);
    }

    [Fact]
    public void InsertRange_InsertsItemsAtStart()
    {
        var list = new List<int>
        {
            3, 4, 5
        };
        list.InsertRange(0, [1, 2]);
        Assert.Equal([1, 2, 3, 4, 5], list);
    }

    [Fact]
    public void InsertRange_InsertsItemsAtEnd()
    {
        var list = new List<int>
        {
            1, 2, 3
        };
        list.InsertRange(3, [4, 5]);
        Assert.Equal([1, 2, 3, 4, 5], list);
    }

    [Fact]
    public void FindIndex_ReturnsCorrectIndex_WhenItemMatches()
    {
        var list = new List<int>
        {
            1,
            2,
            3,
            4,
            5
        };
        var index = list.FindIndex(x => x == 3);
        Assert.Equal(2, index);
    }

    [Fact]
    public void FindIndex_ReturnsNegativeOne_WhenNoItemMatches()
    {
        var list = new List<int>
        {
            1,
            2,
            3,
            4,
            5
        };
        var index = list.FindIndex(x => x == 6);
        Assert.Equal(-1, index);
    }

    [Fact]
    public void AddFirst_AddsItemAtStart()
    {
        var list = new List<int>
        {
            2, 3, 4
        };
        list.AddFirst(1);
        Assert.Equal([1, 2, 3, 4], list);
    }

    [Fact]
    public void AddLast_AddsItemAtEnd()
    {
        var list = new List<int>
        {
            1, 2, 3
        };
        list.AddLast(4);
        Assert.Equal([1, 2, 3, 4], list);
    }

    [Fact]
    public void InsertAfter_InsertsItemAfterExistingItem()
    {
        var list = new List<int>
        {
            1, 2, 4
        };
        list.InsertAfter(2, 3);
        Assert.Equal([1, 2, 3, 4], list);
    }

    [Fact]
    public void InsertAfter_AddsItemAtStart_WhenExistingItemNotFound()
    {
        var list = new List<int>
        {
            2, 3, 4
        };
        list.InsertAfter(1, 1);
        Assert.Equal([1, 2, 3, 4], list);
    }

    [Fact]
    public void InsertBefore_InsertsItemBeforeExistingItem()
    {
        var list = new List<int>
        {
            1, 3, 4
        };
        list.InsertBefore(3, 2);
        Assert.Equal([1, 2, 3, 4], list);
    }

    [Fact]
    public void InsertBefore_AddsItemAtEnd_WhenExistingItemNotFound()
    {
        var list = new List<int>
        {
            1, 2, 3
        };
        list.InsertBefore(4, 4);
        Assert.Equal([1, 2, 3, 4], list);
    }

    [Fact]
    public void ReplaceWhile_ReplacesAllMatchingItems()
    {
        var list = new List<int>
        {
            1,
            2,
            3,
            2,
            4
        };
        list.ReplaceWhile(x => x == 2, 5);
        Assert.Equal([1, 5, 3, 5, 4], list);
    }

    [Fact]
    public void ReplaceWhileWithFactory_ReplacesAllMatchingItemsWithFactory()
    {
        var list = new List<int>
        {
            1,
            2,
            3,
            2,
            4
        };
        list.ReplaceWhile(x => x == 2, x => x + 3);
        Assert.Equal([1, 5, 3, 5, 4], list);
    }

    [Fact]
    public void ReplaceOne_ReplacesFirstMatchingItem()
    {
        var list = new List<int>
        {
            1,
            2,
            3,
            2,
            4
        };
        list.ReplaceOne(x => x == 2, 5);
        Assert.Equal([1, 5, 3, 2, 4], list);
    }

    [Fact]
    public void ReplaceOneWithFactory_ReplacesFirstMatchingItemWithFactory()
    {
        var list = new List<int>
        {
            1,
            2,
            3,
            2,
            4
        };
        list.ReplaceOne(x => x == 2, x => x + 3);
        Assert.Equal([1, 5, 3, 2, 4], list);
    }

    [Fact]
    public void ReplaceOne_ReplacesFirstMatchingItemByValue()
    {
        var list = new List<int>
        {
            1,
            2,
            3,
            2,
            4
        };
        list.ReplaceOne(2, 5);
        Assert.Equal([1, 5, 3, 2, 4], list);
    }

    [Fact]
    public void MoveItem_MovesItemToTargetIndex()
    {
        var list = new List<int>
        {
            1,
            2,
            3,
            4,
            5
        };
        list.MoveItem(x => x == 3, 1);
        Assert.Equal([1, 3, 2, 4, 5], list);
    }

    [Fact]
    public void MoveItem_ThrowsException_WhenTargetIndexIsOutOfRange()
    {
        var list = new List<int>
        {
            1,
            2,
            3,
            4,
            5
        };
        _ = Assert.Throws<IndexOutOfRangeException>(() => list.MoveItem(x => x == 3, 10));
    }

    [Fact]
    public void GetOrAdd_ReturnsExistingItem_WhenItemMatches()
    {
        var list = new List<int>
        {
            1, 2, 3
        };
        var result = list.GetOrAdd(x => x == 2, () => 4);
        Assert.Equal(2, result);
    }

    [Fact]
    public void GetOrAdd_AddsAndReturnsNewItem_WhenNoItemMatches()
    {
        var list = new List<int>
        {
            1, 2, 3
        };
        var result = list.GetOrAdd(x => x == 4, () => 4);
        Assert.Equal(4, result);
        Assert.Contains(4, list);
    }

    [Fact]
    public void SortByDependencies_SortsItemsByDependencies()
    {
        var list = new List<string>
        {
            "a", "b", "c"
        };
        var dependencies = new Dictionary<string, string[]>
        {
            {
                "a", []
            },
            {
                "b", ["a"]
            },
            {
                "c", ["b"]
            }
        };
        var result = list.SortByDependencies(x => dependencies[x]);
        Assert.Equal(["a", "b", "c"], result);
    }

    [Fact]
    public void SortByDependencies_ThrowsException_WhenCircularDependencyDetected()
    {
        var list = new List<string>
        {
            "a", "b", "c"
        };
        var dependencies = new Dictionary<string, string[]>
        {
            {
                "a", ["c"]
            },
            {
                "b", ["a"]
            },
            {
                "c", ["b"]
            }
        };
        _ = Assert.Throws<ArgumentException>(() => list.SortByDependencies(x => dependencies[x]));
    }
}
