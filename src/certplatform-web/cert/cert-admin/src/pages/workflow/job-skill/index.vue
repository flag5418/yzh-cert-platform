<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { YzhPageLayout } from '@yzh-core'
import {
  getSkillPage,
  getSkill,
  saveSkill,
  deleteSkill,
  toggleSkillActive,
  analyzeSkill,
  getSkillCategories,
  saveSkillCategory,
  deleteSkillCategory,
  toggleSkillCategoryActive,
  type Skill,
  type SkillCategory,
  type AnalyzedSkill
} from '@share/api/workflow/job-skill'

const loading = ref(false)
const tableData = ref<Skill[]>([])
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const keyword = ref('')
const currentCategory = ref('')
const currentCategoryName = ref('')

// 分类
const categories = ref<SkillCategory[]>([])
const categoryDialogVisible = ref(false)

// 编辑
const dialogVisible = ref(false)
const editFormRef = ref()
const editForm = reactive<{
  id?: number
  skillCode: string
  skillName: string
  description?: string
  category: string
  isActive: boolean
  inputs: any[]
  outputs: any[]
  reflection: { classPath: string; methodName: string }
}>({
  skillCode: '',
  skillName: '',
  category: 'data_access',
  isActive: true,
  inputs: [],
  outputs: [],
  reflection: { classPath: '', methodName: 'ExecuteAsync' }
})

// 反射分析
const analyzing = ref(false)
const analyzed = ref<AnalyzedSkill | null>(null)
const analyzeError = ref('')

const typeMap: Record<string, string> = {
  boolean: 'success',
  string: '',
  number: 'warning',
  date: 'info',
  json: 'danger'
}

const bindModeMap: Record<string, string> = {
  Link: '仅连线',
  LinkOrConstant: '连线/常量',
  Enum: '字典选择'
}

const bindModeTagMap: Record<string, string> = {
  Link: 'primary',
  LinkOrConstant: 'warning',
  Enum: 'success'
}

async function loadCategories() {
  try {
    const res = await getSkillCategories()
    categories.value = res || []
  } catch (e: any) {
    ElMessage.error('加载分类失败')
  }
}

function getCategoryName(code: string) {
  return categories.value.find(c => c.categoryCode === code)?.categoryName || code
}

function selectCategory(code: string, name: string) {
  currentCategory.value = code
  currentCategoryName.value = name || ''
  page.value = 1
  loadData()
}

async function loadData() {
  loading.value = true
  try {
    const res = await getSkillPage({ page: page.value, rows: pageSize.value }, { keyword: keyword.value || null, category: currentCategory.value || null })
    tableData.value = res?.rows || []
    total.value = res?.total || 0
  } catch (e: any) {
    ElMessage.error('加载失败')
  } finally {
    loading.value = false
  }
}

function openEdit(row: Skill | null) {
  if (row) {
    editForm.id = row.id
    editForm.skillCode = row.skillCode
    editForm.skillName = row.skillName
    editForm.description = row.description
    editForm.category = row.category
    editForm.isActive = row.isActive
    editForm.inputs = row.inputs || []
    editForm.outputs = row.outputs || []
    editForm.reflection = row.reflection || { classPath: '', methodName: 'ExecuteAsync' }
    if (row.reflection?.classPath && (row.inputs?.length > 0 || row.outputs?.length > 0)) {
      analyzed.value = {
        code: row.skillCode,
        name: row.skillName,
        returnType: row.outputs?.find(o => o.outputName === 'result')?.outputType || 'json',
        description: row.description || '',
        inputPorts: (row.inputs || []).map(i => ({ ...i })),
        outputPorts: (row.outputs || []).map(o => ({ ...o }))
      }
    }
  } else {
    editForm.id = undefined
    editForm.skillCode = ''
    editForm.skillName = ''
    editForm.description = ''
    editForm.category = currentCategory.value || 'data_access'
    editForm.isActive = true
    editForm.inputs = []
    editForm.outputs = []
    editForm.reflection = { classPath: '', methodName: 'ExecuteAsync' }
    analyzed.value = null
    analyzeError.value = ''
  }
  dialogVisible.value = true
}

