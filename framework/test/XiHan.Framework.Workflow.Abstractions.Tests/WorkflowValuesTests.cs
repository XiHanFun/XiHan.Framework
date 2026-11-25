// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Workflow.Abstractions.Tasks;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// JSON 值的转换规则与变量包装
/// </summary>
public sealed class WorkflowValuesTests
{
    [Fact]
    public void TryConvert_FollowsScalarRules()
    {
        Assert.True(WorkflowValues.TryConvert<int>(JsonValue.Create(5), out var five) && five == 5);
        Assert.True(WorkflowValues.TryConvert<decimal>(JsonValue.Create("2.5"), out var half) && half == 2.5m);
        Assert.True(WorkflowValues.TryConvert<string>(JsonValue.Create(7), out var seven) && seven == "7");
        Assert.True(WorkflowValues.TryConvert<string>(JsonValue.Create(true), out var truth) && truth == "true");
        Assert.True(WorkflowValues.TryConvert<bool>(JsonValue.Create("true"), out var flag) && flag);
        Assert.False(WorkflowValues.TryConvert<int>(JsonValue.Create("abc"), out _));
        Assert.False(WorkflowValues.TryConvert<int>(JsonValue.Create(3_000_000_000L), out _));
        Assert.False(WorkflowValues.TryConvert<int>(null, out _));
        Assert.False(WorkflowValues.TryConvert<int>(new JsonObject(), out _));
        Assert.True(WorkflowValues.TryConvert<JsonObject>(new JsonObject { ["a"] = 1 }, out var obj) && obj!.Count == 1);
        Assert.False(WorkflowValues.TryConvert<JsonArray>(new JsonObject(), out _));
        Assert.True(WorkflowValues.TryConvert<UserTaskCompletionPolicy>(JsonValue.Create("sequential"), out var policy) && policy == UserTaskCompletionPolicy.Sequential);
        Assert.Equal(9, WorkflowValues.ConvertOr(JsonValue.Create("x"), 9));
    }

    [Theory]
    [InlineData("2026-01-02", "2026-01-02T00:00:00.0000000+00:00")]
    [InlineData("2026-01-02 03:04", "2026-01-02T03:04:00.0000000+00:00")]
    [InlineData("2026-01-02T03:04:05+08:00", "2026-01-01T19:04:05.0000000+00:00")]
    [InlineData("2026-01-02T03:04:05.1234567Z", "2026-01-02T03:04:05.1234567+00:00")]
    public void TryConvert_ReadsTimesByTheExpressionGrammar_AsUtc(string text, string expected)
    {
        // RFC-0021「活动里的时间」（ADR-0296）：与表达式的时间同一条文法，读出的时刻换算成 UTC
        var instant = DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(WorkflowValues.TryConvert<DateTimeOffset>(JsonValue.Create(text), out var when));
        Assert.Equal((instant, TimeSpan.Zero), (when, when.Offset));
        Assert.True(WorkflowValues.TryConvert<DateTime>(JsonValue.Create(text), out var utc));
        Assert.Equal((instant.UtcDateTime, DateTimeKind.Utc), (utc, utc.Kind));

        // 由 CLR 的时间构造、还没过存储的值按它的 JSON 文本读，结果相同
        Assert.True(WorkflowValues.TryConvert<DateTimeOffset>(JsonValue.Create(instant.ToOffset(TimeSpan.FromHours(-5))), out var clr));
        Assert.Equal(instant, clr);
    }

    [Theory]
    [InlineData("12:30")]
    [InlineData("01/02/2026")]
    [InlineData("Jan 2, 2026")]
    [InlineData("2026-1-2")]
    [InlineData("2026-01")]
    [InlineData(" 2026-01-02")]
    [InlineData("2026-01-02T03")]
    public void TryConvert_RejectsTimesTheGrammarDoesNotRead(string text)
    {
        // 以前交给平台的日期解析：只有时间的补上机器当天的日期，斜线日期、月份名、不补零的都读得出
        Assert.False(WorkflowValues.TryConvert<DateTimeOffset>(JsonValue.Create(text), out _));
        Assert.False(WorkflowValues.TryConvert<DateTime>(JsonValue.Create(text), out _));
        Assert.False(WorkflowValues.TryConvert<DateTimeOffset>(JsonValue.Create(20260102), out _));
    }

    [Fact]
    public void ToStringList_IsTruthy_AndAsString()
    {
        Assert.Equal(["a", "b"], WorkflowValues.ToStringList(new JsonArray("a", null, "b")));
        Assert.Equal(["a", "b", "c"], WorkflowValues.ToStringList(JsonValue.Create(" a, b ,,c ")));
        Assert.Equal(["7"], WorkflowValues.ToStringList(JsonValue.Create(7)));
        Assert.Empty(WorkflowValues.ToStringList(null));
        Assert.Empty(WorkflowValues.ToStringList(new JsonObject()));

        Assert.True(WorkflowValues.IsTruthy(JsonValue.Create(1)) && WorkflowValues.IsTruthy(JsonValue.Create("x")) && WorkflowValues.IsTruthy(new JsonArray(1)));
        Assert.False(WorkflowValues.IsTruthy(null) || WorkflowValues.IsTruthy(JsonValue.Create(0)) || WorkflowValues.IsTruthy(JsonValue.Create("")) || WorkflowValues.IsTruthy(JsonValue.Create(false)) || WorkflowValues.IsTruthy(new JsonObject()));
        // 数字的文本只取决于数值，不带 decimal 内部记着的结尾的零（ADR-0287）
        Assert.Equal("2.5", WorkflowValues.AsString(JsonValue.Create(2.50m)));
        Assert.Equal("false", WorkflowValues.AsString(JsonValue.Create(false)));
    }

    [Fact]
    public void Variables_StoreDeepCopies()
    {
        var backing = new JsonObject();
        var variables = new WorkflowVariables(backing);
        var payload = new JsonObject { ["n"] = 1 };
        variables.Set("p", payload);
        payload["n"] = 2;
        Assert.True(variables.TryGet<JsonObject>("p", out var p));
        Assert.Equal(1, p["n"]!.GetValue<int>());

        variables.Merge(new JsonObject { ["a"] = 1, ["b"] = "x" });
        Assert.Equal(["p", "a", "b"], variables.Names);
        Assert.True(variables.TryGet<int>("a", out var a) && a == 1);
        Assert.False(variables.TryGet<int>("b", out _));
        Assert.True(variables.TryGet<string>("b", out var b));
        Assert.Equal("x", b);
        Assert.True(variables.Contains("a") && !variables.Contains("zz"));
        Assert.True(variables.Remove("a") && !variables.Remove("a"));

        var snapshot = variables.Snapshot();
        snapshot["b"] = "changed";
        Assert.Equal("x", backing["b"]!.GetValue<string>());
        Assert.Same(backing, variables.AsJsonObject());
        Assert.Throws<ArgumentException>(() => variables.Set(" ", null));
    }
}
