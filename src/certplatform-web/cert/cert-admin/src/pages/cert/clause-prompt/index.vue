<script setup lang="ts">
/**
 * 条款属性通用提示词维护（列表页 · 也可嵌入左侧其他页面的抽屉作子组件）
 *
 * ★ 用途二合一：
 *   ① 作为独立路由 `/cert/clause-prompt` 的主页
 *   ② 在 `clause-attr` 页顶部作为「通用提示词维护」抽屉的内容子组件
 *      （通过 `embedded` prop 控制是否自主展示标题和新增按钮）
 *
 * ★ 功能：
 *   - 标准筛选下拉（只看平台级 / 某标准级）
 *   - 列出所有 clause_attr 提示词（编码 / 名称 / 适用标准 / 说明 / 生效状态）
 *   - 新增 / 编辑（走抽屉弹窗）/ 删除 / 激活
 *   - 编辑弹窗内 AI 辅助生成 / 优化草稿
 */
import { onMounted } from 'vue'
import {
  ElCard,
  ElSelect,
  ElOption,
  ElButton,
  ElTable,
  ElTableColumn,
  ElTag,
  ElPopconfirm,
  ElEmpty,
} from 'element-plus'
import { Edit, Delete, MagicStick, Check } from '@element-plus/icons-vue'
import ClausePromptLogic from './logic'
import type { ClausePromptItem } from '@share/api/cert/clause-prompt'
import ClausePromptDialog from './dialog.vue'

/** 是否嵌入模式（在抽屉中展示时不重复标题和新增按钮） */
const props = withDefaults(defineProps<{ embedded?: boolean }>(), {
  embedded: false,
})

const logic = new ClausePromptLogic()

onMounted(async () => {
  await Promise.all([logic.loadPrompts(), logic.loadStandards()])
  logic.selectedStandard.value = ''
})

async function refresh() {
  await logic.loadPrompts()
}
</script>

<template>
  <div class="clause-prompt-page">
    <!-- 头部（仅独立路由时显示） -->
    <div v-if="!props.embedded" class="clause-prompt-page__header">
      <div class="clause-prompt-page__title">条款属性提示词维护</div>
      <div class="clause-prompt-page__subtitle">
        管理 AI 预生成条款属性时使用的提示词（PromptType = clause_attr）。
        同类型只允许一条生效；按标准级 → 平台级回退。
      </div>
    </div>

    <!-- 工具栏 -->
    <div class="clause-prompt-page__toolbar">
      <div class="clause-prompt-page__filter">
        <span class="clause-prompt-page__filter-label">标准筛选：</span>
        <ElSelect
          :model-value="logic.selectedStandard.value"
          placeholder="选择标准（空 = 平台级）"
          style="width: 280px"
          clearable
          @update:model-value="(v: string) => (logic.selectedStandard.value = v || '')"
        >
          <ElOption label="平台级（全部标准）" value="" />
          <ElOption
            v-for="s in logic.standardOptions.value"
            :key="s.code"
            :label="s.display"
            :value="s.code"
          />
        </ElSelect>
      </div>
      <ElButton type="primary" :icon="MagicStick" @click="logic.openAdd()">
        {{ logic.selectedStandard.value ? '新增该标准提示词' : '新增提示词' }}
      </ElButton>
    </div>

    <!-- 列表 -->
    <ElCard shadow="never" class="clause-prompt-page__card">
      <ElEmpty
        v-if="logic.filteredPrompts.length === 0"
        description="该标准下暂无提示词，右上角点「新增提示词」开始配置"
      />
      <ElTable v-else :data="logic.filteredPrompts" stripe>
        <ElTableColumn prop="promptCode" label="提示词编码" width="220" show-overflow-tooltip />
        <ElTableColumn prop="promptName" label="名称" min-width="200" show-overflow-tooltip />
        <ElTableColumn label="适用标准" width="180">
          <template #default="{ row }">
            <ElTag :type="row.standardCode ? 'primary' : 'info'" size="small">
              {{ logic.standardDisplay(row.standardCode) }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="description" label="说明" min-width="160" show-overflow-tooltip />
        <ElTableColumn label="生效" width="80">
          <template #default="{ row }">
            <ElTag :type="row.isActive ? 'success' : 'info'" size="small">
              {{ row.isActive ? '生效' : '未生效' }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="操作" width="220" fixed="right">
          <template #default="{ row }">
            <ElButton
              link
              type="primary"
              :icon="Edit"
              size="small"
              @click="logic.openEdit(row as ClausePromptItem)"
            >
              编辑
            </ElButton>
            <ElButton
              v-if="!row.isActive"
              link
              type="success"
              :icon="Check"
              size="small"
              @click="logic.activatePrompt(row as ClausePromptItem)"
            >
              激活
            </ElButton>
            <ElPopconfirm
              :title="`确定删除提示词「${row.promptName}」？`"
              confirm-button-text="删除"
              cancel-button-text="取消"
              @confirm="logic.deletePrompt(row as ClausePromptItem)"
            >
              <template #reference>
                <ElButton link type="danger" :icon="Delete" size="small">删除</ElButton>
              </template>
            </ElPopconfirm>
          </template>
        </ElTableColumn>
      </ElTable>
    </ElCard>

    <!-- 编辑弹窗（抽屉形态） -->
    <ClausePromptDialog
      v-if="logic.dialogVisible.value"
      :logic="logic"
      @saved="refresh"
    />
  </div>
</template>

<style scoped>
.clause-prompt-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  padding: var(--yzh-space-3, 12px);
  box-sizing: border-box;
  overflow: auto;
  background: var(--el-fill-color-light, #f5f7fa);
}

.clause-prompt-page__header {
  margin-bottom: var(--yzh-space-3, 12px);
}

.clause-prompt-page__title {
  font-size: var(--yzh-font-size-lg, 16px);
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.clause-prompt-page__subtitle {
  margin-top: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: 1.6;
  color: var(--el-text-color-regular);
}

.clause-prompt-page__toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--yzh-space-3, 12px);
}

.clause-prompt-page__filter {
  display: flex;
  align-items: center;
  gap: 8px;
}

.clause-prompt-page__filter-label {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-regular);
}

.clause-prompt-page__card {
  flex: 1;
  min-height: 0;
}

:deep(.clause-prompt-page__card .el-card__body) {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: auto;
}
</style>
