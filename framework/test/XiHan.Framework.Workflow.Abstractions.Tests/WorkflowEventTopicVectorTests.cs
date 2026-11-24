// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Workflow.Abstractions.Events;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 回放 <c>workflow-event-topics.json</c>：工作流发布的事件的来源与主题
/// </summary>
/// <remarks>
/// 规范 RFC-0021「实例与执行」的「事件」（ADR-0277）：装配了 <c>messaging.queue</c> 时引擎把实例、节点、待办与外部任务的变化作为 CloudEvents 信封发布，主题与 <c>type</c> 都是表里的名字，每个名字都合主题文法。关闭了自动建主题的部署要按这张表预先建好主题，所以常量与表必须一个不多、一个不少。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class WorkflowEventTopicVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly TopicVectors _vectors = ConformanceVectors.Load<TopicVectors>("workflow-event-topics.json");

    [Fact]
    public void Source_IsTheOneInTheVectors()
        => Assert.Equal(WorkflowTopics.Source, _vectors.Source);

    [Fact]
    public void Topics_AreExactlyTheOnesInTheVectors()
    {
        // WorkflowTopics 里除来源之外的每个常量都是一个主题：新增或改名而规范没跟上时在这里失败
        var declared = typeof(WorkflowTopics).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.IsLiteral && field.Name != nameof(WorkflowTopics.Source))
            .Select(static field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(_vectors.Topics);
        Assert.Distinct(_vectors.Topics, StringComparer.Ordinal);
        Assert.Distinct(declared, StringComparer.Ordinal);
        Assert.Equal(_vectors.Topics.Order(StringComparer.Ordinal), declared);
    }

    [Fact]
    public void Topics_MatchTheTopicGrammar()
        => Assert.All(_vectors.Topics, static topic => Assert.True(TopicName.IsValid(topic), topic));

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Source">信封的 <c>source</c></param>
    /// <param name="Topics">主题，也是信封的 <c>type</c></param>
    private sealed record TopicVectors(string Source, string[] Topics);
}
