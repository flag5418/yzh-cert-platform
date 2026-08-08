<template>
  <YzhOrgLink
    tree-title="认证机构"
    :columns="tableColumns"
    :load-data-fn="loadStandards"
    sync-api="/api/org-link/SyncOrgStandards"
    get-ids-api="/api/org-link/GetOrgStdIds"
    @tree-select="handleTreeSelect"
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
import { ref } from 'vue'
import YzhOrgLink from '@/yzh/components/YzhOrgLink.vue'
import http from '@/api/http'

// ============================================================
// 右侧表格列配置（使用视图字段，中文显示）
// ============================================================

const tableColumns = [
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
    
    // Vol 返回格式：res.rows（不是 res.data.rows）
    const rows = res?.rows || res?.data?.rows || []
    const total = res?.total || res?.data?.total || 0
    
    return { data: rows, total }
  } catch (e) {
    console.error('[OrgStandard] 加载标准列表失败', e)
    return { data: [], total: 0 }
  }
}

// ============================================================
// 事件处理
// ============================================================

function handleTreeSelect(data: any) {
  console.log('[OrgStandard] 选择机构:', data.Name, data.Code)
}
</script>
