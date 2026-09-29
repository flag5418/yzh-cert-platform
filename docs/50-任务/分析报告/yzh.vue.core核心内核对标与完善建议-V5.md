# yzh.vue.core 核心内核对标与完善建议 V5

> 参考基准：`/Volumes/Expand/wangqingquan/Documents/work/work/测绘成果管理系统` + `YZH架构/YZH.WPF.Core`
> 对标对象：`yzh.vue.core/src/logic/{SingleTableCore,TreeTableCore}.ts` 及周边
> 结论摘要：**WPF 用"组合 + 委托 + 能力驱动显隐"做到 25 个页面只有 16 个 XAML；Vue 内核已具备 70% 骨架，但缺 3 个关键机制（能力驱动显隐 / 表单渲染后钩子 / 选择器内核），加上 8 项配置消费缺口。**

---

## 〇、本次取证的参考项目全貌

### 0.1 不是一个项目，是一个「Core + 多项目 + 共享业务库」的三层家族

```
work/work/
├── YZH架构/                      ← 框架层（30 个 Core 项目）
│   ├── YZH.WPF.Core      9,953 行 ← WPF 前端框架（yzh.vue.core 的原型）
│   ├── YZH.Avalonia.Lib          ← Avalonia 前端框架（同层）
│   ├── YZH.Blazor.Core / YZH.Blazor.Stand
│   ├── YZH.Web.Core      3,362 行 ← 后端框架（BaseEntityController 在此）
│   ├── YZH.Api.Core / YZH.Stand(153 文件) / YZH.DataBase
│   ├── YZH.Cad.Share / YZH.Arcgis.Core / YZH.Windows.Core
│   └── …
├── 房产测绘系统/src/Share/
│   ├── YZH.Survey.Lib   139 文件 / 32,950 行 ← 共享业务库（被多项目引用）
│   └── YZH.Survey.Stand
└── 测绘成果管理系统/              ← 业务项目（本次主样本）
    ├── YZH.SurveyResult.App     303 行（薄壳：App.xaml + AcadMgr）
    ├── YZH.SurveyResult.Lib   8,656 行 ← 业务主体
    ├── YZH.SurveyResult.Api   2,516 行
    └── YZH.SurveyResult.Stand    32 行
```

**关键证据**：`测绘成果管理系统/YZH.SurveyResult.Lib.csproj` 直接 `ProjectReference` 到
`..\..\YZH架构\YZH.WPF.Core\YZH.WPF.Core.csproj` + `..\..\YZH架构\YZH.Stand\YZH.Stand.csproj` + `..\..\房产测绘系统\src\Share\YZH.Survey.Lib\YZH.Survey.Lib.csproj`
→ **`{项目}.Lib` = 业务层；`YZH.*.Core` = 框架层；`Share/*.Lib` = 跨项目业务共享层。三层分明。**

### 0.2 「庞大系统没有几个真页面」——量化

| 指标 | 实测值 |
|---|---|
| 菜单派发的业务页面数（`FrmMainMenuModel.CommandAction` 的 `case`） | **25** |
| 该项目全部 XAML | **16 个 / 1,612 行**（最大 204 行） |
| XAML 性质 | **全部是查询条件面板 / CAD 专用面板**，**没有一个 CRUD 页面** |
| `new BaseMainModel<T>` 实例化 | **25 次** |
| `new BaseEditModel<T>` 实例化 | **29 次** |
| `new SelectModel<T>` 实例化 | **44 次** |
| `FillGrid<T>` 调用 | **17 次** |
| `BaseView/*.xaml`（框架基类视图） | 5 个：`FrmBaseMain`(167) / `FrmTree`(752) / `FrmSelect`(459) / `FrmBaseEdit`(185) / `FrmInput`(96) |

→ **基类实例化 : XAML ≈ 7 : 1。CRUD 页面的 XAML 数量 = 0。**

---

## 一、「基类即页面」的完整机制（四层）

### 第 1 层 · 菜单层：DB 菜单表 + 一个 `switch`

`YZH.SurveyResult.Lib/ViewModel/Menu/FrmMainMenuModel.cs`（172 行）：

```csharp
public void Instance()
{
    var frm = new FrmMainMenu();                                       // 唯一的菜单 XAML
    new TreeMenuControlModel().Instance(frm._tv_tree, CommandAction);  // 菜单树从 DB 加载
    Cad.Share.GlobalData.ps.Add("系统菜单", hostMenu);                  // 挂进 AutoCAD 面板
}

private void CommandAction(string command)          // ★ 全部页面的唯一入口
{
    switch (command.ToUpper())
    {
        case "JCZL":      new JczlModel().Instance();            break;  // 基础资料
        case "RYGL":      new DepartAndUserModel().Instance();   break;  // 人员管理
        case "JSGL":      new RoleAndUserModel().Instance();     break;  // 角色管理
        case "LDBA":      new EstateBuildModel().Instance();     break;  // 楼栋备案
        case "LDXXGL":    new EstateBuildEditModel().Instance(); break;  // 交易楼栋信息管理
        … 共 25 个 case
    }
}
```

