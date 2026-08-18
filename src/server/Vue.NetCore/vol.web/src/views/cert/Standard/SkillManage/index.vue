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

        <el-table :data="tableData" stripe border v-loading="loading" style="width: 100%">
          <el-table-column prop="skillCode" label="编码" width="140" />
          <el-table-column prop="skillName" label="名称" width="160" show-overflow-tooltip />
          <el-table-column label="类型" width="80" align="center">
            <template #default="{ row }">
              <el-tag :type="row.skillType === 'api' ? 'warning' : 'primary'" size="small">
                {{ row.skillType === 'api' ? 'API' : '方法' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="性质" width="80" align="center">
            <template #default="{ row }">{{ row.sideEffect ? '功能性' : '逻辑性' }}</template>
          </el-table-column>
          <el-table-column label="输出约束" width="90" align="center">
            <template #default="{ row }">
              <el-tag :type="row.outputStrict ? 'danger' : 'info'" size="small">
                {{ row.outputStrict ? '强约束' : '弱约束' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="returnType" label="返回类型" width="90" align="center" />
          <el-table-column prop="version" label="版本" width="70" align="center" />
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
        <el-pagination
          v-model:current-page="page"
          :page-size="pageSize"
          :total="total"
          layout="total, prev, pager, next"
          style="margin-top: 16px; justify-content: flex-end"
          @current-change="loadData"
        />
      </el-card>
    </div>

    <!-- ============ Skill 编辑弹窗：5 Tab ============ -->
    <el-dialog
      v-model="dialogVisible"
      :title="editForm.id ? `编辑 Skill：${editForm.skillName}` : '新建 Skill'"
      width="920px"
      top="4vh"
      destroy-on-close
    >
      <el-tabs v-model="activeTab">
        <el-tab-pane label="基本信息" name="base">
          <el-form :model="editForm" label-width="110px" ref="baseFormRef">
            <el-row :gutter="16">
              <el-col :span="12">
                <el-form-item label="Skill 编码" prop="skillCode" :rules="[{ required: true, message: '请输入 Skill 编码（唯一，如 get_field）' }]">
                  <el-input v-model="editForm.skillCode" :disabled="!!editForm.id" placeholder="get_field" />
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="Skill 名称" prop="skillName" :rules="[{ required: true, message: '请输入 Skill 名称' }]">
                  <el-input v-model="editForm.skillName" placeholder="字段提取" />
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="类型" prop="skillType">
                  <el-select v-model="editForm.skillType" style="width: 100%">
                    <el-option label="后台方法（method）" value="method" />
                    <el-option label="API 接口（api）" value="api" />
                  </el-select>
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item label="功能分类" prop="category">
                  <el-select v-model="editForm.category" style="width: 100%">
                    <el-option v-for="cat in categories" :key="cat.categoryCode" :label="cat.categoryName" :value="cat.categoryCode" />
                  </el-select>
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="性质">
                  <el-switch v-model="editForm.sideEffect" active-text="功能性" inactive-text="逻辑性" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="输出约束">
                  <el-switch v-model="editForm.outputStrict" active-text="强约束" inactive-text="弱约束" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="返回类型">
                  <el-select v-model="editForm.returnType" style="width: 100%">
                    <el-option v-for="t in ['json', 'string', 'number', 'boolean', 'date']" :key="t" :label="t" :value="t" />
                  </el-select>
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="版本">
                  <el-input v-model="editForm.version" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="图标">
                  <el-input v-model="editForm.icon" placeholder="面板图标" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="颜色">
                  <el-input v-model="editForm.color" placeholder="#409EFF" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="排序">
                  <el-input-number v-model="editForm.sortOrder" :min="0" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="启用">
                  <el-switch v-model="editForm.isActive" />
                </el-form-item>
              </el-col>
              <el-col :span="24">
                <el-form-item label="作用说明">
                  <el-input v-model="editForm.description" type="textarea" :rows="2" placeholder="该 Skill 的作用说明" />
                </el-form-item>
              </el-col>
              <el-col :span="24">
                <el-form-item label="AI 提示词">
                  <el-input v-model="editForm.skillPrompt" type="textarea" :rows="3" placeholder="解释器组装给 AI 使用的 Skill 使用提示词（名词解释），可选" />
                </el-form-item>
              </el-col>
              <el-col :span="24">
                <el-form-item label="备注">
                  <el-input v-model="editForm.remark" type="textarea" :rows="1" />
                </el-form-item>
              </el-col>
            </el-row>
          </el-form>
        </el-tab-pane>

        <el-tab-pane label="输入项" name="inputs">
          <div class="subtable-tip">输入表单模板（画布生成输入表单用，非硬校验；ai_node 等动态输入可留空）</div>
          <el-table :data="editForm.inputs" border size="small">
            <el-table-column label="参数名" min-width="130">
              <template #default="{ row }"><el-input v-model="row.inputName" placeholder="fieldCode" size="small" /></template>
            </el-table-column>
            <el-table-column label="显示名" min-width="120">
              <template #default="{ row }"><el-input v-model="row.inputLabel" placeholder="字段编码" size="small" /></template>
            </el-table-column>
            <el-table-column label="类型" width="130">
              <template #default="{ row }">
                <el-select v-model="row.inputType" size="small" style="width: 100%">
                  <el-option v-for="t in ['text', 'number', 'date', 'boolean', 'enum', 'field_ref', 'table_ref', 'json']" :key="t" :label="t" :value="t" />
                </el-select>
              </template>
            </el-table-column>
            <el-table-column label="枚举值" min-width="110">
              <template #default="{ row }"><el-input v-model="row.enumValues" placeholder="a,b,c" size="small" /></template>
            </el-table-column>
            <el-table-column label="必填" width="60" align="center">
              <template #default="{ row }"><el-switch v-model="row.isRequired" size="small" /></template>
            </el-table-column>
            <el-table-column label="默认值" min-width="110">
              <template #default="{ row }"><el-input v-model="row.defaultValue" size="small" /></template>
            </el-table-column>
            <el-table-column label="排序" width="70">
              <template #default="{ row }"><el-input-number v-model="row.sortOrder" :min="0" size="small" controls-position="right" style="width: 100%" /></template>
            </el-table-column>
            <el-table-column label="操作" width="70" align="center">
              <template #default="{ $index }">
                <el-button type="danger" link size="small" @click="removeRow(editForm.inputs, $index)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
          <el-button type="primary" plain size="small" style="margin-top: 8px" @click="addInputRow">+ 添加输入项</el-button>
        </el-tab-pane>

        <el-tab-pane label="输出项" name="outputs">
          <div class="subtable-tip">强约束输出契约（output_strict=1 时解释器按此校验）；弱约束可留空</div>
          <el-table :data="editForm.outputs" border size="small">
            <el-table-column label="端口名" min-width="130">
              <template #default="{ row }"><el-input v-model="row.outputName" placeholder="fieldValue" size="small" /></template>
            </el-table-column>
            <el-table-column label="类型" width="110">
              <template #default="{ row }">
                <el-select v-model="row.outputType" size="small" style="width: 100%">
                  <el-option v-for="t in ['json', 'string', 'number', 'boolean', 'date']" :key="t" :label="t" :value="t" />
                </el-select>
              </template>
            </el-table-column>
            <el-table-column label="解读提示词" min-width="180">
              <template #default="{ row }"><el-input v-model="row.outputPrompt" placeholder="该输出的解读提示词" size="small" /></template>
            </el-table-column>
            <el-table-column label="说明" min-width="120">
              <template #default="{ row }"><el-input v-model="row.description" size="small" /></template>
            </el-table-column>
            <el-table-column label="排序" width="70">
              <template #default="{ row }"><el-input-number v-model="row.sortOrder" :min="0" size="small" controls-position="right" style="width: 100%" /></template>
            </el-table-column>
            <el-table-column label="操作" width="70" align="center">
              <template #default="{ $index }">
                <el-button type="danger" link size="small" @click="removeRow(editForm.outputs, $index)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
          <el-button type="primary" plain size="small" style="margin-top: 8px" @click="addOutputRow">+ 添加输出项</el-button>
        </el-tab-pane>

        <el-tab-pane label="反射信息" name="reflection">
          <el-alert type="info" :closable="false" show-icon title="method 型 Skill 通过反射执行：填写类型全名，ReflectionSkillLoader 加载（DI 优先，找不到则反射创建）" style="margin-bottom: 12px" />
          <el-form label-width="120px">
            <el-form-item label="反射地址">
              <el-input v-model="editForm.reflection.classPath" placeholder="YZH.Core.Skills.GetFieldSkill" />
            </el-form-item>
            <el-form-item label="反射方法">
              <el-input v-model="editForm.reflection.methodName" placeholder="ExecuteAsync" />
            </el-form-item>
            <el-form-item label="参数绑定">
              <el-input v-model="editForm.reflection.paramBinding" type="textarea" :rows="3" placeholder='{"输入项名":"方法参数名或顺序"}' />
            </el-form-item>
          </el-form>
        </el-tab-pane>

        <el-tab-pane label="API 信息" name="api">
          <el-alert type="warning" :closable="false" show-icon title="api 型 Skill 信息维护（执行由 HttpApiSkillNode 后续实现，本期仅配置）" style="margin-bottom: 12px" />
          <el-form label-width="120px">
            <el-form-item label="接口地址">
              <el-input v-model="editForm.api.url" placeholder="https://api.example.com/v1/xxx" />
            </el-form-item>
            <el-form-item label="请求方法">
              <el-select v-model="editForm.api.httpMethod" style="width: 200px">
                <el-option label="POST" value="POST" />
                <el-option label="GET" value="GET" />
              </el-select>
            </el-form-item>
            <el-form-item label="请求头">
              <el-input v-model="editForm.api.headers" type="textarea" :rows="2" placeholder='{"Content-Type":"application/json"}' />
            </el-form-item>
            <el-form-item label="鉴权配置">
              <el-input v-model="editForm.api.authConfig" type="textarea" :rows="2" placeholder='{"type":"bearer","tokenSource":"$sys.xxx"}' />
            </el-form-item>
            <el-form-item label="参数映射">
              <el-input v-model="editForm.api.paramMapping" type="textarea" :rows="2" placeholder='{"输入项名":"请求参数名"}' />
            </el-form-item>
            <el-form-item label="响应解析">
              <el-input v-model="editForm.api.responseMapping" type="textarea" :rows="2" placeholder='{"输出项名":"$.data.xxx"}' />
            </el-form-item>
            <el-form-item label="超时（秒）">
              <el-input-number v-model="editForm.api.timeoutSeconds" :min="1" :max="300" />
            </el-form-item>
          </el-form>
        </el-tab-pane>
      </el-tabs>

      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave">保存</el-button>
      </template>
    </el-dialog>

    <!-- ============ 分类管理弹窗 ============ -->
    <el-dialog v-model="categoryDialogVisible" title="Skill 分类管理" width="760px" destroy-on-close>
      <el-alert type="info" :closable="false" show-icon title="分类为基础资料：左侧导航 + 面板分组；分类下仍有启用 Skill 时不可删除" style="margin-bottom: 12px" />
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
import { IconSetting, IconAdd } from '@/yzh/icons'

const { proxy } = getCurrentInstance()
const loading = ref(false)
const tableData = ref([])
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const keyword = ref('')
const dialogVisible = ref(false)
const activeTab = ref('base')
const baseFormRef = ref(null)

// 分类
const categories = ref([])
const currentCategory = ref('')
const currentCategoryName = ref('')
const categoryDialogVisible = ref(false)

const emptyReflection = () => ({ id: 0, skillCode: '', classPath: '', methodName: 'ExecuteAsync', paramBinding: '' })
const emptyApi = () => ({
  id: 0, skillCode: '', url: '', httpMethod: 'POST', headers: '',
  authConfig: '', paramMapping: '', responseMapping: '', timeoutSeconds: 30
})

const editForm = reactive({
  id: null, code: '', skillCode: '', skillName: '', skillType: 'method',
  category: 'data_access', sideEffect: true, description: '', skillPrompt: '',
  isActive: true, outputStrict: true, returnType: 'json', version: '1.0',
  icon: '', color: '', sortOrder: 0, remark: '',
  inputs: [], outputs: [], reflection: emptyReflection(), api: emptyApi()
})

// ── 分类 ──

async function loadCategories() {
  try {
    const res = await proxy.http.get('api/skill-category/list', null, false)
    if (res?.status) categories.value = res.data || []
  } catch (e) { console.error('加载分类失败', e) }
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

// ── Skill 编辑弹窗 ──

function resetForm() {
  Object.assign(editForm, {
    id: null, code: '', skillCode: '', skillName: '', skillType: 'method',
    category: currentCategory.value || 'data_access', sideEffect: true, description: '', skillPrompt: '',
    isActive: true, outputStrict: true, returnType: 'json', version: '1.0',
    icon: '', color: '', sortOrder: 0, remark: '',
    inputs: [], outputs: [], reflection: emptyReflection(), api: emptyApi()
  })
}

async function openEdit(row) {
  resetForm()
  activeTab.value = 'base'
  if (row) {
    try {
      const res = await proxy.http.get(`api/skill/${row.skillCode}`, null, true)
      if (res?.status && res.data) {
        const d = res.data
        Object.assign(editForm, {
          id: d.id, code: d.code || '', skillCode: d.skillCode, skillName: d.skillName,
          skillType: d.skillType || 'method', category: d.category || 'data_access',
          sideEffect: !!d.sideEffect, description: d.description || '', skillPrompt: d.skillPrompt || '',
          isActive: d.isActive !== false, outputStrict: d.outputStrict !== false,
          returnType: d.returnType || 'json', version: d.version || '1.0',
          icon: d.icon || '', color: d.color || '', sortOrder: d.sortOrder || 0, remark: d.remark || '',
          inputs: (d.inputs || []).map(i => ({ ...i })),
          outputs: (d.outputs || []).map(o => ({ ...o })),
          reflection: d.reflection ? { ...d.reflection } : emptyReflection(),
          api: d.api ? { ...d.api } : emptyApi()
        })
      }
    } catch (e) { console.error('加载详情失败', e) }
  }
  dialogVisible.value = true
}

// ── 子表行操作 ──

function addInputRow() {
  editForm.inputs.push({ id: 0, inputName: '', inputLabel: '', inputType: 'text', enumValues: '', isRequired: false, defaultValue: '', sortOrder: editForm.inputs.length + 1 })
}

function addOutputRow() {
  editForm.outputs.push({ id: 0, outputName: '', outputType: 'json', outputPrompt: '', description: '', sortOrder: editForm.outputs.length + 1 })
}

function removeRow(list, index) {
  list.splice(index, 1)
}

// ── Skill 保存 / 删除 / 启停 ──

async function handleSave() {
  const valid = await baseFormRef.value?.validate().catch(() => false)
  if (!valid) return
  try {
    const body = {
      id: editForm.id, code: editForm.code,
      skillCode: editForm.skillCode, skillName: editForm.skillName,
      skillType: editForm.skillType, category: editForm.category,
      sideEffect: editForm.sideEffect, description: editForm.description,
      skillPrompt: editForm.skillPrompt, isActive: editForm.isActive,
      outputStrict: editForm.outputStrict, returnType: editForm.returnType,
      version: editForm.version, icon: editForm.icon, color: editForm.color,
      sortOrder: editForm.sortOrder, remark: editForm.remark,
      inputs: editForm.inputs, outputs: editForm.outputs,
      reflection: editForm.reflection,
      api: editForm.api
    }
    const res = await proxy.http.post('api/skill', body, true)
    if (res?.status) { ElMessage.success('保存成功'); dialogVisible.value = false; loadData() }
    else ElMessage.error(res?.message || '保存失败')
  } catch (e) { ElMessage.error('保存失败') }
}

const handleDelete = async (row) => {
  try {
    await ElMessageBox.confirm(`确认删除 Skill「${row.skillName}」（${row.skillCode}）？子表将一并删除`, '确认', { type: 'warning' })
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

function openCategoryManage() {
  categoryDialogVisible.value = true
}

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
.skill-manage-page { padding: 16px; height: 100%; display: flex; flex-direction: column; overflow: hidden; box-sizing: border-box; }
.page-body { display: flex; gap: 16px; flex: 1; min-height: 0; }
.category-card { width: 200px; min-width: 200px; display: flex; flex-direction: column; }
.category-header { display: flex; align-items: center; justify-content: space-between; }
.category-list { overflow-y: auto; }
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
.card-header { display: flex; align-items: center; justify-content: space-between; }
.card-title { font-size: 15px; font-weight: 600; }
.card-actions { display: flex; align-items: center; }
.row-actions { display: flex; gap: 4px; white-space: nowrap; }
.subtable-tip { color: #909399; font-size: 12px; margin-bottom: 8px; }
</style>
