// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Serialization;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 定义 JSON 里模型会拒绝的值：读入时报带键的问题，与模型的规则逐条对照
/// </summary>
/// <remarks>
/// 每条规则三处对照：读入报出这条问题（路径、消息键，zh-CN 与 en-US 的精确文案）；同一个值直接构造模型会抛出英文异常；边界上合法的文档读入通过、构造不抛出（ADR-0106 修订）
/// </remarks>
public sealed class WorkflowDefinitionJsonRulesTests
{
    /// <summary>
    /// 测试用的解析器
    /// </summary>
    private static readonly WorkflowCatalogResolver _messages = new();

    /// <summary>
    /// 每条规则一行：改坏文档的办法、路径、消息键、两种语言的文案、同一个值构造模型
    /// </summary>
    public static TheoryData<string> Rules => [.. Cases.Keys];

    /// <summary>
    /// 规则表
    /// </summary>
    private static Dictionary<string, (Action<JsonObject> Break, string Path, string Key, string Chinese, string English, Action Model)> Cases { get; } = new(StringComparer.Ordinal)
    {
        ["definition id"] = (static d => d["id"] = " ", "$.id", WorkflowMessages.DefinitionIdEmpty,
            "工作流定义的 ID 不能为空。", "The workflow definition ID must not be empty.",
            static () => _ = new WorkflowDefinition { Id = " ", Name = new WorkflowName("ok") }),
        ["definition id null"] = (static d => d["id"] = null, "$.id", WorkflowMessages.DefinitionIdEmpty,
            "工作流定义的 ID 不能为空。", "The workflow definition ID must not be empty.",
            static () => _ = new WorkflowDefinition { Id = null!, Name = new WorkflowName("ok") }),
        ["name"] = (static d => d["name"] = "Bad Name", "$.name", WorkflowMessages.DefinitionNameInvalid,
            "工作流名称 'Bad Name' 不合法：必须以小写字母或数字开头，只能包含小写字母、数字、'.'、'_' 或 '-'（最多 128 个字符）。",
            "Workflow name 'Bad Name' is invalid: it must start with a lowercase letter or digit and contain only lowercase letters, digits, '.', '_' or '-' (at most 128 characters).",
            static () => _ = new WorkflowName("Bad Name")),
        ["version"] = (static d => d["version"] = 0, "$.version", WorkflowMessages.DefinitionVersionInvalid,
            "工作流定义的版本号 0 必须不小于 1。", "The workflow definition version 0 must be at least 1.",
            static () => _ = new WorkflowDefinition { Id = "d", Name = new WorkflowName("ok"), Version = 0 }),
        ["category"] = (static d => d["category"] = new string('c', 129), "$.category", WorkflowMessages.DefinitionCategoryTooLong,
            "分类有 129 个字符，最多允许 128 个。", "The category has 129 characters; at most 128 are allowed.",
            static () => _ = new WorkflowDefinition { Id = "d", Name = new WorkflowName("ok"), Category = new string('c', 129) }),
        ["definition timeout"] = (static d => d["timeout"] = "00:00:00", "$.timeout", WorkflowMessages.DefinitionTimeoutInvalid,
            "超时 00:00:00 必须为正。", "The timeout 00:00:00 must be positive.",
            static () => _ = new WorkflowDefinition { Id = "d", Name = new WorkflowName("ok"), Timeout = TimeSpan.Zero }),
        ["node id"] = (static d => Node(d)["id"] = "bad id", "$.nodes[0].id", WorkflowMessages.DefinitionNodeIdInvalid,
            "节点 ID 'bad id' 不合法：必须以字母或数字开头，只能包含字母、数字、'.'、'_' 或 '-'（最多 64 个字符）。",
            "Node ID 'bad id' is invalid: it must start with a letter or digit and contain only letters, digits, '.', '_' or '-' (at most 64 characters).",
            static () => _ = new WorkflowNode { Id = "bad id", ActivityType = ActivityTypes.Start }),
        ["activity type"] = (static d => Node(d)["activityType"] = "1bad", "$.nodes[0].activityType", WorkflowMessages.DefinitionActivityTypeInvalid,
            "活动类型 '1bad' 不合法：必须以字母开头，只能包含字母、数字、'.'、'_' 或 '-'（最多 64 个字符）。",
            "Activity type '1bad' is invalid: it must start with a letter and contain only letters, digits, '.', '_' or '-' (at most 64 characters).",
            static () => _ = new ActivityType("1bad")),
        ["node timeout"] = (static d => Node(d)["timeout"] = "-00:00:01", "$.nodes[0].timeout", WorkflowMessages.DefinitionTimeoutInvalid,
            "超时 -00:00:01 必须为正。", "The timeout -00:00:01 must be positive.",
            static () => _ = new WorkflowNode { Id = "start", ActivityType = ActivityTypes.Start, Timeout = TimeSpan.FromSeconds(-1) }),
        ["retry attempts"] = (static d => Retry(d)["maxAttempts"] = 0, "$.nodes[0].retryPolicy.maxAttempts", WorkflowMessages.DefinitionRetryAttemptsInvalid,
            "最多尝试次数 0 必须不小于 1。", "The maximum number of attempts 0 must be at least 1.",
            static () => _ = new WorkflowRetryPolicy { MaxAttempts = 0 }),
        ["retry delay"] = (static d => Retry(d)["firstDelay"] = "-00:00:01", "$.nodes[0].retryPolicy.firstDelay", WorkflowMessages.DefinitionRetryDelayNegative,
            "首次重试延迟 -00:00:01 不能为负。", "The first retry delay -00:00:01 must not be negative.",
            static () => _ = new WorkflowRetryPolicy { FirstDelay = TimeSpan.FromSeconds(-1) }),
        ["retry backoff"] = (static d => Retry(d)["backoffFactor"] = 0.5, "$.nodes[0].retryPolicy.backoffFactor", WorkflowMessages.DefinitionRetryBackoffInvalid,
            "退避因子 0.5 必须不小于 1。", "The backoff factor 0.5 must be at least 1.",
            static () => _ = new WorkflowRetryPolicy { BackoffFactor = 0.5 }),
        ["transition id"] = (static d => Transition(d)["id"] = "bad id", "$.transitions[0].id", WorkflowMessages.DefinitionTransitionIdInvalid,
            "转移 ID 'bad id' 不合法：必须以字母或数字开头，只能包含字母、数字、'.'、'_' 或 '-'（最多 64 个字符）。",
            "Transition ID 'bad id' is invalid: it must start with a letter or digit and contain only letters, digits, '.', '_' or '-' (at most 64 characters).",
            static () => _ = new WorkflowTransition { Id = "bad id", SourceNodeId = "a", TargetNodeId = "b" }),
        ["variable name"] = (static d => ((JsonObject)d["variables"]![0]!)["name"] = " ", "$.variables[0].name", WorkflowMessages.DefinitionVariableNameEmpty,
            "变量名不能为空。", "A variable name must not be empty.",
            static () => _ = new WorkflowVariableDefinition { Name = " " }),
    };

