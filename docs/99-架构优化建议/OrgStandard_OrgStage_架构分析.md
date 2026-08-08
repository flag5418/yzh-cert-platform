# 体系认证平台 - 关联管理页面架构分析报告

## 分析范围
- `http://127.0.0.1:9990/#/CertPlatform/Cert/CertificationBody`
- `http://127.0.0.1:9990/#/CertPlatform/Link/OrgStage`
- `http://127.0.0.1:9990/#/CertPlatform/Link/OrgStandard`
- `http://127.0.0.1:9990/#/CertPlatform/Base/CertStage`
- `http://127.0.0.1:9990/#/CertPlatform/Base/ISOStandard`

---

## 一、当前架构概览

### 1.1 页面职责划分

| 页面 | 用途 | 技术栈 | 数据流向 |
|------|------|--------|----------|
| `/CertPlatform/Cert/CertificationBody` | 认证机构管理（CRUD） | YzhCrudTable + options.js | 前端 → API → Service → DB |
| `/CertPlatform/Base/CertStage` | 认证阶段定义（CRUD） | YzhCrudTable + options.js | 前端 → API → Service(视图) → DB |
| `/CertPlatform/Base/ISOStandard` | ISO 标准注册（CRUD） | YzhCrudTable + options.js | 前端 → API → Service(视图) → DB |
| `/CertPlatform/Link/OrgStage` | 机构-阶段关联管理 | YzhTreeCheckboxTable | 前端 → OrgLinkController → DB |
| `/CertPlatform/Link/OrgStandard` | 机构-标准关联管理 | YzhTreeCheckboxTable | 前端 → OrgLinkController → DB |

### 1.2 后端架构

```
┌─────────────────────────────────────────────────────────────────┐
│                        Controller 层                            │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │CertCertification│  │   CertStage     │  │ ISOStandard     │ │
│  │  BodyController │  │   Controller    │  │   Controller    │ │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘ │
│           │                    │                    │           │
│  ┌────────▼────────┐  ┌────────▼────────┐  ┌────────▼────────┐ │
│  │  OrgLink        │  │                 │  │                 │ │
│  │  Controller     │  │                 │  │                 │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────────┐
│                        Service 层                               │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │CertCertification│  │  CertStage      │  │ ISOStandard     │ │
│  │   BodyService   │  │  Service        │  │  Service        │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────────┐
│                        Entity 层                                │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │CertificationBody│  │  CertStage      │  │ ISOStandard     │ │
│  │    (T)          │  │    (T)          │  │    (T)          │ │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘ │
│           │                    │                    │           │
│  ┌────────▼────────┐  ┌────────▼────────┐  ┌────────▼────────┐ │
│  │CertificationBody│  │  CertStageView  │  │ ISOStandardView │ │
│  │    (V, 无)      │  │    (V)          │  │    (V)          │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
│                                                                 │
│  ┌─────────────────┐  ┌─────────────────┐                       │
│  │  CertOrgStage   │  │ CertOrgStandard │                       │
│  │    (关联表)     │  │   (关联表)      │                       │
│  └─────────────────┘  └─────────────────┘                       │
└─────────────────────────────────────────────────────────────────┘
```

---

## 二、发现的问题

### 2.1 冗余代码问题

#### 问题 1：OrgLinkController 与 Service 层分离

**现状：**
- `CertCertificationBody`、`CertStage`、`ISOStandard` 都有对应的 Service 层
- `OrgLinkController` 直接操作 `_db`（VOLContext），绕过了 Service 层

**问题：**
```csharp
// OrgLinkController.cs - 直接操作 DbContext
var existingStageIds = _db.Set<CertOrgStage>()
    .Where(x => x.CbCode == request.CbCode && request.AddStageIds.Contains(x.StageId))
    .Select(x => x.StageId)
    .ToHashSet();
```

**影响：**
- 无法复用 Service 层的业务逻辑（如权限检查、审计日志、缓存）
- 违反分层架构原则
- 难以进行单元测试