**机制**：菜单不是代码，是 **DB 数据**（`t_collectmenu` / `t_menu`，字段 `menu_name` / `ml`=命令码 / `ordercode`）。
→ 加一个页面 = **加一条菜单数据 + 一个 case**，不需要新的窗体文件。

### 第 2 层 · 页面层：`BaseMainModel<T>` 组合装配

`YZH.WPF.Core/ViewModel/BaseMainModel.cs`（122 行）核心是 `Instance()`：

```csharp
public void Instance(Grid? gMain = null)
{
    var gridconfig = GridConfig.GetGridConfig<T>(_configName);   // ① 配置驱动
    if (gridconfig == null) { MessageModel.ShowMessage("未找到表格配置信息"); return; }

    if (GetTreeNodes != null) Nodes = WaitModel.Wait(() => GetTreeNodes());
    else { Frm.BorderTv.Visibility = Visibility.Collapsed;       // ② 无树 → 自动隐藏左栏
           Frm.BorderMain.Margin = new Thickness(0); }

    if (SearchAction == null) Frm.G_Other.Visibility = Visibility.Collapsed;  // ③ 无搜索 → 隐藏
    else { Frm.BtnSearch.Click += …; AllItems = WaitModel.Wait(() => SearchAction(SearchText)); }

    Frm.GV.FillGrid<T>(gridconfig, CheckFlag, _fillMode);        // ④ 一行渲染整表

    // ⑤ ★★ 能力驱动显隐：没挂委托的按钮自动消失
    if (Tree_Add_Action == null)  Frm.BtnTree_Add.Visibility  = Visibility.Collapsed;
    if (Tree_Edit_Action == null) Frm.BtnTree_Edit.Visibility = Visibility.Collapsed;
    if (Tree_Del_Action == null)  Frm.BtnTree_Del.Visibility  = Visibility.Collapsed;
    if (Add_Action == null)       Frm.Btn_Add.Visibility      = Visibility.Collapsed;
    if (Edit_Action == null)      Frm.Btn_Edit.Visibility     = Visibility.Collapsed;
    if (Del_Action == null)       Frm.Btn_Del.Visibility      = Visibility.Collapsed;
    if (Refresh_Action == null)   Frm.Btn_Refresh.Visibility  = Visibility.Collapsed;
    if (Export_Action == null)    Frm.Btn_Export.Visibility   = Visibility.Collapsed;

    Frm.DataContext = this;
    if (gMain == null) { /* 包成独立窗口 BaseWinModel，1000×800 */ }
    else { gMain.Children.Clear(); gMain.Children.Add(Frm); }    // ⑥ 或注入宿主 Grid
}
```

**业务侧只需挂委托 + 调 `Instance()`**。最小完整样本（`YL_TodoModel.cs`，**全文件 77 行，零 XAML**）：

```csharp
public class YL_TodoModel : ObservableObject
{
    private BaseMainModel<Survey_ToDo> bm;

    public void Instance()
    {
        bm = new BaseMainModel<Survey_ToDo>("待办事宜");                     // 标题即配置入口

        var hd = new HttpRequestData { ApiRoute = "YLSurvey_ToDo", RequestMethod = HttpMethod.GET };
        bm.AllItems = hd.GetItemList<Survey_ToDo>();                        // 取数（原子能力）

        bm.Frm.BtnAdd.PrimaryButton("受理");                                 // 改按钮文案+样式
        bm.Add_Action = new RelayCommand(() => SurveyAudit());               // 挂委托
        bm.Frm.Gv.MouseDoubleClick += (o, e) => SurveyAudit();               // 挂交互

        bm.Frm.BtnDel.WarningButton("退回");
        bm.Del_Action = new RelayCommand(() => { /* 退回逻辑 */ });

        bm.Instance();                                                      // 装配 → 页面出现
    }
}
```

### 第 3 层 · 表单层：`BaseEditModel<T>` + `XamlHelper.CreateControls` 动态建控件

`BaseEditModel.Instance(title, entity, operType, configName, groupIndex)`：

```csharp
XamlHelper.CreateControls(Frm.Grid_Control, Entity, configName, groupIndex);  // ★ 反射+配置建控件
if (InitAction != null) InitAction();                                        // ★ 控件建好后的钩子

OkCommand = new RelayCommand(() => {
    if (DefineSaveAction != null) { DefineSaveAction(); }        // ① 完全自定义保存
    else {
        XamlHelper.SaveCheck(Entity, Frm.Grid_Control, Gridconfig, out Err, groupIndex);  // ② 校验
        if (SaveCheckAction != null && SaveCheckAction() == false) return;                // ③ 业务校验
        if (operType == 0) { if (AddAction  != null) AddAction();  else Entity.Insert(); } // ④ 新增
        else               { if (EditAction != null) EditAction(); else Entity.Update(); } // ⑤ 修改
        if (SaveFinishedAction != null) SaveFinishedAction();                              // ⑥ 保存后
        Frm.Close();
    }
});
```

**6 个扩展点**（`DefineSaveAction` / `SaveCheckAction` / `AddAction` / `EditAction` / `SaveFinishedAction` / `DefineCloseAction`）+ **1 个渲染后钩子**（`InitAction`）+ **默认落库**（`Entity.Insert()`）。

