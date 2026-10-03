<script setup lang="ts">
/**
 * 企业全局参数定义（专家端 · ★ 左树右表）
 *
 * 菜单 `MENU_AUD_10`｜路由 `/enterprise-fill-params`
 *
 * ★ 左树 = 企业 → 标准 → 阶段；右区 = 两个 Tab，分别回答两个问题：
 *   ① 「参数完善」—— 这家企业的文档还缺哪些信息？谁来补？（系统已自动带出一部分）
 *   ② 「填充预览」—— 补齐之后，文档会长什么样？每一处的值从哪来？
 *
 * ★ 三种树点击都有明确语义（见 `logic.ts` 的 `applyScope` 注释）：
 *   点企业 = 通用参数｜点标准 = 通用 + 该标准专属｜点阶段 = 再加阶段专属与标准×阶段专属。
 *
 * ⛔ 本页不使用 `useSingleTable` / `useTreeTable`：数据形态是「后台定义 × 企业档案」的
 *    **合并视图**，右区是分组卡片而不是表格，硬套内核只会让两边都变形。
 *    左树直接用 `YzhTreeTableLayout` 外壳 + 自己管理的 `treeNodes`。
 */
import { onMounted, ref } from 'vue'
import { YzhTreeTableLayout, type TreeNode } from '@yzh-core'
import {
  EnterpriseFillParamsLogic,
  VALUE_SOURCE_LABEL,
  VALUE_SOURCE_TAG,
  parseEnumOptions,
} from './logic'

const logic = new EnterpriseFillParamsLogic()
const activeTab = ref('params')

/** 作用域层级 → 界面标签 */
const SCOPE_LEVEL_LABEL: Record<string, string> = {
  enterprise: '企业级 · 通用参数',
  standard: '标准级 · 通用 + 标准专属',
  stage: '阶段级 · 通用 + 标准专属 + 阶段专属',
}

onMounted(() => logic.init())

function onTabChange(name: string | number): void {
  if (name === 'preview' && !logic.preview.value) logic.runPreview()
}

async function handleNodeClick(node: TreeNode): Promise<void> {
  await logic.onNodeClick(node)
}
</script>

