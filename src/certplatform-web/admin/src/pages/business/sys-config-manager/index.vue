<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { YzhPageLayout } from '@yzh-core/components/layout'
import {
  getParamList,
  saveParam,
  deleteParam,
  type SysParam
} from '@share/api/system-param'

const loading = ref(false)
const saving = ref(false)
const tableData = ref<SysParam[]>([])
const editVisible = ref(false)
const editFormRef = ref()

const editForm = reactive<Partial<SysParam>>({
  paramCode: '',
  paramName: '',
  paramValue: '',
  paramType: 'string',
  description: ''
})

const editRules = {
  paramCode: [{ required: true, message: '请输入参数编码', trigger: 'blur' }],
  paramName: [{ required: true, message: '请输入参数名称', trigger: 'blur' }],
  paramType: [{ required: true, message: '请选择参数类型', trigger: 'change' }]
}

const typeTagMap: Record<string, string> = {
  string: 'primary',
  number: 'warning',
  boolean: 'success',
  json: 'danger'
}

async function loadData() {
  loading.value = true
  try {
    const res = await getParamList()
    tableData.value = res || []
  } catch (e: any) {
    ElMessage.error(e?.message || '加载失败')
  } finally {
    loading.value = false
  }
}

function openEdit(row: SysParam | null) {
  if (row) {
    Object.assign(editForm, { ...row })
  } else {
    Object.assign(editForm, {
      id: undefined,
      paramCode: '',
      paramName: '',
      paramValue: '',
      paramType: 'string',
      description: ''
    })
  }
  editVisible.value = true
}

async function doSave() {
  const valid = await editFormRef.value?.validate().catch(() => false)
  if (!valid) return
  saving.value = true
  try {
    await saveParam(editForm as SysParam)
    ElMessage.success('保存成功')
    editVisible.value = false
    loadData()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

async function doDelete(row: SysParam) {
  try {
    await ElMessageBox.confirm(`确定删除参数「${row.paramName}」？`, '确认删除', { type: 'warning' })
  } catch { return }
  try {
    await deleteParam(row.id!)
    ElMessage.success('删除成功')
    loadData()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

onMounted(loadData)
</script>

<template>
  <YzhPageLayout title="系统参数配置">
    <template #toolbar>
      <el-button type="primary" @click="openEdit(null)">
        <el-icon><Plus /></el-icon> 新建参数
      </el-button>
    </template>

    <el-card shadow="never">
      <el-table :data="tableData" stripe border v-loading="loading">
        <el-table-column prop="paramCode" label="参数编码" width="200" />
        <el-table-column prop="paramName" label="参数名称" width="180" />
        <el-table-column prop="paramType" label="类型" width="100" align="center">
          <template #default="{ row }">
            <el-tag :type="typeTagMap[row.paramType]" size="small">{{ row.paramType }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="paramValue" label="参数值" min-width="200" show-overflow-tooltip />
        <el-table-column prop="description" label="说明" min-width="200" show-overflow-tooltip />
        <el-table-column label="操作" width="150" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="openEdit(row)">编辑</el-button>
            <el-button link type="danger" size="small" @click="doDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="editVisible" :title="editForm.id ? '编辑参数' : '新建参数'" width="600px">
      <el-form ref="editFormRef" :model="editForm" :rules="editRules" label-width="100px">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="参数编码" prop="paramCode">
              <el-input v-model="editForm.paramCode" placeholder="如：AI_Qwen_Model" :disabled="!!editForm.id" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="参数名称" prop="paramName">
              <el-input v-model="editForm.paramName" placeholder="如：千问AI模型" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="参数类型" prop="paramType">
              <el-select v-model="editForm.paramType" style="width: 100%">
                <el-option label="字符串" value="string" />
                <el-option label="数字" value="number" />
                <el-option label="布尔" value="boolean" />
                <el-option label="JSON" value="json" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="参数值" prop="paramValue">
              <el-input v-model="editForm.paramValue" :type="editForm.paramType === 'json' ? 'textarea' : 'text'" :rows="editForm.paramType === 'json' ? 4 : 1" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="说明">
          <el-input v-model="editForm.description" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="editVisible = false">取消</el-button>
        <el-button type="primary" @click="doSave" :loading="saving">保存</el-button>
      </template>
    </el-dialog>
  </YzhPageLayout>
</template>

<style scoped>
</style>
