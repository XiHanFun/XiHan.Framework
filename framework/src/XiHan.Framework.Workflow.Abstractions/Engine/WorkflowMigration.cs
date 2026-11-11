// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 迁移请求：把运行中的实例指向同名的另一个已发布版本
/// </summary>
/// <remarks>
/// 以后加字段只加初始化器属性、不改构造签名（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowMigration
{
    /// <summary>
    /// 创建迁移请求
    /// </summary>
    /// <param name="instanceId">实例 ID</param>
    /// <param name="targetDefinitionId">目标定义 ID</param>
    public WorkflowMigration(string instanceId, string targetDefinitionId)
    {
        InstanceId = instanceId;
        TargetDefinitionId = targetDefinitionId;
    }

    /// <summary>
    /// 实例 ID
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// 目标定义 ID
    /// </summary>
    public string TargetDefinitionId { get; }
}