`XamlHelper.CreateControls`（`YZH.WPF.Core/Helper/XamlHelper.cs:127`）的要点：
- 遍历 `dataConfig.Columns`，每列一行（48px），`c.FieldName.ToUpper() == p.Name.ToUpper()` **大小写不敏感**匹配实体属性
- 7 种控件：`TextBox` / `PasswordBox` / `CheckBox`(映射 int 0/1) / `Label` / `DatePicker`(空值自动填 `DateTime.Now`) / `Decimal` / `ButtonEdit`(双击清空)
- **控件命名约定** `T_{FieldName.ToUpper()}`、标签 `LB_{FieldName}` → 业务可用 `Frm.FindName("T_PROJECT_NAME")` 精调
- **`GroupIndex` 分组**：`ct.IsEnabled = c.GroupIndex.Split(',').FirstOrDefault(t => t == GroupIndex) != null` → **同一实体、不同分组显示不同可编辑字段**
- **`!c.YXK` → 标签变棕色（必填标记）**；`SaveCheck` 里 `if (!c.YXK && value == null) err = "【x】字段不能为空"`
- 另有 `GridControls`：用 `c.Row/c.Col/c.RowSpan/c.ColSpan` 做**自由网格布局**（与 `CreateControls` 的顺序流并列的第二套）

**真实复杂样本**（`EstateBuildEditModel.cs`，557 行）展示精调模式：

```csharp
private void BuildEdit(tb_building building, int OperType)
{
    var bem = new BaseEditModel<tb_building>();
    bem.InitAction += () => {
        var T_PROJECT_NAME = bem.Frm.FindName("T_PROJECT_NAME") as ButtonEdit;  // ★ 按约定名取控件
        T_PROJECT_NAME.IsTextEditable = true;                                   // 精调
        T_DISTRINCT.SetSelectMethod("AREANO", item => { … });                    // ★ 字典选择器一行接入
        T_PROJECT_NAME.DefaultButtonClick += (o, e) => {
            var sl = new SelectModel<t_dict_info>("请选择");                     // ★ 复用选择器内核
            sl.GetAllItems += () => HttpHelper.GetItemList<t_dict_info>(new { dict_id = "CHLX" });
            sl.GetItemFinished += item => { T_REAL_FLAG.Text = item.dict_key; };
            sl.Instance();
        };
    };
    bem.Instance("楼栋信息", building, OperType, "tb_buildinfo.xml");
}
```

### 第 4 层 · 持久层：实体自带 CRUD → 一个控制器服务全系统

`YZH.WPF.Core/Extensions/Extension.BaseEntity.cs`：

```csharp
public static bool Insert<T>(this T t, bool showErrMessage = true)
    => WaitModel.Wait(() => HttpHelper.HttpPost("BaseEntity/Insert", t, GetHeader<T>(), null, showErrMessage), showErrMessage).SuccessFlag;
// Update / Delete / InsertList / UpdateList / DeleteList 同形

private static Dictionary<string, string> GetHeader<T>() => new() {
    { "TableName",    EntityHelper.GetTableName<T>() },   // ★ 表名走 HTTP Header
    { "AssemblyName", typeof(T).Assembly.GetName().FullName },
    { "FullName",     typeof(T).FullName }
};
```

后端 `YZH.Web.Core/Controllers/BaseEntityController.cs`（**204 行**）：

| 端点 | 能力 |
|---|---|
| `BaseEntity/Insert` / `Update` / `Delete` | 单条 CRUD（`Request.GetBaseEntityItem(out tableName)`） |
| `BaseEntity/InsertList` / `UpdateList` / `DeleteList` | **批量 + 显式事务 + 回滚** |
| `BaseEntity/GetItem` / `GetItemLists` | `SqlHelper.GetQuerySql(tableName, out err, out dbParams, postParams)` **按实体属性动态建 SQL** |

附加原子能力：`SqlHelper.ClearXh(item)`（新增前清流水号）、`SqlHelper.CheckXh(item, tableName)`（修改时校验流水号）。

→ **一个 204 行控制器 = 全系统所有实体的 CRUD + 动态查询。** 这就是你说的"原子能力遍布整个后端逻辑"。
→ **也是 `BaseEditModel` 能做到 `Entity.Insert()` 零业务代码落库的前提。**

---

## 二、WPF 基类能力 → Vue 内核 逐项对照（本报告核心）

图例：✅ 已具备 ｜ ⚠️ 部分具备/语义偏移 ｜ ❌ 缺失

