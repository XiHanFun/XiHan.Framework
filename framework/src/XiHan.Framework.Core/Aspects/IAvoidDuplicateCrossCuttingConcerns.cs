// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
