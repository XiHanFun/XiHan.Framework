// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Chat;

namespace XiHan.Framework.AI.Abstractions.Tests.Chat;

/// <summary>
/// XiHan 会话选项测试
/// </summary>
/// <remarks>
/// 这个类型的全部价值在于「组合而非继承」：它在原生 ChatOptions 之外只加一层 provider 选择，
/// 原生选项整体透传。一旦改成继承 ChatOptions，所有读 <c>options.ChatOptions</c> 的调用点都会静默拿到 null。
/// </remarks>
public class XiHanChatOptionsTests
{
    /// <summary>
    /// 新实例不指定 provider 也不携带原生选项
    /// </summary>
    /// <remarks>Provider 为 null 即「用默认 provider」，与 provider 解析器的约定一致。</remarks>
    [Fact]
    public void Defaults_WhenNewInstance_AreAllNull()
    {
        var options = new XiHanChatOptions();

        Assert.Null(options.Provider);
        Assert.Null(options.ChatOptions);
    }

    /// <summary>
    /// 原生选项以组合方式持有，而不是被继承或复制
    /// </summary>
    /// <remarks>
    /// 断言引用相同，是为了确认赋值时没有做防御性克隆——
    /// 若克隆，调用方在赋值后再改原生选项（如追加工具）就不会生效。
    /// </remarks>
    [Fact]
    public void ChatOptions_WhenAssigned_KeepsSameReference()
    {
        var native = new ChatOptions
        {
            ModelId = "gpt-4o-mini"
        };
        var options = new XiHanChatOptions
        {
            ChatOptions = native
        };

        Assert.Same(native, options.ChatOptions);
        Assert.Equal("gpt-4o-mini", options.ChatOptions!.ModelId);
    }

    /// <summary>
    /// 赋值后再改原生选项，改动对已持有的引用可见
    /// </summary>
    [Fact]
    public void ChatOptions_WhenMutatedAfterAssignment_ReflectsChange()
    {
        var native = new ChatOptions();
        var options = new XiHanChatOptions
        {
            ChatOptions = native
        };

        native.ModelId = "deepseek-chat";

        Assert.Equal("deepseek-chat", options.ChatOptions!.ModelId);
    }

    /// <summary>
    /// provider 名可设置也可清空回默认
    /// </summary>
    /// <param name="provider">provider 名</param>
    [Theory]
    [InlineData("openai")]
    [InlineData("OpenAI")]
    [InlineData("ollama")]
    public void Provider_WhenSet_RoundTripsAndCanBeCleared(string provider)
    {
        var options = new XiHanChatOptions
        {
            Provider = provider
        };

        Assert.Equal(provider, options.Provider);

        options.Provider = null;

        Assert.Null(options.Provider);
    }

    /// <summary>
    /// 类型不继承原生 ChatOptions，两者是组合关系
    /// </summary>
    /// <remarks>
    /// 这条是本类型的结构契约：XiHan 语义（provider 选择）与原生语义（温度/工具/模型）分层存放，
    /// 便于原生选项整体透传给 Microsoft.Extensions.AI 而不做字段级搬运。
    /// </remarks>
    [Fact]
    public void Type_ComposesNativeOptionsInsteadOfInheriting()
    {
        Assert.False(typeof(XiHanChatOptions).IsSubclassOf(typeof(ChatOptions)));
        Assert.Equal(typeof(ChatOptions), typeof(XiHanChatOptions).GetProperty(nameof(XiHanChatOptions.ChatOptions))!.PropertyType);
    }

    /// <summary>
    /// 两处 XiHan 语义与原生语义互不覆盖
    /// </summary>
    /// <remarks>指定 provider 不应影响原生选项里的模型覆盖，二者可同时生效。</remarks>
    [Fact]
    public void ProviderAndNativeModelId_CanBeSpecifiedTogether()
    {
        var options = new XiHanChatOptions
        {
            Provider = "custom",
            ChatOptions = new ChatOptions { ModelId = "qwen2.5:14b" }
        };

        Assert.Equal("custom", options.Provider);
        Assert.Equal("qwen2.5:14b", options.ChatOptions!.ModelId);
    }
}
