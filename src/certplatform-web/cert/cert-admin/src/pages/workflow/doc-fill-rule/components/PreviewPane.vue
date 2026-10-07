<script setup lang="ts">
/**
 * ★ 中栏预览面板 —— 「看哪一份」的策略层（2026-10-04 新增）
 *
 * ────────────────────────────────────────────────────────────────
 * 【用户原话，逐字】
 *   「我们下载的时候，如果还没有空白文档，那查看的是原始文件的 PDF；
 *     如果我们上传了空白文档，我们应该**刷新**查看空白文档的 pdf。
 *     这是一项非常精准的工作，所以得有必要的逻辑支撑，这也是我们程序严谨性的表现。」
 * ────────────────────────────────────────────────────────────────
 *
 * 【为什么单独一个组件】
 *   「该看哪一份」是**页面策略**（依赖模板状态），而 `DocPreview` 是**通用渲染器**
 *   （给它一个文件，它负责渲染）。把策略塞进渲染器，会让 `cert-share` 里的共享组件
 *   被迫理解「空白模板 / 归一产物 / 锚点」这些只有本页才有的概念。
 *   ⇒ 策略留在本组件，`DocPreview` 保持哑。
 *
 * 【★ 三条预览源（2026-10-06 加第三条 = C10）】
 * | 源 | 取哪份字节 | 端点 |
 * |---|---|---|
 * | `original` | 资料清单里的**原始文档** | `file-preview?fileCode=`（产物链，回写 DB） |
 * | `template` | 已上传的**空白模板** | `preview-by-path?storagePath=`（裸路径，无 fileCode） |
 * | `filled` | **试填后转出的 PDF**（`…/_preview/x.docx.pdf`） | 同上（`.pdf` 原样透传，⛔ 不二次转换） |
 *
 * ⛔ 三者**不是**同一份文件的三种渲染，是**三份不同的文件**：
 *   原始文档是 `.doc`（143/168）且含示例数据；模板是用户加工后的 `.docx`；
 *   填充后是**空白模板被填过一遍**的结果。
 *   页面上必须让用户一眼看出「现在看的是哪一份」，否则会误判「我的模板没生效」。
 *
 * 【★ 自动切换规则（用户口述的那条）】
 *   未上传模板 → `original`；已上传 → `template`。
 *   上传成功 ⇒ 父页调 `showTemplate()` **显式**切过去并重载 —— 因为换版时
 *   `storagePath` 一模一样，`DocPreview` 内部的 watch **不会触发**（它按路径做 key）。
 *   ⛔ **`filled` 不参与自动切换**：它是「看过一眼结果」的动作，
 *      自动切过去会让用户以为「模板就是这个样子」（而那是**填过的**，不是模板）。
 *      必须由「自动填充 / 预览」按钮显式切（`showFilled()`）。
 */
import { computed, ref, watch } from 'vue'
import { Document, Files, MagicStick, Upload } from '@element-plus/icons-vue'
import { DocPreview } from '@share/components'
import { YzhEmptyState } from '@yzh-core'

const props = defineProps<{
  /** 标准资料清单文件 Code（原始文档预览用；未选中时为空串） */
  fileCode: string
  /** 原始文档文件名（含扩展名） */
  fileName: string
  /** 原始文档在 MinIO 的路径 */
  originalPath: string
  /** 空白模板在 MinIO 的路径（`…/_template/x.docx`）；未上传时为空串 */
  templatePath: string
  /** 空白模板文件名（含扩展名）—— `preview-by-path` 靠它判扩展名 */
  templateFileName: string
  /** 该文件是否已上传空白模板 */
  hasTemplate: boolean
  /**
   * ★ C10：试填预览 PDF 在 MinIO 的路径（`…/_preview/x.docx.pdf`）；
   * **空串 = 从未试填过** ⇒ 「填充后预览」选项禁用。
   */
  filledPath?: string
  /** 最近一次试填时间（展示用事实，⛔ 不弹提醒、⛔ 不阻断） */
  filledTime?: string
  /**
   * ★ C9：是否允许「上传空白模板」。
   *
   * 判据由父页给（= 选中文件且**非固定格式**）——
   * 固定文档不生成内容，没有空白模板可传，显示这个按钮只会误导。
   */
  canUploadTemplate?: boolean
  /** 上传进行中（按钮转圈） */
  uploading?: boolean
}>()

