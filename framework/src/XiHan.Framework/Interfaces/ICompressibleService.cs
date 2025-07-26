#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICompressibleService
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5ea
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Interfaces;

/// <summary>
/// 可压缩服务接口
/// </summary>
public interface ICompressibleService : IFrameworkService
{
    /// <summary>
    /// 压缩数据
    /// </summary>
    /// <param name="data">要压缩的数据</param>
    /// <returns>压缩结果</returns>
    byte[] Compress(byte[] data);

    /// <summary>
    /// 解压数据
    /// </summary>
    /// <param name="compressedData">压缩的数据</param>
    /// <returns>解压结果</returns>
    byte[] Decompress(byte[] compressedData);

    /// <summary>
    /// 获取压缩率
    /// </summary>
    /// <param name="originalSize">原始大小</param>
    /// <param name="compressedSize">压缩后大小</param>
    /// <returns>压缩率</returns>
    double GetCompressionRatio(long originalSize, long compressedSize);
}
