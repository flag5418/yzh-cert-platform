<script setup lang="ts">
/**
 * 系统菜单管理 - V4 标准布局（树形）
 *
 * 结构：
 * - 标题栏
 * - 控制栏（新增/刷新）
 * - 树形表格
 */
import { Delete, Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { onMounted, reactive, ref } from 'vue'
import { deleteMenu, getMenuTree, saveMenu, type SysMenu } from '@/yzh/api/system-menu'
import { YzhForm, type YzhFormFieldV4 } from '@/yzh/components/form'
import YzhPageLayout from '@/yzh/components/layout/YzhPageLayout.vue'

const loading = ref(false)
const treeData = ref<SysMenu[]>([])
const selectedRow = ref<SysMenu | null>(null)

const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const formData = reactive<Partial<SysMenu>>({})
const formRef = ref()
const submitting = ref(false)

/** 扁平菜单项（带层级信息） */
interface MenuNode extends SysMenu {
  children?: MenuNode[]
  level: number
}

/** 将扁平菜单列表转为树，并标注层级 */
function buildTree(items: SysMenu[]): MenuNode[] {
  const map = new Map<number, MenuNode>()
  items.forEach((item) => {
    map.set(item.id, { ...item, children: [], level: 0 })
  })
  const roots: MenuNode[] = []
  for (const item of map.values()) {
    if (item.parentId && map.has(item.parentId)) {
      const parent = map.get(item.parentId)!
      item.level = parent.level + 1
      parent.children!.push(item)
    } else {
      roots.push(item)
    }
  }
  return roots
}

async function loadTree() {
  loading.value = true
  try {
    const data = await getMenuTree()
    treeData.value = buildTree(Array.isArray(data) ? data : [])
  } catch (e: any) {
    ElMessage.error(e?.message || '加载菜单失败')
    treeData.value = []
  } finally {
    loading.value = false
  }
}

function onAdd() {
  dialogMode.value = 'add'
  Object.assign(formData, {
    id: undefined,
    name: '',
    parentId: selectedRow.value?.id ?? null,
    url: '',
    icon: '',
    enable: 1,
    tableName: '',
    permission: []
  })
  dialogVisible.value = true
}

function onEdit(row: SysMenu) {
  dialogMode.value = 'edit'
  Object.assign(formData, { ...row })
  dialogVisible.value = true
}

async function onDelete(row: SysMenu) {
  try {
    await ElMessageBox.confirm(`确定删除菜单「${row.name}」吗？`, '删除确认', { type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteMenu(row.id)
    ElMessage.success('删除成功')
    loadTree()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

async function onSubmit() {
  const valid = await formRef.value?.validate()
  if (!valid) return
  submitting.value = true
  try {
    await saveMenu(formData as SysMenu)
    ElMessage.success(dialogMode.value === 'add' ? '新增成功' : '保存成功')
    dialogVisible.value = false
    loadTree()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    submitting.value = false
  }
}

const menuTypeLabel = (url?: string | null) => {
  if (!url) return '目录'
  return '菜单'
}

const formFields: YzhFormFieldV4[] = [
  { prop: 'name', label: '菜单名称', type: 'text', required: true, span: 12 },
  { prop: 'parentId', label: '父级菜单', type: 'number', span: 12 },
  { prop: 'url', label: '菜单路径', type: 'text', span: 12 },
  { prop: 'icon', label: '图标', type: 'text', span: 12 },
  { prop: 'tableName', label: '关联表名', type: 'text', span: 12 },
  {
    prop: 'enable',
    label: '状态',
    type: 'switch',
    span: 12,
    fieldProps: { activeText: '启用', inactiveText: '禁用' }
  }
]

onMounted(loadTree)
</script>

<template>
  <YzhPageLayout pageTitle="菜单管理" helpText="管理系统菜单，支持树形层级关系">
    <!-- 控制栏 -->
    <template #toolbar-left>
      <el-button type="primary" :icon="Plus" @click="onAdd">新增菜单</el-button>
    </template>
    <template #toolbar-right>
      <el-button :icon="Refresh" @click="loadTree">刷新</el-button>
    </template>

    <!-- 树形表格 -->
    <div v-loading="loading" class="menu-tree-container">
      <el-table
        :data="treeData"
        row-key="id"
        :tree-props="{ children: 'children' }"
        border
        default-expand-all
        @row-click="(row: SysMenu) => (selectedRow = row)"
      >
        <!-- 菜单名称（带缩进） -->
        <el-table-column label="菜单名称" min-width="220">
          <template #default="{ row }">
            <span :style="{ paddingLeft: (row.level || 0) * 24 + 'px' }">
              {{ row.name }}
            </span>
          </template>
        </el-table-column>

        <!-- 类型 -->
        <el-table-column label="类型" width="80" align="center">
          <template #default="{ row }">
            <el-tag :type="menuTypeLabel(row.url) === '目录' ? 'info' : 'primary'">
              {{ menuTypeLabel(row.url) }}
            </el-tag>
          </template>
        </el-table-column>

        <!-- 路径 -->
        <el-table-column prop="url" label="路径" min-width="200" />

        <!-- 图标 -->
        <el-table-column prop="icon" label="图标" width="100" />

        <!-- 状态 -->
        <el-table-column prop="enable" label="状态" width="80" align="center">
          <template #default="{ row }">
            <el-tag :type="row.enable === 1 ? 'success' : 'info'">
              {{ row.enable === 1 ? '启用' : '禁用' }}
            </el-tag>
          </template>
        </el-table-column>

        <!-- 权限 -->
        <el-table-column prop="permission" label="权限" min-width="180">
          <template #default="{ row }">
            <span v-if="row.permission?.length" style="font-size: 12px; color: #666">
              {{ row.permission.join(', ') }}
            </span>
            <span v-else style="color: #ccc">-</span>
          </template>
        </el-table-column>

        <!-- 操作 -->
        <el-table-column label="操作" width="160" fixed="right">
          <template #default="{ row }">
            <el-button text type="primary" :icon="Edit" @click.stop="onEdit(row)">编辑</el-button>
            <el-button text type="danger" :icon="Delete" @click.stop="onDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 编辑对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="dialogMode === 'add' ? '新增菜单' : '编辑菜单'"
      width="720px"
      align-center
      destroy-on-close
      :close-on-click-modal="false"
    >
      <YzhForm
        ref="formRef"
        v-model="formData"
        :fields="formFields"
        :loading="submitting"
        :cols="2"
        @submit="onSubmit"
        @reset="dialogVisible = false"
      />
    </el-dialog>
  </YzhPageLayout>
</template>

<style scoped>
.menu-tree-container {
  background: #fff;
  border-radius: 4px;
  overflow: hidden;
}
</style>
