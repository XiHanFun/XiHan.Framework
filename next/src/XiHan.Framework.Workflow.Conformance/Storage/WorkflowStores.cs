// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.Conformance.Storage;

/// <summary>
/// 一次调用里解析出的三个存储，以及这次调用的分区。
/// </summary>
/// <param name="TenantId">分区：调用的租户，空为平台。</param>
/// <param name="Definitions">定义存储。</param>
/// <param name="Instances">实例存储。</param>
/// <param name="Bookmarks">书签存储。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowStores(string? TenantId, IWorkflowDefinitionStore Definitions, IWorkflowInstanceStore Instances, IWorkflowBookmarkStore Bookmarks);
