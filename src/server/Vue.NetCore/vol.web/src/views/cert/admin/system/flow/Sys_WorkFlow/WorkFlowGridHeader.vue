<template>
  <vol-box :lazy="false" v-model="model" :title="isAdd ? '新建流程设计' : '流程架构设计'" :width="width" :padding="0">
    <div class="workflow-editor-wrapper" :style="{ height: height + 'px' }">
      <div class="editor-header-bar">
        <div class="header-left">
          <el-tag effect="dark" type="primary" class="version-tag">V1.0</el-tag>
          <span class="workflow-name">{{ flow?.formFields?.WorkName || '未命名流程' }}</span>
        </div>
        <div class="header-right">
          <el-button @click="model = false" size="default">退出设计</el-button>
          <el-button type="primary" @click="save" size="default" class="save-btn">发布流程</el-button>
        </div>
      </div>
      <flow-panel ref="flow"></flow-panel>
    </div>
    <template #footer>
      <!-- 底部留空，使用顶部工具栏 -->
      <div style="display: none"></div>
    </template>
  </vol-box>
</template>

<style scoped lang="less">
.workflow-editor-wrapper {
  display: flex;
  flex-direction: column;
  background-color: #f8fafc;
}

.editor-header-bar {
  height: 64px;
  background: #fff;
  border-bottom: 1px solid #edf2f7;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px;
  z-index: 100;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.02);
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.version-tag {
  border-radius: 4px;
  font-weight: 700;
}

.workflow-name {
  font-size: 18px;
  font-weight: 700;
  color: #1a202c;
}

.save-btn {
  padding-left: 24px;
  padding-right: 24px;
  font-weight: 700;
  box-shadow: 0 4px 12px rgba(24, 144, 255, 0.3);
}

:deep(.flow-panel) {
  flex: 1;
  min-height: 0;
}
</style>

<script setup>
import FlowPanel from '@/components/workflow/panel'
import { ref, onMounted, nextTick, getCurrentInstance } from 'vue'

const props = defineProps({
  form: {
    type: Boolean,
    default: false
  }
})

const { proxy } = getCurrentInstance();

const emit = defineEmits(['parentCall', 'save'])

const flow = ref(null)
const nodeList = ref([])
const lineList = ref([])
const model = ref(false)
const height = ref(500)
const width = ref(1200)
const row = ref(null)
const isAdd = ref(false)


height.value = document.body.clientHeight - 140
let clientWidth = document.body.clientWidth * 0.9
width.value = clientWidth > 1800 ? 1800 : clientWidth

// 打开弹窗方法
const open = (rowData, isCopyClick) => {
  row.value = rowData
  model.value = true
  isAdd.value = isCopyClick === true || Object.keys(row.value).length === 0

  // 初始化节点和连线数据
  if (row.value.NodeConfig) {
    nodeList.value = JSON.parse(row.value.NodeConfig)
  } else {
    nodeList.value = []
  }

  if (row.value.LineConfig) {
    lineList.value = JSON.parse(row.value.LineConfig)
  } else {
    lineList.value = []
  }

  nextTick(() => {
    //表单流程
    if (props.form) {
      flow.value.formRules.flat().forEach(x => {
        if (['WorkName', 'WorkTable', 'Weight'].includes(x.field)) {
          x.required = false;
          x.hidden = true;
        }
      })
      //生成表单查询字段条件
      nextTick(() => {
        const fn = proxy.base.getItem('form:filter')
        fn(rowData.FormOptions)
      })
    }
    if (flow.value) {
      flow.value.dataReload(
        {
          lineList: lineList.value,
          nodeList: nodeList.value
        },
        isAdd.value
      )
      // 复制表单字段
      Object.assign(flow.value.formFields, row.value)

      // 新增时初始化表单
      if (isAdd.value && isCopyClick !== true) {
        flow.value.formFields.WorkName = ''
        flow.value.formFields.WorkTable = ''
        flow.value.formFields.WorkTableName = ''
        flow.value.formFields.Remark = ''
        flow.value.formFields.Weight = 1
        flow.value.formFields.AuditingEdit = 0
      }
      if (isCopyClick === true) {
        flow.value.formFields.WorkName += '复制'
      }

      // 表单重置逻辑
      if (flow.value) {
        // 注释掉的原逻辑，根据实际需求恢复
        // flow.value.$refs.nodeForm.$refs.filter.getOptions(row.value.WorkTable)
      } else {
        flow.value.$refs.form.reset(
          Object.keys(row.value).length
            ? row.value
            : { WorkName: '', WorkTable: '', WorkTableName: '', Remark: '', Weight: 1 }
        )
      }
    }
  })
}

// 获取步骤值方法
const getStepValue = (item) => {
  let val
  // 根据审核类型获取对应值
  if (item.auditType === 1) {
    val = item.userId // 用户审批
  } else if (item.auditType === 2) {
    val = item.roleId // 角色审批
  } else {
    val = item.deptId // 部门审批
  }

  if (!val) return ''
  return Array.isArray(val) ? val.join(',') : val
}

