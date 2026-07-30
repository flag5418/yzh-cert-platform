<template>
  <div class="builder-left">
    <div class="coder-left-inner">
      <div class="coder-left-title">
        <i class="el-icon-receiving title-icon"></i> 代码生成配置
      </div>
      <div class="coder-left-search">
        <el-input
          v-model="searchKeyword"
          placeholder="按名称或表名搜索"
          clearable
          :prefix-icon="'Search'"
          @input="onSearchInput"
        >
        </el-input>
      </div>
      <el-scrollbar :width="1" class="coder-left-scrollbar">
        <el-tree
          ref="treeRef"
          :key="searchKeyword ? 'search' : 'all'"
          highlight-current
          node-key="id"
          class="tree-contianer"
          :data="filteredTreeData"
          :expand-on-click-node="false"
          :default-expanded-keys="effectiveExpandedKeys"
          icon="ArrowRight"
          @node-click="onNodeClick"
        >
        </el-tree>
      </el-scrollbar>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, nextTick, getCurrentInstance } from "vue";

defineOptions({ name: "coderV2Tree" });

const props = defineProps({
  orginData: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(["nodeClick"]);

const { proxy } = getCurrentInstance();
const treeRef = ref(null);
const treeData = ref([]);
const searchKeyword = ref("");

const treeDataValue = computed(() => treeData.value || []);

const filterTreeNode = (node, keyword) => {
  const kw = keyword.trim().toLowerCase();
  const match =
    (node.name && String(node.name).toLowerCase().includes(kw)) ||
    (node.tableName && String(node.tableName).toLowerCase().includes(kw));
  if (node.children?.length) {
    const filtered = node.children.map((c) => filterTreeNode(c, keyword)).filter(Boolean);
    if (match || filtered.length) {
      return { ...node, children: filtered.length ? filtered : node.children };
    }
  }
  return match ? node : null;
};

const filteredTreeData = computed(() => {
  const kw = searchKeyword.value?.trim();
  if (!kw) return treeDataValue.value;
  return treeDataValue.value.map((node) => filterTreeNode(node, kw)).filter(Boolean);
});

const effectiveExpandedKeys = computed(() => {
  const kw = searchKeyword.value?.trim();
  if (!kw) return [];
  const expandIds = [];
  const collect = (nodes) => {
    nodes.forEach((n) => {
      if (n.children?.length) {
        expandIds.push(n.id);
        collect(n.children);
      }
    });
  };
  collect(filteredTreeData.value);
  return [...new Set(expandIds)];
});

const onSearchInput = () => {
  nextTick(() => treeRef.value?.setCurrentKey(null));
};

const rebuildTree = () => {
  const list = props.orginData || [];
  list.forEach((o) => {
    if (o.children) delete o.children;
  });
  treeData.value = proxy.base.convertTree(list, (node) => {
    node.label = node.name;
    node.value = node.id;
    node.key = node.id;
  });
};

watch(
  () => props.orginData,
  () => {
    rebuildTree();
  },
  { flush: "post" }
);

const onNodeClick = (node) => {
  emit("nodeClick", node.id);
};

const setCurrentKey = (id) => {
  treeRef.value?.setCurrentKey(id);
};

defineExpose({
  setCurrentKey,
  rebuildTree,
});
</script>

<style scoped>
.builder-left {
  position: relative;
  width: 200px;
  height: 100%;
  display: flex;
  flex-direction: column;
  box-sizing: border-box;
}

.coder-left-inner {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  height: 100%;
  background: #fff;
  border: 1px solid #efefef;
  border-radius: 5px;
  box-sizing: border-box;
}

.coder-left-scrollbar {
  flex: 1;
  min-height: 0;
}

.coder-left-scrollbar :deep(.el-scrollbar__wrap) {
  overflow-x: hidden;
}

.coder-left-title {
  display: flex;
  align-items: center;
  height: 45px;
  font-size: 13px;
  font-weight: bolder;
  background: rgba(102, 177, 255, 0.059);
  padding: 6px 16px;
  border-bottom: 1px solid #efefef;
  flex-shrink: 0;
}

.coder-left-title .title-icon {
  font-size: 16px;
  margin-right: 3px;
}

.coder-left-search {
  padding: 5px;
  border-bottom: 1px solid #efefef;
  flex-shrink: 0;
}

.builder-left :deep(.coder-left-scrollbar .el-scrollbar__bar.is-vertical) {
  width: 2px !important;
}

.builder-left :deep(.tree-contianer.el-tree .el-tree-node__content) {
  height: 40px;
}

.builder-left :deep(.tree-contianer.el-tree .el-tree-node__content i),
.builder-left :deep(.tree-contianer.el-tree .el-tree-node__content span) {
  color: #363b4c;
}

.builder-left :deep(.tree-contianer.el-tree .is-current > .el-tree-node__content),
.builder-left :deep(.tree-contianer.el-tree .is-current > .el-tree-node__content:hover) {
  background-color: #ddeaff !important;
  border-radius: 5px !important;
}

.builder-left :deep(.tree-contianer.el-tree .is-current > .el-tree-node__content i),
.builder-left :deep(.tree-contianer.el-tree .is-current > .el-tree-node__content span),
.builder-left :deep(.tree-contianer.el-tree .el-tree-node__content:hover i),
.builder-left :deep(.tree-contianer.el-tree .el-tree-node__content:hover span) {
  color: #1e6fff;
}

.builder-left :deep(.tree-contianer .el-tree-node) {
  position: relative;
}

.builder-left :deep(.tree-contianer .el-tree-node::before) {
  content: "";
  width: 1px;
  height: 100%;
  border-left: 1px solid #d9d9d9;
  position: absolute;
  left: -4px;
  top: -17px;
}

.builder-left :deep(.tree-contianer .el-tree-node::after) {
  content: "";
  width: 20px;
  height: 0px;
  border-top: 1px solid #d9d9d9;
  position: absolute;
  top: 20px;
  left: -4px;
}

.builder-left :deep(.tree-contianer .el-tree-node:last-child::before) {
  height: 38px;
}

.builder-left :deep(.tree-contianer .el-tree-node__children) {
  padding-left: 16px;
}

.builder-left :deep(.tree-contianer .el-tree-node__expand-icon.is-leaf) {
  display: none;
}

.builder-left :deep(.tree-contianer > .el-tree-node::before) {
  border-left: none;
}

.builder-left :deep(.tree-contianer > .el-tree-node::after) {
  border-top: none;
}
</style>