const emit = defineEmits<{ (e: 'upload-template'): void }>()

/** 预览源。`original` = 资料清单原始文档；`template` = 已上传的空白模板；`filled` = 试填结果 */
type PreviewSource = 'original' | 'template' | 'filled'
const source = ref<PreviewSource>('original')

/** 重载序号 —— 路径不变但字节变了时（换版 / 重跑试填），靠它强制 `DocPreview` 重新拉取 */
const reloadSeq = ref(0)

/**
 * 模板源**可用**的判据 = 有路径。
 *
 * ⚠️ 用路径而不是 `hasTemplate`：树节点的 `hasTemplate` 来自后端快照，
 *   而 `templatePath` 是同一份快照里的路径 —— 两者本应一致，但**只要有一处不一致**，
 *   按路径判定能保证「有得看就让你看」，按布尔判定则会出现「按钮亮着但点开是空的」。
 */
const templateAvailable = computed(() => props.templatePath.length > 0)

/** ★ C10：试填源可用的判据 = 有 PDF 路径（同 `templateAvailable` 的理由） */
const filledAvailable = computed(() => (props.filledPath ?? '').length > 0)

/**
 * 自动跟随模板状态（用户口述的那条规则）。
 *
 * ⛔ `immediate: true` 是必须的：先选了文件 A（无模板）→ 再选文件 B（有模板）时，
 *   `hasTemplate` 从 false 变 true 会触发；但**首次挂载**时 watch 不会跑，
 *   `source` 会停在初始值 `original` —— 于是「已上传模板的文件一进来看到的是原始文档」。
 *
 * ⛔ **`filled` 时不要覆盖**：用户点「预览」切到填充后视图后，
 *   若此时任何 prop 变化让 `hasTemplate` 重新求值，watch 会把视图抢回 `template`
 *   ⇒ 「点了预览却跳回空白模板」。试填视图只能由 `showFilled()` 进出。
 */
watch(
  () => props.hasTemplate,
  (has) => {
    if (source.value === 'filled') return
    if (has && templateAvailable.value) source.value = 'template'
    else if (!has) source.value = 'original'
  },
  { immediate: true },
)

/**
 * 换文件时**回到该文件的默认源**。
 *
 * ⚠️ 不这么做会串台：文件 A 手动切到「原始文档」→ 点文件 B（已上传模板）
 *   ⇒ `hasTemplate` 若恰好都是 `true`，watch 不触发 ⇒ B 也显示原始文档。
 */
watch(
  () => props.fileCode,
  () => {
    source.value = props.hasTemplate && templateAvailable.value ? 'template' : 'original'
    reloadSeq.value++
  },
)

/**
 * ★ 试填产物**后到**（点「自动填充」→ 后端跑完 → 父页把路径传下来）时，
 * 若当前正停在「填充后预览」视图，必须重载 ——
 * 路径是**固定 key**（同一模板重复试填路径不变）⇒ `DocPreview` 的 watch 不触发，
 * 用户会看到**上一次**的试填结果。与「换版重传模板」是同一类「静默显示旧内容」。
 */
watch(
  () => props.filledPath,
  (p, old) => {
    if (source.value === 'filled' && p && p === old) reloadSeq.value++
  },
)

/** 交给 `DocPreview` 的文件对象（`fileCode` 只在原始源下给 —— 模板/试填都没有 fileCode） */
const activeFile = computed(() => {
  if (source.value === 'filled') {
    return {
      fileName: filledFileName.value,
      storagePath: props.filledPath ?? '',
    }
  }
  if (source.value === 'template') {
    return {
      fileName: props.templateFileName || '空白模板',
      storagePath: props.templatePath,
    }
  }
  return {
    fileCode: props.fileCode,
    fileName: props.fileName,
    storagePath: props.originalPath,
  }
})

/**
 * 试填产物的显示名。
 *
 * ⚠️ 必须带 `.pdf` 扩展名：`DocPreview` **只从文件名推扩展名**
 *   （`ext` computed 读 `fileName`），推不出 `.pdf` 就会落到「暂不支持在线预览」分支
 *   —— 而字节其实是 PDF。这是「标题对、内容错」的经典形态。
 */
