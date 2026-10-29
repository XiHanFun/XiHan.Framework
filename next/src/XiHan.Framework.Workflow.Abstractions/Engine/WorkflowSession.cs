// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 一次锁内操作的会话。
/// </summary>
/// <remarks>
/// 实例、定义图、节点实例与书签的内存副本，记录脏项与删除项供波次结束一次写回；事件与锁外动作也先攒在这里。
/// </remarks>
internal sealed class WorkflowSession
{
    /// <summary>
    /// 节点实例，按 ID。
    /// </summary>
    private readonly Dictionary<string, WorkflowNodeInstance> _nodeInstances = new(StringComparer.Ordinal);

    /// <summary>
    /// 节点实例的加载 / 创建顺序。
    /// </summary>
    private readonly List<WorkflowNodeInstance> _nodeInstanceOrder = [];

    /// <summary>
    /// 书签，按 ID。
    /// </summary>
    private readonly Dictionary<string, WorkflowBookmark> _bookmarks = new(StringComparer.Ordinal);

    /// <summary>
    /// 改过的节点实例 ID。
    /// </summary>
    private readonly HashSet<string> _dirtyNodeInstances = new(StringComparer.Ordinal);

    /// <summary>
    /// 改过或新建的书签 ID。
    /// </summary>
    private readonly HashSet<string> _dirtyBookmarks = new(StringComparer.Ordinal);

    /// <summary>
    /// 删除的书签 ID。
    /// </summary>
    private readonly HashSet<string> _deletedBookmarks = new(StringComparer.Ordinal);

    /// <summary>
    /// 节点实例状态里记录子实例 ID 的键。
    /// </summary>
    public const string ChildInstanceIdsKey = "childInstanceIds";

    /// <summary>
    /// 创建。
    /// </summary>
    public WorkflowSession(WorkflowDefinition definition, WorkflowInstance instance, IReadOnlyList<WorkflowNodeInstance> nodeInstances, IReadOnlyList<WorkflowBookmark> bookmarks, DateTimeOffset now)
    {
        Definition = definition;
        Graph = new WorkflowDefinitionGraph(definition);
        Instance = instance;
        Variables = new WorkflowVariables(instance.Variables);
        Now = now;
        foreach (var nodeInstance in nodeInstances)
        {
            _nodeInstances[nodeInstance.Id] = nodeInstance;
            _nodeInstanceOrder.Add(nodeInstance);
        }

        foreach (var bookmark in bookmarks)
        {
            _bookmarks[bookmark.Id] = bookmark;
        }
    }

    /// <summary>
    /// 定义。
    /// </summary>
    public WorkflowDefinition Definition { get; }

    /// <summary>
    /// 定义图。
    /// </summary>
    public WorkflowDefinitionGraph Graph { get; }

    /// <summary>
    /// 实例。
    /// </summary>
    public WorkflowInstance Instance { get; }

    /// <summary>
    /// 变量视图。
    /// </summary>
    public WorkflowVariables Variables { get; }

    /// <summary>
    /// 本次操作的时刻。
    /// </summary>
    public DateTimeOffset Now { get; }

    /// <summary>
    /// 待执行的工作项。
    /// </summary>
    public Queue<WorkItem> Queue { get; } = new();

    /// <summary>
    /// 是否有节点请求结束实例。
    /// </summary>
    public bool EndRequested { get; set; }

    /// <summary>
    /// 本波次已执行的节点数。
    /// </summary>
    public int Executed { get; set; }

    /// <summary>
    /// 攒下的事件。
    /// </summary>
    public List<PendingEvent> Events { get; } = [];

    /// <summary>
    /// 锁外要启动的子流程。
    /// </summary>
    public List<WorkflowStartRequest> ChildStarts { get; } = [];

    /// <summary>
    /// 锁外是否通知父实例。
    /// </summary>
    public bool NotifyParent { get; set; }

    /// <summary>
    /// 锁外级联到子实例的状态。
    /// </summary>
    public WorkflowInstanceStatus? CascadeToChildren { get; set; }

    /// <summary>
    /// 故障时请求了补偿（<see cref="WorkflowDefinition.CompensateOnFault"/>），波次结束后执行。
    /// </summary>
    public bool CompensationRequested { get; set; }

    /// <summary>
    /// 节点实例（加载 / 创建顺序）。
    /// </summary>
    public IReadOnlyList<WorkflowNodeInstance> NodeInstances => _nodeInstanceOrder;

