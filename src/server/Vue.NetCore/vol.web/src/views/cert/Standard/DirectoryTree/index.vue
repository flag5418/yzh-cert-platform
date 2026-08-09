<template>
  <div class="app-container">
    <el-alert
      title="标准目录树管理：查看和管理标准目录的文件夹结构"
      type="info"
      :closable="false"
      show-icon
      style="margin-bottom: 10px"
    />

    <el-row :gutter="20">
      <el-col :span="8">
        <el-card shadow="never">
          <template #header>
            <div class="card-header">
              <span>目录配置列表</span>
            </div>
          </template>
          <el-tree
            :data="configList"
            :props="{ label: 'directoryCode', children: 'children' }"
            node-key="directoryCode"
            @node-click="handleNodeClick"
          />
        </el-card>
      </el-col>

      <el-col :span="16">
        <el-card shadow="never">
          <template #header>
            <div class="card-header">
              <span>文件夹结构</span>
              <el-button type="primary" size="small" @click="handleAddFolder">
                <i class="el-icon-plus"></i> 新建文件夹
              </el-button>
            </div>
          </template>
          <el-table :data="folderTree" v-loading="loading" border stripe row-key="folderCode">
            <el-table-column prop="folderName" label="文件夹名称" />
            <el-table-column prop="depth" label="层级" width="80" />
            <el-table-column prop="sortOrder" label="排序" width="80" />
            <el-table-column label="操作" width="150">
              <template #default="{ row }">
                <el-button size="small" @click="handleEdit(row)">编辑</el-button>
                <el-button size="small" type="danger" @click="handleDelete(row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import http from '@/api/http'

const loading = ref(false)
const configList = ref([])
const folderTree = ref([])
const currentConfig = ref(null)

// 加载配置列表
const loadConfigs = async () => {
  try {
    const res = await http.post('/api/standard-directory/configs', { page: 1, rows: 100 })
    if (res.status === 0) {
      configList.value = res.data?.rows || []
    }
  } catch (error) {
    ElMessage.error('加载配置失败')
  }
}

// 加载文件夹树
const loadFolders = async (directoryCode) => {
  loading.value = true
  try {
    const res = await http.get(`/api/standard-directory/configs/${directoryCode}/folders`)
    if (res.status === 0) {
      folderTree.value = res.data || []
    }
  } catch (error) {
    ElMessage.error('加载文件夹失败')
  } finally {
    loading.value = false
  }
}

// 点击配置节点
const handleNodeClick = (data) => {
  currentConfig.value = data
  loadFolders(data.directoryCode)
}

// 新建文件夹
const handleAddFolder = () => {
  ElMessage.info('新建文件夹功能开发中...')
}

// 编辑文件夹
const handleEdit = (row) => {
  ElMessage.info('编辑文件夹功能开发中...')
}

// 删除文件夹
const handleDelete = async (row) => {
  try {
    await ElMessageBox.confirm('确定要删除此文件夹吗？', '提示', { type: 'warning' })
    await http.delete(`/api/standard-directory/folders/${row.folderCode}`)
    ElMessage.success('删除成功')
    if (currentConfig.value) {
      loadFolders(currentConfig.value.directoryCode)
    }
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error('删除失败')
    }
  }
}

onMounted(() => {
  loadConfigs()
})
</script>

<style scoped>
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
</style>
