// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Architecture.Tests.ProjectGraph;

/// <summary>
/// 架构违规类别
/// </summary>
internal enum ArchitectureViolationKind
{
    /// <summary>
    /// 源码项目未登记到分层目录
    /// </summary>
    UnregisteredLayer,

    /// <summary>
    /// 引用了依赖图中不存在的项目
    /// </summary>
    UnknownProject,

    /// <summary>
    /// 源码项目引用了测试、示例或工具项目
    /// </summary>
    SourceReferencesNonSource,

    /// <summary>
    /// 未登记例外的向上引用
    /// </summary>
    UpwardReference,

    /// <summary>
    /// 契约包引用了自身的实现包
    /// </summary>
    AbstractionsReferencesImplementation,

    /// <summary>
    /// 例外清单中已不成立的条目
    /// </summary>
    StaleException,

    /// <summary>
    /// 循环依赖
    /// </summary>
    Cycle
}

/// <summary>
/// 一条架构违规
/// </summary>
/// <param name="Kind">违规类别</param>
/// <param name="Message">包含起止项目与修正方向的说明</param>
internal sealed record ArchitectureViolation(ArchitectureViolationKind Kind, string Message);
