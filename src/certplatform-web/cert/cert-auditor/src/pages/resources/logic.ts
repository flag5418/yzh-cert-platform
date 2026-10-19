/**
 * 企业资料管理 Logic（审核员端 /resources）
 *
 * 布局契约（01 分册）：
 * - 左侧：**一棵树**（企业为父节点、企业已关联的「阶段」为子节点）——
 *   专家会管多个企业，左侧只保留树本身（旧版「企业树 + 阶段单选组」两套控件互相打架）；
 *   选阶段才能上传（需求 1 硬约束：未选阶段所有上传入口不可用）
 * - 右侧顶部：阶段头（企业/阶段 + 上传入口）+ 全局汇总条（标准数 / 应上传 / 已就位 / 转换中 / 缺失）
 * - 右侧主体：按标准分 Tab，Tab 内 = 模板**文件夹树**（按 ParentCode 嵌套）+ 槽位行 + 操作列
 *
 * ★ 数据源（05 §二 目标端点）：
 *   `stage-tree`（左树）→ `stage-overview`（汇总）→ `standard-directory`（每标准主数据）
 * ★ 上传（04 §一 四段式）：`upload/plan` 预览（DispatchMatcher M0–M3）→ `upload/init` → `upload/file` → `upload/confirm`
 * ★ 就位状态以后端 `Status` 字段为唯一权威（01 §5.1），前端不自行推断组合条件
 */
import { confirmOrFalse, SingleTableCore } from '@yzh-core'
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { YzhTableColumn, YzhTableDataLoader } from '@yzh-core'
import {
  stageTree, stageOverview, standardDirectory, activeQueue, cancelQueue,
  uploadInit, uploadFileStep, uploadConfirm, uploadCancel,
  enterpriseDownloadBlob,
  replaceEnterpriseFile,
  isSlotLive
} from '@share/api'
import type {
  EnterpriseNode, StageItem, StageOverviewResult, StageStandardSummary,
  StandardDirectoryResult, FileSlot, FileVersionRow, HistoryItem, DispatchRow,
  UploadConfirmResult, ManualValuesArchivedNotice
} from '@share/api'
import { useFileOps, type FileOpEntry } from '@share/composables/useFileOps'
import { createUploadState, createUploadActions, type UploadActions } from './useUpload'
import { createFileActionsState, createFileActions, type FileActions } from './useFileActions'

/** 右侧文件夹树节点（模板结构 + 该文件夹下的槽位行） */
export interface FolderNode {
  Code: string
  FolderName: string
  FullPath: string
  Depth: number
  Files: FileSlot[]
  Children: FolderNode[]
  Required: number
  Live: number
  Ready: number
  Converting: number
  Missing: number
}

/**
 * 左树节点（企业 → 阶段，**一棵树**）
 *
 * <para>为什么要把企业/阶段塞进同一棵树：企业节点与阶段节点原来是「树 + 单选组」两套控件，
 * 专家管理多个企业时无法一眼看清「哪个企业有哪些阶段」，且选中态分散在两处互相打架。</para>
 */
export interface ResourceTreeNode {
  /**
   * 树内唯一键。
   *
   * <para>⚠️ 阶段节点键必须**带企业 Code**（`S:{企业Code}:{阶段Code}`）：同一工作区里多个企业会关联
   * 同一个认证阶段（实测 `G4测试企业甲` 与 `测试企业b` 都关联了 `复审`），只用 `S:{阶段Code}` 会撞键 ⇒
   * `el-tree` 的 `node-key` 失去唯一性，`setCurrentKey` 会把高亮打到别的企业的节点上。</para>
   */
  Key: string
  Type: 'enterprise' | 'stage'
  /** 企业 Code 或阶段 StageCode */
  Code: string
  Name: string
  /** 仅阶段节点：所属企业 Code */
  EnterpriseCode?: string
  /** 仅企业节点：阶段数量 */
  StageCount?: number
  /** 仅阶段节点：关联标准数（0 = 未关联，置灰） */
  StandardCount?: number
  Disabled?: boolean
  Children?: ResourceTreeNode[]
}

/** 企业节点键 */
function enterpriseKey(code: string): string {
  return `E:${code}`
}

/** 阶段节点键（带企业前缀，避免跨企业同阶段撞键） */
function stageKey(enterpriseCode: string, stageCode: string): string {
  return `S:${enterpriseCode}:${stageCode}`
}

/**
 * 单个标准的「不可用」文案：`标准名（编号）：原因`
 *
 * <para>为什么不直接用后端 Message：后端那句自带处置建议（"补齐模板后会自动初始化"），
 * 整句很长，塞进逐标准的清单里会被读成四条重复的告警。这里只给**短原因**，
 * 「关联有效 / 补齐后自动生成」这类处置说明统一放在上方提示条的 hint 里，避免两处口径不一致。</para>
 */