    [Theory]
    [MemberData(nameof(Rules))]
    public void EachModelRule_IsReportedWithPathKeyAndBothLanguages_AndTheModelRejectsTheSameValue(string rule)
    {
        var (breakIt, path, key, chinese, english, model) = Cases[rule];
        var document = Valid();
        breakIt(document);

        var issue = SingleIssue(document);
        Assert.Equal((path, key), (issue.Path, issue.MessageKey));
        Assert.Equal(chinese, Render("zh-CN", issue));
        Assert.Equal(english, Render("en-US", issue));
        Assert.False(string.IsNullOrEmpty(issue.Reason));

        // 同一个值直接构造模型：英文异常照旧，规则是同一份
        Assert.ThrowsAny<ArgumentException>(model);
    }

    [Fact]
    public void MissingRequiredMembers_AreReported_FromTheSerializerMetadata()
    {
        var document = Valid();
        document.Remove("name");
        Node(document).Remove("activityType");
        Transition(document).Remove("targetNodeId");
        ((JsonObject)document["variables"]![0]!).Remove("name");

        var issues = Issues(document);
        Assert.Equal(
            ["$.name", "$.nodes[0].activityType", "$.transitions[0].targetNodeId", "$.variables[0].name"],
            issues.Select(static issue => issue.Path).Order(StringComparer.Ordinal));
        Assert.All(issues, static issue => Assert.Equal(WorkflowMessages.DefinitionMemberMissing, issue.MessageKey));
        var name = Assert.Single(issues, static issue => issue.Path == "$.name");
        Assert.Equal("缺少必填成员 'name'。", Render("zh-CN", name));
        Assert.Equal("The required member 'name' is missing.", Render("en-US", name));
    }

    [Fact]
    public void ValuesOnTheBoundary_PassTheCheck_AndTheModelIsBuilt()
    {
        var document = Valid();
        document["name"] = new string('n', WorkflowName.MaxLength);
        document["version"] = 1;
        document["category"] = new string('c', WorkflowDefinition.MaxCategoryLength);
        document["timeout"] = "00:00:00.0010000";
        Node(document)["id"] = "N" + new string('o', WorkflowIdentifiers.MaxElementIdLength - 1);
        Node(document)["timeout"] = "00:00:01";
        Transition(document)["sourceNodeId"] = Node(document)["id"]!.GetValue<string>();
        Transition(document)["targetNodeId"] = Node(document)["id"]!.GetValue<string>();
        Retry(document)["maxAttempts"] = 1;
        Retry(document)["firstDelay"] = "00:00:00";
        Retry(document)["backoffFactor"] = 1;

        var definition = WorkflowJson.DeserializeDefinition(document.ToJsonString()).ValueOrThrow();
        Assert.Equal((WorkflowName.MaxLength, 1, 1, 1.0), (definition.Name.Value.Length, definition.Version, definition.Nodes[0].RetryPolicy!.MaxAttempts, definition.Nodes[0].RetryPolicy!.BackoffFactor));
    }

