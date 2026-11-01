# ADR-0086 `xihan pack` 的 S3 依赖服务换成 RustFS、选项打印遮蔽凭据、只装 OpenAIFeature 时工具名启动校验

## 状态

已采纳（`spec/packaging/app-manifest.md`、`spec/rfcs/0016-packaging.md`、`spec/rfcs/0012-objectstorage.md` 随之修订；修订 ADR-0082 第二节）。

## 日期

2026-11-01

## 背景

第三批留下的几项待定由用户逐项拍板：MinIO 镜像拉不到，`xihan pack` 的 `blob` 依赖服务换成另一个能拉取的 S3 兼容服务；选项记录的 `ToString()` 会打印口令；只装 `OpenAIFeature` 时工具名仍到第一次聊天才校验。本 ADR 每项一节。

## `xihan pack` 的 `blob` 依赖服务换成 RustFS

### 背景

`xihan pack` 为 `blob` 绑定生成的 Compose 服务与 Aspire 容器用的是 `minio/minio`。MinIO 社区版已改为只发源码（仓库归档，维护者答复要容器的自行构建），Docker Hub 上的 `minio/minio` 整个仓库已不存在，任何标签都拉不到、拿不到摘要（ADR-0083 时的核实：Docker Hub 与 registry 都返回仓库不存在或拒绝访问）；生成物里的 `blob` 依赖服务实际已不可用，`BindingKindTests` 也只能豁免它的固定版本检查。框架的 S3 Provider 只要 S3 协议，本地联调换哪个兼容服务都行，前提是框架 blob 依赖用到的能力都对：条件写入（`If-None-Match: *`，单次写入与分片完成两处）、范围读取、批量删除、列举、分片上传（乱序、同一编号重传覆盖、不合法的分片清单被拒绝、放弃）与预签名（下载、上传、分片地址）。

### 备选与实测

本机 Docker 29.6.2 从 Docker Hub 拉取各候选的最新稳定标签，起单节点容器（非默认端口），把 `ObjectStorage.S3.Tests`（39 例：18 例不连服务，21 例经 `XIHAN_TEST_S3` 连真实服务，含 `BlobStoreConformance` 与 `BlobStoreProviderConformance` 全部用例、预签名直连、命名实例、健康项与缺桶语义）指向它跑一遍；另用 AWS SDK 4.0.102.3（与框架同版本、同样 `WHEN_REQUIRED` 校验和）单独探测几处语义。

