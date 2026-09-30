// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.SqlSugar.Entities;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 测试用的工作流实体类型清单
/// </summary>
internal static class TestEntityTypes
{
    /// <summary>
    /// 全部工作流实体类型
    /// </summary>
    public static Type[] All { get; } =
    [
        typeof(SysWorkflowDefinition),
        typeof(SysWorkflowInstance),
        typeof(SysWorkflowNodeInstance),
        typeof(SysWorkflowBookmark)
    ];
}
