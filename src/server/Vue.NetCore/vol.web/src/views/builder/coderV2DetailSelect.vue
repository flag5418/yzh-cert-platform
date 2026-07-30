<template>
  <vol-box
    ref="add"
    :width="width"
    title="选择明细表(此功能主要解决快速配置明细表信息;注意：明细表需要先在代码生成器上正常生成配置！没有明细表的忽略此配置)"
    :height="height"
    :padding="10"
    v-model="model"
  >
    <!-- <el-alert title="此功能主要解决快速配置明细表信息;注意：明细表需要先在代码生成器上正常生成配置！没有明细表的忽略此配置" class="alert-primary"
      :closable="false"></el-alert> -->
    <div class="detail-select-body">
      <div class="detail-select-left">
        <div style="margin: 8px 0">
          <vol-title title="代码生成配置表" icon="FolderOpened">
            <!-- <span style="color: #41a940; font-size: 13px" class="el-icon-info">选择明细表</span> -->
            <!-- <span style="color: #868686; font-size: 13px" class="el-icon-info">表生成配置后下面才会显示</span> -->
          </vol-title>
        </div>
        <div class="detail-select-search">
          <el-input
            v-model="searchKeyword"
            placeholder="按名称或表名搜索"
            clearable
            :prefix-icon="'Search'"
          >
          </el-input>
        </div>
        <el-scrollbar class="detail-select-tree-scroll" :height="height - 95">
          <el-tree
            ref="treeRef"
            :key="searchKeyword ? 'search' : 'all'"
            highlight-current
            node-key="id"
            class="detail-select-tree"
            :data="filteredTreeData"
            :expand-on-click-node="false"
            :default-expanded-keys="effectiveExpandedKeys"
            :props="{ label: 'name', children: 'children' }"
            @node-click="onTreeNodeClick"
            icon="ArrowRight"
          >
            <template #default="{ node }">
              <div class="tree-node-wrapper">
                <span class="tree-node-label">{{ node.label }}</span>
                <el-button
                  v-if="isTableNode(node)"
                  color="#0425dd"
                  link
                  plain
                  size="small"
                  style="padding: 5px !important; margin: 0"
                  @click.stop="addTableFromNode(node)"
                >
                  <i style="font-size: 13px" class="el-icon-plus"></i>选择
                </el-button>
              </div>
            </template>
          </el-tree>
        </el-scrollbar>
      </div>
      <div class="detail-select-table-list">
        <div style="margin: 8px 0">
          <vol-title title="已选明细表" icon="MessageBox">
            <span style="color: #868686; font-size: 13px" class="el-icon-info"
              >可拖拽表格对明细表显示排序</span
            >
          </vol-title>
          <el-alert
            style="margin-top: 7px"
            title="点击行可加载右边【明细表配置】信息"
            class="alert-primary"
            :closable="false"
          ></el-alert>
        </div>
        <!-- <el-alert style="margin: 5px 0;" title="明细表格可拖拽排序" class="alert-primary" :closable="false"></el-alert> -->
        <vol-table
          ref="tableRef"
          :column-index="false"
          :ck="false"
          :table-data="tableData"
          :columns="columns"
          :height="height - 128 + 33"
          @rowClick="
            ({ row }) => {
              configClick(row);
            }
          "
          :sortable="true"
          :text-inline="false"
          :pagination-hide="true"
        ></vol-table>
      </div>
      <div class="detail-select-right">
        <div style="margin: 8px 0; display: flex">
          <vol-title :title="'明细表 ' + (detailTableInfo?.tableName || '')" icon="Edit">
            <div style="font-size: 13px; flex: 1">
              <span
                class="el-icon-info"
                style="color: red; font-weight: bolder; font-size: 15px"
              >
                修改配置后请点击[保存明细表],否则缓存不会刷新</span
              >
            </div>
          </vol-title>
          <div style="flex: 1; text-align: right">
            <el-button
              color="#0425dd"
              link
              plain
              size="small"
              style="padding: 5px !important; margin: 0"
              @click="syncDetailTable"
              ><i style="font-size: 13px" class="el-icon-refresh"></i
              >同步表结构</el-button
            >
            <el-button
              color="#0425dd"
              link
              plain
              size="small"
              style="padding: 5px !important; margin: 0"
              @click="saveDetailConfig"
              ><i style="font-size: 13px" class="el-icon-check"></i>保存明细表</el-button
            >
          </div>
        </div>
        <el-alert
          style="margin-top: -5px; margin-bottom: 7px"
          title="1. 拖拽字段调整显示顺序; 2. 如有修改明细表字段,请点[同步表结构]"
          class="alert-primary"
          :closable="false"
        ></el-alert>
        <div class="detail-extra-fields detail-extra-fields--row">
          <div class="detail-field-cell detail-field-cell--mainkey">
            <el-tooltip
              class="detail-field-label-wrap"
              effect="dark"
              placement="top-start"
            >
              <template #content>
                <div style="font-size: 12px">
                  明细表与主表关联的外键字段；保存后写入当前明细表配置
                </div>
              </template>
              <span class="detail-field-label"
                ><i class="el-icon-warning-outline"></i>与主表关联字段
              </span>
            </el-tooltip>
            <el-select
              v-model="detailTableInfo.mainKeyField"
              clearable
              filterable
              placeholder="请选择"
              size="default"
              class="detail-field-control detail-field-control--mainkey"
            >
              <el-option
                v-for="opt in mainKeyFieldOptions"
                :key="String(opt.key)"
                :label="opt.value"
                :value="opt.key"
              />
            </el-select>
          </div>
          <div class="detail-field-cell detail-field-cell--sort">
            <span class="detail-field-label detail-field-label--sort"
              >明细表排序字段</span
            >
            <div class="detail-sort-row">
              <el-input
                v-model="detailTableInfo.sortName"
                disabled
                size="default"
                placeholder="排序字段"
                class="detail-sort-input"
              />
              <el-button
                color="#0425dd"
                link
                plain
                size="small"
                @click="showDetailSortSelect"
              >
                <i class="el-icon-plus"></i>选择排序字段
              </el-button>
              <el-button
                color="#0425dd"
                style="margin-left: 0"
                link
                plain
                size="small"
                @click="toDetail"
              >
                <i class="el-icon-edit-outline"></i>完整配置
              </el-button>
            </div>
          </div>
        </div>
        <vol-table
          ref="editTableRef"
          :column-index="true"
          :ck="false"
          :table-data="editData"
          :columns="editColumns"
          :height="height - 175 + 33"
          :sortable="true"
          @onSortEnd="onSortEnd"
          :text-inline="true"
          :pagination-hide="true"
        ></vol-table>
      </div>
    </div>
    <template #footer>
      <div class="detail-select-footer">
        <el-button type="default" size="small" @click="syncDetailTable">
          <i style="font-size: 13px" class="el-icon-refresh"></i> 同步明细表结构
        </el-button>
        <el-button type="danger" size="small" @click="saveDetailConfig">
          <i style="font-size: 13px" class="el-icon-check"></i> 保存明细表
        </el-button>
        <el-button type="primary" color="#4f58d2" size="small" @click="onSelect"
          ><i class="el-icon-check"></i> 确定</el-button
        >
        <el-button type="default" size="small" @click="model = false"
          ><i class="el-icon-close"></i> 关闭</el-button
        >
      </div>
    </template>
  </vol-box>
  <coderV2SortFieldSelect
    ref="sortFieldSelectRef"
    @onSortConfirm="onDetailSortConfirm"
  ></coderV2SortFieldSelect>
