<!--
  机构-XX 关联管理通用组件
  用于 OrgStage 和 OrgStandard 关联管理页面
  
  特性：
  - 左侧：认证机构树（复用 CertCertificationBody 数据）
  - 右侧：关联列表（checkbox 表格）
  - 自动保存：勾选/取消勾选时立即同步到数据库
-->
<template>
  <YzhTreeCheckboxTable
    ref="linkTableRef"
    :tree-data="treeData"
    :tree-title="treeTitle"
    :columns="columns"
    :load-data-fn="loadDataFn"
    :link-api="linkApi"
    row-key-field="Id"
    auto-save
    allow-refresh
    @tree-node-select="handleTreeSelect"
  />
</template>

<script setup lang="ts">
/**
 * 机构-XX 关联管理通用组件
 * 
 * Props:
 * - treeTitle: 树标题（如 "认证机构"）
 * - columns: 表格列配置
 * - loadDataFn: 数据加载函数
 * - syncApi: 同步 API 路径（如 "/api/org-link/SyncOrgStandards"）
 * - getIdsApi: 获取已选 IDs API 路径（如 "/api/org-link/GetOrgStdIds"）
 * 
 * Emits:
 * - tree-select: 树节点选中事件
 */
import { ref, onMounted } from 'vue'
import YzhTreeCheckboxTable from './YzhTreeCheckboxTable.vue'
import http from '@/api/http'

// ============================================================
// Props & Emits
// ============================================================

interface Props {
  treeTitle: string
  columns: any[]
  loadDataFn: (params: any) => Promise<any>
  syncApi: string
  getIdsApi: string
}

interface Emits {
  (e: 'tree-select', data: any): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// ============================================================
// 响应式数据
// ============================================================

const linkTableRef = ref()
const treeData = ref<any[]>([])

// ============================================================
// 加载机构树
// ============================================================

async function loadOrgTree() {
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
    
    // Vol 返回格式可能是 res.rows 或 res.data.rows
    const rows = res?.data?.rows || res?.rows || []
    
    if (Array.isArray(rows) && rows.length > 0) {
      treeData.value = rows.map((item: any) => ({
        ...item,
        Code: item.Code || item.Id,
        keyField: item.Id,
      }))
    }
  } catch (e) {
    console.error('[YzhOrgLink] 加载机构树失败', e)
  }
}

// ============================================================
// 关联 API
// ============================================================

const linkApi = {
  async syncFn(cbCode: string, addIds: number[], removeIds: number[]) {
    // 根据 syncApi 动态构建请求参数
    const isStage = props.syncApi.includes('Stage')
    const request = {
      CbCode: cbCode,
      [isStage ? 'AddStageIds' : 'AddStdIds']: addIds,
      [isStage ? 'RemoveStageIds' : 'RemoveStdIds']: removeIds,
    }
    
    return http.post(props.syncApi, request, null, false)
  },

  async getIdsFn(cbCode: string) {
    const res: any = await http.get(`${props.getIdsApi}/${cbCode}`, null, false)
    
    // Vol Controller 返回格式：{ Status:true, Data: [id1, id2, ...] }
    // 需要转为 String 以匹配表格的 rowKeyField（Id 是字符串）
    const ids = (res?.Data || res?.data || res?.rows || []).map((id: any) => String(id))
    return ids
  },
}

// ============================================================
// 事件处理
// ============================================================

function handleTreeSelect(data: any) {
  emit('tree-select', data)
}

// ============================================================
// 初始化
// ============================================================

onMounted(() => {
  loadOrgTree()
})
</script>