// 保存方法
const save = () => {
  if (!flow.value) return

  let mainData = JSON.parse(JSON.stringify(flow.value.formFields))

  if (!props.form) {
    // 表单验证
    if (!mainData.WorkName) {
      proxy.$message.error('请填写左侧表单【流程名称】')
      return
    }
    if (!mainData.WorkTable) {
      proxy.$message.error('请选择左侧表单【流程实例】')
      return
    }
  }
  // 处理节点数据
  let nodeList = flow.value.data.nodeList
  let nodeListOptions = JSON.parse(JSON.stringify(nodeList))

  nodeListOptions.forEach((item) => {
    if (item.filters && item.filters.data) {
      item.filters.data = undefined
    }
  })
  mainData.NodeConfig = JSON.stringify(nodeListOptions)

  // 处理连线数据
  let lineList = flow.value.data.lineList
  lineList = JSON.parse(JSON.stringify(lineList))
  lineList.forEach((item) => {
    if (item.filters) {
      item.filters.forEach((x) => {
        if (x.data) {
          x.data = []
        }
      })
    }
  })
  mainData.LineConfig = JSON.stringify(lineList)

  // 验证开始节点
  let rootNode = nodeList
    .filter(c => c.type === 'start')
    .map(c => ({
      StepId: c.id,
      StepName: c.name,
      StepAttrType: c.type,
      StepAuditType: null,
      ParentId: [''],
      Filters: c.filters
    }))

  if (rootNode.length === 0) {
    return proxy.$message.error('请添加流程开始节点')
  }

  if (rootNode.length > 1) {
    return proxy.$message.error('只能选择一个流程开始节点')
  }

  // 验证结束节点
  let endNodeCount = nodeList.filter(c => c.type === 'end').length
  if (endNodeCount === 0) {
    return proxy.$message.error('请选择左侧【流程结束】节点')
  }

  if (endNodeCount > 1) {
    return proxy.$message.error('只能选择一个【流程结束】节点')
  }

  // 验证开始节点是否被回连
  if (lineList.some(c => c.to === rootNode[0].id)) {
    return proxy.$message.error('不允许开始节点回连')
  }

  for (let index = 0; index < rootNode.length; index++) {
    const node = rootNode[index]
    node.OrderId = index
    //这里有一节点有多个上级节点的时候数据重复了，比如线束节点

    lineList
      .filter((c) => {
        return c.from == node.StepId
      })
      .forEach((c) => {
        let item = nodeList.find((x) => {
          return x.id == c.to
        })
        let _obj = rootNode.find((x) => {
          return x.StepId === item.id
        })
        if (_obj) {
          _obj.ParentId.push(node.StepId)
        } else {
          rootNode.push({
            ParentId: [node.StepId], //父级id
            StepId: item.id,
            StepName: item.name,
            StepAttrType: item.type, //节点类型.start开始，end结束
            StepType: item.auditType, //审核类型,角色，用户，部门(这里后面考虑同时支持多个角色、用户、部门)
            StepValue: getStepValue(item),
            AuditRefuse: item.auditRefuse, //审核未通过(返回上一节点,流程重新开始,流程结束)
            AuditBack: item.auditBack, //驳回(返回上一节点,流程重新开始,流程结束)
            AuditMethod: item.auditMethod, //审批方式(启用会签)
            SendMail: item.sendMail, //审核后发送邮件通知：
            Filters: item.filters
          })
        }
      })
    // rootNode.push(...data);
  }


  // 处理节点父ID和过滤器
  rootNode.forEach((item) => {
    item.ParentId = item.ParentId.filter((x) => {
      return x
    }).join(',')
    if (item.Filters && item.Filters.length) {
      item.Filters = item.Filters.map((m) => {
        return {
          field: m.field,
          filterType: m.filterType,
          value: Array.isArray(m.value) ? m.value.join(',') : m.value
        }
      })
      item.Filters = JSON.stringify(item.Filters)
    } else {
      item.Filters = null
    }
  })
  // 验证节点属性
  for (let i = 0; i < rootNode.length; i++) {
    const step = rootNode[i]
    if (Array.isArray(step.StepValue)) {
      step.StepValue = step.StepValue.join(',')
    }

    if (!step.StepName) {
      return proxy.$message.error('请输入节点名称')
    }
    if (
      step.StepType !== 4 &&
      step.StepType !== 5 &&
      step.StepType !== 6 &&
      step.StepType !== 7 &&
      step.StepAttrType === 'node' &&
      !step.StepValue
    ) {
      return proxy.$message.error(`请选择【${step.StepName}】的审批类型`)
    }
  }
  // 准备提交参数
  const params = {
    mainData: mainData,
    detailData: rootNode,
    delKeys: []
  }
  if (props.form) {
    emit('save', params)
    return;
  }


  // 发送请求
  const url = `api/Sys_WorkFlow/${isAdd.value ? 'add' : 'update'}`
  proxy.http.post(url, params, true).then(result => {
    if (!result.status) {
      return proxy.$message.error(result.message)
    }
    proxy.$message.success('保存成功')
    model.value = false
    emit('parentCall');
  })
}

const close = () => {
  model.value = false
}

// 暴露方法给父组件
defineExpose({
  open,
  close
})
</script>