| 候选 | 镜像与摘要 | 许可证 | 起服务 | 凭据 | 健康检查 | S3 用例 | 实测差异 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| RustFS | `rustfs/rustfs:1.0.4`，`sha256:ed9210d6411d77ffceb70d94b964a3cb7d225983368bc25bebe01e995293dcc2` | Apache-2.0 | 不需配置，缺省以 `/data` 启动并自建目录，约 0.9 秒就绪 | `RUSTFS_ACCESS_KEY` / `RUSTFS_SECRET_KEY`（不设时为 `rustfsadmin`） | 内建 `/health`，不要求认证；镜像带 `curl`、`wget` | **39 / 39** | 无；条件写入（单次与分片完成）都返回 412，未知桶写入 404 `NoSuchBucket`。镜像 419 MB，空载约 150 MiB |
| Versity S3 Gateway | `versity/versitygw:v1.10.0`，`sha256:89cad13cc981986ee284cbafea4613f9533ce6fd5f37a2c96c4c4fb8adb3a316` | Apache-2.0 | 需 `VGW_BACKEND=posix` 与 `VGW_BACKEND_ARG`；后端目录必须已存在（`/data` 不挂卷时 `chdir /data` 失败退出） | `ROOT_ACCESS_KEY` / `ROOT_SECRET_KEY` | 设 `VGW_HEALTH=/health` 才有；镜像带 `wget` | **39 / 39** | 无功能差异；POSIX 后端把元数据放在文件的扩展属性里。镜像 117 MB，空载约 11 MiB |
| SeaweedFS | `chrislusf/seaweedfs:4.51`，`sha256:f7a49e3f3972ebed5be2ed4befa4a0c68eb7bdddba6e692d42729ceb545134b8` | Apache-2.0 | `weed mini -dir=/data` 一进程 | `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` | — | 37 / 39 | 同一编号重传后 `ListParts` 列出两条（`1:5, 2:4, 2:6`），续传按列表完成会带上重复分片；写入不存在的桶不报 `NoSuchBucket` 而是自动建桶 |
| Garage | `dxflrs/garage:v2.5.0`，`sha256:677e5b758e4425a325b811c425c026bf35a4bf42f7ddbaa773de3f4fd779a5a5` | AGPL-3.0 | 需配置文件（`rpc_secret`、目录、`s3_region`）再加 `--single-node --default-access-key` | `GARAGE_DEFAULT_ACCESS_KEY` / `GARAGE_DEFAULT_SECRET_KEY`（ID 须 `GK` + 24 位十六进制，密钥 64 位十六进制） | — | 38 / 39 | **不认 `If-None-Match`**：已存在的键 `PutObject` 与带条件的 `CompleteMultipartUpload` 都静默覆盖（用例只因框架先 HEAD 才通过，并发写入不原子）；不限制非末片的 5 MiB 下限 |
| Zenko CloudServer | `zenko/cloudserver:8.2.7`，`sha256:44ef9109901a223aca3151d6f980bf1995b55e80301965219c1fa512ba94d4e0`（Docker Hub 上最新的版本标签，构建于 2020-11） | Apache-2.0 | 环境变量即可 | `SCALITY_ACCESS_KEY_ID` / `SCALITY_SECRET_ACCESS_KEY` | — | 35 / 39 | `DeleteObjects` 报 Content-MD5 不符（与 AWS SDK v4 的请求校验和不兼容，批量删除不可用）；不认 `If-None-Match`；对已完成的上传再列分片或再传分片，返回的不是 `NoSuchUpload`（框架只能映射为服务不可用） |
| LocalStack | `localstack/localstack:2026.10.4`，`sha256:2290500d7398e6458544fab2f49aaf170b81a1006ee69067e8e7fc59b810a824` | 需许可证 | 启动即退出（代码 55）："License activation failed"，要求 `LOCALSTACK_AUTH_TOKEN` | — | — | 跑不起来 | 本地联调不能要求账号令牌 |
| Adobe S3Mock | `adobe/s3mock:5.2.3`，`sha256:ab01a6946750f451ca215a47e91030695b260e4003b8a5a6201d25029b8fca92` | Apache-2.0 | 环境变量即可 | 不校验凭据 | — | 38 / 39 | 只完成一个分片（它就是末片）也按 5 MiB 下限拒绝；不鉴权，测不出凭据注入是否正确；定位是测试替身 |

另两条路没有走：第三方冻结的 MinIO 镜像（如 `bitnamilegacy/minio`，第三批对它 39 / 39）非官方、已不再更新，安全修复要使用者自己跟；按 MinIO 官方说明自行构建镜像则要求每个使用者先构建、再在清单里写镜像名，`xihan pack` 不再开箱可用。

RustFS 与 Versity 的凭据都确实生效：用错的密钥跑同一套用例，两者都是 20 例失败（鉴权被拒），只有不依赖凭据的用例通过。

### 决策