const filledFileName = computed(() => {
  const base = props.templateFileName || props.fileName || '文档'
  // 去掉模板自身的扩展名再拼 `.pdf`：`x.docx` → `x.pdf`（不是 `x.docx.pdf`）
  return base.replace(/\.[A-Za-z0-9]{1,8}$/, '') + '.pdf'
})


/**
 * `DocPreview` 的 key。
 *
 * ⛔ 三样都必须拼进去，少一样都会留下一个**静默不刷新**的场景：
 *   ① `source` —— 同一份 `fileCode` 在两种源下是**两份文件**，只按路径做 key 会复用同一实例；
 *   ② `reloadSeq` —— 换版时路径不变，只有它能把它顶掉；
 *   ③ **`fileName` / `templateFileName`** —— `DocPreview` 靠文件名推扩展名。
 *      2026-10-04 实测缺陷：契约接口是异步的，**先**用带徽标的树节点名渲染了一次
 *      （扩展名推成 `doc  ⬜未上传模板` ⇒ 报「暂不支持在线预览」），**后**契约回来把
 *      标题修干净了 —— 但 key 没变 ⇒ 不重挂 ⇒ **标题对、内容错**。
 */
const previewKey = computed(
  () =>
    [
      source.value,
      activeFile.value.storagePath,
      activeFile.value.fileCode ?? '',
      activeFile.value.fileName,
      reloadSeq.value,
    ].join('|'),
)

/** 当前源对应的下载按钮文案（三份文件不同名，⛔ 不能都叫「下载」） */
const downloadLabel = computed(() => {
  if (source.value === 'filled') return '下载填充后预览'
  if (source.value === 'template') return '下载模板'
  return '下载原始件'
})

/**
 * 面板脚注：一句话说明「现在看的是哪一份」。
 *
 * ⛔ 纯文本，⛔ 不要写 `**加粗**` —— 这里用 `{{ }}` 插值渲染，
 *   Markdown 不会被解析，星号会**原样显示**（本页已踩过一次：`选择一个**文件**`）。
 *   需要强调就用静态文案 + 动态文案分开渲染，别在插值里写标记。
 */
const sourceHint = computed(() => {
  if (source.value === 'filled') {
    // ★ 带上试填时间 —— 这是**事实**（用户能据此判断「这份结果是不是我刚改完规则跑的」），
    //   ⛔ 不是「提醒你重跑」（那是教操作，按收敛标准已删）。
    const t = props.filledTime ? `（试填于 ${formatTime(props.filledTime)}）` : ''
    return `试填结果：把空白模板按当前规则填过一遍${t}`
  }
  if (source.value === 'template') {
    return '已上传的空白模板 —— 扫描出的锚点位置就在这份文件里'
  }
  if (templateAvailable.value) {
    return '资料清单原始文档（含示例数据）；可切换到「空白模板」看加工后的版本'
  }
  return '资料清单原始文档。加工成空白模板并上传后，这里会自动切换成模板'
})

/**
 * 试填时间的展示格式（`YYYY-MM-DD HH:mm`）。
 *
 * ⛔ 不用 `new Date(s).toLocaleString()`：不同浏览器/语言环境输出不同
 *   （中文系统给「2026/10/6 下午2:39」），同一页面在不同机器上显示不一致。
 *   ⛔ 也不引入 dayjs —— 本组件只需要一个固定格式，不值得一个依赖。
 */
function formatTime(iso: string): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})/.exec(iso)
  return m ? `${m[1]}-${m[2]}-${m[3]} ${m[4]}:${m[5]}` : iso
}

/** 重载当前源（路径不变、字节变了时用；也可由用户点面板内的「刷新」触发） */
function refresh() {
  reloadSeq.value++
}

/**
 * ★ 切到空白模板并重载 —— **上传成功后由父页显式调用**。
 *
 * 为什么不靠 watch：换版时 `hasTemplate` 已经是 `true`、`templatePath` 也没变，
 * 两个 watch 都不触发 ⇒ 用户上传完看到的还是上一版的 PDF。
 */
function showTemplate() {
  source.value = 'template'
  reloadSeq.value++
}

