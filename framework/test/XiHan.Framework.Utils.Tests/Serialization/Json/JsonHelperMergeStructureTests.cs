// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Utils.Serialization.Json;

namespace XiHan.Framework.Utils.Tests.Serialization.Json;

/// <summary>
/// MergeJson 结构与值类型保真测试
/// </summary>
/// <remarks>
/// 修复前 MergeJson 先扁平化成字符串字典再重建，标量一律降级为字符串（1 变 "1"、true 变 "True"），
/// 数组被重建成以下标为键的对象（[a,b] 变 {"0":"a","1":"b"}），合并结果与两侧输入都不同构。
/// 这里一律用 JsonDocument 断言 ValueKind 而不是断言文本，直接锁死"类型与结构不失真"这条语义。
/// </remarks>
public class JsonHelperMergeStructureTests
{
    /// <summary>
    /// 数字与布尔标量在合并后仍是数字与布尔，不被降级为字符串
    /// </summary>
    [Fact]
    public void MergeJson_PreservesScalarValueKinds()
    {
        var merged = JsonHelper.MergeJson("{\"count\":1,\"enabled\":true}", "{\"ratio\":1.5,\"disabled\":false}");

        using var document = JsonDocument.Parse(merged);
        var root = document.RootElement;

        Assert.Equal(JsonValueKind.Number, root.GetProperty("count").ValueKind);
        Assert.Equal(1, root.GetProperty("count").GetInt32());
        Assert.Equal(JsonValueKind.True, root.GetProperty("enabled").ValueKind);
        Assert.Equal(JsonValueKind.Number, root.GetProperty("ratio").ValueKind);
        Assert.Equal(1.5d, root.GetProperty("ratio").GetDouble());
        Assert.Equal(JsonValueKind.False, root.GetProperty("disabled").ValueKind);
    }

