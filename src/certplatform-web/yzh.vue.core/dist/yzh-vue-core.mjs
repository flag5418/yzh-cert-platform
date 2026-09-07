var Sn = Object.defineProperty;
var En = (e, t, n) => t in e ? Sn(e, t, { enumerable: !0, configurable: !0, writable: !0, value: n }) : e[t] = n;
var Ce = (e, t, n) => En(e, typeof t != "symbol" ? t + "" : t, n);
import { defineComponent as Te, computed as ae, resolveComponent as C, openBlock as g, createBlock as x, reactive as Fe, watch as We, createElementBlock as U, createElementVNode as D, Fragment as se, renderList as be, unref as Rn, toDisplayString as j, normalizeStyle as Le, withKeys as On, withCtx as P, createCommentVNode as W, createVNode as H, createTextVNode as V, renderSlot as F, ref as M, onMounted as vn, resolveDirective as An, normalizeClass as Ge, withDirectives as Tn, mergeProps as fe, createSlots as xn, resolveDynamicComponent as dt } from "vue";
import { ElMessage as Cn } from "element-plus";
const Pn = /* @__PURE__ */ Te({
  __name: "YzhPagination",
  props: {
    page: {},
    pageSize: {},
    total: {},
    pageSizes: { default: () => [10, 20, 50, 100] },
    layout: { default: "total, sizes, prev, pager, next, jumper" },
    background: { type: Boolean, default: !0 },
    size: { default: "default" }
  },
  emits: ["update:page", "update:pageSize"],
  setup(e, { emit: t }) {
    const n = e, o = t, r = ae({
      get: () => n.page,
      set: (a) => o("update:page", a)
    }), s = ae({
      get: () => n.pageSize,
      set: (a) => o("update:pageSize", a)
    });
    return (a, i) => {
      const u = C("el-pagination");
      return g(), x(u, {
        "current-page": r.value,
        "onUpdate:currentPage": i[0] || (i[0] = (p) => r.value = p),
        "page-size": s.value,
        "onUpdate:pageSize": i[1] || (i[1] = (p) => s.value = p),
        total: e.total,
        "page-sizes": e.pageSizes,
        layout: e.layout,
        background: e.background,
        size: e.size
      }, null, 8, ["current-page", "page-size", "total", "page-sizes", "layout", "background", "size"]);
    };
  }
}), le = (e, t) => {
  const n = e.__vccOpts || e;
  for (const [o, r] of t)
    n[o] = r;
  return n;
}, kn = /* @__PURE__ */ le(Pn, [["__scopeId", "data-v-13879d57"]]), zn = { class: "yzh-search-bar" }, Un = { class: "yzh-search-bar__inner" }, Nn = { class: "yzh-search-bar__fields" }, Dn = { class: "yzh-search-bar__field-row" }, Ln = { class: "yzh-search-bar__label" }, Fn = { class: "yzh-search-bar__actions" }, Bn = /* @__PURE__ */ Te({
  __name: "YzhSearchBar",
  props: {
    fields: {},
    defaultValues: {},
    cols: { default: 2 },
    maxFields: { default: 2 },
    inputWidth: { default: "200px" }
  },
  emits: ["search", "reset"],
  setup(e, { emit: t }) {
    const n = e, o = t, r = Fe({});
    We(
      () => n.defaultValues,
      (u) => {
        u && (Object.keys(r).forEach((p) => delete r[p]), Object.assign(r, u));
      },
      { immediate: !0, deep: !0 }
    );
    const s = n.fields.slice(0, n.maxFields);
    function a() {
      const u = {};
      s.forEach((p) => {
        const c = r[p.prop];
        c !== void 0 && c !== "" && !(Array.isArray(c) && c.length === 0) && (u[p.prop] = c);
      }), o("search", u);
    }
    function i() {
      s.forEach((u) => {
        delete r[u.prop];
      }), o("reset");
    }
    return (u, p) => {
      const c = C("el-input"), m = C("el-input-number"), b = C("el-option"), w = C("el-select"), R = C("el-date-picker"), T = C("el-button");
      return g(), U("div", zn, [
        D("div", Un, [
          D("div", Nn, [
            (g(!0), U(se, null, be(Rn(s), (d) => (g(), U("div", {
              key: d.prop,
              class: "yzh-search-bar__field"
            }, [
              D("div", Dn, [
                D("label", Ln, j(d.label), 1),
                D("div", {
                  class: "yzh-search-bar__input-wrap",
                  style: Le({ width: e.inputWidth })
                }, [
                  !d.type || d.type === "text" ? (g(), x(c, {
                    key: 0,
                    modelValue: r[d.prop],
                    "onUpdate:modelValue": (f) => r[d.prop] = f,
                    placeholder: d.placeholder || `请输入${d.label}`,
                    clearable: "",
                    onKeyup: On(a, ["enter"])
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : d.type === "number" ? (g(), x(m, {
                    key: 1,
                    modelValue: r[d.prop],
                    "onUpdate:modelValue": (f) => r[d.prop] = f,
                    placeholder: d.placeholder || `请输入${d.label}`
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : d.type === "select" ? (g(), x(w, {
                    key: 2,
                    modelValue: r[d.prop],
                    "onUpdate:modelValue": (f) => r[d.prop] = f,
                    placeholder: d.placeholder || `请选择${d.label}`,
                    clearable: "",
                    filterable: ""
                  }, {
                    default: P(() => [
                      (g(!0), U(se, null, be(d.options || [], (f) => (g(), x(b, {
                        key: f.value,
                        label: f.label,
                        value: f.value
                      }, null, 8, ["label", "value"]))), 128))
                    ]),
                    _: 2
                  }, 1032, ["modelValue", "onUpdate:modelValue", "placeholder"])) : d.type === "date" ? (g(), x(R, {
                    key: 3,
                    modelValue: r[d.prop],
                    "onUpdate:modelValue": (f) => r[d.prop] = f,
                    type: "date",
                    placeholder: d.placeholder || `请选择${d.label}`,
                    "value-format": "YYYY-MM-DD"
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : d.type === "dateRange" ? (g(), x(R, {
                    key: 4,
                    modelValue: r[d.prop],
                    "onUpdate:modelValue": (f) => r[d.prop] = f,
                    type: "daterange",
                    placeholder: d.placeholder || `请选择${d.label}`,
                    "value-format": "YYYY-MM-DD",
                    "range-separator": "至",
                    "start-placeholder": "开始日期",
                    "end-placeholder": "结束日期"
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : W("", !0)
                ], 4)
              ])
            ]))), 128)),
            p[0] || (p[0] = D("div", { class: "yzh-search-bar__spacer" }, null, -1))
          ]),
          D("div", Fn, [
            H(T, {
              type: "primary",
              onClick: a
            }, {
              default: P(() => [...p[1] || (p[1] = [
                D("i", { class: "bi bi-search" }, null, -1),
                V(" 查询 ", -1)
              ])]),
              _: 1
            }),
            H(T, { onClick: i }, {
              default: P(() => [...p[2] || (p[2] = [
                D("i", { class: "bi bi-arrow-counterclockwise" }, null, -1),
                V(" 重置 ", -1)
              ])]),
              _: 1
            })
          ])
        ])
      ]);
    };
  }
}), $n = /* @__PURE__ */ le(Bn, [["__scopeId", "data-v-0412b8ca"]]), Vn = { class: "yzh-toolbar" }, In = { class: "yzh-toolbar__left" }, jn = { class: "yzh-toolbar__right" }, qn = /* @__PURE__ */ Te({
  __name: "YzhToolbar",
  setup(e) {
    return (t, n) => (g(), U("div", Vn, [
      D("div", In, [
        F(t.$slots, "left", {}, void 0, !0)
      ]),
      D("div", jn, [
        F(t.$slots, "right", {}, void 0, !0)
      ])
    ]));
  }
}), Mn = /* @__PURE__ */ le(qn, [["__scopeId", "data-v-ad5ba70b"]]), Hn = { class: "yzh-table" }, Yn = { class: "yzh-column-settings" }, Kn = { class: "yzh-column-settings__body" }, Wn = { class: "yzh-column-settings__footer" }, Jn = { class: "yzh-table__wrapper" }, Xn = { key: 1 }, Zn = { class: "yzh-table__empty" }, Gn = {
  key: 1,
  class: "yzh-table__error"
}, Qn = {
  key: 2,
  class: "yzh-table__pagination"
}, eo = /* @__PURE__ */ Te({
  __name: "YzhTable",
  props: {
    columns: {},
    dataLoader: {},
    searchFields: {},
    selectable: { type: Boolean, default: !1 },
    showPagination: { type: Boolean, default: !0 },
    pageSize: { default: 20 },
    defaultSort: {},
    height: {},
    rowKey: { default: "id" },
    emptyText: { default: "暂无数据" },
    toolbar: { type: [Boolean, Object], default: !0 },
    searchMaxFields: { default: 2 }
  },
  emits: ["selection-change", "row-click", "refresh"],
  setup(e, { expose: t, emit: n }) {
    const o = e, r = n, s = M(!1), a = M(""), i = M([]), u = M(0), p = M([]), c = M(1), m = M(o.pageSize), b = M(o.defaultSort || null), w = Fe({}), R = M(/* @__PURE__ */ new Set()), T = ae(
      () => o.columns.filter((S) => S.label && S.prop !== "__yzh_action")
    ), d = ae(
      () => o.columns.filter((S) => !(S.hidden || R.value.has(S.prop)))
    );
    function f(S, z) {
      z ? R.value.delete(S.prop) : R.value.add(S.prop), R.value = new Set(R.value);
    }
    function _(S) {
      if (S.sortable === !1) return;
      const z = S.prop;
      b.value && b.value.prop === z ? b.value = { ...b.value, order: b.value.order === "asc" ? "desc" : "asc" } : b.value = { prop: z, order: "asc" };
    }
    function O(S) {
      const z = S.prop;
      return !b.value || b.value.prop !== z ? "排序" : b.value.order === "asc" ? "↑ 升序" : "↓ 降序";
    }
    function k() {
      R.value = /* @__PURE__ */ new Set(), b.value = o.defaultSort || null;
    }
    function $() {
      X();
    }
    const L = ae(() => o.selectable), ee = ae(() => o.toolbar === !1 ? {} : o.toolbar === !0 ? { columnSetting: !0 } : o.toolbar);
    async function X() {
      s.value = !0, a.value = "";
      try {
        const S = {
          page: c.value,
          rows: m.value,
          ...b.value ? { sort: b.value.prop, order: b.value.order } : {},
          ...w
        }, z = await o.dataLoader(S);
        i.value = z.rows || [], u.value = z.total || 0;
      } catch (S) {
        a.value = (S == null ? void 0 : S.message) || "数据加载失败", i.value = [], u.value = 0, Cn.error(a.value);
      } finally {
        s.value = !1;
      }
    }
    function we({ prop: S, order: z }) {
      z ? b.value = {
        prop: S,
        order: z === "ascending" ? "asc" : "desc"
      } : b.value = null, X();
    }
    function ne(S) {
      c.value = S, X();
    }
    function Z(S) {
      m.value = S, c.value = 1, X();
    }
    function pe(S) {
      Object.assign(w, S), c.value = 1, X();
    }
    function xe() {
      Object.keys(w).forEach((S) => delete w[S]), o.searchFields && o.searchFields.slice(0, o.searchMaxFields).forEach((S) => {
        S.defaultValue !== void 0 && (w[S.prop] = S.defaultValue);
      }), c.value = 1, X();
    }
    function G(S) {
      p.value = S, r("selection-change", S);
    }
    function he(S, z) {
      r("row-click", S, z);
    }
    function re() {
      X(), r("refresh");
    }
    return vn(() => {
      o.searchFields && o.searchFields.slice(0, o.searchMaxFields).forEach((S) => {
        S.defaultValue !== void 0 && (w[S.prop] = S.defaultValue);
      }), X();
    }), t({
      refresh: re,
      loadData: X,
      getSelectedRows: () => p.value,
      clearSelection: () => {
        p.value = [];
      }
    }), (S, z) => {
      const h = C("el-button"), v = C("el-checkbox"), I = C("el-popover"), ie = C("el-table-column"), me = C("el-tag"), _e = C("el-empty"), qe = C("el-table"), N = An("loading");
      return g(), U("div", Hn, [
        e.searchFields && e.searchFields.length ? (g(), x($n, {
          key: 0,
          fields: e.searchFields,
          "default-values": w,
          cols: 2,
          "max-fields": e.searchMaxFields,
          onSearch: pe,
          onReset: xe
        }, null, 8, ["fields", "default-values", "max-fields"])) : W("", !0),
        Object.keys(ee.value).length > 0 ? (g(), x(Mn, { key: 1 }, {
          left: P(() => [
            F(S.$slots, "toolbar-left", {}, void 0, !0)
          ]),
          right: P(() => [
            F(S.$slots, "toolbar-right", {
              selected: p.value,
              refresh: re
            }, () => [
              ee.value.columnSetting ? (g(), x(I, {
                key: 0,
                trigger: "click",
                placement: "bottom-end",
                width: 240
              }, {
                reference: P(() => [
                  H(h, { text: "" }, {
                    default: P(() => [...z[0] || (z[0] = [
                      D("i", { class: "bi bi-columns" }, null, -1),
                      V(" 列设置 ", -1)
                    ])]),
                    _: 1
                  })
                ]),
                default: P(() => [
                  D("div", Yn, [
                    z[3] || (z[3] = D("div", { class: "yzh-column-settings__header" }, "列筛选与排序", -1)),
                    D("div", Kn, [
                      (g(!0), U(se, null, be(T.value, (E) => {
                        var q;
                        return g(), U("div", {
                          key: E.prop,
                          class: "yzh-column-settings__item"
                        }, [
                          H(v, {
                            "model-value": !R.value.has(E.prop) && !E.hidden,
                            onChange: (ce) => f(E, ce)
                          }, {
                            default: P(() => [
                              V(j(E.label), 1)
                            ]),
                            _: 2
                          }, 1032, ["model-value", "onChange"]),
                          H(h, {
                            size: "small",
                            link: "",
                            type: "primary",
                            class: Ge({ "is-active": ((q = b.value) == null ? void 0 : q.prop) === E.prop }),
                            disabled: E.sortable === !1,
                            onClick: (ce) => _(E)
                          }, {
                            default: P(() => [
                              V(j(O(E)), 1)
                            ]),
                            _: 2
                          }, 1032, ["class", "disabled", "onClick"])
                        ]);
                      }), 128))
                    ]),
                    D("div", Wn, [
                      H(h, {
                        size: "small",
                        onClick: k
                      }, {
                        default: P(() => [...z[1] || (z[1] = [
                          V("重置", -1)
                        ])]),
                        _: 1
                      }),
                      H(h, {
                        size: "small",
                        type: "primary",
                        onClick: $
                      }, {
                        default: P(() => [...z[2] || (z[2] = [
                          V("确定", -1)
                        ])]),
                        _: 1
                      })
                    ])
                  ])
                ]),
                _: 1
              })) : W("", !0)
            ], !0)
          ]),
          _: 3
        })) : W("", !0),
        D("div", Jn, [
          D("div", {
            class: "yzh-table__body",
            style: Le(e.height ? { height: typeof e.height == "number" ? e.height + "px" : e.height } : {})
          }, [
            Tn((g(), x(qe, {
              data: i.value,
              "row-key": e.rowKey,
              height: e.height ? "100%" : void 0,
              stripe: "",
              border: "",
              onSelectionChange: G,
              onSortChange: we,
              onRowClick: he
            }, {
              empty: P(() => [
                D("div", Zn, [
                  !s.value && !a.value ? (g(), x(_e, {
                    key: 0,
                    description: e.emptyText
                  }, null, 8, ["description"])) : a.value ? (g(), U("div", Gn, [
                    z[5] || (z[5] = D("i", { class: "bi bi-exclamation-triangle" }, null, -1)),
                    D("span", null, j(a.value), 1),
                    H(h, {
                      text: "",
                      type: "primary",
                      onClick: re
                    }, {
                      default: P(() => [...z[4] || (z[4] = [
                        V("重试", -1)
                      ])]),
                      _: 1
                    })
                  ])) : W("", !0)
                ])
              ]),
              default: P(() => [
                L.value ? (g(), x(ie, {
                  key: 0,
                  type: "selection",
                  width: "48",
                  "reserve-selection": !1
                })) : W("", !0),
                (g(!0), U(se, null, be(d.value, (E) => (g(), x(ie, {
                  key: E.prop,
                  prop: E.prop,
                  label: E.label,
                  width: E.width,
                  "min-width": E.minWidth,
                  fixed: E.fixed,
                  sortable: E.sortable,
                  align: E.align || "left",
                  "show-overflow-tooltip": !E.slot,
                  "class-name": E.className
                }, {
                  default: P(({ row: q, $index: ce }) => [
                    E.slot ? F(S.$slots, `column-${String(E.prop)}`, {
                      key: 0,
                      row: q,
                      index: ce,
                      value: q[E.prop]
                    }, () => [
                      V(j(E.formatter ? E.formatter(q[E.prop], q, ce) : q[E.prop]), 1)
                    ], !0) : E.dictCode ? (g(), U(se, { key: 1 }, [
                      E.tagType ? (g(), x(me, {
                        key: 0,
                        type: E.tagType,
                        "disable-transitions": ""
                      }, {
                        default: P(() => [
                          V(j(q[E.prop]), 1)
                        ]),
                        _: 2
                      }, 1032, ["type"])) : (g(), U("span", Xn, j(q[E.prop]), 1))
                    ], 64)) : (g(), U(se, { key: 2 }, [
                      V(j(E.formatter ? E.formatter(q[E.prop], q, ce) : q[E.prop]), 1)
                    ], 64))
                  ]),
                  _: 2
                }, 1032, ["prop", "label", "width", "min-width", "fixed", "sortable", "align", "show-overflow-tooltip", "class-name"]))), 128))
              ]),
              _: 3
            }, 8, ["data", "row-key", "height"])), [
              [N, s.value]
            ])
          ], 4)
        ]),
        e.showPagination ? (g(), U("div", Qn, [
          H(kn, {
            page: c.value,
            "page-size": m.value,
            total: u.value,
            "onUpdate:page": ne,
            "onUpdate:pageSize": Z
          }, null, 8, ["page", "page-size", "total"])
        ])) : W("", !0)
      ]);
    };
  }
}), Vs = /* @__PURE__ */ le(eo, [["__scopeId", "data-v-2b637dd2"]]), to = {
  key: 0,
  class: "yzh-form__actions"
}, no = /* @__PURE__ */ Te({
  __name: "YzhForm",
  props: {
    modelValue: {},
    fields: {},
    rules: {},
    labelWidth: { default: "100px" },
    labelPosition: { default: "right" },
    size: { default: "default" },
    showActions: { type: Boolean, default: !0 },
    cols: { default: 2 },
    submitText: { default: "保存" },
    resetText: { default: "取消" },
    loading: { type: Boolean, default: !1 }
  },
  emits: ["update:modelValue", "submit", "reset", "validate"],
  setup(e, { expose: t, emit: n }) {
    const o = e, r = n, s = M(), a = ae(() => 24 / o.cols), i = ae(() => {
      if (o.rules) return o.rules;
      const d = {};
      return o.fields.forEach((f) => {
        if (f.hidden) return;
        const _ = [];
        f.required && _.push({
          required: !0,
          message: `请${f.type === "select" || f.type === "radio" || f.type === "switch" ? "选择" : "输入"}${f.label}`,
          trigger: f.trigger || (f.type === "select" || f.type === "switch" ? "change" : "blur")
        }), f.validator && _.push({ validator: f.validator, trigger: f.trigger || "blur" }), _.length && (d[f.prop] = _);
      }), d;
    }), u = Fe({});
    async function p(d) {
      if (d.options) return d.options;
      if (!d.loadOptions) return [];
      if (u[d.prop]) return u[d.prop];
      const f = await d.loadOptions();
      return u[d.prop] = f, f;
    }
    (async () => {
      for (const d of o.fields)
        d.loadOptions && !d.options && await p(d);
    })();
    const c = Fe({});
    function m() {
      Object.keys(c).forEach((d) => delete c[d]), Object.assign(c, o.modelValue || {}), o.fields.forEach((d) => {
        c[d.prop] === void 0 && d.defaultValue !== void 0 && (c[d.prop] = d.defaultValue);
      });
    }
    m(), We(
      () => o.modelValue,
      () => m(),
      { deep: !0 }
    ), We(
      c,
      (d) => {
        r("update:modelValue", { ...d });
      },
      { deep: !0 }
    );
    async function b() {
      if (s.value)
        try {
          await s.value.validate(), r("submit", { ...c }), r("validate", !0);
        } catch (d) {
          r("validate", !1, d);
        }
    }
    function w() {
      var d;
      m(), (d = s.value) == null || d.clearValidate(), r("reset");
    }
    async function R() {
      var d;
      return (d = s.value) == null ? void 0 : d.validate();
    }
    async function T() {
      var d;
      (d = s.value) == null || d.resetFields();
    }
    return t({ validate: R, resetFields: T, formRef: s }), (d, f) => {
      const _ = C("el-input"), O = C("el-input-number"), k = C("el-option"), $ = C("el-select"), L = C("el-radio"), ee = C("el-radio-group"), X = C("el-checkbox"), we = C("el-checkbox-group"), ne = C("el-switch"), Z = C("el-date-picker"), pe = C("el-tree-select"), xe = C("el-cascader"), G = C("el-form-item"), he = C("el-col"), re = C("el-row"), S = C("el-button"), z = C("el-form");
      return g(), x(z, {
        ref_key: "formRef",
        ref: s,
        model: c,
        rules: i.value,
        "label-width": e.labelWidth,
        "label-position": e.labelPosition,
        size: e.size,
        class: "yzh-form"
      }, {
        default: P(() => [
          H(re, { gutter: 20 }, {
            default: P(() => [
              (g(!0), U(se, null, be(e.fields, (h) => (g(), U(se, {
                key: h.prop
              }, [
                h.hidden ? W("", !0) : (g(), x(he, {
                  key: 0,
                  span: h.span || a.value
                }, {
                  default: P(() => [
                    H(G, {
                      label: h.label,
                      prop: h.prop
                    }, {
                      default: P(() => [
                        !h.type || h.type === "text" || h.type === "textarea" || h.type === "password" ? (g(), x(_, fe({
                          key: 0,
                          modelValue: c[h.prop],
                          "onUpdate:modelValue": (v) => c[h.prop] = v,
                          type: h.type === "textarea" ? "textarea" : h.type === "password" ? "password" : "text",
                          placeholder: h.placeholder || `请输入${h.label}`,
                          disabled: h.disabled,
                          rows: h.type === "textarea" ? 3 : void 0
                        }, { ref_for: !0 }, h.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "type", "placeholder", "disabled", "rows"])) : h.type === "number" ? (g(), x(O, fe({
                          key: 1,
                          modelValue: c[h.prop],
                          "onUpdate:modelValue": (v) => c[h.prop] = v,
                          placeholder: h.placeholder,
                          disabled: h.disabled,
                          style: { width: "100%" }
                        }, { ref_for: !0 }, h.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : h.type === "select" ? (g(), x($, fe({
                          key: 2,
                          modelValue: c[h.prop],
                          "onUpdate:modelValue": (v) => c[h.prop] = v,
                          placeholder: h.placeholder || `请选择${h.label}`,
                          multiple: h.multiple,
                          filterable: h.filterable,
                          disabled: h.disabled,
                          style: { width: "100%" }
                        }, { ref_for: !0 }, h.fieldProps), {
                          default: P(() => [
                            (g(!0), U(se, null, be(h.options || u[h.prop] || [], (v) => (g(), x(k, {
                              key: v.value,
                              label: v.label,
                              value: v.value,
                              disabled: v.disabled
                            }, null, 8, ["label", "value", "disabled"]))), 128))
                          ]),
                          _: 2
                        }, 1040, ["modelValue", "onUpdate:modelValue", "placeholder", "multiple", "filterable", "disabled"])) : h.type === "radio" ? (g(), x(ee, {
                          key: 3,
                          modelValue: c[h.prop],
                          "onUpdate:modelValue": (v) => c[h.prop] = v,
                          disabled: h.disabled
                        }, {
                          default: P(() => [
                            (g(!0), U(se, null, be(h.options || [], (v) => (g(), x(L, {
                              key: v.value,
                              value: v.value
                            }, {
                              default: P(() => [
                                V(j(v.label), 1)
                              ]),
                              _: 2
                            }, 1032, ["value"]))), 128))
                          ]),
                          _: 2
                        }, 1032, ["modelValue", "onUpdate:modelValue", "disabled"])) : h.type === "checkbox" ? (g(), x(we, {
                          key: 4,
                          modelValue: c[h.prop],
                          "onUpdate:modelValue": (v) => c[h.prop] = v,
                          disabled: h.disabled
                        }, {
                          default: P(() => [
                            (g(!0), U(se, null, be(h.options || [], (v) => (g(), x(X, {
                              key: v.value,
                              value: v.value
                            }, {
                              default: P(() => [
                                V(j(v.label), 1)
                              ]),
                              _: 2
                            }, 1032, ["value"]))), 128))
                          ]),
                          _: 2
                        }, 1032, ["modelValue", "onUpdate:modelValue", "disabled"])) : h.type === "switch" ? (g(), x(ne, fe({
                          key: 5,
                          modelValue: c[h.prop],
                          "onUpdate:modelValue": (v) => c[h.prop] = v,
                          disabled: h.disabled
                        }, { ref_for: !0 }, h.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "disabled"])) : h.type === "date" ? (g(), x(Z, fe({
                          key: 6,
                          modelValue: c[h.prop],
                          "onUpdate:modelValue": (v) => c[h.prop] = v,
                          type: "date",
                          placeholder: h.placeholder || `请选择${h.label}`,
                          disabled: h.disabled,
                          "value-format": "YYYY-MM-DD",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, h.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : h.type === "datetime" ? (g(), x(Z, fe({
                          key: 7,
                          modelValue: c[h.prop],
                          "onUpdate:modelValue": (v) => c[h.prop] = v,
                          type: "datetime",
                          placeholder: h.placeholder || `请选择${h.label}`,
                          disabled: h.disabled,
                          "value-format": "YYYY-MM-DD HH:mm:ss",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, h.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : h.type === "dateRange" ? (g(), x(Z, fe({
                          key: 8,
                          modelValue: c[h.prop],
                          "onUpdate:modelValue": (v) => c[h.prop] = v,
                          type: "daterange",
                          placeholder: h.placeholder || `请选择${h.label}`,
                          disabled: h.disabled,
                          "value-format": "YYYY-MM-DD",
                          "range-separator": "至",
                          "start-placeholder": "开始日期",
                          "end-placeholder": "结束日期",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, h.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : h.type === "treeSelect" ? (g(), x(pe, fe({
                          key: 9,
                          modelValue: c[h.prop],
                          "onUpdate:modelValue": (v) => c[h.prop] = v,
                          data: h.options || [],
                          placeholder: h.placeholder || `请选择${h.label}`,
                          disabled: h.disabled,
                          "check-strictly": "",
                          clearable: "",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, h.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "data", "placeholder", "disabled"])) : h.type === "cascader" ? (g(), x(xe, fe({
                          key: 10,
                          modelValue: c[h.prop],
                          "onUpdate:modelValue": (v) => c[h.prop] = v,
                          options: h.options || [],
                          placeholder: h.placeholder || `请选择${h.label}`,
                          disabled: h.disabled,
                          style: { width: "100%" }
                        }, { ref_for: !0 }, h.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "options", "placeholder", "disabled"])) : h.type === "custom" && h.slot ? F(d.$slots, h.slot, {
                          key: 11,
                          value: c[h.prop],
                          field: h,
                          data: c
                        }, void 0, !0) : F(d.$slots, `field-${h.prop}`, {
                          key: 12,
                          value: c[h.prop],
                          field: h,
                          data: c
                        }, void 0, !0)
                      ]),
                      _: 2
                    }, 1032, ["label", "prop"])
                  ]),
                  _: 2
                }, 1032, ["span"]))
              ], 64))), 128))
            ]),
            _: 3
          }),
          e.showActions ? (g(), U("div", to, [
            F(d.$slots, "actions", {
              submit: b,
              reset: w
            }, () => [
              H(S, { onClick: w }, {
                default: P(() => [
                  V(j(e.resetText), 1)
                ]),
                _: 1
              }),
              H(S, {
                type: "primary",
                loading: e.loading,
                onClick: b
              }, {
                default: P(() => [
                  V(j(e.submitText), 1)
                ]),
                _: 1
              }, 8, ["loading"])
            ], !0)
          ])) : W("", !0)
        ]),
        _: 3
      }, 8, ["model", "rules", "label-width", "label-position", "size"]);
    };
  }
}), Is = /* @__PURE__ */ le(no, [["__scopeId", "data-v-1b44471b"]]), oo = { class: "yzh-page-layout" }, ro = {
  key: 0,
  class: "yzh-page-layout__search"
}, so = { class: "yzh-page-layout__toolbar" }, ao = { class: "yzh-page-layout__toolbar-left" }, io = { class: "yzh-page-layout__toolbar-right" }, lo = { class: "yzh-page-layout__content" }, co = {
  key: 1,
  class: "yzh-page-layout__footer"
}, uo = /* @__PURE__ */ Te({
  __name: "YzhPageLayout",
  props: {
    pageTitle: {},
    helpText: {},
    showTitle: { type: Boolean }
  },
  setup(e) {
    return (t, n) => (g(), U("div", oo, [
      t.$slots.search ? (g(), U("div", ro, [
        F(t.$slots, "search", {}, void 0, !0)
      ])) : W("", !0),
      D("div", so, [
        F(t.$slots, "toolbar", {}, () => [
          D("div", ao, [
            F(t.$slots, "toolbar-left", {}, void 0, !0)
          ]),
          D("div", io, [
            F(t.$slots, "toolbar-right", {}, void 0, !0)
          ])
        ], !0)
      ]),
      D("div", lo, [
        F(t.$slots, "default", {}, void 0, !0)
      ]),
      t.$slots.pagination ? (g(), U("div", co, [
        F(t.$slots, "pagination", {}, void 0, !0)
      ])) : W("", !0)
    ]));
  }
}), js = /* @__PURE__ */ le(uo, [["__scopeId", "data-v-a6321bbc"]]), fo = { class: "yzh-dialog__body" }, po = { class: "yzh-dialog__footer" }, ho = /* @__PURE__ */ Te({
  __name: "YzhDialog",
  props: {
    modelValue: { type: Boolean },
    title: { default: "提示" },
    width: { default: "600px" },
    fullscreen: { type: Boolean, default: !1 },
    showFooter: { type: Boolean, default: !0 },
    confirmText: { default: "确定" },
    cancelText: { default: "取消" },
    confirmType: { default: "primary" },
    confirmDisabled: { type: Boolean, default: !1 },
    confirmLoading: { type: Boolean, default: !1 },
    closeOnClickModal: { type: Boolean, default: !1 },
    showClose: { type: Boolean, default: !0 },
    zIndex: {},
    customClass: { default: "" },
    destroyOnClose: { type: Boolean, default: !1 },
    top: { default: "15vh" }
  },
  emits: ["update:modelValue", "confirm", "cancel", "open", "close"],
  setup(e, { emit: t }) {
    const n = e, o = t, r = ae(() => typeof n.width == "number" ? `${n.width}px` : n.width);
    function s() {
      o("update:modelValue", !1), o("close");
    }
    function a() {
      n.confirmDisabled || n.confirmLoading || o("confirm");
    }
    function i() {
      o("cancel"), s();
    }
    return We(
      () => n.modelValue,
      (u) => {
        u && o("open");
      }
    ), (u, p) => {
      const c = C("el-button"), m = C("el-dialog");
      return g(), x(m, {
        "model-value": e.modelValue,
        title: e.title,
        width: e.fullscreen ? "100%" : r.value,
        fullscreen: e.fullscreen,
        "show-close": e.showClose,
        "close-on-click-modal": e.closeOnClickModal,
        "z-index": e.zIndex,
        class: Ge(e.customClass),
        top: e.fullscreen ? "0" : e.top,
        "destroy-on-close": e.destroyOnClose,
        "onUpdate:modelValue": p[0] || (p[0] = (b) => o("update:modelValue", b))
      }, xn({
        default: P(() => [
          D("div", fo, [
            F(u.$slots, "default", {}, void 0, !0)
          ])
        ]),
        _: 2
      }, [
        e.showFooter ? {
          name: "footer",
          fn: P(() => [
            F(u.$slots, "footer", {
              confirm: a,
              cancel: i
            }, () => [
              D("div", po, [
                H(c, { onClick: i }, {
                  default: P(() => [
                    V(j(e.cancelText), 1)
                  ]),
                  _: 1
                }),
                H(c, {
                  type: e.confirmType,
                  disabled: e.confirmDisabled,
                  loading: e.confirmLoading,
                  onClick: a
                }, {
                  default: P(() => [
                    V(j(e.confirmText), 1)
                  ]),
                  _: 1
                }, 8, ["type", "disabled", "loading"])
              ])
            ], !0)
          ]),
          key: "0"
        } : void 0
      ]), 1032, ["model-value", "title", "width", "fullscreen", "show-close", "close-on-click-modal", "z-index", "class", "top", "destroy-on-close"]);
    };
  }
}), qs = /* @__PURE__ */ le(ho, [["__scopeId", "data-v-dbdcca59"]]), mo = { class: "yzh-empty-state__inner" }, yo = { class: "yzh-empty-state__title" }, bo = {
  key: 2,
  class: "yzh-empty-state__description"
}, go = {
  key: 3,
  class: "yzh-empty-state__action"
}, wo = {
  __name: "YzhEmptyState",
  props: {
    icon: { type: Object, required: !0 },
    title: { type: String, required: !0 },
    description: { type: String, default: "" },
    actionLabel: { type: String, default: "" },
    onAction: { type: Function, default: null },
    compact: { type: Boolean, default: !1 },
    iconSize: { type: Number, default: 48 },
    iconColor: { type: String, default: "var(--yzh-color-text-secondary)" },
    iconBackgroundColor: { type: String, default: "" },
    iconBackgroundPadding: { type: String, default: "20px" }
  },
  setup(e) {
    return (t, n) => {
      const o = C("el-icon"), r = C("el-button");
      return g(), U("div", {
        class: Ge(["yzh-empty-state", { "is-compact": e.compact, "is-icon-bg": e.iconBackgroundColor }])
      }, [
        D("div", mo, [
          e.iconBackgroundColor ? (g(), U("div", {
            key: 0,
            class: "yzh-empty-state__icon-wrap",
            style: Le({ backgroundColor: e.iconBackgroundColor })
          }, [
            H(o, {
              class: "yzh-empty-state__icon",
              style: Le({ fontSize: e.iconSize + "px", color: e.iconColor })
            }, {
              default: P(() => [
                (g(), x(dt(e.icon)))
              ]),
              _: 1
            }, 8, ["style"])
          ], 4)) : (g(), x(o, {
            key: 1,
            class: "yzh-empty-state__icon",
            style: Le({ fontSize: e.iconSize + "px", color: e.iconColor })
          }, {
            default: P(() => [
              (g(), x(dt(e.icon)))
            ]),
            _: 1
          }, 8, ["style"])),
          D("div", yo, j(e.title), 1),
          e.description ? (g(), U("div", bo, j(e.description), 1)) : W("", !0),
          e.actionLabel && e.onAction ? (g(), U("div", go, [
            F(t.$slots, "action", {}, () => [
              H(r, {
                size: "small",
                onClick: e.onAction
              }, {
                default: P(() => [
                  V(j(e.actionLabel), 1)
                ]),
                _: 1
              }, 8, ["onClick"])
            ], !0)
          ])) : W("", !0)
        ])
      ], 2);
    };
  }
}, Ms = /* @__PURE__ */ le(wo, [["__scopeId", "data-v-3dff062a"]]), _o = {
  __name: "YzhStatusBadge",
  props: {
    type: { type: String, default: "info" },
    // success | warning | danger | info
    text: { type: String, default: "" },
    icon: { type: Object, default: null },
    size: { type: String, default: "small" }
    // small | default
  },
  setup(e) {
    const t = e, n = ae(() => ({
      success: null,
      // 后续引入图标
      warning: null,
      danger: null,
      info: null
    })[t.type] || null);
    return (o, r) => {
      const s = C("el-icon");
      return g(), U("span", {
        class: Ge(["yzh-status-badge", [`is-${e.type}`, `is-${e.size}`]])
      }, [
        e.icon || n.value ? (g(), x(s, {
          key: 0,
          class: "yzh-status-badge__icon"
        }, {
          default: P(() => [
            (g(), x(dt(e.icon || n.value)))
          ]),
          _: 1
        })) : W("", !0),
        F(o.$slots, "default", {}, () => [
          V(j(e.text), 1)
        ], !0)
      ], 2);
    };
  }
}, Hs = /* @__PURE__ */ le(_o, [["__scopeId", "data-v-19b41d29"]]), So = { class: "yzh-card" }, Eo = {
  key: 0,
  class: "yzh-card__header"
}, Ro = { class: "yzh-card__body" }, Oo = {
  key: 1,
  class: "yzh-card__footer"
}, vo = {
  __name: "YzhCard",
  props: {
    title: { type: String, default: "" }
  },
  setup(e) {
    return (t, n) => (g(), U("div", So, [
      t.$slots.header || e.title ? (g(), U("div", Eo, [
        F(t.$slots, "header", {}, () => [
          V(j(e.title), 1)
        ], !0)
      ])) : W("", !0),
      D("div", Ro, [
        F(t.$slots, "default", {}, void 0, !0)
      ]),
      t.$slots.footer ? (g(), U("div", Oo, [
        F(t.$slots, "footer", {}, void 0, !0)
      ])) : W("", !0)
    ]));
  }
}, Ys = /* @__PURE__ */ le(vo, [["__scopeId", "data-v-40d47496"]]), ot = {}, rt = "YZH_TOKEN", ge = {
  get: () => localStorage.getItem(rt),
  set: (e) => localStorage.setItem(rt, e),
  clear: () => localStorage.removeItem(rt)
};
class Ao {
  constructor(t) {
    Ce(this, "baseURL");
    Ce(this, "getToken");
    Ce(this, "onUnauthorized");
    Ce(this, "onError");
    Ce(this, "legacy");
    this.baseURL = t.baseURL.replace(/\/$/, ""), this.getToken = t.getToken || (() => ge.get()), this.onUnauthorized = t.onUnauthorized, this.onError = t.onError, this.legacy = t.legacy ?? !0;
  }
  /**
   * 通用请求方法
   */
  async request(t, n = {}) {
    var m, b;
    const { method: o = "POST", params: r, body: s, headers: a = {}, requireAuth: i = !0, raw: u = !1 } = n;
    let p = t;
    const c = {
      method: o,
      headers: {
        "Content-Type": "application/json",
        ...a
      }
    };
    if (i !== !1) {
      const w = this.getToken();
      w && (c.headers.Authorization = `Bearer ${w}`);
    }
    if (o === "GET" && r) {
      const w = new URLSearchParams();
      Object.entries(r).forEach(([T, d]) => {
        d != null && w.append(T, String(d));
      });
      const R = w.toString();
      R && (p += (t.includes("?") ? "&" : "?") + R);
    } else s !== void 0 ? c.body = JSON.stringify(s) : r && (c.body = JSON.stringify(r));
    try {
      const w = await fetch(this.baseURL + p, c);
      if (w.status === 401)
        throw ge.clear(), (m = this.onUnauthorized) == null || m.call(this), new Error("登录已过期，请重新登录");
      if (u)
        return await w.json();
      const R = await w.json();
      return this.normalizeResponse(R);
    } catch (w) {
      throw (b = this.onError) == null || b.call(this, w), w;
    }
  }
  /**
   * 忽略大小写获取对象属性（兼容 Vol JsonNormal 返回 PascalCase）
   */
  pick(t, ...n) {
    for (const o of n) {
      if (t[o] !== void 0) return t[o];
      const r = o.toLowerCase();
      for (const s of Object.keys(t))
        if (s.toLowerCase() === r) return t[s];
    }
  }
  /**
   * PascalCase → camelCase
   * DepartmentName → departmentName
   */
  toCamelCase(t) {
    return t.charAt(0).toLowerCase() + t.slice(1);
  }
  /**
   * 递归转换对象的 key 为 camelCase（用于 Vol JsonNormal 返回 PascalCase 实体）
   * 仅在检测到第一个 key 的首字母大写时转换，避免误伤已是 camelCase 的数据
   */
  normalizeKeys(t) {
    if (t == null || typeof t != "object") return t;
    if (Array.isArray(t)) return t.map((s) => this.normalizeKeys(s));
    const n = Object.keys(t);
    if (n.length === 0 || !n.some((s) => s.length > 0 && s[0] >= "A" && s[0] <= "Z")) return t;
    const r = {};
    for (const s of n)
      r[this.toCamelCase(s)] = this.normalizeKeys(t[s]);
    return r;
  }
  /**
   * 归一化 vol 响应为新格式
   *  - vol JsonNormal 返回 PascalCase：{ Status, Msg, Rows, Total, Data }
   *  - vol Json 返回 camelCase：{ status, msg, rows, total, data }
   */
  normalizeResponse(t) {
    if (!this.legacy)
      return this.normalizeKeys(this.pick(t, "data") ?? t);
    const n = this.pick(t, "status", "code");
    if ((n === !0 || n === 0 || n === void 0) === !1 || n === 401) {
      const a = this.pick(t, "message", "msg", "error") || "请求失败", i = new Error(a);
      throw i.response = t, i;
    }
    const r = this.pick(t, "data");
    if (r != null)
      return this.normalizeKeys(r);
    const s = this.pick(t, "rows");
    return s !== void 0 ? { rows: this.normalizeKeys(s), total: this.pick(t, "total") || 0 } : this.normalizeKeys(t);
  }
  get(t, n, o) {
    return this.request(t, { ...o, method: "GET", params: n });
  }
  post(t, n, o) {
    return this.request(t, { ...o, method: "POST", body: n });
  }
  put(t, n, o) {
    return this.request(t, { ...o, method: "PUT", body: n });
  }
  delete(t, n) {
    return this.request(t, { ...n, method: "DELETE" });
  }
}
const Ks = new Ao({
  baseURL: (ot == null ? void 0 : ot.VITE_API_BASE) || "http://127.0.0.1:9992",
  onUnauthorized: () => {
    console.warn("[YzhApi] 401 未授权，请重新登录");
  },
  legacy: !0
});
async function Ws(e, t) {
  var s;
  const n = await fetch("/api/Auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ userName: e, password: t })
  }), o = await n.json();
  if (!n.ok || (o == null ? void 0 : o.success) === !1)
    throw new Error((o == null ? void 0 : o.message) || (o == null ? void 0 : o.msg) || "登录失败");
  const r = (s = o == null ? void 0 : o.data) == null ? void 0 : s.token;
  if (!r)
    throw new Error("登录响应缺少 token");
  return ge.set(r), {
    token: r,
    userName: o.data.userName,
    userTrueName: o.data.userTrueName,
    roleId: o.data.roleId
  };
}
function Js() {
  ge.clear();
}
function Xs() {
  return !!ge.get();
}
function Zs() {
  const e = M(!1), t = M([]), n = M(0), o = M(1), r = M(20), s = Fe({});
  async function a(p) {
    e.value = !0;
    try {
      const c = {
        page: o.value,
        rows: r.value,
        ...s
      }, m = await p(c);
      t.value = m.rows || [], n.value = m.total || 0;
    } finally {
      e.value = !1;
    }
  }
  function i(p) {
    Object.assign(s, p), o.value = 1;
  }
  function u() {
    Object.keys(s).forEach((p) => delete s[p]), o.value = 1;
  }
  return {
    loading: e,
    rows: t,
    total: n,
    page: o,
    pageSize: r,
    searchParams: s,
    loadData: a,
    setSearchParams: i,
    resetSearchParams: u
  };
}
function Gs() {
  const e = M(ge.get() || ""), t = M(null), n = ae(() => !!e.value);
  function o(i) {
    e.value = i, ge.set(i);
  }
  function r() {
    e.value = "", t.value = null, ge.clear();
  }
  function s(i, u) {
    return Promise.resolve();
  }
  function a() {
    r();
  }
  return {
    token: e,
    userInfo: t,
    isAuthenticated: n,
    setToken: o,
    clearToken: r,
    login: s,
    logout: a
  };
}
function Ht(e, t) {
  return function() {
    return e.apply(t, arguments);
  };
}
const { toString: To } = Object.prototype, { getPrototypeOf: ke } = Object, { iterator: $e, toStringTag: Yt } = Symbol, Je = (({ hasOwnProperty: e }) => (t, n) => e.call(t, n))(Object.prototype), Be = (e, t) => {
  let n = e;
  const o = [];
  for (; n != null && n !== Object.prototype; ) {
    if (o.indexOf(n) !== -1)
      return !1;
    if (o.push(n), Je(n, t))
      return !0;
    n = ke(n);
  }
  return !1;
}, xo = (e, t) => e != null && Be(e, t) ? e[t] : void 0, yt = /* @__PURE__ */ ((e) => (t) => {
  const n = To.call(t);
  return e[n] || (e[n] = n.slice(8, -1).toLowerCase());
})(/* @__PURE__ */ Object.create(null)), oe = (e) => (e = e.toLowerCase(), (t) => yt(t) === e), Qe = (e) => (t) => typeof t === e, { isArray: Oe } = Array, ve = Qe("undefined");
function ze(e) {
  return e !== null && !ve(e) && e.constructor !== null && !ve(e.constructor) && Q(e.constructor.isBuffer) && e.constructor.isBuffer(e);
}
const Kt = oe("ArrayBuffer");
function Co(e) {
  let t;
  return typeof ArrayBuffer < "u" && ArrayBuffer.isView ? t = ArrayBuffer.isView(e) : t = e && e.buffer && Kt(e.buffer), t;
}
const Po = Qe("string"), Q = Qe("function"), Wt = Qe("number"), Ue = (e) => e !== null && typeof e == "object", ko = (e) => e === !0 || e === !1, He = (e) => {
  if (!Ue(e))
    return !1;
  const t = ke(e);
  return (t === null || t === Object.prototype || ke(t) === null) && // Treat any genuine (non-Object.prototype-polluted) Symbol.toStringTag or
  // Symbol.iterator as evidence the value is a tagged/iterable type rather
  // than a plain object, while ignoring keys injected onto Object.prototype.
  !Be(e, Yt) && !Be(e, $e);
}, zo = (e) => {
  if (!Ue(e) || ze(e))
    return !1;
  try {
    return Object.keys(e).length === 0 && Object.getPrototypeOf(e) === Object.prototype;
  } catch {
    return !1;
  }
}, Uo = oe("Date"), No = oe("File"), Do = (e) => !!(e && typeof e.uri < "u"), Lo = (e) => e && typeof e.getParts < "u", Fo = oe("Blob"), Bo = oe("FileList"), $o = oe("Set"), Vo = (e) => Ue(e) && Q(e.pipe);
function Io() {
  return typeof globalThis < "u" ? globalThis : typeof self < "u" ? self : typeof window < "u" ? window : typeof global < "u" ? global : {};
}
const At = Io(), Tt = typeof At.FormData < "u" ? At.FormData : void 0, jo = (e) => {
  if (!e) return !1;
  if (Tt && e instanceof Tt) return !0;
  const t = ke(e);
  if (!t || t === Object.prototype || !Q(e.append)) return !1;
  const n = yt(e);
  return n === "formdata" || // detect form-data instance
  n === "object" && Q(e.toString) && e.toString() === "[object FormData]";
}, qo = oe("URLSearchParams"), [Mo, Ho, Yo, Ko] = [
  "ReadableStream",
  "Request",
  "Response",
  "Headers"
].map(oe), Wo = (e) => e.trim ? e.trim() : e.replace(/^[\s\uFEFF\xA0]+|[\s\uFEFF\xA0]+$/g, "");
function Ve(e, t, { allOwnKeys: n = !1 } = {}) {
  if (e === null || typeof e > "u")
    return;
  let o, r;
  if (typeof e != "object" && (e = [e]), Oe(e))
    for (o = 0, r = e.length; o < r; o++)
      t.call(null, e[o], o, e);
  else {
    if (ze(e))
      return;
    const s = n ? Object.getOwnPropertyNames(e) : Object.keys(e), a = s.length;
    let i;
    for (o = 0; o < a; o++)
      i = s[o], t.call(null, e[i], i, e);
  }
}
function Jt(e, t) {
  if (ze(e))
    return null;
  t = t.toLowerCase();
  const n = Object.keys(e);
  let o = n.length, r;
  for (; o-- > 0; )
    if (r = n[o], t === r.toLowerCase())
      return r;
  return null;
}
const Ee = typeof globalThis < "u" ? globalThis : typeof self < "u" ? self : typeof window < "u" ? window : global, Xt = (e) => !ve(e) && e !== Ee;
function ft(...e) {
  const { caseless: t, skipUndefined: n } = Xt(this) && this || {}, o = {}, r = (s, a) => {
    if (a === "__proto__" || a === "constructor" || a === "prototype")
      return;
    const i = t && typeof a == "string" && Jt(o, a) || a, u = Je(o, i) ? o[i] : void 0;
    He(u) && He(s) ? o[i] = ft(u, s) : He(s) ? o[i] = ft({}, s) : Oe(s) ? o[i] = s.slice() : (!n || !ve(s)) && (o[i] = s);
  };
  for (let s = 0, a = e.length; s < a; s++) {
    const i = e[s];
    if (!i || ze(i) || (Ve(i, r), typeof i != "object" || Oe(i)))
      continue;
    const u = Object.getOwnPropertySymbols(i);
    for (let p = 0; p < u.length; p++) {
      const c = u[p];
      ar.call(i, c) && r(i[c], c);
    }
  }
  return o;
}
const Jo = (e, t, n, { allOwnKeys: o } = {}) => (Ve(
  t,
  (r, s) => {
    n && Q(r) ? Object.defineProperty(e, s, {
      // Null-proto descriptor so a polluted Object.prototype.get cannot
      // hijack defineProperty's accessor-vs-data resolution.
      __proto__: null,
      value: Ht(r, n),
      writable: !0,
      enumerable: !0,
      configurable: !0
    }) : Object.defineProperty(e, s, {
      __proto__: null,
      value: r,
      writable: !0,
      enumerable: !0,
      configurable: !0
    });
  },
  { allOwnKeys: o }
), e), Xo = (e) => (e.charCodeAt(0) === 65279 && (e = e.slice(1)), e), Zo = (e, t, n, o) => {
  e.prototype = Object.create(t.prototype, o), Object.defineProperty(e.prototype, "constructor", {
    __proto__: null,
    value: e,
    writable: !0,
    enumerable: !1,
    configurable: !0
  }), Object.defineProperty(e, "super", {
    __proto__: null,
    value: t.prototype
  }), n && Object.assign(e.prototype, n);
}, Go = (e, t, n, o) => {
  let r, s, a;
  const i = {};
  if (t = t || {}, e == null) return t;
  do {
    for (r = Object.getOwnPropertyNames(e), s = r.length; s-- > 0; )
      a = r[s], (!o || o(a, e, t)) && !i[a] && (t[a] = e[a], i[a] = !0);
    e = n !== !1 && ke(e);
  } while (e && (!n || n(e, t)) && e !== Object.prototype);
  return t;
}, Qo = (e, t, n) => {
  e = String(e), (n === void 0 || n > e.length) && (n = e.length), n -= t.length;
  const o = e.indexOf(t, n);
  return o !== -1 && o === n;
}, er = (e) => {
  if (!e) return null;
  if (Oe(e)) return e;
  let t = e.length;
  if (!Wt(t)) return null;
  const n = new Array(t);
  for (; t-- > 0; )
    n[t] = e[t];
  return n;
}, tr = /* @__PURE__ */ ((e) => (t) => e && t instanceof e)(typeof Uint8Array < "u" && ke(Uint8Array)), nr = (e, t) => {
  const o = (e && e[$e]).call(e);
  let r;
  for (; (r = o.next()) && !r.done; ) {
    const s = r.value;
    t.call(e, s[0], s[1]);
  }
}, or = (e, t) => {
  let n;
  const o = [];
  for (; (n = e.exec(t)) !== null; )
    o.push(n);
  return o;
}, rr = oe("HTMLFormElement"), sr = (e) => e.toLowerCase().replace(/[-_\s]([a-z\d])(\w*)/g, function(n, o, r) {
  return o.toUpperCase() + r;
}), { propertyIsEnumerable: ar } = Object.prototype, ir = oe("RegExp"), Zt = (e, t) => {
  const n = Object.getOwnPropertyDescriptors(e), o = {};
  Ve(n, (r, s) => {
    let a;
    (a = t(r, s, e)) !== !1 && (o[s] = a || r);
  }), Object.defineProperties(e, o);
}, lr = (e) => {
  Zt(e, (t, n) => {
    if (Q(e) && ["arguments", "caller", "callee"].includes(n))
      return !1;
    const o = e[n];
    if (Q(o)) {
      if (t.enumerable = !1, "writable" in t) {
        t.writable = !1;
        return;
      }
      t.set || (t.set = () => {
        throw Error("Can not rewrite read-only method '" + n + "'");
      });
    }
  });
}, cr = (e, t) => {
  const n = {}, o = (r) => {
    r.forEach((s) => {
      n[s] = !0;
    });
  };
  return Oe(e) ? o(e) : o(String(e).split(t)), n;
}, ur = () => {
}, dr = (e, t) => e != null && Number.isFinite(e = +e) ? e : t;
function fr(e) {
  return !!(e && Q(e.append) && e[Yt] === "FormData" && e[$e]);
}
const pr = (e) => {
  const t = /* @__PURE__ */ new WeakSet(), n = (o) => {
    if (Ue(o)) {
      if (t.has(o))
        return;
      if (ze(o))
        return o;
      if (!("toJSON" in o)) {
        t.add(o);
        let r;
        if ($o(o)) {
          r = [];
          for (const s of o) {
            const a = n(s);
            !ve(a) && r.push(a);
          }
        } else
          r = Oe(o) ? [] : {}, Ve(o, (s, a) => {
            const i = n(s);
            !ve(i) && (r[a] = i);
          });
        return t.delete(o), r;
      }
    }
    return o;
  };
  return n(e);
}, hr = oe("AsyncFunction"), mr = (e) => e && (Ue(e) || Q(e)) && Q(e.then) && Q(e.catch), Gt = ((e, t) => e ? setImmediate : t ? ((n, o) => (Ee.addEventListener(
  "message",
  ({ source: r, data: s }) => {
    r === Ee && s === n && o.length && o.shift()();
  },
  !1
), (r) => {
  o.push(r), Ee.postMessage(n, "*");
}))(`axios@${Math.random()}`, []) : (n) => setTimeout(n))(typeof setImmediate == "function", Q(Ee.postMessage)), yr = typeof queueMicrotask < "u" ? queueMicrotask.bind(Ee) : typeof process < "u" && process.nextTick || Gt, Qt = (e) => e != null && Q(e[$e]), br = (e) => e != null && Be(e, $e) && Qt(e), l = {
  isArray: Oe,
  isArrayBuffer: Kt,
  isBuffer: ze,
  isFormData: jo,
  isArrayBufferView: Co,
  isString: Po,
  isNumber: Wt,
  isBoolean: ko,
  isObject: Ue,
  isPlainObject: He,
  isEmptyObject: zo,
  isReadableStream: Mo,
  isRequest: Ho,
  isResponse: Yo,
  isHeaders: Ko,
  isUndefined: ve,
  isDate: Uo,
  isFile: No,
  isReactNativeBlob: Do,
  isReactNative: Lo,
  isBlob: Fo,
  isRegExp: ir,
  isFunction: Q,
  isStream: Vo,
  isURLSearchParams: qo,
  isTypedArray: tr,
  isFileList: Bo,
  forEach: Ve,
  merge: ft,
  extend: Jo,
  trim: Wo,
  stripBOM: Xo,
  inherits: Zo,
  toFlatObject: Go,
  kindOf: yt,
  kindOfTest: oe,
  endsWith: Qo,
  toArray: er,
  forEachEntry: nr,
  matchAll: or,
  isHTMLForm: rr,
  hasOwnProperty: Je,
  hasOwnProp: Je,
  // an alias to avoid ESLint no-prototype-builtins detection
  hasOwnInPrototypeChain: Be,
  getSafeProp: xo,
  reduceDescriptors: Zt,
  freezeMethods: lr,
  toObjectSet: cr,
  toCamelCase: sr,
  noop: ur,
  toFiniteNumber: dr,
  findKey: Jt,
  global: Ee,
  isContextDefined: Xt,
  isSpecCompliantForm: fr,
  toJSONObject: pr,
  isAsyncFn: hr,
  isThenable: mr,
  setImmediate: Gt,
  asap: yr,
  isIterable: Qt,
  isSafeIterable: br
}, gr = l.toObjectSet([
  "age",
  "authorization",
  "content-length",
  "content-type",
  "etag",
  "expires",
  "from",
  "host",
  "if-modified-since",
  "if-unmodified-since",
  "last-modified",
  "location",
  "max-forwards",
  "proxy-authorization",
  "referer",
  "retry-after",
  "user-agent"
]), wr = (e) => {
  const t = {};
  let n, o, r;
  return e && e.split(`
`).forEach(function(a) {
    r = a.indexOf(":"), n = a.substring(0, r).trim().toLowerCase(), o = a.substring(r + 1).trim();
    const i = l.hasOwnProp(t, n);
    !n || i && l.hasOwnProp(gr, n) || (n === "set-cookie" ? i ? t[n].push(o) : t[n] = [o] : t[n] = i ? t[n] + ", " + o : o);
  }), t;
};
function _r(e) {
  let t = 0, n = e.length;
  for (; t < n; ) {
    const o = e.charCodeAt(t);
    if (o !== 9 && o !== 32)
      break;
    t += 1;
  }
  for (; n > t; ) {
    const o = e.charCodeAt(n - 1);
    if (o !== 9 && o !== 32)
      break;
    n -= 1;
  }
  return t === 0 && n === e.length ? e : e.slice(t, n);
}
const Sr = new RegExp("[\\u0000-\\u0008\\u000a-\\u001f\\u007f]+", "g"), Er = new RegExp("[^\\u0009\\u0020-\\u007e\\u0080-\\u00ff]+", "g");
function bt(e, t) {
  return l.isArray(e) ? e.map((n) => bt(n, t)) : _r(String(e).replace(t, ""));
}
const Rr = (e) => bt(e, Sr), Or = (e) => bt(e, Er);
function en(e) {
  const t = /* @__PURE__ */ Object.create(null);
  return l.forEach(e.toJSON(), (n, o) => {
    t[o] = Or(n);
  }), t;
}
const xt = Symbol("internals");
function De(e) {
  return e && String(e).trim().toLowerCase();
}
function Ye(e) {
  return e === !1 || e == null ? e : l.isArray(e) ? e.map(Ye) : Rr(String(e));
}
function vr(e) {
  const t = /* @__PURE__ */ Object.create(null), n = /([^\s,;=]+)\s*(?:=\s*([^,;]+))?/g;
  let o;
  for (; o = n.exec(e); )
    t[o[1]] = o[2];
  return t;
}
const Ar = /^[!#$%&'*+\-.^_`|~0-9A-Za-z]+$/;
function st(e) {
  let t = 0, n = e.length;
  for (; t < n; ) {
    const o = e.charCodeAt(t);
    if (o !== 9 && o !== 32)
      break;
    t += 1;
  }
  for (; n > t; ) {
    const o = e.charCodeAt(n - 1);
    if (o !== 9 && o !== 32)
      break;
    n -= 1;
  }
  return t === 0 && n === e.length ? e : e.slice(t, n);
}
function Tr(e) {
  const t = e.length - 1;
  if (t < 1 || e.charCodeAt(0) !== 34 || e.charCodeAt(t) !== 34)
    return e;
  let n = "";
  for (let o = 1; o < t; o++) {
    const r = e.charCodeAt(o);
    if (r === 34 || r === 92 && (o += 1, o >= t))
      return e;
    n += e[o];
  }
  return n;
}
function xr(e) {
  const t = /* @__PURE__ */ Object.create(null), n = String(e);
  let o = 0, r = !1, s = !1;
  function a(i) {
    const u = st(n.slice(o, i)), p = u.indexOf("=");
    if (p < 1)
      return;
    const c = st(u.slice(0, p));
    if (!Ar.test(c))
      return;
    const m = c.toLowerCase();
    if (m === "__proto__" || m === "constructor" || m === "prototype")
      return;
    const b = st(u.slice(p + 1));
    t[m] = Tr(b);
  }
  for (let i = 0; i < n.length; i++) {
    const u = n.charCodeAt(i);
    r ? s ? s = !1 : u === 92 ? s = !0 : u === 34 && (r = !1) : u === 34 ? r = !0 : (u === 44 || u === 59) && (a(i), o = i + 1);
  }
  return a(n.length), t;
}
const Cr = (e) => /^[-_a-zA-Z0-9^`|~,!#$%&'*+.]+$/.test(e.trim());
function at(e, t, n, o, r) {
  if (l.isFunction(o))
    return o.call(this, t, n);
  if (r && (t = n), !!l.isString(t)) {
    if (l.isString(o))
      return t.indexOf(o) !== -1;
    if (l.isRegExp(o))
      return o.test(t);
  }
}
function Pr(e) {
  return e.trim().toLowerCase().replace(/([a-z\d])(\w*)/g, (t, n, o) => n.toUpperCase() + o);
}
function kr(e, t) {
  const n = l.toCamelCase(" " + t);
  ["get", "set", "has"].forEach((o) => {
    Object.defineProperty(e, o + n, {
      // Null-proto descriptor so a polluted Object.prototype.get cannot turn
      // this data descriptor into an accessor descriptor on the way in.
      __proto__: null,
      value: function(r, s, a) {
        return this[o].call(this, t, r, s, a);
      },
      configurable: !0
    });
  });
}
let J = class {
  constructor(t) {
    t && this.set(t);
  }
  set(t, n, o) {
    const r = this;
    function s(i, u, p) {
      const c = De(u);
      if (!c)
        return;
      const m = l.findKey(r, c);
      (!m || r[m] === void 0 || p === !0 || p === void 0 && r[m] !== !1) && (r[m || u] = Ye(i));
    }
    const a = (i, u) => l.forEach(i, (p, c) => s(p, c, u));
    if (l.isPlainObject(t) || t instanceof this.constructor)
      a(t, n);
    else if (l.isString(t) && (t = t.trim()) && !Cr(t))
      a(wr(t), n);
    else if (l.isObject(t) && l.isSafeIterable(t)) {
      let i = /* @__PURE__ */ Object.create(null), u, p;
      for (const c of t) {
        if (!l.isArray(c))
          throw new TypeError("Object iterator must return a key-value pair");
        p = c[0], l.hasOwnProp(i, p) ? (u = i[p], i[p] = l.isArray(u) ? [...u, c[1]] : [u, c[1]]) : i[p] = c[1];
      }
      a(i, n);
    } else
      t != null && s(n, t, o);
    return this;
  }
  get(t, n) {
    if (t = De(t), t) {
      const o = l.findKey(this, t);
      if (o) {
        const r = this[o];
        if (!n)
          return r;
        if (n === !0)
          return vr(r);
        if (l.isFunction(n))
          return n.call(this, r, o);
        if (l.isRegExp(n))
          return n.exec(r);
        throw new TypeError("parser must be boolean|regexp|function");
      }
    }
  }
  has(t, n) {
    if (t = De(t), t) {
      const o = l.findKey(this, t);
      return !!(o && this[o] !== void 0 && (!n || at(this, this[o], o, n)));
    }
    return !1;
  }
  delete(t, n) {
    const o = this;
    let r = !1;
    function s(a) {
      if (a = De(a), a) {
        const i = l.findKey(o, a);
        i && (!n || at(o, o[i], i, n)) && (delete o[i], r = !0);
      }
    }
    return l.isArray(t) ? t.forEach(s) : s(t), r;
  }
  clear(t) {
    const n = Object.keys(this);
    let o = n.length, r = !1;
    for (; o--; ) {
      const s = n[o];
      (!t || at(this, this[s], s, t, !0)) && (delete this[s], r = !0);
    }
    return r;
  }
  normalize(t) {
    const n = this, o = {};
    return l.forEach(this, (r, s) => {
      const a = l.findKey(o, s);
      if (a) {
        n[a] = Ye(r), delete n[s];
        return;
      }
      const i = t ? Pr(s) : String(s).trim();
      i !== s && delete n[s], n[i] = Ye(r), o[i] = !0;
    }), this;
  }
  concat(...t) {
    return this.constructor.concat(this, ...t);
  }
  toJSON(t) {
    const n = /* @__PURE__ */ Object.create(null);
    return l.forEach(this, (o, r) => {
      o != null && o !== !1 && (n[r] = t && l.isArray(o) ? o.join(", ") : o);
    }), n;
  }
  [Symbol.iterator]() {
    return Object.entries(this.toJSON())[Symbol.iterator]();
  }
  toString() {
    return Object.entries(this.toJSON()).map(([t, n]) => t + ": " + n).join(`
`);
  }
  getSetCookie() {
    const t = this.get("set-cookie");
    return l.isArray(t) ? t : t == null || t === !1 ? [] : [t];
  }
  get [Symbol.toStringTag]() {
    return "AxiosHeaders";
  }
  static from(t) {
    return t instanceof this ? t : new this(t);
  }
  static parseParameters(t) {
    return xr(t);
  }
  static concat(t, ...n) {
    const o = new this(t);
    return n.forEach((r) => o.set(r)), o;
  }
  static accessor(t) {
    const o = (this[xt] = this[xt] = {
      accessors: {}
    }).accessors, r = this.prototype;
    function s(a) {
      const i = De(a);
      o[i] || (kr(r, a), o[i] = !0);
    }
    return l.isArray(t) ? t.forEach(s) : s(t), this;
  }
};
J.accessor([
  "Content-Type",
  "Content-Length",
  "Accept",
  "Accept-Encoding",
  "User-Agent",
  "Authorization"
]);
l.reduceDescriptors(J.prototype, ({ value: e }, t) => {
  let n = t[0].toUpperCase() + t.slice(1);
  return {
    get: () => e,
    set(o) {
      this[n] = o;
    }
  };
});
l.freezeMethods(J);
const Xe = "[REDACTED ****]";
function zr(e) {
  if (l.hasOwnProp(e, "toJSON"))
    return !0;
  let t = Object.getPrototypeOf(e);
  for (; t && t !== Object.prototype; ) {
    if (l.hasOwnProp(t, "toJSON"))
      return !0;
    t = Object.getPrototypeOf(t);
  }
  return !1;
}
function Ur(e, t) {
  const n = new Set(t.map((s) => String(s).toLowerCase())), o = [], r = (s) => {
    if (s === null || typeof s != "object" || l.isBuffer(s)) return s;
    if (o.indexOf(s) !== -1) return;
    s instanceof J && (s = s.toJSON()), o.push(s);
    let a;
    if (l.isArray(s))
      a = [], s.forEach((i, u) => {
        const p = r(i);
        l.isUndefined(p) || (a[u] = p);
      });
    else {
      if (!l.isPlainObject(s) && zr(s))
        return o.pop(), s;
      a = /* @__PURE__ */ Object.create(null);
      for (const [i, u] of Object.entries(s)) {
        const p = n.has(i.toLowerCase()) ? Xe : r(u);
        l.isUndefined(p) || (a[i] = p);
      }
    }
    return o.pop(), a;
  };
  return r(e);
}
function Ct(e) {
  try {
    return String(e);
  } catch {
    return "";
  }
}
function Nr(e) {
  return e.errors.map((n) => {
    try {
      return n && n.message ? Ct(n.message) : Ct(n);
    } catch {
      return "";
    }
  }).filter(Boolean).join("; ") || e.name || "AggregateError";
}
let y = class tn extends Error {
  static from(t, n, o, r, s, a) {
    let i = t.message;
    !i && l.isArray(t.errors) && t.errors.length && (i = Nr(t));
    const u = new tn(i, n || t.code, o, r, s);
    return Object.defineProperty(u, "cause", {
      __proto__: null,
      value: t,
      writable: !0,
      enumerable: !1,
      configurable: !0
    }), u.name = t.name, t.status != null && u.status == null && (u.status = t.status), a && Object.assign(u, a), u;
  }
  /**
   * Create an Error with the specified message, config, error code, request and response.
   *
   * @param {string} message The error message.
   * @param {string} [code] The error code (for example, 'ECONNABORTED').
   * @param {Object} [config] The config.
   * @param {Object} [request] The request.
   * @param {Object} [response] The response.
   *
   * @returns {Error} The created error.
   */
  constructor(t, n, o, r, s) {
    super(t), Object.defineProperty(this, "message", {
      // Null-proto descriptor so a polluted Object.prototype.get cannot turn
      // this data descriptor into an accessor descriptor on the way in.
      __proto__: null,
      value: t,
      enumerable: !0,
      writable: !0,
      configurable: !0
    }), this.name = "AxiosError", this.isAxiosError = !0, n && (this.code = n), o && (this.config = o), r && (this.request = r), s && (this.response = s, this.status = s.status);
  }
  toJSON() {
    const t = this.config, n = t && l.hasOwnProp(t, "redact") ? t.redact : void 0, o = l.isArray(n) && n.length > 0 ? Ur(t, n) : l.toJSONObject(t);
    return {
      // Standard
      message: this.message,
      name: this.name,
      // Microsoft
      description: this.description,
      number: this.number,
      // Mozilla
      fileName: this.fileName,
      lineNumber: this.lineNumber,
      columnNumber: this.columnNumber,
      stack: this.stack,
      // Axios
      config: o,
      code: this.code,
      status: this.status
    };
  }
};
y.ERR_BAD_OPTION_VALUE = "ERR_BAD_OPTION_VALUE";
y.ERR_BAD_OPTION = "ERR_BAD_OPTION";
y.ECONNABORTED = "ECONNABORTED";
y.ETIMEDOUT = "ETIMEDOUT";
y.ECONNREFUSED = "ECONNREFUSED";
y.ERR_NETWORK = "ERR_NETWORK";
y.ERR_FR_TOO_MANY_REDIRECTS = "ERR_FR_TOO_MANY_REDIRECTS";
y.ERR_DEPRECATED = "ERR_DEPRECATED";
y.ERR_BAD_RESPONSE = "ERR_BAD_RESPONSE";
y.ERR_BAD_REQUEST = "ERR_BAD_REQUEST";
y.ERR_CANCELED = "ERR_CANCELED";
y.ERR_NOT_SUPPORT = "ERR_NOT_SUPPORT";
y.ERR_INVALID_URL = "ERR_INVALID_URL";
y.ERR_FORM_DATA_DEPTH_EXCEEDED = "ERR_FORM_DATA_DEPTH_EXCEEDED";
const Dr = null, nn = 100;
function pt(e) {
  return l.isPlainObject(e) || l.isArray(e);
}
function on(e) {
  return l.endsWith(e, "[]") ? e.slice(0, -2) : e;
}
function it(e, t, n) {
  return e ? e.concat(t).map(function(r, s) {
    return r = on(r), !n && s ? "[" + r + "]" : r;
  }).join(n ? "." : "") : t;
}
function Lr(e) {
  return l.isArray(e) && !e.some(pt);
}
const Fr = l.toFlatObject(l, {}, null, function(t) {
  return /^is[A-Z]/.test(t);
});
function et(e, t, n) {
  if (!l.isObject(e))
    throw new TypeError("target must be an object");
  t = t || new FormData(), n = l.toFlatObject(
    n,
    {
      metaTokens: !0,
      dots: !1,
      indexes: !1
    },
    !1,
    function(_, O) {
      return !l.isUndefined(O[_]);
    }
  );
  const o = n.metaTokens, r = n.visitor || R, s = n.dots, a = n.indexes, i = n.Blob || typeof Blob < "u" && Blob, u = n.maxDepth === void 0 ? nn : n.maxDepth, p = i && l.isSpecCompliantForm(t), c = [];
  if (!l.isFunction(r))
    throw new TypeError("visitor must be a function");
  function m(f) {
    if (f === null) return "";
    if (l.isDate(f))
      return f.toISOString();
    if (l.isBoolean(f))
      return f.toString();
    if (!p && l.isBlob(f))
      throw new y("Blob is not supported. Use a Buffer instead.");
    if (l.isArrayBuffer(f) || l.isTypedArray(f)) {
      if (p && typeof i == "function")
        return new i([f]);
      throw new y("Blob is not supported. Use a Buffer instead.", y.ERR_NOT_SUPPORT);
    }
    return f;
  }
  function b(f) {
    if (f > u)
      throw new y(
        "Object is too deeply nested (" + f + " levels). Max depth: " + u,
        y.ERR_FORM_DATA_DEPTH_EXCEEDED
      );
  }
  function w(f, _) {
    if (u === 1 / 0)
      return JSON.stringify(f);
    const O = [];
    return JSON.stringify(f, function($, L) {
      if (!l.isObject(L))
        return L;
      for (; O.length && O[O.length - 1] !== this; )
        O.pop();
      return O.push(L), b(_ + O.length - 1), L;
    });
  }
  function R(f, _, O) {
    let k = f;
    if (l.isReactNative(t) && l.isReactNativeBlob(f))
      return t.append(it(O, _, s), m(f)), !1;
    if (f && !O && typeof f == "object") {
      if (l.endsWith(_, "{}"))
        _ = o ? _ : _.slice(0, -2), f = w(f, 1);
      else if (l.isArray(f) && Lr(f) || (l.isFileList(f) || l.endsWith(_, "[]")) && (k = l.toArray(f)))
        return _ = on(_), k.forEach(function(L, ee) {
          !(l.isUndefined(L) || L === null) && t.append(
            // eslint-disable-next-line no-nested-ternary
            a === !0 ? it([_], ee, s) : a === null ? _ : _ + "[]",
            m(L)
          );
        }), !1;
    }
    return pt(f) ? !0 : (t.append(it(O, _, s), m(f)), !1);
  }
  const T = Object.assign(Fr, {
    defaultVisitor: R,
    convertValue: m,
    isVisitable: pt
  });
  function d(f, _, O = 0) {
    if (!l.isUndefined(f)) {
      if (b(O), c.indexOf(f) !== -1)
        throw new Error("Circular reference detected in " + _.join("."));
      c.push(f), l.forEach(f, function($, L) {
        (!(l.isUndefined($) || $ === null) && r.call(t, $, l.isString(L) ? L.trim() : L, _, T)) === !0 && d($, _ ? _.concat(L) : [L], O + 1);
      }), c.pop();
    }
  }
  if (!l.isObject(e))
    throw new TypeError("data must be an object");
  return d(e), t;
}
function Pt(e) {
  const t = {
    "!": "%21",
    "'": "%27",
    "(": "%28",
    ")": "%29",
    "~": "%7E",
    "%20": "+"
  };
  return encodeURIComponent(e).replace(/[!'()~]|%20/g, function(o) {
    return t[o];
  });
}
function gt(e, t) {
  this._pairs = [], e && et(e, this, t);
}
const rn = gt.prototype;
rn.append = function(t, n) {
  this._pairs.push([t, n]);
};
rn.toString = function(t) {
  const n = t ? (o) => t.call(this, o, Pt) : Pt;
  return this._pairs.map(function(r) {
    return n(r[0]) + "=" + n(r[1]);
  }, "").join("&");
};
function Br(e) {
  return encodeURIComponent(e).replace(/%3A/gi, ":").replace(/%24/g, "$").replace(/%2C/gi, ",").replace(/%20/g, "+");
}
function sn(e, t, n) {
  if (!t)
    return e;
  e = e || "";
  const o = l.isFunction(n) ? {
    serialize: n
  } : n, r = l.getSafeProp(o, "encode") || Br, s = l.getSafeProp(o, "serialize");
  let a;
  if (s ? a = s(t, o) : a = l.isURLSearchParams(t) ? t.toString() : new gt(t, o).toString(r), a) {
    const i = e.indexOf("#");
    i !== -1 && (e = e.slice(0, i)), e += (e.indexOf("?") === -1 ? "?" : "&") + a;
  }
  return e;
}
class kt {
  constructor() {
    this.handlers = [];
  }
  /**
   * Add a new interceptor to the stack
   *
   * @param {Function} fulfilled The function to handle `then` for a `Promise`
   * @param {Function} rejected The function to handle `reject` for a `Promise`
   * @param {Object} options The options for the interceptor, synchronous and runWhen
   *
   * @return {Number} An ID used to remove interceptor later
   */
  use(t, n, o) {
    return this.handlers.push({
      fulfilled: t,
      rejected: n,
      synchronous: o ? o.synchronous : !1,
      runWhen: o ? o.runWhen : null
    }), this.handlers.length - 1;
  }
  /**
   * Remove an interceptor from the stack
   *
   * @param {Number} id The ID that was returned by `use`
   *
   * @returns {void}
   */
  eject(t) {
    this.handlers[t] && (this.handlers[t] = null);
  }
  /**
   * Clear all interceptors from the stack
   *
   * @returns {void}
   */
  clear() {
    this.handlers && (this.handlers = []);
  }
  /**
   * Iterate over all the registered interceptors
   *
   * This method is particularly useful for skipping over any
   * interceptors that may have become `null` calling `eject`.
   *
   * @param {Function} fn The function to call for each interceptor
   *
   * @returns {void}
   */
  forEach(t) {
    l.forEach(this.handlers, function(o) {
      o !== null && t(o);
    });
  }
}
const wt = {
  silentJSONParsing: !0,
  forcedJSONParsing: !0,
  clarifyTimeoutError: !1,
  legacyInterceptorReqResOrdering: !0,
  advertiseZstdAcceptEncoding: !1,
  validateStatusUndefinedResolves: !0
}, $r = typeof URLSearchParams < "u" ? URLSearchParams : gt, Vr = typeof FormData < "u" ? FormData : null, Ir = typeof Blob < "u" ? Blob : null, jr = {
  isBrowser: !0,
  classes: {
    URLSearchParams: $r,
    FormData: Vr,
    Blob: Ir
  },
  protocols: ["http", "https", "file", "blob", "url", "data"]
}, _t = typeof window < "u" && typeof document < "u", ht = typeof navigator == "object" && navigator || void 0, qr = _t && (!ht || ["ReactNative", "NativeScript", "NS"].indexOf(ht.product) < 0), Mr = typeof WorkerGlobalScope < "u" && // eslint-disable-next-line no-undef
self instanceof WorkerGlobalScope && typeof self.importScripts == "function", Hr = _t && window.location.href || "http://localhost", Yr = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  hasBrowserEnv: _t,
  hasStandardBrowserEnv: qr,
  hasStandardBrowserWebWorkerEnv: Mr,
  navigator: ht,
  origin: Hr
}, Symbol.toStringTag, { value: "Module" })), Y = {
  ...Yr,
  ...jr
};
function Kr(e, t) {
  return et(e, new Y.classes.URLSearchParams(), {
    visitor: function(n, o, r, s) {
      return Y.isNode && l.isBuffer(n) ? (this.append(o, n.toString("base64")), !1) : s.defaultVisitor.apply(this, arguments);
    },
    ...t
  });
}
const zt = nn;
function an(e) {
  if (e > zt)
    throw new y(
      "FormData field is too deeply nested (" + e + " levels). Max depth: " + zt,
      y.ERR_FORM_DATA_DEPTH_EXCEEDED
    );
}
function Wr(e) {
  const t = [], n = /[^.[\]]+|\[([^.[\]]*)]/g;
  let o;
  for (; (o = n.exec(e)) !== null; )
    an(t.length), t.push(o[0] === "[]" ? "" : o[1] || o[0]);
  return t;
}
function Jr(e) {
  const t = {}, n = Object.keys(e);
  let o;
  const r = n.length;
  let s;
  for (o = 0; o < r; o++)
    s = n[o], t[s] = e[s];
  return t;
}
function ln(e) {
  function t(n, o, r, s) {
    an(s);
    let a = n[s++];
    if (a === "__proto__") return !0;
    const i = Number.isFinite(+a), u = s >= n.length;
    return a = !a && l.isArray(r) ? r.length : a, u ? (l.hasOwnProp(r, a) ? r[a] = l.isArray(r[a]) ? r[a].concat(o) : [r[a], o] : r[a] = o, !i) : ((!l.hasOwnProp(r, a) || !l.isObject(r[a])) && (r[a] = []), t(n, o, r[a], s) && l.isArray(r[a]) && (r[a] = Jr(r[a])), !i);
  }
  if (l.isFormData(e) && l.isFunction(e.entries)) {
    const n = {};
    return l.forEachEntry(e, (o, r) => {
      t(Wr(o), r, n, 0);
    }), n;
  }
  return null;
}
const Pe = (e, t) => e != null && l.hasOwnProp(e, t) ? e[t] : void 0;
function Xr(e, t, n) {
  if (l.isString(e))
    try {
      return (t || JSON.parse)(e), l.trim(e);
    } catch (o) {
      if (o.name !== "SyntaxError")
        throw o;
    }
  return (n || JSON.stringify)(e);
}
const Ie = {
  transitional: wt,
  adapter: ["xhr", "http", "fetch"],
  transformRequest: [
    function(t, n) {
      const o = n.getContentType() || "", r = o.indexOf("application/json") > -1, s = l.isObject(t);
      if (s && l.isHTMLForm(t) && (t = new FormData(t)), l.isFormData(t))
        return r ? JSON.stringify(ln(t)) : t;
      if (l.isArrayBuffer(t) || l.isBuffer(t) || l.isStream(t) || l.isFile(t) || l.isBlob(t) || l.isReadableStream(t))
        return t;
      if (l.isArrayBufferView(t))
        return t.buffer;
      if (l.isURLSearchParams(t))
        return n.setContentType("application/x-www-form-urlencoded;charset=utf-8", !1), t.toString();
      let i;
      if (s) {
        const u = Pe(this, "formSerializer");
        if (o.indexOf("application/x-www-form-urlencoded") > -1)
          return Kr(t, u).toString();
        if ((i = l.isFileList(t)) || o.indexOf("multipart/form-data") > -1) {
          const p = Pe(this, "env"), c = p && p.FormData;
          return et(
            i ? { "files[]": t } : t,
            c && new c(),
            u
          );
        }
      }
      return s || r ? (n.setContentType("application/json", !1), Xr(t)) : t;
    }
  ],
  transformResponse: [
    function(t) {
      const n = Pe(this, "transitional") || Ie.transitional, o = n && n.forcedJSONParsing, r = Pe(this, "responseType"), s = r === "json";
      if (l.isResponse(t) || l.isReadableStream(t))
        return t;
      if (t && l.isString(t) && (o && !r || s)) {
        const i = !(n && n.silentJSONParsing) && s;
        try {
          return JSON.parse(t, Pe(this, "parseReviver"));
        } catch (u) {
          if (i)
            throw u.name === "SyntaxError" ? y.from(u, y.ERR_BAD_RESPONSE, this, null, Pe(this, "response")) : u;
        }
      }
      return t;
    }
  ],
  /**
   * A timeout in milliseconds to abort a request. If set to 0 (default) a
   * timeout is not created.
   */
  timeout: 0,
  xsrfCookieName: "XSRF-TOKEN",
  xsrfHeaderName: "X-XSRF-TOKEN",
  maxContentLength: -1,
  maxBodyLength: -1,
  env: {
    FormData: Y.classes.FormData,
    Blob: Y.classes.Blob
  },
  validateStatus: function(t) {
    return t >= 200 && t < 300;
  },
  headers: {
    common: {
      Accept: "application/json, text/plain, */*",
      "Content-Type": void 0
    }
  }
};
l.forEach(["delete", "get", "head", "post", "put", "patch", "query"], (e) => {
  Ie.headers[e] = {};
});
function lt(e, t) {
  const n = this || Ie, o = t || n, r = J.from(o.headers);
  let s = o.data;
  return l.forEach(e, function(i) {
    s = i.call(n, s, r.normalize(), t ? t.status : void 0);
  }), r.normalize(), s;
}
function cn(e) {
  return !!(e && e.__CANCEL__);
}
let je = class extends y {
  /**
   * A `CanceledError` is an object that is thrown when an operation is canceled.
   *
   * @param {string=} message The message.
   * @param {Object=} config The config.
   * @param {Object=} request The request.
   *
   * @returns {CanceledError} The created error.
   */
  constructor(t, n, o) {
    super(t ?? "canceled", y.ERR_CANCELED, n, o), this.name = "CanceledError", this.__CANCEL__ = !0;
  }
};
function un(e, t, n) {
  const o = n.config.validateStatus;
  !n.status || !o || o(n.status) ? e(n) : t(new y(
    "Request failed with status code " + n.status,
    n.status >= 400 && n.status < 500 ? y.ERR_BAD_REQUEST : y.ERR_BAD_RESPONSE,
    n.config,
    n.request,
    n
  ));
}
function Zr(e) {
  const t = /^([-+\w]{1,25}):(?:\/\/)?/.exec(e);
  return t && t[1] || "";
}
function Gr(e, t) {
  e = e || 10;
  const n = new Array(e), o = new Array(e);
  let r = 0, s = 0, a;
  return t = t !== void 0 ? t : 1e3, function(u) {
    const p = Date.now(), c = o[s];
    a || (a = p), n[r] = u, o[r] = p;
    let m = s, b = 0;
    for (; m !== r; )
      b += n[m++], m = m % e;
    if (r = (r + 1) % e, r === s && (s = (s + 1) % e), p - a < t)
      return;
    const w = c && p - c;
    return w ? Math.round(b * 1e3 / w) : void 0;
  };
}
function Qr(e, t) {
  let n = 0, o = 1e3 / t, r, s;
  const a = (p, c = Date.now()) => {
    n = c, r = null, s && (clearTimeout(s), s = null), e(...p);
  };
  return [(...p) => {
    const c = Date.now(), m = c - n;
    m >= o ? a(p, c) : (r = p, s || (s = setTimeout(() => {
      s = null, a(r);
    }, o - m)));
  }, () => r && a(r)];
}
const Ze = (e, t, n = 3) => {
  let o = 0;
  const r = Gr(50, 250);
  return Qr((s) => {
    if (!s || typeof s.loaded != "number")
      return;
    const a = s.loaded, i = s.lengthComputable ? s.total : void 0, u = Math.max(0, i != null ? Math.min(a, i) : a), p = Math.max(0, u - o), c = r(p);
    o = Math.max(o, u);
    const m = {
      loaded: u,
      total: i,
      progress: i ? u / i : void 0,
      bytes: p,
      rate: c || void 0,
      estimated: c && i ? (i - u) / c : void 0,
      event: s,
      lengthComputable: i != null,
      [t ? "download" : "upload"]: !0
    };
    e(m);
  }, n);
}, Ut = (e, t) => {
  const n = e != null;
  return [
    (o) => t[0]({
      lengthComputable: n,
      total: e,
      loaded: o
    }),
    t[1]
  ];
}, Nt = (e, t = l.asap) => (...n) => t(() => e(...n)), es = Y.hasStandardBrowserEnv ? /* @__PURE__ */ ((e, t) => (n) => (n = new URL(n, Y.origin), e.protocol === n.protocol && e.host === n.host && (t || e.port === n.port)))(
  new URL(Y.origin),
  Y.navigator && /(msie|trident)/i.test(Y.navigator.userAgent)
) : () => !0, ts = Y.hasStandardBrowserEnv ? (
  // Standard browser envs support document.cookie
  {
    write(e, t, n, o, r, s, a) {
      if (typeof document > "u") return;
      const i = [`${e}=${encodeURIComponent(t)}`];
      l.isNumber(n) && i.push(`expires=${new Date(n).toUTCString()}`), l.isString(o) && i.push(`path=${o}`), l.isString(r) && i.push(`domain=${r}`), s === !0 && i.push("secure"), l.isString(a) && i.push(`SameSite=${a}`), document.cookie = i.join("; ");
    },
    read(e) {
      if (typeof document > "u") return null;
      const t = document.cookie.split(";");
      for (let n = 0; n < t.length; n++) {
        const o = t[n].replace(/^\s+/, ""), r = o.indexOf("=");
        if (r !== -1 && o.slice(0, r) === e)
          try {
            return decodeURIComponent(o.slice(r + 1));
          } catch {
            return o.slice(r + 1);
          }
      }
      return null;
    },
    remove(e) {
      this.write(e, "", Date.now() - 864e5, "/");
    }
  }
) : (
  // Non-standard browser env (web workers, react-native) lack needed support.
  {
    write() {
    },
    read() {
      return null;
    },
    remove() {
    }
  }
);
function ns(e) {
  return typeof e != "string" ? !1 : /^([a-z][a-z\d+\-.]*:)?\/\//i.test(e);
}
function os(e, t) {
  if (!t)
    return e;
  let n = e.length;
  for (; n > 0 && e.charCodeAt(n - 1) === 47; )
    n--;
  return e.slice(0, n) + "/" + t.replace(/^\/+/, "");
}
const rs = /^https?:(?!\/\/)/i, ss = /[\t\n\r]/g;
function as(e) {
  let t = 0;
  for (; t < e.length && e.charCodeAt(t) <= 32; )
    t++;
  return e.slice(t);
}
function is(e) {
  return as(e).replace(ss, "");
}
function ls(e) {
  return e && e.replace(/(^|&)([^=&]*=)?[^&]+/g, (t, n, o = "") => `${n}${o}${Xe}`);
}
function cs(e) {
  const t = e.replace(/^(https?:\/{0,2})[^/?#]*@/i, `$1${Xe}@`), n = t.indexOf("#"), r = (n === -1 ? t : t.slice(0, n)).replace(
    /([?&][^=&#]*=)[^&#]*/g,
    `$1${Xe}`
  );
  return n === -1 ? r : `${r}#${ls(t.slice(n + 1))}`;
}
function Dt(e, t) {
  if (typeof e == "string") {
    const n = is(e);
    if (rs.test(n))
      throw new y(
        `Invalid URL ${JSON.stringify(cs(n))}: missing "//" after protocol`,
        y.ERR_INVALID_URL,
        t
      );
  }
}
function dn(e, t, n, o) {
  Dt(t, o);
  let r = !ns(t);
  return e && (r || n === !1) ? (Dt(e, o), os(e, t)) : t;
}
const Lt = (e) => e instanceof J ? { ...e } : e, us = (e) => Object.getOwnPropertySymbols && Object.getOwnPropertyDescriptor ? Object.keys(e).concat(
  Object.getOwnPropertySymbols(e).filter(
    (t) => Object.getOwnPropertyDescriptor(e, t).enumerable
  )
) : Object.keys(e);
function Ae(e, t) {
  e = e || {}, t = t || {};
  const n = /* @__PURE__ */ Object.create(null);
  Object.defineProperty(n, "hasOwnProperty", {
    // Null-proto descriptor so a polluted Object.prototype.get cannot turn
    // this data descriptor into an accessor descriptor on the way in.
    __proto__: null,
    value: Object.prototype.hasOwnProperty,
    enumerable: !1,
    writable: !0,
    configurable: !0
  });
  function o(c, m, b, w) {
    return l.isPlainObject(c) && l.isPlainObject(m) ? l.merge.call({ caseless: w }, c, m) : l.isPlainObject(m) ? l.merge({}, m) : l.isArray(m) ? m.slice() : m;
  }
  function r(c, m, b, w) {
    if (l.isUndefined(m)) {
      if (!l.isUndefined(c))
        return o(void 0, c, b, w);
    } else return o(c, m, b, w);
  }
  function s(c, m) {
    if (!l.isUndefined(m))
      return o(void 0, m);
  }
  function a(c, m) {
    if (l.isUndefined(m)) {
      if (!l.isUndefined(c))
        return o(void 0, c);
    } else return o(void 0, m);
  }
  function i(c) {
    const m = l.hasOwnProp(t, "transitional") ? t.transitional : void 0;
    if (!l.isUndefined(m))
      if (l.isPlainObject(m)) {
        if (l.hasOwnProp(m, c))
          return m[c];
      } else
        return;
    const b = l.hasOwnProp(e, "transitional") ? e.transitional : void 0;
    if (l.isPlainObject(b) && l.hasOwnProp(b, c))
      return b[c];
  }
  function u(c, m, b) {
    if (l.hasOwnProp(t, b))
      return o(c, m);
    if (l.hasOwnProp(e, b))
      return o(void 0, c);
  }
  const p = {
    url: s,
    method: s,
    data: s,
    baseURL: a,
    transformRequest: a,
    transformResponse: a,
    paramsSerializer: a,
    timeout: a,
    timeoutMessage: a,
    withCredentials: a,
    withXSRFToken: a,
    adapter: a,
    responseType: a,
    xsrfCookieName: a,
    xsrfHeaderName: a,
    onUploadProgress: a,
    onDownloadProgress: a,
    decompress: a,
    maxContentLength: a,
    maxBodyLength: a,
    beforeRedirect: a,
    transport: a,
    httpAgent: a,
    httpsAgent: a,
    cancelToken: a,
    socketPath: a,
    allowedSocketPaths: a,
    responseEncoding: a,
    validateStatus: u,
    headers: (c, m, b) => r(Lt(c), Lt(m), b, !0)
  };
  return l.forEach(us({ ...e, ...t }), function(m) {
    if (m === "__proto__" || m === "constructor" || m === "prototype") return;
    const b = l.hasOwnProp(p, m) ? p[m] : r, w = l.hasOwnProp(e, m) ? e[m] : void 0, R = l.hasOwnProp(t, m) ? t[m] : void 0, T = b(w, R, m);
    l.isUndefined(T) && b !== u || (n[m] = T);
  }), l.hasOwnProp(t, "validateStatus") && l.isUndefined(t.validateStatus) && i("validateStatusUndefinedResolves") === !1 && (l.hasOwnProp(e, "validateStatus") ? n.validateStatus = o(void 0, e.validateStatus) : delete n.validateStatus), n;
}
const ds = ["content-type", "content-length"];
function fs(e, t, n) {
  if (n !== "content-only") {
    e.set(t);
    return;
  }
  Object.entries(t || {}).forEach(([o, r]) => {
    ds.includes(o.toLowerCase()) && e.set(o, r);
  });
}
const ps = (e) => encodeURIComponent(e).replace(
  /%([0-9A-F]{2})/gi,
  (t, n) => String.fromCharCode(parseInt(n, 16))
);
function fn(e) {
  const t = Ae({}, e), n = (b) => l.hasOwnProp(t, b) ? t[b] : void 0, o = n("data");
  let r = n("withXSRFToken");
  const s = n("xsrfHeaderName"), a = n("xsrfCookieName");
  let i = n("headers");
  const u = n("auth"), p = n("baseURL"), c = n("allowAbsoluteUrls"), m = n("url");
  if (t.headers = i = J.from(i), t.url = sn(
    dn(p, m, c, t),
    n("params"),
    n("paramsSerializer")
  ), u) {
    const b = l.getSafeProp(u, "username") || "", w = l.getSafeProp(u, "password") || "";
    try {
      i.set(
        "Authorization",
        "Basic " + btoa(b + ":" + (w ? ps(w) : ""))
      );
    } catch (R) {
      throw y.from(R, y.ERR_BAD_OPTION_VALUE, e);
    }
  }
  if (l.isFormData(o) && (Y.hasStandardBrowserEnv || Y.hasStandardBrowserWebWorkerEnv || l.isReactNative(o) ? i.setContentType(void 0) : l.isFunction(o.getHeaders) && fs(i, o.getHeaders(), n("formDataHeaderPolicy"))), Y.hasStandardBrowserEnv && (l.isFunction(r) && (r = r(t)), r === !0 || r == null && es(t.url))) {
    const w = s && a && ts.read(a);
    w && i.set(s, w);
  }
  return t;
}
const hs = typeof XMLHttpRequest < "u", ms = hs && function(e) {
  return new Promise(function(n, o) {
    const r = fn(e);
    let s = r.data;
    const a = J.from(r.headers).normalize();
    let { responseType: i, onUploadProgress: u, onDownloadProgress: p } = r, c, m, b, w, R;
    function T() {
      w && w(), R && R(), r.cancelToken && r.cancelToken.unsubscribe(c), r.signal && r.signal.removeEventListener("abort", c);
    }
    let d = new XMLHttpRequest();
    d.open(r.method.toUpperCase(), r.url, !0), d.timeout = r.timeout;
    function f() {
      if (!d)
        return;
      const O = J.from(
        "getAllResponseHeaders" in d && d.getAllResponseHeaders()
      ), $ = {
        data: !i || i === "text" || i === "json" ? d.responseText : d.response,
        status: d.status,
        statusText: d.statusText,
        headers: O,
        config: e,
        request: d
      };
      un(
        function(ee) {
          n(ee), T();
        },
        function(ee) {
          o(ee), T();
        },
        $
      ), d = null;
    }
    "onloadend" in d ? d.onloadend = f : d.onreadystatechange = function() {
      !d || d.readyState !== 4 || d.status === 0 && !(d.responseURL && d.responseURL.startsWith("file:")) || setTimeout(f);
    }, d.onabort = function() {
      d && (o(new y("Request aborted", y.ECONNABORTED, e, d)), T(), d = null);
    }, d.onerror = function(k) {
      const $ = k && k.message ? k.message : "Network Error", L = new y($, y.ERR_NETWORK, e, d);
      L.event = k || null, o(L), T(), d = null;
    }, d.ontimeout = function() {
      let k = r.timeout ? "timeout of " + r.timeout + "ms exceeded" : "timeout exceeded";
      const $ = r.transitional || wt;
      r.timeoutErrorMessage && (k = r.timeoutErrorMessage), o(
        new y(
          k,
          $.clarifyTimeoutError ? y.ETIMEDOUT : y.ECONNABORTED,
          e,
          d
        )
      ), T(), d = null;
    }, s === void 0 && a.setContentType(null), "setRequestHeader" in d && l.forEach(en(a), function(k, $) {
      d.setRequestHeader($, k);
    }), l.isUndefined(r.withCredentials) || (d.withCredentials = !!r.withCredentials), i && i !== "json" && (d.responseType = r.responseType), p && ([b, R] = Ze(p, !0), d.addEventListener("progress", b)), u && d.upload && ([m, w] = Ze(u), d.upload.addEventListener("progress", m), d.upload.addEventListener("loadend", w)), (r.cancelToken || r.signal) && (c = (O) => {
      d && (o(!O || O.type ? new je(null, e, d) : O), d.abort(), T(), d = null);
    }, r.cancelToken && r.cancelToken.subscribe(c), r.signal && (r.signal.aborted ? c() : r.signal.addEventListener("abort", c)));
    const _ = Zr(r.url);
    if (_ && !Y.protocols.includes(_)) {
      o(
        new y(
          "Unsupported protocol " + _ + ":",
          y.ERR_BAD_REQUEST,
          e
        )
      ), T();
      return;
    }
    d.send(s || null);
  });
}, ys = (e, t) => {
  if (e = e ? e.filter(Boolean) : [], !t && !e.length)
    return;
  const n = new AbortController();
  let o = !1;
  const r = function(u) {
    if (!o) {
      o = !0, a();
      const p = u instanceof Error ? u : this.reason;
      n.abort(
        p instanceof y ? p : new je(p instanceof Error ? p.message : p)
      );
    }
  };
  let s = t && setTimeout(() => {
    s = null, r(new y(`timeout of ${t}ms exceeded`, y.ETIMEDOUT));
  }, t);
  const a = () => {
    e && (s && clearTimeout(s), s = null, e.forEach((u) => {
      u.unsubscribe ? u.unsubscribe(r) : u.removeEventListener("abort", r);
    }), e = null);
  };
  e.forEach((u) => {
    if (!o) {
      if (u.aborted) {
        r.call(u);
        return;
      }
      u.addEventListener("abort", r, { once: !0 });
    }
  });
  const { signal: i } = n;
  return i.unsubscribe = () => l.asap(a), i;
}, bs = function* (e, t) {
  let n = e.byteLength;
  if (n < t) {
    yield e;
    return;
  }
  let o = 0, r;
  for (; o < n; )
    r = o + t, yield e.slice(o, r), o = r;
}, gs = async function* (e, t) {
  for await (const n of ws(e))
    yield* bs(n, t);
}, ws = async function* (e) {
  if (e[Symbol.asyncIterator]) {
    yield* e;
    return;
  }
  const t = e.getReader();
  try {
    for (; ; ) {
      const { done: n, value: o } = await t.read();
      if (n)
        break;
      yield o;
    }
  } finally {
    await t.cancel();
  }
}, Ft = (e, t, n, o) => {
  const r = gs(e, t);
  let s = 0, a, i = (u) => {
    a || (a = !0, o && o(u));
  };
  return new ReadableStream(
    {
      async pull(u) {
        try {
          const { done: p, value: c } = await r.next();
          if (p) {
            i(), u.close();
            return;
          }
          let m = c.byteLength;
          if (n) {
            let b = s += m;
            n(b);
          }
          u.enqueue(new Uint8Array(c));
        } catch (p) {
          throw i(p), p;
        }
      },
      cancel(u) {
        return i(u), r.return();
      }
    },
    {
      highWaterMark: 2
    }
  );
}, Bt = (e) => e >= 48 && e <= 57 || e >= 65 && e <= 70 || e >= 97 && e <= 102, pn = (e, t, n) => t + 2 < n && Bt(e.charCodeAt(t + 1)) && Bt(e.charCodeAt(t + 2)), $t = (e) => e <= 57 ? e - 48 : (e & 223) - 55, _s = (e) => e >= 65 && e <= 90 || // A-Z
e >= 97 && e <= 122 || // a-z
e >= 48 && e <= 57 || // 0-9
e === 43 || // +
e === 47 || // /
e === 45 || // - (base64url)
e === 95, Ss = (e) => e === 9 || e === 10 || e === 12 || e === 13 || e === 32, Es = (e) => {
  const t = Math.floor(e / 4), n = e % 4;
  return t * 3 + (n === 2 ? 1 : n === 3 ? 2 : 0);
}, Rs = (e) => {
  const t = e.length;
  let n = 0;
  return t > 0 && e.charCodeAt(t - 1) === 61 && (n++, t > 1 && e.charCodeAt(t - 2) === 61 && n++), Math.floor((t - n) * 3 / 4);
}, Os = (e) => {
  const t = e.length;
  let n = 0, o = 0, r = !1;
  for (let s = 0; s < t; s++) {
    let a = e.charCodeAt(s);
    if (a === 37 && pn(e, s, t) && (a = $t(e.charCodeAt(s + 1)) * 16 + $t(e.charCodeAt(s + 2)), s += 2), !Ss(a)) {
      if (a === 61) {
        o++;
        continue;
      }
      if (!_s(a) || o > 0) {
        r = !0;
        continue;
      }
      n++;
    }
  }
  return r || o > 2 || o > 0 && (n + o) % 4 !== 0 || n % 4 === 1 ? Rs(e) : Es(n);
}, vs = (e, t) => {
  if (!e || typeof e != "string" || !e.startsWith("data:")) return 0;
  const n = e.indexOf(",");
  if (n < 0) return 0;
  const o = e.slice(5, n), r = e.slice(n + 1);
  if (/;base64/i.test(o))
    return t(r);
  let a = 0;
  for (let i = 0, u = r.length; i < u; i++) {
    const p = r.charCodeAt(i);
    if (p === 37 && pn(r, i, u))
      a += 1, i += 2;
    else if (p < 128)
      a += 1;
    else if (p < 2048)
      a += 2;
    else if (p >= 55296 && p <= 56319 && i + 1 < u) {
      const c = r.charCodeAt(i + 1);
      c >= 56320 && c <= 57343 ? (a += 4, i++) : a += 3;
    } else
      a += 3;
  }
  return a;
};
function As(e) {
  const t = typeof e == "string" ? e.indexOf("#") : -1;
  return vs(
    t === -1 ? e : e.slice(0, t),
    Os
  );
}
const St = "1.19.0", Vt = 64 * 1024, { isFunction: Me } = l, Ts = (e) => encodeURIComponent(e).replace(
  /%([0-9A-F]{2})/gi,
  (t, n) => String.fromCharCode(parseInt(n, 16))
), It = (e) => {
  if (!l.isString(e))
    return e;
  try {
    return decodeURIComponent(e);
  } catch {
    return e;
  }
}, jt = (e, ...t) => {
  try {
    return !!e(...t);
  } catch {
    return !1;
  }
}, xs = (e) => {
  const t = e.indexOf("://");
  let n = e;
  return t !== -1 && (n = n.slice(t + 3)), n.includes("@") || n.includes(":");
}, Cs = (e) => {
  const t = l.global !== void 0 && l.global !== null ? l.global : globalThis, { ReadableStream: n, TextEncoder: o } = t;
  e = l.merge.call(
    {
      skipUndefined: !0
    },
    {
      Request: t.Request,
      Response: t.Response
    },
    e
  );
  const { fetch: r, Request: s, Response: a } = e, i = r ? Me(r) : typeof fetch == "function", u = Me(s), p = Me(a);
  if (!i)
    return !1;
  const c = i && Me(n), m = i && (typeof o == "function" ? /* @__PURE__ */ ((f) => (_) => f.encode(_))(new o()) : async (f) => new Uint8Array(await new s(f).arrayBuffer())), b = u && c && jt(() => {
    let f = !1;
    const _ = new s(Y.origin, {
      body: new n(),
      method: "POST",
      get duplex() {
        return f = !0, "half";
      }
    }), O = _.headers.has("Content-Type");
    return _.body != null && _.body.cancel(), f && !O;
  }), w = p && c && jt(() => l.isReadableStream(new a("").body)), R = {
    stream: w && ((f) => f.body)
  };
  i && ["text", "arrayBuffer", "blob", "formData", "stream"].forEach((f) => {
    !R[f] && (R[f] = (_, O) => {
      let k = _ && _[f];
      if (k)
        return k.call(_);
      throw new y(
        `Response type '${f}' is not supported`,
        y.ERR_NOT_SUPPORT,
        O
      );
    });
  });
  const T = async (f) => {
    if (f == null)
      return 0;
    if (l.isBlob(f))
      return f.size;
    if (l.isSpecCompliantForm(f))
      return (await new s(Y.origin, {
        method: "POST",
        body: f
      }).arrayBuffer()).byteLength;
    if (l.isArrayBufferView(f) || l.isArrayBuffer(f))
      return f.byteLength;
    if (l.isURLSearchParams(f) && (f = f + ""), l.isString(f))
      return (await m(f)).byteLength;
  }, d = async (f, _) => {
    const O = l.toFiniteNumber(f.getContentLength());
    return O ?? T(_);
  };
  return async (f) => {
    let {
      url: _,
      method: O,
      data: k,
      signal: $,
      cancelToken: L,
      timeout: ee,
      onDownloadProgress: X,
      onUploadProgress: we,
      responseType: ne,
      headers: Z,
      withCredentials: pe = "same-origin",
      fetchOptions: xe,
      maxContentLength: G,
      maxBodyLength: he
    } = fn(f);
    const re = l.isNumber(G) && G > -1, S = l.isNumber(he) && he > -1, z = (N) => l.hasOwnProp(f, N) ? f[N] : void 0;
    let h = r || fetch;
    ne = ne ? (ne + "").toLowerCase() : "text";
    let v = ys(
      [$, L && L.toAbortSignal()],
      ee
    ), I = null;
    const ie = v && v.unsubscribe && (() => {
      v.unsubscribe();
    });
    let me, _e = null;
    const qe = () => new y(
      "Request body larger than maxBodyLength limit",
      y.ERR_BAD_REQUEST,
      f,
      I
    );
    try {
      let N;
      const E = z("auth");
      if (E) {
        const A = l.getSafeProp(E, "username") || "", te = l.getSafeProp(E, "password") || "";
        N = {
          username: A,
          password: te
        };
      }
      if (xs(_)) {
        const A = new URL(_, Y.origin);
        if (!N && (A.username || A.password)) {
          const te = It(A.username), ye = It(A.password);
          N = {
            username: te,
            password: ye
          };
        }
        (A.username || A.password) && (A.username = "", A.password = "", _ = A.href);
      }
      if (N && (Z.delete("authorization"), Z.set(
        "Authorization",
        "Basic " + btoa(Ts((N.username || "") + ":" + (N.password || "")))
      )), re && typeof _ == "string" && _.startsWith("data:") && As(_) > G)
        throw new y(
          "maxContentLength size of " + G + " exceeded",
          y.ERR_BAD_RESPONSE,
          f,
          I
        );
      if (S && O !== "get" && O !== "head") {
        const A = await T(k);
        if (typeof A == "number" && isFinite(A) && (me = A, A > he))
          throw qe();
      }
      const q = S && (l.isReadableStream(k) || l.isStream(k)), ce = (A, te, ye) => Ft(
        A,
        Vt,
        (Se) => {
          if (S && Se > he)
            throw _e = qe();
          te && te(Se);
        },
        ye
      );
      if (b && O !== "get" && O !== "head" && (we || q)) {
        if (me = me ?? await d(Z, k), me !== 0 || q) {
          let A = new s(_, {
            method: "POST",
            body: k,
            duplex: "half"
          }), te;
          if (l.isFormData(k) && (te = A.headers.get("content-type")) && Z.setContentType(te), A.body) {
            const [ye, Se] = we && Ut(
              me,
              Ze(Nt(we))
            ) || [];
            k = ce(A.body, ye, Se);
          }
        }
      } else if (q && !u && c && O !== "get" && O !== "head")
        k = ce(k);
      else if (q && u && !b && O !== "get" && O !== "head")
        throw new y(
          "Stream request bodies are not supported by the current fetch implementation",
          y.ERR_NOT_SUPPORT,
          f,
          I
        );
      l.isString(pe) || (pe = pe ? "include" : "omit");
      const wn = u && "credentials" in s.prototype;
      if (l.isFormData(k)) {
        const A = Z.getContentType();
        A && /^multipart\/form-data/i.test(A) && !/boundary=/i.test(A) && Z.delete("content-type");
      }
      Z.set("User-Agent", "axios/" + St, !1);
      const Rt = {
        ...xe,
        signal: v,
        method: O.toUpperCase(),
        headers: en(Z.normalize()),
        body: k,
        duplex: "half",
        credentials: wn ? pe : void 0
      };
      I = u && new s(_, Rt);
      let ue = await (u ? h(I, xe) : h(_, Rt));
      const Ot = J.from(ue.headers);
      if (re) {
        const A = l.toFiniteNumber(Ot.getContentLength());
        if (A != null && A > G)
          throw new y(
            "maxContentLength size of " + G + " exceeded",
            y.ERR_BAD_RESPONSE,
            f,
            I
          );
      }
      const nt = w && (ne === "stream" || ne === "response");
      if (w && ue.body && (X || re || nt && ie)) {
        const A = {};
        ["status", "statusText", "headers"].forEach((Ne) => {
          A[Ne] = ue[Ne];
        });
        const te = l.toFiniteNumber(Ot.getContentLength()), [ye, Se] = X && Ut(
          te,
          Ze(Nt(X), !0)
        ) || [];
        let vt = 0;
        const _n = (Ne) => {
          if (re && (vt = Ne, vt > G))
            throw new y(
              "maxContentLength size of " + G + " exceeded",
              y.ERR_BAD_RESPONSE,
              f,
              I
            );
          ye && ye(Ne);
        };
        ue = new a(
          Ft(ue.body, Vt, _n, () => {
            Se && Se(), ie && ie();
          }),
          A
        );
      }
      ne = ne || "text";
      let de = await R[l.findKey(R, ne) || "text"](
        ue,
        f
      );
      if (re && !w && !nt) {
        let A;
        if (de != null && (typeof de.byteLength == "number" ? A = de.byteLength : typeof de.size == "number" ? A = de.size : typeof de == "string" && (A = typeof o == "function" ? new o().encode(de).byteLength : de.length)), typeof A == "number" && A > G)
          throw new y(
            "maxContentLength size of " + G + " exceeded",
            y.ERR_BAD_RESPONSE,
            f,
            I
          );
      }
      return !nt && ie && ie(), await new Promise((A, te) => {
        un(A, te, {
          data: de,
          headers: J.from(ue.headers),
          status: ue.status,
          statusText: ue.statusText,
          config: f,
          request: I
        });
      });
    } catch (N) {
      if (ie && ie(), v && v.aborted && v.reason instanceof y) {
        const E = v.reason;
        throw E.config = f, I && (E.request = I), N !== E && Object.defineProperty(E, "cause", {
          __proto__: null,
          value: N,
          writable: !0,
          enumerable: !1,
          configurable: !0
        }), E;
      }
      if (_e)
        throw I && !_e.request && (_e.request = I), _e;
      if (N instanceof y)
        throw I && !N.request && (N.request = I), N;
      if (N && N.name === "TypeError" && /Load failed|fetch/i.test(N.message)) {
        const E = new y(
          "Network Error",
          y.ERR_NETWORK,
          f,
          I,
          N && N.response
        );
        throw Object.defineProperty(E, "cause", {
          __proto__: null,
          value: N.cause || N,
          writable: !0,
          enumerable: !1,
          configurable: !0
        }), E;
      }
      throw y.from(N, N && N.code, f, I, N && N.response);
    }
  };
}, Ps = /* @__PURE__ */ new Map(), hn = (e) => {
  let t = e && e.env || {};
  const { fetch: n, Request: o, Response: r } = t, s = [o, r, n];
  let a = s.length, i = a, u, p, c = Ps;
  for (; i--; )
    u = s[i], p = c.get(u), p === void 0 && c.set(u, p = i ? /* @__PURE__ */ new Map() : Cs(t)), c = p;
  return p;
};
hn();
const Et = {
  http: Dr,
  xhr: ms,
  fetch: {
    get: hn
  }
};
l.forEach(Et, (e, t) => {
  if (e) {
    try {
      Object.defineProperty(e, "name", { __proto__: null, value: t });
    } catch {
    }
    Object.defineProperty(e, "adapterName", { __proto__: null, value: t });
  }
});
const qt = (e) => `- ${e}`, ks = (e) => l.isFunction(e) || e === null || e === !1;
function zs(e, t) {
  e = l.isArray(e) ? e : [e];
  const { length: n } = e;
  let o, r;
  const s = {};
  for (let a = 0; a < n; a++) {
    o = e[a];
    let i;
    if (r = o, !ks(o) && (r = Et[(i = String(o)).toLowerCase()], r === void 0))
      throw new y(`Unknown adapter '${i}'`);
    if (r && (l.isFunction(r) || (r = r.get(t))))
      break;
    s[i || "#" + a] = r;
  }
  if (!r) {
    const a = Object.entries(s).map(
      ([u, p]) => `adapter ${u} ` + (p === !1 ? "is not supported by the environment" : "is not available in the build")
    );
    let i = n ? a.length > 1 ? `since :
` + a.map(qt).join(`
`) : " " + qt(a[0]) : "as no adapter specified";
    throw new y(
      "There is no suitable adapter to dispatch the request " + i,
      y.ERR_NOT_SUPPORT
    );
  }
  return r;
}
const mn = {
  /**
   * Resolve an adapter from a list of adapter names or functions.
   * @type {Function}
   */
  getAdapter: zs,
  /**
   * Exposes all known adapters
   * @type {Object<string, Function|Object>}
   */
  adapters: Et
};
function ct(e) {
  if (e.cancelToken && e.cancelToken.throwIfRequested(), e.signal && e.signal.aborted)
    throw new je(null, e);
}
function ut(e) {
  return ct(e), e.headers = J.from(e.headers), e.data = lt.call(e, e.transformRequest), ["post", "put", "patch"].indexOf(e.method) !== -1 && e.headers.setContentType("application/x-www-form-urlencoded", !1), mn.getAdapter(e.adapter || Ie.adapter, e)(e).then(
    function(o) {
      ct(e), e.response = o;
      try {
        o.data = lt.call(e, e.transformResponse, o);
      } finally {
        delete e.response;
      }
      return o.headers = J.from(o.headers), o;
    },
    function(o) {
      if (!cn(o) && (ct(e), o && o.response)) {
        e.response = o.response;
        try {
          o.response.data = lt.call(
            e,
            e.transformResponse,
            o.response
          );
        } finally {
          delete e.response;
        }
        o.response.headers = J.from(o.response.headers);
      }
      return Promise.reject(o);
    }
  );
}
const tt = {};
["object", "boolean", "number", "function", "string", "symbol"].forEach((e, t) => {
  tt[e] = function(o) {
    return typeof o === e || "a" + (t < 1 ? "n " : " ") + e;
  };
});
const Mt = {};
tt.transitional = function(t, n, o) {
  function r(s, a) {
    return "[Axios v" + St + "] Transitional option '" + s + "'" + a + (o ? ". " + o : "");
  }
  return (s, a, i) => {
    if (t === !1)
      throw new y(
        r(a, " has been removed" + (n ? " in " + n : "")),
        y.ERR_DEPRECATED
      );
    return n && !Mt[a] && (Mt[a] = !0, console.warn(
      r(
        a,
        " has been deprecated since v" + n + " and will be removed in the near future"
      )
    )), t ? t(s, a, i) : !0;
  };
};
tt.spelling = function(t) {
  return (n, o) => (console.warn(`${o} is likely a misspelling of ${t}`), !0);
};
function Us(e, t, n) {
  if (typeof e != "object" || e === null)
    throw new y("options must be an object", y.ERR_BAD_OPTION_VALUE);
  const o = Object.keys(e);
  let r = o.length;
  for (; r-- > 0; ) {
    const s = o[r], a = Object.prototype.hasOwnProperty.call(t, s) ? t[s] : void 0;
    if (a) {
      const i = e[s], u = i === void 0 || a(i, s, e);
      if (u !== !0)
        throw new y(
          "option " + s + " must be " + u,
          y.ERR_BAD_OPTION_VALUE
        );
      continue;
    }
    if (n !== !0)
      throw new y("Unknown option " + s, y.ERR_BAD_OPTION);
  }
}
const Ke = {
  assertOptions: Us,
  validators: tt
}, K = Ke.validators;
let Re = class {
  constructor(t) {
    this.defaults = t || {}, this.interceptors = {
      request: new kt(),
      response: new kt()
    };
  }
  /**
   * Dispatch a request
   *
   * @param {String|Object} configOrUrl The config specific for this request (merged with this.defaults)
   * @param {?Object} config
   *
   * @returns {Promise} The Promise to be fulfilled
   */
  async request(t, n) {
    try {
      return await this._request(t, n);
    } catch (o) {
      if (o instanceof Error) {
        let r = {};
        Error.captureStackTrace ? Error.captureStackTrace(r) : r = new Error();
        const s = (() => {
          if (!r.stack)
            return "";
          const a = r.stack.indexOf(`
`);
          return a === -1 ? "" : r.stack.slice(a + 1);
        })();
        try {
          if (!o.stack)
            o.stack = s;
          else if (s) {
            const a = s.indexOf(`
`), i = a === -1 ? -1 : s.indexOf(`
`, a + 1), u = i === -1 ? "" : s.slice(i + 1);
            String(o.stack).endsWith(u) || (o.stack += `
` + s);
          }
        } catch {
        }
      }
      throw o;
    }
  }
  _request(t, n) {
    typeof t == "string" ? (n = n || {}, n.url = t) : n = t || {}, n = Ae(this.defaults, n);
    const { transitional: o, paramsSerializer: r, headers: s } = n;
    o !== void 0 && Ke.assertOptions(
      o,
      {
        silentJSONParsing: K.transitional(K.boolean),
        forcedJSONParsing: K.transitional(K.boolean),
        clarifyTimeoutError: K.transitional(K.boolean),
        legacyInterceptorReqResOrdering: K.transitional(K.boolean),
        advertiseZstdAcceptEncoding: K.transitional(K.boolean),
        validateStatusUndefinedResolves: K.transitional(K.boolean)
      },
      !1
    ), r != null && (l.isFunction(r) ? n.paramsSerializer = {
      serialize: r
    } : Ke.assertOptions(
      r,
      {
        encode: K.function,
        serialize: K.function
      },
      !0
    )), n.allowAbsoluteUrls !== void 0 || (this.defaults.allowAbsoluteUrls !== void 0 ? n.allowAbsoluteUrls = this.defaults.allowAbsoluteUrls : n.allowAbsoluteUrls = !0), Ke.assertOptions(
      n,
      {
        baseUrl: K.spelling("baseURL"),
        withXsrfToken: K.spelling("withXSRFToken")
      },
      !0
    ), n.method = (n.method || this.defaults.method || "get").toLowerCase();
    let a = s && l.merge(s.common, s[n.method]);
    s && l.forEach(["delete", "get", "head", "post", "put", "patch", "query", "common"], (R) => {
      delete s[R];
    }), n.headers = J.concat(a, s);
    const i = [];
    let u = !0;
    this.interceptors.request.forEach(function(T) {
      if (typeof T.runWhen == "function" && T.runWhen(n) === !1)
        return;
      u = u && T.synchronous;
      const d = n.transitional || wt;
      d && d.legacyInterceptorReqResOrdering ? i.unshift(T.fulfilled, T.rejected) : i.push(T.fulfilled, T.rejected);
    });
    const p = [];
    this.interceptors.response.forEach(function(T) {
      p.push(T.fulfilled, T.rejected);
    });
    let c, m = 0, b;
    if (!u) {
      const R = [ut.bind(this), void 0];
      for (R.unshift(...i), R.push(...p), b = R.length, c = Promise.resolve(n); m < b; )
        c = c.then(R[m++], R[m++]);
      return c;
    }
    b = i.length;
    let w = n;
    for (; m < b; ) {
      const R = i[m++], T = i[m++];
      try {
        w = R ? R(w) : w;
      } catch (d) {
        if (!T) {
          c = Promise.reject(d);
          break;
        }
        try {
          const f = T.call(this, d);
          l.isThenable(f) && (c = Promise.resolve(f).then(
            () => ut.call(this, w)
          ));
        } catch (f) {
          c = Promise.reject(f);
        }
        break;
      }
    }
    if (!c)
      try {
        c = ut.call(this, w);
      } catch (R) {
        c = Promise.reject(R);
      }
    for (m = 0, b = p.length; m < b; )
      c = c.then(p[m++], p[m++]);
    return c;
  }
  getUri(t) {
    t = Ae(this.defaults, t);
    const n = dn(t.baseURL, t.url, t.allowAbsoluteUrls, t);
    return sn(n, t.params, t.paramsSerializer);
  }
};
l.forEach(["delete", "get", "head", "options"], function(t) {
  Re.prototype[t] = function(n, o) {
    return this.request(
      Ae(o || {}, {
        method: t,
        url: n,
        data: o && l.hasOwnProp(o, "data") ? o.data : void 0
      })
    );
  };
});
l.forEach(["post", "put", "patch", "query"], function(t) {
  function n(o) {
    return function(s, a, i) {
      return this.request(
        Ae(i || {}, {
          method: t,
          headers: o ? {
            "Content-Type": "multipart/form-data"
          } : {},
          url: s,
          data: a
        })
      );
    };
  }
  Re.prototype[t] = n(), t !== "query" && (Re.prototype[t + "Form"] = n(!0));
});
let Ns = class yn {
  constructor(t) {
    if (typeof t != "function")
      throw new TypeError("executor must be a function.");
    let n;
    this.promise = new Promise(function(s) {
      n = s;
    });
    const o = this;
    this.promise.then((r) => {
      if (!o._listeners) return;
      let s = o._listeners.length;
      for (; s-- > 0; )
        o._listeners[s](r);
      o._listeners = null;
    }), this.promise.then = (r) => {
      let s;
      const a = new Promise((i) => {
        o.subscribe(i), s = i;
      }).then(r);
      return a.cancel = function() {
        o.unsubscribe(s);
      }, a;
    }, t(function(s, a, i) {
      o.reason || (o.reason = new je(s, a, i), n(o.reason));
    });
  }
  /**
   * Throws a `CanceledError` if cancellation has been requested.
   */
  throwIfRequested() {
    if (this.reason)
      throw this.reason;
  }
  /**
   * Subscribe to the cancel signal
   */
  subscribe(t) {
    if (this.reason) {
      t(this.reason);
      return;
    }
    this._listeners ? this._listeners.push(t) : this._listeners = [t];
  }
  /**
   * Unsubscribe from the cancel signal
   */
  unsubscribe(t) {
    if (!this._listeners)
      return;
    const n = this._listeners.indexOf(t);
    n !== -1 && this._listeners.splice(n, 1);
  }
  toAbortSignal() {
    const t = new AbortController(), n = (o) => {
      t.abort(o);
    };
    return this.subscribe(n), t.signal.unsubscribe = () => this.unsubscribe(n), t.signal;
  }
  /**
   * Returns an object that contains a new `CancelToken` and a function that, when called,
   * cancels the `CancelToken`.
   */
  static source() {
    let t;
    return {
      token: new yn(function(r) {
        t = r;
      }),
      cancel: t
    };
  }
};
function Ds(e) {
  return function(n) {
    return e.apply(null, n);
  };
}
function Ls(e) {
  return l.isObject(e) && e.isAxiosError === !0;
}
const mt = {
  Continue: 100,
  SwitchingProtocols: 101,
  Processing: 102,
  EarlyHints: 103,
  Ok: 200,
  Created: 201,
  Accepted: 202,
  NonAuthoritativeInformation: 203,
  NoContent: 204,
  ResetContent: 205,
  PartialContent: 206,
  MultiStatus: 207,
  AlreadyReported: 208,
  ImUsed: 226,
  MultipleChoices: 300,
  MovedPermanently: 301,
  Found: 302,
  SeeOther: 303,
  NotModified: 304,
  UseProxy: 305,
  Unused: 306,
  TemporaryRedirect: 307,
  PermanentRedirect: 308,
  BadRequest: 400,
  Unauthorized: 401,
  PaymentRequired: 402,
  Forbidden: 403,
  NotFound: 404,
  MethodNotAllowed: 405,
  NotAcceptable: 406,
  ProxyAuthenticationRequired: 407,
  RequestTimeout: 408,
  Conflict: 409,
  Gone: 410,
  LengthRequired: 411,
  PreconditionFailed: 412,
  PayloadTooLarge: 413,
  UriTooLong: 414,
  UnsupportedMediaType: 415,
  RangeNotSatisfiable: 416,
  ExpectationFailed: 417,
  ImATeapot: 418,
  MisdirectedRequest: 421,
  UnprocessableEntity: 422,
  Locked: 423,
  FailedDependency: 424,
  TooEarly: 425,
  UpgradeRequired: 426,
  PreconditionRequired: 428,
  TooManyRequests: 429,
  RequestHeaderFieldsTooLarge: 431,
  UnavailableForLegalReasons: 451,
  InternalServerError: 500,
  NotImplemented: 501,
  BadGateway: 502,
  ServiceUnavailable: 503,
  GatewayTimeout: 504,
  HttpVersionNotSupported: 505,
  VariantAlsoNegotiates: 506,
  InsufficientStorage: 507,
  LoopDetected: 508,
  NotExtended: 510,
  NetworkAuthenticationRequired: 511,
  WebServerReturnsAnUnknownError: 520,
  WebServerIsDown: 521,
  ConnectionTimedOut: 522,
  OriginIsUnreachable: 523,
  TimeoutOccurred: 524,
  SslHandshakeFailed: 525,
  InvalidSslCertificate: 526
};
Object.entries(mt).forEach(([e, t]) => {
  mt[t] = e;
});
function bn(e) {
  const t = new Re(e), n = Ht(Re.prototype.request, t);
  return l.extend(n, Re.prototype, t, { allOwnKeys: !0 }), l.extend(n, t, null, { allOwnKeys: !0 }), n.create = function(r) {
    return bn(Ae(e, r));
  }, n;
}
const B = bn(Ie);
B.Axios = Re;
B.CanceledError = je;
B.CancelToken = Ns;
B.isCancel = cn;
B.VERSION = St;
B.toFormData = et;
B.AxiosError = y;
B.Cancel = B.CanceledError;
B.all = function(t) {
  return Promise.all(t);
};
B.spread = Ds;
B.isAxiosError = Ls;
B.mergeConfig = Ae;
B.AxiosHeaders = J;
B.formToJSON = (e) => ln(l.isHTMLForm(e) ? new FormData(e) : e);
B.getAdapter = mn.getAdapter;
B.HttpStatusCode = mt;
B.default = B;
const {
  Axios: na,
  AxiosError: oa,
  CanceledError: ra,
  isCancel: sa,
  CancelToken: aa,
  VERSION: ia,
  all: la,
  Cancel: ca,
  isAxiosError: ua,
  spread: da,
  toFormData: fa,
  AxiosHeaders: pa,
  HttpStatusCode: ha,
  formToJSON: ma,
  getAdapter: ya,
  mergeConfig: ba,
  create: ga
} = B, gn = B.create({
  baseURL: "/api",
  timeout: 3e4,
  headers: { "Content-Type": "application/json" }
});
gn.interceptors.request.use(
  (e) => {
    const t = localStorage.getItem("YZH_TOKEN");
    return t && (e.headers.Authorization = `Bearer ${t}`), e;
  },
  (e) => Promise.reject(e)
);
gn.interceptors.response.use(
  (e) => {
    var n, o;
    const t = e.data;
    if (typeof t.status == "boolean") {
      if (t.status === !0)
        return { code: 200, message: t.message || "success", data: t.data };
      const r = t.message || "请求失败";
      return (r.includes("token") || r.includes("登录") || r.includes("未授权")) && (localStorage.removeItem("YZH_TOKEN"), window.location.pathname !== "/login" && (window.location.href = "/login")), Promise.reject(new Error(r));
    }
    return typeof t.code == "number" ? t.code === 200 ? t : ((t.code === 401 || (n = t.message) != null && n.includes("token") || (o = t.message) != null && o.includes("登录")) && (localStorage.removeItem("YZH_TOKEN"), window.location.pathname !== "/login" && (window.location.href = "/login")), Promise.reject(new Error(t.message || "请求失败"))) : { code: 200, message: "success", data: t };
  },
  (e) => {
    var n, o, r;
    ((n = e.response) == null ? void 0 : n.status) === 401 && (localStorage.removeItem("YZH_TOKEN"), window.location.pathname !== "/login" && (window.location.href = "/login"));
    const t = ((r = (o = e.response) == null ? void 0 : o.data) == null ? void 0 : r.message) || e.message || "网络错误";
    return Promise.reject(new Error(t));
  }
);
export {
  Ao as YzhApiClient,
  Ys as YzhCard,
  qs as YzhDialog,
  Ms as YzhEmptyState,
  Is as YzhForm,
  js as YzhPageLayout,
  kn as YzhPagination,
  $n as YzhSearchBar,
  Hs as YzhStatusBadge,
  Vs as YzhTable,
  Mn as YzhToolbar,
  gn as http,
  Xs as isAuthenticated,
  Ws as login,
  Js as logout,
  ge as tokenStore,
  Gs as useAuth,
  Zs as useTable,
  Ks as yzhApi
};
