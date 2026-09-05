<script setup lang="ts">
/**
 * YzhMenu - 自定义侧边栏菜单组件
 * 
 * 解决 Element Plus el-menu 层级样式问题
 * 支持多级菜单、折叠、激活态、引导线
 */
import { ref, computed } from 'vue'
import type { MenuNode } from './types'

const props = defineProps<{
  /** 菜单数据 */
  data: MenuNode[]
  /** 当前激活菜单 ID */
  activeId: string | number
  /** 是否折叠 */
  collapsed?: boolean
}>()

const emit = defineEmits<{
  (e: 'select', node: MenuNode): void
}>()

// 展开的菜单项
const openedKeys = ref<Set<string>>(new Set())

// 递归渲染菜单项
function renderMenuItem(node: MenuNode, level: number = 0) {
  const hasChildren = node.children && node.children.length > 0
  const isActive = props.activeId === node.id || 
    (node.children?.some(child => props.activeId === child.id))
  
  // 切换展开/收起
  function toggleOpen() {
    if (hasChildren) {
      const newOpened = new Set(openedKeys.value)
      if (newOpened.has(String(node.id))) {
        newOpened.delete(String(node.id))
      } else {
        newOpened.add(String(node.id))
      }
      openedKeys.value = newOpened
    }
  }
  
  // 点击菜单项
  function handleClick() {
    if (!hasChildren) {
      emit('select', node)
    } else {
      toggleOpen()
    }
  }
  
  // 菜单项类名
  const itemClass = computed(() => ({
    'yzh-menu__item': level === 0,
    'yzh-menu__submenu-item': level > 0,
    'is-active': isActive,
    'is-opened': hasChildren && openedKeys.value.has(String(node.id))
  }))
  
  return h('li', {
    class: itemClass.value,
    onClick: handleClick
  }, [
    // 图标
    node.icon ? h('i', { class: `bi ${node.icon} yzh-menu__icon` }) : null,
    // 文字
    h('span', { class: 'yzh-menu__text' }, node.label),
    // 展开箭头（仅一级菜单显示）
    hasChildren && level === 0 ? h('i', { class: 'bi bi-chevron-right yzh-menu__arrow' }) : null,
    // 子菜单
    hasChildren ? h('ul', { 
      class: ['yzh-menu__submenu', openedKeys.value.has(String(node.id)) ? 'is-opened' : ''] 
    }, node.children!.map(child => renderMenuItem(child, level + 1))) : null
  ])
}

// 计算菜单类名
const menuClass = computed(() => ({
  'yzh-menu': true,
  'yzh-menu--collapsed': props.collapsed
}))
</script>

<template>
  <ul :class="menuClass">
    <template v-for="node in data" :key="String(node.id)">
      <template v-if="node.children && node.children.length">
        <!-- 有子菜单 -->
        <li 
          class="yzh-menu__item"
          :class="{ 'is-active': activeId === node.id, 'is-opened': openedKeys.has(String(node.id)) }"
          @click="toggleOpen(String(node.id))"
        >
          <i v-if="node.icon" :class="`bi ${node.icon} yzh-menu__icon`"></i>
          <span v-if="!collapsed" class="yzh-menu__text">{{ node.label }}</span>
          <i v-if="!collapsed" class="bi bi-chevron-right yzh-menu__arrow"></i>
          
          <!-- 子菜单 -->
          <ul v-if="openedKeys.has(String(node.id))" class="yzh-menu__submenu">
            <template v-for="child in node.children" :key="String(child.id)">
              <template v-if="child.children && child.children.length">
                <!-- 三级菜单 -->
                <li 
                  class="yzh-menu__submenu-item"
                  :class="{ 'is-active': activeId === child.id }"
                  @click="toggleOpen(String(child.id))"
                >
                  <span class="yzh-menu__text">{{ child.label }}</span>
                  <i class="bi bi-chevron-right yzh-menu__arrow"></i>
                  
                  <ul v-if="openedKeys.has(String(child.id))" class="yzh-menu__submenu yzh-menu__submenu-level2">
                    <li
                      v-for="grandchild in child.children"
                      :key="String(grandchild.id)"
                      class="yzh-menu__submenu-item"
                      :class="{ 'is-active': activeId === grandchild.id }"
                      @click="emit('select', grandchild)"
                    >
                      <span class="yzh-menu__text">{{ grandchild.label }}</span>
                    </li>
                  </ul>
                </li>
              </template>
              <template v-else>
                <!-- 二级菜单 -->
                <li
                  class="yzh-menu__submenu-item"
                  :class="{ 'is-active': activeId === child.id }"
                  @click="emit('select', child)"
                >
                  <span class="yzh-menu__text">{{ child.label }}</span>
                </li>
              </template>
            </template>
          </ul>
        </li>
      </template>
      <template v-else>
        <!-- 无子菜单 -->
        <li
          class="yzh-menu__item"
          :class="{ 'is-active': activeId === node.id }"
          @click="emit('select', node)"
        >
          <i v-if="node.icon" :class="`bi ${node.icon} yzh-menu__icon`"></i>
          <span class="yzh-menu__text">{{ node.label }}</span>
        </li>
      </template>
    </template>
  </ul>
