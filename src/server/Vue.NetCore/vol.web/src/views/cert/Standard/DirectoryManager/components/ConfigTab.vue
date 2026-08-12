<template>
  <div class="config-tab">
    <!-- 无阶段选中时的提示信息 -->
    <div v-if="!hasActiveConfig" class="empty-hint">
      <el-icon class="hint-icon"><FolderOpened /></el-icon>
      <div class="hint-text">请选择左侧阶段，进行文件管理</div>
      <div class="hint-sub">点击左侧树中的阶段节点，即可查看和上传该阶段的认证材料</div>
    </div>

    <!-- 有配置时显示配置管理 -->
    <el-card v-else shadow="never" class="config-card">
      <template #header>
        <div class="card-header">
          <span>目录配置管理</span>
          <el-button type="primary" size="small" @click="handleAdd">
            <el-icon><Plus /></el-icon> 新建配置
          </el-button>
        </div>
      </template>

      <el-table :data="tableData" v-loading="loading" border stripe>
        <el-table-column prop="directoryCode" label="目录编码" width="220" />
        <el-table-column prop="standardCode" label="标准编码" width="150" />
        <el-table-column prop="phaseCode" label="阶段编码" width="120" />
        <el-table-column prop="rootFolderName" label="根文件夹名" width="200" />
        <el-table-column prop="status" label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="row.status === 'active' ? 'success' : 'info'" size="small">
              {{ row.status === 'active' ? '启用' : '草稿' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="createDate" label="创建时间" width="160" />
        <el-table-column label="操作" width="180">
          <template #default="{ row }">
            <el-button size="small" @click="handleEdit(row)">编辑</el-button>
            <el-button size="small" type="danger" @click="handleDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 编辑对话框 -->
    <el-dialog v-model="dialogVisible" :title="form.directoryCode ? '编辑目录配置' : '新建目录配置'" width="500px" destroy-on-close>
      <el-form :model="form" label-width="120px">
        <el-form-item label="目录编码" v-if="form.directoryCode">
          <el-input v-model="form.directoryCode" disabled />
        </el-form-item>
        <el-form-item label="标准编码">
          <el-input v-model="form.standardCode" placeholder="如：ISO9001" />
        </el-form-item>
        <el-form-item label="阶段编码">
          <el-input v-model="form.phaseCode" placeholder="如：STAGE01" />
        </el-form-item>
        <el-form-item label="根文件夹名">
          <el-input v-model="form.rootFolderName" placeholder="如：企业基础资料" />
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="form.status">
            <el-radio value="draft">草稿</el-radio>
            <el-radio value="active">启用</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSubmit" :loading="submitting">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { Plus, FolderOpened } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { onMounted, ref, computed } from 'vue'
import http from '@/api/http'

const loading = ref(false)
const submitting = ref(false)
const tableData = ref([])
const dialogVisible = ref(false)
const form = ref({})

// 计算是否有启用的配置
const hasActiveConfig = computed(() => tableData.value.some(r => r.status === 'active'))

const loadData = async () => {
  loading.value = true
  try {
    const res = await http.get('/api/standard-directory/configs')
    if (res.Status === true || res.status === 0) {
      tableData.value = res.Data?.rows || res.data?.rows || []
    } else {
      ElMessage.error(res.Message || '加载失败')
    }
  } catch (error) {
    console.error('[ConfigTab] 加载失败:', error)
  } finally {
    loading.value = false
  }
}

const handleAdd = () => {
  form.value = { status: 'draft' }
  dialogVisible.value = true
}

const handleEdit = (row) => {
  form.value = { ...row }
  dialogVisible.value = true
}

const handleDelete = async (row) => {
  try {
    await ElMessageBox.confirm('确定要删除此配置吗？', '提示', { type: 'warning' })
    await http.delete(`/api/standard-directory/configs/${row.directoryCode}`)
    ElMessage.success('删除成功')
    loadData()
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error('删除失败')
    }
  }
}

const handleSubmit = async () => {
  submitting.value = true
  try {
    if (form.value.directoryCode) {
      await http.put(`/api/standard-directory/configs/${form.value.directoryCode}`, form.value)
    } else {
      await http.post('/api/standard-directory/configs/create', form.value)
    }
    ElMessage.success('保存成功')
    dialogVisible.value = false
    loadData()
  } catch (error) {
    console.error('[ConfigTab] 保存失败:', error)
    ElMessage.error('保存失败')
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  loadData()
})
</script>

<style scoped>
.config-tab {
  padding: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 300px;
}

.empty-hint {
  text-align: center;
  color: #909399;
}

.hint-icon {
  font-size: 64px;
  color: #c0c4cc;
  margin-bottom: 16px;
}

.hint-text {
  font-size: 18px;
  color: #606266;
  font-weight: 500;
  margin-bottom: 8px;
}

.hint-sub {
  font-size: 13px;
  color: #909399;
}

.config-card {
  width: 100%;
  max-width: 900px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
</style>