| # | 能力 | WPF 实现 | Vue 现状 | 判定 |
|---|---|---|---|---|
| 1 | 配置驱动列 | `FillGrid<T>(gridconfig, …)` 反射匹配 + 按类型自动格式化（DateTime→`yyyy-MM-dd`、double/decimal→`0.00`） | `toTableColumns` 从 `Columns` 生成 | ⚠️ 无类型自动格式化 |
| 2 | **能力驱动按钮显隐** | `if (Xxx_Action == null) Btn.Visibility = Collapsed` ×9 | `toToolbarActions`: `if (tb.Add !== false) push(…)` **默认全显示** | ❌ **P0** |
| 3 | 委托式动作 | `Add_Action` / `Edit_Action` / `Del_Action` / `SearchAction` / `GetTreeNodes` | `registerHandler(key, fn)` + `dispatch()` | ✅（但未驱动 UI，见 #2） |
| 4 | **表单渲染后钩子** | `BaseEditModel.InitAction`（`CreateControls` 之后）→ `FindName("T_XXX")` 精调 | 只有 `onAfterInit`（**配置加载后**，表单未渲染）；精调需写页面模板 slot | ❌ **P0** |
| 5 | **泛型选择器内核** | `SelectModel<T>(title, configName, checkFlag)`：单选(双击/确定) / 多选(勾选列)，44 处复用 | 有 `YzhTreeTableSelector.vue`(417) + `YzhTreeTableCheckSelector.vue`(736) **两个组件**，`logic/` 下**无对应 Core** | ❌ **P0** |
| 6 | 可选带树（单表→左树右表） | 一个 `BaseMainModel`：`GetTreeNodes == null` → 自动隐藏左栏 | `SingleTableCore` 无树；要树必须换 `TreeTableCore`（页面重写） | ⚠️ **设计选择**（见 §三 G4） |
| 7 | 分组字段（多组） | `c.GroupIndex.Split(',').FirstOrDefault(t => t == GroupIndex)` → 字段可属**多组** | `fieldGroupIndex !== editMode` → **单值** | ⚠️ P1 |
| 8 | 自由网格布局 | `GridControls` 用 `Row/Col/RowSpan/ColSpan` | 契约有 4 字段，`toFormFields` 只算 `span = floor(24/cols)`；`YzhForm` 用 `el-row/el-col` **顺序流** | ❌ P1 |
| 9 | `Format` 格式化 | `DefineColumn.Format` + 类型推断 | 契约有 `Format`，`toTableColumns` **不消费**（`YzhTableColumn.formatter` 恒 undefined） | ❌ P1 |
| 10 | 必填标记 | `!YXK` → 标签棕色 | `required: !c.Yxk` → Element 星号 | ✅ |
| 11 | 校验 | `SaveCheck`：按 `BCFlag` + `GroupIndex` 逐字段校验 | Element `rules`（`required`） | ⚠️ 未按 `BcFlag`/`GroupIndex` 裁剪 |
| 12 | 提交裁剪 | `SaveCheck` 只处理本组 `BCFlag` 字段 | `submitForm` 提交 `{...formData}` **全量** | ⚠️ P1 |
| 13 | 行号列 | `LoadingRow` → `e.Row.Header = index+1` | 无 `type="index"` | ❌ P2 |
| 14 | 冻结列 | `FrozenColumnCount = 5` | 只有 `col.fixed` 单列声明 | ❌ P2 |
| 15 | 斑马纹 | `AlternationCount = 2` | 无 `striped` | ❌ P2 |
| 16 | 合计行 | `DefineColumn.SumFlag` | 契约无、组件无 | ❌ P2 |
| 17 | 导出复用表格列 | `Export<T>()` 从 `datagrid.Columns` 反推 `GridConfig` → 用户不用传字段 | `exportData(format, fields?)` 需显式传 `fields` | ⚠️ P2 |
| 18 | 双击行 | `Frm.Gv.MouseDoubleClick += …`（业务可挂） | `YzhTable` 无 `row-dblclick`，内核无钩子 | ❌ P2 |
| 19 | 多选 | `CheckFlag` + `GetSelectItems/CheckAll/UnCheckAll` | `type="selection"` + `selectedRows` | ✅ |
| 20 | 原位替换保序 | `ChangeItem<T>(dg, item)` | `replaceRowByCode(code, row)` | ✅ |
| 21 | 全局等待遮罩 | `WaitModel.Wait(fn)` | 仅 `loading`（per-table） | ❌（V4 已列为能力缺口） |
| 22 | 独立窗口宿主 | `BaseWinModel`（`frm` + `Instance(title, content)` + 宽高） | `YzhDialog` + `YzhPageLayout` | ✅ |
| 23 | 实体自带 CRUD | `entity.Insert()/Update()/Delete()` → `BaseEntity/*` | `apiPost('/add'|'/update'|'/delete')` | ✅（等价，需显式调用） |
| 24 | 分页 | 无（全量 `GetItemList`） | `pagination` + `/filter` | ✅ **Vue 更强** |
| 25 | 排序/固定/对齐/字典 | 部分无 | `Sortable`/`Fixed`/`Align`/`DictCode` | ✅ **Vue 更强** |

**统计：✅ 9 项 ｜ ⚠️ 8 项 ｜ ❌ 8 项。**

---

## 三、必须完善的问题清单

> 判据：**凡"不解决就必须退回手写模板"的，进 P0**——因为那会直接摧毁"基类即页面"这个目标。

### 🔴 P0-1｜能力驱动按钮显隐（G1）

