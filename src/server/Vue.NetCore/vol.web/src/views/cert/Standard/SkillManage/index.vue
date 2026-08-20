<template>
  <div class="skill-manage-page">
    <CertPageHeader title="Skill 管理" :icon="IconSetting" />

    <div class="page-body">
      <!-- ===== 左侧分类栏 ===== -->
      <el-card shadow="never" class="category-card">
        <template #header>
          <div class="category-header">
            <span>Skill 分类</span>
            <el-button link size="small" @click="openCategoryManage">
              <el-icon><IconSetting /></el-icon> 管理
            </el-button>
          </div>
        </template>
        <div class="category-list">
          <div
            class="category-item"
            :class="{ active: currentCategory === '' }"
            @click="selectCategory('', '全部')"
          >
            <span class="cat-dot" style="background: #909399"></span>
            全部
          </div>
          <div
            v-for="cat in categories"
            :key="cat.categoryCode"
            class="category-item"
            :class="{ active: currentCategory === cat.categoryCode }"
            @click="selectCategory(cat.categoryCode, cat.categoryName)"
          >
            <span class="cat-dot" :style="{ background: cat.color || '#409EFF' }"></span>
            {{ cat.categoryName }}
          </div>
        </div>
      </el-card>

      <!-- ===== 右侧列表 ===== -->
      <el-card shadow="never" class="table-card">
        <template #header>
          <div class="card-header">
            <span class="card-title">
              Skill 列表
              <el-tag v-if="currentCategory" size="small" style="margin-left: 8px">{{ currentCategoryName }}</el-tag>
            </span>
            <div class="card-actions">
              <el-input
                v-model="keyword"
                placeholder="按编码/名称搜索"
                clearable
                style="width: 220px; margin-right: 8px"
                @keyup.enter="loadData"
                @clear="loadData"
              />
              <el-button type="primary" @click="loadData">查询</el-button>
              <el-button type="primary" @click="openEdit(null)">
                <el-icon><IconAdd /></el-icon> 新建 Skill
              </el-button>
            </div>
          </div>
        </template>

        <div class="table-wrapper">
          <el-table :data="tableData" stripe border v-loading="loading" style="width: 100%" height="100%">
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
                <div class="row-actions">
                  <el-button type="primary" link size="small" @click="openEdit(row)">编辑</el-button>
                  <el-button type="danger" link size="small" @click="handleDelete(row)">删除</el-button>
                </div>
              </template>
            </el-table-column>
          </el-table>
        </div>
        <el-pagination
          v-model:current-page="page"
          :page-size="pageSize"
          :total="total"
          layout="total, prev, pager, next"
          style="margin-top: 12px; justify-content: flex-end; flex-shrink: 0"
          @current-change="loadData"
        />
      </el-card>
    </div>

    <!-- ============ Skill 编辑弹窗 ============ -->
    <el-dialog
      v-model="dialogVisible"
      :title="editForm.id ? `编辑 Skill：${editForm.skillCode}` : '新建 Skill'"
      width="800px"
      top="8vh"
      destroy-on-close
      :close-on-click-modal="false"
    >
      <div class="dialog-body">
        <!-- ===== 基本信息 ===== -->
        <el-form :model="editForm" label-width="110px" ref="baseFormRef" class="edit-form">
          <el-row :gutter="16">
            <el-col :span="12">
              <el-form-item label="Skill 编码" prop="skillCode" :rules="[{ required: true, message: '请输入 Skill 编码' }]">
                <el-input v-model="editForm.skillCode" :disabled="!!editForm.id" placeholder="get_field" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="Skill 名称" prop="skillName" :rules="[{ required: true, message: '请输入 Skill 名称' }]">
                <el-input v-model="editForm.skillName" placeholder="值比较" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="功能分类" prop="category">
                <el-select v-model="editForm.category" style="width: 100%">
                  <el-option v-for="cat in categories" :key="cat.categoryCode" :label="cat.categoryName" :value="cat.categoryCode" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="24">
              <el-form-item label="说明" prop="description">
                <el-input v-model="editForm.description" type="textarea" :rows="2" placeholder="Skill 功能说明" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="启用">
                <el-switch v-model="editForm.isActive" />
              </el-form-item>
            </el-col>
          </el-row>

          <!-- ===== 反射信息（必填，验证后展示只读端口） ===== -->
          <el-divider content-position="left">反射信息</el-divider>
          <el-row :gutter="16">
            <el-col :span="16">
              <el-form-item
                label="实现类全名"
                prop="reflection.classPath"
                :rules="[{ required: true, message: '请输入实现类全名' }]"
              >
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
              <el-icon><IconSuccess /></el-icon> 验证反射
            </el-button>
          </div>
        </el-form>

        <!-- ===== 反射分析结果（验证后展示，只读） ===== -->
        <div v-if="analyzed" class="port-section">
          <el-alert type="success" :closable="false" show-icon style="margin-bottom: 12px">
            <template #title>
              反射验证通过：{{ analyzed.name }}（{{ analyzed.code }}）| 返回类型：{{ analyzed.returnType }}
            </template>
            <template #default>
              {{ analyzed.description }}
            </template>
          </el-alert>

          <!-- 输入端口 -->
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
            <el-table-column label="默认值" width="100">
              <template #default="{ row }"><span class="code-desc">{{ row.defaultValue || '—' }}</span></template>
            </el-table-column>
            <el-table-column label="绑定模式" width="120" align="center">
              <template #default="{ row }">
                <el-tag size="small" :type="getBindModeTagType(row.bindMode)">{{ getBindModeLabel(row.bindMode) }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="字典来源" width="140">
              <template #default="{ row }"><span class="code-desc">{{ row.enumSource || '—' }}</span></template>
            </el-table-column>
            <el-table-column label="描述" min-width="180">
              <template #default="{ row }"><span class="code-desc">{{ row.description || '—' }}</span></template>
            </el-table-column>
          </el-table>

          <!-- 输出端口 -->
          <div class="section-title" style="margin-top: 12px">输出端口（标准输出 + 业务输出）</div>
          <el-table :data="analyzed.outputPorts || getStandardOutputs(analyzed?.returnType)" border size="small">
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

        <!-- 未验证提示 -->
        <div v-if="!analyzed && analyzeError" class="empty-tip">
          <el-alert type="error" :closable="false" show-icon :title="analyzeError" />
        </div>
      </div>

      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave" :disabled="!analyzed">保存</el-button>
      </template>
    </el-dialog>

    <!-- ============ 分类管理弹窗 ============ -->
    <el-dialog v-model="categoryDialogVisible" title="Skill 分类管理" width="760px" destroy-on-close :close-on-click-modal="false">
      <el-alert type="info" :closable="false" show-icon title="分类为基础资料：左侧导航 + 面板分组" style="margin-bottom: 12px" />
      <el-table :data="categories" border size="small">
        <el-table-column label="编码" width="140">
          <template #default="{ row }"><el-input v-model="row.categoryCode" size="small" placeholder="data_access" /></template>
        </el-table-column>
        <el-table-column label="名称" width="130">
          <template #default="{ row }"><el-input v-model="row.categoryName" size="small" placeholder="数据获取" /></template>
        </el-table-column>
        <el-table-column label="图标" width="120">
          <template #default="{ row }"><el-input v-model="row.icon" size="small" placeholder="Folder" /></template>
        </el-table-column>
        <el-table-column label="颜色" width="90" align="center">
          <template #default="{ row }"><el-color-picker v-model="row.color" size="small" /></template>
        </el-table-column>
        <el-table-column label="排序" width="80">
          <template #default="{ row }"><el-input-number v-model="row.sortOrder" :min="0" size="small" controls-position="right" style="width: 100%" /></template>
        </el-table-column>
        <el-table-column label="启用" width="70" align="center">
          <template #default="{ row }">
            <el-switch :model-value="row.enable" size="small" @change="toggleCategory(row)" />
          </template>
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
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, getCurrentInstance } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { CertPageHeader } from '@/certcore'
import { IconSetting, IconAdd, IconSuccess } from '@/yzh/icons'

