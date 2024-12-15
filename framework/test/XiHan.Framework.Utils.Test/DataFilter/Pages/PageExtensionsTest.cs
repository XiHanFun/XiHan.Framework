using JetBrains.Annotations;
using XiHan.Framework.Utils.DataFilter.Pages;
using XiHan.Framework.Utils.DataFilter.Pages.Dtos;

[TestSubject(typeof(PageExtensions))]
public class PageExtensionsTests
{
    [Fact]
    public void ToPageList_IEnumerable_ReturnsCorrectPage()
    {
        var data = Enumerable.Range(1, 100).Select(i => new TestClass
        {
            Value = i
        }).ToList();
        var result = data.ToPageList(2, 10);
        var expected = Enumerable.Range(11, 10).Select(i => new TestClass
        {
            Value = i
        }).ToList();

        Assert.Equal(expected.Select(e => e.Value), result.Select(r => r.Value));
    }

    [Fact]
    public void ToPageList_IQueryable_ReturnsCorrectPage()
    {
        var data = Enumerable.Range(1, 100).Select(i => new TestClass
        {
            Value = i
        }).AsQueryable();
        var result = data.ToPageList(2, 10);
        var expected = Enumerable.Range(11, 10).Select(i => new TestClass
        {
            Value = i
        }).ToList();

        Assert.Equal(expected.Select(e => e.Value), result.Select(r => r.Value));
    }

    [Fact]
    public void ToPageList_IEnumerable_WithPageInfoDto_ReturnsCorrectPage()
    {
        var data = Enumerable.Range(1, 100).Select(i => new TestClass
        {
            Value = i
        }).ToList();
        var pageInfo = new PageInfoDto
        {
            CurrentIndex = 2, PageSize = 10
        };
        var result = data.ToPageList(pageInfo);
        var expected = Enumerable.Range(11, 10).Select(i => new TestClass
        {
            Value = i
        }).ToList();

        Assert.Equal(expected.Select(e => e.Value), result.Select(r => r.Value));
    }

    [Fact]
    public void ToPageList_IQueryable_WithPageInfoDto_ReturnsCorrectPage()
    {
        var data = Enumerable.Range(1, 100).Select(i => new TestClass
        {
            Value = i
        }).AsQueryable();
        var pageInfo = new PageInfoDto
        {
            CurrentIndex = 2, PageSize = 10
        };
        var result = data.ToPageList(pageInfo);
        var expected = Enumerable.Range(11, 10).Select(i => new TestClass
        {
            Value = i
        }).ToList();

        Assert.Equal(expected.Select(e => e.Value), result.Select(r => r.Value));
    }

    [Fact]
    public void ToPageData_IEnumerable_ReturnsCorrectPageData()
    {
        var data = Enumerable.Range(1, 100).Select(i => new TestClass
        {
            Value = i
        }).ToList();
        var result = data.ToPageData(2, 10);
        Assert.Equal(2, result.PageInfo.CurrentIndex);
        Assert.Equal(10, result.PageInfo.PageSize);
        Assert.Equal(100, result.TotalCount);
    }

    [Fact]
    public void ToPageData_IQueryable_ReturnsCorrectPageData()
    {
        var data = Enumerable.Range(1, 100).Select(i => new TestClass
        {
            Value = i
        }).AsQueryable();
        var result = data.ToPageData(2, 10);
        Assert.Equal(2, result.PageInfo.CurrentIndex);
        Assert.Equal(10, result.PageInfo.PageSize);
        Assert.Equal(100, result.TotalCount);
    }

    [Fact]
    public void ToPagePageResponse_IEnumerable_ReturnsCorrectPageResponse()
    {
        var data = Enumerable.Range(1, 100).Select(i => new TestClass
        {
            Value = i
        }).ToList();
        var result = data.ToPagePageResponse(2, 10);

        Assert.Equal(2, result.PageData.PageInfo.CurrentIndex);
        Assert.Equal(10, result.PageData.PageInfo.PageSize);
        Assert.Equal(100, result.PageData.TotalCount);
        var expected = Enumerable.Range(11, 10).Select(i => new TestClass
        {
            Value = i
        }).ToList();

        Assert.Equal(expected.Select(e => e.Value), result.ResponseDatas?.Select(r => r.Value));
    }

    [Fact]
    public void ToPagePageResponse_IQueryable_ReturnsCorrectPageResponse()
    {
        var data = Enumerable.Range(1, 100).Select(i => new TestClass
        {
            Value = i
        }).AsQueryable();
        var result = data.ToPagePageResponse(2, 10);

        Assert.Equal(2, result.PageData.PageInfo.CurrentIndex);
        Assert.Equal(10, result.PageData.PageInfo.PageSize);
        Assert.Equal(100, result.PageData.TotalCount);
        var expected = Enumerable.Range(11, 10).Select(i => new TestClass
        {
            Value = i
        }).ToList();

        Assert.Equal(expected.Select(e => e.Value), result.ResponseDatas?.Select(r => r.Value));
    }

    public class TestClass
    {
        public int Value { get; set; }
    }
}
