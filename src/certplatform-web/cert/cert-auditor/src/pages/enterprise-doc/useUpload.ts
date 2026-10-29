/**
 * 企业文档页 · **上传状态机**（五段式：plan 预检 → init → file… → confirm，cancel 可回滚）
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★★ 与老 `resources/useUpload.ts` 的本质差别（36 号 §6.3）
 *
 * 老状态机要处理「一个文件夹里可能有 3 个标准的文件」⇒ 槽位派发 / 未归属指派 /
 * 扩展名不兼容 / M3 待确认…… 一整套 900 行的分派逻辑。
 *
 * **新模型下这些全都不存在**：
 *   · 上传**不带标准**（标准只在「提取」阶段出现）⇒ 无槽位、无派发、无未归属
 *   · 上传**不带阶段**（一阶段与二阶段是同一批文件）⇒ 无阶段选择
 *   ⇒ 上传退化成「选文件 → 传字节 → 入队转换」，剩下的交给后端判重。
 * ════════════════════════════════════════════════════════════════════════
 *
 * ★ 两条不能动的硬约束
 *   ① **关联键 = `webkitRelativePath || name`** —— 一个文件夹里可以有同名文件
 *      （`1质量手册/x.doc` 与 `4记录文件/x.doc`），用 `File.name` 做索引会把多行塌缩到
 *      同一个 File ⇒ 同一份字节被写进多个位置。
 *   ② `Blocked` / `skip` 的行**默认不勾选**（后端已判过，前端不替用户决定）。
 *
 * ⚠️ 状态**不放在模块作用域**：由 `createUploadState()` 每次调用新建，
 *    挂在 logic 实例上 ⇒ 离开页面再进来一定重置、多标签页互不干扰（R24）。
 */
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import {
  entDocPlanUpload,
  entDocUploadCancel,
  entDocUploadConfirm,
  entDocUploadFile,
  entDocUploadInit,
  type EntDocPlanItem,
  type EntDocPlanRow,
  type EntDocPlanSummary,
} from '@share/api/ent/enterprise-doc'

/**
 * 宿主能力（由 `EnterpriseDocLogic` 提供）。
 *
 * ⛔ 不让本文件 import logic 再 `new` —— 会成环，而且状态就不再随页面实例生灭。
 */
export interface UploadHost {
  /** 当前选中的企业 Code（空串 = 没选，⛔ 不许上传） */
  enterpriseCode(): string
  /** 上传成功后的收尾（刷新树 + 表 + 状态条） */
  afterUploaded(): Promise<void>
}

/** 待上传项（File + 相对文件夹路径） */
export interface PickedFile {
  file: File
  /** 相对文件夹路径（空串 = 根目录） */
  relFolderPath: string
  /** 关联键（`webkitRelativePath || name`）—— 见文件头硬约束 ① */
  key: string
}

/** 预检行 + 前端勾选态 */
export interface UploadPlanItem extends EntDocPlanRow {
  /**
   * 行唯一键 = `RelFolderPath|FileName`。
   *
   * ⛔ 不能拿 `FileName` 当表格 `row-key`：一个文件夹里可以有同名文件，
   *   键重复会让表格的行状态（勾选 / 展开）串行。
   */
  Key: string
  /** 是否参与本次上传（`Blocked` / `skip` 默认 false） */
  Checked: boolean
  /** 对应的 File（按 key 关联，⛔ 不按 FileName —— 同名文件会串） */
  File: File | null
}

/**
 * 客户端 sha256 的体积上限（64 MB）。
 *
 * ★ 为什么要有上限：`crypto.subtle.digest` 需要把整个文件读进内存，
 *   一个 200MB 的扫描件会把标签页顶爆。≤64MB 覆盖了绝大多数 Word/Excel/PDF 文档。
 * ⚠️ 超过上限 ⇒ 传空 sha ⇒ 后端按「同路径 = 替换」处理（版本 +1，重跑转换/提取）。
 *   这是**已知取舍**，不是 bug：宁可多跑一次，也不要在用户浏览器里 OOM。
 */
const SHA_LIMIT_BYTES = 64 * 1024 * 1024

/**
 * 计算 sha256（十六进制小写）。
 *
 * ⚠️ `crypto.subtle` 只在**安全上下文**（https / localhost）可用；
 *   局域网 http 部署时它是 `undefined` ⇒ 这里返回空串，上传照常进行（降级为「替换」）。
 * ⛔ 绝不因为算不出 hash 就中断上传 —— 用户要的是把文件传上去。
 */
export async function sha256OfFile(file: File, limit = SHA_LIMIT_BYTES): Promise<string> {
  try {
    const subtle = globalThis.crypto?.subtle
    if (!subtle || file.size <= 0 || file.size > limit) return ''
    const buf = await file.arrayBuffer()
    const digest = await subtle.digest('SHA-256', buf)
    return Array.from(new Uint8Array(digest))
      .map((b) => b.toString(16).padStart(2, '0'))
      .join('')
  } catch {
    return ''
  }
}