const { proxy } = getCurrentInstance()
const loading = ref(false)
const tableData = ref([])
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const keyword = ref('')
const dialogVisible = ref(false)
const baseFormRef = ref(null)

// 分类
const categories = ref([])
const currentCategory = ref('')
const currentCategoryName = ref('')
const categoryDialogVisible = ref(false)

// 反射分析
const analyzing = ref(false)
const analyzed = ref(null)
const analyzeError = ref('')

const getStandardOutputs = (returnType) => [
  { name: 'success', type: 'boolean', description: '是否执行成功' },
  { name: 'error', type: 'string', description: '失败时的错误信息' },
  { name: 'result', type: returnType || 'json', description: '执行结果（业务数据）' }
]

const emptyReflection = () => ({ id: 0, skillCode: '', classPath: '', methodName: 'ExecuteAsync', paramBinding: '' })

const editForm = reactive({
  id: null, code: '', skillCode: '',
  skillName: '', description: '',
  category: 'data_access',
  isActive: true,
  inputs: [], outputs: [], reflection: emptyReflection()
})

// ── 分类 ──

async function loadCategories() {
  try {
    const res = await proxy.http.get('api/skill-category/list', null, false)
    if (res?.status) categories.value = res.data || []
  } catch (e) { console.error('加载分类失败', e) }
}

function getCategoryName(code) {
  const cat = categories.value.find(c => c.categoryCode === code)
  return cat?.categoryName || code
}

