# XiHan.Framework.Testing

## 关于

`XiHan.Framework.Testing` 是测试基座：没有 HTTP 的内存测试宿主、可注入行为的特性桩、按能力 ID 文本构造清单与独立调用上下文的工具。供框架、提供程序与应用的测试项目引用，也是各一致性套件装配宿主的基础。

## 主要特性

* `TestHost` 在内存里装配并启动应用，调用直接走完整管道
* `StubFeature` 注入配置、启动、停止行为并记录事件
* 不依赖宿主就能构造调用上下文
* 内存里的通行密钥认证器，按 WebAuthn 格式生成可篡改的响应

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Testing --prerelease
```

### 使用

在测试里装配特性、读配置、走一次管道：

```csharp
var feature = new StubFeature(TestManifests.Create("xihan.test.stub", requires: ["kernel.hosting@1"]));
await using var host = await TestHost.StartAsync(
    b => b.UseFeature(feature),
    configuration: [new KeyValuePair<string, string?>("XiHan:Kernel:StartTimeoutSeconds", "5")]);

var result = await host.InvokeAsync(static context =>
{
    context.SetOutcome(Result.Success());
    return ValueTask.CompletedTask;
});
```

## 主要类型

此库提供的主要类型：

* `TestHost`：内存测试宿主
* `StubFeature`：可注入行为并记录事件的特性桩
* `TestManifests`：用能力 ID 文本构造清单
* `TestInvocationContext`：不依赖宿主的调用上下文
* `WebAuthnTestAuthenticator`：测试用的通行密钥认证器，生成注册与认证响应

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/testing)


## 反馈与贡献

`XiHan.Framework.Testing` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