function formatStandardIssue(std: StageStandardSummary): string {
  const no = std.StandardNo ? `（${std.StandardNo}）` : ''
  // ★ 2026-09-30 提示信息优化：原「管理端未配置该标准的目录模板」是写给管理员的诊断，
  //   专家看不懂也管不了。改为专家视角 —— 只说现状，处置建议收进提示条的一行短话。
  return `${std.StandardName}${no} 暂无资料目录`
}

/** 待上传项（plan 预览 → 四段式执行） */
export interface UploadPlanRow {
  /** 本地 File 引用（按 fileKey 建索引回来） */
  File: File
  /** ★ 计划行 ↔ 本地 File 的关联键：RelativePath || FileName（同名不同目录必须分开） */
  Key: string
  FileName: string
  StandardCode: string
  StandardName: string
  SlotCode: string
  SlotFileName: string
  FolderCode: string
  FolderPath: string
  Level: string
  NeedsConfirm: boolean
  BlockReason?: string | null
  /** 人工取消勾选后不执行 */
  Selected: boolean
}

/** 未归属文件的人工指派（后端 `upload/init` 指派模式：只传 FolderCode 不传 SlotCode） */
export interface UnmatchedAssign {
  File: File
  FileName: string
  StandardCode: string
  FolderCode: string
}

/** 上传机制上下文（四段式的标准定位） */
export interface UploadCtx { enterpriseCode: string; stageCode: string; standardCode: string }
/** 替换机制上下文 */
export interface ReplaceCtx { fileCode: string; enterpriseCode: string; reason: string; expectedModifyTime?: string }
/** 下载机制上下文 */
export interface DownloadCtx { storagePath: string; fileName: string }
/** 上传项业务载荷（槽位模式给 SlotCode，指派模式给 FolderCode） */
export type UploadPayload = { SlotCode?: string; FolderCode?: string }

export class ResourcesLogic extends SingleTableCore<any> {
  /** 本页没有 EntityConfig（右表是「标准 Tab × 模板文件夹树 × 槽位行」），仅为满足 R24 的基类契约 */
  controllerName = 'Auditor/Resources'

  /** 本页无 /config 端点 ⇒ 空实现，避免 `init()` 打 404 */
  protected async loadConfig(): Promise<void> {}

  protected async onAfterInit(): Promise<void> {
    await this.loadEnterprises()
  }

  /**
   * ★ 文件操作机制族（唯一入口）—— 上传/替换/下载统一走它，见 `@share/composables/useFileOps`。
   * 失败语义 = `rollback`（与既有 `uploadFiles` 同口径：任一步失败即回滚草稿行 + 抛错）。
   */
  readonly fileOps = useFileOps<
    UploadCtx, UploadConfirmResult, ReplaceCtx, ManualValuesArchivedNotice | null, DownloadCtx
  >({
    upload: {
      failureMode: 'rollback',
      init: async (entries, ctx) => {
        const res = await uploadInit(ctx.enterpriseCode, ctx.stageCode, ctx.standardCode,
          entries.map(e => {
            const p = e.payload as UploadPayload | undefined
            return { SlotCode: p?.SlotCode, FolderCode: p?.FolderCode, FileName: e.file.name, FileSize: e.file.size }
          }))
        return {
          taskId: res.TaskId,
          // ★ 按 index 把后端目标行绑回本地 File 的关联键（既有 `uploadFiles` 同口径）
          targets: res.Items.map((it, i) => ({ key: entries[i]?.key ?? `#${i}`, fileCode: it.FileCode }))
        }
      },
      file: (taskId, target, file) => uploadFileStep(file, target.fileCode, taskId),
      confirm: (taskId, ctx) => uploadConfirm(taskId, ctx.enterpriseCode),
      cancel: (taskId, ctx) => uploadCancel(taskId, ctx.enterpriseCode)
    },
    replace: {
      run: (file, ctx) =>
        replaceEnterpriseFile(file, ctx.fileCode, ctx.enterpriseCode, ctx.reason, ctx.expectedModifyTime)
    },
    download: {
      fetchBlob: ctx => enterpriseDownloadBlob(ctx.storagePath),
      fileName: ctx => ctx.fileName
    }
  })

  /** 计划行/裸文件 → 机制入口项（关联键优先用计划行 `Key`，回退目录感知键） */
  toEntry(file: File, payload: UploadPayload, key?: string): FileOpEntry<UploadPayload> {
    return { key: key || this.fileKey(file), file, payload }
  }

