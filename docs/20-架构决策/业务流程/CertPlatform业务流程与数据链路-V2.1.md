# CertPlatform 业务流程与数据链路（V2.1 联调版）

> **文档目的**：作为 CertPlatform 域后续 AI 开发、测试与排障的统一业务蓝本。所有模块的新增字段、联调跳转、字典取值、前后端 Hook 编写均以本文为准。

---

## 一、七张核心业务表（PascalCase 列名）

| 表名（物理） | 实体类 | 主键 Code | 业务唯一键 | 说明 |
|-------------|--------|-----------|-----------|------|
| `cert_certification_body` | `CertificationBody` | GUID `Code` | `CbCode` (CNAS 编号) | 认证机构（CB）—— 出证主体 |
| `cert_iso_standard` | `ISOStandard` | GUID `Code` | `StandardCode` (如 ISO 13485:2016) | 机构下某一年版的认证标准 |
| `cert_iso_clause` | `ISOClause` | GUID `Code` | `StandardCode + ClauseNumber` | 某标准下的条款树（10 章结构） |
| `cert_enterprise` | `Enterprise` | GUID `Code` | `CreditCode` (统一社会信用代码) | 申请认证的企业 |
| `cert_application` | `CertApplication` | GUID `Code` | `ApplicationNo` (自动生成流水号) | 企业向机构发起的一次认证申请 |
| `audit_project` | `AuditProject` | GUID `Code` | `ProjectNo` (PRJ-YYYYMM-XXX) | 对应申请的审核项目（包含 5 阶段） |
| `audit_task` | `AuditTask` | GUID `Code` | `TaskNumber` (PRJ-T01/T02/…) | 某阶段的具体审核任务（分配审核员） |

---

## 二、核心业务链路（端到端）

```
┌────────────────────┐     1:N     ┌────────────────────┐     1:N     ┌────────────────────┐
│ CertificationBody  │────────────▶│   ISOStandard      │────────────▶│    ISOClause       │
│   (认证机构 CB)    │    CbCode    │  (机构拥有的标准)  │ StandardCode │  (条款 4.1/5.1…)  │
└────────────────────┘              └────────────────────┘              └────────────────────┘
                                                                    ▲
                                                                    │ 关联条款
                                          发起申请                   │
┌────────────────────┐     1:N     ┌────────────────────┐     1:1   │   5阶段拆分
│    Enterprise      │────────────▶│  CertApplication   │─────────┐ │
│   (申请企业)       │ EnterpriseCode│  (认证申请)        │         ▼ │
└────────────────────┘              └────────────────────┘   ┌────────────────────┐   1:N   ┌────────────────────┐
                                                             │   AuditProject     │────────▶│    AuditTask       │
                                                             │  (审核项目 整体)    │ ProjectCode│ (5个阶段任务)      │
                                                             └────────────────────┘         └────────────────────┘
```

---

## 三、业务流程阶段（与 Phase2 测试数据对应）

### Phase 0 · 基础数据准备（运营/管理员）
| 操作模块 | 关键字段 | 触发操作 |
|----------|----------|----------|
| 认证机构管理 | `CbCode=CB001` | 创建机构，设置 `Status=active` |
| ISO 标准管理 | 关联 `CbCode` | 为 CB001 新建 ISO 13485:2016 标准 |
| 条款管理 | 关联 `StandardCode` | 导入 43 条条款（4–10 章） |
| 企业管理 | `CreditCode` | 注册申请企业 |

### Phase 1 · 申请受理（Task T01）
1. 企业提交申请 → `cert_application.Status = 'submitted'`
2. 项目经理点「受理」→ 创建 `audit_project` + `audit_task(TaskNumber=T01)`
3. T01 审核员检查材料完整性 → 完成后 `task.Status = done`

### Phase 2 · 文件评审（Task T02）
1. T01 完成 → 自动生成 T02（文件评审）
2. 审核员上传「文审报告」→ 发现的不符合项记录（后续 NC 模块）
3. 结论 → `project.CurrentPhase = 'document_review'`

### Phase 3 · 一阶段审核（Task T03，现场）
1. 确认企业现场就绪 → T03 派发审核组长
2. 现场审核：确认审核计划可行性、体系运行概况
3. 记录 NC（不符合项）轻微 → 关闭后进入 T04

### Phase 4 · 二阶段审核（Task T04，现场）⭐ 关键
1. 全面评价 QMS 与 ISO 标准的符合性
2. 抽样审核：43 条款逐条过审
3. 严重 NC → 需整改验证；一般 NC → 认证决定前关闭

### Phase 5 · 认证决定（Task T05）
1. 综合 T01–T04 资料 + NC 关闭证据
2. 认证委员会表决
3. 通过 → 生成证书（`cert_certificate` 模块）；不通过 → 申请退回/拒绝