</template>
<script setup lang="jsx">
import { ref, getCurrentInstance, computed, defineAsyncComponent } from 'vue'

const coderV2SortFieldSelect = defineAsyncComponent(() => import('./coderV2SortFieldSelect.vue'))

const { proxy } = getCurrentInstance()
const emit = defineEmits(['onDetailSelect'])

const treeRef = ref()
const tableRef = ref()
const editTableRef = ref()
const tableData = ref([])
const treeData = ref([])
const searchKeyword = ref('')
const model = ref(false)

const filterTreeNode = (node, keyword) => {
  const kw = keyword.trim().toLowerCase()
  const match = (node.name && String(node.name).toLowerCase().includes(kw)) ||
    (node.tableName && String(node.tableName).toLowerCase().includes(kw))
  if (node.children?.length) {
    const filtered = node.children.map((c) => filterTreeNode(c, keyword)).filter(Boolean)
    if (match || filtered.length) {
      return { ...node, children: filtered.length ? filtered : node.children }
    }
  }
  return match ? node : null
}

const filteredTreeData = computed(() => {
  const kw = searchKeyword.value?.trim()
  if (!kw) return treeData.value
  return treeData.value.map((node) => filterTreeNode(node, kw)).filter(Boolean)
})

const effectiveExpandedKeys = computed(() => {
  const kw = searchKeyword.value?.trim()
  if (!kw) return []
  const expandIds = []
  const collect = (nodes) => {
    nodes.forEach((n) => {
      if (n.children?.length) {
        expandIds.push(n.id)
        collect(n.children)
      }
    })
  }
  collect(filteredTreeData.value)
  return expandIds
})
const height = ref(document.body.clientHeight * 0.85);
if (height.value > 600) {
  height.value = 600;
}