  // ─── 左侧：企业 / 阶段（stage-tree 一次取齐） ───
  readonly enterpriseNodes = ref<EnterpriseNode[]>([])
  readonly selectedEnterprise = ref<string>('')
  readonly selectedEnterpriseName = ref<string>('')
  readonly loadingEnterprises = ref(false)
  readonly stages = ref<StageItem[]>([])
  readonly selectedStage = ref<string>('')
  readonly selectedStageName = ref<string>('')
  /** 左树「企业 → 阶段」一棵树的数据源 */
  readonly treeNodes = ref<ResourceTreeNode[]>([])
  /** 当前高亮的树节点 Key（`E:{企业Code}` / `S:{企业Code}:{阶段Code}`） */
  readonly selectedKey = ref<string>('')

  // ─── 右侧：阶段汇总 + 各标准主数据 ───
  readonly overview = ref<StageOverviewResult | null>(null)
  readonly directories = ref<Record<string, StandardDirectoryResult>>({})
  readonly activeTab = ref<string>('')
  /** 不可配置的标准（机构未配模板）沿用后端给的说明文案 */
  readonly loadingMessage = ref<string>('')

  // ─── 轮询 ───
  readonly polling = ref(false)
  readonly queueBusy = ref(false)
  private pollTimer: ReturnType<typeof setInterval> | null = null

  // ═══════════════════ 拆分接线（计划 §3.11 ③ · 2026-10-19） ═══════════════════
  //
  // 上传状态机 → `useUpload.ts`｜文件操作 → `useFileActions.ts`
  // ⛔ 调用面**保持不变**（仍是 `logic.openSingleUpload(...)` / `logic.batchFiles`）——
  //    `index.vue` 一个字都没改，39 例安全网也没改。
  // ⚠️ `createUploadActions(this, …)` 传 `this` 当宿主：下面这些字段**必须**在它之前初始化
  //    （`fileOps` / `toEntry` 在本块之前，树与文件夹查询是原型方法，不受字段顺序影响）。

  private readonly _uploadState = createUploadState()
  private readonly _upload = createUploadActions(this, this._uploadState)
  private readonly _fileState = createFileActionsState()
  private readonly _fileActions = createFileActions(this, this._fileState)

  readonly uploading = this._uploadState.uploading
  readonly uploadProgress = this._uploadState.uploadProgress
  readonly singleUploadVisible = this._uploadState.singleUploadVisible
  readonly currentUploadStandardCode = this._uploadState.currentUploadStandardCode
  readonly singleUploadFolderCode = this._uploadState.singleUploadFolderCode
  readonly singleFolderLocked = this._uploadState.singleFolderLocked
  readonly singleUploadFiles = this._uploadState.singleUploadFiles
  readonly singlePlanLoading = this._uploadState.singlePlanLoading
  readonly singlePlanRows = this._uploadState.singlePlanRows
  readonly singlePlanUnmatched = this._uploadState.singlePlanUnmatched
  readonly singlePlanBlocked = this._uploadState.singlePlanBlocked
  readonly singleAssignUnmatched = this._uploadState.singleAssignUnmatched
  readonly batchUploadVisible = this._uploadState.batchUploadVisible
  readonly batchFiles = this._uploadState.batchFiles
  readonly batchPlanLoading = this._uploadState.batchPlanLoading
  readonly batchPlanRows = this._uploadState.batchPlanRows
  readonly batchPlanUnmatched = this._uploadState.batchPlanUnmatched
  readonly batchPlanConflicts = this._uploadState.batchPlanConflicts
  readonly batchPlanCross = this._uploadState.batchPlanCross
  readonly batchPlanBlocked = this._uploadState.batchPlanBlocked
  readonly batchPlanMessage = this._uploadState.batchPlanMessage
  readonly unmatchedAssign = this._uploadState.unmatchedAssign
  readonly replaceDialogVisible = this._uploadState.replaceDialogVisible
  readonly replacingFile = this._uploadState.replacingFile
  readonly replaceNewFile = this._uploadState.replaceNewFile
  readonly replaceReason = this._uploadState.replaceReason

  readonly deleteDialogVisible = this._fileState.deleteDialogVisible
  readonly deletingFile = this._fileState.deletingFile
  readonly deleteReason = this._fileState.deleteReason
  readonly versionsVisible = this._fileState.versionsVisible
  readonly versionsFile = this._fileState.versionsFile
  readonly versionRows = this._fileState.versionRows
  readonly historyRows = this._fileState.historyRows
  readonly restoringVersion = this._fileState.restoringVersion
  readonly versionsRev = this._fileState.versionsRev
  readonly previewVisible = this._fileState.previewVisible
  readonly previewUrl = this._fileState.previewUrl
  readonly previewName = this._fileState.previewName
  readonly resultDrawerVisible = this._fileState.resultDrawerVisible
  readonly resultData = this._fileState.resultData

