// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Web.Api.SqlSugar.Entities;

namespace XiHan.Framework.Web.Api.SqlSugar.Tests;

/// <summary>
/// 接口幂等记录实体测试
/// </summary>
public class SysIdempotencyRecordTests : IDisposable
{
    private readonly string _databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_idem_{Guid.NewGuid():N}.db");

    /// <summary>
    /// 释放临时库文件
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            File.Delete(_databaseFile);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>
    /// 连续两次建表不抛出
    /// </summary>
    [Fact]
    public void InitTables_IsRepeatable()
    {
        var client = CreateClient();

        client.CodeFirst.InitTables(typeof(SysIdempotencyRecord));
        var exception = Record.Exception(() => client.CodeFirst.InitTables(typeof(SysIdempotencyRecord)));

        Assert.Null(exception);
    }

    /// <summary>
    /// 唯一索引拒绝重复的记录键摘要
    /// </summary>
    [Fact]
    public async Task UniqueIndex_RejectsDuplicateKeyHash()
    {
        var client = CreateClient();
        client.CodeFirst.InitTables(typeof(SysIdempotencyRecord));

        await client.Insertable(CreateRecord("hash-a")).ExecuteCommandAsync();
        await client.Insertable(CreateRecord("hash-b")).ExecuteCommandAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => client.Insertable(CreateRecord("hash-a")).ExecuteCommandAsync());
        Assert.Equal(2, await client.Queryable<SysIdempotencyRecord>().CountAsync());
    }

    private static SysIdempotencyRecord CreateRecord(string keyHash)
    {
        return new SysIdempotencyRecord(Guid.NewGuid())
        {
            KeyHash = keyHash,
            TenantId = string.Empty,
            SubjectId = "subject",
            HttpMethod = "POST",
            Endpoint = "/orders",
            IdempotencyKey = "key",
            Fingerprint = "fingerprint",
            Status = SysIdempotencyRecord.StatusProcessing,
            OwnerToken = Guid.NewGuid(),
            LeaseExpiresTime = DateTimeOffset.UtcNow.AddMinutes(1),
            CreatedTime = DateTimeOffset.UtcNow
        };
    }

    private SqlSugarClient CreateClient()
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"DataSource={_databaseFile};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });
    }
}
