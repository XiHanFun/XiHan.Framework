// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 回放 <c>topics.json</c>：主题的文法、长度上限与死信主题
/// </summary>
/// <remarks>
/// <c>messaging/topics.json</c>（RFC-0009「信封」，ADR-0277）。文法经 <see cref="TopicName"/> 与发布者回放。死信主题是主题加 <see cref="MessagingOptions.DeadLetterSuffix"/>：拼接写在各代理里（Kafka、RabbitMQ、Redis），没有单一入口，这里只核对后缀的缺省值、选项收下向量里的后缀，以及按「主题加后缀」拼出来的就是向量给的死信主题
/// </remarks>
public sealed class TopicVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly TopicVectors _vectors = ConformanceVectors.Load<TopicVectors>("topics.json");

    [Fact]
    public void MaxLength_IsTheOneInTheVectors()
        => Assert.Equal(TopicName.MaxLength, _vectors.MaxLength);

    [Fact]
    public void IsValid_AcceptsTheValidTopics()
    {
        Assert.NotEmpty(_vectors.Valid);
        foreach (var topic in _vectors.Valid)
        {
            Assert.True(TopicName.IsValid(topic), topic);
            Assert.Equal(topic, TopicName.Validate(topic));
        }
    }

    [Fact]
    public void IsValid_RejectsTheInvalidTopics()
    {
        Assert.NotEmpty(_vectors.Invalid);
        foreach (var topic in _vectors.Invalid)
        {
            Assert.False(TopicName.IsValid(topic), topic);
            Assert.Throws<ArgumentException>(() => TopicName.Validate(topic));
        }
    }

    [Fact]
    public async Task Publisher_ForwardsTheValidTopics_AndRejectsTheInvalidOnes()
    {
        var broker = new FixedOutcomeBroker(Result.Success());
        var publisher = new DirectPublisher(broker);
        var envelope = new MessageEnvelope { Id = "1", Source = "/orders", Type = "orders.placed" };

        foreach (var topic in _vectors.Valid)
        {
            Assert.True((await publisher.PublishAsync(topic, envelope, CancellationToken.None)).IsSuccess, topic);
        }

        // 不合文法的主题在发布时就拒绝，到不了代理
        foreach (var topic in _vectors.Invalid)
        {
            Assert.Equal(MessagingErrors.InvalidTopic, (await publisher.PublishAsync(topic, envelope, CancellationToken.None)).Error?.Code);
            Assert.All(await publisher.PublishManyAsync(topic, [envelope, envelope], CancellationToken.None), static result => Assert.Equal(MessagingErrors.InvalidTopic, result.Error?.Code));
        }

        Assert.Equal(_vectors.Valid, broker.Topics);
    }

    [Fact]
    public void DeadLetterSuffix_DefaultsToTheOneInTheVectors()
        => Assert.Equal(_vectors.DeadLetter.DefaultSuffix, new MessagingOptions().DeadLetterSuffix);

    [Fact]
    public void DeadLetterTopics_AreTheTopicPlusTheConfiguredSuffix()
    {
        Assert.NotEmpty(_vectors.DeadLetter.Cases);
        foreach (var vector in _vectors.DeadLetter.Cases)
        {
            // 后缀为空表示用缺省值：不设这个选项
            var options = vector.Suffix is null ? new MessagingOptions() : new MessagingOptions { DeadLetterSuffix = vector.Suffix };

            Assert.True(TopicName.IsValid(vector.Topic), vector.Topic);
            Assert.Equal(vector.DeadLetter, vector.Topic + options.DeadLetterSuffix);
        }
    }

    [Fact]
    public void TheVectors_CoverBothSidesOfTheLimit_AndADeadLetterTopicBeyondIt()
    {
        // 防止向量换了写法后上限一条都没核对还照样通过：正好到上限的要收，只因超长而不合法的要拒
        Assert.Contains(_vectors.Valid, topic => topic.Length == _vectors.MaxLength);
        Assert.Contains(_vectors.Invalid, topic => topic.Length == _vectors.MaxLength + 1 && topic.All(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9')));

        // 死信主题是代理上的名字，不再过主题文法：要有用缺省后缀的、用配置后缀的，还要有加了后缀就超过上限的
        Assert.Contains(_vectors.DeadLetter.Cases, static vector => vector.Suffix is null);
        Assert.Contains(_vectors.DeadLetter.Cases, static vector => vector.Suffix is not null);
        Assert.Contains(_vectors.DeadLetter.Cases, vector => vector.DeadLetter.Length > _vectors.MaxLength && !TopicName.IsValid(vector.DeadLetter));
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxLength">主题的字符数上限</param>
    /// <param name="Valid">合文法的主题</param>
    /// <param name="Invalid">不合文法的主题</param>
    /// <param name="DeadLetter">死信主题的用例</param>
    private sealed record TopicVectors(int MaxLength, string[] Valid, string[] Invalid, DeadLetterVectors DeadLetter);

    /// <summary>
    /// 死信主题的用例
    /// </summary>
    /// <param name="DefaultSuffix">死信后缀的缺省值</param>
    /// <param name="Cases">主题、后缀与死信主题</param>
    private sealed record DeadLetterVectors(string DefaultSuffix, DeadLetterCase[] Cases);

    /// <summary>
    /// 一个死信主题的用例
    /// </summary>
    /// <param name="Topic">主题</param>
    /// <param name="Suffix">配置的死信后缀；<see langword="null"/> 表示用缺省值</param>
    /// <param name="DeadLetter">死信主题</param>
    private sealed record DeadLetterCase(string Topic, string? Suffix, string DeadLetter);
}
