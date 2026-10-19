/**
 * `resources` 页面 · **上传状态机**（四段式：plan 预览 → init → file… → confirm）
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★ 为什么从 `logic.ts` 拆出来（2026-10-19 · 计划 §3.11 ③）
 *
 * `resources/logic.ts` 原来 1405 行，一个类里塞了**四套状态机**：上传、替换/删除、
 * 版本/历史、提取结果。R24 只要求「class 有 extends」，但把它整类塞进 `SingleTableCore`
 * 毫无收益（本页没有 `/filter`、没有 `add|update|delete`）。
 * ⇒ 按计划拆分：**`logic.ts` 只留「树 + 阶段汇总 + 轮询 + 列配置」**，上传独立成这个文件。
 *
 * ★ 本文件承载的三条上传入口（04 分册）
 *   · A 单标准上传（槽位/目录点「上传」，目标文件夹锁定）
 *   · B 单标准上传（顶部「上传文件」，目标文件夹可切换）
 *   · C 多标准批量上传（一个文件夹里可能有 3 个标准的文件 —— 需求 3 核心）
 *   外加 D4 的另一半：**未归属文件的人工指派**（只传 FolderCode，不传 SlotCode）。
 *
 * ★★ 两条不能动的硬约束
 *   ① **关联键 = `webkitRelativePath || name`**（不是 `File.name`）——
 *      一个文件夹里可以有同名文件（`1质量手册/x.doc` 与 `4记录文件/x.doc`），
 *      用文件名做索引会把多行塌缩到同一个 File ⇒ **同一份字节被写进多个槽位**。
 *   ② **M3（NeedsConfirm）/ 被拦（BlockReason）默认不勾选** —— 后端已不再瞎猜，
 *      前端也不该替用户确认。
 *
 * ⚠️ 状态**不放在模块作用域**：由 `createUploadState()` 每次调用新建，
 *    挂在 `ResourcesLogic` 实例上 ⇒ 离开页面再进来一定重置、多标签页互不干扰。
 * ════════════════════════════════════════════════════════════════════════
 */
import { ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { planDispatch } from '@share/api'
import { slotModifyTime } from '@share/api'
import type { DispatchRow, FileSlot, UploadConfirmResult, ManualValuesArchivedNotice } from '@share/api'
import type { UploadRunResult } from '@share/composables/useFileOps'
import { partitionUploadable, checkUploadable } from '@share/constants/upload-file-policy'
import type { FileOpEntry } from '@share/composables/useFileOps'
import type { UploadCtx, UploadPayload, UploadPlanRow, UnmatchedAssign } from './logic'

/**
 * 宿主能力（由 `ResourcesLogic` 提供）。
 *
 * ⛔ 不让本文件 import `ResourcesLogic` 再 `new` —— 会成环，而且状态就不再随页面实例生灭。
 * 用「能力接口」把依赖收窄到 10 个成员，也顺便说明了上传**到底需要页面提供什么**。
 */
export interface UploadHost {
  selectedEnterprise: { value: string }
  selectedStage: { value: string }
  activeTab: { value: string }
  /** 文件操作机制族（`@share/composables/useFileOps`）—— 上传/替换/下载的唯一入口 */
  fileOps: {
    /** 四段式执行（`@share/composables/useFileOps` 的 `start`，不是 `upload`） */
    start(
      entries: FileOpEntry<UploadPayload>[],
      ctx: UploadCtx,
      onProgress?: (done: number, total: number, fileName: string) => void,
    ): Promise<UploadRunResult<UploadConfirmResult>>
    replace(file: File, ctx: { fileCode: string; enterpriseCode: string; reason: string; expectedModifyTime?: string }): Promise<ManualValuesArchivedNotice | null>
  }
  /** 计划行/裸文件 → 机制入口项 */
  toEntry(file: File, payload: UploadPayload, key?: string): FileOpEntry<UploadPayload>
  folderOptionsOf(standardCode: string): Array<{ FolderCode: string; Label: string; FullPath: string }>
  folderFullPathOf(standardCode: string, folderCode: string): string
  standardOf(standardCode: string): { StandardCode: string; StandardName: string } | null
  reloadStandard(standardCode: string): Promise<void>
  loadOverview(): Promise<void>
}

/** 未归属原因 → 中文（`ambiguous` = 有候选但分不清，提示人工指派而不是硬匹配；
 *  `ext_mismatch` = 文件名命中了槽位但扩展名与模板不同族，格式不符按未归属处理，槽位保持「缺失」） */
const UNMATCHED_REASON_TEXT: Record<string, string> = {
  no_standard: '该阶段无可用标准',
  no_match: '未命中任何标准槽位',
  ambiguous: '有相近槽位但分不清',
  ext_mismatch: '扩展名与匹配槽位不兼容（格式不符）'
}

/** 上传状态（每次调用新建 ⇒ 随页面实例生灭，⛔ 不放模块作用域） */
export function createUploadState() {
  const uploading = ref(false)
  const uploadProgress = ref('')

  // ─── 单标准上传（plan 预览 → 确认） ───
  const singleUploadVisible = ref(false)
  const currentUploadStandardCode = ref<string>('')
  const singleUploadFolderCode = ref<string>('')
  /** ★ 目标文件夹是否锁定：从「缺失槽位 / 目录」的「上传」进入时锁定 = 只读，不要求手选 */
  const singleFolderLocked = ref(false)
  const singleUploadFiles = ref<File[]>([])
  const singlePlanLoading = ref(false)
  const singlePlanRows = ref<UploadPlanRow[]>([])
  const singlePlanUnmatched = ref<string[]>([])
  const singlePlanBlocked = ref<UploadPlanRow[]>([])
  /** 「未命中槽位的文件直接放入所选文件夹」（D8 模板外文件的人工落地出口，默认关） */
  const singleAssignUnmatched = ref(false)

  // ─── 多标准上传（顶部入口，需求 3 核心） ───
  const batchUploadVisible = ref(false)
  const batchFiles = ref<File[]>([])
  const batchPlanLoading = ref(false)
  const batchPlanRows = ref<UploadPlanRow[]>([])
  const batchPlanUnmatched = ref<Array<{ Key: string; FileName: string; RelativePath: string; Reason: string }>>([])
  const batchPlanConflicts = ref<Array<{ StandardName: string; SlotFileName: string; FileNames: string[] }>>([])
  const batchPlanCross = ref<Array<{ FileName: string; Standards: string[] }>>([])
  const batchPlanBlocked = ref<UploadPlanRow[]>([])
  const batchPlanMessage = ref('')
  /** 未归属 → 人工指派：key = fileKey，值 = 目标标准 + 目标文件夹（D4 的另一半） */
  const unmatchedAssign = ref<Record<string, { StandardCode: string; FolderCode: string }>>({})

  // ─── 替换（走 fileOps.replace，与上传同一机制族） ───
  const replaceDialogVisible = ref(false)
  const replacingFile = ref<FileSlot | null>(null)
  const replaceNewFile = ref<File | null>(null)
  const replaceReason = ref('')

  return {
    uploading,
    uploadProgress,
    singleUploadVisible,
    currentUploadStandardCode,
    singleUploadFolderCode,
    singleFolderLocked,
    singleUploadFiles,
    singlePlanLoading,
    singlePlanRows,
    singlePlanUnmatched,
    singlePlanBlocked,
    singleAssignUnmatched,
    batchUploadVisible,
    batchFiles,
    batchPlanLoading,
    batchPlanRows,
    batchPlanUnmatched,
    batchPlanConflicts,
    batchPlanCross,
    batchPlanBlocked,
    batchPlanMessage,
    unmatchedAssign,
    replaceDialogVisible,
    replacingFile,
    replaceNewFile,
    replaceReason
  }
}

/**
 * 上传动作。
 *
 * ⚠️ 返回值里的方法**不是** class 方法：`ResourcesLogic` 会把它们逐个转发，
 *    所以外部调用面仍是 `logic.openSingleUpload(...)`，`index.vue` 一个字都不用改。
 */
export function createUploadActions(host: UploadHost, s: ReturnType<typeof createUploadState>) {
  // ═══════════════════ 单标准上传（入口 A/B） ═══════════════════

  /**
   * 打开单标准上传弹窗。
   *
   * <para>★ `folderCode !== undefined`（从「缺失槽位 / 目录」的「上传」进入）⇒ **锁定**到该目录
   * （含根目录 `''`）并置只读 —— 槽位上传按文件名匹配，无需用户手选目录；⛔ 不能用 `??`
   * 判空（根目录的 `FolderCode` 是空串，会被 `??` 当成"有值"而卡住回退）。</para>
   */
  function openSingleUpload(standardCode: string, folderCode?: string) {
    s.currentUploadStandardCode.value = standardCode
    if (folderCode !== undefined) {
      s.singleUploadFolderCode.value = folderCode
      s.singleFolderLocked.value = true
    } else {
      // 顶部「上传文件」入口：不锁定，默认第一个目录，可自由切换
      s.singleUploadFolderCode.value = host.folderOptionsOf(standardCode)[0]?.FolderCode ?? ''
      s.singleFolderLocked.value = false
    }
    s.singleUploadFiles.value = []
    resetSinglePlan()
    s.singleUploadVisible.value = true
  }

  function closeSingleUploadDialog() {
    s.singleUploadVisible.value = false
    s.singleUploadFiles.value = []
    s.singleFolderLocked.value = false
    resetSinglePlan()
  }

  /** 锁定态下目标文件夹的可读名称（根目录 `''` → 「根目录」） */

  function resetSinglePlan() {
    s.singlePlanRows.value = []
    s.singlePlanUnmatched.value = []
    s.singlePlanBlocked.value = []
  }

  /**
   * ★ 上传契约闸门（唯一事实源 = `@share/constants/upload-file-policy`，与后端 `UploadFilePolicy.Check` 同口径）：
   * 系统噪声静默剔除（计数提示）；白名单外剔除并列出 —— ⛔ 页面不许自建后缀 if/else（守卫 R21）。
   * 默认类别 document/image/pdf（⛔ 不含压缩包，与后端 AllowedExtensions 一致）。
   */
  function partitionByPolicy(files: File[]): File[] {
    if (!files.length) return files
    const part = partitionUploadable(files)
    if (part.noise.length > 0) {
      ElMessage.info(
        `已自动忽略 ${part.noise.length} 个系统文件（${part.noise.slice(0, 3).map((nf) => nf.name).join('、')}${part.noise.length > 3 ? ' 等' : ''}）`)
    }
    if (part.rejected.length > 0) {
      ElMessage.warning(
        `已跳过 ${part.rejected.length} 个不支持的文件：${part.rejected.slice(0, 3).map((rf) => rf.name).join('、')}${part.rejected.length > 3 ? ' 等' : ''}`)
    }
    if (!part.accepted.length) return []
    const ok = new Set(part.accepted.map((a) => a.name))
    return files.filter((f) => ok.has(f.name))
  }

  function setSingleUploadFiles(files: File[]) {
    const clean = partitionByPolicy(files)
    if (!clean.length) return
    const added = appendFiles(s.singleUploadFiles.value, clean)
    if (added < clean.length) ElMessage.info(`已忽略 ${clean.length - added} 个重复文件`)
    resetSinglePlan()
    autoPreviewSingle()
  }

  /**
   * 选完文件**自动**干跑预览（04 §4.4 原文「直接展示该标准的匹配预览」）。
   *
   * <para>★ 为什么要自动：目标标准与目标文件夹在打开弹窗时就已给定，剩下的「文件 → 槽位」
   * 是纯计算，后端 upload/plan 一次调用就能出结果 —— 让用户多点一次「匹配预览」只是白费一次点击。
   * 真正需要人看的只有 M3 标黄、冲突、未归属这三类，它们在预览结果里逐行可见，勾选态即确认。</para>
   */
  function autoPreviewSingle() {
    if (s.singleUploadFiles.value.length === 0) return
    void previewSingleUpload()
  }

  function clearSingleUploadFiles() {
    s.singleUploadFiles.value = []
    resetSinglePlan()
  }

  function clearBatchFiles() {
    s.batchFiles.value = []
    resetBatchPlan()
  }

  /** 上传文件夹（webkitdirectory）：保留相对路径，才能让 M0 文件夹路径强匹配生效 */
  function setSingleUploadFolderFiles(files: File[]) {
    // ★ 文件夹模式也过契约闸门（文件夹里什么都可能有：.DS_Store / ~$ 锁文件 / 可执行文件）
    const clean = partitionByPolicy(files)
    if (!clean.length) return
    s.singleUploadFiles.value = clean
    resetSinglePlan()
    autoPreviewSingle()
  }

  function removeSingleUploadFile(index: number) {
    s.singleUploadFiles.value.splice(index, 1)
    resetSinglePlan()
    autoPreviewSingle()
  }

  /** `YzhFolderUpload` 回传完整列表 ⇒ 覆盖式写入（组件自持列表，⛔ 不再 append） */
  function setSingleUploadFilesFromPicker(files: File[]) {
    s.singleUploadFiles.value = dedupeByKey(partitionByPolicy(files))
    resetSinglePlan()
    autoPreviewSingle()
  }

  /** `YzhFolderUpload`（批量）回传完整列表 ⇒ 覆盖式写入 + 去重提示 */
  function setBatchFilesFromPicker(files: File[]) {
    const clean = partitionByPolicy(files)
    const deduped = dedupeByKey(clean)
    if (deduped.length < clean.length) ElMessage.info(`已忽略 ${clean.length - deduped.length} 个重复文件`)
    s.batchFiles.value = deduped
    resetBatchPlan()
  }

  /** 按关联键去重（同名不同目录必须保留两份，见 {@link fileKey}） */
  function dedupeByKey(files: File[]): File[] {
    const seen = new Set<string>()
    const out: File[] = []
    for (const f of files) {
      const k = fileKey(f)
      if (seen.has(k)) continue
      seen.add(k)
      out.push(f)
    }
    return out
  }

  // ═══════════════════ 分发预览：本地文件 ↔ 计划行的关联键 ═══════════════════

  /**
   * 文件的关联键 = `webkitRelativePath || name`（单文件上传时补目标文件夹前缀，与请求体同口径）。
   *
   * <para>★ 为什么不能用 `File.name`：一个文件夹里可以有多份同名文件
   * （`1质量手册/x.doc` 与 `4记录文件/x.doc`），而分发计划对它们各产出一行。
   * 用文件名做索引会把多行塌缩到<b>同一个 File 对象</b> ⇒ 同一份字节被写进多个槽位
   * （这正是需求 3「一个文件夹里可能有 3 个标准的文件」的主场景）。</para>
   */
  function fileKey(file: File, folderPath = ''): string {
    const rel = (file as any).webkitRelativePath as string | undefined
    if (rel) return rel
    return folderPath ? `${folderPath}/${file.name}` : file.name
  }

  /** 未归属原因 → 中文 */
  function unmatchedReasonText(reason: string): string {
    return UNMATCHED_REASON_TEXT[reason] ?? reason
  }

  /** 干跑预览：走后端 DispatchMatcher（M0–M3，与批量入口同一套匹配） */
  async function previewSingleUpload() {
    if (s.singleUploadFiles.value.length === 0) { ElMessage.warning('请先选择文件'); return }
    s.singlePlanLoading.value = true
    resetSinglePlan()
    try {
      const folderPath = host.folderFullPathOf(s.currentUploadStandardCode.value, s.singleUploadFolderCode.value)
      const plan = await planDispatch(
        host.selectedEnterprise.value, host.selectedStage.value,
        s.singleUploadFiles.value.map(f => ({
          FileName: f.name,
          RelativePath: fileKey(f, folderPath),
          FileSize: f.size
        }))
      )
      const mine = plan.Standards.find(std => std.StandardCode === s.currentUploadStandardCode.value)
      const byKey = new Map(s.singleUploadFiles.value.map(f => [fileKey(f, folderPath), f]))
      const rows = (mine?.Rows ?? [])
      s.singlePlanRows.value = rows
        .filter(r => byKey.has(fileKeyOf(r)))
        .map(r => toPlanRow(r, byKey.get(fileKeyOf(r))!, r.StandardCode, r.StandardName))
      s.singlePlanBlocked.value = s.singlePlanRows.value.filter(r => r.BlockReason).map(r => ({ ...r, Selected: false }))
      // 未命中名单 = 后端报的 Unmatched + 本地未被任何计划行认领的文件（去重）
      const hitKeys = new Set(rows.map(r => fileKeyOf(r)))
      const backendUnmatched = new Set(plan.Unmatched.map(u => fileKeyOf({ RelativePath: u.RelativePath, FileName: u.FileName } as DispatchRow)))
      const names: string[] = []
      for (const u of plan.Unmatched) if (!names.includes(u.FileName)) names.push(u.FileName)
      for (const f of s.singleUploadFiles.value) {
        if (hitKeys.has(fileKey(f, folderPath)) || backendUnmatched.has(fileKey(f, folderPath))) continue
        if (!names.includes(f.name)) names.push(f.name)
      }
      s.singlePlanUnmatched.value = names
      if (s.singlePlanRows.value.length === 0 && !s.singleAssignUnmatched.value) {
        ElMessage.warning('所选文件未命中该标准的任何槽位（不会上传）；可勾选「放入所选文件夹」直接落地')
      }
    } catch (e: any) {
      ElMessage.error(e?.message ?? '匹配预览失败')
    } finally {
      s.singlePlanLoading.value = false
    }
  }

  /** 计划行的关联键（后端回显的 RelativePath，单文件上传时等于 FileName） */
  function fileKeyOf(r: DispatchRow): string {
    return r.RelativePath || r.FileName
  }

  function toPlanRow(
    r: DispatchRow, file: File, standardCode: string, standardName: string
  ): UploadPlanRow {
    return {
      File: file,
      Key: fileKeyOf(r),
      FileName: r.FileName,
      StandardCode: standardCode,
      StandardName: standardName,
      SlotCode: r.SlotCode,
      SlotFileName: r.SlotFileName,
      FolderCode: r.FolderCode,
      FolderPath: r.FolderPath,
      Level: r.Level,
      NeedsConfirm: r.NeedsConfirm,
      BlockReason: r.BlockReason,
      // ★ M3（NeedsConfirm）默认**不勾选**：后端已不再瞎猜，前端也不该替用户确认
      Selected: !r.BlockReason && !r.NeedsConfirm
    }
  }

  /** 确认上传：四段式（init → file... → confirm），未选/被拦的行不执行 */
  async function confirmSingleUpload() {
    const rows = s.singlePlanRows.value.filter(r => r.Selected && !r.BlockReason)
    // 勾了「放入所选文件夹」时，未命中槽位的文件走指派模式落进所选文件夹（D8）
    const loose = s.singleAssignUnmatched.value ? looseSingleFiles(rows) : []
    if (rows.length === 0 && loose.length === 0) { ElMessage.warning('没有可执行的上传行'); return }
    if (rows.length > 0) await runUpload(rows, s.currentUploadStandardCode.value)
    if (loose.length > 0) {
      ElMessage.success(`另有 ${loose.length} 个文件将直接放入「${
        host.folderFullPathOf(s.currentUploadStandardCode.value, s.singleUploadFolderCode.value) || '根目录'
      }」`)
      await runUpload(
        loose.map(f => ({ ...({} as UploadPlanRow), File: f, Selected: true })),
        s.currentUploadStandardCode.value,
        s.singleUploadFolderCode.value
      )
    }
    closeSingleUploadDialog()
  }

  /** 单标准入口里「未命中槽位、且未被计划行认领」的文件（按关联键去重） */
  function looseSingleFiles(matched: UploadPlanRow[]): File[] {
    const folderPath = host.folderFullPathOf(s.currentUploadStandardCode.value, s.singleUploadFolderCode.value)
    const taken = new Set(matched.map(r => r.Key))
    const out: File[] = []
    const seen = new Set<string>()
    for (const f of s.singleUploadFiles.value) {
      const k = fileKey(f, folderPath)
      if (taken.has(k) || seen.has(k)) continue
      seen.add(k)
      out.push(f)
    }
    return out
  }
  // ═══════════════════ 多标准上传（入口 C，需求 3 核心） ═══════════════════

  function openBatchUpload() {
    s.batchFiles.value = []
    resetBatchPlan()
    s.batchUploadVisible.value = true
  }

  function closeBatchUpload() {
    s.batchUploadVisible.value = false
    s.batchFiles.value = []
    resetBatchPlan()
  }

  function resetBatchPlan() {
    s.batchPlanRows.value = []
    s.batchPlanUnmatched.value = []
    s.batchPlanConflicts.value = []
    s.batchPlanCross.value = []
    s.batchPlanBlocked.value = []
    s.batchPlanMessage.value = ''
    s.unmatchedAssign.value = {}
  }

  /**
   * 追加文件（按关联键去重）。
   *
   * <para>★ 为什么必须去重：同一批里出现两份同一文件时，后端会为它产出<b>两行同槽位</b>的计划，
   * 于是「冲突」与「跨标准」告警全是假的（实测出现过 <c>9001标准 + 9001标准</c> 自我跨标准），
   * 用户反而看不懂真正的问题。</para>
   */
  function appendFiles(target: File[], files: File[], folderPath = ''): number {
    const seen = new Set(target.map(f => fileKey(f, folderPath)))
    let added = 0
    for (const f of files) {
      const k = fileKey(f, folderPath)
      if (seen.has(k)) continue
      seen.add(k)
      target.push(f)
      added++
    }
    return added
  }

  function addBatchFiles(files: File[]) {
    const clean = partitionByPolicy(files)
    if (!clean.length) return
    const added = appendFiles(s.batchFiles.value, clean)
    if (added < clean.length) ElMessage.info(`已忽略 ${clean.length - added} 个重复文件`)
    resetBatchPlan()
  }

  function removeBatchFile(index: number) {
    s.batchFiles.value.splice(index, 1)
    resetBatchPlan()
  }

  /** 选中即干跑：逐标准独立匹配（未命中不进入该标准 —— 需求 3 硬约束） */
  async function previewBatchUpload() {
    if (s.batchFiles.value.length === 0) { ElMessage.warning('请先选择文件'); return }
    s.batchPlanLoading.value = true
    resetBatchPlan()
    try {
      const plan = await planDispatch(
        host.selectedEnterprise.value, host.selectedStage.value,
        s.batchFiles.value.map(f => ({
          FileName: f.name,
          RelativePath: fileKey(f),
          FileSize: f.size
        }))
      )
      s.batchPlanMessage.value = plan.Message ?? ''
      // ★ 按关联键（RelativePath || FileName）索引，不按文件名 —— 同名不同目录必须各拿各的字节
      const byKey = new Map(s.batchFiles.value.map(f => [fileKey(f), f]))
      const rows: UploadPlanRow[] = []
      for (const s of plan.Standards) {
        for (const r of s.Rows) {
          const file = byKey.get(fileKeyOf(r))
          if (!file) continue
          rows.push(toPlanRow(r, file, s.StandardCode, s.StandardName))
        }
      }
      s.batchPlanRows.value = rows
      s.batchPlanBlocked.value = rows.filter(r => r.BlockReason)
      s.batchPlanUnmatched.value = plan.Unmatched.map(u => ({
        Key: u.RelativePath || u.FileName,
        FileName: u.FileName,
        RelativePath: u.RelativePath,
        Reason: u.Reason
      }))
      s.batchPlanConflicts.value = plan.Conflicts.map(c => ({
        StandardName: c.StandardName, SlotFileName: c.SlotFileName, FileNames: c.FileNames
      }))
      s.batchPlanCross.value = plan.CrossStandard.map(c => ({ FileName: c.FileName, Standards: c.Standards }))
    } catch (e: any) {
      ElMessage.error(e?.message ?? '分发预览失败')
    } finally {
      s.batchPlanLoading.value = false
    }
  }

  // ═══════════════════ 未归属 → 人工指派（D4 的另一半 / H6-N2） ═══════════════════

  /**
   * 设置某个未归属文件的目标标准 / 目标文件夹（可分两步选）。
   *
   * <para>★ 半选状态必须保留：只选标准、还没选文件夹时，若把这条记录丢掉，
   * 绑定它的标准下拉会立刻回空，用户永远走不到第二步（文件夹下拉也因此没有数据源）。</para>
   */
  function setUnmatchedAssign(key: string, patch: { StandardCode?: string; FolderCode?: string }) {
    const cur = s.unmatchedAssign.value[key] ?? { StandardCode: '', FolderCode: '' }
    const next = { ...cur, ...patch }
    // 换标准 ⇒ 文件夹选择失效（文件夹 Code 跨标准不通用）
    if (patch.StandardCode !== undefined && patch.StandardCode !== cur.StandardCode) next.FolderCode = ''
    s.unmatchedAssign.value = { ...s.unmatchedAssign.value, [key]: next }
  }

  function unmatchedAssignOf(key: string): { StandardCode: string; FolderCode: string } {
    return s.unmatchedAssign.value[key] ?? { StandardCode: '', FolderCode: '' }
  }

  /** 执行人工指派：按标准分组走四段式指派模式（不传 SlotCode） */
  async function confirmUnmatchedAssign(): Promise<boolean> {
    const items = assignedUnmatched()
    if (items.length === 0) { ElMessage.warning('请先为未归属文件选择目标标准与文件夹'); return false }

    const groups = new Map<string, UnmatchedAssign[]>()
    for (const it of items) {
      if (!groups.has(it.StandardCode)) groups.set(it.StandardCode, [])
      groups.get(it.StandardCode)!.push(it)
    }

    s.uploading.value = true
    let okFiles = 0
    let failed = 0
    try {
      for (const [standardCode, list] of groups) {
        const name = host.standardOf(standardCode)?.StandardName ?? standardCode
        try {
          const res = await host.fileOps.start(
            list.map(i => host.toEntry(i.File, { FolderCode: i.FolderCode })),
            { enterpriseCode: host.selectedEnterprise.value, stageCode: host.selectedStage.value, standardCode },
            (done, total, n) => { s.uploadProgress.value = `${name} 指派 ${done}/${total} ${n}` }
          )
          okFiles += res.result?.ActivatedCount ?? list.length
          if (res.result?.QueueError) ElMessage.warning(`${name}：${res.result.QueueError}`)
        } catch (e: any) {
          failed++
          ElMessage.error(`${name} 指派失败：${e?.message ?? '未知错误'}`)
        }
      }
    } finally {
      s.uploading.value = false
      s.uploadProgress.value = ''
    }

    if (okFiles > 0) ElMessage.success(`已指派上传 ${okFiles} 个文件到 ${groups.size} 个标准`)
    if (failed > 0) return false
    // 指派成功的行从未归属清单移除，只留失败项
    const assignedKeys = new Set(s.batchPlanUnmatched.value
      .filter(u => assignedUnmatched().some(i => fileKey(i.File) === u.Key))
      .map(u => u.Key))
    s.batchPlanUnmatched.value = s.batchPlanUnmatched.value.filter(u => !assignedKeys.has(u.Key))
    s.unmatchedAssign.value = {}
    await host.loadOverview()
    return true
  }

  /** 勾选/取消一行（冲突区人工取舍的落点） */
  function toggleBatchRow(row: UploadPlanRow, selected: boolean) {
    row.Selected = selected
  }

  /** 确认：按标准分组逐组走四段式（4.4），未归属/被拦行不产生任何调用 */
  async function confirmBatchUpload() {
    const groups = batchPlanRowGroups()
      .map(g => ({ ...g, Rows: g.Rows.filter(r => r.Selected && !r.BlockReason) }))
      .filter(g => g.Rows.length > 0)
    if (groups.length === 0) { ElMessage.warning('没有可执行的上传行'); return }

    let okFiles = 0
    let failed = 0
    s.uploading.value = true
    try {
      for (const g of groups) {
        try {
          const res = await host.fileOps.start(
            g.Rows.map(r => host.toEntry(r.File, { SlotCode: r.SlotCode }, r.Key)),
            { enterpriseCode: host.selectedEnterprise.value, stageCode: host.selectedStage.value, standardCode: g.StandardCode },
            (done, total, name) => { s.uploadProgress.value = `${g.StandardName} ${done}/${total} ${name}` }
          )
          okFiles += res.result?.ActivatedCount ?? g.Rows.length
          if (res.result?.QueueError) ElMessage.warning(`${g.StandardName}：${res.result.QueueError}`)
        } catch (e: any) {
          failed++
          ElMessage.error(`${g.StandardName} 上传失败：${e?.message ?? '未知错误'}`)
        }
      }
    } finally {
      s.uploading.value = false
      s.uploadProgress.value = ''
    }

    if (okFiles > 0) ElMessage.success(`已上传 ${okFiles} 个文件到 ${groups.length} 个标准`)
    // 还有待指派的未归属文件时不关弹窗，否则用户的指派选择会被静默丢弃
    if (failed === 0 && assignedUnmatched.length === 0) closeBatchUpload()
    await host.loadOverview()
  }

  /** 单标准执行体：四段式 + 局部刷新。给了 `assignFolderCode` 则走指派模式（模板外文件） */
  async function runUpload(rows: UploadPlanRow[], standardCode: string, assignFolderCode?: string) {
    s.uploading.value = true
    try {
      const entries = rows.map(r => host.toEntry(
        r.File,
        assignFolderCode ? { FolderCode: assignFolderCode } : { SlotCode: r.SlotCode },
        r.Key
      ))
      const res = await host.fileOps.start(
        entries,
        { enterpriseCode: host.selectedEnterprise.value, stageCode: host.selectedStage.value, standardCode },
        (done, total, name) => { s.uploadProgress.value = `${done}/${total} ${name}` }
      )
      const confirmed = res.result
      ElMessage.success(`已上传 ${confirmed?.ActivatedCount ?? rows.length} 个文件，转换队列已受理`)
      if (confirmed?.QueueError) ElMessage.warning(confirmed.QueueError)
    } catch (e: any) {
      ElMessage.error(e?.message ?? '上传失败')
    } finally {
      s.uploading.value = false
      s.uploadProgress.value = ''
    }
    await host.reloadStandard(standardCode)
  }

  // ═══════════════════ 替换 / 移除 ═══════════════════

  function openReplaceDialog(file: FileSlot) {
    s.replacingFile.value = file
    s.replaceNewFile.value = null
    s.replaceReason.value = ''
    s.replaceDialogVisible.value = true
  }

  function closeReplaceDialog() {
    s.replaceDialogVisible.value = false
    s.replacingFile.value = null
    s.replaceNewFile.value = null
    s.replaceReason.value = ''
  }

  function setReplaceNewFile(file: File | null) {
    // ★ 替换也过契约闸门（后端 ReplaceFileAsync 同样 Check —— 前端先拦，用户看到人话而不是后端错误码）
    if (file) {
      const v = checkUploadable(file.name)
      if (!v.ok) { ElMessage.warning(v.message ?? '不支持的文件格式'); return }
    }
    s.replaceNewFile.value = file
  }

  async function confirmReplace() {
    const target = s.replacingFile.value
    if (!target) return
    if (!s.replaceNewFile.value) { ElMessage.warning('请选择新文件'); return }
    if (!s.replaceReason.value.trim()) { ElMessage.warning('请填写替换原因'); return }
    try {
      const notice = await host.fileOps.replace(s.replaceNewFile.value, {
        fileCode: target.Code,
        enterpriseCode: host.selectedEnterprise.value,
        reason: s.replaceReason.value.trim(),
        expectedModifyTime: slotModifyTime(target)
      })
      ElMessage.success('文件已替换，旧版本已归档')
      closeReplaceDialog()
      await host.reloadStandard(target.StandardCode ?? host.activeTab.value)

      // ★ 2026-09-30 裁决 J4 的连带后果：按 RuleCode 归档会把人工补录值一起清掉。
      //   后端已写 change_log 留痕，这里弹窗问专家是否要重新补录（05 号 §5.3）。
      if (notice?.manualValuesArchived) {
        ElMessageBox.alert(notice.notice, '人工补录值已被覆盖', {
          type: 'warning',
          confirmButtonText: '知道了',
          dangerouslyUseHTMLString: false,
        }).catch(() => undefined)
      }
    } catch (e: any) {
      ElMessage.error(e?.message ?? '替换失败')
    }
  }

  /** 批量计划行按标准分组（冲突区人工取舍的落点） */
  function batchPlanRowGroups(): Array<{ StandardCode: string; StandardName: string; Rows: UploadPlanRow[] }> {
    const map = new Map<string, { StandardCode: string; StandardName: string; Rows: UploadPlanRow[] }>()
    for (const r of s.batchPlanRows.value) {
      if (!map.has(r.StandardCode)) map.set(r.StandardCode, { StandardCode: r.StandardCode, StandardName: r.StandardName, Rows: [] })
      map.get(r.StandardCode)!.Rows.push(r)
    }
    return Array.from(map.values())
  }

  /** 已指派齐（标准 + 文件夹都选好）的未归属文件 */
  function assignedUnmatched(): UnmatchedAssign[] {
    const byKey = new Map(s.batchFiles.value.map(f => [fileKey(f), f]))
    const out: UnmatchedAssign[] = []
    for (const u of s.batchPlanUnmatched.value) {
      const a = s.unmatchedAssign.value[u.Key]
      const file = byKey.get(u.Key)
      if (!a?.StandardCode || !a.FolderCode || !file) continue
      out.push({ File: file, FileName: u.FileName, StandardCode: a.StandardCode, FolderCode: a.FolderCode })
    }
    return out
  }

  return {
    get singleFolderLabel(): string {
      const code = s.singleUploadFolderCode.value
      if (!code) return '根目录'
      return host.folderOptionsOf(s.currentUploadStandardCode.value)
        .find(o => o.FolderCode === code)?.Label.trim() || code
    },
    get batchPlanRowGroups(): Array<{ StandardCode: string; StandardName: string; Rows: UploadPlanRow[] }> {
      return batchPlanRowGroups()
    },
    get selectedBatchCount(): number {
      return s.batchPlanRows.value.filter(r => r.Selected && !r.BlockReason).length
    },
    assignedUnmatchedList: assignedUnmatched,
    openSingleUpload, closeSingleUploadDialog, resetSinglePlan,
    setSingleUploadFiles, clearSingleUploadFiles, setSingleUploadFolderFiles,
    removeSingleUploadFile, setSingleUploadFilesFromPicker, setBatchFilesFromPicker,
    clearBatchFiles, addBatchFiles, removeBatchFile, setUnmatchedAssign,
    unmatchedAssignOf, confirmUnmatchedAssign, previewSingleUpload, confirmSingleUpload,
    previewBatchUpload, confirmBatchUpload, openBatchUpload, closeBatchUpload,
    openReplaceDialog, closeReplaceDialog, setReplaceNewFile, confirmReplace,
    unmatchedReasonText, toggleBatchRow,
    // ⚠️ 原本是 private 的内部件，但壳里要能调到（`openSingleUpload` 等会间接用到）⇒ 一并导出
    partitionByPolicy, autoPreviewSingle, fileKeyOf, looseSingleFiles,
    resetBatchPlan, runUpload,
    // ⚠️ 下面几个原本是 private，但 39 例安全网直接断言它们（`fileKey` / `dedupeByKey` /
    //    `appendFiles` / `toPlanRow`），且拆分后它们就是「上传契约」本身 ⇒ 转为公开
    fileKey, dedupeByKey, appendFiles, toPlanRow
  }
}

export type UploadActions = ReturnType<typeof createUploadActions>
