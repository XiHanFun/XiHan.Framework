// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectStorage.Options;

/// <summary>
/// 本地存储配置
/// </summary>
public class LocalStorageOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:ObjectStorage:Local";

    /// <summary>
    /// 根目录路径（默认置于 Web 根 wwwroot 下，便于静态文件服务托管与直链）
    /// </summary>
    public string RootPath { get; set; } = "wwwroot/uploads";

    /// <summary>
    /// URL前缀
    /// </summary>
    public string UrlPrefix { get; set; } = "/uploads";
}