function selectCategory(code, name) {
  currentCategory.value = code
  currentCategoryName.value = name || ''
  page.value = 1
  loadData()
}

// ── 列表 ──

async function loadData() {
  loading.value = true
  try {
    const res = await proxy.http.post('api/skill/page', {
      Page: page.value, Rows: pageSize.value, Sort: 'SortOrder', Order: 'asc'
    }, true, { params: { keyword: keyword.value || null, category: currentCategory.value || null } })
    if (res?.status) {
      tableData.value = res.data?.rows || []
      total.value = res.data?.total || 0
    }
  } catch (e) { console.error(e) } finally { loading.value = false }
}

// ── 端口类型标签 ──

function getTypeTagType(type) {
  const map = {
    boolean: 'success',
    string: '',
    number: 'warning',
    date: 'info',
    json: 'danger'
  }
  return map[type] || ''
}

function getBindModeLabel(mode) {
  const map = {
    Link: '仅连线',
    LinkOrConstant: '连线/常量',
    Enum: '字典选择'
  }
  return map[mode] || mode || '连线/常量'
}

function getBindModeTagType(mode) {
  const map = {
    Link: 'primary',
    LinkOrConstant: 'warning',
    Enum: 'success'
  }
  return map[mode] || ''
}

// ── 反射验证（POST /api/skill/analyze） ──

async function analyzeReflection() {
  if (!editForm.reflection.classPath) {
    ElMessage.warning('请先填写实现类全名')
    return
  }
  analyzing.value = true
  analyzed.value = null
  analyzeError.value = ''
  try {
    const res = await proxy.http.post('api/skill/analyze', {
      classPath: editForm.reflection.classPath,
      methodName: editForm.reflection.methodName || 'ExecuteAsync'
    }, true)
    if (res?.status && res.data) {
      analyzed.value = res.data
      ElMessage.success('反射验证通过')
    } else {
      analyzeError.value = res?.message || '反射验证失败'
      ElMessage.error(analyzeError.value)
    }
  } catch (e) {
    analyzeError.value = '反射验证请求失败'
    ElMessage.error('反射验证请求失败')
  } finally {
    analyzing.value = false
  }
}

// ── Skill 编辑弹窗 ──

function resetForm() {
  Object.assign(editForm, {
    id: null, code: '', skillCode: '',
    category: currentCategory.value || 'data_access',
    isActive: true,
    inputs: [], outputs: [], reflection: emptyReflection()
  })
  analyzed.value = null
  analyzeError.value = ''
}

async function openEdit(row) {
  resetForm()
  if (row) {
    try {
      const res = await proxy.http.get(`api/skill/${row.skillCode}`, null, true)
      if (res?.status && res.data) {
        const d = res.data
        Object.assign(editForm, {
          id: d.id, code: d.code || '', skillCode: d.skillCode,
          skillName: d.skillName || '', description: d.description || '',
          category: d.category || 'data_access',
          isActive: d.isActive !== false,
          inputs: (d.inputs || []).map(i => ({ ...i })),
          outputs: (d.outputs || []).map(o => ({ ...o })),
          reflection: d.reflection ? { ...d.reflection } : emptyReflection()
        })
        // 编辑模式：有反射数据时自动恢复端口展示（从 DB 镜像重建 analyzed）
        if (editForm.reflection.classPath && (d.inputs?.length > 0 || d.outputs?.length > 0)) {
          analyzed.value = {
            code: d.skillCode,
            name: editForm.skillName,
            returnType: d.outputs?.find(o => o.outputName === 'result')?.outputType || 'json',
            description: editForm.description || '',
            inputPorts: (d.inputs || []).map(i => ({
              name: i.inputName,
              type: i.inputType,
              required: i.isRequired,
              defaultValue: i.defaultValue || null,
              description: i.inputLabel || '',
              bindMode: i.bindMode || 'LinkOrConstant',
              enumSource: i.enumSource || null
            })),
            outputPorts: (d.outputs || []).map(o => ({
              name: o.outputName,
              type: o.outputType,
              description: o.description || ''
            }))
          }
        }
      }
    } catch (e) { console.error('加载详情失败', e) }
  }
  dialogVisible.value = true
}

// ── Skill 保存 / 删除 / 启停 ──

async function handleSave() {
  if (!analyzed.value) {
    ElMessage.warning('请先验证反射信息')
    return
  }
  const valid = await baseFormRef.value?.validate().catch(() => false)
  if (!valid) return
  try {
      const body = {
      id: editForm.id, code: editForm.code,
      skillCode: editForm.skillCode,
      skillName: editForm.skillName,
      description: editForm.description,
      category: editForm.category,
      isActive: editForm.isActive,
      inputs: editForm.inputs || [],
      outputs: editForm.outputs || [],
      reflection: editForm.reflection
    }
    const res = await proxy.http.post('api/skill', body, true)
    if (res?.status) { ElMessage.success('保存成功'); dialogVisible.value = false; loadData() }
    else ElMessage.error(res?.message || '保存失败')
  } catch (e) { ElMessage.error('保存失败') }
}

