// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Tasks;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 回填查询列的用例共用的数据
/// </summary>
/// <remarks>
/// 经存储写入定义、实例与书签（存储按新规则算好查询列），再直接改写行，换成升级前的样子：关键字查询列按 ADR-0296 之前的逐个码元的不变文化小写折叠（<c>İ</c> 与平面以外的大写字母原样留下），另有几列是升级前还没有的空值。几个本机与门控的夹具都用这一份
/// </remarks>
internal static class BackfillScenario
{
    /// <summary>
    /// 写入的实例数
    /// </summary>
    public const int Instances = 5;

    /// <summary>
    /// 写入的定义数
    /// </summary>
    public const int Definitions = 3;

    /// <summary>
    /// 写入的书签数：每个实例一个用户任务，另有一个定义级定时书签
    /// </summary>
    public const int Bookmarks = Instances + 1;

    /// <summary>
    /// 写入定义、实例与书签，再把行换成升级前的样子
    /// </summary>
    /// <remarks>
    /// 实例两两同一个创建时刻：批的边界落在同刻的行中间时，游标要跳过同刻已经读过的行。返回换过之后与新规则不同的行数，也就是回填应当改写的行数
    /// </remarks>
    public static async Task<int> SeedAsync(TestHost host, string? tenantId)
    {
        var at = new DateTimeOffset(2026, 11, 25, 8, 0, 0, TimeSpan.Zero);
        WorkflowDefinition[] definitions =
        [
            Definition(tenantId, "bf-leave", "İzin Talebi", at),
            Definition(tenantId, "bf-deseret", "Deseret 𐐀𐐁", at.AddSeconds(1)),
            Definition(tenantId, "bf-plain", "Plain flow", at.AddSeconds(2)),
        ];
        var instances = Enumerable.Range(0, Instances).Select(index => new WorkflowInstance
        {
            Id = $"bf-{tenantId ?? "platform"}-{index}",
            TenantId = tenantId,
            DefinitionId = definitions[0].Id,
            DefinitionName = definitions[0].Name,
            DisplayName = index % 2 == 0 ? $"İstanbul 𐐀 {index}" : $"plain {index}",
            CorrelationId = $"order-{index}",
            CreatedAt = at.AddSeconds(index / 2),
            StartedAt = at.AddSeconds(index / 2),
        }).ToList();
        var bookmarks = instances.Select((instance, index) =>
        {
            var bookmark = new WorkflowBookmark
            {
                Id = $"{instance.Id}-task",
                TenantId = tenantId,
                InstanceId = instance.Id,
                NodeId = "approve",
                NodeInstanceId = $"{instance.Id}-node",
                Kind = BookmarkKinds.UserTask,
                Key = "alice",
                CreatedAt = instance.CreatedAt,
            };
            bookmark.Payload[UserTaskPayloadKeys.Title] = $"İmza {index}";
            return bookmark;
        }).Append(new WorkflowBookmark
        {
            Id = $"bf-{tenantId ?? "platform"}-timer",
            TenantId = tenantId,
            InstanceId = definitions[2].Id,
            NodeId = "start",
            NodeInstanceId = definitions[2].Id,
            Kind = BookmarkKinds.DefinitionTimer,
            DueAt = at.AddDays(1),
            CreatedAt = at,
        }).ToList();
        await Partitions.InAsync(host, tenantId, async stores =>
        {
            Assert.True((await stores.Definitions.SaveAsync(tenantId, definitions, CancellationToken.None)).IsSuccess);
            Assert.True((await stores.Instances.SaveAsync(tenantId, instances, CancellationToken.None)).IsSuccess);
            Assert.True((await stores.Bookmarks.SaveAsync(tenantId, bookmarks, CancellationToken.None)).IsSuccess);
        });

        return tenantId is null
            ? await DegradeAsync<PlatformWorkflowDefinitionRecord, PlatformWorkflowInstanceRecord, PlatformWorkflowBookmarkRecord>(host, null)
            : await DegradeAsync<WorkflowDefinitionRecord, WorkflowInstanceRecord, WorkflowBookmarkRecord>(host, tenantId);
    }

