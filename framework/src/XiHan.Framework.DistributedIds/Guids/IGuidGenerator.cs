#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IGuidGenerator
// Guid:b6e24e47-1df6-43e9-8bc8-503014e91681
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 5:35:32
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DistributedIds.Guids;

/// <summary>
/// GUID 生成器接口
/// </summary>
public interface IGuidGenerator
{
    /// <summary>
    /// 创建一个新的 GUID
    /// </summary>
    Guid Create();
}