<template>
  <div class="efp-page">
    <YzhTreeTableLayout
      :tree-data="logic.treeNodes.value"
      :tree-width="300"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :tree-default-expand-all="true"
      @tree-node-click="handleNodeClick"
    >
      <!-- 树底部：空树 / 无关联时的兜底提示（后端下发 Hint） -->
      <template #treeFooter>
        <div v-if="logic.treeHint.value" class="efp-tree-hint">
          {{ logic.treeHint.value }}
        </div>
      </template>

      <template #default>
        <div class="efp-main">
          <!-- ══════════ 当前作用域 ══════════ -->
          <div class="efp-scopebar">
            <div class="efp-scopebar__left">
              <span class="efp-scopebar__label">当前作用域</span>
              <span class="efp-scopebar__value">{{ logic.scopeLabel.value || '未选择' }}</span>
              <el-tag v-if="logic.scopeLevel.value" size="small" effect="plain">
                {{ SCOPE_LEVEL_LABEL[logic.scopeLevel.value] || logic.scopeLevel.value }}
              </el-tag>
            </div>
            <el-button
              size="small"
              :loading="logic.loading.value"
              @click="logic.loadMergeList()"
            >
              刷新清单
            </el-button>
          </div>

          <el-tabs v-model="activeTab" class="efp-tabs" @tab-change="onTabChange">
            <!-- ══════════ Tab 1：参数完善 ══════════ -->
            <el-tab-pane label="参数完善" name="params">
              <div v-if="logic.treeNodes.value.length === 0 && !logic.treeLoading.value" class="efp-empty">
                当前工作区还没有企业档案。请先到「企业管理」建档，再回来完善参数。
              </div>

              <!--
                ★ 有企业、但当前作用域下**没有任何参数定义** → 明确说清原因。
                ⛔ 不要落到下面那张表：那会渲染成「已完善 0 / 0 项」的空清单，
                   让人以为是**企业资料没填**，从而去企业管理白找（真正要改的是后台参数定义）。
              -->
              <div
                v-else-if="logic.loaded.value && logic.groups.value.length === 0"
                class="efp-empty"
              >
                <p>当前作用域下还没有参数定义，所以这张清单是空的。</p>
                <p>
                  这不是企业资料的问题 —— 请到后台菜单「体系认证全局参数定义」为当前机构预定义参数；
                  其中<b>标准 / 阶段留空 = 不限</b>，对该机构下所有标准、所有阶段生效。
                </p>
              </div>

              <template v-else>
                <!-- 完成度 -->
                <div class="efp-progress">
                  <div class="efp-progress__head">
                    <span class="efp-progress__name">{{ logic.enterpriseName.value }}</span>
                    <span class="efp-progress__num">
                      已完善 {{ logic.progress.value.Filled }} / {{ logic.progress.value.Total }} 项
                      <span class="efp-progress__req">
                        （必填 {{ logic.progress.value.RequiredFilled }} / {{ logic.progress.value.Required }}）
                      </span>
                    </span>
                  </div>
                  <el-progress
                    :percentage="logic.liveCompletion.value"
                    :stroke-width="14"
                    :status="logic.liveCompletion.value >= 100 ? 'success' : undefined"
                  />
                  <div class="efp-progress__hint">
                    清单已自动带出企业档案中已有的信息；标「自动带出」的项请到「企业管理」修改，改一处全文档同步。
                  </div>
                </div>

                <!-- 分组清单 -->
                <el-card
                  v-for="g in logic.groups.value"
                  :key="g.GroupName"
                  class="efp-group"
                  shadow="never"
                >
                  <template #header>
                    <span class="efp-group__title">{{ g.GroupName }}</span>
                    <span class="efp-group__count">
                      {{ g.Items.filter((i) => i.IsFilled).length }} / {{ g.Items.length }}
                    </span>
                  </template>

                  <div
                    v-for="item in g.Items"
                    :key="item.ParamCode"
                    class="efp-item"
                    :class="{ 'efp-item--readonly': !item.Editable }"
                  >
                    <div class="efp-item__label">
                      <span v-if="item.IsRequired" class="efp-item__required">*</span>
                      <span class="efp-item__name">{{ item.ParamName }}</span>
                      <el-tag
                        :type="VALUE_SOURCE_TAG[item.ValueSource] || 'info'"
                        size="small"
                        effect="plain"
                      >
                        {{ VALUE_SOURCE_LABEL[item.ValueSource] || item.ValueSource }}
                      </el-tag>
                      <el-tag v-if="item.IsBuiltin" type="warning" size="small" effect="plain">内置</el-tag>
                    </div>

                    <div class="efp-item__control">
                      <!-- 枚举 -->
                      <el-select
                        v-if="item.ValueType === 'enum'"
                        :model-value="logic.valueOf(item)"
                        :disabled="!item.Editable"
                        :placeholder="item.Placeholder || `请选择${item.ParamName}`"
                        clearable
                        style="width: 100%"
                        @update:model-value="logic.onValueChange(item, $event ?? '')"
                      >
                        <el-option
                          v-for="o in parseEnumOptions(item.EnumOptions)"
                          :key="o.value"
                          :label="o.label"
                          :value="o.value"
                        />
                      </el-select>

                      <!-- 数字 -->
                      <el-input
                        v-else-if="item.ValueType === 'number'"
                        :model-value="logic.valueOf(item)"
                        :disabled="!item.Editable"
                        :placeholder="item.Placeholder || `请输入${item.ParamName}`"
                        @update:model-value="logic.onValueChange(item, String($event ?? ''))"
                      />

                      <!-- 日期 -->
                      <el-date-picker
                        v-else-if="item.ValueType === 'date'"
                        :model-value="logic.valueOf(item)"
                        :disabled="!item.Editable"
                        type="date"
                        value-format="YYYY-MM-DD"
                        :placeholder="item.Placeholder || `请选择${item.ParamName}`"
                        style="width: 100%"
                        @update:model-value="logic.onValueChange(item, String($event ?? ''))"
                      />

                      <!-- 长文本（质量方针等） -->
                      <el-input
                        v-else-if="item.ParamCode === 'quality_policy'
                          || item.ParamCode === 'quality_objective'
                          || item.ParamCode === 'company_profile'"
                        :model-value="logic.valueOf(item)"
                        :disabled="!item.Editable"
                        type="textarea"
                        :rows="4"
                        :placeholder="item.Placeholder || `请输入${item.ParamName}`"
                        @update:model-value="logic.onValueChange(item, String($event ?? ''))"
                      />

                      <!-- 文本 -->
                      <el-input
                        v-else
                        :model-value="logic.valueOf(item)"
                        :disabled="!item.Editable"
                        :placeholder="item.Placeholder || `请输入${item.ParamName}`"
                        @update:model-value="logic.onValueChange(item, String($event ?? ''))"
                      />
                    </div>

                    <div class="efp-item__foot">
                      <span class="efp-item__ref">
                        <code>{{ item.ParamCode }}</code>
                        {{ item.SourceRef }}
                      </span>
                      <span class="efp-item__actions">
                        <el-button
                          v-if="!item.Editable"
                          link
                          type="primary"
                          size="small"
                          @click="logic.goToEnterprise()"
                        >
                          去企业管理修改
                        </el-button>
                        <el-button
                          v-if="item.SourceKind === 'ai'"
                          link
                          type="warning"
                          size="small"
                          @click="logic.showAiPrompt(item)"
                        >
                          生成提示词
                        </el-button>
                      </span>
                    </div>
                  </div>
                </el-card>

                <!-- 保存条 -->
                <div class="efp-savebar">
                  <span class="efp-savebar__text">
                    <template v-if="logic.dirtyCount.value > 0">
                      有 <b>{{ logic.dirtyCount.value }}</b> 项改动未保存
                    </template>
                    <template v-else>暂无改动</template>
                  </span>
                  <el-button
                    v-if="logic.dirtyCount.value > 0"
                    :disabled="logic.saving.value"
                    @click="logic.resetEdits()"
                  >
                    放弃改动
                  </el-button>
                  <el-button
                    type="primary"
                    :loading="logic.saving.value"
                    :disabled="logic.dirtyCount.value === 0"
                    @click="logic.save()"
                  >
                    保存完善结果
                  </el-button>
                </div>
              </template>
            </el-tab-pane>

            <!-- ══════════ Tab 2：填充预览 ══════════ -->
            <el-tab-pane label="填充预览" name="preview">
              <!-- 4 项能力 -->
              <div class="efp-caps">
                <div v-for="c in logic.capabilities.value" :key="c.kind" class="efp-cap">
                  <div class="efp-cap__head">
                    <code class="efp-cap__syntax">{{ c.syntax }}</code>
                    <span class="efp-cap__name">{{ c.name }}</span>
                  </div>
                  <div class="efp-cap__meaning">{{ c.meaning }}</div>
                  <div class="efp-cap__example">例：{{ c.example }}</div>
                </div>
              </div>

              <div class="efp-previewbar">
                <el-switch
                  v-model="logic.aiEnabled.value"
                  active-text="启用 AI 生成项"
                  inactive-text="关闭 AI 生成项"
                />
                <el-button
                  type="primary"
                  :loading="logic.previewLoading.value"
                  :disabled="!logic.enterpriseCode.value"
                  @click="logic.runPreview()"
                >
                  按当前企业填充演示文档
                </el-button>
              </div>

              <div v-if="logic.preview.value" class="efp-preview">
                <!-- 左：成文 -->
                <div class="efp-preview__left">
                  <div class="efp-preview__title">成文预览（页眉 / 页脚每页重复）</div>
                  <div class="efp-paper">
                    <div class="efp-paper__header">{{ logic.preview.value.header }}</div>
                    <pre class="efp-paper__body">{{ logic.preview.value.output }}</pre>
                    <div class="efp-paper__footer">{{ logic.preview.value.footer }}</div>
                  </div>
                </div>

                <!-- 右：证据报告 -->
                <div class="efp-preview__right">
                  <div class="efp-preview__title">证据摘要</div>

                  <div class="efp-report__score">
                    <span class="efp-report__score-num">
                      {{ logic.preview.value.report.resolved }} / {{ logic.preview.value.report.total }}
                    </span>
                    <span class="efp-report__score-label">处已填</span>
                  </div>

                  <el-progress
                    :percentage="Math.round(logic.preview.value.report.completion * 1000) / 10"
                    :stroke-width="12"
                  />

                  <div class="efp-report__stats">
                    <span>参数 {{ logic.preview.value.stats.paramCount }} 项</span>
                    <span>自动带出 {{ logic.preview.value.stats.autoMappedCount }}</span>
                    <span>企业填写 {{ logic.preview.value.stats.filledFromTableCount }}</span>
                  </div>

                  <!-- 按能力分布 -->
                  <div class="efp-report__section">按能力分布</div>
                  <div
                    v-for="k in logic.preview.value.report.byKind"
                    :key="k.kind"
                    class="efp-report__kind"
                  >
                    <span class="efp-report__kind-name">{{ k.name }}</span>
                    <el-progress
                      :percentage="Math.round((k.resolved / Math.max(1, k.resolved + k.pending)) * 100)"
                      :color="logic.capabilityColor(k.kind) === 'primary' ? 'var(--yzh-color-primary, #409eff)'
                        : logic.capabilityColor(k.kind) === 'success' ? 'var(--yzh-color-success, #67c23a)'
                          : logic.capabilityColor(k.kind) === 'warning' ? 'var(--yzh-color-warning, #e6a23c)' : 'var(--yzh-color-text-tertiary, #909399)'"
                      :stroke-width="10"
                      :show-text="false"
                      class="efp-report__kind-bar"
                    />
                    <span class="efp-report__kind-num">{{ k.resolved }} / {{ k.resolved + k.pending }}</span>
                  </div>

                  <!-- 待办 -->
                  <template v-if="logic.preview.value.report.pendings.length > 0">
                    <div class="efp-report__section">待办（{{ logic.preview.value.report.pendings.length }} 处）</div>
                    <div class="efp-report__pendings">
                      <div
                        v-for="(p, i) in logic.preview.value.report.pendings"
                        :key="`${p.token}-${i}`"
                        class="efp-report__pending"
                      >
                        <code>{{ p.token }}</code>
                        <el-tag size="small" effect="plain">{{ p.kindName }}</el-tag>
                        <span class="efp-report__pending-reason">{{ p.reason }}</span>
                      </div>
                    </div>
                  </template>

                  <template v-else>
                    <div class="efp-report__section efp-report__section--ok">
                      全部锚点已填充 —— 该文档可交付复核
                    </div>
                  </template>
                </div>
              </div>

              <div v-else-if="!logic.previewLoading.value" class="efp-empty">
                点「按当前企业填充演示文档」，看这份文档里的每一处信息从哪来。
              </div>
            </el-tab-pane>
          </el-tabs>
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- ══════════ AI 提示词弹窗 ══════════ -->
    <el-dialog
      v-model="logic.aiDialogVisible.value"
      :title="`AI 生成提示词 —— ${logic.aiPromptTarget.value?.ParamName ?? ''}`"
      width="720px"
    >
      <el-alert type="info" :closable="false" class="efp-ai-note">
        本期不直连大模型：这里产出的是**已经拼好企业上下文的提示词**。
        复制到任意模型生成后，把结果填回该参数即可 —— 与人工填写的值等价，
        都可被覆盖、都可被多份文档复用。
      </el-alert>
      <el-input
        :model-value="logic.aiPromptText.value"
        type="textarea"
        :rows="16"
        readonly
      />
      <template #footer>
        <el-button @click="logic.aiDialogVisible.value = false">关闭</el-button>
        <el-button type="primary" @click="logic.copyAiPrompt()">复制提示词</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.efp-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  box-sizing: border-box;
  overflow: hidden;
}

