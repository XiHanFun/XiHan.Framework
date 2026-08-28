// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Serialization.Json;

namespace XiHan.Framework.Utils.Tests.Serialization.Json;

/// <summary>
/// JsonHelper 序列化与反序列化契约测试
/// </summary>
/// <remarks>
/// 只锁定对外可观察的契约：命名策略、编码器、异常类型、错误处理策略与往返一致性；
/// 不锁定缩进空格数等纯格式细节，避免实现微调即红灯。
/// </remarks>
public class JsonHelperSerializationTests
{
    /// <summary>
    /// 构造一个字段齐全的示例用户
    /// </summary>
    private static JsonSampleUser CreateSampleUser()
    {
        return new JsonSampleUser
        {
            Name = "曦寒",
            Age = 18,
            IsActive = true,
            Nickname = null,
            Tags = ["框架", "工具库"],
            Address = new JsonSampleAddress { City = "上海", Country = "中国" }
        };
    }

    /// <summary>
    /// 默认选项使用驼峰命名，且中文按原样输出不转义
    /// </summary>
    [Fact]
    public void Serialize_WithDefaultOptions_UsesCamelCaseAndKeepsChinese()
    {
        var json = JsonHelper.Serialize(CreateSampleUser());

        Assert.Contains("\"name\"", json);
        Assert.Contains("\"isActive\"", json);
        Assert.DoesNotContain("\"Name\"", json);
        // 默认编码器为 UnsafeRelaxedJsonEscaping，中文必须是原样字符而非 \uXXXX
        Assert.Contains("曦寒", json);
        Assert.DoesNotContain("\\u", json);
    }

