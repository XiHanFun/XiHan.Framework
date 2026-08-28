// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Runtime;

/// <summary>
/// 工作流值转换器测试
/// </summary>
/// <remarks>
/// 该转换器是抽象层唯一有真实分支逻辑的类型，也是"变量经 JSON 持久化往返后仍能被活动正常读取"的唯一保证，
/// 因此按分支逐条覆盖：归一化的六种 JsonValueKind、目标类型转换的枚举/时间/GUID/可转换/JSON 兜底五条路径。
/// </remarks>
public class WorkflowValueConverterTests
{
    /// <summary>
    /// 非 JsonElement 的值原样返回
    /// </summary>
    [Fact]
    public void Normalize_WithNonJsonElement_ReturnsSameReference()
    {
        var value = new List<string> { "a" };

        Assert.Same(value, WorkflowValueConverter.Normalize(value));
        Assert.Equal("abc", WorkflowValueConverter.Normalize("abc"));
        Assert.Equal(42, WorkflowValueConverter.Normalize(42));
    }

    /// <summary>
    /// 空值原样返回空
    /// </summary>
    [Fact]
    public void Normalize_WithNull_ReturnsNull()
    {
        Assert.Null(WorkflowValueConverter.Normalize(null));
    }

    /// <summary>
    /// JSON null 与 undefined 归一化为空
    /// </summary>
    [Fact]
    public void Normalize_WithJsonNull_ReturnsNull()
    {
        Assert.Null(WorkflowValueConverter.Normalize(Parse("null")));
        Assert.Null(WorkflowValueConverter.Normalize(default(JsonElement)));
    }

    /// <summary>
    /// JSON 布尔归一化为原生布尔
    /// </summary>
    [Fact]
    public void Normalize_WithJsonBoolean_ReturnsNativeBoolean()
    {
        Assert.True(Assert.IsType<bool>(WorkflowValueConverter.Normalize(Parse("true"))));
        Assert.False(Assert.IsType<bool>(WorkflowValueConverter.Normalize(Parse("false"))));
    }

    /// <summary>
    /// JSON 字符串归一化为原生字符串
    /// </summary>
    [Fact]
    public void Normalize_WithJsonString_ReturnsNativeString()
    {
        Assert.Equal("张三", Assert.IsType<string>(WorkflowValueConverter.Normalize(Parse("\"张三\""))));
    }

    /// <summary>
    /// JSON 数值优先归一化为 decimal
    /// </summary>
    /// <remarks>
    /// 统一成 decimal 是为了让表达式求值里的金额比较不受二进制浮点误差影响。
    /// </remarks>
    [Fact]
    public void Normalize_WithJsonNumber_ReturnsDecimal()
    {
        Assert.Equal(42m, Assert.IsType<decimal>(WorkflowValueConverter.Normalize(Parse("42"))));
        Assert.Equal(-7m, Assert.IsType<decimal>(WorkflowValueConverter.Normalize(Parse("-7"))));
        Assert.Equal(3.5m, Assert.IsType<decimal>(WorkflowValueConverter.Normalize(Parse("3.5"))));
        Assert.Equal(0m, Assert.IsType<decimal>(WorkflowValueConverter.Normalize(Parse("0"))));
    }

    /// <summary>
    /// 超出 decimal 表示范围的数值退化为 double 而不是抛异常
    /// </summary>
    [Fact]
    public void Normalize_WithNumberBeyondDecimalRange_FallsBackToDouble()
    {
        var normalized = WorkflowValueConverter.Normalize(Parse("1e30"));

        Assert.Equal(1e30, Assert.IsType<double>(normalized));
    }

    /// <summary>
    /// JSON 数组归一化为可读列表且逐项递归
    /// </summary>
    [Fact]
    public void Normalize_WithJsonArray_ReturnsListWithNormalizedItems()
    {
        var list = Assert.IsType<List<object?>>(WorkflowValueConverter.Normalize(Parse("[1,\"a\",true,null]")));

        Assert.Equal(4, list.Count);
        Assert.Equal(1m, list[0]);
        Assert.Equal("a", list[1]);
        Assert.True(Assert.IsType<bool>(list[2]));
        Assert.Null(list[3]);
    }

    /// <summary>
    /// JSON 对象归一化为字典且递归处理嵌套结构
    /// </summary>
    [Fact]
    public void Normalize_WithNestedJsonObject_ReturnsNestedDictionaries()
    {
        var json = "{\"total\":99,\"customer\":{\"name\":\"李四\",\"tags\":[\"vip\",\"new\"]}}";

        var root = Assert.IsType<Dictionary<string, object?>>(WorkflowValueConverter.Normalize(Parse(json)));

        Assert.Equal(99m, root["total"]);
        var customer = Assert.IsType<Dictionary<string, object?>>(root["customer"]);
        Assert.Equal("李四", customer["name"]);
        var tags = Assert.IsType<List<object?>>(customer["tags"]);
        Assert.Equal(new object?[] { "vip", "new" }, tags);
    }