- `blob` 的依赖服务换成 RustFS，镜像固定为 `rustfs/rustfs:1.0.4`（摘要见上表）。Compose：`RUSTFS_ACCESS_KEY=xihan`、`RUSTFS_SECRET_KEY=xihanxihan`，healthcheck `curl -fs http://localhost:9000/health`（`interval` 5 秒），数据目录挂命名卷 `<绑定名>-data:/data`，不写 `command`。Aspire：`AddContainer(<绑定名>, "rustfs/rustfs", "1.0.4")`，同一对凭据以 `WithEnvironment` 设置，HTTP 端点名仍是 `api`。
- 注入应用的内容不变：主值 `http://<绑定名>:9000`，绑定属性 `accessKey` / `secretKey` 仍是 `xihan` / `xihanxihan`（ADR-0082 第二节定的属性名，与 S3 Provider 读取的绑定属性一致）。
- 依赖服务镜像一律固定版本，`BindingKindTests.EveryBindingKind_PinsItsComposeImageTag` 不再豁免 `blob`。`BindingKind` 新增 `ImageName` / `ImageTag`（Aspire 的容器资源分开传镜像名与标签，Nacos 一并改为从同一处取），内部常量 `MinioUser` / `MinioPassword` 改名 `BlobAccessKey` / `BlobSecretKey`。
- RFC-0016 新增规则 6，写明替换 `blob` 依赖服务的合格条件：公开仓库可拉取的固定版本、环境变量给出访问密钥、单节点即可启动且有健康端点、S3 Provider 的一致性用例全部通过；不认条件写入的服务不合格（并发的不覆盖写入会静默覆盖）。
- S3 Provider 的测试缺省密钥由 `minioadmin` 改为 `rustfsadmin`，`docker run -p 9000:9000 rustfs/rustfs:1.0.4` 加 `XIHAN_TEST_S3=http://localhost:9000` 即可跑全部用例；RFC-0012 的一致性验证改记为对 RustFS 通过。

### 理由

- RustFS 与 Versity 都让 39 例全部通过，条件写入、分片同号重传、不合法清单、预签名都对。选 RustFS 是因为它零配置：只要两项凭据，镜像缺省就以 `/data` 起服务并自建目录，内建不要求认证的 `/health`；Versity 还要配后端类型、已存在的后端目录与健康路径，Aspire 的容器不挂卷时直接起不来。RustFS 的运维形态与 MinIO 相同（端口 9000、根凭据环境变量、`/data` 卷），生成物注入的主值与 Aspire 端点名都不变，已经写好的应用配置不用改。
- 其余候选各有一处框架用得到的能力不对：Garage 与 CloudServer 不认 `If-None-Match`，框架的“不覆盖”只剩先 HEAD 的检查，两个进程同时写同一个键时后者静默覆盖；SeaweedFS 的分片列表在重传后重复；CloudServer 的批量删除与当前 AWS SDK 不兼容；LocalStack 要账号令牌；S3Mock 不鉴权且分片下限判断有误。这些都是服务端的限制或缺陷，不是框架的问题，框架不为它们加兼容分支。
- 代价是镜像较大（419 MB 对 Versity 的 117 MB）、空载内存较高（约 150 MiB 对 11 MiB），RustFS 1.0 发布时间也不长。本地联调里这些可以接受；Versity 作为同样通过的备选记在这里，RustFS 出问题时可以按规则 6 替换。

### 影响

- `xihan pack` 重新生成后，Compose 的 `blob` 服务从 `minio/minio`（拉不到）变为 `rustfs/rustfs:1.0.4`，环境变量从 `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` 变为 `RUSTFS_ACCESS_KEY` / `RUSTFS_SECRET_KEY`，去掉 `command`，healthcheck 改为请求 `/health`；Aspire 的 `blob` 容器带固定标签并在同一行设凭据，原来单独一行的 `blob.WithEnvironment("MINIO_ROOT_USER", …)` 去掉。Kustomize 产物不变（不起依赖服务）。旧命名卷里的 MinIO 数据 RustFS 不读，本地联调删卷重建即可。
- RustFS 不自动建桶：应用开 `XiHan:ObjectStorage:S3:EnsureBuckets` 或先自行建桶，与 MinIO 相同。
- 只改 CLI（工具包）与测试缺省值，框架库包没有公开 API、配置键或行为变化。
- 实测：上述 39 例对独立容器、挂命名卷的容器、`xihan pack` 生成的 Compose 服务（`docker compose up --wait` 进入 healthy，另以 override 文件映射端口）都是 39 / 39；跑完已删除全部容器与卷。

ADR-0082 第二节「`xihan pack` 给 MinIO 注入 `accessKey` / `secretKey`」中的依赖服务由本节改为 RustFS，属性名与取值不变。