/* ── 右区容器（左树由 YzhTreeTableLayout 提供）── */
.efp-main {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: 12px 16px 0;
  box-sizing: border-box;
}

/* ── 树底部提示 ── */
.efp-tree-hint {
  font-size: 12px;
  line-height: 1.6;
  color: var(--el-color-warning);
}

/* ── 作用域条 ── */
.efp-scopebar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  flex-wrap: wrap;
}

.efp-scopebar__left {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.efp-scopebar__label {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  flex-shrink: 0;
}

.efp-scopebar__value {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.efp-tabs {
  margin-top: 4px;
}

.efp-empty {
  padding: 48px 0;
  text-align: center;
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.efp-empty p {
  margin: 0 0 6px;
  line-height: 1.8;
}

.efp-empty p:last-child {
  margin-bottom: 0;
}

/* ── 完成度 ── */
.efp-progress {
  padding: 12px 16px;
  margin-bottom: 12px;
  border-radius: 8px;
  background: var(--el-fill-color-lighter);
}

.efp-progress__head {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  margin-bottom: 8px;
}

.efp-progress__name {
  font-size: 15px;
  font-weight: 600;
}

.efp-progress__num {
  font-size: 13px;
  color: var(--el-text-color-regular);
}

.efp-progress__req {
  color: var(--el-text-color-secondary);
}

.efp-progress__hint {
  margin-top: 8px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  line-height: 1.6;
}

/* ── 分组 ── */
.efp-group {
  margin-bottom: 12px;
  border: 1px solid var(--el-border-color-lighter);
}

.efp-group :deep(.el-card__header) {
  padding: 10px 16px;
}

.efp-group__title {
  font-weight: 600;
  font-size: 14px;
}

.efp-group__count {
  margin-left: 10px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

/* ── 参数项 ── */
.efp-item {
  padding: 10px 0;
  border-bottom: 1px dashed var(--el-border-color-lighter);
}

.efp-item:last-child {
  border-bottom: none;
}

.efp-item__control {
  max-width: 640px;
}

.efp-item--readonly .efp-item__control {
  opacity: 0.85;
}

.efp-item__label {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}

.efp-item__required {
  color: var(--el-color-danger);
  font-weight: 700;
}

.efp-item__name {
  font-size: 13px;
  font-weight: 500;
}

.efp-item__foot {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  margin-top: 5px;
}

.efp-item__ref {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.efp-item__ref code {
  margin-right: 6px;
  padding: 0 4px;
  border-radius: 3px;
  background: var(--el-fill-color);
  color: var(--el-text-color-regular);
}

.efp-item__actions {
  white-space: nowrap;
}

/* ── 保存条 ── */
.efp-savebar {
  position: sticky;
  bottom: 0;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 12px;
  padding: 12px 16px;
  margin-top: 8px;
  border-top: 1px solid var(--el-border-color-lighter);
  background: var(--el-bg-color);
}

.efp-savebar__text {
  margin-right: auto;
  font-size: 13px;
  color: var(--el-text-color-regular);
}

/* ── 能力卡片 ── */
.efp-caps {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 12px;
  margin-bottom: 14px;
}

.efp-cap {
  padding: 12px 14px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
  background: var(--el-fill-color-lighter);
}

.efp-cap__head {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin-bottom: 6px;
}

.efp-cap__syntax {
  padding: 2px 6px;
  border-radius: 4px;
  background: var(--el-bg-color);
  color: var(--el-color-primary);
  font-size: 12px;
}

.efp-cap__name {
  font-weight: 600;
  font-size: 14px;
}

.efp-cap__meaning {
  font-size: 12px;
  color: var(--el-text-color-regular);
  line-height: 1.6;
}

.efp-cap__example {
  margin-top: 6px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.efp-previewbar {
  display: flex;
  align-items: center;
  gap: 18px;
  margin-bottom: 14px;
}

/* ── 预览 ── */
.efp-preview {
  display: grid;
  grid-template-columns: minmax(0, 1.5fr) minmax(320px, 1fr);
  gap: 16px;
  align-items: start;
}

.efp-preview__title {
  font-size: 13px;
  font-weight: 600;
  margin-bottom: 8px;
  color: var(--el-text-color-regular);
}

.efp-paper {
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: var(--el-bg-color);
  overflow: hidden;
}

.efp-paper__header,
.efp-paper__footer {
  padding: 6px 14px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-lighter);
}

.efp-paper__header {
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.efp-paper__footer {
  border-top: 1px solid var(--el-border-color-lighter);
}

.efp-paper__body {
  margin: 0;
  padding: 16px;
  max-height: 560px;
  overflow: auto;
  font-family: var(--el-font-family);
  font-size: 13px;
  line-height: 1.85;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}

/* ── 报告 ── */
.efp-report__score {
  display: flex;
  align-items: baseline;
  gap: 6px;
  margin-bottom: 6px;
}

.efp-report__score-num {
  font-size: 22px;
  font-weight: 700;
  color: var(--el-color-primary);
}

.efp-report__score-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.efp-report__stats {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 14px;
  margin-top: 10px;
  font-size: 12px;
  color: var(--el-text-color-regular);
}

.efp-report__section {
  margin: 16px 0 8px;
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-regular);
}

.efp-report__section--ok {
  color: var(--el-color-success);
}

.efp-report__kind {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}

.efp-report__kind-name {
  width: 76px;
  flex: none;
  font-size: 12px;
  color: var(--el-text-color-regular);
}

.efp-report__kind-bar {
  flex: 1;
  min-width: 0;
}

.efp-report__kind-num {
  width: 48px;
  flex: none;
  text-align: right;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.efp-report__pendings {
  max-height: 300px;
  overflow: auto;
}

.efp-report__pending {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 6px;
  padding: 6px 0;
  border-bottom: 1px dashed var(--el-border-color-lighter);
  font-size: 12px;
}

.efp-report__pending code {
  padding: 1px 5px;
  border-radius: 3px;
  background: var(--el-fill-color);
  color: var(--el-color-danger);
}

.efp-report__pending-reason {
  flex: 1 1 100%;
  color: var(--el-text-color-secondary);
  line-height: 1.6;
}

.efp-ai-note {
  margin-bottom: 10px;
}
</style>
