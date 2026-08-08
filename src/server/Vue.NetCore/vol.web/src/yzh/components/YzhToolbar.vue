<template>
  <div class="yzh-toolbar">
    <!-- 左侧：操作按钮组 -->
    <div class="yzh-toolbar__left">
      <!-- 预定义按钮（从配置生成） -->
      <el-button
        v-for="btn in resolvedButtons"
        :key="btn.key"
        :type="btn.type || 'default'"
        :size="size"
        :icon="btn.icon"
        :disabled="btn.disabled || false"
        :loading="btn.loading || false"
        @click="handleButtonClick(btn)"
      >
        {{ btn.label }}
      </el-button>

      <!-- 左侧自定义插槽 -->
      <slot name="left" />
    </div>

    <!-- 右侧：工具区域 -->
    <div class="yzh-toolbar__right">
      <!-- 列设置按钮 -->
      <el-popover
        v-if="showColumnSetting"
        trigger="click"
        placement="bottom-end"
        :width="220"
      >
        <template #reference>
          <el-button :size="size" title="列设置">
            <el-icon><Setting /></el-icon>
            列设置
          </el-button>
        </template>
        <div class="yzh-toolbar__column-settings">
          <div class="yzh-toolbar__column-settings-header">列筛选与排序</div>
          <div class="yzh-toolbar__column-settings-body">
            <div
              v-for="col in columnList"
              :key="col.fieldAlias"
              class="yzh-toolbar__column-settings-item"
            >
              <el-checkbox
                :model-value="!col.hidden"
                @change="(val: boolean) => emit('columnVisibilityChange', col.fieldAlias, val)"
              >
                {{ col.title || col.fieldAlias }}
              </el-checkbox>
              <el-button
                v-if="col.sortable"
                size="small"
                link
                type="primary"
                :class="{ 'is-active': currentSortField === col.fieldName }"
                @click="emit('sortChange', col.fieldName)"
              >
                {{ getSortLabel(col.fieldName) }}
              </el-button>
            </div>
          </div>
          <div class="yzh-toolbar__column-settings-footer">
            <el-button :size="size" @click="emit('columnReset')">重置</el-button>
            <el-button :size="size" type="primary" @click="emit('columnApply')">确定</el-button>
          </div>
        </div>
      </el-popover>

      <!-- 右侧自定义插槽 -->
      <slot name="right" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { Setting } from '@element-plus/icons-vue'
import type { IButtonInstance } from '../types/YZHV3Config'

// ====== 类型定义 ======
export interface YzhButtonConfig {
  key: string
  label: string
  icon?: any
  type?: '' | 'default' | 'primary' | 'success' | 'warning' | 'danger' | 'info'
  visible?: boolean
  disabled?: boolean
  loading?: boolean
}

export interface YzhColumnItem {
  fieldAlias: string
  fieldName: string
  title: string
  hidden: boolean
  sortable?: boolean
}

// ====== Props ======
const props = withDefaults(defineProps<{
  /** 按钮配置列表 */
  buttons: (YzhButtonConfig | string)[]
  /** 按钮尺寸 */
  size?: 'large' | 'default' | 'small'
  /** 是否显示列设置 */
  showColumnSetting?: boolean
  /** 列设置面板的列数据 */
  columnList?: YzhColumnItem[]
  /** 当前排序字段 */
  currentSortField?: string
  /** 当前排序方向 */
  currentSortOrder?: 'asc' | 'desc'
}>(), {
  size: 'small',
  showColumnSetting: true,
  columnList: () => [],
  currentSortField: '',
  currentSortOrder: 'desc',
})

// ====== Emits ======
const emit = defineEmits<{
  (e: 'buttonClick', key: string): void
  (e: 'columnVisibilityChange', fieldAlias: string, visible: boolean): void
  (e: 'sortChange', fieldName: string): void
  (e: 'columnReset'): void
  (e: 'columnApply'): void
}>()

// ====== 解析按钮配置（支持简写字符串和完整对象）=====
const resolvedButtons = computed<YzhButtonConfig[]>(() => {
  return props.buttons
    .map(btn => {
      if (typeof btn === 'string') {
        return resolvePresetButton(btn)
      }
      return btn
    })
    .filter(btn => btn.visible !== false)
})

/**
 * 解析预定义按钮（字符串简写）
 * 内置按钮：add, edit, delete, refresh, batchDelete, export, import, save, cancel
 */
function resolvePresetButton(key: string): YzhButtonConfig {
  const presetMap: Record<string, Omit<YzhButtonConfig, 'key'>> = {
    add: { label: '新增', type: 'primary', icon: 'Plus' },
    edit: { label: '修改', type: 'primary', icon: 'Edit' },
    delete: { label: '删除', type: 'danger', icon: 'Delete' },
    refresh: { label: '刷新', type: 'default', icon: 'Refresh' },
    batchDelete: { label: '批量删除', type: 'danger', icon: 'Delete' },
    export: { label: '导出', type: 'success', icon: 'Download' },
    import: { label: '导入', type: 'warning', icon: 'Upload' },
    save: { label: '保存', type: 'primary', icon: 'Check' },
    cancel: { label: '取消', type: 'default', icon: 'Close' },
    search: { label: '查询', type: 'primary', icon: 'Search' },
    reset: { label: '重置', type: 'default', icon: 'RefreshRight' },
  }

  const preset = presetMap[key] || { label: key, type: 'default' }
  return { key, ...preset }
}

/** 处理按钮点击 */
function handleButtonClick(btn: YzhButtonConfig) {
  emit('buttonClick', btn.key)
}

/** 获取排序标签文字 */
function getSortLabel(fieldName: string): string {
  if (props.currentSortField !== fieldName) return '排序'
  return props.currentSortOrder === 'asc' ? '↑ 升序' : '↓ 降序'
}
</script>

<style scoped lang="scss">
.yzh-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
  padding: 8px 0;
  margin-bottom: 8px;

  &__left {
    display: flex;
    gap: 6px;
    align-items: center;
    flex-wrap: wrap;
  }

  &__right {
    display: flex;
    align-items: center;
    flex-shrink: 0;
    margin-left: auto;
    gap: 6px;
  }

  // 列设置面板样式
  &__column-settings {
    &__header {
      font-size: 14px;
      font-weight: 600;
      color: #303133;
      padding-bottom: 8px;
      border-bottom: 1px solid #ebeef5;
      margin-bottom: 8px;
    }

    &__body {
      max-height: 300px;
      overflow-y: auto;
    }

    &__item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 4px 0;

      .el-checkbox {
        flex: 1;
        min-width: 0;
      }

      .is-active {
        color: var(--el-color-primary);
        font-weight: 600;
      }
    }

    &__footer {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
      padding-top: 10px;
      margin-top: 8px;
      border-top: 1px solid #ebeef5;
    }
  }
}
</style>
