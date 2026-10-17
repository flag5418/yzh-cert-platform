# 三处上传入口统一机制 — TODO 清单 - V1

> 日期：2026-10-16 | 版本：V1 | 状态：🔄 进行中
> 上游设计：`三处上传入口统一机制设计-V2.md`（**先读它**；本清单只做任务拆解与勾销）
> 范围：`cert-admin` 标准文档管理 · `cert-auditor` 企业资料库 / 企业原始资料（+ 队列中心两页 + AI 释义入口收敛）

## 维护约定（★ 用户指定：完成一项勾销一项）

- 每完成一项 → 把 `- [ ]` 改成 `- [x]`，并在「进度总览」更新已完成数。
- 每完成**一期** → 跑该期的「出口验收」，全绿方可结期（`- [x]` 到该期标题）。
- 勾销时若发现范围变化 → 同步改本清单 + 上游设计文档（`-V2`）。
- ⛔ 未勾销的项不得跳期开做（除标注「独立轨」的 P9）。

## 决策基线（全部已定案 2026-10-16）

| # | 决策 |
|---|---|
| ① | AI 释义入口服务 **收敛**（唯一后端改动） |
| ② | 进度 = 轮询收口 **+** 启用 SignalR（走 `/api/yzh-msg`，⛔ 不接 `/api/yzh-upload`） |
| ③ | 企业资料库**全改**用 `YzhFolderUpload` |
| ④ | 文件夹树**合并**为 `YzhFolderTree` |
| ⑤ | 队列中心两页**本轮做** |
| ⑥ | 标准文档手写 `<table>` **本轮改**共享布局壳 |
| ⑦ | 本轮批次 **= L1 全做**（上传/替换/下载/打包 + 进度） |

## 进度总览

| 期 | 主题 | 项数 | 已完成 | 状态 |
|---|---|---|---|---|
| P1 | `useFileOps` 新建 + 企业资料库接入 | 10 | 10 | ✅ |
| P2 | 标准文档管理接入 | 4 | 0 | ⬜ |
| P3 | 企业原始资料接入 | 2 | 0 | ⬜ |
| P4 | 进度收口 + SignalR | 5 | 0 | ⬜ |
| P5 | `YzhQueueBanner` | 4 | 0 | ⬜ |
| P6 | `YzhFolderTree` 合并 | 5 | 0 | ⬜ |
| P7 | 状态字典接入 | 4 | 0 | ⬜ |
| P8 | 队列中心两页统一 | 4 | 0 | ⬜ |
| P9 | AI 释义入口收敛（后端·独立轨） | 5 | 0 | ⬜ |
| P10 | 标准文档手写 `<table>` → 共享壳 | 3 | 0 | ⬜ |
| **合计** | | **46** | **10** | |

---

## P1 — `useFileOps` 新建 + 企业资料库接入

- [x] **P1.1** 新建 `cert-share/src/composables/useFileOps.ts`：上传族（`init`→`file*`→`confirm`，**`plan` 可选**，见设计 §3.4）
- [x] **P1.2** 消「同名不同参」：`downloadBlob`（`utils/download.ts` vs `api/ent/enterprise-file.ts`）、`replaceFile`（`useDirectoryApi.ts` vs `api/ent/enterprise-file.ts`）→ 合并或改名
- [x] **P1.3** 抽统一「Blob→保存」`saveBlob`（替代 3 份等价实现）
- [x] **P1.4** 抽替换机制（选文件→覆盖→回填→进队列），端点/签名由调用方注入
- [x] **P1.5** 抽下载机制（注入 `fetchBlob`；保存统一）
- [x] **P1.6** 抽打包机制（注入打包口径；⛔ 与「压缩包上传」严格区分）
- [x] **P1.7** 企业资料库 `resources/logic.ts` 接入：3 个裸 `el-upload`（`:249`/`:317`/`:415`）→ `YzhFolderUpload` + 管线
- [x] **P1.8** 企业资料库替换走统一机制（`onReplacePick` 收编）
- [x] **P1.9** 企业资料库下载走统一机制（`downloadRow`/`downloadVersion` 收编）
- [x] **P1.10** **出口验收**：网络请求序列与改造前逐条一致 · `vitest` · `vue-tsc` 5 包 0 错误
  - ✅ `vue-tsc` 5 包 0 错误 · `guards.mjs` 34 规则通过 · `vitest` 437 passed
  - ✅ 网络请求序列：init/file×N/confirm 走 `uploadInit/uploadFileStep/uploadConfirm`，cancel 走 `uploadCancel`，与既有 `uploadFiles` 同口径（rollback 失败自动 cancel + 抛错）

## P2 — 标准文档管理接入

- [ ] **P2.1** `directory/index.vue` 上传编排（`:694-720`，**无 plan**）接入 `useFileOps`
- [ ] **P2.2** 替换接入（`onReplacePicked:613` 收编）
- [ ] **P2.3** 下载接入（`downloadFile` 等收编）
- [ ] **P2.4** **出口验收**：同 P1.10

