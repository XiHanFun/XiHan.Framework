// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Auditing.SqlSugar.Mapping;

namespace XiHan.Framework.Auditing.SqlSugar.Tests;

/// <summary>
/// 日志映射测试
/// </summary>
public class AuditingLogMapperTests
{
    private static readonly DateTimeOffset CreatedTime = new(2026, 9, 21, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void 操作日志映射保留全部字段()
    {
        var record = new OperationLogRecord
        {
            TraceId = "trace-1",
            SessionId = "session-1",
            UserId = 42,
            UserName = "tester",
            ControllerName = "Order",
            ActionName = "Create",
            Method = "POST",
            Path = "/Order",
            RequestParams = "{}",
            ResponseResult = "{\"ok\":true}",
            StatusCode = 200,
            ElapsedMilliseconds = 12,
            RemoteIp = "127.0.0.1",
            UserAgent = "xunit",
            ErrorMessage = null
        };

        var entity = AuditingLogMapper.ToEntity(record, 1001L, CreatedTime);

        Assert.Equal(1001L, entity.BasicId);
        Assert.Equal(CreatedTime, entity.CreatedTime);
        Assert.Equal("trace-1", entity.TraceId);
        Assert.Equal("session-1", entity.SessionId);
        Assert.Equal(42L, entity.UserId);
        Assert.Equal("tester", entity.UserName);
        Assert.Equal("Order", entity.ControllerName);
        Assert.Equal("Create", entity.ActionName);
        Assert.Equal("POST", entity.Method);
        Assert.Equal("/Order", entity.Path);
        Assert.Equal("{}", entity.RequestParams);
        Assert.Equal("{\"ok\":true}", entity.ResponseResult);
        Assert.Equal(200, entity.StatusCode);
        Assert.Equal(12L, entity.ElapsedMilliseconds);
        Assert.Equal("127.0.0.1", entity.RemoteIp);
        Assert.Equal("xunit", entity.UserAgent);
        Assert.Null(entity.ErrorMessage);
    }

    [Fact]
    public void 五类日志映射保留记录的租户标识()
    {
        Assert.Equal(7L, AuditingLogMapper.ToEntity(new AccessLogRecord { TenantId = 7 }, 1L, CreatedTime).TenantId);
        Assert.Equal(7L, AuditingLogMapper.ToEntity(new ApiLogRecord { TenantId = 7 }, 1L, CreatedTime).TenantId);
        Assert.Equal(7L, AuditingLogMapper.ToEntity(new ExceptionLogRecord { TenantId = 7 }, 1L, CreatedTime).TenantId);
        Assert.Equal(7L, AuditingLogMapper.ToEntity(new LoginLogRecord { TenantId = 7 }, 1L, CreatedTime).TenantId);
        Assert.Equal(7L, AuditingLogMapper.ToEntity(new OperationLogRecord { TenantId = 7 }, 1L, CreatedTime).TenantId);
    }

    [Fact]
    public void 五类日志的平台记录租户标识保持为空()
    {
        Assert.Null(AuditingLogMapper.ToEntity(new AccessLogRecord(), 1L, CreatedTime).TenantId);
        Assert.Null(AuditingLogMapper.ToEntity(new ApiLogRecord(), 1L, CreatedTime).TenantId);
        Assert.Null(AuditingLogMapper.ToEntity(new ExceptionLogRecord(), 1L, CreatedTime).TenantId);
        Assert.Null(AuditingLogMapper.ToEntity(new LoginLogRecord(), 1L, CreatedTime).TenantId);
        Assert.Null(AuditingLogMapper.ToEntity(new OperationLogRecord(), 1L, CreatedTime).TenantId);
    }

    [Fact]
    public void 登录日志的登录时间与创建时间各自独立()
    {
        var loginTime = new DateTimeOffset(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);
        var record = new LoginLogRecord
        {
            TraceId = "trace-2",
            UserId = 7,
            UserName = "tester",
            SessionId = "session-2",
            LoginResult = 1,
            Message = "成功",
            LoginIp = "10.0.0.1",
            UserAgent = "xunit",
            DeviceId = "device-1",
            LoginTime = loginTime
        };

        var entity = AuditingLogMapper.ToEntity(record, 1002L, CreatedTime);

        Assert.Equal(CreatedTime, entity.CreatedTime);
        Assert.Equal(loginTime, entity.LoginTime);
        Assert.Equal(1, entity.LoginResult);
        Assert.Equal("device-1", entity.DeviceId);
    }

    [Fact]
    public void 异常日志映射保留异常三要素()
    {
        var record = new ExceptionLogRecord
        {
            TraceId = "trace-3",
            StatusCode = 500,
            ExceptionType = "System.InvalidOperationException",
            ExceptionMessage = "boom",
            ExceptionStackTrace = "at X.Y()"
        };

        var entity = AuditingLogMapper.ToEntity(record, 1003L, CreatedTime);

        Assert.Equal("System.InvalidOperationException", entity.ExceptionType);
        Assert.Equal("boom", entity.ExceptionMessage);
        Assert.Equal("at X.Y()", entity.ExceptionStackTrace);
        Assert.Equal(500, entity.StatusCode);
    }

    [Fact]
    public void 接口日志映射保留签名校验结果()
    {
        var record = new ApiLogRecord
        {
            TraceId = "trace-4",
            ClientId = "client-1",
            AppId = "app-1",
            IsSignatureValid = false,
            SignatureAlgorithm = "HMACSHA256",
            Method = "GET",
            Path = "/Api",
            StatusCode = 401,
            IsSuccess = false
        };

        var entity = AuditingLogMapper.ToEntity(record, 1004L, CreatedTime);

        Assert.False(entity.IsSignatureValid);
        Assert.Equal("HMACSHA256", entity.SignatureAlgorithm);
        Assert.False(entity.IsSuccess);
        Assert.Equal("client-1", entity.ClientId);
    }

    [Fact]
    public void 访问日志映射保留响应大小与耗时()
    {
        var record = new AccessLogRecord
        {
            TraceId = "trace-5",
            ResourceName = "Home",
            Method = "GET",
            Path = "/",
            QueryString = "?a=1",
            StatusCode = 200,
            ElapsedMilliseconds = 8,
            ResponseSize = 2048
        };

        var entity = AuditingLogMapper.ToEntity(record, 1005L, CreatedTime);

        Assert.Equal("Home", entity.ResourceName);
        Assert.Equal("?a=1", entity.QueryString);
        Assert.Equal(8L, entity.ElapsedMilliseconds);
        Assert.Equal(2048L, entity.ResponseSize);
    }

    [Fact]
    public void 超出列宽的文本按列宽截断()
    {
        var record = new AccessLogRecord
        {
            TraceId = new string('t', 200),
            ResourceName = new string('n', 600),
            Method = new string('m', 40),
            Path = new string('p', 900),
            QueryString = new string('q', 5000),
            RemoteIp = new string('1', 200),
            UserAgent = new string('u', 5000),
            Referer = new string('r', 900)
        };

        var entity = AuditingLogMapper.ToEntity(record, 1006L, CreatedTime);

        Assert.Equal(64, entity.TraceId.Length);
        Assert.Equal(256, entity.ResourceName!.Length);
        Assert.Equal(16, entity.Method.Length);
        Assert.Equal(512, entity.Path.Length);
        Assert.Equal(2048, entity.QueryString!.Length);
        Assert.Equal(64, entity.RemoteIp!.Length);
        Assert.Equal(512, entity.UserAgent!.Length);
        Assert.Equal(512, entity.Referer!.Length);
        Assert.StartsWith("qqq", entity.QueryString);
    }

    [Fact]
    public void 截断不会在代理对中间切开()
    {
        var record = new AccessLogRecord
        {
            TraceId = new string('t', 63) + "😀",
            Method = "GET",
            Path = "/"
        };

        var entity = AuditingLogMapper.ToEntity(record, 1007L, CreatedTime);

        Assert.Equal(63, entity.TraceId.Length);
        Assert.DoesNotContain(entity.TraceId, char.IsSurrogate);
    }

    [Fact]
    public void 代理对正好落在列宽内时完整保留()
    {
        var record = new AccessLogRecord
        {
            TraceId = new string('t', 62) + "😀",
            Method = "GET",
            Path = "/"
        };

        var entity = AuditingLogMapper.ToEntity(record, 1008L, CreatedTime);

        Assert.Equal(64, entity.TraceId.Length);
        Assert.EndsWith("😀", entity.TraceId);
    }

    [Fact]
    public void 未超列宽的文本与空值原样保留()
    {
        var record = new AccessLogRecord
        {
            TraceId = "trace-short",
            QueryString = "?a=1",
            UserAgent = null
        };

        var entity = AuditingLogMapper.ToEntity(record, 1007L, CreatedTime);

        Assert.Equal("trace-short", entity.TraceId);
        Assert.Equal("?a=1", entity.QueryString);
        Assert.Null(entity.UserAgent);
    }

    [Fact]
    public void 实体差异日志映射保留全部字段()
    {
        var record = new EntityDiffLogRecord
        {
            AuditType = "EntityChange",
            OperationType = "Update",
            EntityType = "Order",
            EntityId = "1001",
            BeforeData = "{\"Status\":\"Pending\"}",
            AfterData = "{\"Status\":\"Paid\"}",
            ChangedFields = "[{\"Field\":\"Status\",\"Before\":\"Pending\",\"After\":\"Paid\"}]",
            RequestPath = "/api/orders/1001",
            RequestMethod = "PUT",
            OperationIp = "127.0.0.1",
            RequestId = "req-1",
            UserId = 42,
            UserName = "tester",
            TenantId = 7
        };

        var entity = AuditingLogMapper.ToEntity(record, 1009L, CreatedTime);

        Assert.Equal(1009L, entity.BasicId);
        Assert.Equal(CreatedTime, entity.CreatedTime);
        Assert.Equal("EntityChange", entity.AuditType);
        Assert.Equal("Update", entity.OperationType);
        Assert.Equal("Order", entity.EntityType);
        Assert.Equal("1001", entity.EntityId);
        Assert.Equal("{\"Status\":\"Pending\"}", entity.BeforeData);
        Assert.Equal("{\"Status\":\"Paid\"}", entity.AfterData);
        Assert.Equal("[{\"Field\":\"Status\",\"Before\":\"Pending\",\"After\":\"Paid\"}]", entity.ChangedFields);
        Assert.Equal("/api/orders/1001", entity.RequestPath);
        Assert.Equal("PUT", entity.RequestMethod);
        Assert.Equal("127.0.0.1", entity.OperationIp);
        Assert.Equal("req-1", entity.RequestId);
        Assert.Equal(42L, entity.UserId);
        Assert.Equal("tester", entity.UserName);
        Assert.Equal(7L, entity.TenantId);
    }

    [Fact]
    public void 实体差异日志未显式设置审计类型时保留默认值()
    {
        var record = new EntityDiffLogRecord
        {
            OperationType = "Create",
            EntityType = "Order"
        };

        var entity = AuditingLogMapper.ToEntity(record, 1010L, CreatedTime);

        Assert.Equal("EntityChange", entity.AuditType);
    }

    [Fact]
    public void 实体差异日志的大文本字段不截断()
    {
        var longJson = "{\"Data\":\"" + new string('x', 5000) + "\"}";
        var record = new EntityDiffLogRecord
        {
            OperationType = "Update",
            EntityType = "Order",
            BeforeData = longJson,
            AfterData = longJson,
            ChangedFields = longJson
        };

        var entity = AuditingLogMapper.ToEntity(record, 1011L, CreatedTime);

        Assert.Equal(longJson, entity.BeforeData);
        Assert.Equal(longJson, entity.AfterData);
        Assert.Equal(longJson, entity.ChangedFields);
    }

    [Fact]
    public void 实体差异日志超长定长字段按列宽截断()
    {
        var record = new EntityDiffLogRecord
        {
            OperationType = new string('o', 40),
            EntityType = new string('e', 600),
            RequestPath = new string('p', 900)
        };

        var entity = AuditingLogMapper.ToEntity(record, 1012L, CreatedTime);

        Assert.Equal(16, entity.OperationType.Length);
        Assert.Equal(256, entity.EntityType.Length);
        Assert.Equal(512, entity.RequestPath!.Length);
        Assert.StartsWith("ooo", entity.OperationType);
        Assert.StartsWith("eee", entity.EntityType);
        Assert.StartsWith("ppp", entity.RequestPath);
    }
}