  /** 锁定态下目标文件夹的可读名称（根目录 `''` → 「根目录」） */
  get singleFolderLabel(): string { return this._upload.singleFolderLabel }
  get batchPlanRowGroups() { return this._upload.batchPlanRowGroups }
  get selectedBatchCount(): number { return this._upload.selectedBatchCount }
  get assignedUnmatched(): UnmatchedAssign[] { return this._upload.assignedUnmatchedList() }

  /** 清单重挂载计数（YzhTable 只在挂载时跑 dataLoader） */
  readonly dataRevision = ref(0)

  // ═══════════════════ YzhTable 配置（守卫 R6：页面禁内联 el-table） ═══════════════════

  readonly slotColumns: YzhTableColumn[] = [
    { prop: 'FileName', label: '标准文件槽位', minWidth: 300, slot: true },
    { prop: 'FileSize', label: '大小', width: 90, slot: true },
    { prop: 'Status', label: '就位状态', width: 110, slot: true },
    { prop: 'ExtractState', label: '提取状态', width: 90, slot: true },
    { prop: 'VersionNumber', label: '版本', width: 70, slot: true }
    // ⛔ 操作列不再手写（2026-10-07）：改用 YzhTable 内置行动作列
    //    rowActionButtons + action-dropdown-only ⇒ 列宽按 actionColWidth 自适应
    //    （原 width:320 固定像素不随按钮数收缩，全缺失的文件夹白占 270px）
  ]

  readonly versionColumns: YzhTableColumn[] = [
    { prop: 'VersionNumber', label: '版本', width: 70, slot: true },
    { prop: 'FileName', label: '文件名', minWidth: 170, showOverflowTooltip: true },
    { prop: 'FileSize', label: '大小', width: 90, slot: true },
    { prop: 'Reason', label: '归档原因', minWidth: 150, showOverflowTooltip: true },
    { prop: 'Restore', label: '操作', width: 100, fixed: 'right', slot: true }
  ]

  /** 不可配置标准的原因清单（标准名（编号）：原因） */
  readonly unconfiguredStandards = ref<string[]>([])

  folderRowsLoader(folder: FolderNode): YzhTableDataLoader<FileSlot> {
    return () => Promise.resolve({ rows: folder.Files, total: folder.Files.length })
  }

  versionRowsLoader(): YzhTableDataLoader<FileVersionRow> {
    return () => Promise.resolve({ rows: this.versionRows.value, total: this.versionRows.value.length })
  }

  /** YzhTable 内层表格 height:100%，嵌在折叠面板里会塌成 0 ⇒ 必须给显式高度 */
  panelHeight(count: number): number {
    return Math.min(44 * Math.max(count, 1) + 56, 420)
  }

  // ═══════════════════ 数据加载 ═══════════════════

  /** 左树：本工作区企业 → 已关联阶段（后端 stage-tree，一次取齐名称与标准数） */
  async loadEnterprises() {
    this.loadingEnterprises.value = true
    try {
      const data = await stageTree()
      this.enterpriseNodes.value = data?.Enterprises ?? []
      // 企业 → 阶段 拍平成一棵树（企业为父、阶段为子）
      this.treeNodes.value = this.enterpriseNodes.value.map(e => ({
        Key: enterpriseKey(e.Code),
        Type: 'enterprise' as const,
        Code: e.Code,
        Name: e.Name,
        StageCount: e.Stages?.length ?? 0,
        Children: (e.Stages ?? []).map(s => ({
          Key: stageKey(e.Code, s.StageCode),
          Type: 'stage' as const,
          Code: s.StageCode,
          Name: s.StageName,
          EnterpriseCode: e.Code,
          StandardCount: s.StandardCount,
          Disabled: s.StandardCount === 0
        }))
      }))
      if (!data?.Configured && data?.Message) ElMessage.warning(data.Message)
      // 选中项若已不在树上（切工作区/被删）→ 清空右侧
      if (this.selectedEnterprise.value
          && !this.enterpriseNodes.value.some(e => e.Code === this.selectedEnterprise.value)) {
        this.resetRightPane()
      }
    } catch (e: any) {
      ElMessage.error(e?.message ?? '企业加载失败')
      this.enterpriseNodes.value = []
      this.treeNodes.value = []
    } finally {
      this.loadingEnterprises.value = false
    }
  }

