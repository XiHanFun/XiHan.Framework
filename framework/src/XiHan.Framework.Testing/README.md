# XiHan.Framework.Testing

## 关于

`XiHan.Framework.Testing` 是测试基座：没有 HTTP 的内存测试宿主、可注入行为的特性桩、按能力 ID 文本构造清单与独立调用上下文的工具。供框架、提供程序与应用的测试项目引用，也是各一致性套件装配宿主的基础。

## 主要特性

* `TestHost` 在内存里装配并启动应用，调用直接走完整管道
* `StubFeature` 注入配置、启动、停止行为并记录事件
* 不依赖宿主就能构造调用上下文
* 内存里的通行密钥认证器，按 WebAuthn 格式生成可篡改的响应
* `FaultyCache`：可以随时切换成「缓存后端不可达」的 `ICache` 包装，测组件在缓存故障时的行为
* `ScriptedUnitOfWork` 与 `ScriptedRepository<TEntity, TKey>`：按脚本在开始、读写、提交、回滚、释放上失败的替身，测自己开工作单元的代码在数据库故障时怎样收尾
* `LogCapture`：把日志收进内存的日志工厂，按级别、类别、消息与异常断言组件写了哪条日志
* `SpanCapture` 与 `MetricCapture`：按源名与 Meter 名监听，把 Span、采样器被询问的创建与测量值收进内存，有条数上限，按 trace-id 或异步流只取本用例的
* `SpecVectors` 与 `FrameworkWorkspace`：在框架仓库里定位工作区根与规范的一致性向量，按目录与文件名严格读成对象模型

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

在装配特性之前把 `FaultyCache` 登记为 `ICache` 单例，测试中途让缓存后端「断线」：

```csharp
var cache = new FaultyCache(innerCache);
await using var host = await TestHost.StartAsync(b => b.UseFeature(feature), services: s => s.AddSingleton<ICache>(cache));

// 之后每个 ICache 操作都抛出携带 CACHING_UNAVAILABLE_001 的 FrameworkException
cache.Failing = true;
```

几个宿主登记同一个实例时共用一份缓存，可以模拟多实例共用的缓存后端。

管道外自己开工作单元的代码（单例存储、后台循环）用两个替身测收尾：数据库断开时读取抛出、回滚再抛，结果不该被回滚的异常盖掉。

```csharp
var unitOfWork = new ScriptedUnitOfWork { RollbackFailure = new InvalidOperationException("The connection is closed.") };
await using var services = new ServiceCollection()
    .AddScoped<IUnitOfWork>(_ => unitOfWork)
    .AddScoped<IRepository<Order, Guid>>(_ => new ScriptedRepository<Order, Guid> { Failure = new TimeoutException() })
    .BuildServiceProvider();

// 跑被测代码之后：unitOfWork.Calls 是 ["begin", "rollback", "dispose"]
```

断言组件写了哪条日志：把 `LogCapture` 登记为 `ILoggerFactory`，或直接从它取日志器交给被测对象。

```csharp
using var logs = new LogCapture();
var purger = new OrderPurger(services, TimeProvider.System, logs.CreateLogger<OrderPurger>());

await purger.PurgeAsync("acme", CancellationToken.None);

Assert.Contains(logs.Entries, e => e.Level == LogLevel.Warning && e.Exception is TimeoutException);
```

断言组件开了哪条 Span、记了哪个指标：监听器是进程级的，Span 按本用例的 trace-id 取，指标限定到本用例的异步流。

```csharp
using var spans = new SpanCapture([FrameworkDiagnostics.SourceName]);
using var metrics = new MetricCapture([FrameworkDiagnostics.SourceName]);
metrics.RestrictToCurrentFlow();

var response = await invoker.InvokeAsync(context, request);

var span = Assert.Single(spans.Spans(context.TraceId));
Assert.Single(metrics.Entries, m => m.Instrument == "xihan.invocations");
```

在框架仓库里回放规范的一致性向量：读取是严格的，对象模型要认得文件里的每个成员（顶层的 `$comment` 除外），没有缺省值的构造参数与 `required` 成员都要出现。

```csharp
private sealed record CacheKeyVectors(int MaxBytes, CacheKeyCases Keys, CacheKeyCases Prefixes);

var vectors = SpecVectors.Read<CacheKeyVectors>("caching", "cache-keys.json");
```

## 主要类型

此库提供的主要类型：

* `TestHost`：内存测试宿主
* `StubFeature`：可注入行为并记录事件的特性桩
* `TestManifests`：用能力 ID 文本构造清单
* `TestInvocationContext`：不依赖宿主的调用上下文
* `WebAuthnTestAuthenticator`：测试用的通行密钥认证器，生成注册与认证响应
* `FaultyCache`：可切换成缓存后端不可达的 `ICache` 包装
* `ScriptedUnitOfWork`：按脚本失败并记录调用顺序的工作单元替身
* `ScriptedRepository<TEntity, TKey>`：按脚本返回或抛出的仓储替身
* `LogCapture`、`LogCaptureEntry`：收日志的工厂与一条日志（级别、类别、消息、异常）
* `SpanCapture`、`SpanSamplingRequest`：收 Span 的监听器与采样器被询问的一次创建（源、名字、种类、trace-id、创建时的标签）
* `MetricCapture`、`MetricCaptureEntry`：收测量值的监听器与一个测量值（Meter、仪表、值、标签）
* `SpecVectors`：规范向量的目录与严格读取，反射与源生成两种类型信息都行
* `FrameworkWorkspace`：框架工作区（`XiHan.Framework.slnx` 所在目录）的根

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/testing)


## 反馈与贡献

`XiHan.Framework.Testing` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
