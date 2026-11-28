# ADR-0350 Oracle 测试夹具的临时用户用连接池，管理连接失败时等一会儿再试

## 状态

已采纳（只改测试夹具：`Persistence.SqlSugar.Tests` 的 `OracleTestServer` 与 `OracleSchema`、`Workflow.Database.Tests` 的 `OracleSchema`；框架的包、公开面、错误码、消息键、配置键与规范都不变）。更正 ADR-0338 决策 11 的前提：失败不是驱动在拒绝池里的请求，是监听程序暂时拒绝新连接。2026-11-28 修订：结束会话时回 `ORA-00030`、`ORA-00031` 不算失败（推送后 CI 的 oracle rest 分片有两条用例在这里失败）。ADR-0352 更正这次修订的判断：`ORA-00031` 来自清池之后驱动补建、正在登录的连接，删用户前改为先锁账户。

## 日期

2026-11-27

## 背景

第十一轮推送之后（框架 `a848fdf88`），CI 的「测试（oracle，workflow-a）」前两次运行都失败，第三次通过（run 46009131650）：

- **第 1 次**：第一条用例 `ExternalTask_FetchCompleteFailAndLock` 跑了 30 秒，在 `OracleSchema.DisposeAsync` 打开管理连接时报 `ORA-50201`。接着 15 条用例每条 3–10 ms 就失败，ADR-0338 的清池再试也一样失败；之后 8 条通过。
- **第 2 次**：同一条用例第 447 行的 `FailAsync` 没有成功；下一条在释放时报 `ORA-50201`；其余 19 条都在几毫秒内失败。

这正是第九轮 CI 记下、ADR-0338 以「清池再试」处理的那种偶发。第十轮在同一镜像（摘要相同，23.26.4）、同一运行器镜像上通过了三次。第十一轮没有改 `Workflow.Database` 与 Oracle 夹具；用例里的 `FailAsync` 直接调服务，不经第十一轮改过的 XGI 调用路径。

## 查明的事实

- **复现条件**：
  - 在本机 Windows（32 核）上照 CI 的过滤器与覆盖率跑这 24 条，全部通过。
  - 改在本地 Linux 镜像 `xihan-basicapp-backend:local` 里 `--cpus 4` 运行（CI 是 4 vCPU），与临时 Oracle 容器（本机已有的 `gvenzl/oracle-free:23-slim`，只绑回环，用完删除）共享网络，连接串用 `localhost:1521`，照样复现：第一条在释放时报 `ORA-50201`，任务类其余 8 条几毫秒内失败，引擎类 15 条通过。
- **真正的错误**：
  - SqlSugar 打开连接失败时不带内部异常，日志里只有 `ORA-50201`。
  - 在夹具失败处改为直接用 `OracleConnection` 打开，内层是 `ORA-12516`：监听程序报告没有可用的 TCP 协议处理器，或没有为服务 `FREEPDB1` 注册。
  - 每 500 ms 再试一次，下一次（约 540 ms 后）就成功。一次运行里出现 3 次，每次都在用例释放、宿主刚停之后。
- **不是进程数用尽**：实例的 `PROCESSES` 是 200，空闲时 94；运行中每 2 秒取样一次，最多 96（Windows）与 91（Linux）。
- **原因（推断）**：
  - 临时用户的连接串带 `Pooling=false`。这样做是为了删用户之前不留着闲着的池连接，代价是每次操作都新开、关闭一个专用服务器进程。
  - Oracle 监听程序在实例两次报告负载之间，把交出去的连接都算作负载，不扣掉已经断开的。估计值到了 `PROCESSES` 上限，它就把处理器标成阻塞、拒绝新连接，直到实例下一次报告。
  - 4 vCPU 的 Linux 上连接开关得快，估计值够得着上限；本机 Windows 上慢，够不着。这一段是按监听程序的已知行为推断的，没有直接观测监听程序的计数。
- **ADR-0338 的前提不成立**：它认为「像驱动在失败之后直接拒绝池里的请求」。实际上清池之后立即再试，照样落在同一段拒绝里；后面的用例也一样，所以一失败就是连串的几毫秒失败。
- **用连接池之后删用户要等会话结束**：
  - 两个夹具的释放本来就是先 `OracleConnection.ClearAllPools()`，再以管理账号结束这个用户的会话，最后 `DROP USER`。
  - 改用连接池之后，在复现条件下跑整个 `Persistence.SqlSugar.Tests`，`OracleSchemaConformanceTests.NamedDataSource_ReadsTheSameSchema` 在 `DROP USER` 报 `ORA-01940`（用户还连着）。
  - 原因：清池断开的连接与 `KILL SESSION … IMMEDIATE` 都不等会话真正结束；`Pooling=false` 时每条连接在释放时同步关掉，所以以前没遇到。
