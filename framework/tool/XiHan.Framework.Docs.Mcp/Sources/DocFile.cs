// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Docs.Mcp.Sources;

/// <summary>
/// 一个被索引的文档文件
/// </summary>
/// <param name="AbsolutePath">磁盘上的绝对路径</param>
/// <param name="RelativePath">相对仓库根的路径，统一使用正斜杠，对外展示用</param>
/// <param name="Source">来源分类</param>
/// <param name="LastWriteUtc">最后写入时间，用于热更新判定</param>
public sealed record DocFile(
    string AbsolutePath,
    string RelativePath,
    DocSourceKind Source,
    DateTime LastWriteUtc);
