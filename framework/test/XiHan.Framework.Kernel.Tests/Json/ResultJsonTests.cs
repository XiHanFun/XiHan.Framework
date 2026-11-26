// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Kernel.Json;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Json;

/// <summary>
/// Result 与 Error 线上编码的读写语义
/// </summary>
/// <remarks>
/// 向量只判定读得出与拒绝（<see cref="WireEncodingVectorTests"/>），这里核对读出的值、拒绝时的错误与写出一侧的约束（ADR-0244）
/// </remarks>
public sealed class ResultJsonTests
{
    [Fact]
    public void ReadError_KeepsKeyTextDetailsAndInner()
    {
        var read = ResultJson.ReadError("""{"code":"PERSISTENCE_CONFLICT_001","message":"Order 42 was changed.","messageKey":"persistence.entity.stale_stamp","details":{"id":42,"ratio":2.0,"big":9007199254740993},"inner":{"code":"KERNEL_UNEXPECTED_001","message":"Stamp mismatch."}}""");

        var error = read.ValueOrThrow();
        Assert.Equal("PERSISTENCE_CONFLICT_001", error.Code);
        Assert.Equal("persistence.entity.stale_stamp", error.MessageKey);
        Assert.Equal("Order 42 was changed.", error.Text);
        Assert.Equal(42L, error.Details!["id"]);
        Assert.Equal(2.0d, error.Details["ratio"]);
        Assert.Equal(9007199254740993L, error.Details["big"]);
        Assert.Null(error.Inner!.MessageKey);
        Assert.Equal("Stamp mismatch.", error.Inner.Message);
    }

    [Fact]
    public void ReadError_TreatsEmptyDetailsAsNone()
    {
        var error = ResultJson.ReadError("""{"code":"KERNEL_CANCELLED_001","message":"x","details":{}}""").ValueOrThrow();

        Assert.Null(error.Details);
    }

    [Fact]
    public void Read_ReturnsThePeerFailureAsTheInnerResult()
    {
        var read = ResultJson.Read("""{"ok":false,"error":{"code":"PLUGINS_UNEXPECTED_001","message":"Plugin failed."}}""");

        var result = read.ValueOrThrow();
        Assert.True(result.IsFailure);
        Assert.Equal("PLUGINS_UNEXPECTED_001", result.Error!.Code);
    }

    [Fact]
    public void ReadMaybe_ReadsNullAsNone()
    {
        var none = ResultJson.ReadMaybe("""{"ok":true,"value":null}""", VectorJsonContext.Default.VectorOrder).ValueOrThrow();
        var some = ResultJson.ReadMaybe("""{"ok":true,"value":{"lines":[{"quantity":2}]}}""", VectorJsonContext.Default.VectorOrder).ValueOrThrow();

        Assert.False(none.ValueOrThrow().HasValue);
        Assert.Equal(2, some.ValueOrThrow().ValueOrThrow().Lines.Single().Quantity);
    }

    [Theory]
    [InlineData("""{"ok":"true"}""", "$.ok")]
    [InlineData("""{"value":1}""", "$.ok")]
    [InlineData("""{"ok":true,"value":{}}""", "$.value")]
    [InlineData("""{"ok":false,"value":1,"error":{"code":"KERNEL_UNEXPECTED_001","message":"x"}}""", "$.value")]
    [InlineData("""{"ok":true,"error":{"code":"KERNEL_UNEXPECTED_001","message":"x"}}""", "$.error")]
    [InlineData("""{"ok":false,"error":null}""", "$.error")]
    [InlineData("""{"ok":false,"error":{"code":"KERNEL_UNEXPECTED_001","message":"x","inner":{"code":"bad","message":"y"}}}""", "$.error.inner.code")]
    [InlineData("""{"ok":false,"error":{"code":"KERNEL_UNEXPECTED_001","message":"x","messageKey":null}}""", "$.error.messageKey")]
    [InlineData("[true]", "$")]
    public void Read_ReportsThePathOfTheFirstRuleItBreaks(string json, string reason)
    {
        var error = ResultJson.Read(json).Error;

        Assert.NotNull(error);
        Assert.Equal(ErrorCode.InvalidArgument, error.Code);
        Assert.Equal(KernelMessages.WireInvalid, error.MessageKey);
        Assert.Equal(reason, error.Details!["reason"]);
    }

