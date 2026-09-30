// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Architecture.Tests.ProjectGraph;

/// <summary>
/// 项目所在区域
/// </summary>
internal enum ProjectArea
{
    /// <summary>
    /// framework/src 下的源码项目
    /// </summary>
    Source,

    /// <summary>
    /// framework/test 下的测试项目
    /// </summary>
    Test,

    /// <summary>
    /// framework/sample 下的示例项目
    /// </summary>
    Sample,

    /// <summary>
    /// framework/tool 下的工具项目
    /// </summary>
    Tool,

    /// <summary>
    /// 其他位置的项目
    /// </summary>
    Other
}

/// <summary>
/// slnx 中 /1.src/ 下的一个分层目录
/// </summary>
/// <param name="Rank">层序号，越小越底层</param>
/// <param name="FolderName">目录名去掉序号后的部分</param>
internal sealed record ProjectLayer(int Rank, string FolderName);

/// <summary>
/// 依赖图中的一个项目
/// </summary>
/// <param name="Name">项目名（csproj 文件名，不含扩展名）</param>
/// <param name="Area">所在区域</param>
/// <param name="Layer">所在分层，未登记到分层目录时为 null</param>
/// <param name="RelativePath">csproj 相对 framework 目录的路径，以 / 分隔，合成图中可为 null</param>
internal sealed record ProjectNode(string Name, ProjectArea Area, ProjectLayer? Layer, string? RelativePath = null);

/// <summary>
/// 一条项目引用依赖边，分析器引用不计入
/// </summary>
/// <param name="From">引用方项目名</param>
/// <param name="To">被引用项目名</param>
internal sealed record ProjectEdge(string From, string To);