  private resetRightPane() {
    this.stopPolling()
    this.selectedEnterprise.value = ''
    this.selectedEnterpriseName.value = ''
    this.selectedStage.value = ''
    this.selectedStageName.value = ''
    this.selectedKey.value = ''
    this.stages.value = []
    this.overview.value = null
    this.directories.value = {}
    this.activeTab.value = ''
    // 旧阶段的「不可配置」说明文案不能带到新企业/阶段，否则空态提示是别人的错
    this.loadingMessage.value = ''
    this.unconfiguredStandards.value = []
  }

  /**
   * 左树节点点击的唯一入口（企业节点 → 重置右侧；阶段节点 → 直接进入该阶段）。
   * ★ 未关联标准的阶段不可进入（需求 1：未选阶段不能上传），但仍给用户明确出口提示。
   */
  async selectTreeNode(node: ResourceTreeNode) {
    if (node.Type === 'enterprise') {
      await this.selectEnterpriseByCode(node.Code)
      return
    }
    if (node.Disabled) {
      ElMessage.warning('该阶段尚未关联任何标准，请先在「阶段标准关联」页配置')
      return
    }
    // 跨企业点阶段：先切企业（保持不自动进阶段，由下面显式进入）
    if (this.selectedEnterprise.value !== node.EnterpriseCode) {
      await this.selectEnterpriseByCode(node.EnterpriseCode ?? '', true)
    }
    const stage = this.stages.value.find(s => s.StageCode === node.Code)
    if (stage) await this.selectStage(stage)
  }

  private async selectEnterpriseByCode(code: string, keepStageSelection = false) {
    const node = this.enterpriseNodes.value.find(e => e.Code === code)
    if (!node) return
    this.selectedKey.value = enterpriseKey(code)
    if (this.selectedEnterprise.value === code) return
    this.stopPolling()
    this.selectedEnterprise.value = node.Code
    this.selectedEnterpriseName.value = node.Name
    this.selectedStage.value = ''
    this.selectedStageName.value = ''
    this.overview.value = null
    this.directories.value = {}
    this.activeTab.value = ''
    this.loadingMessage.value = ''
    this.unconfiguredStandards.value = []
    this.stages.value = node.Stages ?? []

    // 只有一个阶段时直接进入（少一次点击；多阶段必须用户显式选，需求 1）
    if (!keepStageSelection && this.stages.value.length === 1) {
      const only = this.stages.value[0]
      if (only.StandardCount > 0) await this.selectStage(only)
    }
  }

  async selectStage(stage: StageItem) {
    if (stage.StandardCount === 0) {
      ElMessage.warning('该阶段尚未关联任何标准，请先在「阶段标准关联」页配置')
      return
    }
    this.selectedStage.value = stage.StageCode
    this.selectedStageName.value = stage.StageName
    this.selectedKey.value = stageKey(this.selectedEnterprise.value, stage.StageCode)
    this.activeTab.value = ''
    this.overview.value = null
    this.directories.value = {}
    await this.loadOverview()
  }

  /** 阶段汇总 + 各标准主数据（= 页面「刷新」/「文件检查」的统一入口） */
  async loadOverview() {
    if (!this.selectedEnterprise.value || !this.selectedStage.value) return
    this.loading.value = true
    this.loadingMessage.value = ''
    try {
      const ov = await stageOverview(this.selectedEnterprise.value, this.selectedStage.value)
      this.overview.value = ov
      if (!ov?.Configured) {
        this.loadingMessage.value = ov?.Message ?? '该阶段暂无可用标准'
        return
      }
      await this.loadDirectories(ov.Standards)
      if (!this.activeTab.value && this.tabStandards.length > 0) {
        this.activeTab.value = this.tabStandards[0].StandardCode
      }
      this.syncPolling()
    } catch (e: any) {
      ElMessage.error(e?.message ?? '加载失败')
    } finally {
      this.loading.value = false
    }
  }

  private async loadDirectories(standards: StageStandardSummary[]) {
    const next: Record<string, StandardDirectoryResult> = {}
    const unconfigured: string[] = []
    for (const s of standards) {
      if (!s.Configured) {
        unconfigured.push(formatStandardIssue(s))
        continue
      }
      try {
        next[s.StandardCode] = await standardDirectory(
          this.selectedEnterprise.value, this.selectedStage.value, s.StandardCode)
      } catch (e: any) {
        // 请求真失败（网络/服务端异常）时不能用「未配置模板」搪塞 —— 原样带出真实原因
        unconfigured.push(`${s.StandardName}：加载失败（${e?.message ?? '未知错误'}）`)
      }
    }
    this.directories.value = next
    this.dataRevision.value++
    /** 不可用标准清单（右侧常驻提示条与 Tab 过滤共用同一份，避免两处口径不一致） */
    this.unconfiguredStandards.value = unconfigured
    // ⛔ 不再弹 Toast：常驻提示条已经说明原因与处置（补模板后自动生成、无需重建关联），
    //   而 loadDirectories 会在每次切阶段与 5s 轮询时重复跑 ⇒ 飘字提示等于反复骚扰。
  }

