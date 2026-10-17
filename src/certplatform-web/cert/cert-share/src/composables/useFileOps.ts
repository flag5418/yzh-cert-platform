/**
 * ★ 文件操作机制族（上传 / 替换 / 下载 / 打包）—— 三处入口统一的「机制层」
 *
 * <para>依据：`docs/40-实施/开发计划/三处上传入口统一机制设计-V2.md` §4.2。</para>
 *
 * <para>抽的是「<b>怎么做</b>」（管线编排、替换、下载、打包、进度、保存）；
 * 不抽「<b>做什么</b>」（列定义、状态机、操作按钮、下载/打包口径）——
 * 端点、签名、业务子集一律由调用方<b>注入</b>（§3.2 / §3.3）。</para>
 *
 * <para>⚠️ 落地铁律（都是本项目踩过的坑，⛔ 不得违反）：</para>
 * <list type="number">
 *   <item>`accept` 必须由 {@link buildAcceptAttribute} 生成，⛔ 页面不许硬编码后缀串。</item>
 *   <item>系统噪声（`.DS_Store` / `~$…`）<b>静默剔除、不阻塞</b> —— 曾因「一个不合规就整体中止」导致整文件夹传不上去。</item>
 *   <item>`plan` <b>可选</b>：标准目录无 `upload/plan`（设计 §3.4），⛔ 不得当必备环节。</item>
 *   <item>取字节必走<b>鉴权 Blob</b>，⛔ 禁裸链接（`window.open` / `iframe src` 必然 401）。</item>
 *   <item>保存统一走 {@link saveBlob}（唯一实现，替代 3 份等价代码）。</item>
 * </list>
 */
import { ref, computed } from 'vue'
import {
  partitionUploadable,
  buildAcceptAttribute,
  type UploadCategory,
} from '@share/constants/upload-file-policy'
import { saveBlob } from '@share/utils/download'

// ═══════════════════════ 上传 ═══════════════════════

/** 上传批次中的一项：调用方把「本地 File + 稳定关联键 + 业务载荷」编好 */
export interface FileOpEntry<TPayload = unknown> {
  /**
   * 稳定关联键（`webkitRelativePath || name`）。
   * ⛔ 不能用 `File.name`：一个文件夹里可有多份同名文件，用文件名做索引会把多行塌缩到同一份字节。
   */
  key: string
  file: File
  /** 业务载荷（SlotCode / FolderCode / …），原样透传给调用方的 `init` */
  payload?: TPayload
}

/** `init` 下发的目标行（composable 用 `key` 找回本地 File 后逐份上传） */
export interface FileOpTarget {
  key: string
  fileCode: string
  /** true = 该行无需传字节（如原始资料 `Action=skip` 的幂等命中） */
  skip?: boolean
}

/**
 * 单文件失败策略 —— 三处既有语义**本就不同**，⛔ 不得强行统一（否则改变行为 = 风险 R1 高危）。
 *   · `rollback` 失败即回滚（cancel）并抛错 —— 企业资料库（清对象 + 撤草稿行）
 *   · `continue` 失败仅告警、继续传其余 —— 企业原始资料（单文件失败不整体中断）
 *   · `abort`    失败即抛错、不回滚 —— 标准文档管理
 */
export type FileFailureMode = 'rollback' | 'continue' | 'abort'

export interface UploadPipeline<TCtx, TResult> {
  /** Step1：建任务 + 下发目标行（`plan` 由调用方在 `start` 之前自行前置调用，本机制不含 plan） */
  init: (entries: FileOpEntry[], ctx: TCtx) => Promise<{ taskId: string; targets: FileOpTarget[] }>
  /** Step2：逐文件传字节 */
  file: (taskId: string, target: FileOpTarget, file: File, ctx: TCtx) => Promise<void>
  /** Step3：激活 + 入队（有则调用；`rollback`/`abort` 失败时不调用） */
  confirm?: (taskId: string, ctx: TCtx) => Promise<TResult>
  /** 回滚（仅 `rollback` 模式自动调用；也可由页面经 {@link useFileOps} 的 `cancel` 手动触发） */
  cancel?: (taskId: string, ctx: TCtx) => Promise<void>
  failureMode?: FileFailureMode
  /** `continue` 模式下逐条失败的告警出口（页面据此 `ElMessage.warning`） */
  onFileError?: (file: File, error: unknown) => void
}

