#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UuidGenerator
// Guid:19580578-69cd-4da0-b4d1-6a68ba08538e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/28 19:32:56
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DistributedIds;

/// <summary>
/// UUID生成器
/// </summary>
public class UuidGenerator : IDistributedIdGenerator
{
    /// <summary>
    /// 获取下一个ID
    /// </summary>
    /// <returns>生成的UUID</returns>
    public long NextId()
    {
        return BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0);
    }

    /// <summary>
    /// 获取下一个ID（字符串形式）
    /// </summary>
    /// <returns>生成的UUID字符串</returns>
    public string NextIdString()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 从ID中提取时间戳
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>时间戳</returns>
    public DateTime ExtractTime(long id)
    {
        // UUID不包含时间信息，返回当前时间
        return DateTime.UtcNow;
    }

    /// <summary>
    /// 从ID中提取工作机器ID
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>工作机器ID</returns>
    public int ExtractWorkerId(long id)
    {
        // UUID不包含工作机器ID信息，返回0
        return 0;
    }

    /// <summary>
    /// 从ID中提取序列号
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>序列号</returns>
    public int ExtractSequence(long id)
    {
        // UUID不包含序列号信息，返回0
        return 0;
    }
}
