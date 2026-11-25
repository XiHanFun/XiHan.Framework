// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 回放 <c>envelope-extensions.json</c>：扩展属性名的文法与 Baggage 的对应
/// </summary>
/// <remarks>
/// <c>messaging/envelope-extensions.json</c>（RFC-0009「信封」，<c>spec/conventions/baggage-keys.md</c> 的 CloudEvents 载体，ADR-0277）。名字的文法经 <see cref="EnvelopeExtensions.IsValidName"/> 与信封的扩展表回放；对应表除了与 <see cref="EnvelopeExtensions.BaggageMappings"/> 逐项比较，还经发布侧的补齐与派发侧的新调用各走一遍
/// </remarks>
public sealed class EnvelopeExtensionVectorTests
{
    /// <summary>
    /// 派发用的主题
    /// </summary>
    private const string Topic = "orders";

    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ExtensionVectors _vectors = SpecVectors.Read<ExtensionVectors>("messaging", "envelope-extensions.json");

    [Fact]
    public void IsValidName_AcceptsTheValidNames()
    {
        Assert.NotEmpty(_vectors.Names.Valid);
        foreach (var name in _vectors.Names.Valid)
        {
            Assert.True(EnvelopeExtensions.IsValidName(name), name);
            Assert.Equal("x", Envelope().WithExtension(name, "x").Extensions[name]);
        }
    }

    [Fact]
    public void IsValidName_RejectsTheInvalidNames()
    {
        Assert.NotEmpty(_vectors.Names.Invalid);
        foreach (var name in _vectors.Names.Invalid)
        {
            Assert.False(EnvelopeExtensions.IsValidName(name), name);
            Assert.Throws<ArgumentException>(() => Envelope().WithExtension(name, "x"));
        }
    }