const width = ref(document.body.clientWidth * 0.95);
if (width.value > 1600) {
  width.value = 1600
}

const columns = ref([
  { title: "表名", field: "detailName", width: 170 },
  {
    title: "表中文名",
    field: "detailCnName",
    width: 100,
    edit: { type: 'input', keep: true }
  },
  {
    title: "操作",
    field: "_action",
    width: 60,
    align: "center",
    render: (h, { row, index }) => {
      return (
        <div style="display:flex;">
          <el-button color="#0425dd" link plain size="small"
            style="padding: 5px 3px !important;margin: 0;" onClick={() => delRow(index)}> <i style="font-size:13px" class="el-icon-delete"></i>删除</el-button>
          {/* <el-button color="#0425dd" link plain size="small"
            style="padding: 5px 3px !important;margin: 0;margin-left:0" onClick={() => configClick(row)}> <i style="font-size:13px" class="el-icon-edit-outline"></i>配置</el-button> */}
        </div>
      )
    }
  }
])

import { columnType, dataType } from "./coderV2Options.jsx";

const editColumns = ref([
  {
    field: "columnId",
    title: "ColumnId",
    width: 120,
    align: "left",
    edit: { type: "text" },
    hidden: true,
  },
  {
    field: "columnCnName",
    title: "名称",
    fixed: true,
    width: 100,
    align: "left",
    edit: { type: "text" },
  },
  {
    field: "columnName",
    title: "字段",
    fixed: true,
    width: 100,
    align: "left",
    edit: { type: "text" },
  },
  // {
  //   field: "isKey",
  //   title: "主键",
  //   width: 90,
  //   align: "left",
  //   edit: { type: "switch" },
  // },
  {
    field: "editRowNo",
    title: "编辑行",
    width: 65,
    align: "numberbox",
    edit: { type: "text" },
    renderHeader: (h, { }) => {
      return (
        <div>
          <el-tooltip placement="top-start" title="" trigger="hover">
            {{
              default: () => {
                return (
                  <span>
                    编辑行
                    <i style="font-size:12px;margin-left:3px" class="el-icon-warning-outline"></i>
                  </span>
                )
              },
              content: () => {
                return (
                  <div>
                    如果字段需要编辑,值必须大于0,否则输入0(对应代码生成器上编辑表单配置信息，也就是字段放在表单上的位置)
                  </div>
                )
              }
            }}
          </el-tooltip>
        </div>
      )
    }
  },
  {
    field: "editType",
    title: "编辑类型",
    width: 100,
    align: "left",
    edit: { type: "select" },
    bind: { data: dataType },
  },
  {
    field: "dropNo",
    title: "数据源",
    width: 100,
    align: "left",
    bind: { data: [] },
    edit: { type: "select", data: [] },
  },
  {
    field: "isImage",
    title: "显示类型",
    hidden: false,
    width: 90,
    align: "left",
    edit: { type: "select" },
    bind: { data: columnType },
  },
  {
    field: "isReadDataset",
    title: "只读",
    width: 55,
    align: "center",
    edit: { type: "switch", keep: false },
  },
  // {
  //   field: "orderNo",
  //   title: "显示顺序",
  //   width: 90,
  //   align: "left",
  //   edit: { type: "text" },
  // },
  {
    field: "isDisplay",
    title: "是否显示",
    width: 76,
    align: "center",
    edit: { type: "switch", keep: false },
  },
  {
    field: "columnWidth",
    title: "列宽度",
    width: 65,
    align: "left",
    edit: { type: "text" },
  }
])

