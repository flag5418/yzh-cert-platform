<template>
  <div
    class="app-container"
    style="
      padding: 16px 24px;
      box-sizing: border-box;
      width: 100%;
      min-height: 100%;
      background: #fff;
    "
  >
    <el-alert
      title="标准目录配置管理：为每个机构-标准-阶段组合定义文件目录结构"
      type="info"
      :closable="false"
      show-icon
      style="margin-bottom: 10px"
    />

    <el-card shadow="never">
      <template #header>
        <div class="card-header">
          <span>目录配置列表</span>
          <el-button type="primary" size="small" @click="handleAdd">
            <i class="el-icon-plus"></i> 新建配置
          </el-button>
        </div>
      </template>

      <el-table :data="tableData" v-loading="loading" border stripe>
        <el-table-column prop="directoryCode" label="目录编码" width="200" />
        <el-table-column prop="standardCode" label="标准编码" width="150" />
        <el-table-column prop="phaseCode" label="阶段编码" width="120" />
        <el-table-column prop="rootFolderName" label="根文件夹名" width="150" />
        <el-table-column prop="status" label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="row.status === 'active' ? 'success' : 'info'">
              {{ row.status === 'active' ? '启用' : '草稿' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="createDate" label="创建时间" width="160" />
        <el-table-column label="操作" width="200">
          <template #default="{ row }">
            <el-button size="small" @click="handleEdit(row)">编辑</el-button>
            <el-button size="small" type="danger" @click="handleDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 编辑对话框 -->
    <el-dialog v-model="dialogVisible" title="编辑目录配置" width="500px">
      <el-form :model="form" label-width="120px">
        <el-form-item label="标准编码">
          <el-input v-model="form.standardCode" placeholder="如：ISO-9001" />
        </el-form-item>
        <el-form-item label="阶段编码">
          <el-input v-model="form.phaseCode" placeholder="如：PH01" />
        </el-form-item>
        <el-form-item label="根文件夹名">
          <el-input v-model="form.rootFolderName" placeholder="如：企业基础资料" />
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="form.status">
            <el-radio label="draft">草稿</el-radio>
            <el-radio label="active">启用</el-radio>
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

<script setup>
import http from '@/api/http'
import { ElMessage, ElMessageBox } from 'element-plus'
import { onMounted, ref } from 'vue'

const loading = ref(false)
const tableData = ref([])
const dialogVisible = ref(false)
const form = ref({})

// 加载数据
const loadData = async () => {
  loading.value = true
  try {
    const res = await http.get('/api/standard-directory/configs')
    if (res.status === 0) {
      tableData.value = res.data?.rows || []
    }
  } catch (error) {
    ElMessage.error('加载失败')
  } finally {
    loading.value = false
  }
}

// 新建
const handleAdd = () => {
  form.value = { status: 'draft' }
  dialogVisible.value = true
}

// 编辑
const handleEdit = (row) => {
  form.value = { ...row }
  dialogVisible.value = true
}

// 删除
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

// 保存
const handleSubmit = async () => {
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
    ElMessage.error('保存失败')
  }
}

onMounted(() => {
  loadData()
})
</script>

<style scoped>
.app-container {
  padding: 16px 24px;
  box-sizing: border-box;
  height: 100%;
  width: 100%;
  background: #fff;
}
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
</style>
