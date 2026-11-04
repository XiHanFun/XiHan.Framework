# XiHan.Framework.Auditing.Conformance

## 关于

`XiHan.Framework.Auditing.Conformance` 是审计提供程序的一致性测试套件，给写提供程序的人用：继承 `AuditTrailConformance` 并返回夹具，验证写入、过滤与分页行为与框架一致。

## 主要特性

* 写入后按写入顺序读回，字段完整往返
* 各过滤项取交集，分页与续页令牌
* 不合法查询返回校验错误

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Auditing.Conformance --prerelease
```

### 使用

在提供程序的测试项目里引用本包与 xUnit v3：

```csharp
public sealed class MyAuditTrailConformanceTests : AuditTrailConformance
{
    protected override Task<AuditTrailFixture> CreateFixtureAsync() => MyFixture.CreateAsync();
}
```

`AuditTrailFixture` 提供被测的 `IAuditSink`（`Sink`）与 `IAuditTrail`（`Trail`）；`MyFixture` 是你自己的夹具子类。

## 主要类型

此库提供的主要类型：

* `AuditTrailConformance`：测试基类
* `AuditTrailFixture`：夹具，提供被测接收器与查询

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/conformance)
* [一致性套件文档](https://framework.docs.xihanfun.com/packages/conformance)

## 相关包

* 抽象包：[XiHan.Framework.Auditing.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Auditing.Abstractions)

## 反馈与贡献

`XiHan.Framework.Auditing.Conformance` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
