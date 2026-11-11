// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 标识、重试策略、查询、选项、故障与消息目录等小模型的约束
/// </summary>
public sealed class WorkflowModelTests
{
    [Fact]
    public void Identifiers_FollowTheirGrammars()
    {
        Assert.Equal("order.approval_v2", new WorkflowName("order.approval_v2").ToString());
        Assert.Equal(new WorkflowName("a"), (WorkflowName)"a");
        Assert.ThrowsAny<ArgumentException>(() => new WorkflowName("Upper"));
        Assert.ThrowsAny<ArgumentException>(() => new WorkflowName("-dash"));
        Assert.ThrowsAny<ArgumentException>(() => new WorkflowName(new string('a', 129)));
        Assert.Equal(128, new WorkflowName(new string('a', 128)).Value.Length);

        Assert.Equal("Custom.Type-1", new ActivityType("Custom.Type-1").Value);
        Assert.ThrowsAny<ArgumentException>(() => new ActivityType("1starts-with-digit"));
        Assert.ThrowsAny<ArgumentException>(() => new ActivityType("has space"));

        Assert.True(WorkflowIdentifiers.IsValidElementId("a") && WorkflowIdentifiers.IsValidElementId("A-1_b.c") && WorkflowIdentifiers.IsValidElementId(new string('x', 64)));
        Assert.False(WorkflowIdentifiers.IsValidElementId(null) || WorkflowIdentifiers.IsValidElementId("") || WorkflowIdentifiers.IsValidElementId("-x") || WorkflowIdentifiers.IsValidElementId("a b") || WorkflowIdentifiers.IsValidElementId(new string('x', 65)));
        var id = WorkflowIdentifiers.NewId();
        Assert.Equal(32, id.Length);
        Assert.True(id.All(Uri.IsHexDigit));
        Assert.NotEqual(id, WorkflowIdentifiers.NewId());
    }

