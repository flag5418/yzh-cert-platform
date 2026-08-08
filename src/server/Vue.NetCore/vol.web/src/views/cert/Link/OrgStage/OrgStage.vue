<template>
  <YzhTreeCheckboxTable
    ref="linkTableRef"
    :tree-data="treeData"
    tree-title="认证机构"
    :columns="tableColumns"
    :load-data-fn="loadStages"
    :link-api="linkApi"
    row-key-field="Id"
    auto-save
    allow-refresh
    @tree-node-select="handleTreeSelect"
  />
</template>

<script setup lang="ts">
/**
 * 机构-阶段关联管理
 *
 * 左侧：认证机构树
 * 右侧：认证阶段列表（checkbox 表格，默认全选）
 * 操作：勾选即保存到 cert_org_stage 关联表
 *
 * 设计决策（2026-08-07 确认）：
 * - 新建机构时自动在 cert_org_stage 中插入全部 9 个阶段记录
 * - 阶段框架统一（ISO/IEC 17021-1），所有机构一致
 */
import { ref, onMounted } from 'vue'
import YzhTreeCheckboxTable from '@/yzh/components/YzhTreeCheckboxTable.vue'
import http from '@/api/http'

const linkTableRef = ref()

// ============================================================
// 左侧树：认证机构
// ============================================================

const treeData = ref<any[]>([])

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
    console.log('[OrgStage] 📦 机构树 API 响应:', JSON.stringify(res))
    console.log('[OrgStage] 📦 res.data:', JSON.stringify(res?.data))
    console.log('[OrgStage] 📦 res.rows:', JSON.stringify(res?.rows))
    
    // Vol 返回格式可能是 res.rows 或 res.data.rows
    const rows = res?.data?.rows || res?.rows || []
    console.log(`[OrgStage] 📦 解析到 rows: ${Array.isArray(rows) ? rows.length : '非数组'}`, rows)
    
    if (Array.isArray(rows) && rows.length > 0) {
      treeData.value = rows.map((item: any) => ({
        ...item,
        Code: item.Code || item.Id,
        keyField: item.Id,
      }))
      console.log(`[OrgStage] ✅ 机构树加载完成: ${treeData.value.length} 条`)
    } else {
      console.warn('[OrgStage] ⚠️ 机构树响应无 rows 数据:', res)
    }
  } catch (e) {
    console.error('[OrgStage] ❌ 加载机构树失败', e)
  }
}

// ============================================================
// 右侧表格列配置
// ============================================================

const tableColumns = [
  { field: 'Id', title: 'ID', width: 70, align: 'center' },
  { field: 'StageCode', title: '阶段编码', width: 120 },
  { field: 'StageName', title: '阶段名称', width: 180 },
  { field: 'SortOrder', title: '排序', width: 80, align: 'center' },
  { field: 'CategoryName', title: '分类', width: 120, align: 'center' },        // ✅ 视图字段，中文
  { field: 'StatusName', title: '状态', width: 100, align: 'center' },          // ✅ 视图字段，中文
]

// ============================================================
// 数据加载
// ============================================================

async function loadStages(params: any) {
  console.log('[OrgStage] 📋 加载阶段列表: params=', params)
  try {
    const res: any = await http.post('/api/CertStage/GetPageData', {
      page: params.page || 1,
      rows: params.rows || 50,
      order: 'asc',
      sort: 'SortOrder',
      wheres: '',
      value: params.filterValue || '',
      filter: [],
    }, null, false)
    console.log('[OrgStage] 📦 阶段列表 API 响应:', res)
    
    // Vol 返回格式：res.rows（不是 res.data.rows）
    const rows = res?.rows || res?.data?.rows || []
    const total = res?.total || res?.data?.total || 0
    console.log(`[OrgStage] ✅ 阶段列表返回: ${rows.length} 条, 总计 ${total}`)
    
    return { data: rows, total }
  } catch (e) {
    console.error('[OrgStage] 加载阶段列表失败', e)
    return { data: [], total: 0 }
  }
}

// ============================================================
// 关联 API
// ============================================================

const linkApi = {
  async syncFn(cbCode: string, addIds: number[], removeIds: number[]) {
    return http.post('/api/org-link/SyncOrgStages', {
      CbCode: cbCode,
      AddStageIds: addIds,
      RemoveStageIds: removeIds,
    }, null, false)
  },

  async getIdsFn(cbCode: string) {
    const res: any = await http.get(`/api/org-link/GetOrgStageIds/${cbCode}`, null, false)
    console.log('[OrgStage] 🔗 GetOrgStageIds 响应:', res)
    console.log('[OrgStage] 🔗 GetOrgStageIds Data:', JSON.stringify(res?.Data))
    
    // Vol Controller 返回格式：{ Status:true, Data: [id1, id2, ...] }
    // 需要转为 String 以匹配表格的 rowKeyField（Id 是字符串）
    const ids = (res?.Data || res?.data || res?.rows || []).map((id: any) => String(id))
    console.log(`[OrgStage] 🔗 解析到已勾选 IDs(${ids.length}):`, ids)
    return ids
  },
}

// ============================================================
// 事件处理
// ============================================================

function handleTreeSelect(data: any) {
  console.log('[OrgStage] 选择机构:', data.Name, data.Code)
}

// ============================================================
// 初始化
// ============================================================

onMounted(() => {
  loadOrgTree()
})
</script>
