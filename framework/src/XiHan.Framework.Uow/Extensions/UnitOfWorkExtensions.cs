#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWorkExtensions
// Guid:4ef6c0d2-41c7-4cff-974b-96dfecffdf67
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 6:25:24
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.Diagnostics;
using XiHan.Framework.Utils.Objects;

namespace XiHan.Framework.Uow.Extensions;

/// <summary>
/// 工作单元扩展
/// </summary>
public static class UnitOfWorkExtensions
{
    /// <summary>
    /// 判断是否为指定的保留名称
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="reservationName"></param>
    /// <returns></returns>
    public static bool IsReservedFor(this IUnitOfWork unitOfWork, string reservationName)
    {
        Guard.NotNull(unitOfWork, nameof(unitOfWork));

        return unitOfWork.IsReserved && unitOfWork.ReservationName == reservationName;
    }

    /// <summary>
    /// 添加项目
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="unitOfWork"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public static void AddItem<TValue>(this IUnitOfWork unitOfWork, string key, TValue value)
        where TValue : class
    {
        Guard.NotNull(unitOfWork, nameof(unitOfWork));

        unitOfWork.Items[key] = value;
    }

    /// <summary>
    /// 获取项目或默认值
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="unitOfWork"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public static TValue GetItemOrDefault<TValue>(this IUnitOfWork unitOfWork, string key)
        where TValue : class
    {
        Guard.NotNull(unitOfWork, nameof(unitOfWork));

        return unitOfWork.Items.FirstOrDefault(x => x.Key == key).Value.As<TValue>();
    }

    /// <summary>
    /// 获取或添加项目
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="unitOfWork"></param>
    /// <param name="key"></param>
    /// <param name="factory"></param>
    /// <returns></returns>
    public static TValue GetOrAddItem<TValue>(this IUnitOfWork unitOfWork, string key, Func<string, TValue> factory)
        where TValue : class
    {
        Guard.NotNull(unitOfWork, nameof(unitOfWork));

        return unitOfWork.Items.GetOrAdd(key, factory).As<TValue>();
    }

    /// <summary>
    /// 移除项目
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="key"></param>
    public static void RemoveItem(this IUnitOfWork unitOfWork, string key)
    {
        Guard.NotNull(unitOfWork, nameof(unitOfWork));

        unitOfWork.Items.RemoveAllWhere(x => x.Key == key);
    }
}