| 项 | 内容 |
|---|---|
| **问题** | Vue 工具栏/行按钮**默认全显示**，与业务是否实现该能力无关 |
| **证据** | `adapters/entityAdapters.ts:202-205`：`if (tb.Add !== false) btns.push({key:'add',…})`；`:231-232`：`if (rb.Edit !== false) base.push({key:'edit',…})`。对照 `BaseMainModel.cs:95-104` 的 9 行 `Visibility = Collapsed` |
| **后果** | ① 业务没写 `Add` handler 的页面，按钮仍在 → 点了无反应/报错；② 每个页面要么改 JSON（`"Toolbar":{"Add":false}`）、要么覆盖 `toolbarActions` getter → **"零代码"退化** |
| **建议做法** | 在 `SingleTableCore` 增加 **能力注册 → 可见性推导**：<br>① 新增 `protected get supportedActions(): Set<string> \| null`（`null`=沿用 config 全显示，保持向后兼容）<br>② `registerHandler(key, fn)` 时自动 `this._registered.add(key)`<br>③ `toolbarActions` getter 过滤：`actions.filter(a => !this.supportedActions \|\| this.supportedActions.has(a.key))`<br>④ 提供 `markSupported(...keys)` 供内置动作（add/edit/delete/export）显式声明<br>**关键：默认行为不变**（`supportedActions === null` → 全显示），避免破坏存量页面 |
| **成本** | ~1.5h（内核 + 3 个样板页验证） |

```ts
// 建议形态（SingleTableCore.ts）
private _registered = new Set<string>()

registerHandler(key: string, fn: ActionHandler<V>): void {
  this.handlers.set(key, fn)
  this._registered.add(key)          // ★ 注册即声明能力
}

/** 页面声明"我支持哪些动作"；返回 null = 沿用配置（向后兼容） */
protected get supportedActions(): Set<string> | null { return null }

get toolbarActions(): YzhAction[] {
  const all = toToolbarActions(this.config.value)
  const sup = this.supportedActions
  if (!sup) return all
  const effective = new Set([...sup, ...this._registered])
  return all.filter(a => effective.has(a.key))
}
```

### 🔴 P0-2｜缺「表单渲染后」钩子（G3）

| 项 | 内容 |
|---|---|
| **问题** | 内核没有"表单 DOM 已就绪"的回调 → **无法像 WPF 那样"配置驱动 + 少量精调"** |
| **证据** | `SingleTableCore` 只有 `onAfterInit()`（`init()` 内、`loadConfig()` 之后，此时 `dialogVisible=false`、表单未渲染）。WPF 对应物是 `BaseEditModel.cs:54` `if (InitAction != null) InitAction();`（在 `CreateControls` 之后）。真实用法见 `EstateBuildEditModel.cs:166-225`（`bem.Frm.FindName("T_PROJECT_NAME")` + `SetSelectMethod` + 事件挂接） |
| **后果** | 需要精调的页面**只能退回手写 `<YzhForm>` 模板 + 手动 `ref`**，配置驱动形同废弃 |
| **建议做法** | ① `openAddDialog/openEditDialog/openDetailDialog` 在 `dialogVisible.value = true` 后 `await nextTick()`，再调 `protected onFormRendered(mode: 'add'\|'edit'\|'detail')`<br>② `YzhForm` 增 `defineExpose({ getFieldRef(prop) })`，内核转发为 `protected getFieldRef(prop: string): any`（**等价 `FindName("T_XXX")`**）<br>③ 保留 `formRef` 供整体操作 |
| **成本** | ~1.5h（内核 + `YzhForm` + `YzhFormDialog`） |

```ts
// 建议形态
protected async onFormRendered(_mode: 'add' | 'edit' | 'detail'): Promise<void> {}

async openEditDialog(row: V) {
  this.dialogMode.value = 'edit'
  this.formGroupIndex.value = '0'
  this.editingRow.value = row
  this.initFormData(row)
  this.dialogVisible.value = true
  await nextTick()                        // ★ 等表单挂载
  await this.onFormRendered('edit')       // ★ 业务在此精调
}

/** 等价 WPF Frm.FindName("T_" + prop.toUpperCase()) */
protected getFieldRef(prop: string): any {
  return this._formRef?.getFieldRef?.(prop)
}
```

### 🔴 P0-3｜缺泛型「选择器内核」（G5）