proxy.http.post("/api/Sys_Dictionary/GetBuilderDictionary", {}, false).then((dic) => {
  let column = editColumns.value.find((x) => {
    return x.field == "dropNo";
  });
  if (!column) return;
  let data = [{ key: "", value: "请选择" }];
  for (let index = 0; index < dic.length; index++) {
    data.push({ key: dic[index], value: dic[index] });
  }
  column.bind.data = data;
})

const editData = ref([])

const isTableNode = (node) => {
  const data = node.data || node
  const hasChildren = data?.children?.length
  return data && (data.tableName || data.name) && !hasChildren
}

const addTableFromNode = (node) => {
  const data = node.data || node
  const tableName = (data.tableName || data.name || '').trim()
  const displayName = (data.name || data.columnCNName || tableName || '').trim()
  if (!tableName) {
    proxy.$message.warning('该节点无有效表名')
    return
  }
  const exists = tableData.value.some(r => String(r.detailName || '').toLowerCase() === tableName.toLowerCase())
  if (exists) {
    proxy.$message.error(`表名【${tableName}】已存在`)
    return
  }
  tableData.value.push({ detailName: tableName, detailCnName: displayName })
}
const onTreeNodeClick = (data, node) => {
  if (isTableNode(node)) {
    addTableFromNode(node)
  }
}

const delRow = (index) => {
  if (index >= 0 && index < tableData.value.length) {
    tableData.value.splice(index, 1)
  }
}
const detailTableInfo = ref({ tableColumns: [] })

const sortFieldSelectRef = ref()

/** 与主表关联字段下拉：当前明细表列 */
const mainKeyFieldOptions = computed(() => {
  const list = (editData.value || []).map((x) => ({
    key: x.columnName,
    value: x.columnCnName ? `(${x.columnCnName})${x.columnName}` : x.columnName,
  }))
  return [{ key: '', value: '请选择' }, ...list]
})

const showDetailSortSelect = () => {
  if (!detailTableInfo.value?.table_Id) {
    proxy.$message.warning('请先点击【已选明细表】行加载明细表')
    return
  }
  if (!editData.value?.length) {
    proxy.$message.warning('请先加载表结构')
    return
  }
  const physicalName = (detailTableInfo.value.tableTrueName || detailTableInfo.value.tableName || '').trim()
  sortFieldSelectRef.value.show(detailTableInfo.value.sortName, editData.value, physicalName)
}

const onDetailSortConfirm = (jsonStr) => {
  detailTableInfo.value.sortName = jsonStr || ''
}

const onSortEnd = (rows) => {
  let orderNo = 10000;
  editData.value.forEach((x) => {
    orderNo = orderNo - 50;
    x.orderNo = orderNo;
  });
}