    /// <summary>
    /// 归一化保留 JSON 对象的键名大小写
    /// </summary>
    [Fact]
    public void Normalize_WithJsonObject_PreservesPropertyNameCasing()
    {
        var root = Assert.IsType<Dictionary<string, object?>>(WorkflowValueConverter.Normalize(Parse("{\"OrderNo\":\"A1\"}")));

        Assert.True(root.ContainsKey("OrderNo"));
        Assert.False(root.ContainsKey("orderNo"));
    }

    /// <summary>
    /// 目标类型与原值类型一致时原样返回同一引用
    /// </summary>
    [Fact]
    public void ConvertTo_WhenAlreadyTargetType_ReturnsSameReference()
    {
        var payload = new SamplePayload { Name = "a", Count = 1 };

        Assert.Same(payload, WorkflowValueConverter.ConvertTo<SamplePayload>(payload));
    }

    /// <summary>
    /// 数字字符串按可转换路径转为数值类型
    /// </summary>
    [Fact]
    public void ConvertTo_WithNumericString_UsesConvertible()
    {
        Assert.Equal(42, WorkflowValueConverter.ConvertTo<int>("42"));
        Assert.Equal(42L, WorkflowValueConverter.ConvertTo<long>("42"));
        Assert.Equal(3.5, WorkflowValueConverter.ConvertTo<double>("3.5"));
        Assert.Equal("42", WorkflowValueConverter.ConvertTo<string>(42));
    }

    /// <summary>
    /// 数字字符串按不变文化解析，不受机器区域设置影响
    /// </summary>
    /// <remarks>
    /// 显式验证小数点用的是"."：若实现漏掉 InvariantCulture，在小数点为逗号的区域会静默解析成整数。
    /// </remarks>
    [Fact]
    public void ConvertTo_WithDecimalString_UsesInvariantCulture()
    {
        Assert.Equal(1234.56m, WorkflowValueConverter.ConvertTo<decimal>("1234.56"));
    }

    /// <summary>
    /// 枚举名称字符串按忽略大小写解析
    /// </summary>
    [Theory]
    [InlineData("Completed")]
    [InlineData("completed")]
    [InlineData("COMPLETED")]
    public void ConvertTo_WithEnumName_ParsesIgnoringCase(string text)
    {
        Assert.Equal(WorkflowInstanceStatus.Completed, WorkflowValueConverter.ConvertTo<WorkflowInstanceStatus>(text));
    }

    /// <summary>
    /// 枚举数值按底层值还原
    /// </summary>
    [Fact]
    public void ConvertTo_WithEnumNumericValue_ConvertsByUnderlyingValue()
    {
        Assert.Equal(WorkflowInstanceStatus.Faulted, WorkflowValueConverter.ConvertTo<WorkflowInstanceStatus>(5));
        Assert.Equal(WorkflowInstanceStatus.Running, WorkflowValueConverter.ConvertTo<WorkflowInstanceStatus>(1L));
    }

    /// <summary>
    /// 非法枚举名称抛出参数异常
    /// </summary>
    [Fact]
    public void ConvertTo_WithUnknownEnumName_Throws()
    {
        Assert.Throws<ArgumentException>(() => WorkflowValueConverter.ConvertTo<WorkflowInstanceStatus>("NotAStatus"));
    }

    /// <summary>
    /// 时间跨度字符串按不变文化解析
    /// </summary>
    [Fact]
    public void ConvertTo_WithTimeSpanString_ParsesAsTimeSpan()
    {
        Assert.Equal(TimeSpan.FromMinutes(90), WorkflowValueConverter.ConvertTo<TimeSpan>("01:30:00"));
    }