| 项 | 内容 |
|---|---|
| **问题** | 选择/多选弹窗有**组件**无**内核** → 每个用到选择器的页面都要自己写一遍取数/单选/多选/回填 |
| **证据** | `logic/` 目录仅 `{SingleTableCore, TreeTableCore, AssociationTreeCore, LinkTableCore, CheckTreeCore, TreeSide}`，**无 Select 相关 Core**；组件侧 `YzhTreeTableSelector.vue`(417) + `YzhTreeTableCheckSelector.vue`(736) 合计 1,153 行需页面自驱。对照 WPF `SelectModel.cs`（132 行，44 处复用） |
| **后果** | 1,153 行组件的编排逻辑被复制到 N 个页面；且单选/多选两套组件 API 不统一 |
| **建议做法** | 新增 `logic/SelectCore.ts`（不继承 `SingleTableCore`，因为它是**弹窗内嵌**场景，生命周期不同）：<br>```ts<br>export abstract class SelectCore<T> {<br>  abstract controllerName: string<br>  checkFlag = false            // false=单选，true=多选<br>  abstract getAllItems(): Promise<T[]><br>  onItemFinished?: (item: T) => void<br>  onItemsFinished?: (items: T[]) => void<br>  async open(): Promise<void>  // 装配 → 打开 → 回填 → 关闭<br>}<br>```<br>配 `useSelect(SelectClass)`；`YzhTreeTableSelector`/`...CheckSelector` 收敛为**哑组件**（收 `columns` + `rows` + `v-model`） |
| **成本** | ~2.5h（内核 + 2 组件改造 + 3 处页面回归） |
| **备注** | 这是**唯一一个需要改组件**的 P0，其余两项只动内核 |

### 🟡 P1-1｜`Format` 契约有、适配层不消费（G9）

- **证据**：`ColumnConfig.Format?: string`（`types/contracts.ts:149`）已定义；`toTableColumns`（`entityAdapters.ts:86-102`）**未读取**；`YzhTableColumn.formatter` 因此恒为 `undefined`。
- **建议**：① `toTableColumns` 消费 `Format` → 生成 `formatter`；② 未配 `Format` 时按 `fieldSchema.Type` 兜底（等价 WPF `Extension.DataGrid.cs:154-161` 的 `PropertyType.Contains("DateTime")` → `yyyy-MM-dd`、`double/decimal` → `0.00`）；③ 支持常用格式串（`yyyy-MM-dd` / `0.00` / `#,##0.00` / `percent`）。
- **成本**：~1h。

### 🟡 P1-2｜`Row/Col/RowSpan/ColSpan` 自由布局未实现（G8）

- **证据**：契约 4 字段齐备（`contracts.ts:150-153`）；`toFormFields` 只算 `span = Math.floor(24 / layoutCols)`；`YzhForm.vue:229` 用 `:span="field.span || colSpan"` 顺序流。
- **建议**：`YzhForm` 增 `layout?: 'flow' | 'grid'`；`grid` 模式用 `el-row` 的嵌套 `el-col`（或 CSS Grid）消费 `Row/Col/RowSpan/ColSpan`；`toFormFields` 透传 4 字段。
- **成本**：~1.5h。
- **优先级说明**：**当前项目不一定需要**（顺序流够用），但**契约已有字段却不消费**是"撒谎的契约"，至少要在文档里标注"暂不支持"，或实现它。

### 🟡 P1-3｜`GroupIndex` 不支持多组（G7）

- **证据**：`entityAdapters.ts:137`：`const isDisabledByGroupIndex = editMode !== '0' && fieldGroupIndex !== editMode`（单值比较）。WPF：`c.GroupIndex.Split(',').FirstOrDefault(t => t == GroupIndex)`（多组）。
- **建议**：改为 `!(c.GroupIndex || '0').split(',').includes(editMode)`。
- **成本**：~15min。

### 🟡 P1-4｜提交未按 `BcFlag` / `GroupIndex` 裁剪（G12）

- **证据**：`SingleTableCore.submitForm()` 提交 `{ ...this.formData }` **全量**；WPF `SaveCheck` 只处理本组 `BCFlag` 字段（`XamlHelper.cs:592-594`）。
- **后果**：只读字段/非本组字段也会提交，后端可能误改（尤其配合 `GroupIndex='99'` 详情模式时）。
- **建议**：`submitForm` 按 `formGroupIndex` + `BcFlag` 裁剪 payload；**或在文档中明确"全量提交"是有意设计**并说明理由。
- **成本**：~45min（需确认是否有页面依赖全量提交）。

### 🟡 P1-5｜`Mask` 契约漂移（G10）

- **证据**：后端 `YZH.Core.Stand/Models/Config/ColumnConfigDto.cs:32` 有 `public bool Mask`；前端 `types/contracts.ts` 的 `ColumnConfig` **无 `Mask`**；但组件层 `YzhTableColumn.mask`（`components/table/types.ts:90`）有。
- **建议**：补进 `ColumnConfig`；`toTableColumns` 消费（`mask: c.Mask || undefined`）。
- **成本**：~10min。

### 🟢 P2｜表格能力补齐（来自 `Extension.DataGrid.cs` 对照）

| ID | 能力 | WPF 证据 | 建议 | 成本 |
|---|---|---|---|---|
| P2-1 | 行号列 | `LoadingRow` → `e.Row.Header = index+1` | `YzhTable` 增 `showIndex` prop → `type="index"` 列 | 15min |
| P2-2 | 冻结前 N 列 | `FrozenColumnCount = 5` | 增 `frozenColumnCount` prop（对前 N 列自动 `fixed="left"`） | 30min |
| P2-3 | 斑马纹 | `AlternationCount = 2` | 增 `striped` prop | 10min |
| P2-4 | 合计行 | `DefineColumn.SumFlag` | 契约加 `SumFlag` + `YzhTable` 增 `summary` 插槽 | 1.5h |
| P2-5 | 导出复用列 | `Export<T>()` 从 `datagrid.Columns` 反推 `GridConfig` | `exportData()` 默认从 `this.columns` 推导 `fields`（不传即自动） | 30min |
| P2-6 | 双击行 | `Frm.Gv.MouseDoubleClick` | 内核加 `onRowDblClick(row)` 钩子 + `YzhTable` 透传 `row-dblclick` | 30min |

