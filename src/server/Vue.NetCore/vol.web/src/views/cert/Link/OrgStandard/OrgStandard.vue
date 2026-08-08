<template>
  <YzhTreeCheckboxTable
    ref="linkTableRef"
    :tree-data="treeData"
    tree-title="认证机构"
    :columns="tableColumns"
    :load-data-fn="loadStandards"
    :link-api="linkApi"
    row-key-field="Id"
    auto-save
    allow-refresh
    @tree-node-select="handleTreeSelect"
  />
</template>

<script setup lang="ts">
/**
 * 机构-标准关联管理
 *
 * 左侧：认证机构树
 * 右侧：ISO 标准列表（checkbox 表格）
 * 操作：勾选即保存到 cert_org_standard 关联表
 */
import { ref, onMounted, markRaw } from 'vue'
import YzhTreeCheckboxTable from '@/yzh/components/YzhTreeCheckboxTable.vue'
import http from '@/api/http'

const linkTableRef = ref()

// ============================================================
// 左侧树：认证机构
// ============================================================

const treeData = ref<any[]>([])

async function loadOrgTree() {
  console.log('[OrgStandard] 🌲 开始加载机构树...')
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
    console.log('[OrgStandard] 📦 机构树 API 响应:', JSON.stringify(res))
    console.log('[OrgStandard] 📦 res.data:', JSON.stringify(res?.data))
    console.log('[OrgStandard] 📦 res.rows:', JSON.stringify(res?.rows))
    
    // Vol 返回格式可能是 res.rows 或 res.data.rows
    const rows = res?.data?.rows || res?.rows || []
    console.log(`[OrgStandard] 📦 解析到 rows: ${Array.isArray(rows) ? rows.length : '非数组'}`, rows)
    
    if (Array.isArray(rows) && rows.length > 0) {
      treeData.value = rows.map((item: any) => ({
        ...item,
        Code: item.Code || item.Id,
        keyField: item.Id,
      }))
      console.log(`[OrgStandard] ✅ 机构树加载完成: ${treeData.value.length} 条`)
    } else {
      console.warn('[OrgStandard] ⚠️ 机构树响应无 rows 数据:', res)
    }
  } catch (e) {
    console.error('[OrgStandard] ❌ 加载机构树失败', e)
  }
}

// 页面加载时获取机构树数据
onMounted(() => {
  loadOrgTree()
})

// ============================================================
// 右侧表格列配置（使用视图字段，中文显示）
// ============================================================

const tableColumns = [
  // { field: 'Id', title: 'ID', width: 70, align: 'center', sortable: false, hidden: true },  // 隐藏 ID
  { field: 'StandardCode', title: '标准编号', width: 160, sortable: true },
  { field: 'StandardName', title: '标准名称', width: 280, sortable: true, showOverflow: true },
  { field: 'VersionYear', title: '版本', width: 80, align: 'center' },
  { field: 'CategoryName', title: '分类', width: 120, align: 'center' },        // ✅ 视图字段，中文
  { field: 'StatusName', title: '状态', width: 100, align: 'center' },          // ✅ 视图字段，中文
]

// ============================================================
// 数据加载
// ============================================================

async function loadStandards(params: any) {
  console.log('[OrgStandard] 📋 加载标准列表: params=', params)
  try {
    const res: any = await http.post('/api/ISOStandard/GetPageData', {
      page: params.page || 1,
      rows: params.rows || 50,
      order: 'desc',
      sort: 'CreateDate',
      wheres: '',
      value: params.filterValue || '',
      filter: [],
    }, null, false)
    console.log('[OrgStandard] 📦 标准列表 API 响应:', res)
    
    // Vol 返回格式：res.rows（不是 res.data.rows）
    const rows = res?.rows || res?.data?.rows || []
    const total = res?.total || res?.data?.total || 0
    console.log(`[OrgStandard] ✅ 标准列表返回: ${rows.length} 条, 总计 ${total}`)
    
    return { data: rows, total }
  } catch (e) {
    console.error('[OrgStandard] 加载标准列表失败', e)
    return { data: [], total: 0 }
  }
}

// ============================================================
// 关联 API
// ============================================================

const linkApi = {
  async syncFn(cbCode: string, addIds: number[], removeIds: number[]) {
    return http.post('/api/org-link/SyncOrgStandards', {
      CbCode: cbCode,
      AddStdIds: addIds,
      RemoveStdIds: removeIds,
    }, null, false)
  },

  async getIdsFn(cbCode: string) {
    const res: any = await http.get(`/api/org-link/GetOrgStdIds/${cbCode}`, null, false)
    console.log('[OrgStandard] 🔗 GetOrgStdIds 响应:', res)
    console.log('[OrgStandard] 🔗 GetOrgStdIds Data:', JSON.stringify(res?.Data))
    
    // Vol Controller 返回格式：{ Status:true, Data: [id1, id2, ...] }
    // 需要转为 String 以匹配表格的 rowKeyField（Id 是字符串）
    const ids = (res?.Data || res?.data || res?.rows || []).map((id: any) => String(id))
    console.log(`[OrgStandard] 🔗 解析到已勾选 IDs(${ids.length}):`, ids)
    return ids
  },
}

// ============================================================
// 事件处理
// ============================================================

function handleTreeSelect(data: any) {
  console.log('[OrgStandard] 选择机构:', data.Name, data.Code)
}
</script>