---

## 四、前端模块路由与参数跳转规范

> 所有跨页跳转通过 `router.push({ path, query })` 携带编码；**接收方 `onInited` 中读 options.TableOptions.Filter**。

### 4.1 跳转矩阵（SOP）

| 来源模块 | 操作 | 目标路由 | 携带参数 | 接收方处理 |
|----------|------|----------|----------|------------|
| **认证机构列表** | 点「查看标准」 | `/cert/ISOStandard` | `CbCode=xxx` | ISOStandard 列表 `searchBefore` 注入 `Filter: [{ Name: 'CbCode', Value: xxx }]` |
| **ISO 标准列表** | 点「查看条款」 | `/cert/ISOClause` | `StandardCode=xxx` | ISOClause 列表按 StandardCode 固定筛选；顶部显示标准名面包屑 |
| **ISO 标准列表** | 点「新建申请」 | `/cert/CertApplication/new` | `StandardCode=xxx, CbCode=xxx` | 申请表单默认回填 CbCode / StandardCode |
| **申请列表** | 点「启动审核」 | `/cert/AuditProject/new` | `ApplicationCode=xxx` | 项目新建 + 自动生成 5 条 Task（T01–T05） |
| **项目列表** | 点「任务列表」 | `/cert/AuditTask` | `ProjectCode=xxx` | AuditTask 按项目筛选 |
| **任务列表** | 点「分配审核员」 | 弹出 SelectSysUser | `TaskCode=xxx` | 回填 `AuditorId` + 变更状态 `pending_assignment → pending_start` |

### 4.2 接收方实现示例（ISOClause.vue）
```javascript
// cert/ISOClause.vue 的 searchBefore Hook（对应 §12.C Skill）
const searchBefore = async (param) => {
  const route = useRoute()
  if (route.query.StandardCode) {
    param.Filter = param.Filter || []
    param.Filter.push({
      Name: 'StandardCode',
      Value: route.query.StandardCode,
      DisplayType: '等于'
    })
  }
  return true
}
```

---

## 五、字典绑定关系（cert_platform_tables_v2.1.sql → 数据字典）

### 5.1 字段 ↔ 字典编号对应表

| 实体 | 字段 | 字典编号 | 类型 |
|------|------|----------|------|
| `CertificationBody` | `Status` | `org_status` | radio |
| `CertApplication` | `CertType` | `cert_type` | select（QMS/EMS/OHSMS/ISMS/IATF16949） |
| `CertApplication` | `Status` | `application_status` | tag 状态色（9 态） |
| `AuditTask` | `Status` | `task_status` | tag 状态色（6 态） |
| `AuditTask` | `PhaseCode` | `audit_phase` | 下拉（与 TaskNumber T01–T05 对应） |
| `AuditProject` | `Status` | `project_status` | radio（active/closed/suspended） |

### 5.2 状态色 SOP（Vol tag-class）

**申请状态（application_status）**：
- `draft` → 灰色（info）
- `submitted` / `accepted` → 蓝色（primary）
- `document_review` / `auditing` → 橙色（warning）
- `completed_passed` → 绿色（success）
- `completed_failed` / `rejected` / `cancelled` → 红色（danger）

---

## 六、后端 Partial Service Hook 落点（对应 Skill §12.A）

### 6.1 常用 Hook 业务映射表

| 模块 | Hook（Partial Service） | 典型业务 |
|------|--------------------------|----------|
| `CertApplicationService` | `AddOnExecuting` | 自动生成 ApplicationNo：`YYYY-CB001-0001` |
| `CertApplicationService` | `UpdateOnExecuting` | 状态流转校验（draft→submitted 必须填范围、企业、标准） |
| `AuditProjectService` | `AddOnExecuted` | 新建项目后，自动按 `audit_phase` 字典生成 5 条 Task |
| `AuditTaskService` | `UpdateOnExecuted` | 所有子任务完成 → 回写 `AuditProject.ActualEndDate` |
| `ISOClauseService` | `DelOnExecuting` | 有子条款 (ParentCode != null) 时禁止删除（先删子再删父） |

### 6.2 生成流水号的统一代码（CertApplicationService.Partial）
```csharp
AddOnExecuting = (saveModel, entity) =>
{
    if (string.IsNullOrEmpty(entity.ApplicationNo))
    {
        string cbShort = entity.CbCode?.Substring(0, Math.Min(6, entity.CbCode.Length)) ?? "CB";
        int seq = (int)(repository.FindAs<IQueryable>()
            .Count(x => x.CbCode == entity.CbCode
                        && x.CreateDate.Year == DateTime.Now.Year) + 1);
        entity.ApplicationNo = $"{DateTime.Now:yyyy}-{cbShort}-{seq:D4}";
    }
    return true;
};
```

---

## 七、接口清单（路由约定）

