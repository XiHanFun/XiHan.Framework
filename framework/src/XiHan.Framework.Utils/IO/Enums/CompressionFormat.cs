#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CompressionFormat
// Guid:ab258a96-0366-4ff9-a6ae-f1fa8ac801a9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/4 3:39:04
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.IO.Enums;

/// <summary>
/// 压缩格式
/// </summary>
public enum CompressionFormat
{
    /// <summary>
    /// ZIP格式
    /// </summary>
    Zip,

    /// <summary>
    /// GZIP格式
    /// </summary>
    GZip,

    /// <summary>
    /// DEFLATE格式
    /// </summary>
    Deflate
}