- **改后**：临时用户改用连接池，在同样的条件下（Linux、4 CPU）连跑两次 workflow-a 的 24 条，都是 0 失败、0 次连接被拒，用时 261 s 与 181 s（改前 272 s）。

## 备选

1. **临时用户改用连接池**：采纳。根因是连接开关得太频繁；释放时已经清池并结束会话。
2. **调大 CI 上 Oracle 的 `PROCESSES`**：否决。要改 spfile 并重启实例，服务容器里做不了，而且只是把上限推远。
3. **只把再试改成等一会儿**：单独否决。宿主自己的连接（如第 2 次运行里 `FailAsync` 用的）同样会被拒，用例照样失败。作为第二道保留，见决策 2。
4. **释放时的管理连接也再试**：不做。用了连接池，连接不再频繁开关，没有证据说明需要。
5. **再试时仍清池**：保留。代价为零，池里断了的连接不会再被拿到；但它不是这次偶发的解法。
6. **删用户遇到 `ORA-01940` 就再试**：否决。看不出是在等哪个会话；先等会话消失再删，等不到时的错误与以前一样。

## 决策

1. 两个 Oracle 夹具给临时用户的连接串去掉 `Pooling=false`，用驱动缺省的连接池。释放的顺序照旧：先清池，再结束会话，最后删用户；结束会话改为等它们从 `v$session` 里消失：
   - 每 100 ms 查一次：还没标成 `KILLED` 的会话结束掉，期间新出现的也算；已标成 `KILLED` 的只等它消失。
   - 最多等 10 秒，到时仍有会话就照常 `DROP USER`，由它报 `ORA-01940`。
   - 结束一个会话时回 `ORA-00030`（查完之后它自己没了）或 `ORA-00031`（一时结束不了、已标成 `KILLED`；带 `IMMEDIATE` 也会这样回）不算失败，照常等它消失（2026-11-28 修订）。
   - 管理连接用的同一个 `SqlSugarClient`，不另开连接。
2. 建用户时管理连接失败的处理：
   - 先等 2 秒（实测约半秒解除），再清池、换一个用户名再试一次。
   - 仍只试一次；两次的异常照旧以 `AggregateException` 报告，第一次在前。
   - 消息改为 `Creating the Oracle test user failed, and failed again after waiting and clearing the connection pool.`。
3. `OracleSchemaRetryTests` 与 `OracleTestServerRetryTests` 的断言不变（恰有两个 `SqlSugarException`）。每条多等 2 秒，本机约 4.3 秒。

## 影响

- **范围**：只改测试夹具，不改 Provider。生产里关掉 Oracle 连接池的应用，在频繁的短连接下同样会遇到 `ORA-12516`；这属于 Oracle 的配置，不在框架里处理。
- **验证**：
  - 两个夹具的再试用例在本机通过，各约 4.3 秒；架构测试 97 条通过。
  - 在复现条件下（Linux、4 CPU、临时 Oracle），整个程序集连同 Oracle 门控用例都跑过，覆盖 CI 的 rest、workflow-a、workflow-b 三个分片：`Persistence.SqlSugar.Tests` 连跑三次 465 条 0 失败（约 30 秒）；`Workflow.Database.Tests` 548 条 0 失败（656 秒）。
  - CI：推送后（run 46045245513）三个 oracle 分片首跑都过。下一次推送（run 46160646160）rest 分片两条用例在释放时失败：`OracleTestServer.EndSessions` 结束会话回 `ORA-00031`（会话已标成要结束），即 `OracleSchemaConformanceTests.NamedDataSource_ReadsTheSameSchema` 与 `SqlSugarUniqueKeyConflictTests.Add_OfAnUndeletedTwin_ReportsUniqueKeyConflict_OnTheSoftDeletableEntity_OnOracle`。改用连接池之前释放时几乎没有会话要结束，碰不到这一步。
  - 修订之后（2026-11-28）在复现条件下：`Persistence.SqlSugar.Tests` 连跑三次 465 条 0 失败（31–33 秒），`Workflow.Database.Tests` 548 条 0 失败（1159 秒，上次 656 秒；放过的两个错误码只在 Oracle 回它们时才多等，上次没有一条用例因它们失败，多出的时间多半是机器负载）。没写能复现 `ORA-00031` 的用例：要碰上 Oracle 一时结束不了会话的时刻，本机这几次都没碰上。
- **CI 时长**：在复现条件下，workflow-a 比改前快；再试只在失败时多等 2 秒；删用户只在会话还没消失时多等，每次至多 10 秒。

## 没有解决的

- `WorkflowTaskConformance` 第 447 行等处只断言 `IsSuccess`，失败时看不出错误码；这次是靠改夹具与复现才查到。要不要让套件的这类断言带上错误码，没有定。
- 监听程序计数的机制是推断的；若以后在用了连接池的情况下仍出现 `ORA-12516`，要直接看监听程序的日志（`lsnrctl services` 里处理器的 `blocked` 状态）。
