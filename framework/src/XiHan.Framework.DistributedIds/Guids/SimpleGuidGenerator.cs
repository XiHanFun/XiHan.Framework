#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SimpleGuidGenerator
// Guid:4ea92ed7-56c0-457b-83dd-50a5175a2587
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 5:36:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DistributedIds.Guids;

/// <summary>
/// 简单 GUID 生成器
/// </summary>
public class SimpleGuidGenerator : IGuidGenerator
{
    /// <summary>
    /// 单例实例
    /// </summary>
    public static SimpleGuidGenerator Instance { get; } = new SimpleGuidGenerator();

    /// <summary>
    /// 创建一个新的 GUID
    /// </summary>
    /// <returns></returns>
    public virtual Guid Create()
    {
        return Guid.NewGuid();
    }
}