/**
 * ★ C10：切到「填充后预览」并重载 —— **「自动填充 / 预览」按钮由父页显式调用**。
 *
 * ⚠️ `reloadSeq++` 是必须的：试填产物是**固定 key**（同一模板重复试填路径不变）
 *   ⇒ `DocPreview` 的 watch 不触发 ⇒ 会继续显示**上一次**的结果。
 *
 * ⛔ 没有产物时**不做静默空切**（那会切到一个空白视图）：调用方先判 `filledAvailable`。
 */
function showFilled() {
  if (!filledAvailable.value) return false
  source.value = 'filled'
  reloadSeq.value++
  return true
}

defineExpose({ refresh, showTemplate, showFilled })
</script>

<template>
  <div class="preview-pane">
    <!--
      预览源切换条。
      ⚠️ 未上传模板时**整条不显示**：只有一个选项的切换器是噪音，
        还会让人以为「还有别的东西没解锁」。

      ★ C10：加了第三个「填充后预览」。它与前两个**并列**（不是子项）——
        三者是三份不同的文件，用户必须能一眼看出在看哪一份。
        ⚠️ 未试填过时该选项 **disabled**（`el-radio-button` 的 `disabled` 是**逐个**生效的），
          并挂 `title` 说明原因 —— 直接隐藏会让用户以为「这功能不存在」。
          ⛔ 这里刻意**不用 `el-tooltip`**：它要求单元素子节点，必须再包一层 `<span>`，
             而 Element Plus 的「首尾圆角 / 相邻负边距」是按
             `.el-radio-button:first-child|:last-child|:not(:first-child)` 匹配的 ——
             包一层后第三个 label 变成 span 的首个子元素 ⇒ **1px 双线 + 圆角错位**。
             原型（`51-V1`）用的也是 `title`，口径一致。
    -->
    <div v-if="templateAvailable" class="source-bar">
      <span class="source-bar__label">查看</span>
      <el-radio-group v-model="source" size="small">
        <el-radio-button value="original">
          <el-icon><Document /></el-icon>
          原始文档
        </el-radio-button>
        <el-radio-button value="template">
          <el-icon><Files /></el-icon>
          空白模板
        </el-radio-button>
        <el-radio-button
          value="filled"
          :disabled="!filledAvailable"
          :title="filledAvailable ? '查看试填结果' : '还没有试填结果 —— 先在右栏点「自动填充」'"
        >
          <el-icon><MagicStick /></el-icon>
          填充后预览
        </el-radio-button>
      </el-radio-group>
      <span class="source-bar__hint">{{ sourceHint }}</span>
    </div>

    <DocPreview
      v-if="fileCode || templatePath"
      :key="previewKey"
      :file="activeFile"
      :download-label="downloadLabel"
    >
      <!--
        ★ C9：把「上传空白模板」放到**与「下载原始文档」同一行**（`DocPreview` 的
          `.preview-actions` 内）。此前它挂在右栏锚点页签的操作条上 ——
          用户要在「看文档」与「换模板」之间来回横跳两个栏位。

        ⛔ 看「填充后预览」时**不显示**这个按钮：那个视图里没有「模板」可换，
          把「上传空白模板」摆在一份已经填好的文档旁边，语义是错的。
      -->
      <template #actions>
        <el-button
          v-if="canUploadTemplate && source !== 'filled'"
          type="primary"
          size="small"
          :icon="Upload"
          :loading="uploading"
          @click="emit('upload-template')"
        >
          {{ hasTemplate ? '重新上传模板' : '上传空白模板' }}
        </el-button>
      </template>
    </DocPreview>
    <div v-else class="empty-preview">
      <YzhEmptyState :icon="Document" title="请选择左侧文档" />
    </div>
  </div>
</template>

<style scoped>
.preview-pane {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

/* ── 预览源切换条 ── */
.source-bar {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-1, 4px) var(--yzh-space-4, 16px);
  /* 与页面同底（白）—— 分区靠 `border-bottom` 表达（整页统一底色口径） */
  background: var(--yzh-color-bg-container, #fff);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
}
.source-bar__label {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
  flex-shrink: 0;
}
.source-bar__hint {
  flex: 1;
  min-width: 0;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.empty-preview {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