export interface UploadRunResult<TResult> {
  taskId: string
  /** 成功传字节的份数 */
  uploaded: number
  /** 被跳过的份数（无目标行 / `skip`） */
  skipped: number
  /** 传字节失败的份数（仅 `continue` 模式可能 > 0） */
  failed: number
  /** `confirm` 的返回（无 confirm 时为 undefined） */
  result?: TResult
}

// ═══════════════════════ 替换 / 下载 / 打包 ═══════════════════════

/** 替换机制：端点/签名/确认弹窗由调用方注入，本机制只负责「跑 + 状态」 */
export interface ReplaceOp<TCtx, TNotice = unknown> {
  run: (file: File, ctx: TCtx) => Promise<TNotice>
}

/** 下载机制：取数端点由调用方注入（各页口径不同）；**保存统一** */
export interface DownloadOp<TCtx> {
  /** 取字节（必走鉴权 Blob，⛔ 禁裸链接） */
  fetchBlob: (ctx: TCtx) => Promise<Blob>
  fileName: (ctx: TCtx) => string
}

/**
 * 打包机制：范围与产物口径由调用方注入（§3.2）。
 * ⚠️ 抽的是**打包导出**（服务端产 ZIP → 下载）；「上传压缩包自动解压」是另一件事
 *    （`upload-file-policy.ts` 明令 `archive` 默认禁，须新增解压任务），⛔ 二者不得合并。
 */
export interface PackOp<TCtx> {
  /** 返回 Blob 则统一由本机制 `saveBlob`；返回 void 视为内部已保存（如 `packageNormalize`） */
  run: (ctx: TCtx) => Promise<Blob | void>
  fileName?: (ctx: TCtx) => string
}

export interface FileOpsOptions<
  TUploadCtx = unknown,
  TResult = unknown,
  TReplaceCtx = TUploadCtx,
  TNotice = unknown,
  TDownCtx = TUploadCtx,
> {
  /** 允许的上传类别（业务子集）；默认 `document/image/pdf`（⛔ 不含 archive） */
  categories?: readonly UploadCategory[]
  upload: UploadPipeline<TUploadCtx, TResult>
  replace?: ReplaceOp<TReplaceCtx, TNotice>
  download?: DownloadOp<TDownCtx>
  pack?: PackOp<TDownCtx>
  /** 进度回调（`done`/`total` 为已处理/总份数）；页面可据此自定义文案 */
  onProgress?: (done: number, total: number, fileName: string) => void
}

export function useFileOps<
  TUploadCtx = unknown,
  TResult = unknown,
  TReplaceCtx = TUploadCtx,
  TNotice = unknown,
  TDownCtx = TUploadCtx,
