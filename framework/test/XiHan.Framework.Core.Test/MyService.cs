#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MyService
// Guid:db1233b0-7c16-4d2e-b8e4-bcc4f939dd2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/19 6:25:05
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Core.Test;

/// <summary>
/// MyService
/// </summary>
public class MyService
{
    private readonly ILogger<MyService> _logger;

    /// <summary>
    ///
    /// </summary>
    /// <param name="logger"></param>
    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public async Task RunAsync()
    {
        _logger.LogInformation("Running MyService...");
        await Task.Delay(1000); // 模拟异步操作
        _logger.LogInformation("MyService execution completed.");
    }
}