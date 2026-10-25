// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// JSON 值的转换规则与变量包装。
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
        Assert.Equal("2.50", WorkflowValues.AsString(JsonValue.Create(2.50m)));
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
        Assert.Equal(1, variables.Get<JsonObject>("p")!["n"]!.GetValue<int>());

        variables.Merge(new JsonObject { ["a"] = 1, ["b"] = "x" });
        Assert.Equal(["p", "a", "b"], variables.Names);
        Assert.True(variables.TryGet<int>("a", out var a) && a == 1);
        Assert.False(variables.TryGet<int>("b", out _));
        Assert.Equal("x", variables.Get<string>("b"));
        Assert.True(variables.Contains("a") && !variables.Contains("zz"));
        Assert.True(variables.Remove("a") && !variables.Remove("a"));

        var snapshot = variables.Snapshot();
        snapshot["b"] = "changed";
        Assert.Equal("x", backing["b"]!.GetValue<string>());
        Assert.Same(backing, variables.AsJsonObject());
        Assert.Throws<ArgumentException>(() => variables.Set(" ", null));
    }
}