    /// <summary>
    /// 经存储按关键字查到的定义、实例与待办的个数
    /// </summary>
    /// <remarks>
    /// 关键字都含 <c>İ</c> 或平面以外的字母：查询列是旧的折叠时一个也查不到
    /// </remarks>
    public static Task<(long Definitions, long Instances, long Tasks, long Supplementary)> CountMatchesAsync(TestHost host, string? tenantId)
        => Partitions.InAsync(host, tenantId, async stores =>
        {
            var definitions = (await stores.Definitions.CountAsync(tenantId, new WorkflowDefinitionQuery { Keyword = "İZİN" }, CancellationToken.None)).ValueOrThrow();
            var instances = (await stores.Instances.CountAsync(tenantId, new WorkflowInstanceQuery { Keyword = "İstanbul" }, CancellationToken.None)).ValueOrThrow();
            var tasks = (await stores.Bookmarks.CountUserTasksAsync(tenantId, new UserTaskQuery { Keyword = "imza" }, CancellationToken.None)).ValueOrThrow();
            var supplementary = (await stores.Instances.CountAsync(tenantId, new WorkflowInstanceQuery { Keyword = "𐐨" }, CancellationToken.None)).ValueOrThrow();
            return (definitions, instances, tasks, supplementary);
        });

    /// <summary>
    /// 新规则下应当查到的个数
    /// </summary>
    public static (long Definitions, long Instances, long Tasks, long Supplementary) Expected => (1, 3, Instances, 3);

    /// <summary>
    /// 回填
    /// </summary>
    public static async Task<Result<DatabaseWorkflowBackfillReport>> RunAsync(TestHost host, string? tenantId, int batchSize)
        => Assert.Single(await host.Services.GetRequiredService<DatabaseWorkflowBackfill>().RunAsync([tenantId], batchSize, CancellationToken.None));

    /// <summary>
    /// 一个批大小下应有的批数：定义与实例各按批读
    /// </summary>
    public static int BatchesOf(int batchSize) => ((Definitions + batchSize - 1) / batchSize) + ((Instances + batchSize - 1) / batchSize);

    /// <summary>
    /// 扫描时读到的行数：全部定义、实例与书签各一次
    /// </summary>
    public const int Scanned = Definitions + Instances + Bookmarks;

    /// <summary>
    /// 升级之前的关键字折叠：逐个 UTF-16 码元取不变文化的小写
    /// </summary>
    /// <remarks>
    /// ADR-0296 之前的 <c>WorkflowKeyword.Fold</c>：<c>İ</c> 不变，代理项逐个码元转换、平面以外的字母原样留下
    /// </remarks>
    public static string OldFold(string text) => new([.. text.Select(static c => c == WorkflowKeyword.FieldSeparator ? ' ' : char.ToLowerInvariant(c))]);

    /// <summary>
    /// 多个字段按升级之前的折叠拼成一列
    /// </summary>
    private static string OldJoin(params string?[] fields) => string.Join(WorkflowKeyword.FieldSeparator, fields.Select(static field => field is null ? string.Empty : OldFold(field)));

    /// <summary>
    /// 一个已发布的定义
    /// </summary>
    private static WorkflowDefinition Definition(string? tenantId, string name, string displayName, DateTimeOffset createdAt) => new()
    {
        Id = WorkflowIdentifiers.NewId(),
        TenantId = tenantId,
        Name = new WorkflowName(name),
        Version = 1,
        Status = WorkflowDefinitionStatus.Published,
        DisplayName = displayName,
        Category = "hr",
        Nodes = [new WorkflowNode { Id = "start", ActivityType = new ActivityType(ActivityTypes.Start) }],
        CreatedAt = createdAt,
    };

