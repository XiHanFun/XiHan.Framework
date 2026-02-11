#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAvoidDuplicateCrossCuttingConcerns
// Guid:5b17bf36-a246-42bb-a64b-de957f6bfbb6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/13 01:52:55
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Aspects;

/// <summary>
/// 避免重复横切关注点
/// </summary>
public interface IAvoidDuplicateCrossCuttingConcerns
{
    /// <summary>
    /// 应用的横切关注点
    /// </summary>
    List<string> AppliedCrossCuttingConcerns { get; }
}