  /** 只刷新一个标准（上传/替换/恢复后局部刷新，避免整页闪烁） */
  async reloadStandard(standardCode: string) {
    if (!standardCode) return
    try {
      this.directories.value = {
        ...this.directories.value,
        [standardCode]: await standardDirectory(
          this.selectedEnterprise.value, this.selectedStage.value, standardCode)
      }
      this.dataRevision.value++
      await this.refreshOverviewCounts()
    } catch (e: any) {
      ElMessage.error(e?.message ?? '刷新失败')
    }
  }

  private async refreshOverviewCounts() {
    try {
      this.overview.value = await stageOverview(this.selectedEnterprise.value, this.selectedStage.value)
    } catch { /* 汇总失败不覆盖已有清单 */ }
  }

  // ═══════════════════ 派生数据 ═══════════════════

  get tabStandards(): StageStandardSummary[] {
    return (this.overview.value?.Standards ?? []).filter(s => s.Configured)
  }

  get overviewSummary() {
    const s = this.overview.value?.Summary
    if (!s) return null
    return {
      standardCount: s.StandardCount ?? 0,
      totalRequired: s.TotalRequired ?? 0,
      totalLive: s.TotalLive ?? 0,
      totalConverting: s.TotalConverting ?? 0,
      totalMissing: s.TotalMissing ?? 0
    }
  }

  get emptyHint(): string {
    if (this.loadingMessage.value) return this.loadingMessage.value
    if (!this.selectedEnterprise.value) return '请先在左侧选择企业'
    if (!this.selectedStage.value) return '请先在左侧选择认证阶段（未选阶段不能上传资料）'
    return '该企业在此阶段暂无可管理标准'
  }

  directoryOf(standardCode: string): StandardDirectoryResult | null {
    return this.directories.value[standardCode] ?? null
  }

  standardOf(standardCode: string): StageStandardSummary | null {
    return this.tabStandards.find(s => s.StandardCode === standardCode) ?? null
  }

  /** 模板文件夹树（按 ParentCode 嵌套）+ 每级计数（01 §3.2 卡体） */
  folderTreeOf(standardCode: string): FolderNode[] {
    const dir = this.directoryOf(standardCode)
    if (!dir) return []

    const byCode = new Map<string, FolderNode>()
    for (const f of dir.Folders) {
      byCode.set(f.Code, {
        Code: f.Code, FolderName: f.FolderName, FullPath: f.FullPath,
        Depth: f.Depth, Files: [], Children: [], Required: 0, Live: 0, Ready: 0, Converting: 0, Missing: 0
      })
    }
    for (const file of dir.Files) {
      const node = byCode.get(file.FolderCode)
      // 根文件夹行必然存在；缺失（历史脏数据）时挂到第一个根节点，避免文件凭空消失
      const host = node ?? byCode.get(dir.Folders[0]?.Code ?? '')
      if (!host) continue
      host.Files.push(file)
    }

    const roots: FolderNode[] = []
    for (const f of dir.Folders) {
      const node = byCode.get(f.Code)!
      const parent = f.ParentCode ? byCode.get(f.ParentCode) : undefined
      if (parent) parent.Children.push(node)
      else roots.push(node)
    }

    // 计数按**子树全量**汇总（父目录的「已就位/缺失」= 自身 + 全部后代）。
    // ⛔ 只取一层子节点会漏掉三级文件夹里的文件 —— 根目录会显示 49/167 这种明显不对的数。
    const collect = (n: FolderNode): FileSlot[] => {
      const all = [...n.Files]
      for (const c of n.Children) all.push(...collect(c))
      return all
    }
    const roll = (n: FolderNode): void => {
      for (const c of n.Children) roll(c)
      const required = collect(n).filter(f => f.IsRequired)
      n.Required = required.length
      n.Live = required.filter(isSlotLive).length
      n.Ready = required.filter(f => f.Status === 'ready').length
      n.Converting = required.filter(f => f.Status === 'converting').length
      n.Missing = required.filter(f => f.Status === 'missing' || f.Status === 'removed').length
    }
    roots.forEach(roll)
    return roots
  }

