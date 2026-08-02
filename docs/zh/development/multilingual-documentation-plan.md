# Atlas 多语言文档改造方案

> [English](../../en/development/multilingual-documentation-plan.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。

- 状态：Proposed
- 日期：2026-08-02
- 范围：仓库根目录 README、`docs/`、文档引用、文档协作规则，以及直接读取文档的验证测试
- 不包含：产品运行时代码、产品行为、公共 API、NuGet 包结构或构建行为变更

## 1. 目标

Atlas 作为开源项目，应以英文作为默认文档语言，同时保留中文翻译。本次改造需要解决以下问题：

1. 消除当前文档体系中英文、中文正文混杂且没有语言边界的问题。
2. 将英文文档定义为默认入口和每个语言对的主版本，同时保留各类文档原有职责。
3. 将中文文档放入独立目录，并与英文文档保持相同的相对路径和文件名。
4. 为读者提供明确的语言切换入口。
5. 建立翻译同步、权威性、审查和链接验证规则。
6. 在迁移过程中保持需求 ID、设计 ID、ADR 编号和历史证据不变。

## 2. 当前状态审计

### 2.1 文档规模

提交本方案前，`docs/` 中有 38 个已跟踪 Markdown 文件；本方案本身是第 39 个文件：

| 类别 | 数量 | 当前状态 |
|------|------|----------|
| 核心文档 | 4 | `README.md`、SRS、HLD、需求追溯矩阵主要为中文正文 |
| 开发者指南 | 3 | 中文正文与英文技术术语混合 |
| ADR | 18 | 17 个 ADR 加索引，主要为中文正文 |
| Development | 4 | Work Package、remediation 和索引，主要为中文正文 |
| Evidence | 7 | 1 个纯英文文件，其余为中文或中英文混合 |
| Security | 2 | 中文正文与英文安全术语混合 |

审计结果：

- 38 个文件中有 37 个包含中文正文。
- `docs/evidence/atlas-demo-consumer-verification.md` 是当前唯一纯英文文档。
- `docs/SRS.md` 约 4,981 行，`docs/HLD.md` 约 2,781 行，是迁移风险最高的规范性文档。
- 当前 15 个文档包含 145 个跨文件本地 Markdown 链接；若计入 SRS 的 6 个页内 anchor，则为 16 个文档、151 个本地链接。
- 仓库中有 7 个跟踪文件的 28 行包含 29 次显式 `docs/...` 路径引用，包括 `AGENTS.md`、`CONTRIBUTING.md`、根 README 和 GitHub 模板。
- `tests/Atlas.Core.Tests/RequirementsBaselineTests.cs` 还通过 `Path.Combine` 动态读取 `docs/SRS.md` 和 `docs/evidence/p8-requirement-audit.md`；普通文本路径扫描无法发现这类依赖。
- CI 无条件运行该测试。移动 SRS 或 P8 audit 前，必须先把测试改为语言无关、路径可配置的文档合同测试。

### 2.2 当前主要问题

1. 文件路径不能表达文档语言。
2. 文档标题可能是英文，但正文仍以中文为主，读者无法提前判断语言。
3. 当前根 README 是中文，不符合英文优先的默认项目入口。
4. SRS、HLD 和 ADR 同时承担规范性职责，但尚未定义不同语言版本冲突时以哪一份为准。
5. 移动核心文档会影响仓库内部引用，也可能破坏未知的外部链接。
6. 如果直接批量机器翻译 SRS/HLD，可能改变 requirement、failure semantics 或 security claim。

## 3. 语言与权威性规则

### 3.1 默认语言

- 英文目录：`docs/en/`
- 中文目录：`docs/zh/`
- 英文是默认阅读入口。
- 英文版本是迁移完成后每个语言对的主版本（Canonical）。
- 中文版本是翻译（Translation）。如果两种语言存在语义冲突，以英文版本为准，并修复中文翻译。

本方案采用 `zh` 作为当前中文目录简称。若未来需要同时维护简体和繁体中文，再单独设计 `zh-CN`、`zh-TW` 或 `zh-Hans`、`zh-Hant` 的迁移，不在本轮提前增加复杂度。

### 3.2 迁移期间的权威性

不能在英文翻译未经审查时直接宣布其为主版本。每个文件使用以下迁移状态机：

1. `Legacy Canonical`：现有原文仍是临时语言主版本，可能位于旧路径或 `docs/zh/`。
2. `English In Review`：英文翻译已存在，但还不能替代原文。
3. `English Ready for Cutover`：英文已通过领域语义审查，但默认路径尚未原子切换。
4. `English Canonical`：cutover 完成，英文成为该语言对的主版本。
5. `Chinese Synchronized` / `Chinese Outdated`：中文与指定英文源 revision 同步或明确过期。

`English Ready for Cutover` 不是永久状态。Legacy Canonical source blob 或已审查英文 candidate blob
任一变化时，状态必须立即回退为 `English In Review`。P5d 只能切换与审查记录完全匹配的最终 blob。

页眉、语言入口和状态表必须反映当前状态，不能提前统一声明“English canonical”。同一类别完成
cutover 后，仓库默认入口才指向英文版本。迁移期间不创建只有标题或“待翻译”内容的英文占位文档。

`Canonical` 只表示同一文档语言对中的主版本，不改变文档本身的职责：

| 文档类型 | 权威职责 |
|----------|----------|
| SRS | 用户可观察行为和需求合同 |
| HLD | 内部架构与设计合同 |
| Accepted ADR | 已批准架构决策的历史记录 |
| Requirements Traceability | 当前需求、实现和验证状态 |
| Guides | 解释性开发指南，不替代 SRS/HLD |
| Development / Evidence | 工作历史与时间点证据，不定义当前产品合同 |

### 3.3 不翻译的内容

以下内容在所有语言版本中保持一致：

- `REQ-F-*`、`REQ-NF-*`、`DES-*`、`ADR-*` 编号。
- C#、TypeScript、CSS 符号和 API 名称。
- 源码路径、命令、配置项、XML namespace 和 protocol 字段；Markdown 链接和迁移后的文档路径除外。
- 测试名称、错误码、枚举值和包 ID。
- 日期、版本号、提交 SHA、测量数据和验证结果。

fenced block 必须先分类，不能一律按“代码”处理：

1. **结构化规范 prose**：SRS requirement、HLD DES/OI 必须翻译，同时保持 block 数量、字段、ID、
   Actor、优先级、AC 数量与顺序等结构不变量。
2. **机器或可执行内容**：源码、命令、API 签名、JSON/XML/schema 和 protocol 示例内容级保持；
   只允许外围说明和明确标注为注释的自然语言变化。
3. **Diagram / example / mixed content**：Mermaid、ASCII 图和混合示例翻译面向人的 label、comment
   与说明，但保持 Mermaid 关键字、节点 ID、边拓扑、数值、代码标识符和可执行片段不变。

分类结果应由校验 manifest 或脚本记录；不能仅根据 fence language tag 猜测，因为现有部分
`text` fence 同时包含规范 prose、图和 API 片段。

技术名词第一次出现时可以使用本地语言解释，但不能翻译代码标识符本身。

## 4. 目标目录结构

```text
README.md                         # 英文项目首页
README.zh.md                      # 中文项目首页

docs/
├── README.md                     # 最小语言选择页
├── documentation-manifest.json   # 文档角色、当前权威路径和验证入口
├── translation-status.md         # 翻译同步状态
├── en/                           # 英文主文档
│   ├── README.md
│   ├── SRS.md
│   ├── HLD.md
│   ├── requirements-traceability.md
│   ├── guides/
│   ├── adr/
│   ├── development/
│   │   └── multilingual-documentation-plan.md
│   ├── evidence/
│   └── security/
└── zh/                           # 中文翻译
    ├── README.md
    ├── SRS.md
    ├── HLD.md
    ├── requirements-traceability.md
    ├── guides/
    ├── adr/
    ├── development/
    │   └── multilingual-documentation-plan.md
    ├── evidence/
    └── security/
```

`docs/README.md` 只负责语言选择并链接状态表，不复制开发文档内容；迁移完成前不对全部文件作统一权威声明：

```markdown
# Atlas Documentation

- [English](./en/README.md) — canonical after cutover
- [简体中文](./zh/README.md) — Chinese source/translation; see status table
```

## 5. 文件映射

所有语言使用相同的相对路径和文件名。主要映射如下：

| 当前路径 | 英文主版本 | 中文版本 |
|----------|------------|----------|
| `docs/README.md` | `docs/en/README.md` | `docs/zh/README.md` |
| `docs/SRS.md` | `docs/en/SRS.md` | `docs/zh/SRS.md` |
| `docs/HLD.md` | `docs/en/HLD.md` | `docs/zh/HLD.md` |
| `docs/requirements-traceability.md` | `docs/en/requirements-traceability.md` | `docs/zh/requirements-traceability.md` |
| `docs/guides/*.md` | `docs/en/guides/*.md` | `docs/zh/guides/*.md` |
| `docs/adr/*.md` | `docs/en/adr/*.md` | `docs/zh/adr/*.md` |
| `docs/development/**/*.md` | `docs/en/development/**/*.md` | `docs/zh/development/**/*.md` |
| `docs/multilingual-documentation-plan.md` | `docs/en/development/multilingual-documentation-plan.md` | `docs/zh/development/multilingual-documentation-plan.md` |
| `docs/evidence/*.md` | `docs/en/evidence/*.md` | `docs/zh/evidence/*.md` |
| `docs/security/*.md` | `docs/en/security/*.md` | `docs/zh/security/*.md` |

特殊处理：

- Guides、Development、ADR、Evidence、Security 在各自 cutover PR 中使用 `git mv` 移入 `docs/zh/`，同时提交经过审查的英文版本。
- SRS、HLD 和追溯矩阵在英文审查完成前保持旧路径不动，只在 P5d 原子移动，避免提前改变权威来源。
- `docs/evidence/atlas-demo-consumer-verification.md` 现有内容可直接作为英文版本的起点，再创建中文翻译。
- 文件名、ADR 编号和目录层级不因翻译而变化。
- 历史 Evidence 的事实、测量值和“当时状态”不得在翻译时改写为当前结果。

## 6. 根 README 策略

当前根 `README.md` 为中文。迁移后：

1. 将 `README.md` 改为英文，作为 GitHub 默认项目首页。
2. 新增 `README.zh.md`，保存对应中文内容。
3. 两个 README 顶部互相链接：

```markdown
English | [简体中文](README.zh.md)
```

```markdown
[English](README.md) | 简体中文
```

4. 两个版本保持相同章节结构，但英文版本为主版本。
5. README 继续保持精简，不复制 SRS、HLD 或完整测试指南。

## 7. 文档头部与语言切换

每个 `docs/en/` 文档在标题后提供中文链接；每个 `docs/zh/` 文档提供英文链接。页眉状态必须来自
`translation-status.md`，不能仅由所在目录推断。以下示例只适用于已完成 cutover 的语言对。

英文主版本示例：

```markdown
> English (canonical) | [简体中文](../../zh/guides/testing-guide.md)
```

中文翻译示例：

```markdown
> [English](../../en/guides/testing-guide.md) | 简体中文
>
> 状态：Chinese Synchronized。本文是英文主文档的翻译；如有冲突，以英文版本为准。
```

迁移中的英文文档必须显示 `English In Review`，并链接仍为 `Legacy Canonical` 的来源；迁移中的
中文原文必须显示 `Legacy Canonical`，不能提前显示“仅为翻译”。相对路径必须根据文档层级生成，
不能机械复制同一个 `../` 深度。

## 8. 翻译状态管理

新增 `docs/translation-status.md`，每行代表一个稳定的文档语言对，最少记录：

| Document ID | English path | Chinese path | Role | Current authority path / state | Chinese state | translatedFromBlob | reviewedCandidateBlob | canonicalBlob | Reviewer / reviewed revision | Disposition |
|-------------|--------------|--------------|------|--------------------------------|---------------|--------------------|-----------------------|---------------|----------------------------|-------------|
| `srs` | `docs/en/SRS.md` | `docs/zh/SRS.md` | Normative requirements | `docs/SRS.md` / Legacy Canonical | Legacy Canonical | Git blob OID | — | — | maintainer / PR | P5a review, P5d cutover |

状态至少包括：`Not Started`、`Legacy Canonical`、`English In Review`、
`English Ready for Cutover`、`English Canonical`、`Chinese Synchronized`、`Chinese Outdated`
和 `N/A`。规则如下：

1. `Document ID` 是 manifest 与状态表共用的稳定键；英文、中文和当前 authority 路径都必须是完整路径。
2. `translatedFromBlob` 是生成英文候选时所依据的 Legacy Canonical Git blob OID。
3. `reviewedCandidateBlob` 是 bilingual reviewer 实际批准的英文 Git blob OID。
4. P5d 改写临时链接后必须重新计算 candidate blob，并由 reviewer 对最终 blob 复核；不能沿用旧审查记录。
5. `canonicalBlob` 只在 cutover 后填写，并必须等于最终 `reviewedCandidateBlob`。
6. 当前 Legacy blob 不等于 `translatedFromBlob`，或当前英文 blob 不等于 `reviewedCandidateBlob` 时，
   `English Ready for Cutover` 自动失效并回退到 `English In Review`。
7. `Reviewer / reviewed revision` 必须绑定最终候选 blob 和同一次实际审查。
8. 缺少镜像文件时必须记录原因和 planned disposition，不能让“两边同时漏文件”通过集合检查。
9. 根 `README.md` / `README.zh.md` 必须进入状态表。
10. `docs/README.md` 语言选择页、manifest 和兼容 stub 必须列为明确镜像例外；本方案只在 P0～P5 作为根目录临时例外，并声明 P6 移除条件。
11. 状态不得仅凭文件修改时间自动判定。

不要求每个小改动阻塞在翻译完成后才能合并，但规范性修改必须在 PR 中明确中文版本是同步更新
还是标记为 `Outdated`。

### 8.1 机器可读 manifest

`docs/documentation-manifest.json` 是路径和镜像例外的机器可读来源，至少包含：

```json
{
  "schemaVersion": 1,
  "documents": {
    "srs": {
      "documentId": "srs",
      "role": "normative-requirements",
      "migrationPhase": "P5d",
      "targets": {
        "en": "docs/en/SRS.md",
        "zh": "docs/zh/SRS.md"
      },
      "authority": {
        "path": "docs/SRS.md",
        "language": "zh",
        "state": "Legacy Canonical"
      },
      "integrity": {
        "translatedFromBlob": null,
        "reviewedCandidateBlob": null,
        "canonicalBlob": null
      },
      "mirrorRequired": true
    },
    "requirement-audit": {
      "documentId": "requirement-audit",
      "role": "historical-requirement-evidence",
      "migrationPhase": "P4",
      "targets": {
        "en": "docs/en/evidence/p8-requirement-audit.md",
        "zh": "docs/zh/evidence/p8-requirement-audit.md"
      },
      "authority": {
        "path": "docs/evidence/p8-requirement-audit.md",
        "language": "zh",
        "state": "Legacy Canonical"
      },
      "integrity": {
        "translatedFromBlob": null,
        "reviewedCandidateBlob": null,
        "canonicalBlob": null
      },
      "mirrorRequired": true
    }
  },
  "validationInputs": {
    "srs": "srs",
    "requirementAudit": "requirement-audit"
  },
  "legacyRoutes": [
    {
      "sourcePath": "docs/SRS.md",
      "targetDocumentId": "srs",
      "kind": "compatibility-pointer",
      "activeFromPhase": "P5d",
      "removeAfterPhase": null,
      "removalCondition": "permanent",
      "reason": "Preserve the stable SRS entry point"
    }
  ],
  "mirrorExceptions": [
    {
      "path": "docs/README.md",
      "reason": "Language selector",
      "lifetime": "permanent",
      "removalCondition": null
    },
    {
      "path": "docs/multilingual-documentation-plan.md",
      "reason": "Migration control document",
      "lifetime": "P0-P5",
      "removalCondition": "Archive the reviewed language pair under docs/en/development and docs/zh/development in P6"
    }
  ]
}
```

P0 必须先冻结并验证该 JSON schema：`documentId` 唯一，目标路径和迁移 phase 完整，authority
路径存在，integrity 字段满足状态约束。每条临时或兼容路由必须结构化记录 source、target、启用阶段、
清理阶段或永久条件；每条例外必须记录原因、lifetime 和 removal condition。状态表中的 Document ID
必须与 manifest 完全一致。

P0 中 manifest 指向现有路径；P4 和 P5d 仅在目标英文文档通过审查后原子修改 authority。
`RequirementsBaselineTests` 按逻辑 ID 读取 manifest，不在测试代码中再次硬编码文件名，并通过
`atlas:requirements-index:start/end` 标记定位索引，不依赖“附录”等自然语言标题。
`translation-status.md` 记录人工可读的流程状态，manifest 记录机器验证路径、blob 和例外；
两者职责不同，但 CI 必须检查它们没有相互矛盾。

## 9. 链接与兼容策略

### 9.1 仓库内部引用

迁移时必须更新：

- `AGENTS.md`
- `CONTRIBUTING.md`
- 根 `README.md` 和 `README.zh.md`
- `.github/pull_request_template.md`
- `.github/ISSUE_TEMPLATE/feature_request.md`
- `tests/Atlas.Core.Tests/RequirementsBaselineTests.cs`（仅 P0 重构；后续 cutover 只更新 manifest 并运行测试）
- 所有语言目录中的 Markdown 相对链接

当前存在 42 条跨实施阶段的文档链接边。不能假设某一阶段移动后，所有目标语言文件已经存在。
迁移期使用以下路由规则：

1. 已完成 cutover 的语言对，英文内部链接 `docs/en/...`，中文内部链接 `docs/zh/...`。
2. 目标类别尚未迁移时，允许临时链接到 `Legacy Canonical` 路径；例外必须记录在状态表。
3. 后续类别迁移 PR 负责扫描并更新所有已迁移文档中的入站链接，不只修改本类别文件。
4. 每个阶段 Gate 同时要求零失效链接和零未声明的 legacy/cross-language 链接。
5. P6 清除所有临时路由和例外。

### 9.2 旧路径和外部链接兼容

GitHub 不会为移动后的 Markdown 文件自动重定向，Markdown stub 也不能把
`docs/SRS.md#fragment` 自动转到新文件的同名 anchor。因此迁移一定会破坏一部分未知的外部深链接；
这必须被当作文档路径的 breaking change，而不能宣称“链接全部保持可用”。

为降低入口级影响，建议长期保留以下英文兼容入口：

- `docs/SRS.md` → 指向 `docs/en/SRS.md`
- `docs/HLD.md` → 指向 `docs/en/HLD.md`
- `docs/requirements-traceability.md` → 指向英文追溯矩阵

兼容文件只包含迁移说明和可点击链接，不复制正文。`docs/README.md` 长期保留为语言选择页。
ADR、Evidence、Guides 和 Security 的旧路径不保留逐文件副本；其外部链接会发生一次明确的
breaking change。实施前必须批准该取舍，并在迁移 PR、release note 和根 README 中给出新入口。
如果要求所有旧深链接长期有效，就不能完成干净的语言目录迁移，只能保留旧正文副本；本方案不推荐
维护两份相同语言正文。

## 10. 分阶段实施方案

每个阶段使用独立 PR，完成翻译、链接修复、独立审查和验证后再进入下一阶段。

### Phase 0：规则和验证基础设施

- 预留 `docs/en/`、`docs/zh/` 命名，但不提交空目录或占位翻译。
- 新增 `translation-status.md` 和 `documentation-manifest.json`；manifest 初始仍指向旧权威路径。
- 在 SRS requirement index 周围加入稳定的 `atlas:requirements-index:start/end` HTML comment 标记。
- 重构 `RequirementsBaselineTests`：从 manifest 读取 SRS/P8 audit 路径，通过稳定标记定位索引，不依赖中文附录标题。
- 保持现有 150 条 requirement 集合和 audit 覆盖测试通过。
- 增加文件集合、链接、结构化规范 block 和 requirement/ADR tuple 校验脚本。
- 更新文档贡献规则和 PR 模板，但暂不替换 `docs/README.md`，也不移动任何正文。

### Phase 1：项目入口和短指南

- 根 README 改为英文并新增中文版本。
- 在同一 PR 中将现有 `docs/README.md` 原子移动到 `docs/zh/README.md`，创建英文版本，再在旧路径创建语言选择页。
- 迁移 `guides/` 和 `security/`；未迁移目标使用已声明的 legacy 路由。
- 验证文档结构、实际可点击的语言切换和全部入站/出站链接。

### Phase 2：开发历史资料

- 迁移 `development/`。
- 翻译 Work Package 和 remediation。
- 保留历史时间、结论和验证数据，不做需求修订。
- 更新 P1 文档中指向 Development 的临时链接。

### Phase 3：ADR

- 迁移 ADR 索引和 ADR-0001～ADR-0017。
- 保持 ADR 编号、状态、日期、决策、关联 ID 和替代关系一致。
- 自动比较两种语言中的 ADR tuple。
- 更新此前阶段中指向 ADR 的临时链接。

### Phase 4：Evidence

- 迁移 `evidence/`，以现有英文 consumer evidence 为英文起点。
- 明确 Evidence 是历史记录，不因翻译变成当前验证结论。
- 英文 P8 audit 通过语义审查后，只原子更新 manifest authority 并运行现有测试；除非 manifest schema 或解析合同变化，不修改测试代码。
- 更新此前阶段中指向 Evidence 的临时链接。

### Phase 5a：SRS 英文翻译与审查

- 记录 `docs/SRS.md` 的 `translatedFromBlob`，据此创建 `docs/en/SRS.md`，旧文件保持原位。
- 按 requirement block 分批审查结构 tuple 和英文语义。
- 记录实际获批的 `reviewedCandidateBlob` 后标记为 `English Ready for Cutover`，暂不改变默认权威路径。

### Phase 5b：HLD 英文翻译与审查

- 记录 `docs/HLD.md` 的 `translatedFromBlob`，据此创建 `docs/en/HLD.md`，旧文件保持原位。
- 按 DES/OI block 分批审查结构 tuple、需求关联和英文语义。
- 记录实际获批的 `reviewedCandidateBlob` 后标记为 `English Ready for Cutover`。

### Phase 5c：需求追溯矩阵英文翻译与审查

- 记录旧追溯矩阵的 `translatedFromBlob`，据此创建英文版本并逐行核对 `(Requirement ID, status, implementation, evidence)`。
- 只改变文档链接路径，不改变实现状态。
- 记录实际获批的 `reviewedCandidateBlob` 后标记为 `English Ready for Cutover`。

### Phase 5d：核心规范原子切换

- 前置条件：P1、P3、P4 和 P5a～P5c 全部通过。
- 验证当前三个 Legacy Canonical blob 与各自 `translatedFromBlob` 完全相同；任何漂移都回退对应 P5a～P5c 审查。
- 完成临时链接改写后计算最终英文 candidate blob，由 bilingual reviewer 复核最终 blob；不得复用改写前的审查记录。
- 验证最终 candidate blob 等于 `reviewedCandidateBlob`，再将三个旧中文正文用 `git mv` 移入 `docs/zh/`。
- 原子更新 manifest、`AGENTS.md`、`CONTRIBUTING.md` 和 GitHub 模板，并运行现有 `RequirementsBaselineTests`；除非 schema 或解析合同变化，不修改测试代码。
- 创建三个旧核心路径的兼容入口，将英文状态改为 `English Canonical`，并令 `canonicalBlob` 等于最终 `reviewedCandidateBlob`。
- 更新此前阶段中所有指向核心规范的临时链接。

### Phase 6：收尾

- 全仓检查旧路径、跨语言误链和失效 anchor。
- 检查两个语言目录的文件集合与例外 manifest。
- 明确所有未同步翻译的状态。
- 删除迁移过程中不再需要的临时路由。
- 使用 `git mv` 将本方案归档到 `docs/zh/development/multilingual-documentation-plan.md`，并创建经过审查的同路径英文版本 `docs/en/development/multilingual-documentation-plan.md`；根路径不保留 stub。

### 10.1 阶段依赖和 Gate

| Phase | 前置条件 | 主要产出 | 阶段 Gate |
|-------|----------|----------|-----------|
| P0 | 本方案的语言、目录和 breaking-link 决策获批 | manifest、状态表、语言无关测试、校验脚本 | 现有正文未移动；文档合同测试和 CI 通过 |
| P1 | P0 | 两种根 README、docs 入口、Guides、Security | 文件集合、语言切换、链接和术语审查通过 |
| P2 | P1 | Development 双语镜像 | 历史事实、日期、命令和结论逐项一致 |
| P3 | P1 | ADR-0001～0017 双语镜像 | ADR tuple 与语义审查通过 |
| P4 | P1 | Evidence 双语镜像、manifest audit 路径更新 | 测量值和历史状态一致；RequirementsBaselineTests 通过 |
| P5a | P0；SRS reviewer 已确定 | 英文 SRS | 每个 REQ tuple 一致，英文语义审查通过 |
| P5b | P0；HLD reviewer 已确定 | 英文 HLD | 每个 DES/OI tuple 一致，英文语义审查通过 |
| P5c | P0；traceability reviewer 已确定 | 英文追溯矩阵 | 每个 requirement 行的状态与映射一致 |
| P5d | P1、P3、P4、P5a～P5c | 核心规范双语镜像与原子切换 | manifest、内部链接、文档测试和完整 CI 通过 |
| P6 | P1～P5d | 最终链接、兼容入口和归档 | 无失效链接、无未声明 legacy 路由、同步状态真实 |

P2、P3、P4 在资源允许时可以并行，但每个目录仍使用独立 PR。P5a、P5b、P5c 可以并行审查；
P5d 必须等待相关目录和所有核心规范准备完成。

## 11. 自动验证要求

迁移不能只依赖人工点击。至少需要以下检查：

1. 所有相对 Markdown 文件链接和可验证 anchor 目标存在；fenced 示例中的未来路径不作为当前链接。
2. `docs/en/` 与 `docs/zh/` 的预期文件集合一致；控制文件、兼容 stub 和允许缺失项必须在 manifest/状态表声明。
3. SRS 按 requirement ID 比较结构 tuple：Actor、Priority、Source、AC 数量与顺序、引用 ID、代码符号和数值约束。
4. HLD 按 DES/OI ID 比较结构 tuple：状态、关联 requirement、owner/section、引用 ID、代码符号和数值约束。
5. ADR 按 ID 比较状态、日期、关联 requirement/DES、supersedes 关系和关键约束。
6. 需求追溯矩阵按 requirement ID 比较实现状态、实现位置和证据映射；只允许路径按 manifest 映射变化。
7. fenced block 必须先分类：结构化 SRS/HLD prose 全文翻译；机器/可执行内容保持；diagram/example/mixed block 只翻译 human-facing label/comment 并验证语法、节点、边和可执行片段不变。
8. Mermaid 应完成语法解析或构建检查；ASCII 图按节点/层级/连线结构检查，不能只比较文本行数。
9. 自动比较所有否定词无法证明语义等价；每个规范 block 仍需 bilingual reviewer 核对 AC、失败语义、数量、边界词和 must/should/may 强度。
10. 根 README、docs 语言入口和相互切换链接必须是实际可点击链接。
11. 全仓库不再存在指向已移动正文的旧内部路径，已声明的 compatibility/legacy 路由除外。
12. manifest 的当前 authority 和已激活 legacy route 路径必须存在；target 只在其 migration phase 已开始或相应状态生效后要求存在。未激活 target 可缺失但必须有 planned state/disposition；P6 要求所有 `mirrorRequired` target 完整存在。`RequirementsBaselineTests` 不依赖自然语言标题并读取当前 manifest。
13. manifest/status 的 Document ID、目标路径、phase、authority、状态和 blob 字段一致；legacy route 与 exception 都有可机器判断的启用、清理或永久条件。
14. 所有 `English Ready for Cutover` / `English Canonical` 行满足 source、candidate 和 canonical blob 完整性约束。
15. `git diff --check`、Markdown link check、文档结构测试和完整 `dotnet test` 通过。

功能代码未变化时，不要求仅为普通文档移动重复浏览器运行时测试；但文档合同测试属于迁移 Gate，
必须运行。翻译修改了命令、配置说明或行为声明时，还必须与项目文件、CI、SRS/HLD 和实际测试结果核对。

## 12. 翻译与审查流程

每份文档按以下顺序处理：

1. 在 manifest/status 中登记当前原文、文档职责、legacy 路径、owning reviewer 和 `translatedFromBlob`。
2. 从该固定 source blob 创建英文版本，状态标为 `English In Review`；规范性原文此时不移动。
3. 保持结构、编号和表格一致，并按 fenced block 类型决定全文翻译、内容级保持或 mixed-content 翻译。
4. 执行术语、结构 tuple、ID、数值、diagram 结构和链接自动检查。
5. 由独立 bilingual reviewer 检查语义，不只检查语言流畅度，并记录 `reviewedCandidateBlob` 和 reviewed revision。
6. source 或 candidate 漂移时回退 `English In Review`；普通类别在同一 PR 内完成 cutover，核心规范只在 P5d 原子切换。
7. 更新状态表、manifest、页眉、语言切换链接和所有入站引用后，对最终 candidate blob 重新执行自动检查和 reviewer 确认。
8. 只有最终 blob 与审查记录一致时才能填写 `canonicalBlob` 并切换为 `English Canonical`。

SRS、HLD、安全模型和 ADR 必须由理解对应领域合同的 reviewer 审查。语言质量通过不能替代技术语义审查。

## 13. 术语规则

建议建立共享术语表，至少覆盖：

- Workspace
- Dock Layout
- Tool Panel
- Document Tab
- Group
- ToolBar Region / ToolBar Entry
- Splitter / Split Basis
- Snapshot / ChangeSet
- Operation / Intent
- TrustedComponent / SandboxedFrame
- Persistence / Restore / Preset

原则：

- 英文主文档使用代码和产品中的正式术语。
- 中文第一次出现时可写为“工作区（Workspace）”，后续使用一致的中文或英文术语。
- 同一术语不能在不同文档中随意翻译为多个中文名称。
- API、类型名和 wire protocol 字段不翻译。

## 14. 影响与风险

| 范围 | 影响 | 风险控制 |
|------|------|----------|
| GitHub 项目入口 | 英文成为默认首页 | 提供明显中文切换链接 |
| 仓库路径 | 大量文档移动 | 分阶段 `git mv`、链接检查、核心路径兼容入口 |
| 需求与设计合同 | 英文成为最终语言主版本 | REQ/DES/OI tuple 比较、分块 bilingual 领域审查 |
| ADR | 两种语言并存 | 编号、状态、关联 ID 和替代关系自动比对 |
| 翻译维护成本 | 每次规范修改需要处理同步状态 | 状态表允许显式 `Outdated`，不伪装已同步 |
| 外部链接 | 非核心旧路径和所有旧深链接会失效 | 永久核心入口、迁移公告、显式接受 breaking change |
| Git 历史 | 复制文件可能丢失移动关系 | 原文在 cutover 时使用 `git mv`，翻译作为新文件 |
| CI/Agent 流程 | 路径和中文标题硬编码会失败 | manifest、语言无关文档测试、同阶段更新协作入口 |
| 语义漂移 | 自动 ID 集合通过但译文错误 | 结构 tuple 加 bilingual reviewer，不把自动检查当语义证明 |

## 15. 非目标

本方案不做以下工作：

- 不修改任何 SRS requirement 的含义、优先级或验收条件。
- 不修改 HLD 架构决策、ADR 结论或 requirement traceability 状态。
- 不以翻译为理由重写历史 Evidence。
- 不引入文档站点生成器、国际化框架或新运行时依赖。
- 不一次性支持更多语言。
- 不在迁移 PR 中混入产品功能代码修改；允许并要求修改直接验证文档合同的测试和脚本。

## 16. 完成标准

只有满足以下条件，多语言改造才算完成：

1. 根 `README.md` 为英文，并能通过实际 Markdown 链接切换到中文 README。
2. 所有正式正文都位于 `docs/en/` 或 `docs/zh/` 的镜像路径中；控制文件和 stub 例外已登记。
3. 英文版 SRS、HLD、ADR 和追溯矩阵已完成领域审查并标记为 `English Canonical`。
4. 中文版本的同步状态准确，不把过期翻译标记为已同步。
5. 仓库内所有链接和协作流程默认指向英文主文档，无未声明 legacy 路由。
6. 三个核心旧入口长期有效；非核心旧路径和旧深链接的 breaking change 已批准并发布说明。
7. 语言目录文件集合、结构 tuple、manifest 和关键状态自动验证通过。
8. `RequirementsBaselineTests` 使用 manifest 和语言无关解析，完整 `dotnet test` 通过。
9. 全仓 Markdown link/anchor check 和 `git diff --check` 通过。

## 17. 建议决策

本方案给出以下默认建议，但必须在实施前明确批准：

| 决策 | 默认建议 | 理由 |
|------|----------|------|
| 语言主版本 | 英文作为每个语言对的 Canonical | 符合开源协作目标；不改变文档职责 |
| 中文目录与根 README | 使用 `docs/zh/` 和 `README.zh.md` | 与当前需求一致，暂不引入地区变体复杂度 |
| 外部旧链接 | 接受非核心旧路径和全部旧深链接的一次 breaking change | GitHub Markdown 无可靠跨文件 anchor 重定向 |
| 核心旧入口 | `docs/SRS.md`、`docs/HLD.md`、追溯矩阵入口长期保留 | 降低高频外部入口和工具链破坏 |
| 翻译延迟 | 允许中文为 `Outdated`，但页眉和状态表必须醒目标记 | 英文主版本不能被翻译排期永久阻塞 |
| 领域审查 | Atlas maintainer 负责最终语义批准，独立 bilingual reviewer 提供复核 | 翻译流畅度不能替代合同所有者判断 |

决策依赖关系：

| 决策 | 阻塞内容 | 未决定时的处理 |
|------|----------|----------------|
| 英文是否为语言对 Canonical | P0 权威性规则、P5d 最终切换 | 不移动规范性正文 |
| `zh` 或 `zh-CN` | P0 manifest 和所有后续路径 | 不创建中文目录或中文 README |
| 是否接受外部链接 breaking change | P1～P6 路径迁移 | 保留旧正文路径，不开始迁移 |
| 核心入口是否长期保留 | P5d stub、P6 完成标准 | 保留旧核心正文路径 |
| 是否允许 `Outdated` | 状态表和日常贡献流程 | 要求双语同 PR 更新 |
| 最终 bilingual reviewer | SRS/HLD `Ready for Cutover` | 英文保持 `In Review` |

在上述决策明确前，本文件仅作为改造方案，不执行现有文档移动或翻译。