    /// <summary>
    /// 书签。
    /// </summary>
    public IEnumerable<WorkflowBookmark> Bookmarks => _bookmarks.Values;

    /// <summary>
    /// 是否还有任何书签。
    /// </summary>
    public bool HasBookmarks => _bookmarks.Count > 0;

    /// <summary>
    /// 是否还有节点在等待的书签（实例级超时书签不算：它不代表有节点在等）。
    /// </summary>
    public bool HasActivityBookmarks => _bookmarks.Values.Any(static bookmark => !string.Equals(bookmark.Kind, BookmarkKinds.InstanceTimeout, StringComparison.Ordinal));

    /// <summary>
    /// 按 ID 取节点实例。
    /// </summary>
    public WorkflowNodeInstance? NodeInstance(string id) => _nodeInstances.GetValueOrDefault(id);

    /// <summary>
    /// 按 ID 取书签。
    /// </summary>
    public WorkflowBookmark? Bookmark(string id) => _bookmarks.GetValueOrDefault(id);

    /// <summary>
    /// 某节点实例的书签。
    /// </summary>
    public IReadOnlyList<WorkflowBookmark> BookmarksOf(string nodeInstanceId) => _bookmarks.Values.Where(bookmark => string.Equals(bookmark.NodeInstanceId, nodeInstanceId, StringComparison.Ordinal)).ToList();

    /// <summary>
    /// 新建节点实例（运行中）。
    /// </summary>
    public WorkflowNodeInstance AddNodeInstance(WorkflowNode node)
    {
        var nodeInstance = new WorkflowNodeInstance
        {
            Id = WorkflowIdentifiers.NewId(),
            InstanceId = Instance.Id,
            NodeId = node.Id,
            ActivityType = node.ActivityType,
            Status = WorkflowNodeInstanceStatus.Running,
            Attempt = 1,
            StartedAt = Now,
        };
        _nodeInstances[nodeInstance.Id] = nodeInstance;
        _nodeInstanceOrder.Add(nodeInstance);
        _dirtyNodeInstances.Add(nodeInstance.Id);
        return nodeInstance;
    }

    /// <summary>
    /// 标记节点实例已改。
    /// </summary>
    public void Touch(WorkflowNodeInstance nodeInstance) => _dirtyNodeInstances.Add(nodeInstance.Id);

    /// <summary>
    /// 为节点实例新建书签。
    /// </summary>
    public WorkflowBookmark AddBookmark(WorkflowNodeInstance nodeInstance, BookmarkRequest request) => AddBookmark(nodeInstance.NodeId, nodeInstance.Id, request);

    /// <summary>
    /// 新建书签。
    /// </summary>
    /// <remarks>
    /// 节点 ID 与节点实例 ID 由调用方给出：实例级超时书签的节点实例 ID 是实例 ID。
    /// </remarks>
    public WorkflowBookmark AddBookmark(string nodeId, string nodeInstanceId, BookmarkRequest request)
    {
        var bookmark = new WorkflowBookmark
        {
            Id = WorkflowIdentifiers.NewId(),
            InstanceId = Instance.Id,
            NodeId = nodeId,
            NodeInstanceId = nodeInstanceId,
            Kind = request.Kind,
            Key = request.Key,
            Payload = WorkflowJson.CloneObject(request.Payload),
            DueAt = request.DueAt,
            CorrelationId = request.CorrelationId,
            CreatedAt = Now,
            TenantId = Instance.TenantId,
        };
        _bookmarks[bookmark.Id] = bookmark;
        _dirtyBookmarks.Add(bookmark.Id);
        return bookmark;
    }

    /// <summary>
    /// 标记书签已改。
    /// </summary>
    public void Touch(WorkflowBookmark bookmark) => _dirtyBookmarks.Add(bookmark.Id);

    /// <summary>
    /// 删除书签。
    /// </summary>
    public void RemoveBookmark(string id)
    {
        if (_bookmarks.Remove(id))
        {
            // 本次新建又删掉的也记进删除列表：存储对不存在的 ID 忽略，比区分"新建"与"加载"便宜
            _dirtyBookmarks.Remove(id);
            _deletedBookmarks.Add(id);
        }
    }

    /// <summary>
    /// 删除某节点实例的全部书签。
    /// </summary>
    public void RemoveBookmarksOf(string nodeInstanceId)
    {
        foreach (var bookmark in BookmarksOf(nodeInstanceId))
        {
            RemoveBookmark(bookmark.Id);
        }
    }

