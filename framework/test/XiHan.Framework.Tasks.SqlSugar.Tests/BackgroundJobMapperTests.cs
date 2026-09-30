// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.SqlSugar.Entities;
using XiHan.Framework.Tasks.SqlSugar.Mapping;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 后台作业映射测试
/// </summary>
public class BackgroundJobMapperTests
{
    /// <summary>
    /// 契约与实体往返后字段一致
    /// </summary>
    [Fact]
    public void 契约与实体往返后字段一致()
    {
        var info = new BackgroundJobInfo
        {
            Id = Guid.NewGuid(),
            ApplicationName = "Shop",
            TenantId = 42,
            JobName = "Order.Close",
            JobArgs = "{\"orderId\":1}",
            TryCount = 3,
            CreationTime = new DateTime(2030, 1, 1, 8, 0, 0),
            NextTryTime = new DateTime(2030, 1, 1, 9, 0, 0),
            LastTryTime = new DateTime(2030, 1, 1, 8, 30, 0),
            IsAbandoned = true,
            Priority = BackgroundJobPriority.High
        };

        var restored = BackgroundJobMapper.ToJobInfo(BackgroundJobMapper.ToEntity(info));

        Assert.Equal(info.Id, restored.Id);
        Assert.Equal("Shop", restored.ApplicationName);
        Assert.Equal(42L, restored.TenantId);
        Assert.Equal("Order.Close", restored.JobName);
        Assert.Equal("{\"orderId\":1}", restored.JobArgs);
        Assert.Equal((short)3, restored.TryCount);
        Assert.Equal(info.CreationTime, restored.CreationTime);
        Assert.Equal(info.NextTryTime, restored.NextTryTime);
        Assert.Equal(info.LastTryTime, restored.LastTryTime);
        Assert.True(restored.IsAbandoned);
        Assert.Equal(BackgroundJobPriority.High, restored.Priority);
    }

    /// <summary>
    /// 取消标记未请求时存为空，已请求时存为真，还原时空值视为未请求
    /// </summary>
    [Fact]
    public void 取消标记按可空字段映射()
    {
        var info = new BackgroundJobInfo { Id = Guid.NewGuid(), JobName = "Order.Close", JobArgs = "{}" };

        var notRequested = BackgroundJobMapper.ToEntity(info);
        info.IsCancellationRequested = true;
        var requested = BackgroundJobMapper.ToEntity(info);

        Assert.Null(notRequested.IsCancellationRequested);
        Assert.True(requested.IsCancellationRequested);
        Assert.False(BackgroundJobMapper.ToJobInfo(notRequested).IsCancellationRequested);
        Assert.True(BackgroundJobMapper.ToJobInfo(requested).IsCancellationRequested);
    }

    /// <summary>
    /// 实体的领取令牌映射到作业信息
    /// </summary>
    [Fact]
    public void 实体的领取令牌映射到作业信息()
    {
        var entity = new SysBackgroundJob(Guid.NewGuid())
        {
            JobName = "Order.Close",
            JobArgs = "{}",
            ClaimToken = "token-1"
        };

        Assert.Equal("token-1", BackgroundJobMapper.ToJobInfo(entity).ClaimToken);
    }

    /// <summary>
    /// 应用名为空时存为空字符串并还原为空
    /// </summary>
    [Fact]
    public void 应用名为空时存为空字符串并还原为空()
    {
        var info = new BackgroundJobInfo
        {
            Id = Guid.NewGuid(),
            ApplicationName = null,
            JobName = "Order.Close",
            JobArgs = "{}"
        };

        var entity = BackgroundJobMapper.ToEntity(info);
        var restored = BackgroundJobMapper.ToJobInfo(entity);

        Assert.Equal(string.Empty, entity.ApplicationName);
        Assert.Null(restored.ApplicationName);
    }

    /// <summary>
    /// 转换为实体时清空租约
    /// </summary>
    [Fact]
    public void 转换为实体时清空租约()
    {
        var info = new BackgroundJobInfo
        {
            Id = Guid.NewGuid(),
            JobName = "Order.Close",
            JobArgs = "{}",
            Priority = BackgroundJobPriority.High
        };

        var entity = BackgroundJobMapper.ToEntity(info);

        Assert.Null(entity.ClaimToken);
        Assert.Null(entity.ClaimTime);
        Assert.Equal(25, entity.Priority);
        Assert.Equal(info.Id, entity.BasicId);
    }

    /// <summary>
    /// 应用名键把空值转换为空字符串
    /// </summary>
    [Fact]
    public void 应用名键把空值转换为空字符串()
    {
        Assert.Equal(string.Empty, BackgroundJobMapper.ToApplicationKey(null));
        Assert.Equal("Shop", BackgroundJobMapper.ToApplicationKey("Shop"));
    }
}
