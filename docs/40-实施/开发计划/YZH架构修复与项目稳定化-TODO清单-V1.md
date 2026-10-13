# YZH 架构修复与项目稳定化 TODO 清单

> **文档版本**: V1
> **创建时间**: 2026-09-18
> **状态**: 待审批
> **核心目标**: 修复 YZH 架构漏洞 → 统一项目实现 → 稳定后台管理 → 再开新项目

---

## 一、修复原则

| 原则 | 说明 |
|------|------|
| **架构漏洞优先** | 先修 id 问题、多 http 路径等框架级问题，再改业务 |
| **逐层递进** | 框架 → 契约 → 页面 → 路由 → 文档，不跳步 |
| **每阶段可验收** | 每个阶段结束必须有明确的验收标准 |
| **不碰合法组合页** | NC 设计器、文档提取等组合页只收口契约，不改交互 |

---

## 二、阶段 0：YZH 架构漏洞修复（P0，2-3 天）

> **目标**: 修复框架级问题，确保后续业务改造有统一基础

### 0.1 ID 问题修复

| ID | 问题 | 当前状态 | 修复方案 | 验收标准 |
|----|------|---------|---------|---------|
| 0.1.1 | BaseEntity.Id 类型不统一 | 部分用 int，部分用 long，部分用 string | 统一为 `string` 类型（Snowflake/UUID） | 所有继承 BaseEntity 的实体 Id 类型一致 |
| 0.1.2 | 主键命名混乱 | `Id` / `ID` / `{Entity}_Id` 混用 | 统一使用 `Id`（大写 I 小写 d） | grep "public.*_Id" 在 Entities/ 下为 0 |
| 0.1.3 | rowKey 默认值问题 | YzhTable 默认 `'id'`，实际用 `'Code'` | 改默认值为 `'Code' 或 `'Id'` | 所有 YzhTable 页面 rowKey 一致 |
| 0.1.4 | 删除操作传参不一致 | 有的传单个 ID，有的传数组 | 统一为数组格式 `delete(ids: string[])` | 所有删除接口参数类型一致 |

### 0.2 多 HTTP 路径修复

| ID | 问题 | 当前状态 | 修复方案 | 验收标准 |
|----|------|---------|---------|---------|
| 0.2.1 | 双 HTTP 客户端并存 | `yzhApi` + `http.ts`（menu.ts） | 删除 `http.ts` 依赖，统一 `yzhApi` | `grep -r "utils/http" cert-admin/src/` 为 0 |
| 0.2.2 | API 路径风格不统一 | `/api/Sys_User/getPageData` vs `/api/System/User/filter` | 统一为 `/api/{Controller}/filter` | 所有列表查询走 filter |
| 0.2.3 | 响应格式不统一 | `{code:200,data}` vs `{success:true,data}` vs 直接数组 | 统一为 `ApiResponse<T>` | 网络面板成功响应格式一致 |
| 0.2.4 | 未授权处理缺失 | 401 无统一跳转 | `yzhApi.onUnauthorized` 注入 | 401 自动跳转 /login |

### 0.3 框架小坑修复

| ID | 问题 | 修复方案 | 验收标准 |
|----|------|---------|---------|
| 0.3.1 | includeDeleted 未传到 ORM | 删除未使用参数或贯通 | 无死代码 |
| 0.3.2 | 分页强制 IsValid=1 | SqlPageOptions.IncludeDisabled | 管理页可查停用数据 |
| 0.3.3 | JWT 默认密钥兜底 | appsettings 配死，去掉代码内兜底 | 无硬编码密钥 |

---

## 三、阶段 1：API 契约统一（P0，2-3 天）

> **目标**: 所有 API 走基类端点，消灭 Vol 遗留

### 1.1 后端 Controller 改造

| ID | Controller | 当前状态 | 目标状态 | 工作量 |
|----|------------|---------|---------|-------|
| 1.1.1 | PromptTemplateController | ControllerBase + getList | YzhControllerBase + /filter | 中 |
| 1.1.2 | DocExtractionRuleController | 自定义端点 | 响应改 ApiResponse | 中 |
| 1.1.3 | ReportDefinitionController | 自定义端点 | 保留组合页，内部用 EntityService | 小 |
| 1.1.4 | 目录管理 Controller | 自定义端点 | 队列走框架 QueueManager | 小 |

### 1.2 前端 API 文件清理

| ID | 文件 | 当前状态 | 目标状态 | 验收标准 |
|----|------|---------|---------|---------|
| 1.2.1 | user.ts | `/api/Sys_User/getPageData` | `/api/System/User/filter` | 路径统一 |
| 1.2.2 | log.ts | `/api/Sys_Log/getPageData` | `/api/System/Log/filter` 或标占位 | 无 getPageData |
| 1.2.3 | param.ts | `/api/Sys_Parameter/getPageData` | `/api/System/Param/filter` 或标占位 | 无 getPageData |
| 1.2.4 | menu-management.ts | `/api/Sys_Menu/getPageData` | `/api/System/Menu/filter` 或标占位 | 无 getPageData |
| 1.2.5 | menu.ts | 使用 http.ts | 改用 yzhApi | 无 http 依赖 |

### 1.3 契约验证清单

```bash
# 执行以下命令，结果应全为 0
grep -r "getPageData" src/certplatform-web/cert/cert-admin/src/
grep -r "utils/http" src/certplatform-web/cert/cert-admin/src/
grep -r "code: 200" src/certplatform-web/cert/cert-admin/src/
grep -r "{code:" src/certplatform-web/cert/cert-admin/src/api/
```

---

## 四、阶段 2：页面实现统一（P0，3-5 天）

> **目标**: 所有单表页用 CrudPageLogic，树表页用 TreeTableLogic

### 2.1 标杆模板定义

| 模板 | 适用场景 | 参考文件 |
|------|---------|---------|
| CrudPageLogic | 单表 CRUD | system/config/index.vue |
| TreeTableLogic | 左树右表 | system/role/index.vue |
| CheckSelector | 关联勾选 | system/role-user/index.vue |

### 2.2 页面改造清单

#### A 类：必须改造（单表 CRUD）

| ID | 页面 | 当前模式 | 目标模式 | 工作量 |
|----|------|---------|---------|-------|
| 2.2.1 | user | 直写 YzhTable | CrudPageLogic | 中 |
| 2.2.2 | cert-stage | 直写 YzhTable | CrudPageLogic | 中 |
| 2.2.3 | phase-definition | 直写 YzhTable | CrudPageLogic | 中 |
| 2.2.4 | certification-body | 直写 YzhTable | CrudPageLogic（保留钩子） | 中 |
| 2.2.5 | prompt-template | el-table 直写 | CrudPageLogic | 大 |
| 2.2.6 | log | 直写 | CrudPageLogic 或标占位 | 中 |
| 2.2.7 | param | 直写 | CrudPageLogic 或标占位 | 中 |

#### B 类：保持现状（组合页/领域引擎）

| 页面 | 原因 | 仍要收口的点 |
|------|------|-------------|
| nc-config | 画布+解释器 | 下拉走 /filter |
| doc-extraction-rule | 左树+AI+预览 | 响应改 ApiResponse |
| directory | 树+上传+队列 | 队列走 QueueManager |
| report-rule | 组合页 | 模板调 EntityService |
| skill-manage | 树表 | 已用 TreeTableLogic |

#### C 类：删除或重定向

| ID | 页面 | 处理方式 |
|----|------|---------|
| 2.2.8 | job-skill | 删除，重定向到 skill-manage |

### 2.3 每页完成标准

- [ ] 前端零硬编码列（字典 options 可留在 JSON 的 DropDown/Data）
- [ ] 只调 yzhApi + 基类路径
- [ ] 增删改后走 Split 或 loadPage()
- [ ] 弹窗/表单状态由 Logic 类管理
- [ ] 无手动 ElMessageBox 确认（走基类）

---

## 五、阶段 3：路由与菜单同步（P1，1-2 天）

> **目标**: 菜单 URL = router path = Controller

### 3.1 路由挂载

| ID | 路由 | 状态 | 操作 |
|----|------|------|------|
| 3.1.1 | /system/role | 页面存在，路由未挂 | 添加路由 |
| 3.1.2 | /system/user | 页面存在，路由未挂 | 添加路由 |
| 3.1.3 | /system/log | 页面存在，路由未挂 | 确认是否需要 |
| 3.1.4 | /system/param | 页面存在，路由未挂 | 确认是否需要 |

### 3.2 菜单-路由-Controller 对照表

| 菜单 URL | router path | Controller | 前端文件 | 状态 |
|----------|-------------|------------|---------|------|
| /system/organization | /system/organization | OrganizationController | organization/index.vue | ✅ |
| /system/role | /system/role | RoleController | role/index.vue | 待挂 |
| /system/role-user | /system/role-user | RoleUserController | role-user/index.vue | ✅ |
| /system/role-menu | /system/role-menu | RoleMenuController | role-menu/index.vue | ✅ |
| /system/role-api | /system/role-api | RoleApiController | role-api/index.vue | ✅ |
| /system/menu | /system/menu | MenuController | menu/index.vue | ✅ |
| /system/api | /system/api | ApiController | api/index.vue | ✅ |
| /system/dictionary | /system/dictionary | DictionaryController | dictionary/index.vue | ✅ |
| /system/log | /system/log | LogController | log/index.vue | 待挂 |
| /system/config | /system/config | SysConfigController | config/index.vue | ✅ 标杆 |
| /system/user | /system/user | UserController | user/index.vue | 待挂 |
| /cert/iso-standard | /cert/iso-standard | IsoStandardController | iso-standard/index.vue | ✅ |
| /foundation/certification-body | /foundation/certification-body | CertificationBodyController | certification-body/index.vue | ✅ |
| /foundation/phase-definition | /foundation/phase-definition | PhaseDefinitionController | phase-definition/index.vue | ✅ |
| /cert/cert-stage | /cert/cert-stage | CertStageController | cert-stage/index.vue | ✅ |
| /cert/link-org-standard | /cert/link-org-standard | CertOrgStandardController | cert-org-standard/index.vue | ✅ |
| /cert/link-org-stage | /cert/link-org-stage | CertOrgStageController | cert-org-stage/index.vue | ✅ |
| /business/directory-manager | /business/directory-manager | DirectoryController | directory/index.vue | ✅ |
| /business/doc-extraction-rule | /business/doc-extraction-rule | DocExtractionRuleController | doc-extraction-rule/index.vue | ✅ |
| /business/report-def | /business/report-def | ReportDefinitionController | report-rule/index.vue | ✅ |
| /business/prompt-template | /business/prompt-template | PromptTemplateController | prompt-template/index.vue | ✅ |
| /business/skill-manage | /business/skill-manage | SkillController | skill-manage/index.vue | ✅ |
| /business/nc-config | /business/nc-config | NcConfigController | nc-config/index.vue | ✅ |
| /business/ai-usage | /business/ai-usage | AiUsageController | ai-usage/index.vue | ✅ |
| /business/queue-monitor | /business/queue-monitor | QueueController | queue/index.vue | ✅ |

### 3.3 占位明确

| 路由 | 状态 | 说明 |
|------|------|------|
| /system/api | 占位 | 权限表未建，不做假授权 |
| /system/role-api | 占位 | 权限表未建，不做假授权 |

---

## 六、阶段 4：文档同步（P1，1-2 天）

> **目标**: 文档即宪法，代码与文档一致

### 4.1 需修正的文档

| ID | 文档 | 修正内容 |
|----|------|---------|
| 4.1.1 | 01-架构总纲.md | EF→SqlSugar；Logic 落点 yzh.vue.core |
| 4.1.2 | 03-前端架构.md | 标杆页：config（单表）、organization（树表）、role-user（勾选） |
| 4.1.3 | 07-开发流程.md | 禁止清单：getPageData、硬编码 columns、{code:200}、http.ts |
| 4.1.4 | 12-框架能力清单.md | 已用标✅，权限表/审批流标「审核端前不做」 |

### 4.2 新建文档

| ID | 文档 | 内容 |
|----|------|------|
| 4.2.1 | 16-Admin已落地能力与越界清单-V1.md | A/B/C 三类模块表，作为审核端开工前检查单 |

---

## 七、阶段 5：冻结门禁（P2，1 天）

> **目标**: 通过后再写审核端

### 5.1 自动化检查脚本

```bash
#!/bin/bash
# freeze-check.sh

