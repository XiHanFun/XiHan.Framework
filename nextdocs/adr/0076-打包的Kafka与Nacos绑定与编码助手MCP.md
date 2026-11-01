# ADR-0076 打包支持 Kafka 与 Nacos 绑定；`xihan mcp` 与 `xihan setup`

## 状态

已采纳（`spec/rfcs/0016-packaging.md`、`spec/packaging/app-manifest.md`、`spec/packaging/app.schema.json`、`spec/manifest/manifest.schema.json` 随之修订）。

## 日期

2026-11-01

## 一、`xihan pack` 支持 `stream` 与 `configuration:remote` 绑定

### 背景

`Messaging.Kafka` 的清单声明可选绑定 `stream`（类型 `stream`，主值是引导地址），`Configuration.Nacos` 声明可选绑定 `nacos`（类型 `configuration:remote`，主值是服务地址）。`xihan pack` 的绑定类型只认 `connection:relational`、`cache:distributed`、`queue`、`blob`，`AppManifestReader` 遇到别的类型直接判清单不合法，用到 Kafka 或 Nacos 的应用因此打包不了；上一轮只在 RFC-0016 里注明「不支持」。

### 备选

1. 两个 Provider 不再声明绑定，只读各自的配置节。
2. `xihan pack` 认这两种类型，三种产物各自生成对应的依赖服务与注入。

### 决策

取 2。

- `BindingKind` 增加 `stream`（Compose 镜像 `apache/kafka:4.1.0`，端口 9092）与 `configuration:remote`（`nacos/nacos-server:v2.5.1`，端口 8848）；应用清单 Schema 与能力清单 Schema 的绑定类型说明同步。
- 注入的主值与 Provider 读取绑定主值的形式一致：Kafka 是 `<绑定名>:9092`，Nacos 是 `http://<绑定名>:8848`（没有路径时 Provider 用缺省上下文路径 `/nacos`）。
- Compose：Kafka 是单节点 KRaft（`KAFKA_PROCESS_ROLES=broker,controller`，对外监听 `PLAINTEXT://<绑定名>:9092`，内部主题副本数 1），健康检查 `kafka-broker-api-versions.sh --bootstrap-server localhost:9092`，`start_period` 30 秒；Nacos 是 `MODE=standalone`，数据目录挂命名卷 `<绑定名>-data`，健康检查 `curl -fs http://localhost:8848/nacos/v1/console/health/readiness`，`start_period` 60 秒。应用照旧 `depends_on: condition: service_healthy`。
- Aspire：Kafka 用 `AddKafka`（`Aspire.Hosting.Kafka`，连接串即引导地址）并 `WithReference`；Nacos 没有官方集成，以固定标签的容器资源运行，注入它的 `http` 端点。
- Kustomize：与其余绑定一样只生成 Secret 示例（`type`、`uri`）与挂载，不在集群里起依赖服务。

### 理由

- 两种绑定都已在 Provider 清单里声明，且主值格式明确；让 Provider 退回只读配置节，等于放弃 Service Binding 与 Compose / Aspire 的一致注入，部署方要为这两个包另写一套。
- Kafka 镜像一旦给了任何 `KAFKA_*` 变量就不再使用自带的配置文件，所以 KRaft 必需项要写全；对外监听写服务名，否则 Compose 网络里的应用拿到的元数据指向 `localhost`，连不上。
- 镜像都固定版本：Nacos 取一致性套件验证过的 2.5.1（3.x 默认开启鉴权且 v1 接口不再可用）；Kafka 取 4.1.0（`Confluent.Kafka` 2.16 支持）。两者都在本机 Docker 上以生成的 `compose.yaml` 起过：健康检查转为 healthy，另一个容器经 `stream:9092` 生产与消费、经 `http://nacos:8848/nacos` 发布与读取配置都成功。
- Kustomize 产物一贯不托管有状态依赖（PostgreSQL、Redis 也没有），Kafka 与 Nacos 不例外。

### 影响

- `xihan.app.json` 的 `bindings.<名>.type` 多两个合法值；以前写这两种类型的清单是 `CLI_VALIDATION_001`，现在可以打包。
- 清单里的绑定名要写成 Provider 读取的名字（`stream`、`nacos`），Provider 才取得到注入的值；与其余四种绑定相同。
- Compose 依赖服务都不映射宿主机端口；要打开 Nacos 控制台时另加 override 文件。
- 测试：`PackagerTests` 对 Compose 产物做整份快照、对 Aspire 与 Kustomize 做断言；`BindingKindTests` 核对各 Provider 清单声明的绑定类型与 `BindingKind` 一一对应、新增种类固定镜像标签。
