/**
 * 认证任务管理 Logic —— V2 独立认证检查 · 列表
 *
 * 后端：`/api/Auditor/CertTask`
 * 契约：PascalCase；业务失败恒 HTTP 200，success 唯一判据。
 */
import { ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import router from '@/router'
import {
  pauseCertTask,
  startCertTask,
  type CertTaskRow,
} from '@share/api/auditor/cert-check-task'
import { yzhApi } from '@yzh-core'

/**
 * 任务状态 → 中文。
 * ⚠️ 取值以**后端实际写入的**为准（`cert_task.Status`）：
 *    pending / running / paused / done / partial / failed / cancelled
 *    ⛔ 不是 `completed` —— 那是前端早期自己编的词，后端从来没写过，
 *    导致列表里 `done` 显示成英文（2026-11-02 修）。
 *    `completed` 仍保留，兼容历史缓存数据。
 */
const STATUS_LABELS: Record<string, string> = {
  pending: '待启动',
  running: '运行中',
  paused: '已暂停',
  done: '已完成',
  completed: '已完成',
  partial: '部分完成',
  failed: '失败',
  cancelled: '已取消',
  waiting: '排队中',
  skipped: '已跳过',
}

/** el-tag 支持的语义色（避免 Record<string,string> 落到 el-tag 的联合类型上） */
type TagType = 'primary' | 'success' | 'warning' | 'info' | 'danger'

const STATUS_TAGS: Record<string, TagType> = {
  pending: 'info',
  waiting: 'info',
  running: 'primary',
  paused: 'warning',
  partial: 'warning',
  done: 'success',
  completed: 'success',
  failed: 'danger',
  cancelled: 'info',
  skipped: 'info',
}

const TASK_KIND_LABELS: Record<string, string> = {
  prep: '准备',
  onsite: '现场',
}

export function useCertTaskList() {
  const loading = ref(false)
  const rows = ref<CertTaskRow[]>([])
  const total = ref(0)
  const query = ref({ keyword: '', status: '', taskKind: '' })

  async function loadList() {
    loading.value = true
    try {
      const res = await yzhApi.post('/api/Auditor/CertTask/page', {
        PageIndex: 1,
        PageSize: 50,
        Keyword: query.value.keyword || undefined,
        Status: query.value.status || undefined,
        TaskKind: query.value.taskKind || undefined,
      })
      if (res.success && res.data) {
        const d = res.data as { Total: number; Rows: CertTaskRow[] }
        rows.value = d.Rows ?? []
        total.value = d.Total ?? 0
      }
    } finally {
      loading.value = false
    }
  }

  async function start(code: string) {
    await ElMessageBox.confirm('确认启动该任务？', '启动')
    const res = await startCertTask(code)
    ElMessage[res.success ? 'success' : 'error'](res.message || (res.success ? '已启动' : '失败'))
    if (res.success) await loadList()
  }

  async function pause(code: string) {
    await ElMessageBox.confirm('确认暂停该任务？', '暂停')
    const res = await pauseCertTask(code)
    ElMessage[res.success ? 'success' : 'error'](res.message || (res.success ? '已暂停' : '失败'))
    if (res.success) await loadList()
  }

  function viewDetail(code: string) {
    router.push(`/audit-task/${code}`)
  }

  function goAnalyze(code: string) {
    router.push(`/audit-task/analyze?taskCode=${code}`)
  }

  return {
    loading, rows, total, query,
    loadList, start, pause, viewDetail, goAnalyze,
    statusLabel: (s: string) => STATUS_LABELS[s] ?? s,
    statusTag: (s: string): TagType => STATUS_TAGS[s] ?? 'info',
    taskKindLabel: (k: string) => TASK_KIND_LABELS[k] ?? k,
  }
}
