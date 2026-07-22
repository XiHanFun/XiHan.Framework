// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Timing;
using XiHan.Framework.Utils.Diagnostics;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Stores;
using XiHan.Framework.Workflow.Builders;

namespace XiHan.Framework.Workflow.Definitions;

/// <summary>
/// 流程定义管理器默认实现
/// </summary>
public class WorkflowDefinitionManager : IWorkflowDefinitionManager
{
    private readonly IWorkflowDefinitionStore _definitionStore;
    private readonly WorkflowDefinitionValidator _validator;
    private readonly IDistributedIdGenerator<long> _idGenerator;
    private readonly IClock _clock;
    private readonly ICurrentTenant _currentTenant;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="definitionStore">定义存储</param>
    /// <param name="validator">定义校验器</param>
    /// <param name="idGenerator">分布式标识生成器</param>
    /// <param name="clock">时钟</param>
    /// <param name="currentTenant">当前租户</param>
    public WorkflowDefinitionManager(
        IWorkflowDefinitionStore definitionStore,
        WorkflowDefinitionValidator validator,
        IDistributedIdGenerator<long> idGenerator,
        IClock clock,
        ICurrentTenant currentTenant)
    {
        _definitionStore = definitionStore;
        _validator = validator;
        _idGenerator = idGenerator;
        _clock = clock;
        _currentTenant = currentTenant;
    }

    /// <inheritdoc />
    public async Task<WorkflowDefinition> CreateAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(definition, nameof(definition));
        Guard.NotNullOrWhiteSpace(definition.Code, nameof(definition.Code));
        Guard.NotNullOrWhiteSpace(definition.Name, nameof(definition.Name));

        definition.Id = _idGenerator.NextIdString();
        definition.Version = await _definitionStore.GetMaxVersionAsync(definition.Code, cancellationToken) + 1;
        definition.Status = WorkflowDefinitionStatus.Draft;
        definition.CreationTime = _clock.Now;
        definition.PublishTime = null;
        definition.TenantId ??= _currentTenant.Id;

        await _definitionStore.InsertAsync(definition, cancellationToken);
        return definition;
    }

    /// <inheritdoc />
    public async Task<WorkflowDefinition> UpdateDraftAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(definition, nameof(definition));

        var existing = await GetAsync(definition.Id, cancellationToken);
        if (existing.Status != WorkflowDefinitionStatus.Draft)
        {
            throw new WorkflowException($"流程定义 {existing.Code} v{existing.Version} 状态为 {existing.Status}，仅草稿可更新");
        }

        // 编码、版本与创建信息不可变
        definition.Code = existing.Code;
        definition.Version = existing.Version;
        definition.Status = WorkflowDefinitionStatus.Draft;
        definition.CreationTime = existing.CreationTime;
        definition.PublishTime = null;
        definition.TenantId = existing.TenantId;

        await _definitionStore.UpdateAsync(definition, cancellationToken);
        return definition;
    }

    /// <inheritdoc />
    public async Task<WorkflowDefinition> PublishAsync(string id, CancellationToken cancellationToken = default)
    {
        var definition = await GetAsync(id, cancellationToken);
        if (definition.Status != WorkflowDefinitionStatus.Draft)
        {
            throw new WorkflowException($"流程定义 {definition.Code} v{definition.Version} 状态为 {definition.Status}，仅草稿可发布");
        }

        _validator.ValidateAndThrow(definition);

        definition.Status = WorkflowDefinitionStatus.Published;
        definition.PublishTime = _clock.Now;
        await _definitionStore.UpdateAsync(definition, cancellationToken);
        return definition;
    }

    /// <inheritdoc />
    public async Task<WorkflowDefinition> CreateNewVersionAsync(string code, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(code, nameof(code));

        var maxVersion = await _definitionStore.GetMaxVersionAsync(code, cancellationToken);
        if (maxVersion == 0)
        {
            throw new WorkflowException($"流程编码 {code} 不存在，无法创建新版本");
        }

        var source = await _definitionStore.FindByVersionAsync(code, maxVersion, cancellationToken)
            ?? throw new WorkflowException($"流程定义 {code} v{maxVersion} 不存在");

        // 深拷贝走 JSON 往返，避免新旧版本共享节点引用
        var clone = WorkflowDefinitionJsonSerializer.Deserialize(WorkflowDefinitionJsonSerializer.Serialize(source));
        clone.Id = _idGenerator.NextIdString();
        clone.Version = maxVersion + 1;
        clone.Status = WorkflowDefinitionStatus.Draft;
        clone.CreationTime = _clock.Now;
        clone.PublishTime = null;

        await _definitionStore.InsertAsync(clone, cancellationToken);
        return clone;
    }

    /// <inheritdoc />
    public async Task<WorkflowDefinition> DisableAsync(string id, CancellationToken cancellationToken = default)
    {
        var definition = await GetAsync(id, cancellationToken);
        if (definition.Status != WorkflowDefinitionStatus.Published)
        {
            throw new WorkflowException($"流程定义 {definition.Code} v{definition.Version} 状态为 {definition.Status}，仅已发布可停用");
        }

        definition.Status = WorkflowDefinitionStatus.Disabled;
        await _definitionStore.UpdateAsync(definition, cancellationToken);
        return definition;
    }

    /// <inheritdoc />
    public async Task<WorkflowDefinition> ArchiveAsync(string id, CancellationToken cancellationToken = default)
    {
        var definition = await GetAsync(id, cancellationToken);
        definition.Status = WorkflowDefinitionStatus.Archived;
        await _definitionStore.UpdateAsync(definition, cancellationToken);
        return definition;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var definition = await GetAsync(id, cancellationToken);
        if (definition.Status != WorkflowDefinitionStatus.Draft)
        {
            throw new WorkflowException($"流程定义 {definition.Code} v{definition.Version} 状态为 {definition.Status}，仅草稿可删除");
        }

        await _definitionStore.DeleteAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkflowDefinition> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(id, nameof(id));
        return await _definitionStore.FindAsync(id, cancellationToken)
            ?? throw new WorkflowException($"流程定义 {id} 不存在");
    }

    /// <inheritdoc />
    public async Task<WorkflowDefinition> GetPublishedAsync(string code, int? version = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(code, nameof(code));

        if (version is { } value)
        {
            var definition = await _definitionStore.FindByVersionAsync(code, value, cancellationToken)
                ?? throw new WorkflowException($"流程定义 {code} v{value} 不存在");
            return definition.Status == WorkflowDefinitionStatus.Published
                ? definition
                : throw new WorkflowException($"流程定义 {code} v{value} 未发布");
        }

        return await _definitionStore.FindLatestPublishedAsync(code, cancellationToken)
            ?? throw new WorkflowException($"流程编码 {code} 不存在已发布版本");
    }

    /// <inheritdoc />
    public async Task<List<WorkflowDefinition>> GetListAsync(
        string? code = null,
        WorkflowDefinitionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        return await _definitionStore.GetListAsync(code, status, cancellationToken);
    }
}