const handleDelete = async (row) => {
  try {
    await ElMessageBox.confirm(`确认删除 Skill「${row.skillName}」（${row.skillCode}）？`, '确认', { type: 'warning' })
    const res = await proxy.http.post(`api/skill/delete/${row.id}`, null, true)
    if (res?.status) { ElMessage.success('删除成功'); loadData() }
  } catch (e) { if (e !== 'cancel') ElMessage.error('删除失败') }
}

const toggleActive = async (row) => {
  const res = await proxy.http.post(`api/skill/toggle-active/${row.id}`, null, true)
  if (res?.status) ElMessage.success('操作成功')
  else { ElMessage.error('操作失败'); loadData() }
}

// ── 分类管理 ──

function openCategoryManage() { categoryDialogVisible.value = true }

function addCategory() {
  categories.value.push({ id: 0, categoryCode: '', categoryName: '', icon: '', color: '#409EFF', sortOrder: categories.value.length + 1, enable: true })
}

async function saveCategory(row) {
  if (!row.categoryCode || !row.categoryName) { ElMessage.warning('分类编码与名称必填'); return }
  try {
    const res = await proxy.http.post('api/skill-category', row, true)
    if (res?.status) { ElMessage.success('保存成功'); loadCategories() }
    else ElMessage.error(res?.message || '保存失败')
  } catch (e) { ElMessage.error('保存失败') }
}

async function deleteCategory(row) {
  if (!row.id) { categories.value = categories.value.filter(c => c !== row); return }
  try {
    await ElMessageBox.confirm(`确认删除分类「${row.categoryName}」？`, '确认', { type: 'warning' })
    const res = await proxy.http.post(`api/skill-category/delete/${row.id}`, null, true)
    if (res?.status) { ElMessage.success('删除成功'); loadCategories() }
    else ElMessage.error(res?.message || '删除失败')
  } catch (e) { if (e !== 'cancel') ElMessage.error('删除失败') }
}

const toggleCategory = async (row) => {
  if (!row.id) return
  const res = await proxy.http.post(`api/skill-category/toggle-active/${row.id}`, null, true)
  if (res?.status) ElMessage.success('操作成功')
  else { ElMessage.error('操作失败'); loadCategories() }
}

onMounted(() => { loadCategories(); loadData() })
</script>

<style scoped lang="less">
.skill-manage-page {
  padding: 16px; height: 100%;
  display: flex; flex-direction: column; overflow: hidden;
  box-sizing: border-box;
}
.page-body { display: flex; gap: 16px; flex: 1; min-height: 0; }
.category-card { width: 200px; min-width: 200px; display: flex; flex-direction: column; overflow: hidden; }
:deep(.category-card .el-card__body) { flex: 1; overflow: hidden; display: flex; flex-direction: column; }
.category-header { display: flex; align-items: center; justify-content: space-between; }
.category-list { overflow-y: auto; flex: 1; }
.category-item {
  display: flex; align-items: center; gap: 8px;
  padding: 8px 12px; margin-bottom: 4px; border-radius: 6px;
  cursor: pointer; font-size: 13px; color: #606266;
  transition: all .15s;
}
.category-item:hover { background: #f5f7fa; }
.category-item.active { background: #ecf5ff; color: #409EFF; font-weight: 600; }
.cat-dot { width: 8px; height: 8px; border-radius: 50%; display: inline-block; }

.table-card { flex: 1; overflow: hidden; display: flex; flex-direction: column; }
:deep(.table-card .el-card__body) { flex: 1; overflow: hidden; display: flex; flex-direction: column; }
.card-header { display: flex; align-items: center; justify-content: space-between; flex-shrink: 0; }
.card-title { font-size: 15px; font-weight: 600; }
.card-actions { display: flex; align-items: center; }
.table-wrapper { flex: 1; min-height: 0; overflow: hidden; }
.row-actions { display: flex; gap: 4px; white-space: nowrap; }
.empty-tip { padding: 24px 0; text-align: center; }
.section-title { font-size: 13px; font-weight: 600; color: #303133; margin-bottom: 8px; }
.port-name { font-family: 'SF Mono', Monaco, monospace; font-size: 13px; color: #303133; font-weight: 600; }
.code-desc { font-size: 12px; color: #909399; }

.dialog-body { max-height: 70vh; overflow-y: auto; padding-right: 8px; }
.edit-form { margin-bottom: 0; }
</style>
