<script setup lang="ts">
/**
 * 文档提取规则 —— 提示词管理页面（左树 + 右编辑工作区）
 *
 * 菜单：MENU_00222（规则定义 → 提取提示词管理）
 * 路由：/business/doc-extract-rule
 * 后端：/api/Admin/Workflow/DocExtractRule
 *
 * 布局（与 prompt-template 对齐）：
 *   左栏 = 标准树（按体系分类，OrgStandardStageTree）
 *   右栏 = Prompt 编辑器 + Markdown 预览 + 测试面板
 */
import { ref, computed, onMounted, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { YzhPageLayout } from '@yzh-core'
import { YzhStatusBadge } from '@yzh-core'
import { OrgStandardStageTree, type StdStageTreeNode } from '@share/components'
import { buildAcceptAttribute } from '@share/constants/upload-file-policy'
import {
  filterRules,
  getRuleDetail,
  getPrompt,
  savePrompt,
  type DocExtractRuleDto,
  type DocExtractPromptDto,
} from '@share/api/workflow/doc-extract-rule'

// ── 数据 ──
const ruleList = ref<DocExtractRuleDto[]>([])
const loading = ref(false)

async function loadList() {
  loading.value = true
  try {
    const res = await filterRules({ Page: 1, PageSize: 50, SortField: 'Sort', SortOrder: 'asc', Filters: [] })
    ruleList.value = res.Items
  } finally {
    loading.value = false
  }
}

onMounted(loadList)

// ── 构建左树（按 Category 分组，2 层：体系 → 标准） ──
interface TreeNode extends StdStageTreeNode {
  level?: number
}

function buildTree(rules: DocExtractRuleDto[]): TreeNode[] {
  if (!rules.length) return []

  // 按 Category 分组（Category 取 StandardName 前 3 字或全名兜底）
  const groups = new Map<string, DocExtractRuleDto[]>()
  for (const r of rules) {
    const cat = (r.StandardName || '').replace(/\s*[:：]\s*\d+.*$/, '').trim() || '未分类'
    if (!groups.has(cat)) groups.set(cat, [])
    groups.get(cat)!.push(r)
  }

  const root: TreeNode = {
    Code: '__root__',
    Name: '全部标准',
    Type: 'root',
    level: 0,
    Children: [],
  }

  for (const [cat, items] of groups) {
    const children: TreeNode[] = items.map((r) => ({
      Code: r.Code,
      Name: `${r.StandardName} v${r.Version}`,
      Type: 'standard',
      level: 1,
      StandardCode: r.StandardCode,
    }))
    root.Children!.push({
      Code: `__cat_${cat}`,
      Name: cat,
      Type: 'category',
      level: 1,
      Children: children,
    })
  }
  return [root]
}

const treeData = computed(() => buildTree(ruleList.value))

// ── 当前选中的标准 Code ──
const selectedCode = ref<string>('')

function onTreeNodeClick(node: StdStageTreeNode) {
  if (node.Type !== 'standard') return
  selectedCode.value = String(node.Code)
}

// ── 当前规则详情 ──
const detail = ref<DocExtractRuleDto | null>(null)
const detailLoading = ref(false)

async function loadDetail(code: string) {
  if (!code || code.startsWith('__')) return
  detailLoading.value = true
  try {
    const result = await getRuleDetail(code)
    detail.value = result ?? null
  } catch (e: any) {
    ElMessage.error(`加载规则详情失败: ${e?.message ?? e}`)
  } finally {
    detailLoading.value = false
  }
}

// ★ 关键修复：selectedCode 变了立刻加载；同时首次加载完后也立刻加载第一个标准
watch(selectedCode, loadDetail)

watch(ruleList, (list) => {
  if (list.length && !selectedCode.value) {
    selectedCode.value = list[0].Code
  }
}, { once: true })

// ── 编辑（提示词从 cert_prompt_template 读取，不再存在 DocExtractRule 实体中） ──
const promptEditing = ref('')
const promptLoading = ref(false)
const promptData = ref<DocExtractPromptDto | null>(null)
const markdownCollapsed = ref(false)

async function loadPrompt(standardCode: string) {
  if (!standardCode) return
  promptLoading.value = true
  try {
    promptData.value = await getPrompt(standardCode)
    promptEditing.value = promptData.value?.Template ?? ''
  } catch (e: any) {
    ElMessage.error(`加载提示词失败: ${e?.message ?? e}`)
  } finally {
    promptLoading.value = false
  }
}

// 选中标准变了 → 加载该标准的提示词
watch(selectedCode, (code) => {
  if (code && !code.startsWith('__')) {
    const rule = ruleList.value.find(r => r.Code === code)
    if (rule?.StandardCode) {
      loadPrompt(rule.StandardCode)
    }
  }
})

// ── 保存（提示词写回 cert_prompt_template，ScopeKey = 标准Code） ──
const saving = ref(false)

async function handleSave() {
  if (!detail.value) return
  saving.value = true
  try {
    await savePrompt({
      template: promptEditing.value,
      standardCode: detail.value.StandardCode,
    })
    ElMessage.success('保存成功')
  } catch (e: any) {
    ElMessage.error(`保存失败: ${e?.message ?? e}`)
  } finally {
    saving.value = false
  }
}

// ── 测试（Phase C 实现） ──
const testResult = ref<string>('')
const testFile = ref<File | null>(null)

function onFileChange(e: Event) {
  const target = e.target as HTMLInputElement
  testFile.value = target.files?.[0] ?? null
}

async function runTest() {
  if (!testFile.value) {
    ElMessage.warning('请先上传测试文档')
    return
  }
  ElMessage.info('测试功能待 Phase C 实现')
  // TODO: Phase C —— 上传文档 → LLM → 显示 JSON
}

const hasPrompt = computed(() => (promptEditing.value ?? '').length > 0)
const isFallbackPrompt = computed(() => promptData.value?.IsFallback ?? false)
</script>

<template>
  <YzhPageLayout title="提取提示词管理">
    <div class="doc-extract-rule">
      <!-- 左栏：标准树 -->
      <aside class="doc-extract-rule__left">
        <div class="doc-extract-rule__left-header">标准列表</div>
        <div class="doc-extract-rule__tree-body">
          <OrgStandardStageTree
            :data="treeData"
            node-key="Code"
            title=""
            :filterable="true"
            search-placeholder="搜索标准..."
            :default-expand-all="true"
            :leaf-types="['standard']"
            :current-key="selectedCode"
            :loading="loading"
            @select="onTreeNodeClick"
            @node-click="onTreeNodeClick"
          />
        </div>
      </aside>

      <!-- 右栏：编辑工作区 -->
      <main class="doc-extract-rule__right">
        <template v-if="detail">
          <!-- 顶部信息条 -->
          <div class="doc-extract-rule__info">
            <span class="doc-extract-rule__info-title">{{ detail.StandardName }}</span>
            <span class="doc-extract-rule__info-meta">v{{ detail.Version }} · {{ detail.IsPublished ? '已发布' : '草稿' }}</span>
          </div>

          <!-- Prompt 编辑器 -->
          <section class="doc-extract-rule__section">
            <div class="doc-extract-rule__section-title">
              <span class="doc-extract-rule__section-title-text">
                提取提示词
                <YzhStatusBadge v-if="isFallbackPrompt" type="info" text="全局默认" size="small" />
                <YzhStatusBadge v-else-if="!hasPrompt" type="warning" text="未配置" size="small" />
              </span>
              <el-button
                type="primary"
                size="small"
                :loading="saving || promptLoading"
                :disabled="!detail"
                @click="handleSave"
              >
                保存提示词
              </el-button>
            </div>
            <el-input
              v-model="promptEditing"
              type="textarea"
              :rows="14"
              placeholder="请输入提取提示词。使用 {{document_content}} 引用文档内容。"
              class="doc-extract-rule__textarea"
            />
          </section>

          <!-- Markdown 预览（折叠） -->
          <section class="doc-extract-rule__section">
            <div class="doc-extract-rule__section-title" @click="markdownCollapsed = !markdownCollapsed">
              <span>标准原文预览（只读）</span>
              <el-link type="primary">{{ markdownCollapsed ? '展开' : '折叠' }}</el-link>
            </div>
            <div v-show="!markdownCollapsed" class="doc-extract-rule__markdown">
              <pre>{{ detail.MarkdownContent || '（无 Markdown 内容）' }}</pre>
            </div>
          </section>

          <!-- 测试面板 -->
          <section class="doc-extract-rule__section">
            <div class="doc-extract-rule__section-title">规则测试</div>
            <div class="doc-extract-rule__test-bar">
              <input type="file" :accept="buildAcceptAttribute(['document', 'pdf'])" @change="onFileChange" />
              <el-button type="primary" :disabled="!testFile" @click="runTest">
                运行测试
              </el-button>
            </div>
            <div v-if="testResult" class="doc-extract-rule__test-result">
              <pre>{{ testResult }}</pre>
            </div>
            <div v-else class="doc-extract-rule__test-tip">
              上传测试文档后点击"运行测试"，LLM 将返回提取的 JSON 结构（不落库）
            </div>
          </section>
        </template>

        <div v-else-if="detailLoading" class="doc-extract-rule__empty">加载中...</div>
        <div v-else class="doc-extract-rule__empty">请从左侧选择标准</div>
      </main>
    </div>

    <!-- 底部操作栏 -->
    <template #footer>
      <el-button :loading="saving" type="primary" @click="handleSave">保存</el-button>
      <el-button type="default" disabled>保存并发布（Phase C）</el-button>
    </template>
  </YzhPageLayout>
</template>

<style scoped>
/* ★ 样式法条 S01–S12：颜色/字号/间距一律 var(--yzh-*, 兜底值) */

.doc-extract-rule {
  display: flex;
  height: 100%;
  min-height: 0;
  overflow: hidden;
  box-sizing: border-box;
}

.doc-extract-rule__left {
  width: 280px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--yzh-color-border, #2c2c2c);
  background: var(--yzh-color-bg-subtle, #1e1e1e);
  overflow: hidden;
}

.doc-extract-rule__left-header {
  flex-shrink: 0;
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  font-size: var(--yzh-font-size-base, 14px);
  font-weight: 600;
  color: var(--yzh-color-text-secondary, #bbb);
  border-bottom: 1px solid var(--yzh-color-border, #2c2c2c);
}

.doc-extract-rule__tree-body {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

.doc-extract-rule__right {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
  padding: var(--yzh-space-4, 16px);
  box-sizing: border-box;
  background: var(--yzh-color-bg-container, #242424);
}

.doc-extract-rule__info {
  display: flex;
  align-items: baseline;
  gap: var(--yzh-space-3, 12px);
  margin-bottom: var(--yzh-space-4, 16px);
  padding-bottom: var(--yzh-space-3, 12px);
  border-bottom: 1px solid var(--yzh-color-border, #333);
}

.doc-extract-rule__info-title {
  font-size: var(--yzh-font-size-lg, 16px);
  font-weight: 600;
  color: var(--yzh-color-text-primary, #fff);
}

.doc-extract-rule__info-meta {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-secondary, #909399);
}

.doc-extract-rule__section {
  margin-bottom: var(--yzh-space-4, 16px);
}

.doc-extract-rule__section-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-base, 14px);
  font-weight: 600;
  margin-bottom: var(--yzh-space-2, 8px);
  cursor: pointer;
  color: var(--yzh-color-text-primary, #fff);
}

/* 标题文字块 —— flex:1 占满剩余空间，把右边按钮推远 */
.doc-extract-rule__section-title-text {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex: 1;
  min-width: 0;
}

.doc-extract-rule__textarea {
  width: 100%;
  /* 暗色输入框 */
  --el-input-bg-color: var(--yzh-color-bg-subtle, #1a1a1a);
  --el-input-text-color: var(--yzh-color-text-primary, #fff);
  --el-input-border-color: var(--yzh-color-border, #444);
}

.doc-extract-rule__markdown {
  max-height: 400px;
  overflow-y: auto;
  padding: var(--yzh-space-3, 12px);
  background: var(--yzh-color-bg-subtle, #1a1a1a);
  border: 1px solid var(--yzh-color-border, #333);
  border-radius: var(--yzh-border-radius, 4px);
}

.doc-extract-rule__markdown pre {
  white-space: pre-wrap;
  word-break: break-all;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #ccc);
  margin: 0;
}

.doc-extract-rule__test-bar {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-3, 12px);
  margin-bottom: var(--yzh-space-3, 12px);
}

.doc-extract-rule__test-result {
  max-height: 300px;
  overflow-y: auto;
  padding: var(--yzh-space-3, 12px);
  background: var(--yzh-color-bg-subtle, #1a1a1a);
  border: 1px solid var(--yzh-color-border, #333);
  border-radius: var(--yzh-border-radius, 4px);
}

.doc-extract-rule__test-result pre {
  white-space: pre-wrap;
  word-break: break-all;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #ccc);
  margin: 0;
}

.doc-extract-rule__test-tip {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-secondary, #909399);
  padding: var(--yzh-space-3, 12px);
}

.doc-extract-rule__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: var(--yzh-color-text-secondary, #909399);
  font-size: var(--yzh-font-size-base, 14px);
}
</style>