    /// <summary>
    /// null 字面量在合并后仍是 null，不被降级为空字符串
    /// </summary>
    [Fact]
    public void MergeJson_PreservesNullValueKind()
    {
        var merged = JsonHelper.MergeJson("{\"nickname\":null}", "{\"name\":\"曦寒\"}");

        using var document = JsonDocument.Parse(merged);

        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("nickname").ValueKind);
        Assert.Equal("曦寒", document.RootElement.GetProperty("name").GetString());
    }

    /// <summary>
    /// 数组在合并后仍是数组，不被重建成以下标为键的对象
    /// </summary>
    [Fact]
    public void MergeJson_PreservesArrayStructure()
    {
        var merged = JsonHelper.MergeJson("{\"tags\":[\"甲\",\"乙\"]}", "{\"count\":2}");

        using var document = JsonDocument.Parse(merged);
        var tags = document.RootElement.GetProperty("tags");

        Assert.Equal(JsonValueKind.Array, tags.ValueKind);
        Assert.Equal(2, tags.GetArrayLength());
        Assert.Equal("甲", tags[0].GetString());
        Assert.Equal("乙", tags[1].GetString());
    }

    /// <summary>
    /// 嵌套对象逐键递归合并，两侧的子键都保留且类型不变
    /// </summary>
    [Fact]
    public void MergeJson_MergesNestedObjectsRecursively()
    {
        var merged = JsonHelper.MergeJson(
            "{\"server\":{\"host\":\"localhost\",\"port\":80}}",
            "{\"server\":{\"port\":8080,\"secure\":true}}");

        using var document = JsonDocument.Parse(merged);
        var server = document.RootElement.GetProperty("server");

        Assert.Equal(JsonValueKind.Object, server.ValueKind);
        Assert.Equal("localhost", server.GetProperty("host").GetString());
        Assert.Equal(8080, server.GetProperty("port").GetInt32());
        Assert.True(server.GetProperty("secure").GetBoolean());
    }

    /// <summary>
    /// 数组冲突时整体取胜方，不做逐下标合并
    /// </summary>
    /// <remarks>
    /// 对象与数组之间没有"逐键合并"的语义，所以只要有一侧不是对象就整体取胜方；
    /// overwrite 决定谁是胜方。
    /// </remarks>
    [Fact]
    public void MergeJson_WhenArraysConflict_TakesWinningSideWhole()
    {
        var overwritten = JsonHelper.MergeJson("{\"tags\":[\"甲\"]}", "{\"tags\":[\"乙\",\"丙\"]}");
        var preserved = JsonHelper.MergeJson("{\"tags\":[\"甲\"]}", "{\"tags\":[\"乙\",\"丙\"]}", false);

        using var overwrittenDocument = JsonDocument.Parse(overwritten);
        using var preservedDocument = JsonDocument.Parse(preserved);

        var overwrittenTags = overwrittenDocument.RootElement.GetProperty("tags");
        Assert.Equal(2, overwrittenTags.GetArrayLength());
        Assert.Equal("乙", overwrittenTags[0].GetString());

        var preservedTags = preservedDocument.RootElement.GetProperty("tags");
        Assert.Equal(1, preservedTags.GetArrayLength());
        Assert.Equal("甲", preservedTags[0].GetString());
    }

    /// <summary>
    /// 标量与对象冲突时同样整体取胜方
    /// </summary>
    [Fact]
    public void MergeJson_WhenScalarConflictsWithObject_TakesWinningSideWhole()
    {
        var merged = JsonHelper.MergeJson("{\"server\":\"localhost\"}", "{\"server\":{\"host\":\"127.0.0.1\"}}");

        using var document = JsonDocument.Parse(merged);
        var server = document.RootElement.GetProperty("server");

        Assert.Equal(JsonValueKind.Object, server.ValueKind);
        Assert.Equal("127.0.0.1", server.GetProperty("host").GetString());
    }

    /// <summary>
    /// 根节点是数组时整体取胜方
    /// </summary>
    [Fact]
    public void MergeJson_WhenRootsAreArrays_TakesWinningSideWhole()
    {
        Assert.True(JsonHelper.CompareJson("[3]", JsonHelper.MergeJson("[1,2]", "[3]")));
        Assert.True(JsonHelper.CompareJson("[1,2]", JsonHelper.MergeJson("[1,2]", "[3]", false)));
    }

    /// <summary>
    /// 两份完全相同的 JSON 合并后与原文结构等价
    /// </summary>
    /// <remarks>
    /// CompareJson 对标量走 GetRawText 比对，数字被降级成字符串会立刻暴露，
    /// 所以这条用例能同时守住"结构同构"与"字面量不漂移"。
    /// </remarks>
    [Fact]
    public void MergeJson_WithIdenticalInputs_IsStructurallyEqualToSource()
    {
        const string Source = "{\"count\":1,\"enabled\":true,\"tags\":[\"甲\"],\"server\":{\"port\":8080}}";

        Assert.True(JsonHelper.CompareJson(Source, JsonHelper.MergeJson(Source, Source)));
    }

    /// <summary>
    /// 中文按原样保留，不被转义成 \uXXXX
    /// </summary>
    [Fact]
    public void MergeJson_KeepsChineseUnescaped()
    {
        var merged = JsonHelper.MergeJson("{\"name\":\"曦寒\"}", "{\"city\":\"上海\"}");

        Assert.Contains("曦寒", merged);
        Assert.Contains("上海", merged);
        Assert.DoesNotContain("\\u", merged);
    }

    /// <summary>
    /// 任一侧 JSON 非法时抛出 InvalidOperationException
    /// </summary>
    /// <remarks>
    /// 修复前非法输入会被 JsonToDictionary 静默吞成空字典，合并结果是一个 "{}"，
    /// 与方法自身声明的"失败即抛 InvalidOperationException"契约相悖。
    /// </remarks>
    /// <param name="left">第一个 JSON</param>
    /// <param name="right">第二个 JSON</param>
    [Theory]
    [InlineData("{不是 JSON", "{}")]
    [InlineData("{}", "[[[")]
    [InlineData("", "{}")]
    public void MergeJson_WhenInputInvalid_ThrowsInvalidOperationException(string left, string right)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            JsonHelper.MergeJson(left, right);
        });

        Assert.Contains("合并 JSON 失败", exception.Message);
    }
}
