// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 一次持锁执行（波次或控制操作）的内存状态：实例、节点实例、书签都先在这里改，结束时一次批量写回存储；事件与锁外动作也先攒着。
/// </summary>
internal sealed class WorkflowSession
{
    /// <summary>
    /// 节点实例 ID → 节点实例（已加载的与新建的）。
    /// </summary>
    private readonly Dictionary<string, WorkflowNodeInstance> _nodeInstances = new(StringComparer.Ordinal);

    /// <summary>
    /// 节点实例的加载顺序（开始时间升序）。
    /// </summary>
    private readonly List<WorkflowNodeInstance> _nodeInstanceOrder = [];

    /// <summary>
    /// 书签 ID → 书签（活着的）。
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
    /// 删除的书签 ID（加载时存在的）。
    /// </summary>
    private readonly HashSet<string> _deletedBookmarks = new(StringComparer.Ordinal);

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
    /// 图索引。
    /// </summary>
    public WorkflowDefinitionGraph Graph { get; }

    /// <summary>
    /// 实例（在这里改）。
    /// </summary>
    public WorkflowInstance Instance { get; }

    /// <summary>
    /// 实例变量视图。
    /// </summary>
    public WorkflowVariables Variables { get; }

    /// <summary>
    /// 本次操作的时刻。
    /// </summary>
    public DateTimeOffset Now { get; }

    /// <summary>
    /// 待执行的工作。
    /// </summary>
    public Queue<WorkItem> Queue { get; } = new();

    /// <summary>
    /// 结束节点要求结束实例。
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
    /// 锁释放后要启动的子流程。
    /// </summary>
    public List<WorkflowStartRequest> ChildStarts { get; } = [];

    /// <summary>
    /// 锁释放后要通知父实例（本实例到了终态）。
    /// </summary>
    public bool NotifyParent { get; set; }

    /// <summary>
    /// 锁释放后级联到子实例的状态（取消或终止）；为空不级联。
    /// </summary>
    public WorkflowInstanceStatus? CascadeToChildren { get; set; }

    /// <summary>
    /// 全部节点实例，开始时间升序。
    /// </summary>
    public IReadOnlyList<WorkflowNodeInstance> NodeInstances => _nodeInstanceOrder;

    /// <summary>
    /// 活着的书签。
    /// </summary>
    public IEnumerable<WorkflowBookmark> Bookmarks => _bookmarks.Values;

    /// <summary>
    /// 是否还有活着的书签。
    /// </summary>
    public bool HasBookmarks => _bookmarks.Count > 0;

    /// <summary>
    /// 按 ID 找节点实例。
    /// </summary>
    public WorkflowNodeInstance? NodeInstance(string id) => _nodeInstances.GetValueOrDefault(id);

    /// <summary>
    /// 按 ID 找活着的书签。
    /// </summary>
    public WorkflowBookmark? Bookmark(string id) => _bookmarks.GetValueOrDefault(id);

    /// <summary>
    /// 某节点实例的活着的书签。
    /// </summary>
    public IReadOnlyList<WorkflowBookmark> BookmarksOf(string nodeInstanceId) => _bookmarks.Values.Where(bookmark => string.Equals(bookmark.NodeInstanceId, nodeInstanceId, StringComparison.Ordinal)).ToList();

    /// <summary>
    /// 新建节点实例并记为脏。
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
    /// 记节点实例为脏。
    /// </summary>
    public void Touch(WorkflowNodeInstance nodeInstance) => _dirtyNodeInstances.Add(nodeInstance.Id);

    /// <summary>
    /// 新建书签（继承实例租户）。
    /// </summary>
    public WorkflowBookmark AddBookmark(WorkflowNodeInstance nodeInstance, BookmarkRequest request)
    {
        var bookmark = new WorkflowBookmark
        {
            Id = WorkflowIdentifiers.NewId(),
            InstanceId = Instance.Id,
            NodeId = nodeInstance.NodeId,
            NodeInstanceId = nodeInstance.Id,
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
    /// 记书签为脏（改了键或载荷）。
    /// </summary>
    public void Touch(WorkflowBookmark bookmark) => _dirtyBookmarks.Add(bookmark.Id);

    /// <summary>
    /// 删书签（消费）。
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
    /// 删某节点实例的全部书签。
    /// </summary>
    public void RemoveBookmarksOf(string nodeInstanceId)
    {
        foreach (var bookmark in BookmarksOf(nodeInstanceId))
        {
            RemoveBookmark(bookmark.Id);
        }
    }

    /// <summary>
    /// 删全部书签。
    /// </summary>
    public void RemoveAllBookmarks()
    {
        foreach (var id in _bookmarks.Keys.ToList())
        {
            RemoveBookmark(id);
        }
    }

    /// <summary>
    /// 把非终态的节点实例记为取消。
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
    /// 以实例当前状态构造事件数据。
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
    /// 脏的节点实例。
    /// </summary>
    public IReadOnlyList<WorkflowNodeInstance> DirtyNodeInstances() => _dirtyNodeInstances.Select(id => _nodeInstances[id]).ToList();

    /// <summary>
    /// 脏的（仍活着的）书签。
    /// </summary>
    public IReadOnlyList<WorkflowBookmark> DirtyBookmarks() => _dirtyBookmarks.Where(_bookmarks.ContainsKey).Select(id => _bookmarks[id]).ToList();

    /// <summary>
    /// 删除的书签 ID。
    /// </summary>
    public IReadOnlyList<string> DeletedBookmarkIds() => _deletedBookmarks.ToList();

    /// <summary>
    /// 节点实例状态里记录的子实例 ID（<see cref="ChildInstanceIdsKey"/>）。
    /// </summary>
    public static IReadOnlyList<string> ChildInstanceIds(WorkflowNodeInstance nodeInstance)
        => WorkflowValues.ToStringList(nodeInstance.State.TryGetPropertyValue(ChildInstanceIdsKey, out var ids) ? ids : null);

    /// <summary>
    /// 节点状态里记录子实例 ID 的键。
    /// </summary>
    public const string ChildInstanceIdsKey = "childInstanceIds";

    /// <summary>
    /// 登记子流程启动：补父子关系、深度、租户、关联与发起人，分配实例 ID，并记进节点状态。
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
