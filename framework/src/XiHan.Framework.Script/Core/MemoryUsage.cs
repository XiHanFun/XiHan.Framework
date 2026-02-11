#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MemoryUsage
// Guid:f1b9f802-fbc0-43b4-ad7c-7a42a5e4f1b4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/31 06:12:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Core;

/// <summary>
/// 内存使用情况
/// </summary>
public class MemoryUsage
{
    /// <summary>
    /// 执行前内存使用量(字节)
    /// </summary>
    public long MemoryBefore { get; set; }

    /// <summary>
    /// 执行后内存使用量(字节)
    /// </summary>
    public long MemoryAfter { get; set; }

    /// <summary>
    /// 内存增长量(字节)
    /// </summary>
    public long MemoryIncrease => MemoryAfter - MemoryBefore;

    /// <summary>
    /// 垃圾回收次数
    /// </summary>
    public Dictionary<int, int> GcCollections { get; set; } = [];

    /// <summary>
    /// 创建内存使用记录
    /// </summary>
    /// <returns>内存使用记录</returns>
    public static MemoryUsage Create()
    {
        return new MemoryUsage
        {
            MemoryBefore = GC.GetTotalMemory(false),
            GcCollections = new Dictionary<int, int>
            {
                { 0, GC.CollectionCount(0) },
                { 1, GC.CollectionCount(1) },
                { 2, GC.CollectionCount(2) }
            }
        };
    }

    /// <summary>
    /// 完成内存使用记录
    /// </summary>
    public void Complete()
    {
        MemoryAfter = GC.GetTotalMemory(false);

        var currentGcCollections = new Dictionary<int, int>
        {
            { 0, GC.CollectionCount(0) },
            { 1, GC.CollectionCount(1) },
            { 2, GC.CollectionCount(2) }
        };

        // 计算GC增量
        foreach (var kvp in GcCollections.ToList())
        {
            GcCollections[kvp.Key] = currentGcCollections[kvp.Key] - kvp.Value;
        }
    }
}
