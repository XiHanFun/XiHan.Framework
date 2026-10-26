// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 迁移请求：把运行中的实例指向同名的另一个已发布版本。
/// </summary>
/// <param name="InstanceId">实例 ID。</param>
/// <param name="TargetDefinitionId">目标定义 ID。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowMigration(string InstanceId, string TargetDefinitionId);