/** 在树中递归查找 tableName 对应的 table_Id */
const findTableIdInTree = (nodes, tableName) => {
  if (!nodes || !Array.isArray(nodes) || !tableName) return null
  const tn = String(tableName).trim()
  for (const node of nodes) {
    const n = node.data || node
    const match = (n.tableName && String(n.tableName).trim() === tn) ||
      (n.name && String(n.name).trim() === tn)
    if (match && n.id != null) return n.id
    if (n.children?.length) {
      const found = findTableIdInTree(n.children, tableName)
      if (found != null) return found
    }
  }
  return null
}
let currentTableInfo;
const configClick = async (row) => {
  const tableName = (row.detailName || '').trim()
  currentTableInfo=null;
  if (!tableName) {
    proxy.$message.warning('该行表名为空')
    return
  }
  const table_Id = findTableIdInTree(treeData.value, tableName)
  if (table_Id == null) {
    proxy.$message.error(`未在表树中找到表【${tableName}】，请确保该表已加入生成配置`)
    return
  }
  const url = `api/builder/LoadTableInfo?table_Id=${table_Id}&isTreeLoad=true`
  const res = await proxy.http.post(url, {}, true)
  if (!res.status) {
    proxy.$message.error(res.message || '加载失败')
    return
  }

  const data = res.data || {}
    currentTableInfo=data;
  if (!data.tableTrueName) data.tableTrueName = data.tableName
  const _fields = ['sortable', 'isNull', 'isReadDataset', 'isColumnData', 'isDisplay', 'isUnique']
    ; (data.tableColumns || []).forEach((item) => {
      _fields.forEach((f) => { item[f] = item[f] ?? 0 })
    })
  if (data.mainKeyField == null) data.mainKeyField = ''
  if (data.sortName == null) data.sortName = ''
  detailTableInfo.value = data
  editData.value = [...(data.tableColumns || [])]
}

/** 保存明细表配置（参数均取自 detailTableInfo） */
const saveDetailConfig = async () => {
  const info = detailTableInfo.value
  if (!info || !info.tableColumns?.length) {
    proxy.$message.warning('请先点击【已选明细表】表格中的【配置】按钮加载数据')
    return
  }
  const payload = { ...info, tableColumns: [...editData.value] }
  const x = await proxy.http.post('/api/builder/Save', payload, true)
  if (!x.status) {
    proxy.$message.error(x.message || '保存失败')
    return
  }
  proxy.$message.success('保存成功')
  if (x.data) {
    const d = x.data
    if (d.mainKeyField == null) d.mainKeyField = ''
    if (d.sortName == null) d.sortName = ''
    detailTableInfo.value = d
    editData.value = [...(d.tableColumns || [])]
  }
}

/** 同步明细表结构 */
const syncDetailTable = async () => {
  const tableName = detailTableInfo.value?.tableName
  if (!tableName) {
    proxy.$message.warning('未获取到明细表')
    return
  }
  const x = await proxy.http.post(`/api/builder/syncTable?tableName=${encodeURIComponent(tableName)}`, {}, true)
  if (!x.status) {
    proxy.$message.error(x.message || '同步失败')
    return
  }
  proxy.$message.success(x.message || '同步成功')
  const table_Id = detailTableInfo.value?.table_Id
  if (table_Id != null) {
    const res = await proxy.http.post(`/api/builder/LoadTableInfo?table_Id=${table_Id}&isTreeLoad=true`, {}, true)
    if (res.status && res.data) {
      const data = res.data
      if (!data.tableTrueName) data.tableTrueName = data.tableName
      const _fields = ['sortable', 'isNull', 'isReadDataset', 'isColumnData', 'isDisplay', 'isUnique']
        ; (data.tableColumns || []).forEach((item) => {
          _fields.forEach((f) => { item[f] = item[f] ?? 0 })
        })
      if (data.mainKeyField == null) data.mainKeyField = ''
      if (data.sortName == null) data.sortName = ''
      detailTableInfo.value = data
      editData.value = [...(data.tableColumns || [])]
    }
  }
}

const show = (detailInfo, treeDataFromParent) => {
  tableData.value = []
  searchKeyword.value = ''
  detailTableInfo.value = { tableColumns: [] }
  editData.value = []
  treeData.value = Array.isArray(treeDataFromParent)
    ? treeDataFromParent.filter(x => x && x.value != -999)
    : []

  model.value = true

  if (detailInfo?.detailName) {
    const cnArr = (detailInfo.detailCnName || '').split(',').map(s => (s || '').trim()).filter(Boolean)
    const nameArr = (detailInfo.detailName || '').split(',').map(s => (s || '').trim()).filter(Boolean)
    tableData.value = nameArr.map((name, i) => ({
      detailName: name,
      detailCnName: cnArr[i] || ''
    }))
  }
}

