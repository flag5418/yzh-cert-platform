/**
 * YZH 统一图标管理（对齐 vidlang app_icons.dart）
 *
 * 规则：
 * 1. 页面禁止直接 `import { ArrowLeft } from '@element-plus/icons-vue'`
 * 2. 一律经语义命名访问：`<el-icon><IconBack /></el-icon>` 或 `<component :is="YzhIcon.back" />`
 * 3. 换图标 / 换图标库：只改本文件一个地方
 */
import {
  ArrowLeft,
  ArrowRight,
  Menu,
  Close,
  Plus,
  Delete,
  Edit,
  EditPen,
  Search,
  Refresh,
  Download,
  Upload,
  Folder,
  FolderOpened,
  FolderChecked,
  Document,
  DocumentChecked,
  CopyDocument,
  Check,
  CircleCheck,
  CloseBold,
  Warning,
  InfoFilled,
  Clock,
  Loading,
  MagicStick,
  ChatDotRound,
  QuestionFilled,
  Setting,
} from '@element-plus/icons-vue'

/** 统一图标表（对象形式，支持 :is 动态绑定） */
export const YzhIcon = {
  /* 导航 */
  back: ArrowLeft,
  forward: ArrowRight,
  menu: Menu,
  close: Close,
  /* 操作 */
  add: Plus,
  delete: Delete,
  edit: Edit,
  editPen: EditPen,
  search: Search,
  refresh: Refresh,
  download: Download,
  upload: Upload,
  copy: CopyDocument,
  /* 文件 */
  folder: Folder,
  folderOpen: FolderOpened,
  folderChecked: FolderChecked,
  file: Document,
  fileChecked: DocumentChecked,
  /* 状态 */
  success: Check,
  circleSuccess: CircleCheck,
  error: CloseBold,
  warning: Warning,
  info: InfoFilled,
  loading: Loading,
  pending: Clock,
  help: QuestionFilled,
  setting: Setting,
  /* AI */
  analyze: MagicStick,
  prompt: ChatDotRound,
}

/* ===== 语义常量（模板中直接使用，如 <el-icon><IconBack /></el-icon>） ===== */
export const IconBack = ArrowLeft
export const IconForward = ArrowRight
export const IconMenu = Menu
export const IconClose = Close
export const IconAdd = Plus
export const IconDelete = Delete
export const IconEdit = Edit
export const IconEditPen = EditPen
export const IconSearch = Search
export const IconRefresh = Refresh
export const IconDownload = Download
export const IconUpload = Upload
export const IconCopy = CopyDocument
export const IconFolder = Folder
export const IconFolderOpen = FolderOpened
export const IconFolderChecked = FolderChecked
export const IconFile = Document
export const IconFileChecked = DocumentChecked
export const IconSuccess = Check
export const IconCircleSuccess = CircleCheck
export const IconError = CloseBold
export const IconWarning = Warning
export const IconInfo = InfoFilled
export const IconLoading = Loading
export const IconPending = Clock
export const IconHelp = QuestionFilled
export const IconSetting = Setting
export const IconAnalyze = MagicStick
export const IconPrompt = ChatDotRound

export default YzhIcon
