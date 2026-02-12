#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullOperationLogWriter
// Guid:8b2c9cae-55df-4410-8f30-f90e85de3527
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 16:25:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging;

/// <summary>
/// 空操作日志写入器
/// </summary>
public class NullOperationLogWriter : IOperationLogWriter
{
    /// <inheritdoc />
    public Task WriteAsync(OperationLogRecord record, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
