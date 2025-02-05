#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanAIRemoteService
// Guid:655b4609-3d06-4174-bca2-ad03f8a5cf3a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/2 1:59:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Http.Polly;

namespace XiHan.Framework.AI.Providers;

/// <summary>
/// 基于远程的曦寒 AI 服务接口
/// </summary>
public interface IXiHanAIRemoteService
{
    /// <summary>
    /// 网络请求组别
    /// </summary>
    public HttpGroupEnum HttpGroup { get; set; }
}
