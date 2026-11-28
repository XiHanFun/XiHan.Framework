# ADR-0352 Oracle 测试夹具删用户前先锁账户，清池之后补建的连接登录不进来

## 状态

已采纳（只改测试夹具：`Persistence.SqlSugar.Tests` 的 `OracleSchema`（`OracleTestServer` 只改注释）、`Workflow.Database.Tests` 的 `OracleSchema`；框架的包、公开面、错误码、消息键、配置键与规范都不变）。更正 ADR-0350 修订的判断：`ORA-00031` 不是偶然碰上 Oracle 一时结束不了会话，是清池之后驱动补建的连接正在登录时被杀。

## 日期

2026-11-29

## 背景

ADR-0350 修订（结束会话时放过 `ORA-00030` 与 `ORA-00031`）推送后，CI run 46173093985 的 oracle rest 分片 961 条里有 1 条失败：`SqlSugarUniqueKeyConflictTests.Add_OfAnUndeletedTwin_ReportsUniqueKeyConflict_OnTheSoftDeletableEntity_OnOracle` 在 `OracleSchema.Dispose` 的 `DROP USER` 报 `ORA-01940`（用户还连着），用例用了 11.5 秒，即 `EndSessions` 等满 10 秒会话仍在。上一次推送（run 46160646160）这条用例在 `EndSessions` 回 `ORA-00031` 失败。两次推送里另两个 oracle 分片与性能门禁都过。

## 查明的事实

- **用例释放时剩下的会话**（本机 Windows，临时 `gvenzl/oracle-free:23-slim`，ODP.NET 23.8.0；在 `EndSessions` 里临时记下 `v$session`，没有提交）：
  - `SqlSugarUniqueKeyConflictTests` 跑了 15 次。释放时还有会话要结束的只有这一条用例，每次 4～5 个（一次 2 个）；同一个类里另一条 Oracle 用例一次都没有。
  - 这些会话都是 `INACTIVE`，在等 `SQL*Net message from client`。最后执行的都是 ODP.NET 建连接时自己发的查询（`select decode(upper(failover_method) …`），没有执行过业务 SQL；其中几个在第一次查询之后才出现，是清池之后才建的。所以它们不是 Provider 漏还的连接。
  - 杀掉之后标成 `KILLED`，过 2937–3026 ms 才从 `v$session` 消失。
- **只用 ODP.NET 复现**（不经 SqlSugar；同一个连接串的池里并发开 8 个连接、各执行一句后清池，重复几轮）：
  - 第一轮清池之后会话都没了；之后几轮清池，0.15 秒内又出现 3～9 个会话，`Min Pool Size=0` 也一样。
  - 清池之后立刻照现在的做法杀会话：杀的过程中不断冒出新会话（清池时 5～6 个，前后共见到 9～10 个），3149–3253 ms 才清空。
  - 先锁账户、再清池、再杀：65–72 ms 清空，没有新会话。
  - 清池之后等 300 ms、会话都建好了再杀：锁不锁账户、用 `KILL SESSION` 还是 `DISCONNECT SESSION`，都在 75 ms 内清空。
- **结论**：多出的几秒来自清池之后驱动补建、正在登录的连接。杀到它们，会话要过一阵才消失；CI 上回过 `ORA-00031`（本机没回过）。CI 上 Oracle 与测试进程抢 CPU，这段等待超过了 10 秒。`OracleConnection.ClearAllPools()` 清的是整个进程的池，并行用例的池同样会补建连接。

## 备选

1. **删用户前先锁账户（`ALTER USER … ACCOUNT LOCK`），再清池、结束会话**：采纳。补建的连接登录不进来，剩下的会话都已建好，杀掉就消失。用户随后就删，锁住不影响别的用例。
2. **把等待上限从 10 秒调大**：否决。补建在等待期间一直进行，CI 上要等多久没有上限，只是把失败推远。
3. **只清自己连接串的池（`ClearPool`），不清整个进程**：不做。补建的正是自己的池，单独这样做不够；命名数据源等连接串不同的池也要清掉。锁住账户之后，别的用例清池也不会让正在释放的用户再建连接。
4. **删用户遇到 `ORA-01940` 就把用户留下、以后再删**：否决。理由同 ADR-0350 备选 6：会掩盖真正留着连接的问题。
5. **用 `DISCONNECT SESSION` 代替 `KILL SESSION`**：否决。探针里对已建好的会话两者一样快；正在登录的会话交给锁账户挡住。
6. **去掉对 `ORA-00030` 与 `ORA-00031` 的放过**：不做。锁账户之前已在登录的会话仍可能这样回；放过之后照常等会话消失，等不到时仍由 `DROP USER` 报错。

## 决策

1. 两个 Oracle 夹具释放时的顺序改为：
   1. 停止宿主（`Workflow.Database.Tests`），或宿主已由用例先释放（`Persistence.SqlSugar.Tests`）；
   2. 以管理账号执行 `ALTER USER … ACCOUNT LOCK`；
   3. 清池；
   4. 结束会话并等它们从 `v$session` 消失（ADR-0350 决策 1，不变）；
   5. `DROP USER … CASCADE`。

   管理客户端在锁账户之前建好，清池之后照样可用：每条语句都自己开关连接。
2. 注释照实改：结束会话时「期间新出现的会话」指锁账户之前已在登录的会话；夹具的说明写明先锁账户。

## 影响

- **范围**：只改测试夹具。ODP.NET 清池之后补建连接是驱动的行为，与框架无关；生产里不会在删用户之前清池。
- **验证**：
  - 本机 Windows，`SqlSugarUniqueKeyConflictTests` 改前改后各跑 5 次：
    - 改前：那条用例每次释放时等约 3 秒（4～5 个会话），整个类约 7 秒。
    - 先锁账户之后：释放时至多见到 1 个会话、约 30 ms 内消失，整个类约 3.9 秒。
  - 复现条件下（Linux、4 CPU、临时 Oracle）：
    - `Persistence.SqlSugar.Tests` 连跑三次，各 465 条 0 失败（31–34 秒）。
    - `Workflow.Database.Tests` 548 条 0 失败（1201 秒，ADR-0350 修订之后 1159 秒）。
  - 架构测试 97 条通过。
- **CI 时长**：那条用例每次释放少等约 3 秒，其余用例不变。

## 没有解决的

- 为什么清池之后只有这条用例的池补建连接、补建几个，没有查。这是驱动的内部行为，锁住账户之后与它无关。
- CI 的 oracle 分片仍要再看几次（待办「只能在 CI 上验证的」）。