    [Theory]
    [InlineData("""{"ok":true,"value":1""", "$")]
    [InlineData("""{"ok":true,"ok":true}""", "$.ok")]
    [InlineData("""{"ok":false,"error":{"code":"KERNEL_UNEXPECTED_001","message":"x","details":{"a":{"b":1,"b":2}}}}""", "$.error.details.a.b")]
    public void Read_ReportsTextProblemsAsInvalidJson(string json, string reason)
    {
        var error = ResultJson.Read(json).Error;

        Assert.NotNull(error);
        Assert.Equal(KernelMessages.WireInvalidJson, error.MessageKey);
        Assert.Equal(reason, error.Details!["reason"]);
    }

    [Fact]
    public void Read_RejectsALoneSurrogateInTheText()
    {
        var error = ResultJson.Read("{\"ok\":true,\"value\":\"\uD800\"}", VectorJsonContext.Default.JsonElement).Error;

        Assert.Equal(KernelMessages.WireInvalidJson, error!.MessageKey);
    }

    [Fact]
    public void Read_ReportsTheValuePathWhenTheValueDoesNotMatchItsType()
    {
        var error = ResultJson.Read("""{"ok":true,"value":{"lines":[{"quantity":"two"}]}}""", VectorJsonContext.Default.VectorOrder).Error;

        Assert.Equal(KernelMessages.WireInvalid, error!.MessageKey);
        Assert.Equal("$.value.lines[0].quantity", error.Details!["reason"]);
    }

    [Fact]
    public void ReadError_RejectsAWhitespaceMessage()
    {
        var error = ResultJson.ReadError("""{"code":"KERNEL_UNEXPECTED_001","message":"  "}""").Error;

        Assert.Equal("$.message", error!.Details!["reason"]);
    }

    [Fact]
    public void Write_UsesTheSpecifiedMemberOrder()
    {
        var inner = Error.Literal("KERNEL_UNEXPECTED_001", "Stamp mismatch.");
        var error = new Error("PERSISTENCE_CONFLICT_001", "persistence.entity.stale_stamp", MessageArguments.Of(("id", 42), ("ratio", 2.0)), inner).WithText("Order 42 was changed.");

        var written = ResultJson.Write(Result.Failure(error));

        Assert.Equal("""{"ok":false,"error":{"code":"PERSISTENCE_CONFLICT_001","message":"Order 42 was changed.","messageKey":"persistence.entity.stale_stamp","details":{"id":42,"ratio":2.0},"inner":{"code":"KERNEL_UNEXPECTED_001","message":"Stamp mismatch."}}}""", written);
    }

    [Fact]
    public void Write_WritesAUnitSuccessWithoutValue()
    {
        Assert.Equal("""{"ok":true}""", ResultJson.Write(Result.Success()));
    }

    [Fact]
    public void Write_WritesTheDefaultResultAsUninitialized()
    {
        var read = ResultJson.Read(ResultJson.Write(default(Result))).ValueOrThrow();

        Assert.Equal(ErrorCode.UninitializedResult, read.Error!.Code);
    }

    [Fact]
    public void WriteMaybe_WritesNoneAsNull()
    {
        var written = ResultJson.WriteMaybe(Result.Success(Maybe.None<VectorOrder>()), VectorJsonContext.Default.VectorOrder);

        Assert.Equal("""{"ok":true,"value":null}""", written);
    }

    [Fact]
    public void Write_RejectsAValueWrittenAsNull()
    {
        using var document = JsonDocument.Parse("null");
        var value = document.RootElement.Clone();

        Assert.Throws<ArgumentException>(() => ResultJson.Write(Result.Success(value), VectorJsonContext.Default.JsonElement));
        Assert.Throws<ArgumentException>(() => ResultJson.WriteMaybe(Result.Success(Maybe.Some(value)), VectorJsonContext.Default.JsonElement));
    }

