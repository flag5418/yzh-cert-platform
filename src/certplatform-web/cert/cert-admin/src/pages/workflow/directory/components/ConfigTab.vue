<script setup lang="ts">
/**
 * ConfigTab —— 目录配置管理（未选中阶段时显示）
 *
 * 移植自历史项目 DirectoryManager/components/ConfigTab.vue：
 *   目录配置（cert_standard_directory_config）的列表 / 新建 / 编辑 / 删除。
 *   对应新架构接口：/api/Workflow/StandardDirectory/configs*
 */
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, FolderOpened } from '@element-plus/icons-vue'
import {
  getDirectoryConfigs,
  createDirectoryConfig,
  updateDirectoryConfig,
  deleteDirectoryConfig,
} from '@share/composables/useDirectoryApi'
import type { StandardDirectoryConfig } from '@share/types'

const loading = ref(false)
const tableData = ref<StandardDirectoryConfig[]>([])
const dialogVisible = ref(false)

const form = reactive<{
  directoryCode: string
  orgCode: string
  standardCode: string
  phaseCode: string
  rootFolderName: string
  status: string
}>({
  directoryCode: '',
  orgCode: '',
  standardCode: '',
  phaseCode: '',
  rootFolderName: '',
  status: 'active',
})

async function loadConfigs() {
  loading.value = true
  try {
    tableData.value = (await getDirectoryConfigs()) || []
  } catch (e: any) {
    ElMessage.error('加载目录配置失败：' + (e?.message || ''))
    tableData.value = []
  } finally {
    loading.value = false
  }
}

function resetForm() {
  form.directoryCode = ''
  form.orgCode = ''
  form.standardCode = ''
  form.phaseCode = ''
  form.rootFolderName = ''
  form.status = 'active'
}

function handleAdd() {
  resetForm()
  dialogVisible.value = true
}

function handleEdit(row: StandardDirectoryConfig) {
  form.directoryCode = row.DirectoryCode || ''
  form.orgCode = row.OrgCode || ''
  form.standardCode = row.StandardCode || ''
  form.phaseCode = row.PhaseCode || ''
  form.rootFolderName = row.RootFolderName || ''
  form.status = row.Status || 'active'
  dialogVisible.value = true
}

async function handleSubmit() {
  if (!form.directoryCode.trim()) {
    ElMessage.warning('请输入目录编码（SDC-标准|阶段）')
    return
  }
  try {
    const payload = {
      DirectoryCode: form.directoryCode,
      OrgCode: form.orgCode || undefined,
      StandardCode: form.standardCode,
      PhaseCode: form.phaseCode,
      RootFolderName: form.rootFolderName,
      Status: form.status,
    }
    const res =
      form.directoryCode && tableData.value.some((r) => r.DirectoryCode === form.directoryCode)
        ? await updateDirectoryConfig(form.directoryCode, payload)
        : await createDirectoryConfig(payload)
    if (res?.code !== 200) throw new Error(res?.msg || res?.message || '保存失败')
    ElMessage.success('保存成功')
    dialogVisible.value = false
    loadConfigs()
  } catch (e: any) {
    ElMessage.error('保存失败：' + (e?.message || ''))
  }
}

async function handleDelete(row: StandardDirectoryConfig) {
  const code = row.DirectoryCode
  try {
    await ElMessageBox.confirm(
      `确定删除目录配置「${code}」吗？该目录下的文件夹与文件将一并不可管理。`,
      '删除确认',
      { type: 'warning' }
    )
  } catch {
    return
  }
  try {
    const res = await deleteDirectoryConfig(code)
    if (res?.code !== 200) throw new Error(res?.msg || res?.message || '删除失败')
    ElMessage.success('删除成功')
    loadConfigs()
  } catch (e: any) {
    ElMessage.error('删除失败：' + (e?.message || ''))
  }
}

onMounted(loadConfigs)
</script>

<template>
  <div class="config-tab">
    <el-card shadow="never" class="config-card">
      <template #header>
        <div class="card-header">
          <span class="card-title">目录配置管理</span>
          <el-button type="primary" size="small" :icon="Plus" @click="handleAdd">新建配置</el-button>
        </div>
      </template>

      <div class="hint">
        <el-icon><FolderOpened /></el-icon>
        <span>请选择左侧阶段管理该阶段的认证材料；此处维护「机构 + 标准 + 阶段」的目录配置。</span>
      </div>

      <el-table :data="tableData" v-loading="loading" border stripe size="small">
        <el-table-column prop="DirectoryCode" label="目录编码" min-width="240" show-overflow-tooltip />
        <el-table-column prop="StandardCode" label="标准编码" width="150" />
        <el-table-column prop="PhaseCode" label="阶段编码" width="120" />
        <el-table-column prop="RootFolderName" label="根文件夹名" width="180" />
        <el-table-column label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="row.Status === 'active' ? 'success' : 'info'" size="small">
              {{ row.Status === 'active' ? '启用' : '草稿' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="创建时间" width="180">
          <template #default="{ row }">
            {{ row.CreateDate ? new Date(row.CreateDate).toLocaleString('zh-CN') : '--' }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="150">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="handleEdit(row)">编辑</el-button>
            <el-button link type="danger" size="small" @click="handleDelete(row)">删除</el-button>
          </template>
        </el-table-column>
        <template #empty>
          <el-empty description="暂无目录配置" :image-size="80" />
        </template>
      </el-table>
    </el-card>

    <el-dialog
      v-model="dialogVisible"
      :title="form.directoryCode ? '编辑目录配置' : '新建目录配置'"
      width="520px"
      destroy-on-close
    >
      <el-form :model="form" label-width="100px">
        <el-form-item label="目录编码">
          <el-input v-model="form.directoryCode" placeholder="如：SDC-ISO134852016|STAGE01" />
        </el-form-item>
        <el-form-item label="机构编码">
          <el-input v-model="form.orgCode" placeholder="可选" />
        </el-form-item>
        <el-form-item label="标准编码">
          <el-input v-model="form.standardCode" placeholder="如：ISO134852016" />
        </el-form-item>
        <el-form-item label="阶段编码">
          <el-input v-model="form.phaseCode" placeholder="如：STAGE01" />
        </el-form-item>
        <el-form-item label="根文件夹名">
          <el-input v-model="form.rootFolderName" placeholder="如：企业基础资料" />
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="form.status">
            <el-radio value="active">启用</el-radio>
            <el-radio value="draft">草稿</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSubmit">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.config-tab {
  height: 100%;
  overflow: auto;
  padding: 16px;
  background: #f5f7fa;
}
.config-card {
  margin-bottom: 16px;
}
.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.card-title {
  font-weight: 600;
  font-size: 14px;
  color: #303133;
}
.hint {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
  padding: 8px 12px;
  background: #ecf5ff;
  border-radius: 4px;
  font-size: 13px;
  color: #409eff;
}
</style>