>(
  options: FileOpsOptions<TUploadCtx, TResult, TReplaceCtx, TNotice, TDownCtx>,
) {
  const uploading = ref(false)
  const stage = ref<'idle' | 'uploading' | 'done'>('idle')
  const completed = ref(0)
  const total = ref(0)
  const currentFile = ref('')
  const error = ref<string | null>(null)

  const replacing = ref(false)
  const downloading = ref(false)
  const packing = ref(false)

  const accepted = ref<File[]>([])
  const noise = ref<Array<{ name: string; message: string }>>([])
  const rejected = ref<Array<{ name: string; message: string }>>([])

  const categories = options.categories
  const failureMode: FileFailureMode = options.upload.failureMode ?? 'abort'

  /** 默认进度文案 `${done}/${total} ${fileName}`（页面可改用 `completed`/`currentFile` 自定义） */
  const progress = computed(() => (total.value ? `${completed.value}/${total.value} ${currentFile.value}` : ''))

  /**
   * 上传前分区（唯一事实源 = `@share/constants/upload-file-policy`）：
   * 系统噪声静默剔除、白名单外剔除；两类原因分开，页面据此选提示文案。
   */
  function partition(files: File[]): {
    accepted: File[]
    noise: Array<{ name: string; message: string }>
    rejected: Array<{ name: string; message: string }>
  } {
    const part = partitionUploadable(files, categories)
    accepted.value = part.accepted as File[]
    noise.value = part.noise
    rejected.value = part.rejected
    return { accepted: accepted.value, noise: noise.value, rejected: rejected.value }
  }

  /** `<input accept>` 属性值（⛔ 页面不许硬编码后缀串） */
  function acceptAttribute(): string {
    return buildAcceptAttribute(categories)
  }

  /** 执行上传管线：`init → file* → confirm`（`plan` 由调用方前置完成） */
  async function start(
    entries: FileOpEntry[],
    ctx: TUploadCtx,
    onProgress?: (done: number, total: number, fileName: string) => void,
  ): Promise<UploadRunResult<TResult>> {
    const report = onProgress ?? options.onProgress
    uploading.value = true
    stage.value = 'uploading'
    error.value = null
    completed.value = 0
    total.value = entries.length
    currentFile.value = ''

    let taskId = ''
    let uploaded = 0
    let skipped = 0
    let failed = 0
    let result: TResult | undefined

    try {
      const init = await options.upload.init(entries, ctx)
      taskId = init.taskId
      const byKey = new Map(init.targets.map(t => [t.key, t]))

      for (const entry of entries) {
        currentFile.value = entry.file.name
        const target = byKey.get(entry.key)
        if (!target || target.skip) {
          skipped++
          completed.value++
          report?.(completed.value, entries.length, entry.file.name)
          continue
        }
        try {
          await options.upload.file(taskId, target, entry.file, ctx)
          uploaded++
        } catch (e) {
          failed++
          if (failureMode === 'continue') {
            options.upload.onFileError?.(entry.file, e)
          } else if (failureMode === 'rollback') {
            // 回滚失败不覆盖原始错误（与既有 uploadFiles 同口径）
            try { await options.upload.cancel?.(taskId, ctx) } catch { /* ignore */ }
            throw e
          } else {
            throw e
          }
        }
        completed.value++
        report?.(completed.value, entries.length, entry.file.name)
      }

      try {
        if (options.upload.confirm) result = await options.upload.confirm(taskId, ctx)
      } catch (e) {
        // ★ 与既有 `uploadFiles` 同口径：confirm 失败在 rollback 模式同样回滚（其 try 亦包住 confirm）
        if (failureMode === 'rollback') {
          try { await options.upload.cancel?.(taskId, ctx) } catch { /* ignore */ }
        }
        throw e
      }
      stage.value = 'done'
      return { taskId, uploaded, skipped, failed, result }
    } catch (e) {
      error.value = e instanceof Error ? e.message : String(e)
      stage.value = 'idle'
      throw e
    } finally {
      uploading.value = false
    }
  }

  /** 手动回滚（页面「取消上传」出口） */
  async function cancel(taskId: string, ctx: TUploadCtx): Promise<void> {
    await options.upload.cancel?.(taskId, ctx)
  }

  /** 替换：端点/签名由调用方注入 */
  async function replace(file: File, ctx: TReplaceCtx): Promise<TNotice> {
    if (!options.replace) throw new Error('未注入替换机制（replace.run）')
    replacing.value = true
    try {
      return await options.replace.run(file, ctx)
    } finally {
      replacing.value = false
    }
  }

  /** 下载：注入取数端点，保存统一走 `saveBlob` */
  async function download(ctx: TDownCtx): Promise<void> {
    if (!options.download) throw new Error('未注入下载机制（download.fetchBlob）')
    downloading.value = true
    try {
      const blob = await options.download.fetchBlob(ctx)
      saveBlob(blob, options.download.fileName(ctx))
    } finally {
      downloading.value = false
    }
  }

  /** 打包导出：返回 Blob 则统一保存；返回 void 视为内部已保存 */
  async function pack(ctx: TDownCtx): Promise<void> {
    if (!options.pack) throw new Error('未注入打包机制（pack.run）')
    packing.value = true
    try {
      const blob = await options.pack.run(ctx)
      if (blob && options.pack.fileName) saveBlob(blob, options.pack.fileName(ctx))
    } finally {
      packing.value = false
    }
  }

  return {
    // 状态
    uploading,
    stage,
    completed,
    total,
    currentFile,
    progress,
    error,
    replacing,
    downloading,
    packing,
    // 分区结果
    accepted,
    noise,
    rejected,
    // 机制
    partition,
    acceptAttribute,
    start,
    cancel,
    replace,
    download,
    pack,
  }
}
