<template>
  <div class="notification" @click="showMsg">
    <el-badge :is-dot="msgCount > 0" :max="99" :show-zero="false" class="item" :offset="[3, -3]">
      <el-icon size="15">
        <Bell />
      </el-icon>
    </el-badge>
  </div>
  <vol-box v-model="model" :width="460" :padding="0">
    <div class="msg-header">
      <el-tabs v-model="activeName" class="msg-tabs" @tab-change="loadMessages">
        <el-tab-pane name="unread">
          <template #label>
            <span class="tab-label">未读消息
              <el-badge v-if="msgCount > 0" :value="msgCount" :show-zero="false"
                badge-style="background-color: #ff1b0b; margin-left: 4px;" />
            </span>
          </template>
        </el-tab-pane>
        <el-tab-pane label="已读消息" name="read" />
        <el-tab-pane label="全部消息" name="all" />
      </el-tabs>
      <el-button v-if="msgCount > 0" type="primary" link size="small" @click="markAllRead" class="read-all-btn">
        全部已读
      </el-button>
    </div>
    <el-scrollbar :height="400">
      <div v-if="msgList.length === 0" class="empty-state">
        <el-empty description="暂无消息" />
      </div>
      <div v-else class="msg-list">
        <div class="msg-item" v-for="(item, index) in msgList" :key="item.id || index"
          :class="{ 'msg-unread': item.isRead === 0 }"
          @click="markRead(item)">
          <div class="title">
            <span v-if="item.isRead === 0" class="unread-dot" />
            {{ item.title }}
          </div>
          <div class="desc">{{ item.content }}</div>
          <div class="bottom">
            <el-tag :type="getTagType(item.messageType)" size="small">{{ getTypeLabel(item.messageType) }}</el-tag>
            <span class="date">{{ formatDate(item.createDate) }}</span>
          </div>
        </div>
      </div>
    </el-scrollbar>
  </vol-box>
</template>

<script setup>
import VolEmpty from "@/components/basic/VolEmpty.vue";
import { ref, getCurrentInstance, onMounted } from "vue";
import { ElNotification } from "element-plus";

const { proxy } = getCurrentInstance();
const model = ref(false);
const activeName = ref("unread");
const msgCount = ref(0);
const msgList = ref([]);

const showMsg = () => {
  model.value = true;
  loadMessages();
};

const loadMessages = async () => {
  try {
    const unreadOnly = activeName.value === "unread";
    const res = await proxy.http.post("api/message/list", {
      page: 1,
      pageSize: 50,
      unreadOnly: unreadOnly ? 1 : 0,
    }, true);
    if (res.status) {
      msgList.value = res.data || [];
    }
    const countRes = await proxy.http.post("api/message/unread-count", {}, true);
    if (countRes.status) {
      msgCount.value = countRes.data || 0;
    }
  } catch (e) {
    console.error("加载消息失败", e);
  }
};

const markRead = async (item) => {
  if (item.isRead === 0) {
    try {
      await proxy.http.post(`api/message/read/${item.id}`, {}, true);
      item.isRead = 1;
      msgCount.value = Math.max(0, msgCount.value - 1);
    } catch (e) {
      console.error("标记已读失败", e);
    }
  }
};

const markAllRead = async () => {
  try {
    const res = await proxy.http.post("api/message/read-all", {}, true);
    if (res.status) {
      // 修复 Bug 2: 全部已读后立即重新加载当前 tab 的数据，确保与服务器同步
      await loadMessages();
      proxy.$message.success("全部已读");
    } else {
      proxy.$message.error(res.message || "全部已读失败");
    }
  } catch (e) {
    console.error("全部已读失败", e);
    proxy.$message.error("全部已读失败，请重试");
  }
};

const getTagType = (type) => {
  const map = { convert: "success", system: "info", task: "warning" };
  return map[type] || "info";
};

const getTypeLabel = (type) => {
  const map = { convert: "文件转换", system: "系统消息", task: "任务通知" };
  return map[type] || "消息";
};

const formatDate = (dateStr) => {
  if (!dateStr) return "";
  const d = new Date(dateStr);
  return `${d.getMonth() + 1}/${d.getDate()} ${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
};

const onSignalRMessage = (data) => {
  if (data.value === "convert_progress" || data.value === "convert_cancelled") {
    msgCount.value++;
    if (model.value) loadMessages();
    ElNotification({
      title: data.title || "文件转换",
      message: data.message || "",
      type: data.value === "convert_cancelled" ? "warning" : "success",
    });
  } else {
    msgCount.value++;
    if (model.value) loadMessages();
    ElNotification.success({
      title: data.title || "新消息",
      message: data.message || "",
    });
  }
};

defineExpose({ onSignalRMessage });

onMounted(() => {
  loadMessages();
});
</script>

<style scoped lang="less">
.notification {
  outline: none;
  color: #000;
}

.msg-header {
  position: relative;
  padding: 12px 16px 0 16px;

  .read-all-btn {
    position: absolute;
    right: 16px;
    top: 14px;
    z-index: 10;
  }

  .tab-label {
    display: inline-flex;
    align-items: center;
    white-space: nowrap;
  }
}

// 修复 Bug 1: 消除 el-tabs__content 的默认 padding 导致的顶部空白
::v-deep(.el-tabs__content) {
  padding: 0 !important;
  min-height: 0 !important;
}

::v-deep(.el-tab-pane) {
  padding: 0 !important;
}

.msg-list {
  .msg-item {
    border-bottom: 1px solid #eee;
    padding: 10px 16px;
    cursor: pointer;
    transition: background 0.15s;

    &:hover {
      background: #f9f9f9;
    }

    &.msg-unread {
      background: #f0f7ff;
    }

    .title {
      font-weight: 600;
      font-size: 13px;
      color: #303133;
      display: flex;
      align-items: center;
      gap: 6px;
      line-height: 1.5;
    }

    .desc {
      margin-top: 4px;
      line-height: 1.4;
      font-size: 12px;
      color: #909399;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .bottom {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-top: 6px;
      font-size: 12px;
      color: #909399;

      .date {
        flex-shrink: 0;
        margin-left: 8px;
      }
    }
  }
}

.unread-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #ff1b0b;
  flex-shrink: 0;
}

.empty-state {
  padding: 60px 20px;
}

::v-deep(.el-tabs__header) {
  margin: 0;
}

// 已移至上方统一定义，避免重复设置导致空白

::v-deep(.el-tabs__nav) {
  width: 100%;
}

::v-deep(.el-tabs__item) {
  padding: 0 16px;
}
</style>