async function analyzeReflection() {
  if (!editForm.reflection.classPath) {
    ElMessage.warning('请先填写实现类全名')
    return
  }
  analyzing.value = true
  analyzed.value = null
  analyzeError.value = ''
  try {
    const res = await analyzeSkill({
      classPath: editForm.reflection.classPath,
      methodName: editForm.reflection.methodName || 'ExecuteAsync'
    })
    analyzed.value = res
    ElMessage.success('反射验证通过')
  } catch (e: any) {
    analyzeError.value = e?.message || '反射验证失败'
    ElMessage.error(analyzeError.value)
  } finally {
    analyzing.value = false
  }
}

async function handleSave() {
  if (!analyzed.value) {
    ElMessage.warning('请先验证反射信息')
    return
  }
  try {
    const skillData: Skill = {
      id: editForm.id,
      skillCode: editForm.skillCode,
      skillName: editForm.skillName,
      description: editForm.description,
      category: editForm.category,
      isActive: editForm.isActive,
      inputs: editForm.inputs,
      outputs: editForm.outputs,
      reflection: editForm.reflection
    }
    await saveSkill(skillData)
    ElMessage.success('保存成功')
    dialogVisible.value = false
    loadData()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  }
}

async function handleDelete(row: Skill) {
  try {
    await ElMessageBox.confirm(`确认删除 Skill「${row.skillName}」？`, '确认', { type: 'warning' })
  } catch { return }
  try {
    await deleteSkill(row.id!)
    ElMessage.success('删除成功')
    loadData()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

async function toggleActive(row: Skill) {
  try {
    await toggleSkillActive(row.id!)
    ElMessage.success('操作成功')
    loadData()
  } catch (e: any) {
    ElMessage.error(e?.message || '操作失败')
  }
}

function addCategory() {
  categories.value.push({ id: 0, categoryCode: '', categoryName: '', icon: '', color: '#409EFF', sortOrder: categories.value.length + 1, enable: true })
}

async function saveCategory(row: SkillCategory) {
  if (!row.categoryCode || !row.categoryName) {
    ElMessage.warning('分类编码与名称必填')
    return
  }
  try {
    await saveSkillCategory(row)
    ElMessage.success('保存成功')
    loadCategories()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  }
}

async function deleteCategory(row: SkillCategory) {
  if (!row.id) {
    categories.value = categories.value.filter(c => c !== row)
    return
  }
  try {
    await ElMessageBox.confirm(`确认删除分类「${row.categoryName}」？`, '确认', { type: 'warning' })
    await deleteSkillCategory(row.id)
    ElMessage.success('删除成功')
    loadCategories()
  } catch (e: any) {
    if (e !== 'cancel') ElMessage.error(e?.message || '删除失败')
  }
}

function getTypeTagType(type: string) {
  return typeMap[type] || ''
}

function getBindModeLabel(mode: string) {
  return bindModeMap[mode] || mode || '连线/常量'
}

function getBindModeTagType(mode: string) {
  return bindModeTagMap[mode] || ''
}

onMounted(() => { loadCategories(); loadData() })
</script>

<template>
  <YzhPageLayout title="Skill 管理">
    <template #toolbar>
      <el-button @click="categoryDialogVisible = true">
        <el-icon><Setting /></el-icon> 管理分类
      </el-button>
    </template>

    <!-- 左侧分类 -->
    <el-card shadow="never" class="category-card">
      <template #header>
        <div class="category-header">
          <span>Skill 分类</span>
        </div>
      </template>
      <div class="category-list">
        <div class="category-item" :class="{ active: currentCategory === '' }" @click="selectCategory('', '全部')">
          <span class="cat-dot" style="background: #909399"></span>
          全部
        </div>
        <div v-for="cat in categories" :key="cat.categoryCode" class="category-item" :class="{ active: currentCategory === cat.categoryCode }" @click="selectCategory(cat.categoryCode, cat.categoryName)">
          <span class="cat-dot" :style="{ background: cat.color || '#409EFF' }"></span>
          {{ cat.categoryName }}
        </div>
      </div>
    </el-card>

    <!-- 右侧列表 -->
    <el-card shadow="never" class="table-card">
      <template #header>
        <div class="card-header">
          <span class="card-title">
            Skill 列表
            <el-tag v-if="currentCategory" size="small" style="margin-left: 8px">{{ currentCategoryName }}</el-tag>
          </span>
          <div class="card-actions">
            <el-input v-model="keyword" placeholder="按编码/名称搜索" clearable style="width: 220px; margin-right: 8px" @keyup.enter="loadData" @clear="loadData" />
            <el-button type="primary" @click="loadData">查询</el-button>
            <el-button type="primary" @click="openEdit(null)">
              <el-icon><Plus /></el-icon> 新建 Skill
            </el-button>
          </div>
        </div>
      </template>

      <el-table :data="tableData" stripe border v-loading="loading">
        <el-table-column prop="skillCode" label="编码" width="160" />
        <el-table-column prop="skillName" label="名称" width="160" show-overflow-tooltip />
        <el-table-column label="分类" width="120" align="center">
          <template #default="{ row }">
            <el-tag size="small">{{ getCategoryName(row.category) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="description" label="说明" min-width="200" show-overflow-tooltip />
        <el-table-column label="启用" width="80" align="center">
          <template #default="{ row }">
            <el-switch :model-value="row.isActive" @change="toggleActive(row)" />
          </template>
        </el-table-column>
        <el-table-column label="操作" width="140" fixed="right">
          <template #default="{ row }">
            <el-button type="primary" link size="small" @click="openEdit(row)">编辑</el-button>
            <el-button type="danger" link size="small" @click="handleDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>

      <el-pagination
        v-model:current-page="page"
        :page-size="pageSize"
        :total="total"
        layout="total, prev, pager, next"
        style="margin-top: 12px; justify-content: flex-end"
        @current-change="loadData"
      />
    </el-card>

    <!-- 编辑弹窗 -->
    <el-dialog v-model="dialogVisible" :title="editForm.id ? `编辑 Skill：${editForm.skillCode}` : '新建 Skill'" width="800px" destroy-on-close>
      <el-form ref="editFormRef" :model="editForm" label-width="110px">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="Skill 编码" prop="skillCode">
              <el-input v-model="editForm.skillCode" :disabled="!!editForm.id" placeholder="get_field" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="Skill 名称" prop="skillName">
              <el-input v-model="editForm.skillName" placeholder="值比较" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="功能分类">
              <el-select v-model="editForm.category" style="width: 100%">
                <el-option v-for="cat in categories" :key="cat.categoryCode" :label="cat.categoryName" :value="cat.categoryCode" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="说明">
              <el-input v-model="editForm.description" type="textarea" :rows="2" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="16">
            <el-form-item label="实现类全名" prop="reflection.classPath">
              <el-input v-model="editForm.reflection.classPath" placeholder="YZH.Core.Skills.GetFieldSkill" />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="方法名">
              <el-input v-model="editForm.reflection.methodName" placeholder="ExecuteAsync" />
            </el-form-item>
          </el-col>
        </el-row>
        <div style="text-align: center; margin-bottom: 16px">
          <el-button type="warning" @click="analyzeReflection" :loading="analyzing">
            <el-icon><Check /></el-icon> 验证反射
          </el-button>
        </div>
      </el-form>

      <!-- 反射分析结果 -->
      <div v-if="analyzed" class="port-section">
        <el-alert type="success" :closable="false" show-icon style="margin-bottom: 12px">
          <template #title>
            反射验证通过：{{ analyzed.name }}（{{ analyzed.code }}）| 返回类型：{{ analyzed.returnType }}
          </template>
        </el-alert>

        <div class="section-title">输入端口（反射提取，只读）</div>
        <el-table :data="analyzed.inputPorts" border size="small">
          <el-table-column label="端口名" min-width="140">
            <template #default="{ row }"><span class="port-name">{{ row.name }}</span></template>
          </el-table-column>
          <el-table-column label="类型" width="100" align="center">
            <template #default="{ row }"><el-tag size="small" :type="getTypeTagType(row.type)">{{ row.type }}</el-tag></template>
          </el-table-column>
          <el-table-column label="必填" width="60" align="center">
            <template #default="{ row }"><el-tag :type="row.required ? 'danger' : 'info'" size="small">{{ row.required ? '是' : '否' }}</el-tag></template>
          </el-table-column>
          <el-table-column label="绑定模式" width="120" align="center">
            <template #default="{ row }">
              <el-tag size="small" :type="getBindModeTagType(row.bindMode)">{{ getBindModeLabel(row.bindMode) }}</el-tag>
            </template>
          </el-table-column>
        </el-table>

        <div class="section-title" style="margin-top: 12px">输出端口</div>
        <el-table :data="analyzed.outputPorts" border size="small">
          <el-table-column label="端口名" min-width="140">
            <template #default="{ row }"><span class="port-name">{{ row.name }}</span></template>
          </el-table-column>
          <el-table-column label="类型" width="100" align="center">
            <template #default="{ row }"><el-tag size="small" :type="getTypeTagType(row.type)">{{ row.type }}</el-tag></template>
          </el-table-column>
          <el-table-column label="说明" min-width="200">
            <template #default="{ row }"><span class="code-desc">{{ row.description }}</span></template>
          </el-table-column>
        </el-table>
      </div>

      <div v-if="analyzeError" class="empty-tip">
        <el-alert type="error" :closable="false" show-icon :title="analyzeError" />
      </div>

      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave" :disabled="!analyzed">保存</el-button>
      </template>
    </el-dialog>

    <!-- 分类管理弹窗 -->
    <el-dialog v-model="categoryDialogVisible" title="Skill 分类管理" width="760px">
      <el-table :data="categories" border size="small">
        <el-table-column label="编码" width="140">
          <template #default="{ row }"><el-input v-model="row.categoryCode" size="small" /></template>
        </el-table-column>
        <el-table-column label="名称" width="130">
          <template #default="{ row }"><el-input v-model="row.categoryName" size="small" /></template>
        </el-table-column>
        <el-table-column label="颜色" width="90" align="center">
          <template #default="{ row }"><el-color-picker v-model="row.color" size="small" /></template>
        </el-table-column>
        <el-table-column label="排序" width="80">
          <template #default="{ row }"><el-input-number v-model="row.sortOrder" :min="0" size="small" controls-position="right" style="width: 100%" /></template>
        </el-table-column>
        <el-table-column label="操作" width="130" align="center">
          <template #default="{ row }">
            <div class="row-actions">
              <el-button type="primary" link size="small" @click="saveCategory(row)">保存</el-button>
              <el-button type="danger" link size="small" @click="deleteCategory(row)">删除</el-button>
            </div>
          </template>
        </el-table-column>
      </el-table>
      <el-button type="primary" plain size="small" style="margin-top: 8px" @click="addCategory">+ 新增分类</el-button>
    </el-dialog>
  </YzhPageLayout>
</template>

<style scoped>
.category-card { width: 200px; min-width: 200px; }
.category-list { overflow-y: auto; }
.category-item { display: flex; align-items: center; gap: 8px; padding: 8px 12px; margin-bottom: 4px; border-radius: 6px; cursor: pointer; font-size: 13px; }
.category-item:hover { background: #f5f7fa; }
.category-item.active { background: #ecf5ff; color: #409EFF; font-weight: 600; }
.cat-dot { width: 8px; height: 8px; border-radius: 50%; display: inline-block; }
.table-card { flex: 1; }
.card-header { display: flex; align-items: center; justify-content: space-between; }
.card-title { font-size: 15px; font-weight: 600; }
.card-actions { display: flex; align-items: center; }
.section-title { font-size: 13px; font-weight: 600; margin-bottom: 8px; }
.port-name { font-family: monospace; font-size: 13px; font-weight: 600; }
.code-desc { font-size: 12px; color: #909399; }
.row-actions { display: flex; gap: 4px; }
.empty-tip { padding: 24px 0; text-align: center; }
</style>