const onSelect = () => {
  const detailName = tableData.value.map(r => r.detailName).join(',')
  const detailCnName = tableData.value.map(r => r.detailCnName || '').join(',')
  emit('onDetailSelect', { detailName, detailCnName })
  model.value = false
}

const toDetail=()=>{
  if (!currentTableInfo?.tableName) {
    return;
  }
  const url=`${window.location.href}?table=${currentTableInfo.tableName}&dbService=${currentTableInfo.dbServer}`
    window.open(url,'_blank')
}

defineExpose({
  show
})
</script>
<style lang="less" scoped>
.detail-select-footer {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  align-items: center;
}

.detail-select-body {
  display: flex;
  min-height: 420px;
}

.detail-select-left {
  width: 220px;
  border-right: 1px solid #eee;
  display: flex;
  flex-direction: column;
  margin-right: 10px;

  .detail-select-tree-title {
    padding: 10px 12px;
    font-weight: 600;
    border-bottom: 1px solid #eee;
  }

  .detail-select-search {
    padding: 0 8px 8px;
  }

  .detail-select-tree-scroll {
    flex: 1;
    padding-right: 10px;

    :deep(.el-scrollbar__bar.is-vertical) {
      width: 2px !important;
    }
  }

  .detail-select-tree {
    :deep(.el-tree-node__content) {
      margin-top: 2px;
      height: 34px;
      line-height: 34px;
    }

    :deep(.el-tree-node) {
      position: relative;

      &::before {
        content: "";
        width: 1px;
        height: 100%;
        border-left: 1px dashed #d9d9d9;
        position: absolute;
        left: -4px;
        top: -17px;
      }

      &::after {
        content: "";
        width: 20px;
        height: 0;
        border-top: 1px dashed #d9d9d9;
        position: absolute;
        top: 19px;
        left: -4px;
      }

      &:last-child::before {
        height: 38px;
      }

      .el-tree-node__children {
        padding-left: 16px;
      }

      .el-tree-node__expand-icon.is-leaf {
        display: none;
      }
    }

    :deep(> .el-tree-node::before) {
      border-left: none;
    }

    :deep(> .el-tree-node::after) {
      border-top: none;
    }

    .tree-node-wrapper {
      display: flex;
      align-items: center;
      justify-content: space-between;
      width: 100%;
      padding-right: 4px;

      .tree-node-label {
        flex: 1;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .tree-node-select-btn {
        flex-shrink: 0;
        padding: 2px 12px;
        font-size: 12px;
        border-radius: 4px;
      }
    }
  }
}

.detail-select-table-list {
  width: 350px;
  margin-right: 10px;
  border-right: 1px solid #eee;
  padding-right: 10px;
}

.detail-select-right {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;

  .detail-select-table-title {
    padding: 10px 12px;
    font-weight: 600;
    border-bottom: 1px solid #eee;
  }
}

.detail-extra-fields {
  margin-bottom: 5px;
  // padding: 8px 10px;
  // background: #f9fafc;
  // border: 1px solid #ebeef5;
  // border-radius: 4px;
  // flex-shrink: 0;
  background: #f8faff;
  padding: 0 10px;
  border-radius: 3px;
  margin-bottom: 0px;
  border: 1px solid #eee;
}

.detail-extra-fields--row {
  display: flex;
  flex-wrap: nowrap;
  align-items: center;
  gap: 12px 16px;
}

.detail-field-cell {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.detail-field-cell--mainkey {
  flex: 0 0 auto;
}

.detail-field-cell--sort {
  flex: 1;
  min-width: 0;
}

.detail-field-label-wrap {
  flex: 0 0 100px;
}

.detail-field-label--sort {
  // flex: 0 0 84px;
  white-space: nowrap;
}

.detail-field-label {
  font-size: 13px;
  color: #212122;

  .el-icon-warning-outline {
    color: #909399;
    margin-left: 2px;
  }
}

.detail-field-control {
  flex: 1;
  min-width: 80px;
}

.detail-field-control--mainkey {
  flex: 0 0 200px;
  width: 200px;
  min-width: 200px;
}

.detail-sort-row {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
}

.detail-sort-input {
  flex: 1;
  min-width: 0;
}
</style>
