// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Docs.Mcp.Sources;

/// <summary>
/// 文档来源分类
/// </summary>
public enum DocSourceKind
{
    /// <summary>
    /// 使用指南，任务导向：怎么选、易错点、最佳实践（docs/guide）
    /// </summary>
    Guide,

    /// <summary>
    /// 包文档，API 参考：配置项、工作原理、完整清单（docs/packages）
    /// </summary>
    Package,

    /// <summary>
    /// 包自带的 README，简洁说明，与代码同步度最高（framework/src/*/README.md）
    /// </summary>
    PackageReadme,

    /// <summary>
    /// 文档站根目录的全局文档，如快速开始与更新日志（docs/*.md）
    /// </summary>
    Root
}
