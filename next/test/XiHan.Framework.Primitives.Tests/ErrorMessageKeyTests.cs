// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Tests;

/// <summary>
/// 带消息键的错误：键校验、细节即参数、文本错误、换码与填入译文。
/// </summary>
public sealed class ErrorMessageKeyTests
{
    [Fact]
    public void KeyedError_CarriesKeyAndDetails()
    {
        var details = MessageArguments.Of(("entity", "Order"), ("id", 42));
        var error = Error.NotFound("ORDERS_NOTFOUND_001", "orders.order.not_found", details);

        Assert.Equal("orders.order.not_found", error.MessageKey);
        Assert.Null(error.Text);
        Assert.Same(details, error.Details);
        Assert.Equal("NOTFOUND", error.Kind);
    }

    [Fact]
    public void KeyedError_RejectsInvalidKeyOrKind()
    {
        Assert.Throws<ArgumentException>(static () => Error.NotFound("ORDERS_NOTFOUND_001", "Order was not found."));
        Assert.Throws<ArgumentException>(static () => new Error("ORDERS_NOTFOUND_001", "orders"));
        Assert.Throws<ArgumentException>(static () => Error.Conflict("ORDERS_NOTFOUND_001", "orders.order.not_found"));
        Assert.Throws<ArgumentException>(static () => Error.Literal("ORDERS_NOTFOUND_001", " "));
    }

    [Fact]
    public void LiteralError_HasTextButNoKey()
    {
        var error = Error.Literal("ORDERS_NOTFOUND_001", "The order does not exist.");

        Assert.Null(error.MessageKey);
        Assert.Equal("The order does not exist.", error.Text);
        Assert.Equal("The order does not exist.", error.Message);
        Assert.Equal("[ORDERS_NOTFOUND_001] The order does not exist.", error.ToString());
    }

    [Fact]
    public void WithCode_AndWithText_CopyEverythingElse()
    {
        var inner = Error.Literal("PERSISTENCE_UNAVAILABLE_001", "Database unavailable.");
        var error = Error.Unexpected("ORDERS_UNEXPECTED_001", "orders.order.failed", inner, MessageArguments.Of(("id", 1)));

        var recoded = error.WithCode("KERNEL_UNEXPECTED_001");
        Assert.Equal("KERNEL_UNEXPECTED_001", recoded.Code);
        Assert.Equal("orders.order.failed", recoded.MessageKey);
        Assert.Same(error.Details, recoded.Details);
        Assert.Same(inner, recoded.Inner);

        var resolved = error.WithText("订单 1 处理失败。");
        Assert.Equal("订单 1 处理失败。", resolved.Text);
        Assert.Equal("订单 1 处理失败。", resolved.Message);
        Assert.Equal("orders.order.failed", resolved.MessageKey);
        Assert.Throws<ArgumentException>(() => error.WithText(" "));
        Assert.Throws<ArgumentException>(() => error.WithCode("bad"));
    }

    [Fact]
    public void FrameworkException_ConvertsToLiteralError()
    {
        var exception = new FrameworkException("KERNEL_HOSTING_002", "Feature failed.", new FrameworkException("KERNEL_HOSTING_001", "Binding missing."));

        var error = exception.ToError();
        Assert.Null(error.MessageKey);
        Assert.Equal("Feature failed.", error.Message);
        Assert.Equal("Binding missing.", error.Inner!.Message);
    }
}