### 🟢 P3｜契约命名治理（G17）——这是 V4 P0-1「文档没跟上代码」的具体一例

`YZH.Stand/Models/DefineColumn.cs`（WPF 权威）vs `YZH.Core.Stand/Models/Config/ColumnConfigDto.cs`（新架构）：

| 概念 | `YZH.Stand`（WPF） | `YZH.Core.Stand`（新） | 差异 |
|---|---|---|---|
| 显示标志 | `XSFlag` | `XsFlag` | 大小写 |
| 保存标志 | `BCFlag` | `BcFlag` | 大小写 |
| 允许为空 | `YXK` | `Yxk` | 大小写 |
| 默认值 | `MRZ` | `Mrz` | 大小写 |
| 顺序号 | `SXH` | **无** | 能力缺失 |
| 合计标志 | `SumFlag` | **无** | 能力缺失 |
| 选择配置 | `ChooseStr` | **无** | 能力缺失 |
| 查询操作符 | `OPERTYPE`（`OperType` 枚举） | 移到 `SearchFields[].Operator` | 位置迁移 |
| 控件可用 | `EnableFlag` | `Enable` | 命名 |
| 掩码 | 无 | `Mask` | 新增 |

**判定**：**不是 bug**——新架构是有意重构（`OPERTYPE` 移到搜索配置更合理，`Mask` 是新增能力）。
**但必须做一件事**：在 `docs/10-YZH架构/03-前端架构.md` 的契约章节写明 **"谁是权威"** 与 **"哪些能力有意未迁移"**。
否则未来 .NET 端（WPF/Avalonia）与 Vue 端互操作时，`XSFlag` ↔ `XsFlag` 这类差异会**静默不匹配**——正是铁律七要防的那类 bug。

- **成本**：文档 ~30min（并入 V4 的 P0-1）

---

## 四、建议的落地顺序（按杠杆率，不按严重性）

| 序 | 动作 | 内容 | 成本 | 理由（F1/F2/F3） |
|---|---|---|---|---|
| **1** | **G1 能力驱动显隐** | `supportedActions` + `registerHandler` 自动登记 | 1.5h | **F2 复利最高**：一次改动让所有页面都能"只声明能力"；且**默认行为不变**（零回归风险） |
| **2** | **G3 表单渲染后钩子** | `onFormRendered` + `getFieldRef` | 1.5h | **F2**：解锁"配置驱动 + 少量精调"，这是 WPF 最常用模式；不改组件（仅 `YzhForm` 加 `defineExpose`） |
| **3** | **G9 + G7 + G10 配置消费三连** | `Format` / `GroupIndex` 多组 / `Mask` | 1.25h | **F1**：契约已有字段却不消费 = "撒谎的契约"，三个小改一起做，成本 <30min/项 |
| **4** | **G5 选择器内核** | `SelectCore` + 2 组件哑化 | 2.5h | **F2**：44 处复用规模的逻辑收口；但**要动组件**，建议排在 1-3 之后（有回归面） |
| **5** | P2 表格能力 | 行号/冻结/斑马/合计/导出推导/双击 | ~3.2h | **F1**：单项都 <30min（除合计）；按实际页面需要挑做，不必一次做完 |
| **6** | P3 契约命名治理 | 写进 `03-前端架构.md` | 0.5h | 并入 V4 的 P0-1，一起做 |
| **—** | G8 自由布局 | `layout: 'grid'` | 1.5h | **延后**：当前顺序流够用，先文档标注"暂不支持" |
| **—** | P1-4 提交裁剪 | 按 `BcFlag`/`GroupIndex` 裁剪 | 0.75h | **需你决策**：要先确认是否有页面依赖全量提交 |

**前 3 项合计 4.25h，即可补齐"基类即页面"的三个关键机制。**

---

## 五、明确**不建议**做的事（避免过度对齐）