## P3 — 企业原始资料接入

- [ ] **P3.1** `enterprise-original/logic.ts`（`:1122-1150`，**含 plan**）接入 `useFileOps`
- [ ] **P3.2** **出口验收**：同 P1.10

## P4 — 进度收口 + SignalR（裁决 ②）

- [ ] **P4.1** 新建进度 composable：**5s 轮询收口**（替代 `directory:338`·`resources/logic.ts:557`·`enterprise-original/logic.ts:197`）
- [ ] **P4.2** 订阅 `/api/yzh-msg`（`YzhMessageHub`）消费 `QueueProgress` 做实时加速
- [ ] **P4.3** 改造/弃用 `useUploadProgress.ts`（现连无生产者的 `/api/yzh-upload`，勿原样接入）
- [ ] **P4.4** 三处业务页替换各自 `setInterval`
- [ ] **P4.5** **出口验收**：实时事件能到达 + 断线后仍靠轮询兜底 + 无重复进度

## P5 — `YzhQueueBanner`（裁决 ④ 的前置展示件）

- [ ] **P5.1** 新建 `cert-share/src/components/YzhQueueBanner.vue`（纯展示；过样式法条 **S01–S12**）
- [ ] **P5.2** `directory/index.vue:970` 替换
- [ ] **P5.3** `enterprise-original/index.vue:229` 替换
- [ ] **P5.4** **出口验收**：`guards.mjs`（含 **R18/R19/R20**）0 违规

## P6 — `YzhFolderTree` 合并（裁决 ④）

- [ ] **P6.1** 新建泛型 `YzhFolderTree.vue`（递归 + 计数汇总 + `#node-extra` 插槽）
- [ ] **P6.2** 企业资料库接入（替 `StandardFolderTree.vue`，252 行）
- [ ] **P6.3** 企业原始资料接入（替 `OriginalFolderTree.vue`，396 行）
- [ ] **P6.4** 删除两份旧树组件
- [ ] **P6.5** **出口验收**：逐页比对列无丢失

## P7 — 状态字典接入

- [ ] **P7.1** 标准文档 `directory:764` 改用 `convertStatus.ts`
- [ ] **P7.2** 资料库 `api/ent/enterprise-file.ts` 状态 map 改用共享字典
- [ ] **P7.3** 原始资料 `enterprise-original/logic.ts:348` 改用共享字典
- [ ] **P7.4** **出口验收**：文案/颜色与改造前一致（`vitest`）

## P8 — 队列中心两页统一（裁决 ⑤）

- [ ] **P8.1** 差异比对：`admin/workflow/queue/index.vue`（558）vs `auditor/pages/queue/index.vue`（981）
- [ ] **P8.2** 抽共享渲染（视差异定 composable / 组件形态）
- [ ] **P8.3** 两页接入
- [ ] **P8.4** **出口验收**：两页功能无回归

## P9 — AI 释义入口服务收敛（裁决 ① · 后端 · 独立轨）

- [ ] **P9.1** 梳理 `DocExtractionRuleService(.AI).cs` 与 `PromptWorkbenchService.AnalyzeForQueueAsync` 的入参/产物差异
- [ ] **P9.2** 抽出**唯一入口服务**（内部委托 `LlmInvokeService`）
- [ ] **P9.3** 两处调用方改接（提取规则页 + 提示词工作台）
- [ ] **P9.4** 回归：提取规则页 / 提示词工作台 / 原始资料分析（`AnalyzeForQueueAsync` 走队列）
- [ ] **P9.5** **出口验收**：后端编译 0 错误 + 服务能起 + 上述三处功能不回归

## P10 — 标准文档手写 `<table>` → 共享布局壳（裁决 ⑥）

- [ ] **P10.1** 比对 `directory/index.vue:1064` 手写 table 与共享壳差异
- [ ] **P10.2** 迁移到共享布局壳
- [ ] **P10.3** **出口验收**：列/交互无回归 + `vue-tsc` 0 错误

---

## 每期出口必跑（通用）

1. `cd src/certplatform-web && node scripts/guards.mjs` → **0 违规**（含 R12 路由菜单一致性 + R18/R19/R20 样式）
2. `vue-tsc` **5 包 0 错误**
3. `cd src/certplatform-web/cert/cert-admin && ./node_modules/.bin/vitest run` → **全绿**
4. **该页网络请求序列与改造前逐条一致**（R1 缓解）
5. 新增共享 `.vue` → 样式一律 `var(--yzh-*, 兜底)`（⛔ 裸 hex / 裸 px）
6. 改动 `CreateBy`/`UpdateBy`/`DeleteBy` 或时间字段写入 → 追加 `verify_createby.sql` / `check-utcnow.sh`

## 变更记录

| 日期 | 变更 |
|---|---|
| 2026-10-16 | V1 建立：全部裁决定案（①收敛 ②已定案 ③④⑤⑥⑦ 均选 a），拆 10 期 46 项 |
