#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IFileVersioningService
// Guid:d373ad9e-b65e-4d02-ab4f-68c3ef77540a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/08/07 11:03:29
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;

namespace XiHan.Framework.VirtualFileSystem.Services;

/// <summary>
/// 文件版本控制服务
/// </summary>
public interface IFileVersioningService
{
    /// <summary>
    /// 回滚
    /// </summary>
    /// <param name="path"></param>
    /// <param name="steps"></param>
    /// <returns></returns>
    bool Rollback(string path, int steps = 1);

    /// <summary>
    /// 快照
    /// </summary>
    /// <param name="file"></param>
    void Snapshot(IFileInfo file);
}