| 项 | 不建议的理由 |
|---|---|
| **照搬 WPF 的"单表内核可选带树"**（G4） | Vue 拆 `SingleTableCore` / `TreeTableCore`（且后者**已继承前者**）比 WPF 一个类塞两形态更清晰。**要做的是补"升级路径文档"**（从单表升到左树右表的 checklist），而不是合并内核 |
| **照搬 `BaseEntity/{Insert,Update,…}` 泛型端点** | 你说"对后端基类基本满意"；且新架构 `/api/{controller}/{add,update,delete}` 每实体一套，**与 ApiCode/角色-接口关联机制绑定**（改控制器名/动作名会导致授权静默断裂）。**动它的收益 < 风险**。仅作为"若将来要做零代码 CRUD"的备选记录 |
| **照搬 `WaitModel.Wait()` 全局遮罩** | Vue 的 per-table `loading` 粒度更细、更符合 SPA 交互；全局遮罩在 Web 上体验反而更差。V4 列的"Loading 组件缺口"应理解为**补齐 `YzhLoading` 组件**（局部等待用），而非全局遮罩 |
| **收窄 `index.ts` 导出面** | V4 已收回此条：框架层本就该暴露完整能力 |
| **删 `treeUtils`/`treeOps`/`YzhTreeTableSelector` 等** | V4 已收回：它们是能力储备，且有 WPF 对应物（`BaseView/FrmSelect.xaml` ↔ `YzhTreeTableSelector`）。**本轮进一步确认**：`TreeModel.CreateNodes<T>(allItems, displayField, parentField, keyField="code")` ↔ Vue `treeUtils.buildTree`，**Vue 侧是超集**（`treeUtils` 24 个导出 + `treeOps` 9 个导出：`buildTree`/`flattenTree`/`getAncestors`/`getPath`/`searchWithAncestors`/`validate`/`hasCycle`/`addNode`/`removeSubtree`/`moveSubtree`/`diff`…，WPF 侧仅 `CreateNodes` 一个方法） |

---

## 六、一个额外确认：`useTable.ts` 可以结案了

V4 遗留的待判项 —— `composables/useTable.ts`（49 行）：

```
全仓 grep "useTable" → 仅 1 处命中：
  yzh.vue.core/src/composables/index.ts:5:  export { useTable } from './useTable'
```

**零消费方**（只在 barrel 里再导出）。其职责已被 `composables/useCores.ts`（`useSingleTable` / `useTreeTable` / `useCheckTree` / `useLinkTable`）完全取代。
→ **判定：可删（~49 行）**，与 V4 的 `useAuth.ts`(39) / `types/ApiResponse.ts`(9) / `utils/http.ts`(102) 一并处理。

---

## 七、附：本版取证命令

```bash
# 0. 参考项目家族
ls /Volumes/Expand/wangqingquan/Documents/work/work/
ls /Volumes/Expand/wangqingquan/Documents/work/work/YZH架构/

# 1. 项目规模
cd 测绘成果管理系统 && for d in YZH.SurveyResult.App YZH.SurveyResult.Lib YZH.SurveyResult.Api; do
  echo "$d xaml=$(find $d -name '*.xaml' -not -path '*/bin/*' -not -path '*/obj/*' | wc -l) \
cs=$(find $d -name '*.cs' -not -path '*/bin/*' -not -path '*/obj/*' | wc -l)"; done

# 2. ★ 「没有几个真页面」的证据：25 个 case / 16 个 xaml
grep -c "case " 测绘成果管理系统/YZH.SurveyResult.Lib/ViewModel/Menu/FrmMainMenuModel.cs   # → 25
find 测绘成果管理系统/YZH.SurveyResult.Lib -name "*.xaml" -not -path "*/bin/*" -not -path "*/obj/*" | wc -l  # → 16

# 3. ★ 基类复用规模
grep -rn "new BaseMainModel<" 测绘成果管理系统 房产测绘系统/src/Share | grep -v "/bin/\|/obj/" | wc -l  # → 25
grep -rn "new BaseEditModel<" 测绘成果管理系统 房产测绘系统/src/Share | grep -v "/bin/\|/obj/" | wc -l  # → 29
grep -rn "new SelectModel<"   测绘成果管理系统 房产测绘系统/src/Share | grep -v "/bin/\|/obj/" | wc -l  # → 44

# 4. ★ 能力驱动显隐
sed -n '95,104p' YZH架构/YZH.WPF.Core/ViewModel/BaseMainModel.cs

# 5. ★ 表单渲染后钩子
sed -n '47,54p'  YZH架构/YZH.WPF.Core/ViewModel/BaseEditModel.cs
sed -n '127,130p' YZH架构/YZH.WPF.Core/Helper/XamlHelper.cs

# 6. ★ 后端一个控制器服务全系统
wc -l YZH架构/YZH.Web.Core/Controllers/BaseEntityController.cs   # → 204
grep -n "HttpPost(\"BaseEntity/" YZH架构/YZH.Web.Core/Controllers/BaseEntityController.cs

# 7. ★ Yxk 的第三重确认（WPF 侧）
grep -n "YXK" YZH架构/YZH.Stand/Models/DefineColumn.cs        # → /// 允许空标志
sed -n '357,358p;611,615p' YZH架构/YZH.WPF.Core/Helper/XamlHelper.cs   # !YXK → 棕色 / 不能为空

# 8. Vue 侧缺口
grep -n "tb.Add !== false\|rb.Edit !== false" src/certplatform-web/yzh.vue.core/src/adapters/entityAdapters.ts
grep -n "onAfterInit\|onFormRendered\|getFieldRef" src/certplatform-web/yzh.vue.core/src/logic/SingleTableCore.ts
ls src/certplatform-web/yzh.vue.core/src/logic/     # → 无 SelectCore
grep -n "c.Format\|c.Mask\|RowSpan" src/certplatform-web/yzh.vue.core/src/adapters/entityAdapters.ts  # → 0 命中
```