    [Fact]
    public void SeveralProblems_AreReportedTogether_InTheValidatorsShape()
    {
        var document = Valid();
        document["version"] = 0;
        Node(document)["id"] = "bad id";
        Transition(document)["id"] = "";

        var error = WorkflowJson.DeserializeDefinition(document.ToJsonString()).Error!;
        Assert.Equal((WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionInvalid, (object?)3), (error.Code, error.MessageKey, error.Details!["count"]));
        Assert.Equal(["$.nodes[0].id", "$.transitions[0].id", "$.version"], Assert.IsAssignableFrom<IReadOnlyList<SchemaViolation>>(error.Details["errors"]).Select(static issue => issue.Path).Order(StringComparer.Ordinal));
        using (MessageScope.Use(_messages, "zh-CN"))
        {
            Assert.Equal("工作流定义有 3 处问题。", error.MessageKey!.L(error.Details));
        }
    }

    [Fact]
    public void DuplicateMembers_AreCheckedAsTheSerializerReadsThem()
    {
        // 同名成员按最后一个算，与序列化器一致：最后的 ID 为空，报出来；最后的 ID 合法，读入通过
        Assert.Equal(("$.id", WorkflowMessages.DefinitionIdEmpty), Path(SingleIssue("{\"id\":\"d\",\"id\":\"\",\"name\":\"ok\",\"version\":1}")));
        Assert.Equal("d", WorkflowJson.DeserializeDefinition("{\"id\":\"\",\"id\":\"d\",\"name\":\"ok\",\"version\":1}").ValueOrThrow().Id);
    }

    /// <summary>
    /// 一份合法的文档：一个带重试策略的开始节点、一条转移、一个变量
    /// </summary>
    private static JsonObject Valid() => new()
    {
        ["id"] = "d",
        ["name"] = "ok",
        ["version"] = 1,
        ["nodes"] = new JsonArray(new JsonObject
        {
            ["id"] = "start",
            ["activityType"] = ActivityTypes.Start,
            ["retryPolicy"] = new JsonObject { ["maxAttempts"] = 3, ["firstDelay"] = "00:00:10", ["backoffFactor"] = 2 },
        }),
        ["transitions"] = new JsonArray(new JsonObject { ["id"] = "t1", ["sourceNodeId"] = "start", ["targetNodeId"] = "start" }),
        ["variables"] = new JsonArray(new JsonObject { ["name"] = "amount" }),
    };

    /// <summary>
    /// 第一个节点
    /// </summary>
    private static JsonObject Node(JsonObject document) => (JsonObject)document["nodes"]![0]!;

    /// <summary>
    /// 第一个节点的重试策略
    /// </summary>
    private static JsonObject Retry(JsonObject document) => (JsonObject)Node(document)["retryPolicy"]!;

    /// <summary>
    /// 第一条转移
    /// </summary>
    private static JsonObject Transition(JsonObject document) => (JsonObject)document["transitions"]![0]!;

    /// <summary>
    /// 读入，取问题列表
    /// </summary>
    private static IReadOnlyList<SchemaViolation> Issues(JsonObject document)
    {
        var error = WorkflowJson.DeserializeDefinition(document.ToJsonString()).Error!;
        Assert.Equal((WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionInvalid), (error.Code, error.MessageKey));
        return Assert.IsAssignableFrom<IReadOnlyList<SchemaViolation>>(error.Details!["errors"]);
    }

    /// <summary>
    /// 读入，取唯一的问题
    /// </summary>
    private static SchemaViolation SingleIssue(JsonObject document) => Assert.Single(Issues(document));

    /// <summary>
    /// 读入原文，取唯一的问题
    /// </summary>
    private static SchemaViolation SingleIssue(string json)
        => Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<SchemaViolation>>(WorkflowJson.DeserializeDefinition(json).Error!.Details!["errors"]));

    /// <summary>
    /// 问题的路径与消息键
    /// </summary>
    private static (string Path, string? Key) Path(SchemaViolation issue) => (issue.Path, issue.MessageKey);

    /// <summary>
    /// 在给定区域渲染一条问题
    /// </summary>
    private static string Render(string locale, SchemaViolation issue)
    {
        using var scope = MessageScope.Use(_messages, locale);
        return issue.MessageKey!.L(issue.Arguments);
    }
}
