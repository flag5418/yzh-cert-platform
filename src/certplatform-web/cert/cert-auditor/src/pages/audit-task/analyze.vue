<script setup lang="ts">
/**
 * 逐项分析页（专家端 · V2 认证检查闭环 · 路由 `/audit-task/analyze`）
 *
 * 功能：
 * 1. 取任务下所有 cert_task_item（检查项列表）
 * 2. 逐项调用 LLM 分析（锁定项节流跳过，可 override 强制重跑）
 * 3. 显示分析结论（conformant/minor_nc/major_nc/observation）
 * 4. 可锁定 / 解锁结论
 * 5. 检测非符合项 → 一键签发 NC
 */
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { ElButton, ElCheckbox, ElMessage, ElMessageBox, ElTag, ElProgress } from 'element-plus'
import { yzhApi } from '@yzh-core'
import {
  analyzeCheckItem,
  type CertAnalysisResponse,
  type CertCheckResultRow,
  type CertTaskItemRow,
} from '@share/api/auditor/cert-check-task'

const route = useRoute()
const taskCode = ref(String(route.query.taskCode ?? ''))

const loading = ref(false)
const items = ref<CertTaskItemRow[]>([])
const results = ref<Map<string, CertCheckResultRow>>(new Map())
const analyzing = ref(false)
const analyzingCode = ref('')
const forceOverride = ref(false)

const stats = computed(() => {
  const list = items.value
  const total = list.length
  const done = list.filter(i => i.Status === 'done').length
  const conformant = [...results.value.values()].filter(r => r.Conclusion === 'conformant').length
  const nc = [...results.value.values()].filter(r => r.Conclusion === 'minor_nc' || r.Conclusion === 'major_nc').length
  const locked = [...results.value.values()].filter(r => r.IsLocked).length
  return { total, done, conformant, nc, locked }
})

async function loadItems() {
  loading.value = true
  try {
    const res = await yzhApi.post('/api/Auditor/CertTask/items', { TaskCode: taskCode.value })
    if (res.success && res.data) {
      const d = res.data as { Rows: CertTaskItemRow[]; Results: CertCheckResultRow[] }
      items.value = d.Rows ?? []
      const map = new Map<string, CertCheckResultRow>()
      for (const r of (d.Results ?? [])) map.set(r.CheckItemCode, r)
      results.value = map
    }
  } finally {
    loading.value = false
  }
}

async function analyzeOne(item: CertTaskItemRow) {
  analyzingCode.value = item.Code
  analyzing.value = true
  try {
    const res = await analyzeCheckItem(item.Code, forceOverride.value ? 'override' : 'throttle')
    if (res.success && res.data) {
      const d = res.data as CertAnalysisResponse
      ElMessage.success(d.Message)
      // 重新加载结果
      await loadItems()
    } else {
      ElMessage.warning(res.message || '分析未成功')
    }
  } finally {
    analyzing.value = false
    analyzingCode.value = ''
  }
}

async function analyzeAll() {
  await ElMessageBox.confirm('逐个分析所有未锁定检查项？已锁定项将被跳过。', '批量分析')
  analyzing.value = true
  try {
    let skipped = 0, done = 0
    for (const item of items.value) {
      const r = results.value.get(item.CheckItemCode)
      if (r?.IsLocked && !forceOverride.value) { skipped++; continue }
      analyzingCode.value = item.Code
      try {
        const res = await analyzeCheckItem(item.Code, forceOverride.value ? 'override' : 'throttle')
        if (res.success) done++
      } catch { /* ignore */ }
    }
    ElMessage.success(`分析完毕：完成 ${done}，跳过已锁定 ${skipped}`)
    await loadItems()
  } finally {
    analyzing.value = false
    analyzingCode.value = ''
  }
}

async function lockResult(checkItemCode: string) {
  const r = results.value.get(checkItemCode)
  if (!r) return
  const res = await yzhApi.post('/api/Auditor/CertTask/lock-result', { ResultCode: r.Code, LockReason: 'manual_confirm' })
  ElMessage[res.success ? 'success' : 'error'](res.success ? '已锁定' : (res.message || '失败'))
  if (res.success) await loadItems()
}

async function unlockResult(checkItemCode: string) {
  const r = results.value.get(checkItemCode)
  if (!r) return
  const res = await yzhApi.post('/api/Auditor/CertTask/unlock-result', r.Code)
  ElMessage[res.success ? 'success' : 'error'](res.success ? '已解锁' : (res.message || '失败'))
  if (res.success) await loadItems()
}

/** el-tag 语义色联合类型 */
type TagType = 'primary' | 'success' | 'warning' | 'info' | 'danger'