echo "=== YZH 架构冻结检查 ==="

# 1. Vol 遗留检查
echo "--- Vol 遗留 ---"
grep -r "getPageData" src/certplatform-web/cert/cert-admin/src/ && echo "FAIL: 存在 getPageData" || echo "PASS"
grep -r "utils/http" src/certplatform-web/cert/cert-admin/src/ && echo "FAIL: 存在 http.ts 依赖" || echo "PASS"
grep -r "ControllerBase" src/certplatform-web/cert/cert-admin/src/ && echo "FAIL: 存在裸 ControllerBase" || echo "PASS"

# 2. 响应格式检查
echo "--- 响应格式 ---"
grep -r "code: 200" src/certplatform-web/cert/cert-admin/src/api/ && echo "FAIL: 存在 {code:200}" || echo "PASS"
grep -r "{code:" src/certplatform-web/cert/cert-admin/src/api/ && echo "FAIL: 存在旧响应格式" || echo "PASS"

# 3. 页面硬编码检查
echo "--- 页面硬编码 ---"
grep -r "const columns" src/certplatform-web/cert/cert-admin/src/pages/ && echo "WARN: 存在硬编码 columns" || echo "PASS"

# 4. 路由完整性检查
echo "--- 路由完整性 ---"
# 检查所有 pages 下的目录是否都有对应路由
```

### 5.2 冻结清单

- [ ] grep 检查全通过
- [ ] 菜单 URL = router/index.ts = 实际页面
- [ ] 系统管理：机构/角色/角色三人关联/字典/菜单/参数/用户 全可点
- [ ] 基础配置：ISO 树表/认证机构/阶段/阶段定义/机构关联 全可点
- [ ] 工作流：目录/提取/Prompt/技能树/NC 列表+设计器/报告定义+设计器/队列 全可点
- [ ] 占位明确：system/api、system/role-api 标「权限表未建，不做假授权」

---

## 八、明确不做（现在）

| 项目 | 原因 |
|------|------|
| 接口级权限落地 | 权限表未建，不做假授权 |
| 通用审批流迁移 | 旧 Sys_WorkFlow*，审核端前不做 |
| yzh-core 大重构 | 单人项目收益低 |
| 企业端 | 后台管理稳定后再做 |
| 把设计器塞进 TreeTable | 组合页不硬套 Crud |
| 拆 YzhControllerBase 大文件 | 单人项目收益低 |

---

## 九、实施顺序与依赖关系

```
阶段0 架构漏洞修复 (2-3天)
    │
    ├── 0.1 ID 问题
    ├── 0.2 多 HTTP 路径
    └── 0.3 框架小坑
    │
    ▼
