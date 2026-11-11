// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 故障存消息键与参数、按当前消息作用域本地化（ADR-0105）
/// </summary>
public sealed class WorkflowFaultTests
{
    /// <summary>
    /// 测试用的解析器
    /// </summary>
    private static readonly WorkflowCatalogResolver _messages = new();

    [Fact]
    public void From_KeepsKeyArgumentsAndInner_AndLocalizesInTheCurrentScope()
    {
        var inner = Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ActivityFaultRaised, details: MessageArguments.Of(("message", "Budget exceeded")));
        var fault = WorkflowFault.From(Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ChildFailed, MessageArguments.Of(("id", "c1"), ("status", "Faulted")), inner));

        Assert.Equal((WorkflowErrors.ActivityFailed, WorkflowMessages.ChildFailed, "c1", "Faulted"), (fault.Code, fault.MessageKey, fault.Arguments!["id"], fault.Arguments["status"]));
        Assert.Equal((WorkflowMessages.ActivityFaultRaised, "Budget exceeded"), (fault.Inner?.MessageKey, fault.Inner?.Arguments?["message"]));
        Assert.Equal("子流程 'c1' 没有正常完成（状态 Faulted）。原因：流程报告故障：Budget exceeded", In("zh-CN", fault.Localize));
        Assert.Equal("Sub-workflow 'c1' did not complete (status Faulted). Cause: The workflow raised a fault: Budget exceeded", In("en-US", fault.Localize));
        Assert.Equal("サブワークフロー 'c1' は正常に完了しませんでした（状態 Faulted）。原因：ワークフローが障害を報告しました: Budget exceeded", In("ja-JP", fault.Localize));

        // 转回错误：带键、参数与内层，交给宿主在 Egress 按调用方的区域翻译
        var error = fault.ToError();
        Assert.Equal((WorkflowErrors.ActivityFailed, WorkflowMessages.ChildFailed, (object?)"c1"), (error.Code, error.MessageKey, error.Details!["id"]));
        Assert.Equal((WorkflowMessages.ActivityFaultRaised, (object?)"Budget exceeded"), (error.Inner?.MessageKey, error.Inner?.Details?["message"]));
    }

    [Fact]
    public void Arguments_AreStoredAsInvariantStrings()
    {
        var fault = WorkflowFault.From(Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.ExternalTaskMaxTasksInvalid, MessageArguments.Of(("count", 1500), ("max", 100))));

        Assert.Equal(("1500", "100"), (fault.Arguments!["count"], fault.Arguments["max"]));
        Assert.Equal("一次拉取的外部任务数 1500 必须在 1 到 100 之间。", In("zh-CN", fault.Localize));
        Assert.Null(fault.Inner);
    }

    [Fact]
    public void FreeTextFaults_AndFaultsWrittenBeforeTheUpgrade_ShowTheirMessage()
    {
        var literal = WorkflowFault.From(Error.Literal(WorkflowErrors.ActivityFailed, "free text"));
        Assert.Null(literal.MessageKey);
        Assert.Equal("free text", In("zh-CN", literal.Localize));

        // 升级前写入的故障只有错误码与按进程默认区域渲染的英文，没有键：原文展示，转回错误是自由文本
        var stored = new WorkflowFault(WorkflowErrors.ActivityFailed, "Activity 'Http' failed: boom");
        Assert.Equal("Activity 'Http' failed: boom", In("zh-CN", stored.Localize));
        var error = stored.ToError();
        Assert.Equal((WorkflowErrors.ActivityFailed, null, "Activity 'Http' failed: boom"), (error.Code, error.MessageKey, error.Message));
    }

    [Fact]
    public void Json_RoundTripsKeyArgumentsAndInner_AndReadsDocumentsWithoutThem()
    {
        var inner = Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.NodeTimedOut, details: MessageArguments.Of(("node", "wait")));
        var instance = new WorkflowInstance
        {
            Id = "i1",
            DefinitionId = "d1",
            DefinitionName = new WorkflowName("rt"),
            Status = WorkflowInstanceStatus.Faulted,
            Fault = WorkflowFault.From(Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ChildFailed, MessageArguments.Of(("id", "c1"), ("status", "Faulted")), inner)),
        };

        var json = JsonNode.Parse(WorkflowJson.Serialize(instance))!;
        Assert.Equal((WorkflowMessages.ChildFailed, "c1"), (json["fault"]!["messageKey"]!.GetValue<string>(), json["fault"]!["arguments"]!["id"]!.GetValue<string>()));
        var back = WorkflowJson.DeserializeInstance(json.ToJsonString()).Fault!;
        Assert.Equal((WorkflowMessages.ChildFailed, "Faulted", WorkflowMessages.NodeTimedOut, "wait"), (back.MessageKey, back.Arguments!["status"], back.Inner?.MessageKey, back.Inner?.Arguments?["node"]));
        Assert.Equal("子流程 'c1' 没有正常完成（状态 Faulted）。原因：节点 'wait' 超时。", In("zh-CN", back.Localize));

        // 升级前写入的文档没有 messageKey / arguments / inner：读出来只有错误码与消息
        json["fault"] = new JsonObject { ["code"] = WorkflowErrors.ActivityFailed, ["message"] = "boom" };
        var old = WorkflowJson.DeserializeInstance(json.ToJsonString()).Fault!;
        Assert.Equal((WorkflowErrors.ActivityFailed, "boom", null, null, null), (old.Code, old.Message, old.MessageKey, old.Arguments, old.Inner));
    }

    /// <summary>
    /// 在给定区域的消息作用域里渲染
    /// </summary>
    private static string In(string locale, Func<string> render)
    {
        using var scope = MessageScope.Use(_messages, locale);
        return render();
    }
}
