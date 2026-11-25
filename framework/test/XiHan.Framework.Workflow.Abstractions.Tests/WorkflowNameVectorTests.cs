// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Testing.Workspace;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 回放 <c>workflow-names.json</c>：定义名、元素 ID 与活动类型的文法
/// </summary>
/// <remarks>
/// 规范 RFC-0021「名字与值」（ADR-0277）：三条文法都按整串匹配、区分大小写，结尾的换行不例外；定义名与消息主题是同一条文法。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class WorkflowNameVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly NameVectors _vectors = SpecVectors.Read<NameVectors>("workflow", "workflow-names.json");

    [Fact]
    public void MaxLengths_AreTheOnesInTheVectors()
    {
        Assert.Equal(WorkflowName.MaxLength, _vectors.DefinitionNames.MaxLength);
        Assert.Equal(WorkflowIdentifiers.MaxElementIdLength, _vectors.ElementIds.MaxLength);
        Assert.Equal(ActivityType.MaxLength, _vectors.ActivityTypes.MaxLength);
    }

    [Fact]
    public void WorkflowName_AcceptsTheValidNames_AndRejectsTheInvalidOnes()
    {
        Assert.NotEmpty(_vectors.DefinitionNames.Valid);
        foreach (var name in _vectors.DefinitionNames.Valid)
        {
            Assert.True(WorkflowName.IsValid(name), name);
            Assert.Equal(name, WorkflowName.Validate(name));
            Assert.Equal(name, new WorkflowName(name).Value);
        }

        Assert.NotEmpty(_vectors.DefinitionNames.Invalid);
        foreach (var name in _vectors.DefinitionNames.Invalid)
        {
            Assert.False(WorkflowName.IsValid(name), name);
            Assert.Throws<ArgumentException>(() => WorkflowName.Validate(name));
            Assert.Throws<ArgumentException>(() => new WorkflowName(name));
        }
    }

    [Fact]
    public void WorkflowName_SharesTheTopicGrammar()
    {
        // 定义名与消息主题是同一条文法：两边对每个用例的裁定必须一样
        Assert.All(_vectors.DefinitionNames.Valid.Concat(_vectors.DefinitionNames.Invalid), static name => Assert.Equal(TopicName.IsValid(name), WorkflowName.IsValid(name)));
    }

    [Fact]
    public void ElementIds_AcceptTheValidIds_AndRejectTheInvalidOnes()
    {
        Assert.NotEmpty(_vectors.ElementIds.Valid);
        Assert.All(_vectors.ElementIds.Valid, static id => Assert.True(WorkflowIdentifiers.IsValidElementId(id), id));

        Assert.NotEmpty(_vectors.ElementIds.Invalid);
        Assert.All(_vectors.ElementIds.Invalid, static id => Assert.False(WorkflowIdentifiers.IsValidElementId(id), id));
    }

    [Fact]
    public void ActivityType_AcceptsTheValidTypes_AndRejectsTheInvalidOnes()
    {
        Assert.NotEmpty(_vectors.ActivityTypes.Valid);
        foreach (var type in _vectors.ActivityTypes.Valid)
        {
            Assert.True(ActivityType.IsValid(type), type);
            Assert.Equal(type, ActivityType.Validate(type));
            Assert.Equal(type, new ActivityType(type).Value);
        }

        Assert.NotEmpty(_vectors.ActivityTypes.Invalid);
        foreach (var type in _vectors.ActivityTypes.Invalid)
        {
            Assert.False(ActivityType.IsValid(type), type);
            Assert.Throws<ArgumentException>(() => ActivityType.Validate(type));
            Assert.Throws<ArgumentException>(() => new ActivityType(type));
        }
    }

    [Fact]
    public void TheVectors_CoverBothSidesOfEveryLimit()
    {
        // 防止向量换了写法后上限一条都没核对还照样通过：每组都要有正好到上限的合法项、只多一个字符的不合法项、结尾带换行的不合法项
        Assert.All<NameCases>([_vectors.DefinitionNames, _vectors.ElementIds, _vectors.ActivityTypes], static cases =>
        {
            Assert.Contains(cases.Valid, text => text.Length == cases.MaxLength);
            Assert.Contains(cases.Invalid, text => text.Length == cases.MaxLength + 1 && text.All(char.IsAsciiLetterOrDigit));
            Assert.Contains(cases.Invalid, static text => text.EndsWith('\n'));
        });

        // 三条文法的差别：定义名不收大写，元素 ID 收大写与数字开头，活动类型只收字母开头
        Assert.Contains(_vectors.DefinitionNames.Invalid, static name => name.Any(char.IsAsciiLetterUpper));
        Assert.Contains(_vectors.ElementIds.Valid, static id => char.IsAsciiLetterUpper(id[0]));
        Assert.Contains(_vectors.ElementIds.Valid, static id => char.IsAsciiDigit(id[0]));
        Assert.Contains(_vectors.ActivityTypes.Invalid, static type => type.Length > 0 && char.IsAsciiDigit(type[0]) && type.All(char.IsAsciiLetterOrDigit));
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="DefinitionNames">定义名的用例</param>
    /// <param name="ElementIds">节点 ID 与转移 ID 的用例</param>
    /// <param name="ActivityTypes">活动类型的用例</param>
    private sealed record NameVectors(NameCases DefinitionNames, NameCases ElementIds, NameCases ActivityTypes);

    /// <summary>
    /// 一条文法的用例
    /// </summary>
    /// <param name="MaxLength">最长的字符数</param>
    /// <param name="Valid">必须接受的</param>
    /// <param name="Invalid">必须拒绝的</param>
    private sealed record NameCases(int MaxLength, string[] Valid, string[] Invalid);
}