    /// <summary>
    /// 时间跨度数值按秒解释
    /// </summary>
    /// <remarks>
    /// 这是流程定义里 delaySeconds 之类属性能直接绑定到 TimeSpan 的关键约定，语义改成毫秒会静默拉长等待。
    /// </remarks>
    [Fact]
    public void ConvertTo_WithTimeSpanNumber_TreatsValueAsSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(90), WorkflowValueConverter.ConvertTo<TimeSpan>(90));
        Assert.Equal(TimeSpan.FromSeconds(1.5), WorkflowValueConverter.ConvertTo<TimeSpan>(1.5));
    }

    /// <summary>
    /// 时间字符串按往返格式解析
    /// </summary>
    [Fact]
    public void ConvertTo_WithDateTimeString_ParsesRoundtripFormat()
    {
        Assert.Equal(new DateTime(2024, 1, 2, 3, 4, 5), WorkflowValueConverter.ConvertTo<DateTime>("2024-01-02T03:04:05"));
    }

    /// <summary>
    /// GUID 字符串按标准格式解析
    /// </summary>
    [Fact]
    public void ConvertTo_WithGuidString_ParsesAsGuid()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, WorkflowValueConverter.ConvertTo<Guid>(id.ToString()));
    }

    /// <summary>
    /// JsonElement 按目标类型直接反序列化，支持复杂对象绑定
    /// </summary>
    [Fact]
    public void ConvertTo_WithJsonElementObject_DeserializesToTargetType()
    {
        var payload = WorkflowValueConverter.ConvertTo<SamplePayload>(Parse("{\"name\":\"张三\",\"count\":3}"));

        Assert.NotNull(payload);
        Assert.Equal("张三", payload.Name);
        Assert.Equal(3, payload.Count);
    }

    /// <summary>
    /// JsonElement 绑定使用 Web 口径，属性名大小写不敏感
    /// </summary>
    [Fact]
    public void ConvertTo_WithPascalCasedJsonElement_BindsCaseInsensitively()
    {
        var payload = WorkflowValueConverter.ConvertTo<SamplePayload>(Parse("{\"Name\":\"李四\",\"Count\":7}"));

        Assert.NotNull(payload);
        Assert.Equal("李四", payload.Name);
        Assert.Equal(7, payload.Count);
    }

    /// <summary>
    /// JSON null 的 JsonElement 转换为目标类型时返回空
    /// </summary>
    [Fact]
    public void ConvertTo_WithJsonNullElement_ReturnsNull()
    {
        Assert.Null(WorkflowValueConverter.ConvertTo<SamplePayload>(Parse("null")));
        Assert.Null(WorkflowValueConverter.ConvertTo(Parse("null"), typeof(SamplePayload)));
    }

    /// <summary>
    /// 归一化后的字典可经 JSON 兜底路径绑定到 POCO
    /// </summary>
    /// <remarks>
    /// 这是"变量往返一次后仍能绑定到强类型"的最后一道保障：字典既不是目标类型也不是 IConvertible，
    /// 必须走序列化再反序列化的兜底分支。
    /// </remarks>
    [Fact]
    public void ConvertTo_WithNormalizedDictionary_FallsBackToJsonRoundTrip()
    {
        var dictionary = new Dictionary<string, object?> { ["name"] = "王五", ["count"] = 9 };

        var payload = WorkflowValueConverter.ConvertTo<SamplePayload>(dictionary);

        Assert.NotNull(payload);
        Assert.Equal("王五", payload.Name);
        Assert.Equal(9, payload.Count);
    }

    /// <summary>
    /// 空值转换到引用类型返回空
    /// </summary>
    [Fact]
    public void ConvertTo_WhenValueNullAndTargetIsReferenceType_ReturnsNull()
    {
        Assert.Null(WorkflowValueConverter.ConvertTo<string>(null));
        Assert.Null(WorkflowValueConverter.ConvertTo<SamplePayload>(null));
    }

    /// <summary>
    /// 非泛型重载对空值恒返回空，与目标类型无关
    /// </summary>
    [Fact]
    public void ConvertTo_NonGenericWithNullValue_ReturnsNullForAnyTargetType()
    {
        Assert.Null(WorkflowValueConverter.ConvertTo(null, typeof(int)));
        Assert.Null(WorkflowValueConverter.ConvertTo(null, typeof(string)));
        Assert.Null(WorkflowValueConverter.ConvertTo(null, typeof(WorkflowInstanceStatus)));
    }

    /// <summary>
    /// 空值转换到可空值类型返回空
    /// </summary>
    [Fact]
    public void ConvertTo_WhenValueNullAndTargetIsNullableValueType_ReturnsNull()
    {
        Assert.Null(WorkflowValueConverter.ConvertTo<int?>(null));
        Assert.Null(WorkflowValueConverter.ConvertTo<DateTime?>(null));
    }

    /// <summary>
    /// 空值转换到非空值类型应返回该类型默认值
    /// </summary>
    /// <remarks>
    /// 疑似缺陷：<c>ConvertTo&lt;T&gt;</c> 内部对非空值类型 T 执行 <c>(T?)null</c> 拆箱，会抛 NullReferenceException；
    /// 而方法自身的文档契约写的是"原始值为空时返回默认值"。此处按文档语义断言，不迁就当前实现。
    /// 触发路径很现实：变量被显式置为 null 后 <c>WorkflowVariables.Get&lt;int&gt;</c> 即命中。
    /// </remarks>
    [Fact]
    public void ConvertTo_WhenValueNullAndTargetIsValueType_ReturnsDefault()
    {
        Assert.Equal(0, WorkflowValueConverter.ConvertTo<int>(null));
        Assert.False(WorkflowValueConverter.ConvertTo<bool>(null));
    }

    /// <summary>
    /// 无法解析的数值字符串抛出格式异常
    /// </summary>
    [Fact]
    public void ConvertTo_WithUnparsableNumericString_Throws()
    {
        Assert.Throws<FormatException>(() => WorkflowValueConverter.ConvertTo<int>("abc"));
    }

    /// <summary>
    /// 解析 JSON 文本为 JsonElement
    /// </summary>
    /// <param name="json">JSON 文本</param>
    /// <returns>JsonElement</returns>
    private static JsonElement Parse(string json)
    {
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    /// <summary>
    /// 复杂对象绑定用的示例载荷
    /// </summary>
    public sealed class SamplePayload
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 数量
        /// </summary>
        public int Count { get; set; }
    }
}