    /// <summary>
    /// 删除全部书签。
    /// </summary>
    /// <remarks>
    /// <paramref name="keepInstanceTimeout"/> 时保留实例级超时书签（跳转后实例仍在运行，超时仍然有效）。
    /// </remarks>
    public void RemoveAllBookmarks(bool keepInstanceTimeout = false)
    {
        foreach (var bookmark in _bookmarks.Values.ToList())
        {
            if (keepInstanceTimeout && string.Equals(bookmark.Kind, BookmarkKinds.InstanceTimeout, StringComparison.Ordinal))
            {
                continue;
            }

            RemoveBookmark(bookmark.Id);
        }
    }

    /// <summary>
    /// 取消全部未结束的节点实例。
    /// </summary>
    public void CancelOpenNodeInstances()
    {
        foreach (var nodeInstance in _nodeInstanceOrder)
        {
            if (nodeInstance.Status is WorkflowNodeInstanceStatus.Running or WorkflowNodeInstanceStatus.Suspended)
            {
                nodeInstance.Status = WorkflowNodeInstanceStatus.Canceled;
                nodeInstance.EndedAt = Now;
                Touch(nodeInstance);
            }
        }
    }

    /// <summary>
    /// 攒一个事件。
    /// </summary>
    public void Publish(string topic, WorkflowEventData data, string? subject = null) => Events.Add(new PendingEvent(topic, data, subject));

    /// <summary>
    /// 事件数据。
    /// </summary>
    public WorkflowEventData EventData(WorkflowNodeInstance? nodeInstance = null) => new()
    {
        InstanceId = Instance.Id,
        DefinitionId = Instance.DefinitionId,
        DefinitionName = Instance.DefinitionName.Value,
        DefinitionVersion = Instance.DefinitionVersion,
        Status = Instance.Status,
        Title = Instance.Title,
        NodeId = nodeInstance?.NodeId,
        NodeInstanceId = nodeInstance?.Id,
        Outcome = Instance.Outcome,
        Reason = Instance.CancellationReason,
        Fault = nodeInstance?.Fault ?? Instance.Fault,
        CorrelationId = Instance.CorrelationId,
        StarterId = Instance.StarterId,
        ParentInstanceId = Instance.ParentInstanceId,
        TenantId = Instance.TenantId,
        At = Now,
    };

    /// <summary>
    /// 改过的节点实例。
    /// </summary>
    public IReadOnlyList<WorkflowNodeInstance> DirtyNodeInstances() => _dirtyNodeInstances.Select(id => _nodeInstances[id]).ToList();

    /// <summary>
    /// 改过或新建的书签。
    /// </summary>
    public IReadOnlyList<WorkflowBookmark> DirtyBookmarks() => _dirtyBookmarks.Where(_bookmarks.ContainsKey).Select(id => _bookmarks[id]).ToList();

    /// <summary>
    /// 删除的书签 ID。
    /// </summary>
    public IReadOnlyList<string> DeletedBookmarkIds() => _deletedBookmarks.ToList();

    /// <summary>
    /// 节点实例状态里记录的子实例 ID。
    /// </summary>
    public static IReadOnlyList<string> ChildInstanceIds(WorkflowNodeInstance nodeInstance)
        => WorkflowValues.ToStringList(nodeInstance.State.TryGetPropertyValue(ChildInstanceIdsKey, out var ids) ? ids : null);

    /// <summary>
    /// 登记要在锁外启动的子流程，并把子实例 ID 记进节点实例状态。
    /// </summary>
    public void ScheduleChildren(WorkflowNodeInstance nodeInstance, IReadOnlyList<WorkflowStartRequest> children)
    {
        if (children.Count == 0)
        {
            return;
        }

        var ids = new JsonArray();
        foreach (var existing in ChildInstanceIds(nodeInstance))
        {
            ids.Add((JsonNode?)JsonValue.Create(existing));
        }

        foreach (var child in children)
        {
            var request = child with
            {
                InstanceId = child.InstanceId ?? WorkflowIdentifiers.NewId(),
                ParentInstanceId = Instance.Id,
                ParentNodeInstanceId = nodeInstance.Id,
                Depth = Instance.Depth + 1,
                TenantId = child.TenantId ?? Instance.TenantId,
                CorrelationId = child.CorrelationId ?? Instance.CorrelationId,
                StarterId = child.StarterId ?? Instance.StarterId,
            };
            ids.Add((JsonNode?)JsonValue.Create(request.InstanceId));
            ChildStarts.Add(request);
        }

        nodeInstance.State[ChildInstanceIdsKey] = ids;
        Touch(nodeInstance);
    }
}