#### 问题 2：前端代码重复

**现状：**
- `OrgStage.vue` 和 `OrgStandard.vue` 有大量重复代码：
  - `loadOrgTree()` 函数几乎完全相同
  - `handleTreeSelect()` 函数相同
  - 调试日志模式相同

**重复代码示例：**
```javascript
// OrgStage.vue 和 OrgStandard.vue 都有相同的 loadOrgTree
async function loadOrgTree() {
  console.log('[OrgStage] 🌲 开始加载机构树...')
  try {
    const res: any = await http.post('/api/CertCertificationBody/GetPageData', {
      page: 1,
      rows: 1000,
      order: 'asc',
      sort: 'Sort',
      wheres: '',
      value: '',
      filter: [{ name: 'Status', value: 'active', displayType: '==' }],
    }, null, false)
    // ... 完全相同的解析逻辑
  } catch (e) {
    console.error('[OrgStage] ❌ 加载机构树失败', e)
  }
}
```

#### 问题 3：前后端字段命名不一致

**现状：**
- 前端 `OrgStage.vue` 使用 `CategoryName`、`StatusName`（视图字段）
- 后端 `CertStageService` 返回 `CertStageView`，包含 `CategoryName`、`StatusName`
- 但前端 `ISOStandard.vue` 的 options.js 仍使用 `dataKey` 属性进行字典翻译

**问题：**
```javascript
// ISOStandard.vue options.js - 使用字典翻译
{ field: 'Category', title: '分类', width: 100, align: 'center',
  dataKey: 'iso_category' },
{ field: 'Status', title: '状态', width: 100, align: 'center',
  dataKey: 'standard_status' },
```

```javascript
// OrgStage.vue options.js - 使用视图字段（正确）
{ field: 'CategoryName', title: '分类', width: 120, align: 'center' },
{ field: 'StatusName', title: '状态', width: 100, align: 'center' },
```

---

### 2.2 架构不合理问题

#### 问题 4：关联管理没有使用 T+V 模式

**现状：**
- `CertStage` 和 `ISOStandard` 使用了 T+V 模式（实体表 + 视图）
- `CertOrgStage` 和 `CertOrgStandard` 关联表没有对应的视图

**影响：**
- 关联表显示时无法直接获取翻译后的中文字段
- 前端需要额外处理字典翻译

#### 问题 5：实体设计不一致

**现状：**
- `CertificationBody` 继承 `YZHBaseEntity`（有 `Enable`、`Status`、`Code` 等字段）
- `CertOrgStage` 和 `CertOrgStandard` 继承 `BaseEntity`（只有 `Id`）

**问题：**
```csharp
// CertificationBody.cs
public class CertificationBody : YZHBaseEntity  // 有完整审计字段
{
    public string Status { get; set; } = "active";
    public string Code { get; set; }
}

// CertOrgStage.cs
public class CertOrgStage : BaseEntity  // 只有主键
{
    public long Id { get; set; }
    public string CbCode { get; set; }
}
```

**影响：**
- 关联表缺少审计字段（创建人、创建时间、修改人等）
- 数据追溯困难

#### 问题 6：前端缺少统一的错误处理

**现状：**
- 每个页面的错误处理逻辑分散在各处
- 没有统一的错误提示机制

---

## 三、优化建议

### 建议 1：为关联管理创建 Service 层

**目标：** 将 `OrgLinkController` 的操作逻辑迁移到 Service 层

**实施方案：**

