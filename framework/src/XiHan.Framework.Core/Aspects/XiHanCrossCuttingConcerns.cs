#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanCrossCuttingConcerns
// Guid:ee52c9f2-2d08-45a6-bb2f-dc4973238e8d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/13 1:54:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Core.Aspects;

/// <summary>
/// 曦寒横切关注点
/// </summary>
public class XiHanCrossCuttingConcerns
{
    /// <summary>
    /// 添加已应用的横切关注点
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="concerns"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void AddApplied(object obj, params string[] concerns)
    {
        if (concerns.IsNullOrEmpty())
        {
            throw new ArgumentNullException(nameof(concerns), $"未提供 {nameof(concerns)}!");
        }

        (obj as IAvoidDuplicateCrossCuttingConcerns)?.AppliedCrossCuttingConcerns.AddRange(concerns);
    }

    /// <summary>
    /// 移除已应用的横切关注点
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="concerns"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void RemoveApplied(object obj, params string[] concerns)
    {
        if (concerns.IsNullOrEmpty())
        {
            throw new ArgumentNullException(nameof(concerns), $"未提供 {nameof(concerns)}!");
        }

        if (obj is not IAvoidDuplicateCrossCuttingConcerns crossCuttingEnabledObj)
        {
            return;
        }

        foreach (var concern in concerns)
        {
            _ = crossCuttingEnabledObj.AppliedCrossCuttingConcerns.RemoveAll(c => c == concern);
        }
    }

    /// <summary>
    /// 是否已应用
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="concern"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static bool IsApplied([NotNull] object obj, [NotNull] string concern)
    {
        return obj == null
            ? throw new ArgumentNullException(nameof(obj))
            : concern == null
            ? throw new ArgumentNullException(nameof(concern))
            : (obj as IAvoidDuplicateCrossCuttingConcerns)?.AppliedCrossCuttingConcerns.Contains(concern) ?? false;
    }

    /// <summary>
    /// 应用
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="concerns"></param>
    /// <returns></returns>
    public static IDisposable Applying(object obj, params string[] concerns)
    {
        AddApplied(obj, concerns);
        return new DisposeAction<ValueTuple<object, string[]>>(static (state) =>
        {
            var (obj, concerns) = state;
            RemoveApplied(obj, concerns);
        }, (obj, concerns));
    }

    /// <summary>
    /// 获取已应用的横切关注点
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static string[] GetApplieds(object obj)
    {
        return obj is not IAvoidDuplicateCrossCuttingConcerns crossCuttingEnabledObj
            ? []
            : crossCuttingEnabledObj.AppliedCrossCuttingConcerns.ToArray();
    }
}