阶段1 API 契约统一 (2-3天)
    │
    ├── 1.1 后端 Controller
    ├── 1.2 前端 API 文件
    └── 1.3 契约验证
    │
    ▼
阶段2 页面实现统一 (3-5天)
    │
    ├── 2.1 标杆模板
    ├── 2.2 A 类页面改造
    └── 2.3 每页验收
    │
    ▼
阶段3 路由与菜单同步 (1-2天)
    │
    ├── 3.1 路由挂载
    ├── 3.2 对照表
    └── 3.3 占位明确
    │
    ▼
阶段4 文档同步 (1-2天)
    │
    ├── 4.1 修正文档
    └── 4.2 新建文档
    │
    ▼
阶段5 冻结门禁 (1天)
    │
    ├── 5.1 自动化检查
    └── 5.2 冻结清单
    │
    ▼
✅ 后台管理稳定，可开始审核端
```

---

## 十、工时估算

| 阶段 | 任务 | 估算工时 | 累计 |
|------|------|---------|------|
| 阶段 0 | 架构漏洞修复 | 2-3 天 | 2-3 天 |
| 阶段 1 | API 契约统一 | 2-3 天 | 4-6 天 |
| 阶段 2 | 页面实现统一 | 3-5 天 | 7-11 天 |
| 阶段 3 | 路由与菜单同步 | 1-2 天 | 8-13 天 |
| 阶段 4 | 文档同步 | 1-2 天 | 9-15 天 |
| 阶段 5 | 冻结门禁 | 1 天 | 10-16 天 |
| **总计** | | **10-16 天** | |

---

## 十一、风险与应对

| 风险 | 影响 | 应对 |
|------|------|------|
| 后端 Controller 改造影响现有功能 | 高 | 改造后完整回归测试 |
| 前端页面改造引入 bug | 中 | 每页改造后立即验证 |
| 路由挂载导致 404 | 低 | 对照表逐项检查 |
| 文档与代码再次漂移 | 中 | 阶段 5 自动化检查 |

---

## 十二、待审批

- [ ] 是否按此计划执行？
- [ ] 阶段 0 的 ID 问题是否需要立即修复？（可能影响较大）
- [ ] 阶段 2 的 prompt-template（el-table 直写）是否优先改造？
- [ ] 是否需要调整工时估算？

---

**审批人**: ________________
**审批日期**: ________________
