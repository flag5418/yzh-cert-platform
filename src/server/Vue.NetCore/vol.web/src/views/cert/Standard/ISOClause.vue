<template>
  <div class="iso-clause-page">
    <CertPageHeader title="标准条款管理" :icon="IconSetting" />

    <div class="page-body">
      <!-- 左侧：标准选择 -->
      <el-card shadow="never" class="std-card">
        <template #header>
          <span class="card-title">ISO 标准</span>
        </template>
        <div class="std-list">
          <div
            v-for="std in standardList"
            :key="std.code"
            class="std-item"
            :class="{ active: selectedStandard === std.code }"
            @click="onStandardChange(std.code)"
          >
            <span class="std-name">{{ std.standardName || std.standardCode }}</span>
          </div>
        </div>
        <el-empty v-if="!standardList.length" description="请先在标准管理中添加标准" :image-size="60" />
      </el-card>

      <!-- 左侧：标准选择结束 -->

      <!-- 右侧：条款树 + CRUD -->
      <div class="clause-area">
        <el-card shadow="never" class="toolbar-card">
          <div class="toolbar">
            <span class="toolbar-title">
              条款树
              <el-tag v-if="selectedStandardName" size="small" type="info" style="margin-left:8px">{{ selectedStandardName }}</el-tag>
            </span>
            <div>
              <el-button type="primary" size="small" @click="openEdit(null)" :disabled="!selectedStandard">
                <el-icon><IconAdd /></el-icon> 新建条款
              </el-button>
              <el-button size="small" @click="loadClauses" :disabled="!selectedStandard">
                <el-icon><IconRefresh /></el-icon> 刷新
              </el-button>
            </div>
          </div>
        </el-card>

        <el-card shadow="never" class="tree-card">
          <el-tree
            ref="clauseTreeRef"
            :data="clauseTreeData"
            :props="{ label: 'label', children: 'children' }"
            node-key="code"
            highlight-current
            default-expand-all
            @node-click="onNodeClick"
          >
            <template #default="{ node, data }">
              <span class="clause-node">
                <span class="clause-number">{{ data.clauseNumber }}</span>
                <span class="clause-title">{{ data.title }}</span>
                <span class="clause-actions">
                  <el-button type="primary" link size="small" @click.stop="openEdit(data)">编辑</el-button>
                  <el-button type="primary" link size="small" @click.stop="addChild(data)">添加子条款</el-button>
                  <el-button type="danger" link size="small" @click.stop="handleDelete(data)">删除</el-button>
                </span>
              </span>
            </template>
          </el-tree>
          <el-empty v-if="!clauseTreeData.length && selectedStandard" description="该标准暂无条款，点击「新建条款」添加" :image-size="80" />
        </el-card>
      </div>
    </div>

    <!-- 编辑弹窗 -->
    <el-dialog v-model="dialogVisible" :title="editForm.id ? '编辑条款' : '新建条款'" width="550px" destroy-on-close>
      <el-form :model="editForm" label-width="100px" ref="formRef">
        <el-form-item label="所属标准">
          <el-input :model-value="selectedStandardName" disabled />
        </el-form-item>
        <el-form-item label="条款编号" prop="clauseNumber" :rules="[{ required: true, message: '请输入条款编号' }]">
          <el-input v-model="editForm.clauseNumber" placeholder="如：7.1 或 7.1.1" />
        </el-form-item>
        <el-form-item label="条款标题" prop="title" :rules="[{ required: true, message: '请输入条款标题' }]">
          <el-input v-model="editForm.title" placeholder="如：资源" />
        </el-form-item>
        <el-form-item label="父级条款">
          <el-tree-select
            v-model="editForm.parentCode"
            :data="clauseTreeData"
            :props="{ label: 'label', value: 'code', children: 'children' }"
            filterable
            check-strictly
            clearable
            placeholder="无（顶级条款）"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="条款描述">
          <el-input v-model="editForm.description" type="textarea" :rows="4" placeholder="条款原文或要求摘要" />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="editForm.sortOrder" :min="0" :max="999" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, getCurrentInstance } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { CertPageHeader } from '@/certcore'
import { IconSetting, IconAdd, IconRefresh } from '@/yzh/icons'

const { proxy } = getCurrentInstance()
const formRef = ref(null)
const clauseTreeRef = ref(null)
const selectedStandard = ref('')
const selectedStandardName = ref('')
const standardList = ref([])
const clauseTreeData = ref([])
const dialogVisible = ref(false)
const editForm = reactive({
  id: null,
  code: '',
  standardCode: '',
  parentCode: '',
  clauseNumber: '',
  title: '',
  description: '',
  sortOrder: 0
})