  /**
   * 目标文件夹下拉（含层级缩进；⛔ 不再硬编码 1质量手册 等 4 项）。
   *
   * <para>★ 首项固定为「根目录」（`FolderCode=''`）：根目录槽位确实存在（后端 `FolderCode:""`），
   * 早期下拉只列真实文件夹 ⇒ 根目录槽位的目标**永远选不中**，只能被迫选错目录。</para>
   */
  folderOptionsOf(standardCode: string): Array<{ FolderCode: string; Label: string; FullPath: string }> {
    const dir = this.directoryOf(standardCode)
    if (!dir) return []
    const root = { FolderCode: '', Label: '根目录', FullPath: '' }
    const folders = [...dir.Folders]
      .sort((a, b) => a.Depth - b.Depth || a.Sort - b.Sort)
      .map(f => ({
        FolderCode: f.Code,
        FullPath: f.FullPath,
        Label: `${'　'.repeat(Math.max(0, f.Depth - 1))}${f.FolderName}`
      }))
    return [root, ...folders]
  }

  folderFullPathOf(standardCode: string, folderCode: string): string {
    return this.directoryOf(standardCode)?.Folders.find(f => f.Code === folderCode)?.FullPath ?? ''
  }

  // ═══════════════════ 轮询（转换/提取自动流转可见） ═══════════════════

  private hasInFlight(): boolean {
    return Object.values(this.directories.value).some(dir =>
      dir.Files.some(f => f.Status === 'converting' || f.Status === 'uploading'))
  }

  private syncPolling() {
    if (this.hasInFlight()) this.startPolling()
    else this.stopPolling()
  }

  private startPolling() {
    if (this.pollTimer) return
    this.polling.value = true
    this.pollTimer = setInterval(async () => {
      if (!this.selectedEnterprise.value || !this.selectedStage.value) { this.stopPolling(); return }
      try {
        const stds = this.tabStandards
        const refreshed: Record<string, StandardDirectoryResult> = { ...this.directories.value }
        for (const s of stds) {
          refreshed[s.StandardCode] = await standardDirectory(
            this.selectedEnterprise.value, this.selectedStage.value, s.StandardCode)
        }
        this.directories.value = refreshed
        this.dataRevision.value++
        await this.refreshOverviewCounts()

        // 队列忙闲（取消入口 + 写入类操作禁用依据）
        const firstCfg = refreshed[stds[0]?.StandardCode ?? '']?.EnterpriseConfigCode
        if (firstCfg) {
          const q = await activeQueue(firstCfg, this.selectedEnterprise.value)
          this.queueBusy.value = !!q?.IsBusy
        }
        if (!this.hasInFlight() && !this.queueBusy.value) this.stopPolling()
      } catch {
        this.stopPolling()
      }
    }, 5000)
  }

  stopPolling() {
    if (this.pollTimer) { clearInterval(this.pollTimer); this.pollTimer = null }
    this.polling.value = false
    this.queueBusy.value = false
  }

  /** 顶部「取消队列」：取消该标准目录的运行中队列 */
  async cancelRunningQueue(standardCode: string) {
    const cfg = this.directoryOf(standardCode)?.EnterpriseConfigCode
    if (!cfg) return
    try {
      const q = await activeQueue(cfg, this.selectedEnterprise.value)
      if (!q?.QueueCode) { ElMessage.info('当前没有运行中的转换队列'); return }
      const ok = await confirmOrFalse(`取消转换队列 ${q.QueueCode}？未完成的文件会标记为转换失败。`, '取消队列', { type: 'warning' })
      if (!ok) return
      await cancelQueue(q.QueueCode, this.selectedEnterprise.value)
      ElMessage.success('队列已取消')
      await this.reloadStandard(standardCode)
    } catch (e: any) {
      if (e !== 'cancel') ElMessage.error(e?.message ?? '取消失败')
    }
  }

  openSingleUpload(standardCode: string, folderCode?: string) { return this._upload.openSingleUpload(standardCode, folderCode) }
  closeSingleUploadDialog() { return this._upload.closeSingleUploadDialog() }
  resetSinglePlan() { return this._upload.resetSinglePlan() }
  partitionByPolicy(files: File[]) { return this._upload.partitionByPolicy(files) }
  setSingleUploadFiles(files: File[]) { return this._upload.setSingleUploadFiles(files) }
  autoPreviewSingle() { return this._upload.autoPreviewSingle() }
  clearSingleUploadFiles() { return this._upload.clearSingleUploadFiles() }
  clearBatchFiles() { return this._upload.clearBatchFiles() }
  setSingleUploadFolderFiles(files: File[]) { return this._upload.setSingleUploadFolderFiles(files) }
  removeSingleUploadFile(index: number) { return this._upload.removeSingleUploadFile(index) }
  setSingleUploadFilesFromPicker(files: File[]) { return this._upload.setSingleUploadFilesFromPicker(files) }
  setBatchFilesFromPicker(files: File[]) { return this._upload.setBatchFilesFromPicker(files) }
  dedupeByKey(files: File[]) { return this._upload.dedupeByKey(files) }
  fileKey(file: File, folderPath = '') { return this._upload.fileKey(file, folderPath) }