    /// <summary>
    /// 序列化 null 抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Serialize_WhenObjectNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            JsonHelper.Serialize<JsonSampleUser?>(null);
        });
    }

    /// <summary>
    /// 严格选项不改写属性名，保持源码中的帕斯卡命名
    /// </summary>
    [Fact]
    public void Serialize_WithStrictOptions_KeepsOriginalPropertyNames()
    {
        var json = JsonHelper.Serialize(CreateSampleUser(), JsonSerializeOptions.Strict);

        Assert.Contains("\"Name\"", json);
        Assert.Contains("\"IsActive\"", json);
        Assert.DoesNotContain("\"isActive\"", json);
    }

    /// <summary>
    /// 自定义蛇形命名策略在写入与读取两侧都生效
    /// </summary>
    [Fact]
    public void SerializeDeserialize_WithSnakeCasePolicy_RoundTrips()
    {
        var serializeOptions = new JsonSerializeOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        var deserializeOptions = new JsonDeserializeOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var json = JsonHelper.Serialize(CreateSampleUser(), serializeOptions);
        Assert.Contains("\"is_active\"", json);

        var restored = JsonHelper.Deserialize<JsonSampleUser>(json, deserializeOptions);
        Assert.True(restored.IsActive);
        Assert.Equal("曦寒", restored.Name);
    }

    /// <summary>
    /// 紧凑选项忽略只读属性，默认选项则保留只读属性
    /// </summary>
    [Fact]
    public void Serialize_IgnoreReadOnlyProperties_ControlsComputedMember()
    {
        var holder = new JsonSampleReadOnlyHolder();

        var kept = JsonHelper.Serialize(holder);
        var dropped = JsonHelper.Serialize(holder, JsonSerializeOptions.Compact);

        Assert.Contains("\"computed\"", kept);
        Assert.DoesNotContain("\"computed\"", dropped);
        Assert.Contains("\"writable\"", dropped);
    }

    /// <summary>
    /// 指定保守编码器时中文被转义，但语义仍可无损还原
    /// </summary>
    [Fact]
    public void Serialize_WithDefaultEncoder_EscapesNonAsciiButRoundTrips()
    {
        var options = new JsonSerializeOptions
        {
            WriteIndented = false,
            Encoder = JavaScriptEncoder.Default
        };

        var json = JsonHelper.Serialize(CreateSampleUser(), options);

        Assert.DoesNotContain("曦寒", json);
        Assert.Contains("\\u", json);

        var restored = JsonHelper.Deserialize<JsonSampleUser>(json);
        Assert.Equal("曦寒", restored.Name);
    }

    /// <summary>
    /// 默认选项下序列化再反序列化，所有字段值保持一致
    /// </summary>
    [Fact]
    public void SerializeDeserialize_RoundTrip_PreservesAllValues()
    {
        var source = CreateSampleUser();

        var restored = JsonHelper.Deserialize<JsonSampleUser>(JsonHelper.Serialize(source));

        Assert.Equal(source.Name, restored.Name);
        Assert.Equal(source.Age, restored.Age);
        Assert.Equal(source.IsActive, restored.IsActive);
        Assert.Null(restored.Nickname);
        Assert.Equal(source.Tags, restored.Tags);
        Assert.NotNull(restored.Address);
        Assert.Equal("上海", restored.Address!.City);
        Assert.Equal("中国", restored.Address.Country);
    }

    /// <summary>
    /// 空白 JSON 字符串抛出 ArgumentException
    /// </summary>
    /// <param name="json">待反序列化的字符串</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Deserialize_WhenJsonBlank_ThrowsArgumentException(string json)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            JsonHelper.Deserialize<JsonSampleUser>(json);
        });

        Assert.Contains("JSON 字符串不能为空", exception.Message);
    }

    /// <summary>
    /// 非法 JSON 在默认错误策略下抛出 InvalidOperationException
    /// </summary>
    [Fact]
    public void Deserialize_WhenJsonInvalid_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            JsonHelper.Deserialize<JsonSampleUser>("{\"name\": ");
        });

        Assert.Contains("反序列化失败", exception.Message);
    }

    /// <summary>
    /// 错误策略为 UseDefault 时，非法 JSON 返回默认值而不抛异常
    /// </summary>
    [Fact]
    public void Deserialize_WhenErrorHandlingUseDefault_ReturnsDefault()
    {
        var options = new JsonDeserializeOptions { ErrorHandling = JsonErrorHandling.UseDefault };

        var user = JsonHelper.Deserialize<JsonSampleUser>("{不是 JSON", options);

        Assert.Null(user);
    }

    /// <summary>
    /// 错误策略为 Ignore 时，非法 JSON 同样返回默认值
    /// </summary>
    [Fact]
    public void Deserialize_WhenErrorHandlingIgnore_ReturnsDefault()
    {
        var options = new JsonDeserializeOptions { ErrorHandling = JsonErrorHandling.Ignore };

        var user = JsonHelper.Deserialize<JsonSampleUser>("[[[", options);

        Assert.Null(user);
    }

    /// <summary>
    /// 合法但结果为 null 的 JSON 字面量抛出"结果为空"异常
    /// </summary>
    [Fact]
    public void Deserialize_WhenJsonIsNullLiteral_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            JsonHelper.Deserialize<JsonSampleUser>("null");
        });

        Assert.Contains("结果为空", exception.Message);
    }

    /// <summary>
    /// 关闭格式校验后，尾随逗号与注释可被容错解析
    /// </summary>
    /// <remarks>
    /// ValidateJson 走的是默认 JsonDocumentOptions，不认尾随逗号与注释，
    /// 所以这些容错开关必须配合 ValidateJson = false 才能生效。
    /// </remarks>
    [Fact]
    public void Deserialize_WhenValidationDisabled_AcceptsTrailingCommaAndComment()
    {
        var options = new JsonDeserializeOptions
        {
            ValidateJson = false,
            AllowTrailingCommas = true,
            ReadCommentHandling = true
        };

        var user = JsonHelper.Deserialize<JsonSampleUser>("{ \"name\": \"曦寒\", /* 注释 */ \"age\": 18, }", options);

        Assert.Equal("曦寒", user.Name);
        Assert.Equal(18, user.Age);
    }

    /// <summary>
    /// 默认大小写不敏感，属性名大小写不同也能匹配
    /// </summary>
    [Fact]
    public void Deserialize_WhenCaseInsensitive_MatchesDifferentCasing()
    {
        var user = JsonHelper.Deserialize<JsonSampleUser>("{\"NAME\":\"曦寒\",\"AGE\":18}");

        Assert.Equal("曦寒", user.Name);
        Assert.Equal(18, user.Age);
    }

    /// <summary>
    /// 严格选项大小写敏感，大小写不匹配的属性保持默认值
    /// </summary>
    [Fact]
    public void Deserialize_WithStrictOptions_IsCaseSensitive()
    {
        var user = JsonHelper.Deserialize<JsonSampleUser>("{\"NAME\":\"曦寒\",\"age\":18}", JsonDeserializeOptions.Strict);

        Assert.Equal(string.Empty, user.Name);
        Assert.Equal(18, user.Age);
    }

    /// <summary>
    /// 默认数字处理允许从字符串读取数字
    /// </summary>
    [Fact]
    public void Deserialize_WithDefaultNumberHandling_ReadsNumberFromString()
    {
        var user = JsonHelper.Deserialize<JsonSampleUser>("{\"name\":\"曦寒\",\"age\":\"18\"}");

        Assert.Equal(18, user.Age);
    }

    /// <summary>
    /// 严格数字处理拒绝字符串形式的数字
    /// </summary>
    [Fact]
    public void Deserialize_WithStrictNumberHandling_RejectsStringNumber()
    {
        var options = new JsonDeserializeOptions { NumberHandling = JsonNumberHandling.Strict };

        Assert.Throws<InvalidOperationException>(() =>
        {
            JsonHelper.Deserialize<JsonSampleUser>("{\"name\":\"曦寒\",\"age\":\"18\"}", options);
        });
    }

    /// <summary>
    /// TrySerialize 遇到 null 返回 false 且不产出结果
    /// </summary>
    [Fact]
    public void TrySerialize_WhenObjectNull_ReturnsFalse()
    {
        var succeeded = JsonHelper.TrySerialize<JsonSampleUser?>(null, out var json);

        Assert.False(succeeded);
        Assert.Null(json);
    }

    /// <summary>
    /// TrySerialize 正常对象返回 true 且产出可解析的 JSON
    /// </summary>
    [Fact]
    public void TrySerialize_WithValidObject_ReturnsTrueAndParsableJson()
    {
        var succeeded = JsonHelper.TrySerialize(CreateSampleUser(), out var json);

        Assert.True(succeeded);
        Assert.NotNull(json);
        Assert.True(JsonHelper.IsValidJson(json!));
    }

    /// <summary>
    /// TryDeserialize 遇到非法 JSON 返回 false
    /// </summary>
    [Fact]
    public void TryDeserialize_WhenJsonInvalid_ReturnsFalse()
    {
        var succeeded = JsonHelper.TryDeserialize<JsonSampleUser>("{\"name\":", out var user);

        Assert.False(succeeded);
        Assert.Null(user);
    }

    /// <summary>
    /// TryDeserialize 遇到空白字符串返回 false
    /// </summary>
    [Fact]
    public void TryDeserialize_WhenJsonBlank_ReturnsFalse()
    {
        var succeeded = JsonHelper.TryDeserialize<JsonSampleUser>("   ", out var user);

        Assert.False(succeeded);
        Assert.Null(user);
    }

    /// <summary>
    /// 循环引用的对象图序列化时抛出 InvalidOperationException
    /// </summary>
    [Fact]
    public void Serialize_WhenObjectGraphHasCycle_ThrowsInvalidOperationException()
    {
        var node = new JsonSampleNode { Name = "根" };
        node.Next = node;

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            JsonHelper.Serialize(node);
        });

        Assert.Contains("序列化失败", exception.Message);
    }

    /// <summary>
    /// 循环引用下 TrySerialize 返回 false 而不抛异常
    /// </summary>
    [Fact]
    public void TrySerialize_WhenObjectGraphHasCycle_ReturnsFalse()
    {
        var node = new JsonSampleNode { Name = "根" };
        node.Next = node;

        var succeeded = JsonHelper.TrySerialize(node, out var json);

        Assert.False(succeeded);
        Assert.Null(json);
    }

    /// <summary>
    /// 嵌套层级超过 MaxDepth 时序列化失败
    /// </summary>
    [Fact]
    public void Serialize_WhenDepthExceedsMaxDepth_Throws()
    {
        var head = BuildChain(12);
        var options = new JsonSerializeOptions { MaxDepth = 3 };

        Assert.Throws<InvalidOperationException>(() =>
        {
            JsonHelper.Serialize(head, options);
        });
    }

    /// <summary>
    /// 嵌套层级未超过 MaxDepth 时序列化成功
    /// </summary>
    [Fact]
    public void Serialize_WhenDepthWithinMaxDepth_Succeeds()
    {
        var head = BuildChain(2);
        var options = new JsonSerializeOptions { MaxDepth = 8, WriteIndented = false };

        var json = JsonHelper.Serialize(head, options);

        Assert.Contains("\"next\"", json);
    }

    /// <summary>
    /// 反序列化时嵌套层级超过 MaxDepth 抛出异常
    /// </summary>
    [Fact]
    public void Deserialize_WhenDepthExceedsMaxDepth_Throws()
    {
        var json = JsonHelper.Serialize(BuildChain(12), new JsonSerializeOptions { WriteIndented = false });
        var options = new JsonDeserializeOptions { MaxDepth = 3 };

        Assert.Throws<InvalidOperationException>(() =>
        {
            JsonHelper.Deserialize<JsonSampleNode>(json, options);
        });
    }

    /// <summary>
    /// 深层嵌套在默认深度上限内可完整往返
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void SerializeDeserialize_WithDeepNesting_RoundTrips()
    {
        const int Depth = 40;
        var json = JsonHelper.Serialize(BuildChain(Depth), new JsonSerializeOptions { WriteIndented = false });

        var restored = JsonHelper.Deserialize<JsonSampleNode>(json);

        var count = 0;
        var current = restored;
        while (current is not null)
        {
            count++;
            current = current.Next;
        }

        Assert.Equal(Depth, count);
    }

    /// <summary>
    /// 引号、反斜杠、换行、制表符、Emoji 与中文标点均可无损往返
    /// </summary>
    [Fact]
    public void SerializeDeserialize_WithSpecialCharacters_PreservesContent()
    {
        var source = new JsonSampleText
        {
            Content = "引号\"反斜杠\\换行\n制表\t中文：曦寒；标点，测试 🚀"
        };

        var restored = JsonHelper.Deserialize<JsonSampleText>(JsonHelper.Serialize(source));

        Assert.Equal(source.Content, restored.Content);
    }

    /// <summary>
    /// 构造指定长度的链式对象
    /// </summary>
    /// <param name="length">节点数量</param>
    private static JsonSampleNode BuildChain(int length)
    {
        var head = new JsonSampleNode { Name = "节点0" };
        var current = head;
        for (var i = 1; i < length; i++)
        {
            var next = new JsonSampleNode { Name = $"节点{i}" };
            current.Next = next;
            current = next;
        }

        return head;
    }
}