// ── 加载标准列表 ──
async function loadStandards() {
  try {
    const res = await proxy.http.get('api/iso-clause/standards', null, false)
    if (res?.status) {
      standardList.value = res.data || []
      // 自动选第一个标准
      if (standardList.value.length > 0 && !selectedStandard.value) {
        const first = standardList.value[0]
        selectedStandard.value = first.code || first.standardCode
        selectedStandardName.value = first.standardName || first.standardCode || first.code
        loadClauses()
      }
    }
  } catch (e) { console.error('加载标准失败', e) }
}

// ── 标准切换 ──
function onStandardChange(val) {
  selectedStandard.value = val
  const std = standardList.value.find(s => s.code === val)
  selectedStandardName.value = std ? (std.standardName || std.standardCode || val) : val
  loadClauses()
}

// ── 加载条款树 ──
async function loadClauses() {
  if (!selectedStandard.value) {
    clauseTreeData.value = []
    return
  }
  try {
    const res = await proxy.http.get(`api/iso-clause/tree?standardCode=${selectedStandard.value}`, null, false)
    if (res?.status) {
      clauseTreeData.value = res.data || []
    }
  } catch (e) { console.error('加载条款失败', e) }
}

// ── 树节点点击 ──
function onNodeClick(data) {
  // 只展示，不做其他操作
}

// ── 打开编辑弹窗 ──
function openEdit(row) {
  if (row && row.id) {
    // 编辑已有条款（必须从树节点获取 id 才能走更新分支）
    Object.assign(editForm, {
      id: row.id,
      code: row.code || '',
      standardCode: selectedStandard.value,
      parentCode: row.parentCode || '',
      clauseNumber: row.clauseNumber || '',
      title: row.title || '',
      description: row.description || '',
      sortOrder: row.sortOrder ?? 0
    })
  } else {
    // 新建顶级条款
    Object.assign(editForm, {
      id: null, code: '',
      standardCode: selectedStandard.value,
      parentCode: '',
      clauseNumber: '', title: '', description: '',
      sortOrder: clauseTreeData.value.length
    })
  }
  dialogVisible.value = true
}

// ── 添加子条款 ──
function addChild(parentData) {
  Object.assign(editForm, {
    id: null, code: '',
    standardCode: selectedStandard.value,
    parentCode: parentData.code,
    clauseNumber: '', title: '', description: '',
    sortOrder: (parentData.children?.length || 0)
  })
  dialogVisible.value = true
}

// ── 保存 ──
const handleSave = async () => {
  if (!formRef.value) return
  await formRef.value.validate(async (valid) => {
    if (!valid) return
    try {
      const res = await proxy.http.post('api/iso-clause', editForm, true)
      if (res?.status) {
        ElMessage.success('保存成功')
        dialogVisible.value = false
        loadClauses()
      } else {
        ElMessage.error(res?.message || '保存失败')
      }
    } catch (e) { ElMessage.error('保存失败') }
  })
}

// ── 删除 ──
const handleDelete = async (row) => {
  try {
    await ElMessageBox.confirm(`确认删除条款「${row.clauseNumber} ${row.title}」？\n如果有子条款将无法删除。`, '确认', { type: 'warning' })
    const res = await proxy.http.post(`api/iso-clause/delete/${row.id}`, null, true)
    if (res?.status) {
      ElMessage.success('删除成功')
      loadClauses()
    } else {
      ElMessage.error(res?.message || '删除失败')
    }
  } catch (e) { if (e !== 'cancel') ElMessage.error('删除失败') }
}

onMounted(() => { loadStandards() })
</script>

<style scoped lang="less">
.iso-clause-page { padding: var(--yzh-space-5, 20px); }
.page-body { display: flex; gap: 16px; height: calc(100vh - 140px); }
.std-card { width: 220px; min-width: 220px; overflow-y: auto; }
.std-list { display: flex; flex-direction: column; gap: 4px; }
.std-item {
  padding: 8px 12px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  border: 1px solid transparent;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.std-item:hover { background: #f0f7ff; border-color: #d0e3ff; }
.std-item.active { background: #ecf5ff; border-color: #409eff; }
.std-item .std-name { font-size: 13px; font-weight: 500; color: #303133; }
.std-item.active .std-name { color: #409eff; }
.clause-area { flex: 1; display: flex; flex-direction: column; overflow: hidden; }
.toolbar-card { margin-bottom: 12px; }
.toolbar { display: flex; align-items: center; justify-content: space-between; }
.toolbar-title { font-size: 15px; font-weight: 600; }
.tree-card { flex: 1; overflow: auto; }
.clause-node { display: flex; align-items: center; gap: 6px; font-size: 13px; width: 100%; }
.clause-number { font-weight: 600; color: #409eff; min-width: 50px; }
.clause-title { flex: 1; }
.clause-actions { opacity: 0; transition: opacity 0.2s; }
.clause-node:hover .clause-actions { opacity: 1; }
</style>
