import { ref } from 'vue'
import { getMenuTree, addMenu, updateMenu, deleteMenu, toggleEnable } from '@/api/system/menu'
import type { SysMenu } from '@/api/system/menu'
import { ElMessage, ElMessageBox } from 'element-plus'

export function useMenuLogic() {
  const tableData = ref<SysMenu[]>([])
  const loading = ref(false)
  const selectedRows = ref<SysMenu[]>([])

  const dialogVisible = ref(false)
  const dialogTitle = ref('')
  const isEdit = ref(false)
  const formData = ref<Partial<SysMenu>>({})
  const submitting = ref(false)

  async function loadData() {
    loading.value = true
    try {
      const res = await getMenuTree()
      if (res.code === 200) {
        tableData.value = res.data ?? []
      } else {
        ElMessage.error(res.message || '加载菜单失败')
      }
    } catch (e: any) {
      ElMessage.error(e.message || '加载菜单失败')
    } finally {
      loading.value = false
    }
  }

  function handleAddRoot() {
    isEdit.value = false
    dialogTitle.value = '新增根菜单'
    formData.value = {
      menuName: '',
      parentCode: '0',
      url: '',
      icon: '',
      description: '',
      enable: 1,
      orderNo: 0
    }
    dialogVisible.value = true
  }

  function handleAddChild(row: SysMenu) {
    isEdit.value = false
    dialogTitle.value = `新增子菜单 - ${row.menuName}`
    formData.value = {
      menuName: '',
      parentCode: row.code ?? '0',
      url: '',
      icon: '',
      description: '',
      enable: 1,
      orderNo: 0
    }
    dialogVisible.value = true
  }

  function handleEdit(row: SysMenu) {
    isEdit.value = true
    dialogTitle.value = '修改菜单'
    formData.value = { ...row }
    dialogVisible.value = true
  }

  async function handleDelete(row: SysMenu) {
    try {
      await ElMessageBox.confirm(
        `确定删除菜单「${row.menuName}」？`,
        '提示',
        { type: 'warning' }
      )
      const res = await deleteMenu([row.code!])
      if (res.code === 200) {
        ElMessage.success('删除成功')
        await loadData()
      } else {
        ElMessage.error(res.message || '删除失败')
      }
    } catch (e: any) {
      if (e !== 'cancel') {
        ElMessage.error(e.message || '删除失败')
      }
    }
  }

  async function handleBatchDelete() {
    if (!selectedRows.value.length) return
    try {
      await ElMessageBox.confirm(
        `确定删除选中的 ${selectedRows.value.length} 个菜单？`,
        '提示',
        { type: 'warning' }
      )
      const codes = selectedRows.value.map(r => r.code!).filter(Boolean)
      const res = await deleteMenu(codes)
      if (res.code === 200) {
        ElMessage.success('批量删除成功')
        selectedRows.value = []
        await loadData()
      } else {
        ElMessage.error(res.message || '批量删除失败')
      }
    } catch (e: any) {
      if (e !== 'cancel') {
        ElMessage.error(e.message || '批量删除失败')
      }
    }
  }

  async function handleToggleEnable(row: SysMenu) {
    const newEnable = row.enable === 1 ? 0 : 1
    const action = newEnable === 1 ? '启用' : '禁用'
    try {
      const res = await toggleEnable(row.code!, newEnable)
      if (res.code === 200) {
        ElMessage.success(`已${action}`)
        await loadData()
      } else {
        ElMessage.error(res.message || `${action}失败`)
      }
    } catch (e: any) {
      ElMessage.error(e.message || `${action}失败`)
    }
  }

  function handleClose() {
    dialogVisible.value = false
  }

  async function handleSubmit() {
    if (!formData.value.menuName) {
      ElMessage.warning('请输入菜单名称')
      return
    }
    submitting.value = true
    try {
      const res = isEdit.value
        ? await updateMenu(formData.value)
        : await addMenu(formData.value)
      if (res.code === 200) {
        ElMessage.success(isEdit.value ? '修改成功' : '新增成功')
        dialogVisible.value = false
        await loadData()
      } else {
        ElMessage.error(res.message || '操作失败')
      }
    } catch (e: any) {
      ElMessage.error(e.message || '操作失败')
    } finally {
      submitting.value = false
    }
  }

  return {
    tableData,
    loading,
    selectedRows,
    dialogVisible,
    dialogTitle,
    isEdit,
    formData,
    submitting,
    loadData,
    handleAddRoot,
    handleAddChild,
    handleEdit,
    handleDelete,
    handleBatchDelete,
    handleToggleEnable,
    handleClose,
    handleSubmit
  }
}