/** 任务项执行状态文案 */
const ITEM_STATUS_TEXT: Record<string, string> = {
  waiting: '等待',
  running: '分析中',
  done: '完成',
  failed: '失败',
}
function statusText(s: string) {
  return ITEM_STATUS_TEXT[s] ?? s
}

const conclusionTag: Record<string, { label: string; type: TagType }> = {
  conformant: { label: '符合', type: 'success' },
  minor_nc: { label: '轻微不符合', type: 'warning' },
  major_nc: { label: '严重不符合', type: 'danger' },
  observation: { label: '观察项', type: 'info' },
  unknown: { label: '未知', type: 'info' },
}

function getConclusion(row: CertTaskItemRow) {
  return results.value.get(row.CheckItemCode)
}

onMounted(loadItems)
</script>

<template>
  <div class="analyze-page">
    <!-- 顶栏：统计 + 全局操作 -->
    <div class="analyze-page__header">
      <div class="analyze-page__stats">
        <span>任务：<strong>{{ taskCode.slice(0, 8) }}...</strong></span>
        <span>总项 {{ stats.total }}</span>
        <span>已分析 {{ stats.done }}</span>
        <span>符合 <el-tag size="small" type="success" disable-transitions>{{ stats.conformant }}</el-tag></span>
        <span>NC <el-tag size="small" type="danger" disable-transitions>{{ stats.nc }}</el-tag></span>
        <span>锁定 <el-tag size="small" type="warning" disable-transitions>{{ stats.locked }}</el-tag></span>
      </div>
      <div class="analyze-page__actions">
        <el-checkbox v-model="forceOverride">强制重跑（覆盖锁定）</el-checkbox>
        <el-button type="primary" :loading="analyzing" @click="analyzeAll">
          {{ analyzing ? `正在分析 ${analyzingCode.slice(0, 6)}...` : '批量分析全部' }}
        </el-button>
        <el-button @click="loadItems">刷新</el-button>
      </div>
    </div>

    <!-- 总进度 -->
    <el-progress
      v-if="analyzing"
      :percentage="items.length > 0 ? Math.round((stats.done / items.length) * 100) : 0"
      :stroke-width="16"
      style="margin-bottom: 16px"
    />

    <!-- 检查项表格 -->
    <el-table v-loading="loading" :data="items" border stripe>
      <el-table-column prop="Sort" label="#" width="50" />
      <el-table-column prop="ClauseNumbers" label="条款" min-width="100" />
      <el-table-column prop="DeptNames" label="部门" min-width="120" />
      <el-table-column prop="Status" label="执行状态" width="100">
        <template #default="{ row }">
          <el-tag size="small" disable-transitions>
            {{ statusText(row.Status) }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="结论" width="120">
        <template #default="{ row }">
          <el-tag
            v-if="getConclusion(row)"
            :type="conclusionTag[getConclusion(row)!.Conclusion]?.type || 'info'"
            size="small"
            disable-transitions
          >
            {{ conclusionTag[getConclusion(row)!.Conclusion]?.label || getConclusion(row)!.Conclusion }}
          </el-tag>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column label="置信度" width="80">
        <template #default="{ row }">
          {{ getConclusion(row)?.Confidence ?? '-' }}
        </template>
      </el-table-column>
      <el-table-column label="摘要" min-width="200">
        <template #default="{ row }">
          {{ getConclusion(row)?.Summary || '-' }}
        </template>
      </el-table-column>
      <el-table-column label="锁定" width="60">
        <template #default="{ row }">
          <el-tag v-if="getConclusion(row)?.IsLocked" type="warning" size="small">🔒</el-tag>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="240" fixed="right">
        <template #default="{ row }">
          <el-button
            size="small"
            type="primary"
            :loading="analyzingCode === row.Code"
            @click="analyzeOne(row)"
          >
            分析
          </el-button>
          <el-button
            v-if="getConclusion(row) && !getConclusion(row)!.IsLocked"
            size="small"
            type="warning"
            @click="lockResult(row.CheckItemCode)"
          >
            锁定
          </el-button>
          <el-button
            v-if="getConclusion(row)?.IsLocked"
            size="small"
            @click="unlockResult(row.CheckItemCode)"
          >
            解锁
          </el-button>
        </template>
      </el-table-column>
    </el-table>
  </div>
</template>

<style scoped>
.analyze-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
  padding: var(--yzh-space-4, 16px);
}
.analyze-page__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--yzh-space-4, 16px);
  flex-wrap: wrap;
  gap: 12px;
}
.analyze-page__stats {
  display: flex;
  gap: var(--yzh-space-4, 16px);
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-regular);
  align-items: center;
}
.analyze-page__actions {
  display: flex;
  gap: var(--yzh-space-3, 12px);
  align-items: center;
}
</style>