```csharp
// VOL.Builder/Services/CertPlatform/IOrgLinkService.cs
public interface IOrgLinkService : IDependency
{
    Task<SyncResult> SyncOrgStandardsAsync(string cbCode, long[] addIds, long[] removeIds);
    Task<long[]> GetOrgStdIdsAsync(string cbCode);
    Task<SyncResult> SyncOrgStagesAsync(string cbCode, long[] addIds, long[] removeIds);
    Task<long[]> GetOrgStageIdsAsync(string cbCode);
}

// VOL.Builder/Services/CertPlatform/OrgLinkService.cs
public class OrgLinkService : IOrgLinkService
{
    private readonly VOLContext _db;

    public OrgLinkService(VOLContext db)
    {
        _db = db;
    }

    public async Task<SyncResult> SyncOrgStandardsAsync(string cbCode, long[] addIds, long[] removeIds)
    {
        // 业务逻辑迁移到这里
        // 可以复用 Service 层的权限检查、审计日志等
    }
}
```

**优点：**
- 符合分层架构原则
- 便于单元测试
- 可复用 Service 层功能

---

### 建议 2：提取前端公共组件

**目标：** 消除 `OrgStage.vue` 和 `OrgStandard.vue` 的重复代码

**实施方案：**

```vue
<!-- YzhOrgLink.vue - 通用机构关联组件 -->
<template>
  <YzhTreeCheckboxTable
    ref="linkTableRef"
    :tree-data="treeData"
    :tree-title="treeTitle"
    :columns="tableColumns"
    :load-data-fn="loadDataFn"
    :link-api="linkApi"
    row-key-field="Id"
    auto-save
    allow-refresh
    @tree-node-select="handleTreeSelect"
  />
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import YzhTreeCheckboxTable from '@/yzh/components/YzhTreeCheckboxTable.vue'
import http from '@/api/http'

// Props
const props = defineProps<{
  treeTitle: string
  columns: any[]
  loadDataFn: (params: any) => Promise<any>
  syncApi: string  // 如 '/api/org-link/SyncOrgStandards'
  getIdsApi: string  // 如 '/api/org-link/GetOrgStdIds'
}>()

// 公共逻辑
const treeData = ref<any[]>([])

async function loadOrgTree() {
  const res: any = await http.post('/api/CertCertificationBody/GetPageData', {
    page: 1,
    rows: 1000,
    filter: [{ name: 'Status', value: 'active', displayType: '==' }],
  }, null, false)
  
  const rows = res?.data?.rows || res?.rows || []
  treeData.value = rows.map(item => ({
    ...item,
    Code: item.Code || item.Id,
    keyField: item.Id,
  }))
}

const linkApi = {
  async syncFn(cbCode: string, addIds: number[], removeIds: number[]) {
    return http.post(props.syncApi, { CbCode: cbCode, AddStdIds: addIds, RemoveStdIds: removeIds }, null, false)
  },
  async getIdsFn(cbCode: string) {
    const res: any = await http.get(`${props.getIdsApi}/${cbCode}`, null, false)
    return (res?.Data || res?.data || []).map((id: any) => String(id))
  },
}

onMounted(() => {
  loadOrgTree()
})
</script>
```

**使用示例：**
```vue
<!-- OrgStage.vue -->
<YzhOrgLink
  tree-title="认证机构"
  :columns="tableColumns"
  :load-data-fn="loadStages"
  sync-api="/api/org-link/SyncOrgStages"
  get-ids-api="/api/org-link/GetOrgStageIds"
/>

<!-- OrgStandard.vue -->
<YzhOrgLink
  tree-title="认证机构"
  :columns="tableColumns"
  :load-data-fn="loadStandards"
  sync-api="/api/org-link/SyncOrgStandards"
  get-ids-api="/api/org-link/GetOrgStdIds"
/>
```

**优点：**
- 消除重复代码
- 统一错误处理
- 便于维护

---

### 建议 3：统一使用 T+V 模式

**目标：** 为关联表也创建视图，统一数据展示方式

**实施方案：**

