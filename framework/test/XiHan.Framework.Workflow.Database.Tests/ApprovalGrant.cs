// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 测试用的业务行：审批通过时由结果处理器写入的授予
/// </summary>
/// <remarks>
/// 与工作流的表在同一个库、同一个工作单元里，用来验证结论回写与实例完成一起提交或回滚
/// </remarks>
public sealed class ApprovalGrant : IEntity<string>
{
    /// <inheritdoc />
    [StringLength(128)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 授予给谁（流程实例 ID）
    /// </summary>
    [StringLength(128)]
    public string InstanceId { get; set; } = string.Empty;
}