    [Fact]
    public void ReservedNames_AreTheOnesInTheVectors_AndEveryEnvelopeRejectsThem()
    {
        // 保留名（RFC-0009「信封」，ADR-0298）：CloudEvents 的规范属性名与结构化 JSON 格式的成员名。以前文法收得下其中不带下划线的九个，
        // 信封照样接受，要到写出时才因重复的成员被拒，不写出的内存代理照常投递
        Assert.Equal(_vectors.Names.Reserved, EnvelopeExtensions.ReservedNames);
        foreach (var name in _vectors.Names.Reserved)
        {
            Assert.Contains(name, _vectors.Names.Invalid);
            Assert.False(EnvelopeExtensions.IsValidName(name), name);
            var appended = Assert.Throws<ArgumentException>(() => Envelope().WithExtension(name, "x"));
            Assert.StartsWith($"Extension attribute name '{name}' is ", appended.Message, StringComparison.Ordinal);
            Assert.Throws<ArgumentException>(() => Envelope() with { Extensions = new Dictionary<string, string>(StringComparer.Ordinal) { [name] = "x" } });
        }

        // 合文法的保留名给出的原因是「保留」，不是「不合文法」
        Assert.StartsWith("Extension attribute name 'id' is reserved", Assert.Throws<ArgumentException>(static () => Envelope().WithExtension("id", "x")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BaggageMappings_AreTheOnesInTheVectors()
    {
        Assert.NotEmpty(_vectors.Baggage);
        Assert.Equal(
            _vectors.Baggage.Select(static mapping => (mapping.Extension, mapping.BaggageKey)).OrderBy(static mapping => mapping.Extension, StringComparer.Ordinal),
            EnvelopeExtensions.BaggageMappings.OrderBy(static mapping => mapping.Extension, StringComparer.Ordinal));

        // 对应表两边都是已登记的名字：扩展名合文法，Baggage 键是登记过的键
        Assert.All(_vectors.Baggage, static mapping =>
        {
            Assert.True(EnvelopeExtensions.IsValidName(mapping.Extension), mapping.Extension);
            Assert.Contains(mapping.BaggageKey, BaggageKeys.Registered);
        });
    }

    [Fact]
    public void TraceAndPartitionKey_AreTheOnesInTheVectors_AndStayOutOfTheBaggage()
    {
        Assert.Equal(EnvelopeExtensions.TraceParent, _vectors.Trace);
        Assert.Equal(EnvelopeExtensions.PartitionKey, _vectors.PartitionKey);
        Assert.DoesNotContain(EnvelopeExtensions.BaggageMappings, static mapping => mapping.Extension is EnvelopeExtensions.TraceParent or EnvelopeExtensions.PartitionKey);
    }

    [Fact]
    public void KeysThatAreNotCarried_AreRegisteredBaggageKeys_WithoutAnExtension()
    {
        Assert.NotEmpty(_vectors.NotCarried);
        Assert.All(_vectors.NotCarried, static key =>
        {
            Assert.Contains(key, BaggageKeys.Registered);
            Assert.DoesNotContain(EnvelopeExtensions.BaggageMappings, mapping => mapping.BaggageKey == key);
        });

        // 登记过的键要么有对应的扩展，要么列在不进入消息的键里，没有第三种
        Assert.Equal(
            BaggageKeys.Registered.Order(StringComparer.Ordinal),
            _vectors.Baggage.Select(static mapping => mapping.BaggageKey).Concat(_vectors.NotCarried).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Publisher_FillsTheMappedExtensions_FromTheBaggageOfTheCurrentInvocation()
    {
        // 当前调用的 Baggage 带着全部登记过的键，含不进入消息的
        var baggage = _vectors.Baggage.Select(static mapping => mapping.BaggageKey).Concat(_vectors.NotCarried)
            .Select(static (key, index) => KeyValuePair.Create(key, $"value-{index}"))
            .ToDictionary(StringComparer.Ordinal);
        var context = TestInvocationContext.Create(baggage: baggage);

        var enriched = InvocationEnvelopes.Enrich(Envelope(), new FixedInvocation(context));

        // 四个对应的扩展加上追踪：不进入消息的键没有扩展可去，它的值也不出现在别的扩展里
        Assert.Equal(_vectors.Baggage.Length + 1, enriched.Extensions.Count);
        Assert.True(enriched.Extensions.ContainsKey(_vectors.Trace));
        Assert.All(_vectors.Baggage, mapping => Assert.Equal(baggage[mapping.BaggageKey], enriched.Extensions[mapping.Extension]));
        Assert.All(_vectors.NotCarried, key => Assert.DoesNotContain(baggage[key], enriched.Extensions.Values));
    }

    [Fact]
    public async Task Dispatcher_PutsTheMappedExtensions_IntoTheBaggageOfTheNewInvocation()
    {
        var recorder = new BaggageRecorder();
        await using var host = await TestHost.StartAsync(static _ => { }, services: services => services.AddSingleton(recorder).AddScoped<RecordingHandler>());
        var dispatcher = new MessageDispatcher(host.Services, host.App, new MessageSubscriptions().Add(Topic, typeof(RecordingHandler), SubscriptionMode.Competing));

        // 信封带着四个对应的扩展，另带追踪、分区键与一个应用自己的扩展
        var expected = _vectors.Baggage.Select(static (mapping, index) => (mapping, Value: $"value-{index}")).ToList();
        var envelope = expected.Aggregate(Envelope(), static (current, item) => current.WithExtension(item.mapping.Extension, item.Value))
            .WithExtension(_vectors.Trace, "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01")
            .WithExtension(_vectors.PartitionKey, "order-42")
            .WithExtension("region", "cn");

        (await dispatcher.DispatchAsync(new MessageDelivery { Topic = Topic, Envelope = envelope, Attempt = 1, Mode = SubscriptionMode.Competing }, CancellationToken.None)).ThrowIfFailure();

        // 新调用的 Baggage 只有对应表里的四个键：追踪进了调用的追踪字段，分区键、别的扩展与不进入消息的键都不在
        var seen = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(recorder.Baggage);
        Assert.Equal(
            expected.Select(static item => KeyValuePair.Create(item.mapping.BaggageKey, item.Value)).OrderBy(static pair => pair.Key, StringComparer.Ordinal),
            seen.OrderBy(static pair => pair.Key, StringComparer.Ordinal));
        Assert.Equal("0af7651916cd43dd8448eb211c80319c", recorder.TraceId);
    }

    [Fact]
    public void TheVectors_CoverBothSidesOfTheLimit()
    {
        // 防止向量换了写法后上限一条都没核对还照样通过：正好到上限的要收，只因超长而不合法的要拒
        Assert.Contains(_vectors.Names.Valid, name => name.Length == _vectors.Names.MaxLength);
        Assert.Contains(_vectors.Names.Invalid, name => name.Length == _vectors.Names.MaxLength + 1 && name.All(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9')));

        // 保留名按整串比较：只比保留名多一个字符的名字合法
        Assert.Contains(_vectors.Names.Valid, name => _vectors.Names.Reserved.Any(reserved => name.Length == reserved.Length + 1 && name.StartsWith(reserved, StringComparison.Ordinal)));
    }

    /// <summary>
    /// 只有必填属性的信封
    /// </summary>
    private static MessageEnvelope Envelope() => new() { Id = "1", Source = "/orders", Type = "orders.placed" };

    /// <summary>
    /// 固定返回给定上下文的调用访问器
    /// </summary>
    /// <param name="context">当前调用的上下文</param>
    private sealed class FixedInvocation(InvocationContext context) : IInvocationAccessor
    {
        /// <inheritdoc />
        public Maybe<InvocationContext> Current => Maybe.Some(context);
    }

    /// <summary>
    /// 处理器记下的调用上下文内容
    /// </summary>
    private sealed class BaggageRecorder
    {
        /// <summary>
        /// 派发开出的调用的 Baggage
        /// </summary>
        /// <remarks>
        /// 处理器还没执行时为 <see langword="null"/>
        /// </remarks>
        public IReadOnlyDictionary<string, string>? Baggage { get; set; }

        /// <summary>
        /// 派发开出的调用的追踪 ID
        /// </summary>
        public string? TraceId { get; set; }
    }

    /// <summary>
    /// 把调用的 Baggage 与追踪 ID 记下来的处理器
    /// </summary>
    /// <param name="recorder">记录的去处</param>
    private sealed class RecordingHandler(BaggageRecorder recorder) : IMessageHandler
    {
        /// <inheritdoc />
        public ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context)
        {
            recorder.Baggage = context.Baggage;
            recorder.TraceId = context.TraceId;
            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Names">扩展属性名的用例</param>
    /// <param name="Baggage">扩展属性与 Baggage 键的对应</param>
    /// <param name="Trace">承载追踪的扩展</param>
    /// <param name="PartitionKey">承载分区键的扩展</param>
    /// <param name="NotCarried">不进入消息的已登记 Baggage 键</param>
    private sealed record ExtensionVectors(NameVectors Names, BaggageMapping[] Baggage, string Trace, string PartitionKey, string[] NotCarried);

    /// <summary>
    /// 扩展属性名的用例
    /// </summary>
    /// <param name="MaxLength">名字的字符数上限</param>
    /// <param name="Valid">合文法的名字</param>
    /// <param name="Invalid">不合法的名字（不合文法或是保留名）</param>
    /// <param name="Reserved">保留名，按序数升序</param>
    private sealed record NameVectors(int MaxLength, string[] Valid, string[] Invalid, string[] Reserved);

    /// <summary>
    /// 一项对应
    /// </summary>
    /// <param name="Extension">扩展属性名</param>
    /// <param name="BaggageKey">Baggage 键</param>
    private sealed record BaggageMapping(string Extension, string BaggageKey);
}