</template>

<style scoped>
/* 菜单基础样式 */
.yzh-menu {
  list-style: none;
  margin: 0;
  padding: 8px 0;
}

.yzh-menu__item {
  position: relative;
  display: flex;
  align-items: center;
  height: 48px;
  padding: 0 20px;
  color: rgba(255, 255, 255, 0.65);
  cursor: pointer;
  transition: all 0.2s;
  user-select: none;
  
  &:hover {
    background: rgba(255, 255, 255, 0.08);
    color: #fff;
  }
  
  &.is-active {
    background: var(--yzh-color-primary, #1e3a8a);
    color: #fff;
    font-weight: 600;
    
    &::before {
      content: '';
      position: absolute;
      left: 0;
      top: 8px;
      bottom: 8px;
      width: 3px;
      background: #fff;
    }
  }
}

.yzh-menu__icon {
  font-size: 18px;
  margin-right: 10px;
  display: flex;
  align-items: center;
}

.yzh-menu__text {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.yzh-menu__arrow {
  font-size: 12px;
  transition: transform 0.3s;
  margin-left: 8px;
}

.yzh-menu__item.is-opened > .yzh-menu__arrow {
  transform: rotate(90deg);
}

/* 子菜单 */
.yzh-menu__submenu {
  list-style: none;
  margin: 0;
  padding: 0;
  background: rgba(0, 0, 0, 0.2);
  
  &.is-opened {
    display: block;
  }
  
  &:not(.is-opened) {
    display: none;
  }
}

.yzh-menu__submenu-item {
  position: relative;
  display: flex;
  align-items: center;
  height: 44px;
  padding: 0 20px 0 44px; /* 二级菜单向右偏移 24px */
  color: rgba(255, 255, 255, 0.55);
  cursor: pointer;
  transition: all 0.2s;
  
  &:hover {
    background: rgba(255, 255, 255, 0.05);
    color: #fff;
  }
  
  &.is-active {
    background: rgba(30, 58, 138, 0.6);
    color: #fff;
  }
  
  /* 左侧引导线 */
  &::before {
    content: '';
    position: absolute;
    left: 20px;
    top: 0;
    bottom: 0;
    width: 1px;
    background: rgba(255, 255, 255, 0.15);
  }
}

/* 三级菜单 */
.yzh-menu__submenu-level2 {
  .yzh-menu__submenu-item {
    padding-left: 68px; /* 再偏移 24px */
    
    &::before {
      left: 44px;
    }
  }
}

/* 折叠状态 */
.yzh-menu--collapsed {
  .yzh-menu__item {
    justify-content: center;
    padding: 0;
    
    .yzh-menu__icon {
      margin-right: 0;
    }
    
    .yzh-menu__text,
    .yzh-menu__arrow {
      display: none;
    }
  }
  
  .yzh-menu__submenu-item {
    justify-content: center;
    padding: 0;
    
    &::before {
      display: none;
    }
  }
}
</style>