/** 从 `webkitRelativePath` 取相对文件夹路径（去掉文件名本身） */
export function relFolderOf(file: File): string {
  const rel = (file as any).webkitRelativePath as string | undefined
  if (!rel) return ''
  const idx = rel.lastIndexOf('/')
  return idx <= 0 ? '' : rel.slice(0, idx)
}

/** 关联键：`webkitRelativePath || name`（硬约束 ①） */
export function keyOf(file: File): string {
  return ((file as any).webkitRelativePath as string | undefined) || file.name
}

/** 创建上传状态（每次调用新建 ⇒ 随页面实例生灭） */
export function createUploadState(host: UploadHost) {
  /** 弹窗可见 */
  const visible = ref(false)
  /** 阶段：`pick` 选文件 / `plan` 预检确认 / `uploading` 传输中 / `done` 完成 */
  const stage = ref<'pick' | 'plan' | 'uploading' | 'done'>('pick')
  /** 已选文件（去重后） */
  const picked = ref<PickedFile[]>([])
  /** 预检行 */
  const planRows = ref<UploadPlanItem[]>([])
  const planSummary = ref<EntDocPlanSummary | null>(null)
  /** 进度 */
  const doneCount = ref(0)
  const totalCount = ref(0)
  const progressText = ref('')
  const running = ref(false)
  /** 批次号（cancel 用） */
  const taskId = ref('')
  /** 是否已确认（确认后 ⛔ 不能再 cancel） */
  const confirmed = ref(false)

  const canStart = computed(() => !running.value && planRows.value.some((r) => r.Checked))
  const checkedCount = computed(() => planRows.value.filter((r) => r.Checked).length)
  const progressPercent = computed(() =>
    totalCount.value > 0 ? Math.round((doneCount.value * 100) / totalCount.value) : 0,
  )

  function reset(): void {
    stage.value = 'pick'
    picked.value = []
    planRows.value = []
    planSummary.value = null
    doneCount.value = 0
    totalCount.value = 0
    progressText.value = ''
    taskId.value = ''
    confirmed.value = false
    running.value = false
  }

  function open(): void {
    reset()
    visible.value = true
  }

  /**
   * 关闭弹窗。
   *
   * ★ 未确认且已建批次 ⇒ **自动回滚**（撤行 + 删对象），⛔ 不留下半截脏数据。
   *   已确认的批次不能 cancel（后端也会拒），所以这里先判 `confirmed`。
   */
  async function close(): Promise<void> {
    if (running.value) {
      ElMessage.warning('正在上传，请等待完成')
      return
    }
    if (taskId.value && !confirmed.value) {
      const ent = host.enterpriseCode()
      if (ent) {
        try {
          await entDocUploadCancel(taskId.value, ent)
        } catch {
          // 回滚失败不阻断关闭：批次会在过期后由后端清理
        }
      }
    }
    visible.value = false
    reset()
  }

  /** 追加文件（按 `key` 去重；同名不同文件夹**不**去重） */
  function addFiles(files: File[]): void {
    const seen = new Set(picked.value.map((p) => p.key))
    let added = 0
    for (const f of files) {
      const key = keyOf(f)
      if (seen.has(key)) continue
      seen.add(key)
      picked.value = [...picked.value, { file: f, relFolderPath: relFolderOf(f), key }]
      added++
    }
    if (added > 0) ElMessage.success(`已选择 ${added} 个文件（共 ${picked.value.length} 个）`)
  }

  /** 移除一个待上传项 */
  function removeAt(index: number): void {
    const next = [...picked.value]
    next.splice(index, 1)
    picked.value = next
  }

  /** 清空 */
  function clearPicked(): void {
    picked.value = []
  }

  /** 组装请求项（含 sha256） */
  async function buildItems(): Promise<{ items: EntDocPlanItem[]; files: File[] }> {
    const items: EntDocPlanItem[] = []
    const files: File[] = []
    for (const p of picked.value) {
      const sha = await sha256OfFile(p.file)
      items.push({
        FileName: p.file.name,
        RelFolderPath: p.relFolderPath,
        FileSize: p.file.size,
        Sha256: sha,
      })
      files.push(p.file)
    }
    return { items, files }
  }

  /** Step0：预检（纯计算不落库） */
  async function runPlan(): Promise<void> {
    const ent = host.enterpriseCode()
    if (!ent) {
      ElMessage.warning('请先在左侧选择企业')
      return
    }
    if (picked.value.length === 0) {
      ElMessage.warning('请先选择要上传的文件')
      return
    }
    running.value = true
    try {
      const { items, files } = await buildItems()
      const { rows, summary } = await entDocPlanUpload(ent, items)

      // ★ 行 ↔ File 的关联按**同序下标**：后端 `PlanUploadAsync` 逐条产出（顺序不变），
      //   但会**静默剔除**系统噪声（`.DS_Store` 等）⇒ 下标会错位。
      //   ⇒ 按 `(RelFolderPath, FileName)` 双键回查，⛔ 不用下标。
      const byKey = new Map<string, File>()
      for (let i = 0; i < items.length; i++) {
        const it = items[i]
        const k = `${(it.RelFolderPath ?? '').trim()}|${it.FileName}`
        if (!byKey.has(k)) byKey.set(k, files[i])
      }

      planRows.value = rows.map((r) => {
        const k = `${(r.RelFolderPath ?? '').trim()}|${r.FileName}`
        return {
          ...r,
          Key: k,
          // ★ Blocked / skip 默认不勾选：后端已判过，前端不替用户决定
          Checked: !r.Blocked && r.Action !== 'skip',
          File: byKey.get(k) ?? null,
        }
      })
      planSummary.value = summary
      stage.value = 'plan'
    } catch (e: any) {
      ElMessage.error(e?.message || '预检失败')
    } finally {
      running.value = false
    }
  }

  /** 勾选切换 */
  function toggleChecked(row: UploadPlanItem, checked: boolean): void {
    row.Checked = checked
  }

  /** 全选 / 全不选（只作用于可传的行） */
  function toggleAll(checked: boolean): void {
    for (const r of planRows.value) {
      if (r.Blocked || !r.File) continue
      r.Checked = checked
    }
  }

  /** Step1~3：init → 逐文件传字节 → confirm */
  async function start(): Promise<void> {
    const ent = host.enterpriseCode()
    if (!ent) return

    const picked2 = planRows.value.filter((r) => r.Checked && r.File)
    if (picked2.length === 0) {
      ElMessage.warning('没有勾选任何文件')
      return
    }

    running.value = true
    stage.value = 'uploading'
    doneCount.value = 0
    totalCount.value = picked2.length

    try {
      const items: EntDocPlanItem[] = []
      for (const r of picked2) {
        items.push({
          FileName: r.FileName,
          RelFolderPath: r.RelFolderPath,
          FileSize: r.FileSize,
          Sha256: r.Sha256 || '',
        })
      }

      const init = await entDocUploadInit(ent, items)
      if (!init.TaskId) throw new Error('创建上传批次失败')
      taskId.value = init.TaskId

      /*
       * ★ 行 ↔ File 的配对：**双指针顺序走**，⛔ 不建 `FileName → File` 字典。
       *
       * 为什么不能按文件名建字典：一个文件夹里可以有同名文件
       * （`1质量手册/x.doc` 与 `4记录文件/x.doc`），字典会把两份塌缩成一份
       * ⇒ 同一份字节被写进两个位置（硬约束 ① 的同一个坑）。
       *
       * 为什么双指针成立：后端 `UploadInitAsync` 按入参 `items` 的**原顺序**逐条产出
       * （白名单外的静默跳过 ⇒ `init.Items` 是 `picked2` 的子序列），
       * 所以「在 picked2 里向前找到第一个同名项」就是它对应的那一行。
       */
      let cursor = 0
      let ok = 0
      for (const it of init.Items) {
        let matched: UploadPlanItem | null = null
        while (cursor < picked2.length) {
          const cand = picked2[cursor]
          cursor++
          if (cand.FileName === it.FileName) { matched = cand; break }
        }

        if (it.Action === 'skip' || !matched?.File) {
          doneCount.value++
          continue
        }

        progressText.value = `正在上传 ${it.FileName}`
        await entDocUploadFile(init.TaskId, it.FileCode, matched.File, matched.Sha256 || undefined)
        ok++
        doneCount.value++
      }

      progressText.value = '正在确认并排队转换…'
      const confirmedRes = await entDocUploadConfirm(init.TaskId, ent)
      confirmed.value = true

      stage.value = 'done'
      if (confirmedRes.QueueError) {
        ElMessage.warning(`已上传 ${ok} 个文件，但入队失败：${confirmedRes.QueueError}`)
      } else {
        ElMessage.success(
          `已上传 ${ok} 个文件（新增 ${confirmedRes.ConvertCount} 个转换任务，跳过 ${confirmedRes.SkipCount} 个重复文件）`,
        )
      }
      await host.afterUploaded()
    } catch (e: any) {
      ElMessage.error(e?.message || '上传失败')
      stage.value = 'plan'
    } finally {
      running.value = false
      progressText.value = ''
    }
  }

  return {
    // 状态
    visible, stage, picked, planRows, planSummary,
    doneCount, totalCount, progressText, running, confirmed,
    // 派生
    canStart, checkedCount, progressPercent,
    // 动作
    open, close, reset, addFiles, removeAt, clearPicked, runPlan, toggleChecked, toggleAll, start,
  }
}
