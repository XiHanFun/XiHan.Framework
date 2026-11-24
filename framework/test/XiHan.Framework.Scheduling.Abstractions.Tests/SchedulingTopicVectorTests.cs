// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Scheduling.Abstractions.Events;

namespace XiHan.Framework.Scheduling.Abstractions.Tests;

/// <summary>
/// 回放 <c>event-topics.json</c>：调度发布的事件的来源与主题
/// </summary>
/// <remarks>
/// 规范 RFC-0013「引擎」的事件与 RFC-0009「流」里要预建的主题：信封的 <c>source</c> 是 <c>/scheduling</c>，主题与 <c>type</c> 都是表里的名字，每个都合主题文法；关闭了自动建主题的部署照这张表预建（ADR-0277）
/// </remarks>
public sealed class SchedulingTopicVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly EventTopicVectors _vectors = ConformanceVectors.Load<EventTopicVectors>("event-topics.json");

    [Fact]
    public void Source_IsTheOneInTheVectors()
        => Assert.Equal(SchedulingTopics.Source, _vectors.Source);

    [Fact]
    public void TheTopics_AreExactlyTheOnesInTheVectors()
    {
        // 表是封闭的：.NET 多发一个表里没有的主题，照表预建主题的部署就会漏建，所以取全部常量来比，不逐个点名
        var declared = typeof(SchedulingTopics)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field is { IsLiteral: true, Name: not nameof(SchedulingTopics.Source) })
            .Select(static field => Assert.IsType<string>(field.GetRawConstantValue()))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(_vectors.Topics);
        Assert.Equal(_vectors.Topics.Order(StringComparer.Ordinal), declared);
    }

    [Fact]
    public void EveryTopic_IsAValidTopicName()
    {
        Assert.All(_vectors.Topics, static topic => Assert.True(TopicName.IsValid(topic), topic));
        Assert.Equal(_vectors.Topics.Length, _vectors.Topics.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Source">信封的 <c>source</c></param>
    /// <param name="Topics">主题，也是信封的 <c>type</c></param>
    private sealed record EventTopicVectors(string Source, string[] Topics);
}
