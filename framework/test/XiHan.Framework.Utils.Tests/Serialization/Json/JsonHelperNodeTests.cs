// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Serialization.Json;

namespace XiHan.Framework.Utils.Tests.Serialization.Json;

/// <summary>
/// JsonHelper 节点查询与增删改测试
/// </summary>
/// <remarks>
/// 节点路径实现只支持"点号分段 + 数组下标 + 单层通配符"，不是完整 JSONPath；
/// 这里按其实际契约断言，并全部通过重新解析结果来校验，避免依赖中间文本的转义形式。
/// </remarks>
public class JsonHelperNodeTests
{
    private const string SampleJson = """
        {
          "user": { "name": "曦寒", "age": 18 },
          "tags": [ "甲", "乙" ],
          "items": [ { "id": "x1" }, { "id": "x2" } ]
        }
        """;

    /// <summary>
    /// 按嵌套路径查询叶子节点，返回去引号的原始值
    /// </summary>
    [Fact]
    public void QueryNode_WithNestedPath_ReturnsValue()
    {
        Assert.Equal("曦寒", JsonHelper.QueryNode(SampleJson, "$.user.name"));
        Assert.Equal("18", JsonHelper.QueryNode(SampleJson, "$.user.age"));
    }

    /// <summary>
    /// 路径中的数字段被当作数组下标
    /// </summary>
    [Fact]
    public void QueryNode_WithArrayIndex_ReturnsElement()
    {
        Assert.Equal("甲", JsonHelper.QueryNode(SampleJson, "$.tags.0"));
        Assert.Equal("乙", JsonHelper.QueryNode(SampleJson, "$.tags.1"));
        Assert.Equal("x2", JsonHelper.QueryNode(SampleJson, "$.items.1.id"));
    }

    /// <summary>
    /// 路径不存在或下标越界时返回 null
    /// </summary>
    [Fact]
    public void QueryNode_WhenPathMissing_ReturnsNull()
    {
        Assert.Null(JsonHelper.QueryNode(SampleJson, "$.user.missing"));
        Assert.Null(JsonHelper.QueryNode(SampleJson, "$.tags.9"));
    }

    /// <summary>
    /// JSON 非法时返回 null 而不是抛异常
    /// </summary>
    [Fact]
    public void QueryNode_WhenJsonInvalid_ReturnsNull()
    {
        Assert.Null(JsonHelper.QueryNode("{不是 JSON", "$.a"));
    }

    /// <summary>
    /// JSON 或路径为空白时返回 null
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="path">路径表达式</param>
    [Theory]
    [InlineData("", "$.a")]
    [InlineData("   ", "$.a")]
    [InlineData("{\"a\":1}", "")]
    [InlineData("{\"a\":1}", "   ")]
    public void QueryNode_WhenArgumentsBlank_ReturnsNull(string json, string path)
    {
        Assert.Null(JsonHelper.QueryNode(json, path));
    }

    /// <summary>
    /// 通配符可展开数组中每个元素的同名属性
    /// </summary>
    [Fact]
    public void QueryNodes_WithWildcard_ReturnsAllMatches()
    {
        var ids = JsonHelper.QueryNodes(SampleJson, "$.items.*.id");

        Assert.Equal(new[] { "x1", "x2" }, ids);
    }

    /// <summary>
    /// 通配符直接展开数组元素本身
    /// </summary>
    [Fact]
    public void QueryNodes_WithWildcardOnArray_ReturnsAllElements()
    {
        var tags = JsonHelper.QueryNodes(SampleJson, "$.tags.*");

        Assert.Equal(new[] { "甲", "乙" }, tags);
    }

    /// <summary>
    /// JSON 非法或路径为空时返回空集合
    /// </summary>
    [Fact]
    public void QueryNodes_WhenJsonInvalidOrPathBlank_ReturnsEmpty()
    {
        Assert.Empty(JsonHelper.QueryNodes("{不是 JSON", "$.a.*"));
        Assert.Empty(JsonHelper.QueryNodes(SampleJson, "   "));
    }

