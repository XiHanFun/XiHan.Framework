// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 模型的 JSON 往返、深拷贝与非法输入。
/// </summary>
public sealed class WorkflowJsonTests
{
    [Fact]
    public void Definition_RoundTrips_AndCloneIsDeep()
    {
        var definition = WorkflowDefinitionBuilder.Create("rt", "Round trip")
            .AddVariable("x", required: true, defaultValue: 1)
            .AddStart().AddNode("log", ActivityTypes.Log, configure: node => node.WithProperty("Text", "hi").WithRetry(2)).AddEnd("end", outcome: "done")
            .AddTransition("start", "log").AddTransition("log", "end", "x > 0")
            .Build() with { Status = WorkflowDefinitionStatus.Published, PublishedAt = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero), TenantId = "t1" };

        var json = WorkflowJson.Serialize(definition);
        Assert.Contains("\"name\":\"rt\"", json, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"Published\"", json, StringComparison.Ordinal);
        Assert.Contains("\"activityType\":\"Log\"", json, StringComparison.Ordinal);
        var back = WorkflowJson.DeserializeDefinition(json).ValueOrThrow();
        Assert.Equal((definition.Id, definition.Name, definition.DisplayName, definition.Status, definition.PublishedAt, definition.TenantId), (back.Id, back.Name, back.DisplayName, back.Status, back.PublishedAt, back.TenantId));
        Assert.Equal(definition.Nodes.Select(static node => (node.Id, node.ActivityType.Value)), back.Nodes.Select(static node => (node.Id, node.ActivityType.Value)));
        Assert.Equal(2, back.FindNode("log").ValueOrThrow().RetryPolicy!.MaxAttempts);
        Assert.Equal("hi", back.FindNode("log").ValueOrThrow().Properties["Text"]!.GetValue<string>());
        Assert.Equal("x > 0", back.Transitions[1].Condition);
        Assert.Equal(1, back.Variables[0].DefaultValue!.GetValue<int>());

        var clone = WorkflowJson.Clone(definition);
        clone.FindNode("log").ValueOrThrow().Properties["Text"] = "changed";
        Assert.Equal("hi", definition.FindNode("log").ValueOrThrow().Properties["Text"]!.GetValue<string>());

        Assert.Equal(WorkflowErrors.DefinitionInvalid, WorkflowJson.DeserializeDefinition("{\"id\":\"d\",\"name\":\"Bad Name\",\"nodes\":[]}").Error?.Code);
        Assert.Equal(WorkflowErrors.DefinitionInvalid, WorkflowJson.DeserializeDefinition("not json").Error?.Code);
    }

    [Fact]
    public void RuntimeModels_RoundTrip()
    {
        var instance = new WorkflowInstance
        {
            Id = "i1",
            DefinitionId = "d1",
            DefinitionName = new WorkflowName("rt"),
            DefinitionVersion = 3,
            Status = WorkflowInstanceStatus.Faulted,
            Variables = new JsonObject { ["a"] = new JsonArray(1, 2), ["b"] = new JsonObject { ["c"] = "d" } },
            JoinStates = { ["join"] = new WorkflowJoinState { Arrived = { "t1" }, Fired = true } },
            CorrelationId = "doc",
            StarterId = "alice",
            ParentInstanceId = "p",
            Depth = 2,
            Fault = new WorkflowFault("WORKFLOW_UNEXPECTED_001", "boom"),
            FaultNodeId = "n",
            CreatedAt = new DateTimeOffset(2026, 10, 26, 1, 2, 3, TimeSpan.Zero),
        };
        var back = WorkflowJson.DeserializeInstance(WorkflowJson.Serialize(instance));
        Assert.Equal((instance.Id, instance.DefinitionName, 3, WorkflowInstanceStatus.Faulted, "doc", "alice", "p", 2, "n", instance.CreatedAt), (back.Id, back.DefinitionName, back.DefinitionVersion, back.Status, back.CorrelationId, back.StarterId, back.ParentInstanceId, back.Depth, back.FaultNodeId, back.CreatedAt));
        Assert.Equal(2, back.Variables["a"]!.AsArray().Count);
        Assert.Equal("d", back.Variables["b"]!["c"]!.GetValue<string>());
        Assert.True(back.JoinStates["join"].Fired && back.JoinStates["join"].Arrived.Single() == "t1");
        Assert.Equal(("WORKFLOW_UNEXPECTED_001", "boom"), (back.Fault!.Code, back.Fault.Message));
        Assert.True(back.IsFinal);
        var clone = WorkflowJson.Clone(instance);
        clone.Variables["a"]!.AsArray().Add((JsonNode?)JsonValue.Create(3));
        Assert.Equal(2, instance.Variables["a"]!.AsArray().Count);

        var nodeInstance = new WorkflowNodeInstance { Id = "ni", InstanceId = "i1", NodeId = "n", ActivityType = ActivityTypes.UserTask, Attempt = 2, Status = WorkflowNodeInstanceStatus.Suspended, State = new JsonObject { ["assignees"] = new JsonArray("alice") }, Outputs = new JsonObject { ["o"] = 1 } };
        var nodeBack = WorkflowJson.DeserializeNodeInstance(WorkflowJson.Serialize(nodeInstance));
        Assert.Equal((2, WorkflowNodeInstanceStatus.Suspended, "alice", 1), (nodeBack.Attempt, nodeBack.Status, nodeBack.State["assignees"]![0]!.GetValue<string>(), nodeBack.Outputs["o"]!.GetValue<int>()));

        var bookmark = new WorkflowBookmark { Id = "b", InstanceId = "i1", NodeId = "n", NodeInstanceId = "ni", Kind = BookmarkKinds.Timer, Key = "k", DueAt = new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero), Payload = new JsonObject { ["title"] = "T" }, CorrelationId = "doc", TenantId = "t" };
        var bookmarkBack = WorkflowJson.DeserializeBookmark(WorkflowJson.Serialize(bookmark));
        Assert.Equal((BookmarkKinds.Timer, "k", bookmark.DueAt, "T", "doc", "t"), (bookmarkBack.Kind, bookmarkBack.Key, bookmarkBack.DueAt, bookmarkBack.Payload["title"]!.GetValue<string>(), bookmarkBack.CorrelationId, bookmarkBack.TenantId));

        var data = new WorkflowEventData { InstanceId = "i1", DefinitionId = "d1", DefinitionName = "rt", Status = WorkflowInstanceStatus.Running, NodeId = "n", AssigneeId = "alice", WillRetry = true, Payload = new JsonObject { ["x"] = 1 }, At = instance.CreatedAt };
        var eventBack = WorkflowJson.DeserializeEvent(WorkflowJson.SerializeEvent(data));
        Assert.Equal(("i1", "rt", WorkflowInstanceStatus.Running, "n", "alice", true, 1, instance.CreatedAt), (eventBack.InstanceId, eventBack.DefinitionName, eventBack.Status, eventBack.NodeId, eventBack.AssigneeId, eventBack.WillRetry, eventBack.Payload!["x"]!.GetValue<int>(), eventBack.At));

        Assert.Empty(WorkflowJson.CloneObject(null));
        Assert.Null(WorkflowJson.CloneNode(null));
    }
}
