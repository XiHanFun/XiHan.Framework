// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Serialization.Dynamic;

namespace XiHan.Framework.Serialization.Tests.Dynamic;

/// <summary>
/// 动态 JSON 节点类型取值、设值与序列化往返的测试
/// </summary>
public class DynamicJsonNodeTests
{
    /// <summary>
    /// DynamicJsonObject 设值、取值并序列化往返后，应保持数据一致
    /// </summary>
    [Fact]
    public void DynamicJsonObject_SetGetAndSerializeRoundtrip()
    {
        var obj = new DynamicJsonObject();
        obj.SetValue("name", "张三");
        obj["age"] = 30;
        obj["active"] = true;

        Assert.Equal("张三", obj.GetValue<string>("name"));
        Assert.Equal(30, obj.GetValue<int>("age"));
        Assert.True(obj.GetValue<bool>("active"));

        var compact = DynamicJsonHelper.Serialize(obj, false);
        Assert.Equal("""{"name":"张三","age":30,"active":true}""", compact);

        object? result = DynamicJsonHelper.Deserialize(compact);
        var reparsed = Assert.IsType<DynamicJsonObject>(result);

        Assert.Equal("张三", reparsed.GetValue<string>("name"));
        Assert.Equal(30, reparsed.GetValue<int>("age"));
        Assert.True(reparsed.GetValue<bool>("active"));
    }

    /// <summary>
    /// 通过索引访问嵌套对象时，应能逐层取得内层属性的值
    /// </summary>
    [Fact]
    public void DynamicJsonObject_NestedValueAccess()
    {
        const string json = """{"user":{"name":"张三","address":{"city":"上海"}}}""";

        object? result = DynamicJsonHelper.Deserialize(json);
        var obj = Assert.IsType<DynamicJsonObject>(result);

        var user = Assert.IsType<DynamicJsonObject>((object?)obj["user"]);
        Assert.Equal("张三", user.GetValue<string>("name"));

        var address = Assert.IsType<DynamicJsonObject>((object?)user["address"]);
        Assert.Equal("上海", address.GetValue<string>("city"));
    }

    /// <summary>
    /// DynamicJsonArray 应支持元素访问、修改、移除并保持序列化往返一致
    /// </summary>
    [Fact]
    public void DynamicJsonArray_ElementAccessAndModify()
    {
        var array = new DynamicJsonArray
        {
            1,
            "two",
            true
        };

        Assert.Equal(3, array.Count);
        Assert.Equal(1, Assert.IsType<DynamicJsonValue>(array[0]).ToObject<int>());
        Assert.Equal("two", Assert.IsType<DynamicJsonValue>(array[1]).ToObject<string>());
        Assert.True(Assert.IsType<DynamicJsonValue>(array[2]).ToObject<bool>());

        array[0] = 10;
        Assert.Equal(10, Assert.IsType<DynamicJsonValue>(array[0]).ToObject<int>());

        array.RemoveAt(1);
        Assert.Equal(2, array.Count);

        object? result = DynamicJsonHelper.Deserialize(DynamicJsonHelper.Serialize(array, false));
        var reparsed = Assert.IsType<DynamicJsonArray>(result);

        Assert.Equal(2, reparsed.Count);
        Assert.Equal(10, Assert.IsType<DynamicJsonValue>(reparsed[0]).ToObject<int>());
        Assert.True(Assert.IsType<DynamicJsonValue>(reparsed[1]).ToObject<bool>());
    }

    /// <summary>
    /// DynamicJsonValue 应正确报告值类型、类型转换与字符串表示
    /// </summary>
    [Fact]
    public void DynamicJsonValue_ValueKindConversionAndToString()
    {
        var number = new DynamicJsonValue(42);
        Assert.Equal(JsonValueKind.Number, number.ValueKind);
        Assert.Equal(42, number.ToObject<int>());
        Assert.Equal("42", number.ToString());

        var text = new DynamicJsonValue("hello");
        Assert.Equal(JsonValueKind.String, text.ValueKind);
        Assert.Equal("hello", text.ToObject<string>());

        var nullValue = DynamicJsonValue.CreateNull();
        Assert.True(nullValue.IsNull);
        Assert.False(nullValue.HasValue);
        Assert.Equal(JsonValueKind.Null, nullValue.ValueKind);
    }

    /// <summary>
    /// DynamicJsonProperty 应正确暴露属性名与值，并提供字符串表示
    /// </summary>
    [Fact]
    public void DynamicJsonProperty_NameValueAndToString()
    {
        var property = new DynamicJsonProperty("name", "张三");

        Assert.Equal("name", property.Name);
        Assert.Equal("张三", property.Value?.ToString());
        Assert.Equal("张三", property.ToObject<string>());
        Assert.Equal("\"name\": \"张三\"", property.ToString());
    }
}