    /// <summary>
    /// 设置已存在路径的值，其余节点保持不变
    /// </summary>
    [Fact]
    public void SetNode_WithExistingPath_UpdatesValue()
    {
        var updated = JsonHelper.SetNode(SampleJson, "$.user.name", "子墨");

        Assert.Equal("子墨", JsonHelper.QueryNode(updated, "$.user.name"));
        Assert.Equal("18", JsonHelper.QueryNode(updated, "$.user.age"));
    }

    /// <summary>
    /// 设置数组下标位置的值
    /// </summary>
    [Fact]
    public void SetNode_WithArrayIndex_UpdatesElement()
    {
        var updated = JsonHelper.SetNode(SampleJson, "$.tags.0", "丙");

        Assert.Equal("丙", JsonHelper.QueryNode(updated, "$.tags.0"));
        Assert.Equal("乙", JsonHelper.QueryNode(updated, "$.tags.1"));
    }

    /// <summary>
    /// JSON 非法时设置节点抛出 InvalidOperationException
    /// </summary>
    [Fact]
    public void SetNode_WhenJsonInvalid_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            JsonHelper.SetNode("{不是 JSON", "$.a", "v");
        });

        Assert.Contains("设置节点失败", exception.Message);
    }

    /// <summary>
    /// 向对象父节点添加新属性
    /// </summary>
    [Fact]
    public void AddNode_ToObject_AddsProperty()
    {
        var updated = JsonHelper.AddNode(SampleJson, "$.user", "email", "xihan@example.com");

        Assert.Equal("xihan@example.com", JsonHelper.QueryNode(updated, "$.user.email"));
        Assert.Equal("曦寒", JsonHelper.QueryNode(updated, "$.user.name"));
    }

    /// <summary>
    /// 向数组父节点追加元素时忽略键名
    /// </summary>
    [Fact]
    public void AddNode_ToArray_AppendsElement()
    {
        var updated = JsonHelper.AddNode(SampleJson, "$.tags", "被忽略的键", "丙");

        Assert.Equal(new[] { "甲", "乙", "丙" }, JsonHelper.QueryNodes(updated, "$.tags.*"));
    }

    /// <summary>
    /// 父节点不存在时抛出 InvalidOperationException
    /// </summary>
    [Fact]
    public void AddNode_WhenParentMissing_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            JsonHelper.AddNode(SampleJson, "$.notExists", "k", "v");
        });

        Assert.Contains("添加节点失败", exception.Message);
    }

    /// <summary>
    /// 删除对象属性后该路径查询不到
    /// </summary>
    [Fact]
    public void RemoveNode_WithExistingPath_RemovesProperty()
    {
        var updated = JsonHelper.RemoveNode(SampleJson, "$.user.age");

        Assert.Null(JsonHelper.QueryNode(updated, "$.user.age"));
        Assert.Equal("曦寒", JsonHelper.QueryNode(updated, "$.user.name"));
    }

    /// <summary>
    /// 按下标删除数组元素后剩余元素前移
    /// </summary>
    [Fact]
    public void RemoveNode_FromArray_RemovesElement()
    {
        var updated = JsonHelper.RemoveNode(SampleJson, "$.tags.0");

        Assert.Equal(new[] { "乙" }, JsonHelper.QueryNodes(updated, "$.tags.*"));
    }

    /// <summary>
    /// 删除不存在的节点是幂等操作，内容保持等价
    /// </summary>
    [Fact]
    public void RemoveNode_WhenPathMissing_KeepsJsonEquivalent()
    {
        var updated = JsonHelper.RemoveNode(SampleJson, "$.notExists");

        Assert.True(JsonHelper.CompareJson(SampleJson, updated));
    }

    /// <summary>
    /// 删除节点时 JSON 非法抛出 InvalidOperationException
    /// </summary>
    [Fact]
    public void RemoveNode_WhenJsonInvalid_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            JsonHelper.RemoveNode("{不是 JSON", "$.a");
        });

        Assert.Contains("删除节点失败", exception.Message);
    }
}
