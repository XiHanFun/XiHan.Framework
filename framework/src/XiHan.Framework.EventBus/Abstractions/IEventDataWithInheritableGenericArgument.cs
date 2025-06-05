#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IEventDataWithInheritableGenericArgument
// Guid:7c258914-a698-4d7e-94a7-a1596d596f80
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 8:14:20
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.EventBus.Abstractions;

/// <summary>
/// 具有单个泛型参数且该参数将用于继承的事件数据类
/// 必须实现此接口。
///
/// 例如；
/// 假设 Student 继承自 Person。当触发 EntityCreatedEventData{Student} 时，
/// 如果 EntityCreatedEventData 实现此接口，EntityCreatedEventData{Person} 也会被触发。
/// </summary>
public interface IEventDataWithInheritableGenericArgument
{
    /// <summary>
    /// 获取创建此类的参数，因为将创建此类的新实例。
    /// </summary>
    /// <returns>构造函数参数</returns>
    object[] GetConstructorArgs();
}
