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
 * 【两条预览源】
 * | 源 | 取哪份字节 | 端点 |
 * |---|---|---|
 * | `original` | 资料清单里的**原始文档** | `file-preview?fileCode=`（产物链，回写 DB） |
 * | `template` | 已上传的**空白模板** | `preview-by-path?storagePath=`（裸路径，无 fileCode） |
 *
 * ⛔ 两者**不是**同一份文件的两种渲染，是**两份不同的文件**：
 *   原始文档是 `.doc`（143/168）且含示例数据；模板是用户加工后的 `.docx`。
 *   页面上必须让用户一眼看出「现在看的是哪一份」，否则会误判「我的模板没生效」。
 *
 * 【★ 自动切换规则（用户口述的那条）】
 *   未上传模板 → `original`；已上传 → `template`。
 *   上传成功 ⇒ 父页调 `showTemplate()` **显式**切过去并重载 —— 因为换版时
 *   `storagePath` 一模一样，`DocPreview` 内部的 watch **不会触发**（它按路径做 key）。
 */
import { computed, ref, watch } from 'vue'
import { Document, Files } from '@element-plus/icons-vue'
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
}>()

/** 预览源。`original` = 资料清单原始文档；`template` = 已上传的空白模板 */
type PreviewSource = 'original' | 'template'
const source = ref<PreviewSource>('original')

/** 重载序号 —— 路径不变但字节变了时（换版），靠它强制 `DocPreview` 重新拉取 */
const reloadSeq = ref(0)

/**
 * 模板源**可用**的判据 = 有路径。
 *
 * ⚠️ 用路径而不是 `hasTemplate`：树节点的 `hasTemplate` 来自后端快照，
 *   而 `templatePath` 是同一份快照里的路径 —— 两者本应一致，但**只要有一处不一致**，
 *   按路径判定能保证「有得看就让你看」，按布尔判定则会出现「按钮亮着但点开是空的」。
 */
const templateAvailable = computed(() => props.templatePath.length > 0)

/**
 * 自动跟随模板状态（用户口述的那条规则）。
 *
 * ⛔ `immediate: true` 是必须的：先选了文件 A（无模板）→ 再选文件 B（有模板）时，
 *   `hasTemplate` 从 false 变 true 会触发；但**首次挂载**时 watch 不会跑，
 *   `source` 会停在初始值 `original` —— 于是「已上传模板的文件一进来看到的是原始文档」。
 */
watch(
  () => props.hasTemplate,
  (has) => {
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

/** 交给 `DocPreview` 的文件对象（`fileCode` 只在原始源下给 —— 模板没有 fileCode） */
const activeFile = computed(() => {
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

/** 当前源对应的下载按钮文案（两份文件不同名，⛔ 不能都叫「下载」） */
const downloadLabel = computed(() => (source.value === 'template' ? '下载模板' : '下载原始件'))

/**
 * 面板脚注：一句话说明「现在看的是哪一份」。
 *
 * ⛔ 纯文本，⛔ 不要写 `**加粗**` —— 这里用 `{{ }}` 插值渲染，
 *   Markdown 不会被解析，星号会**原样显示**（本页已踩过一次：`选择一个**文件**`）。
 *   需要强调就用静态文案 + 动态文案分开渲染，别在插值里写标记。
 */
const sourceHint = computed(() => {
  if (source.value === 'template') {
    return '已上传的空白模板 —— 扫描出的锚点位置就在这份文件里'
  }
  if (templateAvailable.value) {
    return '资料清单原始文档（含示例数据）；可切换到「空白模板」看加工后的版本'
  }
  return '资料清单原始文档。加工成空白模板并上传后，这里会自动切换成模板'
})

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

defineExpose({ refresh, showTemplate })
</script>

<template>
  <div class="preview-pane">
    <!--
      预览源切换条。
      ⚠️ 未上传模板时**整条不显示**：只有一个选项的切换器是噪音，
        还会让人以为「还有别的东西没解锁」。
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
      </el-radio-group>
      <span class="source-bar__hint">{{ sourceHint }}</span>
    </div>

    <DocPreview
      v-if="fileCode || templatePath"
      :key="previewKey"
      :file="activeFile"
      :download-label="downloadLabel"
    />
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
  background: var(--yzh-color-bg-subtle, #f9fafb);
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
