<template>
  <div class="vol-menu-filter" v-if="$global.navSearch">
    <el-select
      placement="bottom"
      v-model="searchValue"
      clearable
      filterable
      remote
      reserve-keyword
      :placeholder="$ts('搜索') + '...'"
      :remote-method="remoteMethod"
      @change="selectChange"
    >
      <template #prefix><i class="el-icon-search"></i></template>
      <el-option v-for="item in menuData" :key="item.id" :label="item.name" :value="item.id" />
    </el-select>
  </div>
</template>
<script setup>
import store from '@/store/index.js'
import { ref } from 'vue'
import { useRouter } from 'vue-router'
const router = useRouter()
const props = defineProps({
  onSelect: {
    type: Function,
    default: (x) => {}
  }
})

const searchValue = ref('')
const remoteMethod = (query) => {
  if (!query) {
    return []
  }
  menuData.value = store.state.permission.filter((x) => {
    return (
      x.enable == 1 &&
      x.name.indexOf(query) != -1 &&
      !store.state.permission.some((c) => {
        return c.parentId === x.id && x.id
      })
    )
  })
}
const menuData = ref([])

const selectChange = (id) => {
  let _item = store.state.permission.find((c) => {
    return c.id == id
  })
  if (!_item) {
    return
  }

  if (_item.linkType == 1) {
    window.open(_item.url || _item.path, '_blank')
    return
  }
  const item = _item

  props.onSelect(_item.id, _item)
  router.push({ path: _item.path || '', query: _item.query })
}
</script>
<style lang="less" scoped>
.vol-menu-filter {
  align-items: center;
  display: flex;
  margin-right: 20px;

  :deep(.el-select) {
    width: 240px !important; // 适当增加宽度以适应 17px 字体
  }

  :deep(.el-select__wrapper) {
    height: 32px !important;
    border-radius: 2px !important; /* 严谨圆角 */
    background: rgba(0, 0, 0, 0.04) !important;
    box-shadow: none !important;
    transition: all 0.2s;

    .el-select__placeholder,
    .el-select__selected-item,
    input {
      color: #64748b !important; /* 默认灰色文字 */
    }

    &:hover,
    &.is-focus {
      background: #ffffff !important;
      box-shadow: 0 0 0 1px var(--yzh-color-primary) inset !important;

      .el-select__placeholder,
      .el-select__selected-item,
      input {
        color: #1e293b !important; /* 聚焦时深色文字 */
      }
    }
  }
}
</style>
