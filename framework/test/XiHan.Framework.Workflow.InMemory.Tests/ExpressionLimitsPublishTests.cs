// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 发布带超深条件的定义（ADR-0120）
/// </summary>
/// <remarks>
/// 持流程定义权限的租户用户就能发布。修之前这里栈溢出，进程退出，接不住；现在是普通的校验错误，宿主照常工作
/// </remarks>
public sealed class ExpressionLimitsPublishTests
{
    [Theory]
    [InlineData(3000, "workflow.expression.too_long")]
    [InlineData(1900, "workflow.expression.too_deep")]
    public async Task PublishingADeeplyNestedCondition_IsAValidationError_AndTheHostKeepsWorking(int levels, string messageKey)
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryWorkflowFeature>(),
            [new($"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600")]);
        var condition = new string('(', levels) + "true" + new string(')', levels);
        var definition = WorkflowDefinitionBuilder.Create(new WorkflowName("deep-condition"))
            .AddStart()
            .AddEnd()
            .AddTransition("start", "end", condition)
            .AddDefaultTransition("start", "end")
            .Build();
        await using var scope = host.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionCatalog>();

        var created = (await catalog.CreateAsync(definition, CancellationToken.None)).ValueOrThrow();
        var published = await catalog.PublishAsync(created.Id, CancellationToken.None);

        Assert.Equal(WorkflowErrors.DefinitionInvalid, published.Error?.Code);
        var errors = Assert.IsAssignableFrom<IReadOnlyList<SchemaViolation>>(published.Error!.Details!["errors"]);
        Assert.Contains(errors, issue => issue.MessageKey == messageKey);

        var fine = WorkflowDefinitionBuilder.Create(new WorkflowName("fine")).AddStart().AddEnd().AddTransition("start", "end").Build();
        var next = (await catalog.CreateAsync(fine, CancellationToken.None)).ValueOrThrow();
        (await catalog.PublishAsync(next.Id, CancellationToken.None)).ValueOrThrow();
    }
}