    [Fact]
    public void Write_AnErrorWithALoneSurrogate_CannotBeWritten()
    {
        // 写出与转交（RFC-0001，ADR-0320）：修之前写出器把孤立的代理项换成转义的 U+FFFD，对方读到的是另一个错误
        // 不用理论数据：理论数据经序列化交给测试时孤立代理项已被换掉
        Error[] errors =
        [
            Error.Literal("KERNEL_UNEXPECTED_001", "Stamp \uD800 mismatch."),
            Error.Literal("KERNEL_UNEXPECTED_001", "Stamp mismatch.", MessageArguments.Of(("id", "a\uDC00"))),
            Error.Literal("KERNEL_UNEXPECTED_001", "Stamp mismatch.", MessageArguments.Of(("i\uD83D", 1))),
            Error.Literal("KERNEL_UNEXPECTED_001", "Stamp mismatch.", MessageArguments.Of(("ids", new List<object?> { "a", '\uDBFF' }))),
            Error.Literal("KERNEL_UNEXPECTED_001", "Stamp mismatch.", MessageArguments.Of(("nested", new Dictionary<string, object?> { ["\uDFFF"] = 1 }))),
            Error.Literal("PERSISTENCE_CONFLICT_001", "Order changed.", null, Error.Literal("KERNEL_UNEXPECTED_001", "\uD800")),
        ];
        foreach (var error in errors)
        {
            Assert.Throws<ArgumentException>(() => ResultJson.Write(Result.Failure(error)));
            Assert.Throws<ArgumentException>(() => ResultJson.Write(Result.Failure<JsonElement>(error), VectorJsonContext.Default.JsonElement));
            Assert.Throws<ArgumentException>(() => ResultJson.WriteMaybe(Result.Failure<Maybe<JsonElement>>(error), VectorJsonContext.Default.JsonElement));
            Assert.Throws<ArgumentException>(() => ResultJson.WriteError(error));
        }
    }

    [Fact]
    public void Write_AValueWithALoneSurrogate_CannotBeWritten()
    {
        // 成功值按调用方给的元数据写出，同样不换成 U+FFFD
        var value = new Dictionary<string, int> { ["a\uD800"] = 1 };

        Assert.Throws<ArgumentException>(() => ResultJson.Write(Result.Success(value), VectorJsonContext.Default.DictionaryStringInt32));
        Assert.Throws<ArgumentException>(() => ResultJson.WriteMaybe(Result.Success(Maybe.Some(value)), VectorJsonContext.Default.DictionaryStringInt32));
    }

    [Fact]
    public void Write_TheReplacementCharacterAndSurrogatePairs_ReadBackUnchanged()
    {
        // 本来就是 U+FFFD 的字符与代理对照常写出：缺省编码器把它们写成转义，读回来是同一个字符串
        var error = Error.Literal("KERNEL_UNEXPECTED_001", "\uFFFD \uD83D\uDE00", MessageArguments.Of(("text", "\uFFFD"), ("\uFFFD", "\uD83D\uDE00")));
        var value = new Dictionary<string, int> { ["\uFFFD\uD83D\uDE00"] = 1 };

        var failure = ResultJson.Read(ResultJson.Write(Result.Failure(error))).ValueOrThrow().Error!;
        var success = ResultJson.Read(ResultJson.Write(Result.Success(value), VectorJsonContext.Default.DictionaryStringInt32), VectorJsonContext.Default.DictionaryStringInt32).ValueOrThrow().ValueOrThrow();

        Assert.Equal("\uFFFD \uD83D\uDE00", failure.Message);
        Assert.Equal(("\uFFFD", "\uD83D\uDE00"), (failure.Details!["text"], failure.Details["\uFFFD"]));
        Assert.Equal(1, success["\uFFFD\uD83D\uDE00"]);
        Assert.Equal(error.Message, ResultJson.ReadError(ResultJson.WriteError(error)).ValueOrThrow().Message);
    }

    [Fact]
    public void Write_TextFailingTheCommonCheck_CannotBeWritten()
    {
        // 写出的整段文本过「JSON 文本」的共同检查，读取一侧都会拒绝：层数从结果的根算起（值在第 2 层），值或细节里原样带进来的重复成员也算
        using var deepest = JsonDocument.Parse(new string('[', 63) + new string(']', 63));
        using var tooDeep = JsonDocument.Parse(new string('[', 64) + new string(']', 64));
        using var duplicate = JsonDocument.Parse("""{"x":1,"x":2}""");

        var written = ResultJson.Write(Result.Success(deepest.RootElement.Clone()), VectorJsonContext.Default.JsonElement);

        Assert.True(ResultJson.Read(written, VectorJsonContext.Default.JsonElement).ValueOrThrow().IsSuccess);
        Assert.Throws<ArgumentException>(() => ResultJson.Write(Result.Success(tooDeep.RootElement.Clone()), VectorJsonContext.Default.JsonElement));
        Assert.Throws<ArgumentException>(() => ResultJson.Write(Result.Success(duplicate.RootElement.Clone()), VectorJsonContext.Default.JsonElement));
        Assert.Throws<ArgumentException>(() => ResultJson.WriteError(Error.Literal("KERNEL_UNEXPECTED_001", "x", MessageArguments.Of(("value", duplicate.RootElement.Clone())))));
    }
}
