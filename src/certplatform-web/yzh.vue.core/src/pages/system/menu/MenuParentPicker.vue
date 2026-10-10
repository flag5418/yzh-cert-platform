<script setup lang="ts">
/**
 * MenuParentPicker - 菜单上级选择器（菜单管理 · ParentCode 编辑控件）
 *
 * 展示当前上级菜单名称 + 「选择」按钮弹出树选择对话框。
 * 支持「设为根级」清除上级。
 */
import { onMounted, ref, watch } from 'vue'
import { ElDialog, ElTree, ElButton, ElMessage } from 'element-plus'
import { getAllMenuTree, type SysMenu } from '@yzh-core/api/system/menu'

const props = defineProps<{
  /** 当前选中的父菜单 Code（"0" / "" 表示根级） */
  modelValue: string
  /** 当前正在编辑的菜单 Code（排除自身，防环） */
  excludeCode?: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
}>()

// ─── 菜单树 ───
const menuTree = ref<SysMenu[]>([])
const loading = ref(false)

async function loadMenuTree() {
  loading.value = true
  try {
    const res = await getAllMenuTree()
    if (res.success && Array.isArray(res.data)) {
      menuTree.value = res.data
    }
  } catch {
    ElMessage.error('加载菜单树失败')
  } finally {
    loading.value = false
  }
}

onMounted(() => loadMenuTree())

// ─── 当前父菜单名称 ───
const parentName = ref('根级')

function resolveParentName(code: string) {
  if (!code || code === '0') return '根级'
  const find = (nodes: SysMenu[]): string | undefined => {
    for (const n of nodes) {
      if (n.code === code) return n.menuName
      if (n.children?.length) {
        const found = find(n.children)
        if (found) return found
      }
    }
    return undefined
  }
  return find(menuTree.value) ?? code
}

watch(
  () => props.modelValue,
  (v) => {
    parentName.value = resolveParentName(v)
  },
  { immediate: true },
)

// 当菜单树加载完成后，重新解析父菜单名称
watch(menuTree, () => {
  parentName.value = resolveParentName(props.modelValue)
})

// ─── 选择对话框 ───
const dialogVisible = ref(false)
const selectedCode = ref<string>(props.modelValue)

function openDialog() {
  selectedCode.value = props.modelValue
  dialogVisible.value = true
  if (!menuTree.value.length) loadMenuTree()
}

function onNodeClick(data: SysMenu) {
  if (data.code === props.excludeCode) {
    ElMessage.warning('不能选择自身作为上级菜单')
    return
  }
  selectedCode.value = data.code
}

function confirm() {
  emit('update:modelValue', selectedCode.value)
  parentName.value = resolveParentName(selectedCode.value)
  dialogVisible.value = false
}

function setRoot() {
  selectedCode.value = '0'
  emit('update:modelValue', '0')
  parentName.value = '根级'
  dialogVisible.value = false
}
</script>

<template>
  <div class="menu-parent-picker">
    <span class="menu-parent-picker__name">{{ parentName }}</span>
    <ElButton size="small" @click="openDialog">选择</ElButton>
    <ElButton
      v-if="modelValue && modelValue !== '0'"
      size="small"
      type="info"
      @click="setRoot"
    >
      设为根级
    </ElButton>

    <ElDialog
      v-model="dialogVisible"
      title="选择上级菜单"
      width="420px"
      destroy-on-close
    >
      <div class="menu-parent-picker__tree-wrapper">
        <ElTree
          :data="menuTree"
          node-key="code"
          :current-node-key="selectedCode"
          :loading="loading"
          highlight-current
          default-expand-all
          @node-click="onNodeClick"
        >
          <template #default="{ data: node }">
            <span
              :class="{
                'menu-parent-picker__disabled': node.code === excludeCode,
              }"
            >
              {{ node.menuName }}
            </span>
          </template>
        </ElTree>
      </div>
      <template #footer>
        <ElButton @click="dialogVisible = false">取消</ElButton>
        <ElButton type="primary" @click="confirm">确定</ElButton>
      </template>
    </ElDialog>
  </div>
</template>

<style scoped>
.menu-parent-picker {
  display: flex;
  align-items: center;
  gap: 8px;
}

.menu-parent-picker__name {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-primary);
  font-weight: 500;
  min-width: 80px;
}

.menu-parent-picker__tree-wrapper {
  height: 300px;
  overflow: auto;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 4px;
}

.menu-parent-picker__disabled {
  color: var(--el-text-color-disabled);
  cursor: not-allowed;
}
</style>