    [Fact]
    public void RetryPolicy_ComputesBackoff()
    {
        var policy = new WorkflowRetryPolicy { MaxAttempts = 3, FirstDelay = TimeSpan.FromSeconds(10), BackoffFactor = 2 };
        Assert.True(policy.CanRetry(1) && policy.CanRetry(2) && !policy.CanRetry(3));
        Assert.Equal(TimeSpan.FromSeconds(10), policy.DelayAfter(1));
        Assert.Equal(TimeSpan.FromSeconds(20), policy.DelayAfter(2));
        Assert.Equal(TimeSpan.FromSeconds(40), policy.DelayAfter(3));
        Assert.Equal(TimeSpan.MaxValue, (policy with { BackoffFactor = 1000 }).DelayAfter(10));
        Assert.Equal(TimeSpan.FromSeconds(10), (policy with { BackoffFactor = 1 }).DelayAfter(7));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowRetryPolicy { MaxAttempts = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowRetryPolicy { BackoffFactor = 0.5 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowRetryPolicy { FirstDelay = TimeSpan.FromSeconds(-1) });
    }

    [Fact]
    public void QueryOptionsAndFaults_EnforceBounds()
    {
        Assert.Equal(100, new WorkflowInstanceQuery().Limit);
        Assert.Equal(WorkflowInstanceQuery.MaxLimit, new WorkflowInstanceQuery { Limit = WorkflowInstanceQuery.MaxLimit }.Limit);
        // 越界不在构造时抛出：查询常由请求绑定，由引擎以校验错误返回（一致性套件用例 32）
        Assert.Equal(0, new WorkflowInstanceQuery { Limit = 0 }.Limit);
        Assert.Equal(-1, new WorkflowInstanceQuery { Skip = -1 }.Skip);
        Assert.Equal(0, new WorkflowPurgeRequest { EndedBefore = DateTimeOffset.UnixEpoch, Limit = 0 }.Limit);

        var options = new WorkflowOptions();
        Assert.Equal((1000, 16, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5), 10_000), (options.MaxNodeExecutionsPerBurst, options.MaxSubWorkflowDepth, options.LockTimeout, options.NotResumableBackoff, options.ExpressionCacheSize));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowOptions { MaxNodeExecutionsPerBurst = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowOptions { MaxSubWorkflowDepth = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowOptions { ExpressionCacheSize = 0 });

        var error = Error.NotFound(WorkflowErrors.InstanceNotFound, WorkflowMessages.InstanceNotFound, MessageArguments.Of(("id", "i1")));
        var fault = WorkflowFault.From(error);
        Assert.Equal(WorkflowErrors.InstanceNotFound, fault.Code);
        Assert.Equal("NOTFOUND", fault.ToError().Kind);
        Assert.Equal(fault.Code, fault.ToError().Code);

        var instance = new WorkflowInstance { Id = "i", DefinitionId = "d", DefinitionName = new WorkflowName("n") };
        Assert.False(instance.IsFinal);
        foreach (var status in new[] { WorkflowInstanceStatus.Completed, WorkflowInstanceStatus.Faulted, WorkflowInstanceStatus.Canceled, WorkflowInstanceStatus.Terminated })
        {
            instance.Status = status;
            Assert.True(instance.IsFinal);
        }

        instance.Status = WorkflowInstanceStatus.Suspended;
        Assert.False(instance.IsFinal);
    }

    [Fact]
    public void Keyword_FoldsCaseOnly_KeepsWildcardCharacters_AndNeverSpansFields()
    {
        Assert.Null(WorkflowKeyword.Normalize(null));
        Assert.Null(WorkflowKeyword.Normalize("  \t "));
        Assert.Equal("expense", WorkflowKeyword.Normalize("  Expense "));
        // 只做不变文化小写与分隔符替换：% _ [ \ 与全角字符原样保留（ADR-0099 第四节）；折叠幂等
        Assert.Equal("50% off_a[b]\\c dｅ％＿［＼", WorkflowKeyword.Fold("50% OFF_a[b]\\c\u001FdＥ％＿［＼"));
        Assert.Equal(WorkflowKeyword.Fold("A_b%"), WorkflowKeyword.Fold(WorkflowKeyword.Fold("A_b%")));

        Assert.Equal("order\u001F\u001Fbuy _x", WorkflowKeyword.Join("Order", null, "Buy _X"));
        var keyword = WorkflowKeyword.Normalize("BUY _x")!;
        Assert.True(WorkflowKeyword.Matches(keyword, "order", null, "Buy _X"));
        Assert.False(WorkflowKeyword.Matches(keyword, "order", null, "Buy aX"));
        // 全角与半角是不同的字符
        Assert.False(WorkflowKeyword.Matches(WorkflowKeyword.Normalize("50％")!, "50% off"));
        Assert.False(WorkflowKeyword.Matches(WorkflowKeyword.Normalize("50%")!, "50％ off"));
        Assert.True(WorkflowKeyword.Matches(WorkflowKeyword.Normalize(@"A\B[")!, @"x a\b[ y"));
        // 关键字折叠后不含分隔符，在拼成一列的值里也不会跨过字段的边界
        var spanning = WorkflowKeyword.Normalize("der\u001Fbuy")!;
        Assert.False(WorkflowKeyword.Matches(spanning, "order", "buy"));
        Assert.DoesNotContain(spanning, WorkflowKeyword.Join("order", "buy"), StringComparison.Ordinal);
    }

    [Fact]
    public void DefinitionQueryAndSummary_FollowTheContract()
    {
        Assert.Equal((100, 0, 1000), (new WorkflowDefinitionQuery().Limit, new WorkflowDefinitionQuery().Skip, WorkflowDefinitionQuery.MaxLimit));
        // 越界不在构造时抛出：由目录以校验错误返回（一致性套件用例 33）
        Assert.Equal(0, new WorkflowDefinitionQuery { Limit = 0 }.Limit);

        var definition = WorkflowDefinitionBuilder.Create(new WorkflowName("expense"), "Expense Claim").WithCategory("finance").WithDescription("Travel").AddStart().Build();
        Assert.True(new WorkflowDefinitionQuery { Keyword = " CLAIM ", Category = "finance", Status = WorkflowDefinitionStatus.Draft, Name = new WorkflowName("expense") }.Matches(definition));
        Assert.True(new WorkflowDefinitionQuery { Keyword = "travel" }.Matches(definition));
        Assert.False(new WorkflowDefinitionQuery { Category = "Finance" }.Matches(definition));
        // 名字末尾 + 显示名开头拼起来才有的片段不匹配
        Assert.False(new WorkflowDefinitionQuery { Keyword = "seex" }.Matches(definition));

        var summary = WorkflowDefinitionSummary.From(definition);
        Assert.Equal((definition.Id, "expense", "Expense Claim", "Travel", "finance", "Expense Claim"), (summary.Id, summary.Name.Value, summary.DisplayName!, summary.Description!, summary.Category!, summary.Title));
        Assert.Equal("expense", (summary with { DisplayName = " " }).Title);

        // 分类与数据库的分类列同宽：超长构造即抛出，从 JSON 读入是定义不合法
        Assert.Equal(128, (definition with { Category = new string('c', WorkflowDefinition.MaxCategoryLength) }).Category!.Length);
        Assert.Throws<ArgumentException>(() => definition with { Category = new string('c', WorkflowDefinition.MaxCategoryLength + 1) });
        var json = WorkflowJson.Serialize(definition).Replace("\"finance\"", $"\"{new string('c', 129)}\"", StringComparison.Ordinal);
        Assert.Equal(WorkflowErrors.DefinitionInvalid, WorkflowJson.DeserializeDefinition(json).Error?.Code);
    }

    [Fact]
    public void SortKeys_TruncateTitles_WithoutSplittingSurrogatePairs_AndQueriesKeepTheirDefaultOrder()
    {
        Assert.Equal("short", WorkflowSortKeys.Title("short"));
        var exact = new string('a', WorkflowSortKeys.MaxTitleLength);
        Assert.Equal(exact, WorkflowSortKeys.Title(exact));
        Assert.Equal(exact, WorkflowSortKeys.Title(exact + "tail"));
        // 第 256 个字符是高位代理：少取一个，不留下孤立的代理
        var surrogate = new string('a', WorkflowSortKeys.MaxTitleLength - 1) + "\U0001F600" + "tail";
        Assert.Equal(new string('a', WorkflowSortKeys.MaxTitleLength - 1), WorkflowSortKeys.Title(surrogate));

        // 没给排序项（缺省排序由存储施加：定义名字升序，实例创建时间降序）；排序项的方向缺省升序；上限等于可排序字段数
        Assert.Null(new WorkflowDefinitionQuery().Sorts);
        Assert.Null(new WorkflowInstanceQuery().Sorts);
        Assert.False(new WorkflowDefinitionSort(WorkflowDefinitionSortField.Title).Descending);
        Assert.False(new WorkflowInstanceSort(WorkflowInstanceSortField.Title).Descending);
        Assert.Equal((6, 5), (WorkflowDefinitionQuery.MaxSorts, WorkflowInstanceQuery.MaxSorts));
        Assert.Equal(WorkflowDefinitionQuery.MaxSorts, Enum.GetValues<WorkflowDefinitionSortField>().Length);
        Assert.Equal(WorkflowInstanceQuery.MaxSorts, Enum.GetValues<WorkflowInstanceSortField>().Length);

        // 定义的创建时间区间：下界含、上界不含
        var created = DateTimeOffset.UnixEpoch.AddDays(1);
        var definition = WorkflowDefinitionBuilder.Create(new WorkflowName("range")).AddStart().AddEnd().AddTransition("start", "end").Build() with { CreatedAt = created };
        Assert.True(new WorkflowDefinitionQuery { CreatedFrom = created, CreatedTo = created.AddTicks(1) }.Matches(definition));
        Assert.False(new WorkflowDefinitionQuery { CreatedTo = created }.Matches(definition));
        Assert.False(new WorkflowDefinitionQuery { CreatedFrom = created.AddTicks(1) }.Matches(definition));
    }

    [Fact]
    public void InstanceQuery_MatchesCorrelationSetTopLevelAndKeyword()
    {
        Assert.Equal(1000, WorkflowInstanceQuery.MaxCorrelationIds);
        var parent = new WorkflowInstance { Id = "p", DefinitionId = "d", DefinitionName = new WorkflowName("leave"), CorrelationId = "doc-1", DisplayName = "Leave Request" };
        var child = new WorkflowInstance { Id = "c", DefinitionId = "d2", DefinitionName = new WorkflowName("leave-step"), CorrelationId = "doc-1", ParentInstanceId = "p" };

        Assert.True(new WorkflowInstanceQuery { CorrelationIds = ["doc-2", "doc-1"] }.Matches(child));
        Assert.False(new WorkflowInstanceQuery { CorrelationIds = ["doc-2"] }.Matches(parent));
        // 空集合不匹配任何实例；没有关联 ID 的实例也不匹配任何集合
        Assert.False(new WorkflowInstanceQuery { CorrelationIds = [] }.Matches(parent));
        Assert.False(new WorkflowInstanceQuery { CorrelationIds = ["doc-1"] }.Matches(new WorkflowInstance { Id = "x", DefinitionId = "d", DefinitionName = new WorkflowName("leave") }));
        Assert.True(new WorkflowInstanceQuery { TopLevelOnly = true }.Matches(parent));
        Assert.False(new WorkflowInstanceQuery { TopLevelOnly = true }.Matches(child));

        // 标题：显示名，没有显示名取定义名
        Assert.True(new WorkflowInstanceQuery { Keyword = "REQUEST" }.Matches(parent));
        Assert.False(new WorkflowInstanceQuery { Keyword = "leave-" }.Matches(parent));
        Assert.True(new WorkflowInstanceQuery { Keyword = "leave-" }.Matches(child));
        Assert.True(new WorkflowInstanceQuery { Keyword = "  " }.Matches(child));

        // 关联 ID 也被关键字匹配（ADR-0099），不跨字段；没有关联 ID 的实例只按标题
        Assert.True(new WorkflowInstanceQuery { Keyword = "DOC-1" }.Matches(parent));
        Assert.False(new WorkflowInstanceQuery { Keyword = "requestdoc" }.Matches(parent));
        Assert.False(new WorkflowInstanceQuery { Keyword = "doc" }.Matches(new WorkflowInstance { Id = "x", DefinitionId = "d", DefinitionName = new WorkflowName("leave") }));
    }

    [Fact]
    public void BookmarkSummary_CopiesTheWaitPoint_WithoutThePayload()
    {
        var due = DateTimeOffset.UnixEpoch.AddDays(1);
        var bookmark = new WorkflowBookmark { Id = "b", InstanceId = "i", NodeId = "n", NodeInstanceId = "ni", Kind = BookmarkKinds.Signal, Key = "go", DueAt = due, CorrelationId = "doc", CreatedAt = DateTimeOffset.UnixEpoch, TenantId = "t1" };
        bookmark.Payload["secret"] = "x";
        var summary = WorkflowBookmarkSummary.From(bookmark);
        Assert.Equal(("b", "i", "n", "ni", BookmarkKinds.Signal), (summary.Id, summary.InstanceId, summary.NodeId, summary.NodeInstanceId, summary.Kind));
        Assert.Equal(("go", "doc"), (summary.Key!, summary.CorrelationId!));
        Assert.Equal((due, DateTimeOffset.UnixEpoch), (summary.DueAt!.Value, summary.CreatedAt));
    }

    [Fact]
    public void UserTaskDescriptor_ListsEveryInputTheActivityReads()
    {
        // 活动读取的属性名都以常量 *Property 声明：描述符（设计器的属性面板）必须一个不漏、不多不少（ADR-0096 第五节）
        var declared = typeof(UserTaskActivity).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(static field => field.IsLiteral && field.Name.EndsWith("Property", StringComparison.Ordinal))
            .Select(static field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(18, declared.Count);
        Assert.Equal(declared, BuiltInActivityDescriptors.UserTask.Inputs.Select(static input => input.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            [("ApprovalThreshold", "string"), ("DueAfterSeconds", "number"), ("DueAt", "expression"), ("FormKey", "template"), ("Priority", "number"), ("ReminderAfterSeconds", "number")],
            BuiltInActivityDescriptors.UserTask.Inputs.Where(static input => input.Name is "ApprovalThreshold" or "FormKey" or "Priority" or "DueAfterSeconds" or "DueAt" or "ReminderAfterSeconds").Select(static input => (input.Name, input.Kind)).OrderBy(static input => input.Name, StringComparer.Ordinal));
    }

    [Fact]
    public void Messages_AreCompleteForEveryBuiltInLocale()
    {
        var catalog = WorkflowMessages.Catalog;
        Assert.Equal(WorkflowMessages.CatalogName, catalog.Name);
        Assert.Equal(10, catalog.Locales.Count);
        foreach (var locale in new[] { "en-US", "zh-CN", "zh-TW", "ja-JP", "ko-KR", "de-DE", "es-ES", "fr-FR", "pt-BR", "ru-RU" })
        {
            Assert.Contains(locale, catalog.Locales);
            Assert.Contains("{id}", catalog.Find(WorkflowMessages.InstanceNotFound, locale).ValueOrThrow(), StringComparison.Ordinal);
        }
    }
}
