<script setup lang="ts">
/**
 * 企业全局参数定义（专家端 · ★ 左树右表）
 *
 * 菜单 `MENU_AUD_10`｜路由 `/enterprise-fill-params`
 *
 * ★ 左树 = **企业 → 标准（两级）**；右区 = 按后台定义逐项填写的清单 ——
 *   这是一页**简单填写页**：后台「企业资料参数」按标准定义要填哪些字段，
 *   这里把它们列出来让企业补值，仅此而已。
 *
 * ⛔ 本页**没有**「填充预览」（2026-10-06 用户裁决）：预览 UI 归企业资料规范化册承载，
 *    后端 `DocumentFillController` 端点与引擎保留但本页不接。
 *
 * ⛔ 本页**不接 AI**（2026-10-07 用户裁决）：原「生成提示词」按钮、AI 提示词弹窗、
 *    后端 `ai-prompt` 端点已全部删除 —— 信息由人手填，不由 AI 分析产出。
 *
 * ★ 树只有两种点击语义（见 `logic.ts` 的 `applyScope` 注释）：
 *   点企业 = 通用参数｜点标准 = 通用 + 该标准专属。
 *   ⛔ **没有阶段层**：后台定义的 `StageCode` 恒空串（不分阶段），挂阶段只会让同一份
 *   清单重复出现 N 次。
 *
 * ⛔ 本页不使用 `useSingleTable` / `useTreeTable`：数据形态是「后台定义 × 企业档案」的
 *    **合并视图**，右区是分组卡片而不是表格，硬套内核只会让两边都变形。
 *    左树直接用 `YzhTreeTableLayout` 外壳 + 自己管理的 `treeNodes`。
 */
import { onMounted } from 'vue'
import { YzhTreeTableLayout, type TreeNode } from '@yzh-core'
import {
  EnterpriseFillParamsLogic,
  VALUE_SOURCE_LABEL,
  VALUE_SOURCE_TAG,
  parseEnumOptions,
} from './logic'

const logic = new EnterpriseFillParamsLogic()

/** 作用域层级 → 界面标签（只有两级：企业 / 标准） */
const SCOPE_LEVEL_LABEL: Record<string, string> = {
  enterprise: '企业级 · 通用参数',
  standard: '标准级 · 通用 + 标准专属',
}

onMounted(() => logic.init())

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
              这不是企业资料的问题 —— 请到后台菜单「企业资料参数」定义要填写的字段；
              其中<b>标准留空 = 通用参数</b>，对所有标准生效。
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
                清单 = 后台「企业资料参数」按当前标准定义的字段；值由企业逐项填写，保存后各文档统一取用。
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
        </div>
      </template>
    </YzhTreeTableLayout>
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
</style>