```sql
-- 创建 cert_org_stage 视图
DROP VIEW IF EXISTS v_cert_org_stage;
CREATE VIEW v_cert_org_stage AS
SELECT 
    os.*,
    s.StageCode,
    s.StageName,
    cat.DicName AS CategoryName,
    sta.DicName AS StatusName
FROM cert_org_stage os
LEFT JOIN cert_cert_stage s ON os.StageId = s.Id
LEFT JOIN Sys_DictionaryList cat ON s.Category = cat.DicValue AND cat.Dic_ID IN (
    SELECT Dic_ID FROM Sys_Dictionary WHERE DicNo = 'stage_category'
)
LEFT JOIN Sys_DictionaryList sta ON s.Status = sta.DicValue AND sta.Dic_ID IN (
    SELECT Dic_ID FROM Sys_Dictionary WHERE DicNo = 'stage_status'
);
```

```csharp
// CertOrgStageView.cs
[Table("v_cert_org_stage")]
public class CertOrgStageView : CertOrgStage
{
    public string StageName { get; set; }
    public string CategoryName { get; set; }
    public string StatusName { get; set; }
}
```

**优点：**
- 统一数据展示方式
- 减少前端字典翻译逻辑

---

### 建议 4：统一实体基类

**目标：** 为关联表也添加完整的审计字段

**实施方案：**

```csharp
// 方案 A：关联表继承 YZHBaseEntity
public class CertOrgStage : YZHBaseEntity
{
    public string CbCode { get; set; }
    public long StageId { get; set; }
    // ... 其他字段
}

// 方案 B：创建轻量级审计基类
public class AuditBaseEntity : BaseEntity
{
    public int? CreateID { get; set; }
    public string Creator { get; set; }
    public DateTime? CreateDate { get; set; }
    public int? ModifyID { get; set; }
    public string Modifier { get; set; }
    public DateTime? ModifyDate { get; set; }
    public int? DeleteID { get; set; }
    public string Deleter { get; set; }
    public DateTime? DeleteTime { get; set; }
    public bool Enable { get; set; } = true;
}
```

**优点：**
- 统一数据审计
- 便于数据追溯

---

### 建议 5：统一前端配置格式

**目标：** 所有页面统一使用 T+V 模式（视图字段）

**实施方案：**

```javascript
// ISOStandard.vue options.js - 修改为视图字段
columns: [
  { field: 'StandardCode', title: '标准编号', width: 160, sortable: true },
  { field: 'StandardName', title: '标准名称', width: 280, sortable: true },
  { field: 'VersionYear', title: '版本', width: 80, align: 'center' },
  { field: 'CategoryName', title: '分类', width: 120, align: 'center' },  // 视图字段
  { field: 'StatusName', title: '状态', width: 100, align: 'center' },     // 视图字段
]
```

```csharp
// ISOStandardService.cs - 确保返回视图数据
public override PageGridData<ISOStandard> GetPageData(PageDataOptions options)
{
    var query = _db.Set<ISOStandardView>();  // 使用视图
    // ...
}
```

**优点：**
- 前端显示一致性
- 减少字典翻译逻辑

---

## 四、实施优先级

| 优先级 | 建议 | 工作量 | 收益 |
|--------|------|--------|------|
| P0 | 建议 5：统一前端配置格式 | 小 | 高 - 立即改善用户体验 |
| P1 | 建议 2：提取前端公共组件 | 中 | 高 - 消除重复代码 |
| P1 | 建议 3：统一使用 T+V 模式 | 中 | 高 - 数据展示统一 |
| P2 | 建议 1：为关联管理创建 Service 层 | 大 | 中 - 架构更规范 |
| P3 | 建议 4：统一实体基类 | 大 | 中 - 数据审计统一 |

---

## 五、总结

当前架构存在以下主要问题：

1. **分层不清晰**：`OrgLinkController` 绕过了 Service 层
2. **代码重复**：`OrgStage.vue` 和 `OrgStandard.vue` 有大量重复代码
3. **模式不统一**：部分页面使用 T+V 模式，部分使用字典翻译
4. **实体设计不一致**：关联表缺少审计字段

建议按优先级逐步实施优化，首先统一前端配置格式，然后提取公共组件，最后重构后端架构。