  /** 计划行构造（多行签名 ⇒ 生成脚本漏掉的，手工补） */
  toPlanRow(r: DispatchRow, file: File, standardCode: string, standardName: string): UploadPlanRow {
    return this._upload.toPlanRow(r, file, standardCode, standardName)
  }

  /** 批量冲突区的人工取舍 */
  toggleBatchRow(row: UploadPlanRow, selected: boolean) { return this._upload.toggleBatchRow(row, selected) }
  unmatchedReasonText(reason: string) { return this._upload.unmatchedReasonText(reason) }
  previewSingleUpload() { return this._upload.previewSingleUpload() }
  fileKeyOf(r: DispatchRow) { return this._upload.fileKeyOf(r) }
  confirmSingleUpload() { return this._upload.confirmSingleUpload() }
  looseSingleFiles(matched: UploadPlanRow[]) { return this._upload.looseSingleFiles(matched) }
  openBatchUpload() { return this._upload.openBatchUpload() }
  closeBatchUpload() { return this._upload.closeBatchUpload() }
  resetBatchPlan() { return this._upload.resetBatchPlan() }
  appendFiles(target: File[], files: File[], folderPath = '') { return this._upload.appendFiles(target, files, folderPath) }
  addBatchFiles(files: File[]) { return this._upload.addBatchFiles(files) }
  removeBatchFile(index: number) { return this._upload.removeBatchFile(index) }
  previewBatchUpload() { return this._upload.previewBatchUpload() }
  setUnmatchedAssign(key: string, patch: { StandardCode?: string; FolderCode?: string }) { return this._upload.setUnmatchedAssign(key, patch) }
  unmatchedAssignOf(key: string) { return this._upload.unmatchedAssignOf(key) }
  confirmUnmatchedAssign() { return this._upload.confirmUnmatchedAssign() }
  confirmBatchUpload() { return this._upload.confirmBatchUpload() }
  runUpload(rows: UploadPlanRow[], standardCode: string, assignFolderCode?: string) { return this._upload.runUpload(rows, standardCode, assignFolderCode) }
  openReplaceDialog(file: FileSlot) { return this._upload.openReplaceDialog(file) }
  closeReplaceDialog() { return this._upload.closeReplaceDialog() }
  setReplaceNewFile(file: File | null) { return this._upload.setReplaceNewFile(file) }
  confirmReplace() { return this._upload.confirmReplace() }
  openDeleteDialog(file: FileSlot) { return this._fileActions.openDeleteDialog(file) }
  closeDeleteDialog() { return this._fileActions.closeDeleteDialog() }
  confirmDelete() { return this._fileActions.confirmDelete() }
  downloadRow(row: FileSlot) { return this._fileActions.downloadRow(row) }
  downloadVersion(row: FileVersionRow) { return this._fileActions.downloadVersion(row) }
  previewRow(row: FileSlot) { return this._fileActions.previewRow(row) }
  closePreview() { return this._fileActions.closePreview() }
  openVersions(file: FileSlot) { return this._fileActions.openVersions(file) }
  closeVersions() { return this._fileActions.closeVersions() }
  handleRestore(row: FileVersionRow) { return this._fileActions.handleRestore(row) }
  handleTriggerExtract(file: FileSlot) { return this._fileActions.handleTriggerExtract(file) }
  handleViewResult(file: FileSlot) { return this._fileActions.handleViewResult(file) }
  previewExtractedJson(json: string) { return this._fileActions.previewExtractedJson(json) }
  opTypeText(item: HistoryItem) { return this._fileActions.opTypeText(item) }
  levelText(level: string) { return this._fileActions.levelText(level) }
  isLive(row: FileSlot) { return this._fileActions.isLive(row) }
  isRemoved(row: FileSlot) { return this._fileActions.isRemoved(row) }
}

/**
 * ★ 声明合并：把两个 composable 的动作**挂到类型上**。
 *
 * 运行时由类里的 `_upload` / `_fileActions` 逐个转发实现（见上面的转发块），
 * 这里只补类型 —— 好处是 `index.vue` 的 `logic.xxx()` 调用**一个字都不用改**。
 */
export interface ResourcesLogic extends UploadActions, FileActions {}

export default ResourcesLogic