| 接口路由前缀 | 对应 Controller | 标准方法 |
|-------------|-----------------|----------|
| `/api/CertCertificationBody/` | `CertCertificationBodyController` | `getPageData`, `add`, `update`, `del` |
| `/api/ISOStandard/` | `ISOStandardController` | `getPageData`, `add`, `update`, `del`, `getMaxId` |
| `/api/ISOClause/` | `ISOClauseController` | `getPageData`, `add`, `update`, `del` |
| `/api/CertEnterprise/` | `CertEnterpriseController` | `getPageData`, `add`, `update`, `del` |
| `/api/CertCertApplication/` | `CertCertApplicationController` | `getPageData`, `add`, `update`, `del`, `submit` |
| `/api/CertAuditProject/` | `CertAuditProjectController` | `getPageData`, `add`, `update`, `del`, `launch` |
| `/api/AuditTask/` | `AuditTaskController` | `getPageData`, `add`, `update`, `del`, `assign` |

> **注意**：前端 `options.js` `table.url` 必须 **以 `/` 结尾**，否则 VolTable 拼接会出现 `ISOStandardgetPageData` 形式的 404。
> 对应踩坑见：[2026-08-03 踩坑记录 P2-06](./../../60-AI工程设计/YZH-知识库/05-踩坑记录/2026-08-03_Phase2联调全栈问题修复记录.md)

---

## 八、前端 view-grid 写法标准（SOP 模板）

> **强制要求**：CertPlatform 域所有页面均使用此模板，禁止再引入 `YZHBaseCrud`。

```vue
<template>
  <view-grid
    ref="grid"
    :columns="columns" :detail="detail" :details="details"
    :edit-form-fields="editFormFields" :edit-form-options="editFormOptions"
    :search-form-fields="searchFormFields" :search-form-options="searchFormOptions"
    :table="table" :extend="extend"
    :on-init="onInit" :on-inited="onInited"
    :search-before="searchBefore" :add-before="addBefore" :update-before="updateBefore"
    :row-click="rowClick">
    <!-- gridHeader 插槽：必须 <div> 单根（P2-04 踩坑） -->
    <template #gridHeader>
      <div>
        <el-alert v-if="description" :title="description" type="info"
                  :closable="false" show-icon style="margin-bottom:10px" />
      </div>
    </template>
    <!-- btnLeft 插槽：必须 <div> 单根 -->
    <template #btnLeft>
      <div>
        <slot name="btnLeftExt"></slot>
      </div>
    </template>
  </view-grid>
</template>

<script setup lang="jsx">
import { getCurrentInstance, reactive, ref } from 'vue'
import viewOptions from './options.js'

const grid = ref(null)
const { proxy } = getCurrentInstance()

const {
  table, editFormFields, editFormOptions,
  searchFormFields, searchFormOptions,
  columns, detail, details, extend,
} = reactive(viewOptions())

let gridRef
const onInit    = async ($vm) => { gridRef = $vm }
const onInited  = async ()     => { /* 读取路由 query 注入 TableOptions.Filter */ }
const searchBefore = async (p) => { return true }
const addBefore    = async (f) => { return true }
const updateBefore = async (f) => { return true }
const rowClick     = ({ row }) => { /* 保存选中行到 selectedRow ref */ }
</script>
```

---

## 九、权限配置（菜单 → 按钮级）

**菜单树（cert_platform_menu_simple.sql）**：
```
认证管理 (cert)
├─ 认证机构管理     /cert/CertificationBody   (权限: cert:cb:*)
├─ ISO 标准管理     /cert/ISOStandard         (权限: cert:std:*)
├─ 条款管理         /cert/ISOClause           (权限: cert:clause:*)
├─ 企业管理         /cert/Enterprise          (权限: cert:ent:*)
├─ 认证申请管理     /cert/CertApplication     (权限: cert:app:*)
├─ 审核项目管理     /cert/AuditProject        (权限: cert:proj:*)
└─ 审核任务管理     /cert/AuditTask           (权限: cert:task:*)
```

按钮级权限：
- `:add` / `:update:status` / `:del` / `:export` / `:launch` / `:assign`

---

## 十、常用排障入口

| 现象 | 入口文档 |
|------|----------|
| 接口 500 / Unknown column / repository null | [Phase2 联调踩坑记录 P2-01~P2-03](./../../60-AI工程设计/YZH-知识库/05-踩坑记录/2026-08-03_Phase2联调全栈问题修复记录.md) |
| Vue 3 parentNode 空 / formatter 报错 / 404 路径拼接错误 | 同上 P2-04~P2-07 |
| 新增业务 Hook（保存前/后、流水号、自动生成子任务） | Vol Skill §12.A → 本文件 §六 |
| 模块跳转参数丢失 | 本文件 §四 跳转矩阵 SOP |
