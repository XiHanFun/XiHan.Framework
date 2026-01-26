#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TestService
// Guid:t1e2s3t4-s5e6-4r7v-8i9c-0e1a2b3c4d5e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/26 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Services;
using XiHan.Framework.Web.Api.DynamicApi.Attributes;

namespace XiHan.Framework.Web.Tests.Services;

/// <summary>
/// 测试服务
/// </summary>
public class TestService : ApplicationServiceBase
{
    /// <summary>
    /// 获取测试消息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <returns>测试消息</returns>
    public string GetTestMessage(string message)
    {
        return $"This is a test message from TestService. Message: {message}";
    }
}