    /// <summary>
    /// 把一个分区的行换成升级前的样子
    /// </summary>
    /// <remarks>
    /// 关键字查询列按旧的折叠重写；第一个定义的状态序号与分类、第二个实例的标题、定时书签的节点 ID 置空（升级前还没有这几列）。返回与新规则不同的行数
    /// </remarks>
    private static async Task<int> DegradeAsync<TDefinition, TInstance, TBookmark>(TestHost host, string? tenantId)
        where TDefinition : WorkflowDefinitionRow
        where TInstance : WorkflowInstanceRow
        where TBookmark : WorkflowBookmarkRow
    {
        var degraded = await UnitOfWorkScope.RunAsync<int>(host.Services, new UnitOfWorkOptions { TenantId = tenantId }, async (scope, token) =>
        {
            var changed = 0;
            var definitions = scope.GetRequiredService<IRepository<TDefinition, string>>();
            var definitionRows = await definitions.ListAsync(EntityQuery.All<TDefinition>().OrderBy(static row => row.Id), token);
            foreach (var row in definitionRows)
            {
                var definition = WorkflowJson.DeserializeDefinition(row.Document).ValueOrThrow();
                var before = (row.SearchText, row.StatusOrder, row.Category);
                row.SearchText = OldJoin(definition.Name.Value, definition.DisplayName, definition.Description);
                if (definition.Name.Value == "bf-leave")
                {
                    row.StatusOrder = null;
                    row.Category = null;
                }

                changed += before == (row.SearchText, row.StatusOrder, row.Category) ? 0 : 1;
            }

            var instances = scope.GetRequiredService<IRepository<TInstance, string>>();
            var instanceRows = await instances.ListAsync(EntityQuery.All<TInstance>().OrderBy(static row => row.Id), token);
            foreach (var row in instanceRows)
            {
                var instance = WorkflowJson.DeserializeInstance(row.Document);
                var before = (row.SearchText, row.Title);
                row.SearchText = OldJoin(instance.Title, instance.CorrelationId);
                if (row.Id.EndsWith("-1", StringComparison.Ordinal))
                {
                    row.Title = null;
                }

                changed += before == (row.SearchText, row.Title) ? 0 : 1;
            }

            var bookmarks = scope.GetRequiredService<IRepository<TBookmark, string>>();
            var bookmarkRows = await bookmarks.ListAsync(EntityQuery.All<TBookmark>().OrderBy(static row => row.Id), token);
            var owners = instanceRows.ToDictionary(static row => row.Id, StringComparer.Ordinal);
            foreach (var row in bookmarkRows)
            {
                var bookmark = WorkflowJson.DeserializeBookmark(row.Document);
                var before = (row.SearchTitle, row.SearchInstanceText, row.NodeId);
                if (owners.TryGetValue(row.InstanceId, out var owner))
                {
                    var instance = WorkflowJson.DeserializeInstance(owner.Document);
                    row.SearchTitle = OldFold(bookmark.Payload[UserTaskPayloadKeys.Title]!.GetValue<string>());
                    row.SearchInstanceText = OldJoin(instance.Title, instance.CorrelationId, instance.DefinitionName.Value);
                }
                else
                {
                    row.NodeId = null;
                }

                changed += before == (row.SearchTitle, row.SearchInstanceText, row.NodeId) ? 0 : 1;
            }

            var results = new List<Result>();
            results.AddRange(await definitions.UpdateRangeAsync(definitionRows, token));
            results.AddRange(await instances.UpdateRangeAsync(instanceRows, token));
            results.AddRange(await bookmarks.UpdateRangeAsync(bookmarkRows, token));
            return results.All(static result => result.IsSuccess) ? changed : results.First(static result => result.IsFailure).Error!;
        }, NullLogger.Instance, CancellationToken.None);
        return degraded.ValueOrThrow();
    }
}
