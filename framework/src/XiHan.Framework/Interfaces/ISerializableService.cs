#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISerializableService
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5e8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Interfaces;

/// <summary>
/// 可序列化服务接口
/// </summary>
public interface ISerializableService : IFrameworkService
{
    /// <summary>
    /// 序列化对象
    /// </summary>
    /// <param name="obj">要序列化的对象</param>
    /// <returns>序列化结果</returns>
    string Serialize(object obj);

    /// <summary>
    /// 反序列化对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="data">序列化数据</param>
    /// <returns>反序列化结果</returns>
    T? Deserialize<T>(string data);
}
