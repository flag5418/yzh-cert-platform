<template>
  <YzhOrgLink
    tree-title="认证机构"
    :columns="tableColumns"
    :load-data-fn="loadStages"
    sync-api="/api/org-link/SyncOrgStages"
    get-ids-api="/api/org-link/GetOrgStageIds"
    @tree-select="handleTreeSelect"
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
import { ref } from 'vue'
import YzhOrgLink from '@/yzh/components/YzhOrgLink.vue'
import http from '@/api/http'

// ============================================================
// 右侧表格列配置（使用视图字段，中文显示）
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
    
    // Vol 返回格式：res.rows（不是 res.data.rows）
    const rows = res?.rows || res?.data?.rows || []
    const total = res?.total || res?.data?.total || 0
    
    return { data: rows, total }
  } catch (e) {
    console.error('[OrgStage] 加载阶段列表失败', e)
    return { data: [], total: 0 }
  }
}

// ============================================================
// 事件处理
// ============================================================

function handleTreeSelect(data: any) {
  console.log('[OrgStage] 选择机构:', data.Name, data.Code)
}
</script>
