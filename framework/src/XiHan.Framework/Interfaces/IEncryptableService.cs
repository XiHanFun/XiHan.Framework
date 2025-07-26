#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IEncryptableService
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5e9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Interfaces;

/// <summary>
/// 可加密服务接口
/// </summary>
public interface IEncryptableService : IFrameworkService
{
    /// <summary>
    /// 加密数据
    /// </summary>
    /// <param name="data">要加密的数据</param>
    /// <returns>加密结果</returns>
    string Encrypt(string data);

    /// <summary>
    /// 解密数据
    /// </summary>
    /// <param name="encryptedData">加密的数据</param>
    /// <returns>解密结果</returns>
    string Decrypt(string encryptedData);

    /// <summary>
    /// 生成哈希
    /// </summary>
    /// <param name="data">要哈希的数据</param>
    /// <returns>哈希结果</returns>
    string Hash(string data);
}
