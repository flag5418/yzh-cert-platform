var yt = Object.defineProperty;
var gt = (a, o, e) => o in a ? yt(a, o, { enumerable: !0, configurable: !0, writable: !0, value: e }) : a[o] = e;
var F = (a, o, e) => gt(a, typeof o != "symbol" ? o + "" : o, e);
import { defineComponent as ne, ref as k, computed as G, reactive as be, watch as Se, resolveComponent as N, openBlock as p, createBlock as z, withCtx as w, createVNode as P, createElementBlock as D, Fragment as oe, renderList as ue, mergeProps as we, createTextVNode as V, toDisplayString as U, renderSlot as W, createCommentVNode as j, createElementVNode as R, unref as Ae, normalizeStyle as ze, withKeys as vt, normalizeClass as ke, createSlots as bt, resolveDynamicComponent as Ee, withModifiers as Ct, getCurrentInstance as wt, onMounted as Re, resolveDirective as kt, withDirectives as Tt, nextTick as _e } from "vue";
import { ElInput as We, ElTree as _t, ElMessageBox as xe, ElMessage as J } from "element-plus";
const xt = {
  key: 0,
  class: "yzh-form__actions"
}, St = /* @__PURE__ */ ne({
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
  setup(a, { expose: o, emit: e }) {
    const t = a, l = e, n = k(), i = G(() => 24 / t.cols), r = G(() => {
      if (t.rules) return t.rules;
      const f = {};
      return t.fields.forEach((x) => {
        if (x.hidden) return;
        const q = [];
        x.required && q.push({
          required: !0,
          message: `请${x.type === "select" || x.type === "radio" || x.type === "switch" ? "选择" : "输入"}${x.label}`,
          trigger: x.trigger || (x.type === "select" || x.type === "switch" ? "change" : "blur")
        }), x.validator && q.push({ validator: x.validator, trigger: x.trigger || "blur" }), q.length && (f[x.prop] = q);
      }), f;
    }), c = be({});
    async function h(f) {
      if (f.options) return f.options;
      if (!f.loadOptions) return [];
      if (c[f.prop]) return c[f.prop];
      const x = await f.loadOptions();
      return c[f.prop] = x, x;
    }
    (async () => {
      for (const f of t.fields)
        if (f.loadOptions && !f.options)
          try {
            await h(f);
          } catch {
          }
    })();
    const d = be({});
    function v() {
      Object.keys(d).forEach((f) => delete d[f]), Object.assign(d, t.modelValue || {}), t.fields.forEach((f) => {
        d[f.prop] === void 0 && f.defaultValue !== void 0 && (d[f.prop] = f.defaultValue);
      });
    }
    v(), Se(
      () => t.modelValue,
      () => v(),
      { deep: !0 }
    ), Se(
      d,
      (f) => {
        l("update:modelValue", { ...f });
      },
      { deep: !0 }
    );
    async function C() {
      if (n.value)
        try {
          await n.value.validate(), l("submit", { ...d }), l("validate", !0);
        } catch (f) {
          l("validate", !1, f);
        }
    }
    function _() {
      var f;
      v(), (f = n.value) == null || f.clearValidate(), l("reset");
    }
    async function g() {
      var f;
      return (f = n.value) == null ? void 0 : f.validate();
    }
    async function E() {
      var f;
      (f = n.value) == null || f.resetFields();
    }
    return o({ validate: g, resetFields: E, formRef: n }), (f, x) => {
      const q = N("el-input"), ee = N("el-input-number"), ae = N("el-option"), se = N("el-select"), le = N("el-radio"), S = N("el-radio-group"), L = N("el-checkbox"), K = N("el-checkbox-group"), Y = N("el-switch"), X = N("el-date-picker"), te = N("el-tree-select"), Z = N("el-cascader"), me = N("el-form-item"), fe = N("el-col"), pe = N("el-row"), Ce = N("el-button"), re = N("el-form");
      return p(), z(re, {
        ref_key: "formRef",
        ref: n,
        model: d,
        rules: r.value,
        "label-width": a.labelWidth,
        "label-position": a.labelPosition,
        size: a.size,
        class: "yzh-form"
      }, {
        default: w(() => [
          P(pe, { gutter: 20 }, {
            default: w(() => [
              (p(!0), D(oe, null, ue(a.fields, (s) => (p(), D(oe, {
                key: s.prop
              }, [
                s.hidden ? j("", !0) : (p(), z(fe, {
                  key: 0,
                  span: s.span || i.value
                }, {
                  default: w(() => [
                    P(me, {
                      label: s.label,
                      prop: s.prop
                    }, {
                      default: w(() => [
                        !s.type || s.type === "text" || s.type === "textarea" || s.type === "password" ? (p(), z(q, we({
                          key: 0,
                          modelValue: d[s.prop],
                          "onUpdate:modelValue": (u) => d[s.prop] = u,
                          type: s.type === "textarea" ? "textarea" : s.type === "password" ? "password" : "text",
                          placeholder: s.placeholder || `请输入${s.label}`,
                          disabled: s.disabled,
                          rows: s.type === "textarea" ? 3 : void 0,
                          autocomplete: s.type === "password" ? "new-password" : "off"
                        }, { ref_for: !0 }, s.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "type", "placeholder", "disabled", "rows", "autocomplete"])) : s.type === "number" ? (p(), z(ee, we({
                          key: 1,
                          modelValue: d[s.prop],
                          "onUpdate:modelValue": (u) => d[s.prop] = u,
                          placeholder: s.placeholder,
                          disabled: s.disabled,
                          style: { width: "100%" }
                        }, { ref_for: !0 }, s.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : s.type === "select" ? (p(), z(se, we({
                          key: 2,
                          modelValue: d[s.prop],
                          "onUpdate:modelValue": (u) => d[s.prop] = u,
                          placeholder: s.placeholder || `请选择${s.label}`,
                          multiple: s.multiple,
                          filterable: s.filterable,
                          disabled: s.disabled,
                          style: { width: "100%" }
                        }, { ref_for: !0 }, s.fieldProps), {
                          default: w(() => [
                            (p(!0), D(oe, null, ue(s.options || c[s.prop] || [], (u) => (p(), z(ae, {
                              key: u.value,
                              label: u.label,
                              value: u.value,
                              disabled: u.disabled
                            }, null, 8, ["label", "value", "disabled"]))), 128))
                          ]),
                          _: 2
                        }, 1040, ["modelValue", "onUpdate:modelValue", "placeholder", "multiple", "filterable", "disabled"])) : s.type === "radio" ? (p(), z(S, {
                          key: 3,
                          modelValue: d[s.prop],
                          "onUpdate:modelValue": (u) => d[s.prop] = u,
                          disabled: s.disabled
                        }, {
                          default: w(() => [
                            (p(!0), D(oe, null, ue(s.options || [], (u) => (p(), z(le, {
                              key: u.value,
                              value: u.value
                            }, {
                              default: w(() => [
                                V(U(u.label), 1)
                              ]),
                              _: 2
                            }, 1032, ["value"]))), 128))
                          ]),
                          _: 2
                        }, 1032, ["modelValue", "onUpdate:modelValue", "disabled"])) : s.type === "checkbox" ? (p(), z(K, {
                          key: 4,
                          modelValue: d[s.prop],
                          "onUpdate:modelValue": (u) => d[s.prop] = u,
                          disabled: s.disabled
                        }, {
                          default: w(() => [
                            (p(!0), D(oe, null, ue(s.options || [], (u) => (p(), z(L, {
                              key: u.value,
                              value: u.value
                            }, {
                              default: w(() => [
                                V(U(u.label), 1)
                              ]),
                              _: 2
                            }, 1032, ["value"]))), 128))
                          ]),
                          _: 2
                        }, 1032, ["modelValue", "onUpdate:modelValue", "disabled"])) : s.type === "switch" ? (p(), z(Y, we({
                          key: 5,
                          modelValue: d[s.prop],
                          "onUpdate:modelValue": (u) => d[s.prop] = u,
                          disabled: s.disabled,
                          "active-value": 1,
                          "inactive-value": 0
                        }, { ref_for: !0 }, s.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "disabled"])) : s.type === "date" ? (p(), z(X, we({
                          key: 6,
                          modelValue: d[s.prop],
                          "onUpdate:modelValue": (u) => d[s.prop] = u,
                          type: "date",
                          placeholder: s.placeholder || `请选择${s.label}`,
                          disabled: s.disabled,
                          "value-format": "YYYY-MM-DD",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, s.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : s.type === "datetime" ? (p(), z(X, we({
                          key: 7,
                          modelValue: d[s.prop],
                          "onUpdate:modelValue": (u) => d[s.prop] = u,
                          type: "datetime",
                          placeholder: s.placeholder || `请选择${s.label}`,
                          disabled: s.disabled,
                          "value-format": "YYYY-MM-DD HH:mm:ss",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, s.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : s.type === "dateRange" ? (p(), z(X, we({
                          key: 8,
                          modelValue: d[s.prop],
                          "onUpdate:modelValue": (u) => d[s.prop] = u,
                          type: "daterange",
                          placeholder: s.placeholder || `请选择${s.label}`,
                          disabled: s.disabled,
                          "value-format": "YYYY-MM-DD",
                          "range-separator": "至",
                          "start-placeholder": "开始日期",
                          "end-placeholder": "结束日期",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, s.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : s.type === "treeSelect" ? (p(), z(te, we({
                          key: 9,
                          modelValue: d[s.prop],
                          "onUpdate:modelValue": (u) => d[s.prop] = u,
                          data: s.options || [],
                          placeholder: s.placeholder || `请选择${s.label}`,
                          disabled: s.disabled,
                          "check-strictly": "",
                          clearable: "",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, s.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "data", "placeholder", "disabled"])) : s.type === "cascader" ? (p(), z(Z, we({
                          key: 10,
                          modelValue: d[s.prop],
                          "onUpdate:modelValue": (u) => d[s.prop] = u,
                          options: s.options || [],
                          placeholder: s.placeholder || `请选择${s.label}`,
                          disabled: s.disabled,
                          style: { width: "100%" }
                        }, { ref_for: !0 }, s.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "options", "placeholder", "disabled"])) : s.type === "custom" && s.slot ? W(f.$slots, s.slot, {
                          value: d[s.prop],
                          field: s,
                          data: d
                        }, void 0, !0, 11) : W(f.$slots, `field-${s.prop}`, {
                          value: d[s.prop],
                          field: s,
                          data: d
                        }, void 0, !0, 12)
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
          a.showActions ? (p(), D("div", xt, [
            W(f.$slots, "actions", {
              submit: C,
              reset: _
            }, () => [
              P(Ce, { onClick: _ }, {
                default: w(() => [
                  V(U(a.resetText), 1)
                ]),
                _: 1
              }),
              P(Ce, {
                type: "primary",
                loading: a.loading,
                onClick: C
              }, {
                default: w(() => [
                  V(U(a.submitText), 1)
                ]),
                _: 1
              }, 8, ["loading"])
            ], !0)
          ])) : j("", !0)
        ]),
        _: 3
      }, 8, ["model", "rules", "label-width", "label-position", "size"]);
    };
  }
}), he = (a, o) => {
  const e = a.__vccOpts || a;
  for (const [t, l] of o)
    e[t] = l;
  return e;
}, Ft = /* @__PURE__ */ he(St, [["__scopeId", "data-v-0464ba24"]]), Ra = /* @__PURE__ */ ne({
  __name: "YzhFormDialog",
  props: {
    visible: { type: Boolean, default: !1 },
    mode: { default: "add" },
    entityName: { default: "" },
    title: { default: void 0 },
    width: { default: "640px" },
    fields: { default: () => [] },
    modelValue: { default: () => ({}) },
    loading: { type: Boolean, default: !1 },
    cols: { default: 2 },
    labelWidth: { default: "100px" },
    submitText: { default: void 0 },
    destroyOnClose: { type: Boolean, default: !0 }
  },
  emits: ["update:visible", "update:modelValue", "submit", "cancel", "closed"],
  setup(a, { emit: o }) {
    const e = a, t = o, l = G({
      get: () => e.visible,
      set: (d) => t("update:visible", d)
    }), n = G({
      get: () => e.modelValue,
      set: (d) => t("update:modelValue", d)
    }), i = G(() => {
      if (e.title) return e.title;
      const d = e.entityName || "";
      return e.mode === "add" ? d ? `新增${d}` : "新增" : e.mode === "detail" ? d ? `${d}详情` : "详情" : d ? `编辑${d}` : "编辑";
    }), r = G(() => e.submitText ?? (e.mode === "detail" ? "关闭" : "保存"));
    function c() {
      t("submit");
    }
    function h() {
      t("cancel"), t("update:visible", !1);
    }
    return (d, v) => {
      const C = N("el-button"), _ = N("el-dialog");
      return p(), z(_, {
        modelValue: l.value,
        "onUpdate:modelValue": v[1] || (v[1] = (g) => l.value = g),
        title: i.value,
        width: a.width,
        "close-on-click-modal": !1,
        "destroy-on-close": a.destroyOnClose,
        onClosed: v[2] || (v[2] = (g) => t("closed"))
      }, {
        footer: w(() => [
          W(d.$slots, "footer", {}, () => [
            P(C, { onClick: h }, {
              default: w(() => [...v[3] || (v[3] = [
                V("取消", -1)
              ])]),
              _: 1
            }),
            P(C, {
              type: "primary",
              loading: a.loading,
              onClick: c
            }, {
              default: w(() => [
                V(U(r.value), 1)
              ]),
              _: 1
            }, 8, ["loading"])
          ])
        ]),
        default: w(() => [
          W(d.$slots, "default", {}, () => [
            W(d.$slots, "prepend"),
            P(Ft, {
              modelValue: n.value,
              "onUpdate:modelValue": v[0] || (v[0] = (g) => n.value = g),
              fields: a.fields,
              loading: a.loading,
              cols: a.cols,
              "label-width": a.labelWidth,
              "show-actions": !1,
              onSubmit: c,
              onReset: h
            }, null, 8, ["modelValue", "fields", "loading", "cols", "label-width"])
          ])
        ]),
        _: 3
      }, 8, ["modelValue", "title", "width", "destroy-on-close"]);
    };
  }
}), At = { class: "yzh-search-bar" }, zt = { class: "yzh-search-bar__inner" }, Nt = { class: "yzh-search-bar__fields" }, Dt = { class: "yzh-search-bar__field-row" }, $t = { class: "yzh-search-bar__label" }, Bt = { class: "yzh-search-bar__actions" }, Rt = /* @__PURE__ */ ne({
  __name: "YzhSearchBar",
  props: {
    fields: {},
    defaultValues: {},
    cols: { default: 2 },
    maxFields: { default: 2 },
    inputWidth: { default: "200px" }
  },
  emits: ["search", "reset"],
  setup(a, { emit: o }) {
    const e = a, t = o, l = be({});
    Se(
      () => e.defaultValues,
      (c) => {
        c && (Object.keys(l).forEach((h) => delete l[h]), Object.assign(l, c));
      },
      { immediate: !0, deep: !0 }
    );
    const n = e.fields.slice(0, e.maxFields);
    function i() {
      const c = {};
      n.forEach((h) => {
        const d = l[h.prop];
        d !== void 0 && d !== "" && !(Array.isArray(d) && d.length === 0) && (c[h.prop] = d);
      }), t("search", c);
    }
    function r() {
      n.forEach((c) => {
        delete l[c.prop];
      }), t("reset");
    }
    return (c, h) => {
      const d = N("el-input"), v = N("el-input-number"), C = N("el-option"), _ = N("el-select"), g = N("el-date-picker"), E = N("el-button");
      return p(), D("div", At, [
        R("div", zt, [
          R("div", Nt, [
            (p(!0), D(oe, null, ue(Ae(n), (f) => (p(), D("div", {
              key: f.prop,
              class: "yzh-search-bar__field"
            }, [
              R("div", Dt, [
                R("label", $t, U(f.label), 1),
                R("div", {
                  class: "yzh-search-bar__input-wrap",
                  style: ze({ width: a.inputWidth })
                }, [
                  !f.type || f.type === "text" ? (p(), z(d, {
                    key: 0,
                    modelValue: l[f.prop],
                    "onUpdate:modelValue": (x) => l[f.prop] = x,
                    placeholder: f.placeholder || `请输入${f.label}`,
                    clearable: "",
                    onKeyup: vt(i, ["enter"])
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : f.type === "number" ? (p(), z(v, {
                    key: 1,
                    modelValue: l[f.prop],
                    "onUpdate:modelValue": (x) => l[f.prop] = x,
                    placeholder: f.placeholder || `请输入${f.label}`
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : f.type === "select" ? (p(), z(_, {
                    key: 2,
                    modelValue: l[f.prop],
                    "onUpdate:modelValue": (x) => l[f.prop] = x,
                    placeholder: f.placeholder || `请选择${f.label}`,
                    clearable: "",
                    filterable: ""
                  }, {
                    default: w(() => [
                      (p(!0), D(oe, null, ue(f.options || [], (x) => (p(), z(C, {
                        key: x.value,
                        label: x.label,
                        value: x.value
                      }, null, 8, ["label", "value"]))), 128))
                    ]),
                    _: 2
                  }, 1032, ["modelValue", "onUpdate:modelValue", "placeholder"])) : f.type === "date" ? (p(), z(g, {
                    key: 3,
                    modelValue: l[f.prop],
                    "onUpdate:modelValue": (x) => l[f.prop] = x,
                    type: "date",
                    placeholder: f.placeholder || `请选择${f.label}`,
                    "value-format": "YYYY-MM-DD"
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : f.type === "dateRange" ? (p(), z(g, {
                    key: 4,
                    modelValue: l[f.prop],
                    "onUpdate:modelValue": (x) => l[f.prop] = x,
                    type: "daterange",
                    placeholder: f.placeholder || `请选择${f.label}`,
                    "value-format": "YYYY-MM-DD",
                    "range-separator": "至",
                    "start-placeholder": "开始日期",
                    "end-placeholder": "结束日期"
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : j("", !0)
                ], 4)
              ])
            ]))), 128)),
            h[0] || (h[0] = R("div", { class: "yzh-search-bar__spacer" }, null, -1))
          ]),
          R("div", Bt, [
            P(E, {
              type: "primary",
              onClick: i
            }, {
              default: w(() => [...h[1] || (h[1] = [
                R("i", { class: "bi bi-search" }, null, -1),
                V(" 查询 ", -1)
              ])]),
              _: 1
            }),
            P(E, { onClick: r }, {
              default: w(() => [...h[2] || (h[2] = [
                R("i", { class: "bi bi-arrow-counterclockwise" }, null, -1),
                V(" 重置 ", -1)
              ])]),
              _: 1
            })
          ])
        ])
      ]);
    };
  }
}), Vt = /* @__PURE__ */ he(Rt, [["__scopeId", "data-v-d0360654"]]), Pt = { class: "yzh-toolbar" }, Lt = { class: "yzh-toolbar__left" }, Et = { class: "yzh-toolbar__right" }, Mt = /* @__PURE__ */ ne({
  __name: "YzhToolbar",
  props: {
    buttons: { default: () => [] }
  },
  emits: ["action"],
  setup(a, { emit: o }) {
    const e = o;
    function t(l) {
      l.disabled || e("action", l.key, l);
    }
    return (l, n) => {
      const i = N("el-button");
      return p(), D("div", Pt, [
        R("div", Lt, [
          (p(!0), D(oe, null, ue(a.buttons.filter((r) => r.group !== "right"), (r) => (p(), z(i, {
            key: r.key,
            type: r.type ?? "default",
            disabled: r.disabled,
            onClick: (c) => t(r)
          }, {
            default: w(() => [
              V(U(r.text), 1)
            ]),
            _: 2
          }, 1032, ["type", "disabled", "onClick"]))), 128)),
          W(l.$slots, "left", {}, void 0, !0)
        ]),
        R("div", Et, [
          (p(!0), D(oe, null, ue(a.buttons.filter((r) => r.group === "right"), (r) => (p(), z(i, {
            key: r.key,
            type: r.type ?? "default",
            disabled: r.disabled,
            onClick: (c) => t(r)
          }, {
            default: w(() => [
              V(U(r.text), 1)
            ]),
            _: 2
          }, 1032, ["type", "disabled", "onClick"]))), 128)),
          W(l.$slots, "right", {}, void 0, !0)
        ])
      ]);
    };
  }
}), Ut = /* @__PURE__ */ he(Mt, [["__scopeId", "data-v-95dfd97a"]]), It = /* @__PURE__ */ ne({
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
  setup(a, { emit: o }) {
    const e = a, t = o, l = G({
      get: () => e.page,
      set: (i) => t("update:page", i)
    }), n = G({
      get: () => e.pageSize,
      set: (i) => t("update:pageSize", i)
    });
    return (i, r) => {
      const c = N("el-pagination");
      return p(), z(c, {
        "current-page": l.value,
        "onUpdate:currentPage": r[0] || (r[0] = (h) => l.value = h),
        "page-size": n.value,
        "onUpdate:pageSize": r[1] || (r[1] = (h) => n.value = h),
        total: a.total,
        "page-sizes": a.pageSizes,
        layout: a.layout,
        background: a.background,
        size: a.size
      }, null, 8, ["current-page", "page-size", "total", "page-sizes", "layout", "background", "size"]);
    };
  }
}), Ot = /* @__PURE__ */ he(It, [["__scopeId", "data-v-13879d57"]]), Kt = { class: "yzh-page-layout" }, Yt = {
  key: 0,
  class: "yzh-page-layout__search"
}, jt = {
  key: 1,
  class: "yzh-page-layout__toolbar"
}, Wt = { class: "yzh-page-layout__toolbar-left" }, qt = { class: "yzh-page-layout__toolbar-right" }, Gt = {
  key: 2,
  class: "yzh-page-layout__footer"
}, Ht = /* @__PURE__ */ ne({
  __name: "YzhPageLayout",
  props: {
    pageTitle: {},
    helpText: {},
    showTitle: { type: Boolean },
    noPadding: { type: Boolean },
    hideToolbar: { type: Boolean }
  },
  setup(a) {
    return (o, e) => (p(), D("div", Kt, [
      o.$slots.search ? (p(), D("div", Yt, [
        W(o.$slots, "search", {}, void 0, !0)
      ])) : j("", !0),
      !a.hideToolbar && (o.$slots.toolbar || o.$slots["toolbar-left"] || o.$slots["toolbar-right"]) ? (p(), D("div", jt, [
        W(o.$slots, "toolbar", {}, () => [
          R("div", Wt, [
            W(o.$slots, "toolbar-left", {}, void 0, !0)
          ]),
          R("div", qt, [
            W(o.$slots, "toolbar-right", {}, void 0, !0)
          ])
        ], !0)
      ])) : j("", !0),
      R("div", {
        class: ke(["yzh-page-layout__content", { "yzh-page-layout__content--no-padding": a.noPadding }])
      }, [
        W(o.$slots, "default", {}, void 0, !0)
      ], 2),
      o.$slots.pagination ? (p(), D("div", Gt, [
        W(o.$slots, "pagination", {}, void 0, !0)
      ])) : j("", !0)
    ]));
  }
}), Va = /* @__PURE__ */ he(Ht, [["__scopeId", "data-v-756b5466"]]), Xt = { class: "yzh-dialog__body" }, Jt = { class: "yzh-dialog__footer" }, Zt = /* @__PURE__ */ ne({
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
  setup(a, { emit: o }) {
    const e = a, t = o, l = G(() => typeof e.width == "number" ? `${e.width}px` : e.width);
    function n() {
      t("update:modelValue", !1), t("close");
    }
    function i() {
      e.confirmDisabled || e.confirmLoading || t("confirm");
    }
    function r() {
      t("cancel"), n();
    }
    return Se(
      () => e.modelValue,
      (c) => {
        c && t("open");
      }
    ), (c, h) => {
      const d = N("el-button"), v = N("el-dialog");
      return p(), z(v, {
        "model-value": a.modelValue,
        title: a.title,
        width: a.fullscreen ? "100%" : l.value,
        fullscreen: a.fullscreen,
        "show-close": a.showClose,
        "close-on-click-modal": a.closeOnClickModal,
        "z-index": a.zIndex,
        class: ke(a.customClass),
        top: a.fullscreen ? "0" : a.top,
        "destroy-on-close": a.destroyOnClose,
        "onUpdate:modelValue": h[0] || (h[0] = (C) => t("update:modelValue", C))
      }, bt({
        default: w(() => [
          R("div", Xt, [
            W(c.$slots, "default", {}, void 0, !0)
          ])
        ]),
        _: 2
      }, [
        a.showFooter ? {
          name: "footer",
          fn: w(() => [
            W(c.$slots, "footer", {
              confirm: i,
              cancel: r
            }, () => [
              R("div", Jt, [
                P(d, { onClick: r }, {
                  default: w(() => [
                    V(U(a.cancelText), 1)
                  ]),
                  _: 1
                }),
                P(d, {
                  type: a.confirmType,
                  disabled: a.confirmDisabled,
                  loading: a.confirmLoading,
                  onClick: i
                }, {
                  default: w(() => [
                    V(U(a.confirmText), 1)
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
}), Pa = /* @__PURE__ */ he(Zt, [["__scopeId", "data-v-dbdcca59"]]);
/*! Element Plus Icons Vue v2.3.2 */
var Qt = /* @__PURE__ */ ne({
  name: "Document",
  __name: "document",
  setup(a) {
    return (o, e) => (p(), D("svg", {
      xmlns: "http://www.w3.org/2000/svg",
      viewBox: "0 0 1024 1024"
    }, [
      R("path", {
        fill: "currentColor",
        d: "M832 384H576V128H192v768h640zm-26.496-64L640 154.496V320zM160 64h480l256 256v608a32 32 0 0 1-32 32H160a32 32 0 0 1-32-32V96a32 32 0 0 1 32-32m160 448h384v64H320zm0-192h160v64H320zm0 384h384v64H320z"
      })
    ]));
  }
}), eo = Qt, to = /* @__PURE__ */ ne({
  name: "Folder",
  __name: "folder",
  setup(a) {
    return (o, e) => (p(), D("svg", {
      xmlns: "http://www.w3.org/2000/svg",
      viewBox: "0 0 1024 1024"
    }, [
      R("path", {
        fill: "currentColor",
        d: "M128 192v640h768V320H485.76L357.504 192zm-32-64h287.872l128.384 128H928a32 32 0 0 1 32 32v576a32 32 0 0 1-32 32H96a32 32 0 0 1-32-32V160a32 32 0 0 1 32-32"
      })
    ]));
  }
}), oo = to;
const ao = { class: "yzh-tree" }, lo = {
  key: 0,
  class: "yzh-tree__search"
}, no = ["onMouseenter"], so = {
  key: 1,
  class: "yzh-tree__icon"
}, ro = {
  key: 4,
  class: "yzh-tree__badge"
}, io = /* @__PURE__ */ ne({
  __name: "YzhTree",
  props: {
    data: {},
    nodeKey: { default: "Code" },
    labelField: { default: "Name" },
    childrenField: { default: "Children" },
    isLeafField: { default: "IsLeaf" },
    extraField: { default: "Extra" },
    showCheckbox: { type: Boolean, default: !1 },
    checkStrictly: { type: Boolean, default: !1 },
    lazy: { type: Boolean, default: !1 },
    loadData: {},
    defaultExpandAll: { type: Boolean, default: !1 },
    expandOnClickNode: { type: Boolean, default: !0 },
    highlightCurrent: { type: Boolean, default: !0 },
    currentKey: {},
    searchable: { type: Boolean, default: !1 },
    searchPlaceholder: { default: "搜索节点" },
    highlightKeyword: { type: Boolean, default: !0 },
    nodeActions: { type: [Array, Function], default: () => [] },
    legacyNodeActions: { default: () => ({}) },
    getActionLabel: { type: Function, default: void 0 }
  },
  emits: ["node-click", "check-change", "node-expand", "node-collapse", "node-action"],
  setup(a, { expose: o, emit: e }) {
    function t(s) {
      return /[\u{1F300}-\u{1F9FF}]|[\u{2600}-\u{26FF}]|[\u{2700}-\u{27BF}]/u.test(s);
    }
    function l(s, u) {
      if (!s) return;
      const m = u.charAt(0).toLowerCase() + u.slice(1);
      return s[u] ?? s[m];
    }
    const n = a, i = e, r = k(), c = k(""), h = k(null);
    function d(s) {
      return String(l(s, n.nodeKey) ?? "");
    }
    function v(s) {
      return String(l(s, n.labelField) ?? "");
    }
    function C(s) {
      return l(s, n.childrenField) ?? [];
    }
    function _(s) {
      return l(s, n.isLeafField) === !0;
    }
    function g(s) {
      return l(s, n.extraField) ?? {};
    }
    const E = G(() => ({
      label: n.labelField,
      children: n.childrenField,
      // 必须读叶子字段（后端 TreeControllerBase.FillIsLeafBatch 批量计算）。
      // 读错字段会让末端节点也长出展开箭头并白跑一次 tree/children。
      isLeaf: (s) => _(s),
      disabled: (s) => g(s).disabled ?? !1
    }));
    function f(s, u) {
      return s ? (v(u) || "").toLowerCase().includes(String(s).toLowerCase()) : !0;
    }
    function x(s) {
      return c.value ? (v(s) || "").toLowerCase().includes(c.value.toLowerCase()) : !1;
    }
    let q = null;
    Se(c, (s) => {
      q && clearTimeout(q), q = setTimeout(() => {
        var u;
        (u = r.value) == null || u.filter(s);
      }, 200);
    });
    function ee(s) {
      let u;
      typeof n.nodeActions == "function" ? u = n.nodeActions(s) || [] : u = n.nodeActions;
      const m = Object.entries(n.legacyNodeActions || {}).map(([b, T]) => ({
        key: b,
        text: n.getActionLabel ? n.getActionLabel(b, s) : T
      }));
      return [...u, ...m].filter((b) => b.visible !== !1);
    }
    function ae(s) {
      return s.danger ? "yzh-tree__action-danger" : s.type === "warning" ? "yzh-tree__action-toggle" : "";
    }
    function se(s) {
      i("node-click", s);
    }
    function le() {
      if (!r.value) return;
      const s = r.value.getCheckedNodes();
      i("check-change", s);
    }
    function S(s) {
      i("node-expand", s);
    }
    function L(s) {
      i("node-collapse", s);
    }
    function K(s, u) {
      i("node-action", s, u);
    }
    function Y() {
      var s;
      return ((s = r.value) == null ? void 0 : s.getCheckedNodes()) ?? [];
    }
    function X(s) {
      var u;
      (u = r.value) == null || u.setCheckedNodes(s);
    }
    function te(s, u) {
      var m;
      (m = r.value) == null || m.setChecked(s, u, !1);
    }
    function Z() {
      const s = (u) => {
        var m;
        for (const b of u) {
          const T = (m = r.value) == null ? void 0 : m.store;
          T && T.nodesMap[d(b)] && (T.nodesMap[d(b)].expanded = !0), C(b).length && s(C(b));
        }
      };
      s(n.data);
    }
    function me() {
      const s = (u) => {
        var m;
        for (const b of u) {
          const T = (m = r.value) == null ? void 0 : m.store;
          T && T.nodesMap[d(b)] && (T.nodesMap[d(b)].expanded = !1), C(b).length && s(C(b));
        }
      };
      s(n.data);
    }
    function fe(s) {
      var u;
      (u = r.value) == null || u.setCurrentKey(s);
    }
    function pe(s, u) {
      var m;
      if (r.value) {
        if (s) {
          try {
            r.value.append(u, s);
            return;
          } catch {
          }
          const b = r.value.store, T = (m = b == null ? void 0 : b.nodesMap) == null ? void 0 : m[s];
          if (T && typeof T.append == "function") {
            T.append(u);
            return;
          }
          if (Ce(n.data, s, u)) return;
        }
        n.data.push(u);
      }
    }
    function Ce(s, u, m) {
      for (const b of s) {
        if (d(b) === u) {
          const T = C(b);
          return T.push(m), b[n.childrenField] = T, b[n.isLeafField] = !1, !0;
        }
        if (C(b).length && Ce(C(b), u, m))
          return !0;
      }
      return !1;
    }
    o({
      getCheckedNodes: Y,
      setCheckedNodes: X,
      setChecked: te,
      expandAll: Z,
      collapseAll: me,
      setCurrentNode: fe,
      appendNode: pe,
      /** 从树中移除指定节点（不触发 API，仅更新本地树 UI） */
      removeNode: (s, u) => {
        var T;
        if (!r.value) return;
        try {
          r.value.remove(u);
          return;
        } catch {
        }
        const m = r.value.store, b = (T = m == null ? void 0 : m.nodesMap) == null ? void 0 : T[u];
        if (b && b.parentNode) {
          b.parentNode.remove(b);
          return;
        }
        re(n.data, u);
      }
    });
    function re(s, u) {
      for (let m = 0; m < s.length; m++) {
        if (d(s[m]) === u)
          return s.splice(m, 1), !0;
        if (C(s[m]).length && re(C(s[m]), u))
          return !0;
      }
      return !1;
    }
    return (s, u) => {
      const m = N("el-icon"), b = N("el-button"), T = N("el-dropdown-item"), M = N("el-dropdown-menu"), Q = N("el-dropdown");
      return p(), D("div", ao, [
        a.searchable ? (p(), D("div", lo, [
          P(Ae(We), {
            modelValue: c.value,
            "onUpdate:modelValue": u[0] || (u[0] = (A) => c.value = A),
            placeholder: a.searchPlaceholder,
            clearable: "",
            "prefix-icon": "Search",
            size: "small"
          }, null, 8, ["modelValue", "placeholder"])
        ])) : j("", !0),
        P(Ae(_t), {
          ref_key: "treeRef",
          ref: r,
          data: a.data,
          props: E.value,
          "show-checkbox": a.showCheckbox,
          "check-strictly": a.checkStrictly,
          lazy: a.lazy,
          load: a.loadData,
          "default-expand-all": a.defaultExpandAll,
          "expand-on-click-node": a.expandOnClickNode,
          "highlight-current": a.highlightCurrent,
          "node-key": a.nodeKey,
          "current-node-key": a.currentKey,
          "filter-node-method": f,
          "empty-text": "暂无数据",
          class: "yzh-tree__inner",
          onNodeClick: se,
          onCheckChange: le,
          onNodeExpand: S,
          onNodeCollapse: L
        }, {
          default: w(({ data: A }) => [
            R("div", {
              class: "yzh-tree__node",
              onMouseenter: (O) => h.value = d(A),
              onMouseleave: u[2] || (u[2] = (O) => h.value = null)
            }, [
              g(A).icon && !t(g(A).icon) ? (p(), z(m, {
                key: 0,
                class: "yzh-tree__icon"
              }, {
                default: w(() => [
                  (p(), z(Ee(g(A).icon)))
                ]),
                _: 2
              }, 1024)) : g(A).icon ? (p(), D("span", so, U(g(A).icon), 1)) : _(A) ? (p(), z(m, {
                key: 2,
                class: "yzh-tree__icon yzh-tree__icon--leaf"
              }, {
                default: w(() => [
                  P(Ae(eo))
                ]),
                _: 1
              })) : (p(), z(m, {
                key: 3,
                class: "yzh-tree__icon yzh-tree__icon--folder"
              }, {
                default: w(() => [
                  P(Ae(oo))
                ]),
                _: 1
              })),
              R("span", {
                class: ke(["yzh-tree__label", { "is-highlight": a.highlightKeyword && x(A) }])
              }, U(v(A)), 3),
              g(A).badge ? (p(), D("span", ro, U(g(A).badge), 1)) : j("", !0),
              ee(A).length ? (p(), z(Q, {
                key: 5,
                trigger: "click",
                onCommand: (O) => K(O, A),
                onClick: u[1] || (u[1] = Ct(() => {
                }, ["stop"]))
              }, {
                dropdown: w(() => [
                  P(M, null, {
                    default: w(() => [
                      (p(!0), D(oe, null, ue(ee(A), (O) => (p(), z(T, {
                        key: O.key,
                        command: O.key,
                        disabled: O.disabled,
                        class: ke(ae(O))
                      }, {
                        default: w(() => [
                          V(U(O.text), 1)
                        ]),
                        _: 2
                      }, 1032, ["command", "disabled", "class"]))), 128))
                    ]),
                    _: 2
                  }, 1024)
                ]),
                default: w(() => [
                  P(b, {
                    link: "",
                    size: "small",
                    class: "yzh-tree__more-btn"
                  }, {
                    default: w(() => [...u[3] || (u[3] = [
                      V(" ⋯ ", -1)
                    ])]),
                    _: 1
                  })
                ]),
                _: 2
              }, 1032, ["onCommand"])) : j("", !0)
            ], 40, no)
          ]),
          _: 1
        }, 8, ["data", "props", "show-checkbox", "check-strictly", "lazy", "load", "default-expand-all", "expand-on-click-node", "highlight-current", "node-key", "current-node-key"])
      ]);
    };
  }
}), et = /* @__PURE__ */ he(io, [["__scopeId", "data-v-ef58d20f"]]), co = { class: "yzh-tree-table" }, uo = { class: "yzh-tree-table__main" }, ho = {
  key: 0,
  class: "yzh-tree-table__tree-toolbar"
}, fo = {
  key: 1,
  class: "yzh-tree-table__tree-footer"
}, po = { class: "yzh-tree-table__table-panel" }, mo = /* @__PURE__ */ ne({
  __name: "YzhTreeTableLayout",
  props: {
    treeData: { default: () => [] },
    nodeKey: { default: "Code" },
    labelField: { default: "Name" },
    childrenField: { default: "Children" },
    isLeafField: { default: "IsLeaf" },
    extraField: { default: "Extra" },
    treeWidth: { default: 260 },
    treeToolbar: { type: Boolean, default: !0 },
    treeSearchable: { type: Boolean, default: !0 },
    treeCheckable: { type: Boolean, default: !1 },
    treeCheckStrictly: { type: Boolean, default: !1 },
    treeLazy: { type: Boolean, default: !1 },
    treeLoadData: {},
    treeDefaultExpandAll: { type: Boolean, default: !1 },
    nodeActions: { type: [Array, Function], default: () => [] },
    legacyNodeActions: { default: () => ({}) },
    getActionLabel: { type: Function, default: void 0 }
  },
  emits: ["tree-node-click", "tree-check-change", "tree-node-action"],
  setup(a, { expose: o, emit: e }) {
    const t = a, l = e, n = k(), i = k(""), r = G(() => i.value ? _(t.treeData, i.value) : t.treeData);
    function c(g) {
      l("tree-node-click", g);
    }
    function h(g) {
      l("tree-check-change", g);
    }
    function d(g, E) {
      l("tree-node-action", g, E);
    }
    function v() {
      var g;
      (g = n.value) == null || g.expandAll();
    }
    function C() {
      var g;
      (g = n.value) == null || g.collapseAll();
    }
    function _(g, E) {
      const f = E.toLowerCase(), x = [];
      for (const q of g) {
        const ae = String(q[t.labelField] ?? "").toLowerCase().includes(f), se = q[t.childrenField] ?? [], le = _(se, E);
        (ae || le.length > 0) && x.push({ ...q, [t.childrenField]: le });
      }
      return x;
    }
    return o({
      treeRef: n,
      getCheckedNodes: () => {
        var g;
        return ((g = n.value) == null ? void 0 : g.getCheckedNodes()) ?? [];
      },
      expandAll: v,
      collapseAll: C,
      appendNode: (g, E) => {
        var f;
        return (f = n.value) == null ? void 0 : f.appendNode(g, E);
      },
      removeNode: (g, E) => {
        var f;
        return (f = n.value) == null ? void 0 : f.removeNode(g, E);
      }
    }), (g, E) => (p(), D("div", co, [
      R("div", uo, [
        R("div", {
          class: "yzh-tree-table__tree-panel",
          style: ze({ width: a.treeWidth + "px" })
        }, [
          a.treeToolbar ? (p(), D("div", ho, [
            a.treeSearchable ? (p(), z(Ae(We), {
              key: 0,
              modelValue: i.value,
              "onUpdate:modelValue": E[0] || (E[0] = (f) => i.value = f),
              placeholder: "搜索节点",
              clearable: "",
              "prefix-icon": "Search"
            }, null, 8, ["modelValue"])) : j("", !0)
          ])) : j("", !0),
          P(et, {
            ref_key: "treeRef",
            ref: n,
            data: r.value,
            "node-key": a.nodeKey,
            "label-field": a.labelField,
            "children-field": a.childrenField,
            "is-leaf-field": a.isLeafField,
            "extra-field": a.extraField,
            "show-checkbox": a.treeCheckable,
            "check-strictly": a.treeCheckStrictly,
            lazy: a.treeLazy,
            "load-data": a.treeLoadData,
            "default-expand-all": a.treeDefaultExpandAll,
            "node-actions": a.nodeActions,
            "legacy-node-actions": a.legacyNodeActions,
            "get-action-label": a.getActionLabel,
            onNodeClick: c,
            onCheckChange: h,
            onNodeAction: d
          }, null, 8, ["data", "node-key", "label-field", "children-field", "is-leaf-field", "extra-field", "show-checkbox", "check-strictly", "lazy", "load-data", "default-expand-all", "node-actions", "legacy-node-actions", "get-action-label"]),
          g.$slots.treeFooter ? (p(), D("div", fo, [
            W(g.$slots, "treeFooter", {}, void 0, !0)
          ])) : j("", !0)
        ], 4),
        R("div", po, [
          W(g.$slots, "default", {}, void 0, !0)
        ])
      ])
    ]));
  }
}), La = /* @__PURE__ */ he(mo, [["__scopeId", "data-v-d15619e1"]]), yo = { class: "yzh-table" }, go = { class: "yzh-column-settings" }, vo = { class: "yzh-column-settings__body" }, bo = { class: "yzh-column-settings__footer" }, Co = { key: 1 }, wo = { class: "yzh-table__empty" }, ko = {
  key: 1,
  class: "yzh-table__error"
}, To = {
  key: 2,
  class: "yzh-table__pagination"
}, _o = /* @__PURE__ */ ne({
  __name: "YzhTable",
  props: {
    columns: {},
    dataLoader: {},
    searchFields: {},
    selectable: { type: Boolean, default: !1 },
    selectMode: { default: void 0 },
    showPagination: { type: Boolean, default: !0 },
    pageSize: { default: 20 },
    defaultSort: {},
    height: {},
    rowKey: { default: "Code" },
    emptyText: { default: "暂无数据" },
    toolbar: { type: [Boolean, Object], default: !0 },
    toolbarActions: { default: () => [] },
    searchMaxFields: { default: 2 },
    noPadding: { type: Boolean, default: !1 },
    rowActionButtons: { type: [Object, Array, Function], default: () => [] },
    rowActionLink: { type: Boolean, default: !0 },
    actionMaxInline: { default: 0 },
    defaultExpandAll: { type: Boolean, default: !1 }
  },
  emits: ["selection-change", "row-click", "refresh", "row-action", "toolbar-action"],
  setup(a, { expose: o, emit: e }) {
    const t = a, l = e, n = k(!1), i = k(""), r = k([]), c = k(0), h = k([]), d = k(1), v = k(t.pageSize), C = k(t.defaultSort || null), _ = be({}), g = k(/* @__PURE__ */ new Set()), E = G(() => t.selectMode ? t.selectMode : t.selectable ? "multiple" : "none"), f = G(() => E.value === "multiple"), x = G(
      () => t.columns.filter((y) => y.label && y.prop !== "__yzh_action")
    ), q = G(
      () => t.columns.filter((y) => !(y.hidden || g.value.has(y.prop)))
    );
    function ee(y) {
      return Object.entries(y).map(([B, H]) => ({ key: B, text: H }));
    }
    function ae(y) {
      const B = typeof t.rowActionButtons == "function" ? t.rowActionButtons(y) : t.rowActionButtons;
      return (Array.isArray(B) ? B : ee(B || {})).filter((de) => de.visible !== !1);
    }
    const se = G(() => {
      const y = t.columns.some((H) => H.prop === "actions");
      return (typeof t.rowActionButtons == "function" || (Array.isArray(t.rowActionButtons) ? t.rowActionButtons.length : Object.keys(t.rowActionButtons || {}).length) > 0) && !y;
    }), le = G(() => t.actionMaxInline > 0);
    function S(y) {
      return !le.value || y.length <= t.actionMaxInline ? { inline: y, overflow: [] } : { inline: y.slice(0, t.actionMaxInline), overflow: y.slice(t.actionMaxInline) };
    }
    const L = G(
      () => t.toolbarActions.filter((y) => y.visible !== !1)
    );
    async function K(y, B) {
      if (!y.disabled) {
        if (y.confirm)
          try {
            await xe.confirm(y.confirm, "操作确认", { type: "warning" });
          } catch {
            return;
          }
        l("row-action", y.key, B, y);
      }
    }
    async function Y(y) {
      if (!y.disabled) {
        if (y.confirm)
          try {
            await xe.confirm(y.confirm, "操作确认", { type: "warning" });
          } catch {
            return;
          }
        l("toolbar-action", y.key, y);
      }
    }
    function X(y, B) {
      B ? g.value.delete(y.prop) : g.value.add(y.prop), g.value = new Set(g.value);
    }
    function te(y) {
      if (y.sortable === !1) return;
      const B = y.prop;
      C.value && C.value.prop === B ? C.value = { ...C.value, order: C.value.order === "asc" ? "desc" : "asc" } : C.value = { prop: B, order: "asc" };
    }
    function Z(y) {
      const B = y.prop;
      return !C.value || C.value.prop !== B ? "排序" : C.value.order === "asc" ? "↑ 升序" : "↓ 降序";
    }
    function me() {
      g.value = /* @__PURE__ */ new Set(), C.value = t.defaultSort || null;
    }
    function fe() {
      re();
    }
    const pe = G(() => t.toolbar === !1 ? {} : t.toolbar === !0 ? { columnSetting: !0 } : t.toolbar), Ce = G(() => Object.keys(pe.value).length > 0 || L.value.length > 0);
    async function re() {
      n.value = !0, i.value = "";
      try {
        const y = new Set(h.value.map((de) => de[t.rowKey])), B = {
          page: d.value,
          rows: v.value,
          ...C.value ? { sort: C.value.prop, order: C.value.order } : {},
          ..._
        }, H = await t.dataLoader(B);
        if (r.value = H.rows || [], c.value = H.total || 0, y.size > 0) {
          const de = [];
          for (const Pe of r.value)
            y.has(Pe[t.rowKey]) && de.push(Pe);
          h.value = de;
        }
      } catch (y) {
        i.value = (y == null ? void 0 : y.message) || "数据加载失败", r.value = [], c.value = 0, J.error(i.value);
      } finally {
        n.value = !1;
      }
    }
    function s({ prop: y, order: B }) {
      B ? C.value = {
        prop: y,
        order: B === "ascending" ? "asc" : "desc"
      } : C.value = null, re();
    }
    function u(y) {
      d.value = y, re();
    }
    function m(y) {
      v.value = y, d.value = 1, re();
    }
    function b(y) {
      Object.assign(_, y), d.value = 1, re();
    }
    function T() {
      Object.keys(_).forEach((y) => delete _[y]), t.searchFields && t.searchFields.slice(0, t.searchMaxFields).forEach((y) => {
        y.defaultValue !== void 0 && (_[y.prop] = y.defaultValue);
      }), d.value = 1, re();
    }
    function M(y) {
      h.value = y, l("selection-change", y);
    }
    function Q(y, B) {
      l("row-click", y, B);
    }
    wt();
    let A = !1;
    const O = G(() => {
      if (typeof t.rowActionButtons == "function")
        return 4 * 70 + 40;
      const y = Array.isArray(t.rowActionButtons) ? t.rowActionButtons.length : Object.keys(t.rowActionButtons || {}).length;
      return y > 0 ? y * 70 + 40 : 140;
    });
    Se(
      () => typeof t.rowActionButtons == "function" ? 1 : Array.isArray(t.rowActionButtons) ? t.rowActionButtons.length : Object.keys(t.rowActionButtons || {}).length,
      (y) => {
      },
      { immediate: !0 }
    );
    function ye() {
      d.value = 1, re(), l("refresh");
    }
    Re(() => {
      t.searchFields && t.searchFields.slice(0, t.searchMaxFields).forEach((y) => {
        y.defaultValue !== void 0 && (_[y.prop] = y.defaultValue);
      }), re();
    });
    function $e(y, B = "top") {
      B === "top" ? r.value.unshift(y) : r.value.push(y), c.value++;
    }
    function ie(y, B) {
      const H = r.value.findIndex((de) => y(de));
      H >= 0 && r.value.splice(H, 1, B);
    }
    function Fe(y) {
      const B = r.value.findIndex((H) => y(H));
      B >= 0 && (r.value.splice(B, 1), c.value = Math.max(0, c.value - 1));
    }
    function Te() {
      return r.value.length;
    }
    function Ve(y, B) {
      if (B) {
        const H = new Set(h.value);
        for (const de of r.value)
          y(de) && !H.has(de) && h.value.push(de);
      } else
        h.value = h.value.filter((H) => !y(H));
      l("selection-change", [...h.value]);
    }
    return o({
      refresh: ye,
      loadData: re,
      insertRow: $e,
      replaceRow: ie,
      removeRow: Fe,
      getRowCount: Te,
      getSelectedRows: () => h.value,
      setCheckedRows: Ve,
      clearSelection: () => {
        h.value = [], l("selection-change", []);
      }
    }), (y, B) => {
      const H = N("el-button"), de = N("el-checkbox"), Pe = N("el-popover"), Ie = N("el-table-column"), Je = N("el-tag"), ct = N("el-dropdown-item"), ut = N("el-dropdown-menu"), ht = N("el-dropdown"), ft = N("el-empty"), pt = N("el-table"), mt = kt("loading");
      return p(), D("div", yo, [
        a.searchFields && a.searchFields.length ? (p(), z(Vt, {
          key: 0,
          fields: a.searchFields,
          "default-values": _,
          cols: 2,
          "max-fields": a.searchMaxFields,
          onSearch: b,
          onReset: T
        }, null, 8, ["fields", "default-values", "max-fields"])) : j("", !0),
        Ce.value ? (p(), z(Ut, {
          key: 1,
          buttons: L.value,
          onAction: B[0] || (B[0] = ($, I) => Y(I))
        }, {
          left: w(() => [
            W(y.$slots, "toolbar-left", {}, void 0, !0)
          ]),
          right: w(() => [
            W(y.$slots, "toolbar-right", {
              selected: h.value,
              refresh: ye
            }, () => [
              pe.value.columnSetting ? (p(), z(Pe, {
                key: 0,
                trigger: "click",
                placement: "bottom-end",
                width: 200
              }, {
                reference: w(() => [
                  P(H, { text: "" }, {
                    default: w(() => [...B[1] || (B[1] = [
                      R("i", { class: "bi bi-columns" }, null, -1),
                      V(" 列设置 ", -1)
                    ])]),
                    _: 1
                  })
                ]),
                default: w(() => [
                  R("div", go, [
                    B[4] || (B[4] = R("div", { class: "yzh-column-settings__header" }, "列筛选与排序", -1)),
                    R("div", vo, [
                      (p(!0), D(oe, null, ue(x.value, ($) => {
                        var I;
                        return p(), D("div", {
                          key: $.prop,
                          class: "yzh-column-settings__item"
                        }, [
                          P(de, {
                            "model-value": !g.value.has($.prop) && !$.hidden,
                            onChange: (ge) => X($, ge)
                          }, {
                            default: w(() => [
                              V(U($.label), 1)
                            ]),
                            _: 2
                          }, 1032, ["model-value", "onChange"]),
                          P(H, {
                            class: ke(["yzh-column-settings__sort-btn", { "is-active": ((I = C.value) == null ? void 0 : I.prop) === $.prop }]),
                            disabled: $.sortable === !1,
                            onClick: (ge) => te($)
                          }, {
                            default: w(() => [
                              V(U(Z($)), 1)
                            ]),
                            _: 2
                          }, 1032, ["class", "disabled", "onClick"])
                        ]);
                      }), 128))
                    ]),
                    R("div", bo, [
                      P(H, {
                        size: "small",
                        onClick: me
                      }, {
                        default: w(() => [...B[2] || (B[2] = [
                          V("重置", -1)
                        ])]),
                        _: 1
                      }),
                      P(H, {
                        size: "small",
                        type: "primary",
                        onClick: fe
                      }, {
                        default: w(() => [...B[3] || (B[3] = [
                          V("确定", -1)
                        ])]),
                        _: 1
                      })
                    ])
                  ])
                ]),
                _: 1
              })) : j("", !0)
            ], !0)
          ]),
          _: 3
        }, 8, ["buttons"])) : j("", !0),
        R("div", {
          class: ke(["yzh-table__wrapper", { "yzh-table__wrapper--no-padding": a.noPadding }])
        }, [
          R("div", {
            class: "yzh-table__body",
            style: ze(a.height ? { height: typeof a.height == "number" ? a.height + "px" : a.height } : {})
          }, [
            Tt((p(), z(pt, {
              data: r.value,
              "row-key": a.rowKey,
              "default-expand-all": a.defaultExpandAll,
              height: a.height !== void 0 && a.height !== null ? a.height : "100%",
              "highlight-current-row": E.value === "single",
              stripe: "",
              border: "",
              onSelectionChange: M,
              onSortChange: s,
              onRowClick: Q
            }, {
              empty: w(() => [
                R("div", wo, [
                  !n.value && !i.value ? (p(), z(ft, {
                    key: 0,
                    description: a.emptyText
                  }, null, 8, ["description"])) : i.value ? (p(), D("div", ko, [
                    B[7] || (B[7] = R("i", { class: "bi bi-exclamation-triangle" }, null, -1)),
                    R("span", null, U(i.value), 1),
                    P(H, {
                      text: "",
                      type: "primary",
                      onClick: ye
                    }, {
                      default: w(() => [...B[6] || (B[6] = [
                        V("重试", -1)
                      ])]),
                      _: 1
                    })
                  ])) : j("", !0)
                ])
              ]),
              default: w(() => [
                f.value ? (p(), z(Ie, {
                  key: 0,
                  type: "selection",
                  width: "48",
                  "reserve-selection": !1
                })) : j("", !0),
                (p(!0), D(oe, null, ue(q.value, ($) => (p(), z(Ie, {
                  key: $.prop,
                  prop: $.prop,
                  label: $.label,
                  width: $.width,
                  "min-width": $.minWidth,
                  fixed: $.fixed,
                  sortable: $.sortable,
                  align: $.align || "left",
                  "show-overflow-tooltip": !$.slot,
                  "class-name": $.className
                }, {
                  default: w(({ row: I, $index: ge }) => {
                    var Le;
                    return [
                      $.slot ? W(y.$slots, `column-${String($.prop)}`, {
                        row: I,
                        index: ge,
                        value: I[$.prop]
                      }, () => [
                        V(U($.formatter ? $.formatter(I[$.prop], I, ge) : I[$.prop]), 1)
                      ], !0, 0) : $.dictCode ? (p(), D(oe, { key: 1 }, [
                        $.tagType ? (p(), z(Je, {
                          key: 0,
                          type: $.tagType,
                          "disable-transitions": ""
                        }, {
                          default: w(() => [
                            V(U(I[$.prop]), 1)
                          ]),
                          _: 2
                        }, 1032, ["type"])) : (p(), D("span", Co, U(I[$.prop]), 1))
                      ], 64)) : $.tagMap ? (p(), z(Je, {
                        key: 2,
                        type: ((Le = $.tagTypeMap) == null ? void 0 : Le[I[$.prop]]) ?? "info",
                        size: "small",
                        "disable-transitions": ""
                      }, {
                        default: w(() => [
                          V(U($.tagMap[I[$.prop]] ?? I[$.prop]), 1)
                        ]),
                        _: 2
                      }, 1032, ["type"])) : (p(), D(oe, { key: 3 }, [
                        V(U($.formatter ? $.formatter(I[$.prop], I, ge) : I[$.prop]), 1)
                      ], 64))
                    ];
                  }),
                  _: 2
                }, 1032, ["prop", "label", "width", "min-width", "fixed", "sortable", "align", "show-overflow-tooltip", "class-name"]))), 128)),
                se.value ? (p(), z(Ie, {
                  key: 1,
                  label: "操作",
                  width: O.value,
                  fixed: "right",
                  align: "center"
                }, {
                  default: w(({ row: $ }) => [
                    (p(!0), D(oe, null, ue(S(ae($)).inline, (I) => (p(), z(H, {
                      key: I.key,
                      link: a.rowActionLink,
                      size: "small",
                      type: I.type ?? "primary",
                      disabled: I.disabled,
                      onClick: (ge) => K(I, $)
                    }, {
                      default: w(() => [
                        V(U(I.text), 1)
                      ]),
                      _: 2
                    }, 1032, ["link", "type", "disabled", "onClick"]))), 128)),
                    S(ae($)).overflow.length > 0 ? (p(), z(ht, {
                      key: 0,
                      trigger: "click",
                      onCommand: (I) => {
                        const ge = S(ae($)).overflow.find((Le) => Le.key === I);
                        ge && K(ge, $);
                      }
                    }, {
                      dropdown: w(() => [
                        P(ut, null, {
                          default: w(() => [
                            (p(!0), D(oe, null, ue(S(ae($)).overflow, (I) => (p(), z(ct, {
                              key: I.key,
                              command: I.key,
                              disabled: I.disabled,
                              class: ke({ "yzh-row-action-danger": I.type === "danger" })
                            }, {
                              default: w(() => [
                                V(U(I.text), 1)
                              ]),
                              _: 2
                            }, 1032, ["command", "disabled", "class"]))), 128))
                          ]),
                          _: 2
                        }, 1024)
                      ]),
                      default: w(() => [
                        P(H, {
                          link: "",
                          size: "small"
                        }, {
                          default: w(() => [...B[5] || (B[5] = [
                            V("更多", -1)
                          ])]),
                          _: 1
                        })
                      ]),
                      _: 2
                    }, 1032, ["onCommand"])) : j("", !0)
                  ]),
                  _: 1
                }, 8, ["width"])) : j("", !0)
              ]),
              _: 3
            }, 8, ["data", "row-key", "default-expand-all", "height", "highlight-current-row"])), [
              [mt, n.value]
            ])
          ], 4)
        ], 2),
        a.showPagination ? (p(), D("div", To, [
          P(Ot, {
            page: d.value,
            "page-size": v.value,
            total: c.value,
            "onUpdate:page": u,
            "onUpdate:pageSize": m
          }, null, 8, ["page", "page-size", "total"])
        ])) : j("", !0)
      ]);
    };
  }
}), xo = /* @__PURE__ */ he(_o, [["__scopeId", "data-v-f627f997"]]), So = { class: "yzh-tree-table-selector" }, Fo = {
  key: 0,
  class: "yzh-tree-table-selector__tree-search"
}, Ao = { class: "yzh-tree-table-selector__tree-actions" }, zo = {
  key: 1,
  class: "yzh-tree-table-selector__tree-footer"
}, No = { class: "yzh-tree-table-selector__table-panel" }, Do = { class: "yzh-tree-table-selector__table-toolbar" }, $o = { class: "yzh-tree-table-selector__selection-info" }, Bo = /* @__PURE__ */ ne({
  __name: "YzhTreeTableSelector",
  props: {
    treeData: {},
    treeWidth: { default: 260 },
    treeSearchable: { type: Boolean, default: !0 },
    treeDefaultExpandAll: { type: Boolean, default: !1 },
    treeLazy: { type: Boolean, default: !1 },
    treeLoadData: {},
    nodeKey: { default: "code" },
    checkStrictly: { type: Boolean, default: !0 },
    tableColumns: {},
    loadTableData: {},
    showPagination: { type: Boolean, default: !0 },
    pageSize: { default: 20 },
    rowKey: { default: "Code" }
  },
  emits: ["update:checkedTreeNodes", "update:checkedTableRows", "tree-check-change", "selection-change"],
  setup(a, { expose: o, emit: e }) {
    const t = a, l = e, n = k(), i = k(), r = k(""), c = k([]), h = k([]), d = k(/* @__PURE__ */ new Map()), v = G(() => r.value ? le(t.treeData, r.value) : t.treeData);
    function C() {
      var S;
      (S = n.value) == null || S.expandAll();
    }
    function _() {
      var S;
      (S = n.value) == null || S.collapseAll();
    }
    function g() {
      const S = (L) => {
        var K;
        for (const Y of L)
          (K = n.value) == null || K.setChecked(Y.Code, !0), Y.Children && Y.Children.length > 0 && S(Y.Children);
      };
      S(t.treeData);
    }
    function E() {
      var S;
      (S = n.value) == null || S.setCheckedNodes([]);
    }
    function f(S) {
      q(S.Code);
    }
    async function x() {
      if (!n.value) return;
      const S = n.value.getCheckedNodes();
      c.value = S;
      const L = S.map((te) => te.Code), K = [];
      for (const te of L) {
        const Z = await q(te);
        Z && K.push(...Z);
      }
      const Y = /* @__PURE__ */ new Set(), X = K.filter((te) => {
        const Z = te[t.rowKey];
        return Y.has(Z) ? !1 : (Y.add(Z), !0);
      });
      h.value = X, i.value && i.value.setCheckedRows(
        (te) => X.some((Z) => Z[t.rowKey] === te[t.rowKey]),
        !0
      ), l("update:checkedTreeNodes", S), l("update:checkedTableRows", X), l("tree-check-change", S);
    }
    async function q(S) {
      if (d.value.has(S))
        return d.value.get(S);
      try {
        const K = (await t.loadTableData(S)).rows ?? [];
        return d.value.set(S, K), K;
      } catch (L) {
        return J.error(L.message || "加载表格数据失败"), null;
      }
    }
    async function ee(S) {
      if (c.value.length === 0)
        return { rows: [], total: 0 };
      const L = [];
      for (const me of c.value) {
        const fe = await q(me.Code);
        fe && L.push(...fe);
      }
      const K = /* @__PURE__ */ new Set(), Y = L.filter((me) => {
        const fe = me[t.rowKey];
        return K.has(fe) ? !1 : (K.add(fe), !0);
      }), X = (S.page - 1) * S.rows, te = X + S.rows;
      return { rows: Y.slice(X, te), total: Y.length };
    }
    function ae(S) {
      h.value = S, l("update:checkedTableRows", S), l("selection-change", S);
    }
    function se() {
      var S;
      (S = i.value) == null || S.clearSelection(), E(), c.value = [], h.value = [], d.value.clear(), l("update:checkedTreeNodes", []), l("update:checkedTableRows", []);
    }
    function le(S, L) {
      const K = L.toLowerCase(), Y = [];
      for (const X of S) {
        const te = (X.Name || "").toLowerCase().includes(K), Z = le(X.Children ?? [], L);
        (te || Z.length > 0) && Y.push({ ...X, Children: Z });
      }
      return Y;
    }
    return o({
      getCheckedTreeNodes: () => c.value,
      getCheckedTableRows: () => h.value,
      clearSelection: se,
      refreshTable: () => {
        var S;
        return (S = i.value) == null ? void 0 : S.refresh();
      }
    }), (S, L) => {
      const K = N("el-input"), Y = N("el-button");
      return p(), D("div", So, [
        R("div", {
          class: "yzh-tree-table-selector__tree-panel",
          style: ze({ width: a.treeWidth + "px" })
        }, [
          a.treeSearchable ? (p(), D("div", Fo, [
            P(K, {
              modelValue: r.value,
              "onUpdate:modelValue": L[0] || (L[0] = (X) => r.value = X),
              placeholder: "搜索节点",
              clearable: "",
              "prefix-icon": "Search",
              size: "small"
            }, null, 8, ["modelValue"])
          ])) : j("", !0),
          R("div", Ao, [
            P(Y, {
              size: "small",
              onClick: C
            }, {
              default: w(() => [...L[1] || (L[1] = [
                V("展开全部", -1)
              ])]),
              _: 1
            }),
            P(Y, {
              size: "small",
              onClick: _
            }, {
              default: w(() => [...L[2] || (L[2] = [
                V("折叠全部", -1)
              ])]),
              _: 1
            }),
            P(Y, {
              size: "small",
              onClick: g
            }, {
              default: w(() => [...L[3] || (L[3] = [
                V("全选", -1)
              ])]),
              _: 1
            }),
            P(Y, {
              size: "small",
              onClick: E
            }, {
              default: w(() => [...L[4] || (L[4] = [
                V("取消全选", -1)
              ])]),
              _: 1
            })
          ]),
          P(et, {
            ref_key: "treeRef",
            ref: n,
            data: v.value,
            "show-checkbox": !0,
            "check-strictly": a.checkStrictly,
            lazy: a.treeLazy,
            "load-data": a.treeLoadData,
            "default-expand-all": a.treeDefaultExpandAll,
            "node-key": a.nodeKey,
            onCheckChange: x,
            onNodeClick: f
          }, null, 8, ["data", "check-strictly", "lazy", "load-data", "default-expand-all", "node-key"]),
          S.$slots.treeFooter ? (p(), D("div", zo, [
            W(S.$slots, "treeFooter", {}, void 0, !0)
          ])) : j("", !0)
        ], 4),
        R("div", No, [
          R("div", Do, [
            R("div", $o, [
              L[5] || (L[5] = V(" 已选择 ", -1)),
              R("strong", null, U(h.value.length), 1),
              L[6] || (L[6] = V(" 条记录 ", -1))
            ]),
            P(Y, {
              size: "small",
              type: "danger",
              onClick: se,
              disabled: h.value.length === 0
            }, {
              default: w(() => [...L[7] || (L[7] = [
                V(" 清空选择 ", -1)
              ])]),
              _: 1
            }, 8, ["disabled"])
          ]),
          P(xo, {
            ref_key: "tableRef",
            ref: i,
            columns: a.tableColumns,
            "data-loader": ee,
            selectable: !0,
            "show-pagination": a.showPagination,
            "page-size": a.pageSize,
            "row-key": a.rowKey,
            onSelectionChange: ae
          }, null, 8, ["columns", "show-pagination", "page-size", "row-key"])
        ])
      ]);
    };
  }
}), Ea = /* @__PURE__ */ he(Bo, [["__scopeId", "data-v-8e5e846d"]]), Ro = { class: "yzh-tree-table-check-selector" }, Vo = { class: "yzh-tree-table-check-selector__toolbar" }, Po = { class: "yzh-tree-table-check-selector__selection-info" }, Lo = {
  key: 0,
  class: "yzh-tree-table-check-selector__search"
}, Eo = { class: "yzh-tree-table-check-selector__toolbar-actions" }, Mo = /* @__PURE__ */ ne({
  __name: "YzhTreeTableCheckSelector",
  props: {
    flatData: {},
    nodeKey: { default: "Code" },
    parentKey: { default: "ParentCode" },
    nodeTypeField: { default: "NodeType" },
    checkField: { default: "CheckFlag" },
    columns: { default: () => [] },
    showTypeColumn: { type: Boolean, default: !0 },
    defaultExpandAll: { type: Boolean, default: !1 },
    typeLabels: { default: void 0 },
    typeTagTypes: { default: void 0 },
    checkAllExcludeTypes: { default: () => [] },
    cascade: { type: Boolean, default: !0 },
    searchable: { type: Boolean, default: !1 },
    searchFields: { default: () => ["Name"] },
    countType: { default: void 0 },
    searchPlaceholder: { default: "搜索" }
  },
  emits: ["check-change"],
  setup(a, { expose: o, emit: e }) {
    const t = a;
    function l(u) {
      var m;
      return ((m = t.typeLabels) == null ? void 0 : m[u]) ?? u;
    }
    function n(u) {
      var m;
      return ((m = t.typeTagTypes) == null ? void 0 : m[u]) ?? "info";
    }
    const i = e, r = k(), c = k([]), h = k(""), d = k(/* @__PURE__ */ new Set()), v = k(/* @__PURE__ */ new Map()), C = k(/* @__PURE__ */ new Set()), _ = k(!1), g = k(/* @__PURE__ */ new Set()), E = G(() => {
      var m;
      if (!t.countType) return d.value.size;
      let u = 0;
      for (const b of d.value)
        ((m = v.value.get(b)) == null ? void 0 : m[t.nodeTypeField]) === t.countType && u++;
      return u;
    }), f = G(() => {
      var M, Q;
      const u = h.value.trim().toLowerCase();
      if (!u) return t.flatData;
      const m = /* @__PURE__ */ new Set();
      for (const A of t.flatData)
        t.searchFields.some(
          (ye) => String(A[ye] ?? "").toLowerCase().includes(u)
        ) && m.add(String(A[t.nodeKey]));
      const b = new Map(t.flatData.map((A) => [String(A[t.nodeKey]), A])), T = new Set(m);
      for (const A of m) {
        let O = (M = b.get(A)) == null ? void 0 : M[t.parentKey];
        for (; O && !T.has(String(O)); )
          T.add(String(O)), O = (Q = b.get(String(O))) == null ? void 0 : Q[t.parentKey];
      }
      return t.flatData.filter((A) => T.has(String(A[t.nodeKey])));
    });
    function x(u) {
      const m = /* @__PURE__ */ new Map(), b = [];
      for (const T of u) {
        const M = {
          ...T,
          children: []
        };
        m.set(T[t.nodeKey], M), v.value.set(T[t.nodeKey], M);
      }
      for (const T of u) {
        const M = m.get(T[t.nodeKey]), Q = T[t.parentKey];
        Q && m.has(Q) ? m.get(Q).children.push(M) : b.push(M);
      }
      return b;
    }
    function q(u) {
      const m = /* @__PURE__ */ new Set();
      function b(T) {
        for (const M of T)
          M[t.checkField] && m.add(M[t.nodeKey]), M.children && M.children.length > 0 && b(M.children);
      }
      b(u), d.value = m;
    }
    function ee() {
      if (!r.value) return;
      _.value = !0, r.value.clearSelection();
      const u = /* @__PURE__ */ new Set();
      for (const m of d.value) {
        const b = v.value.get(m);
        b && (r.value.toggleRowSelection(b, !0), u.add(m));
      }
      g.value = u, _e(() => {
        _.value = !1;
      });
    }
    function ae(u, m) {
      if (_.value = !0, v.value.clear(), !u || u.length === 0) {
        c.value = [], m && (d.value = /* @__PURE__ */ new Set()), _e(() => {
          ee(), se();
        });
        return;
      }
      c.value = x(u), m && q(c.value), t.defaultExpandAll && (C.value.clear(), le(c.value)), _e(() => {
        ee(), se();
      });
    }
    function se() {
      _e(() => {
        _.value = !1;
      });
    }
    Se(
      () => t.flatData,
      (u) => {
        ae(u, !0);
      },
      { immediate: !0 }
    ), Se(h, () => {
      ae(f.value, !1);
    });
    function le(u) {
      for (const m of u)
        m.children && m.children.length > 0 && (C.value.add(m[t.nodeKey]), le(m.children));
    }
    function S() {
      le(c.value);
    }
    function L() {
      C.value.clear(), _.value = !0;
      const u = c.value;
      c.value = [], _e(() => {
        c.value = u, se();
      });
    }
    function K() {
      if (!r.value) return;
      _.value = !0;
      const u = X(c.value), m = new Set(d.value);
      for (const b of u)
        m.add(b[t.nodeKey]), r.value.toggleRowSelection(b, !0);
      d.value = m, g.value = new Set(m), _.value = !1, pe([], u.map((b) => b[t.nodeKey]));
    }
    function Y() {
      if (!r.value) return;
      _.value = !0;
      const u = Array.from(d.value);
      d.value = /* @__PURE__ */ new Set(), g.value = /* @__PURE__ */ new Set(), r.value.clearSelection(), _.value = !1, pe(u, []);
    }
    function X(u) {
      const m = t.checkAllExcludeTypes ?? [], b = [];
      for (const T of u)
        m.includes(T[t.nodeTypeField]) || b.push(T), T.children && T.children.length > 0 && b.push(...X(T.children));
      return b;
    }
    function te(u) {
      const m = [], b = (T) => {
        var M;
        for (const Q of T)
          m.push(Q), (M = Q.children) != null && M.length && b(Q.children);
      };
      return b(u.children ?? []), m;
    }
    function Z(u, m) {
      const b = [];
      for (const T of m) u.has(T) || b.push(T);
      return b;
    }
    function me(u, m, b) {
      const T = new Set(b), M = (ie, Fe) => {
        var Ve;
        const Te = String(ie[t.nodeKey]);
        Fe ? T.add(Te) : T.delete(Te), (Ve = r.value) == null || Ve.toggleRowSelection(ie, Fe);
      };
      _.value = !0, M(u, m);
      for (const ie of te(u)) M(ie, m);
      const Q = t.checkAllExcludeTypes ?? [];
      let A = u[t.parentKey];
      for (; A; ) {
        const ie = v.value.get(String(A));
        if (!ie) break;
        const Fe = ie.children.filter(
          (Te) => !Q.includes(String(Te[t.nodeTypeField]))
        );
        M(ie, Fe.length > 0 && Fe.every((Te) => T.has(String(Te[t.nodeKey])))), A = ie[t.parentKey];
      }
      _.value = !1;
      const O = Z(T, b), ye = Z(b, T), $e = new Set(d.value);
      for (const ie of ye) $e.add(ie);
      for (const ie of O) $e.delete(ie);
      d.value = $e, g.value = new Set(T), pe(O, ye);
    }
    function fe(u) {
      if (_.value) return;
      const m = new Set(u.map((A) => String(A[t.nodeKey]))), b = g.value, T = Z(b, m), M = Z(m, b);
      if (T.length === 0 && M.length === 0) return;
      if (t.cascade) {
        const O = T.length + M.length === 1 ? T[0] ?? M[0] : void 0, ye = O ? v.value.get(O) : void 0;
        if (ye) {
          me(ye, T.length > 0, b);
          return;
        }
      }
      g.value = m;
      const Q = new Set(d.value);
      for (const A of T) Q.add(A);
      for (const A of M) Q.delete(A);
      d.value = Q, T.length > 0 && pe([], T), M.length > 0 && pe(M, []);
    }
    function pe(u, m) {
      i("check-change", { added: m, removed: u });
    }
    function Ce() {
      return Array.from(d.value);
    }
    function re(u) {
      d.value = new Set(u), _e(() => {
        ee();
      });
    }
    function s() {
      const u = [];
      for (const m of d.value) {
        const b = v.value.get(m);
        b && u.push(b);
      }
      return u;
    }
    return o({
      getCheckedKeys: Ce,
      setCheckedKeys: re,
      getCheckedNodes: s,
      expandAll: S,
      collapseAll: L,
      checkAll: K,
      uncheckAll: Y
    }), (u, m) => {
      const b = N("el-button"), T = N("el-table-column"), M = N("el-tag"), Q = N("el-table");
      return p(), D("div", Ro, [
        R("div", Vo, [
          R("div", Po, [
            m[1] || (m[1] = V(" 已选择 ", -1)),
            R("strong", null, U(E.value), 1),
            m[2] || (m[2] = V(" 条记录 ", -1))
          ]),
          a.searchable ? (p(), D("div", Lo, [
            P(Ae(We), {
              modelValue: h.value,
              "onUpdate:modelValue": m[0] || (m[0] = (A) => h.value = A),
              placeholder: a.searchPlaceholder,
              clearable: "",
              size: "small",
              "prefix-icon": "Search"
            }, null, 8, ["modelValue", "placeholder"])
          ])) : j("", !0),
          R("div", Eo, [
            P(b, {
              size: "small",
              onClick: S
            }, {
              default: w(() => [...m[3] || (m[3] = [
                V("展开全部", -1)
              ])]),
              _: 1
            }),
            P(b, {
              size: "small",
              onClick: L
            }, {
              default: w(() => [...m[4] || (m[4] = [
                V("折叠全部", -1)
              ])]),
              _: 1
            }),
            P(b, {
              size: "small",
              onClick: K
            }, {
              default: w(() => [...m[5] || (m[5] = [
                V("全选", -1)
              ])]),
              _: 1
            }),
            P(b, {
              size: "small",
              onClick: Y
            }, {
              default: w(() => [...m[6] || (m[6] = [
                V("取消全选", -1)
              ])]),
              _: 1
            })
          ])
        ]),
        P(Q, {
          ref_key: "tableRef",
          ref: r,
          data: c.value,
          "row-key": a.nodeKey,
          "tree-props": { children: "children", checkStrictly: !0 },
          onSelectionChange: fe,
          "default-expand-all": a.defaultExpandAll,
          style: { width: "100%" },
          class: "yzh-tree-table-check-selector__table"
        }, {
          default: w(() => [
            P(T, {
              type: "selection",
              width: "50"
            }),
            (p(!0), D(oe, null, ue(a.columns, (A) => (p(), z(T, {
              key: A.prop,
              prop: A.prop,
              label: A.label,
              width: A.width,
              "min-width": A.minWidth,
              fixed: A.fixed,
              "show-overflow-tooltip": A.showOverflowTooltip !== !1
            }, {
              default: w(({ row: O }) => [
                W(u.$slots, `column-${A.prop}`, {
                  row: O,
                  column: A
                }, () => [
                  A.prop === a.nodeTypeField ? (p(), z(M, {
                    key: 0,
                    type: n(O[a.nodeTypeField]),
                    size: "small"
                  }, {
                    default: w(() => [
                      V(U(l(O[a.nodeTypeField])), 1)
                    ]),
                    _: 2
                  }, 1032, ["type"])) : (p(), D(oe, { key: 1 }, [
                    V(U(O[A.prop]), 1)
                  ], 64))
                ], !0)
              ]),
              _: 2
            }, 1032, ["prop", "label", "width", "min-width", "fixed", "show-overflow-tooltip"]))), 128))
          ]),
          _: 3
        }, 8, ["data", "row-key", "default-expand-all"])
      ]);
    };
  }
}), Ma = /* @__PURE__ */ he(Mo, [["__scopeId", "data-v-0aab416f"]]), Uo = { class: "yzh-empty-state__inner" }, Io = { class: "yzh-empty-state__title" }, Oo = {
  key: 2,
  class: "yzh-empty-state__description"
}, Ko = {
  key: 3,
  class: "yzh-empty-state__action"
}, Yo = /* @__PURE__ */ ne({
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
  setup(a) {
    return (o, e) => {
      const t = N("el-icon"), l = N("el-button");
      return p(), D("div", {
        class: ke(["yzh-empty-state", { "is-compact": a.compact, "is-icon-bg": a.iconBackgroundColor }])
      }, [
        R("div", Uo, [
          a.iconBackgroundColor ? (p(), D("div", {
            key: 0,
            class: "yzh-empty-state__icon-wrap",
            style: ze({ backgroundColor: a.iconBackgroundColor })
          }, [
            P(t, {
              class: "yzh-empty-state__icon",
              style: ze({ fontSize: a.iconSize + "px", color: a.iconColor })
            }, {
              default: w(() => [
                (p(), z(Ee(a.icon)))
              ]),
              _: 1
            }, 8, ["style"])
          ], 4)) : (p(), z(t, {
            key: 1,
            class: "yzh-empty-state__icon",
            style: ze({ fontSize: a.iconSize + "px", color: a.iconColor })
          }, {
            default: w(() => [
              (p(), z(Ee(a.icon)))
            ]),
            _: 1
          }, 8, ["style"])),
          R("div", Io, U(a.title), 1),
          a.description ? (p(), D("div", Oo, U(a.description), 1)) : j("", !0),
          a.actionLabel && a.onAction ? (p(), D("div", Ko, [
            W(o.$slots, "action", {}, () => [
              P(l, {
                size: "small",
                onClick: a.onAction
              }, {
                default: w(() => [
                  V(U(a.actionLabel), 1)
                ]),
                _: 1
              }, 8, ["onClick"])
            ], !0)
          ])) : j("", !0)
        ])
      ], 2);
    };
  }
}), Ua = /* @__PURE__ */ he(Yo, [["__scopeId", "data-v-33c080f4"]]), jo = /* @__PURE__ */ ne({
  __name: "YzhStatusBadge",
  props: {
    type: { type: String, default: "info" },
    // success | warning | danger | info
    text: { type: String, default: "" },
    icon: { type: Object, default: null },
    size: { type: String, default: "small" }
    // small | default
  },
  setup(a) {
    const o = a, e = G(() => ({
      success: null,
      // 后续引入图标
      warning: null,
      danger: null,
      info: null
    })[o.type] || null);
    return (t, l) => {
      const n = N("el-icon");
      return p(), D("span", {
        class: ke(["yzh-status-badge", [`is-${a.type}`, `is-${a.size}`]])
      }, [
        a.icon || e.value ? (p(), z(n, {
          key: 0,
          class: "yzh-status-badge__icon"
        }, {
          default: w(() => [
            (p(), z(Ee(a.icon || e.value)))
          ]),
          _: 1
        })) : j("", !0),
        W(t.$slots, "default", {}, () => [
          V(U(a.text), 1)
        ], !0)
      ], 2);
    };
  }
}), Ia = /* @__PURE__ */ he(jo, [["__scopeId", "data-v-d7402330"]]), Wo = { class: "yzh-card" }, qo = {
  key: 0,
  class: "yzh-card__header"
}, Go = { class: "yzh-card__body" }, Ho = {
  key: 1,
  class: "yzh-card__footer"
}, Xo = /* @__PURE__ */ ne({
  __name: "YzhCard",
  props: {
    title: { type: String, default: "" }
  },
  setup(a) {
    return (o, e) => (p(), D("div", Wo, [
      o.$slots.header || a.title ? (p(), D("div", qo, [
        W(o.$slots, "header", {}, () => [
          V(U(a.title), 1)
        ], !0)
      ])) : j("", !0),
      R("div", Go, [
        W(o.$slots, "default", {}, void 0, !0)
      ]),
      o.$slots.footer ? (p(), D("div", Ho, [
        W(o.$slots, "footer", {}, void 0, !0)
      ])) : j("", !0)
    ]));
  }
}), Oa = /* @__PURE__ */ he(Xo, [["__scopeId", "data-v-245f071f"]]);
function Jo(a) {
  return {
    TextBox: "text",
    TextArea: "textarea",
    NumberBox: "number",
    Decimal: "number",
    DatePicker: "date",
    DateTimePicker: "datetime",
    ComboBox: "select",
    DropDownList: "select",
    RadioButtonList: "radio",
    CheckBox: "checkbox",
    Switch: "switch",
    Upload: "upload",
    TreeSelect: "treeSelect",
    Cascader: "cascader",
    PasswordBox: "password",
    Memo: "textarea"
  }[a] || "text";
}
function Zo(a) {
  return {
    NumberBox: "number",
    DatePicker: "date",
    DateTimePicker: "dateRange",
    ComboBox: "select",
    DropDownList: "select",
    RadioButtonList: "select"
  }[a] || "text";
}
function Qo(a) {
  return {
    input: "text",
    select: "select",
    date: "date",
    cascader: "cascader"
  }[a] || "text";
}
function ea(a) {
  const o = a == null ? void 0 : a.Columns;
  if (!o) return [];
  const e = a == null ? void 0 : a.EnableField;
  return o.filter((t) => t.XsFlag).map((t) => {
    const l = {
      prop: t.FieldName,
      label: t.DesName,
      width: Number(t.Width) || void 0,
      sortable: t.Sortable || void 0,
      fixed: t.Fixed || void 0,
      align: t.Align || void 0,
      dictCode: t.DictCode || void 0
    };
    return t.Type === "CustomSlot" && (l.slot = t.FieldName), e && t.FieldName === e && (l.slot = t.FieldName), l;
  });
}
function qe(a) {
  var t;
  const o = a == null ? void 0 : a.FormCols;
  return o && o > 0 ? o : (((t = a == null ? void 0 : a.Columns) == null ? void 0 : t.filter((l) => l.BcFlag).length) ?? 0) <= 10 ? 1 : 2;
}
function ta(a, o = "0", e) {
  const t = a == null ? void 0 : a.Columns, l = a == null ? void 0 : a.Schema;
  if (!t) return [];
  const n = qe(a), i = Math.floor(24 / n), r = (e == null ? void 0 : e.withDefaults) ?? !1;
  return t.filter((c) => c.BcFlag && c.Type !== "Other").map((c) => {
    var g;
    const h = c.FieldName, d = aa(h), v = l == null ? void 0 : l[d], C = c.GroupIndex || "0", _ = o !== "0" && C !== o;
    return {
      prop: h,
      label: c.DesName,
      type: Jo(c.Type),
      required: !c.Yxk,
      disabled: c.Enable === !1 || _,
      span: i,
      dictCode: c.DictCode || void 0,
      options: void 0,
      placeholder: (g = c.Type) != null && g.includes("Picker") ? `请选择${c.DesName}` : `请输入${c.DesName}`,
      defaultValue: r ? c.Mrz ? c.Type === "Switch" ? Number(c.Mrz) : c.Mrz : v == null ? void 0 : v.Default : void 0,
      fieldSchema: v
    };
  });
}
function Ze(a) {
  const o = a == null ? void 0 : a.SearchFields;
  if (o && o.length > 0)
    return o.map((l) => ({
      prop: l.Field,
      label: l.Label,
      type: Qo(l.ControlType),
      placeholder: `请输入${l.Label}`,
      options: l.Options ?? void 0
    }));
  const e = a == null ? void 0 : a.Columns;
  if (!e) return [];
  const t = ["Upload", "TreeSelect", "Cascader", "CheckBox"];
  return e.filter((l) => l.XsFlag && l.Type !== "Other" && !t.includes(l.Type) && l.BcFlag).slice(0, 4).map((l) => ({
    prop: l.FieldName,
    label: l.DesName,
    type: Zo(l.Type),
    placeholder: `请输入${l.DesName}`
  }));
}
function oa(a) {
  const o = a == null ? void 0 : a.Toolbar;
  if (!o) return [];
  const e = [];
  if (o.Add !== !1 && e.push({ key: "add", text: "新增", type: "primary" }), o.Delete !== !1 && e.push({ key: "delete", text: "批量删除", type: "danger" }), o.Export !== !1 && e.push({ key: "export", text: "导出", type: "success" }), o.Import !== !1 && e.push({ key: "import", text: "导入", type: "warning" }), o.CustomButtons)
    for (const [t, l] of Object.entries(o.CustomButtons))
      e.push({ key: `custom:${l}`, text: t, type: "info" });
  return e;
}
function tt(a, o) {
  const e = (a == null ? void 0 : a.RowButtons) ?? {}, t = [];
  if (e.Edit !== !1 && t.push({ key: "edit", text: "编辑", type: "primary" }), e.Delete !== !1 && t.push({ key: "delete", text: "删除", type: "danger" }), e.Enable === !0 && o && t.push({ key: "toggle-valid", text: "禁用/启用", type: "warning" }), e.CustomButtons)
    for (const [l, n] of Object.entries(e.CustomButtons))
      t.push({ key: `custom:${n}`, text: l, type: "info" });
  return t;
}
function Ka(a, o) {
  const e = {};
  for (const t of tt(a, o)) e[t.key] = t.text;
  return e;
}
function Ya(a, o, e) {
  const t = [], l = a;
  if (!l) return t;
  if (l.AllowEdit && ((e == null ? void 0 : e.allowAddChild) !== !1 && t.push({ key: "add-child", text: "新增下级" }), t.push({ key: "edit", text: "编辑" })), l.AllowDelete && t.push({ key: "delete", text: "删除", type: "danger", danger: !0 }), o && t.push({ key: "toggle-valid", text: "禁用/启用", type: "warning" }), l.CustomActions)
    for (const [n, i] of Object.entries(l.CustomActions))
      t.push({ key: `custom:${n}`, text: i, type: "info" });
  return t;
}
function ja(a, o) {
  var t;
  const e = ((t = o == null ? void 0 : o.Extra) == null ? void 0 : t.level) ?? (o == null ? void 0 : o.level) ?? -1;
  return {
    ...a,
    Extra: { ...a.Extra, level: e + 1 },
    Children: []
  };
}
function aa(a) {
  return !a || a[0] >= "a" && a[0] <= "z" ? a : a[0].toLowerCase() + a.slice(1);
}
const Oe = {}, Ke = "YZH_TOKEN", ve = {
  get: () => localStorage.getItem(Ke),
  set: (a) => localStorage.setItem(Ke, a),
  clear: () => localStorage.removeItem(Ke)
};
class ot {
  constructor(o) {
    /** 服务根地址（文件下载等场景需要读取） */
    F(this, "baseURL");
    F(this, "getToken");
    F(this, "onUnauthorized");
    F(this, "onError");
    this.baseURL = o.baseURL.replace(/\/$/, ""), this.getToken = o.getToken || (() => ve.get()), this.onUnauthorized = o.onUnauthorized, this.onError = o.onError;
  }
  /**
   * 通用请求方法
   * 原样透传：返回后端 JSON，不做 key 转换
   */
  async request(o, e = {}) {
    var v, C;
    const {
      method: t = "POST",
      params: l,
      body: n,
      headers: i = {},
      requireAuth: r = !0,
      raw: c = !1
    } = e;
    let h = o;
    const d = {
      method: t,
      headers: {
        "Content-Type": "application/json",
        ...i
      }
    };
    if (r !== !1) {
      const _ = this.getToken();
      _ && (d.headers.Authorization = `Bearer ${_}`);
    }
    if (l) {
      let _ = l;
      const g = Object.keys(l), E = l.params;
      g.length === 1 && g[0] === "params" && E && typeof E == "object" && (console.warn(
        "[YzhApi] 查询参数多包了一层 params（应为 get(url, { a, b }) 而非 get(url, { params: { a, b } })），已自动解包：",
        E
      ), _ = E);
      const f = new URLSearchParams();
      Object.entries(_).forEach(([q, ee]) => {
        ee != null && f.append(q, String(ee));
      });
      const x = f.toString();
      x && (h += (o.includes("?") ? "&" : "?") + x);
    }
    n !== void 0 ? d.body = JSON.stringify(n) : t !== "GET" && !l && (d.body = "{}");
    try {
      const _ = await fetch(this.baseURL + h, d);
      if (_.status === 401)
        throw ve.clear(), (v = this.onUnauthorized) == null || v.call(this), new Error("登录已过期，请重新登录");
      const g = await _.json();
      if (!_.ok) {
        const E = (g == null ? void 0 : g.message) || (g == null ? void 0 : g.msg) || `请求失败 (${_.status})`, f = new Error(E);
        throw f.status = _.status, f.data = g, f;
      }
      return g;
    } catch (_) {
      throw (C = this.onError) == null || C.call(this, _), _;
    }
  }
  get(o, e, t) {
    return this.request(o, { ...t, method: "GET", params: e });
  }
  post(o, e, t) {
    return this.request(o, { ...t, method: "POST", body: e });
  }
  put(o, e, t) {
    return this.request(o, { ...t, method: "PUT", body: e });
  }
  delete(o, e) {
    return this.request(o, { ...e, method: "DELETE" });
  }
  /**
   * GET 二进制内容（带鉴权）——用于预览场景
   *
   * 背景：`<iframe src>` / `<img src>` 无法携带 Authorization 头（本平台 JWT 走 Header），
   * 直接渲染受保护的文件流必然 401。必须先带 Token 取回 Blob，再用 ObjectURL 渲染。
   *
   * @param url    相对路径
   * @param params 查询参数（追加到 URL）
   * @returns      Blob（MIME 取自响应头，缺失时调用方按魔数兜底）
   * @throws       401 / 业务错误：抛出带 status 的 Error（错误信息优先取后端 JSON 的 message）
   */
  async getBlob(o, e) {
    var r;
    const t = this.getToken();
    let l = o;
    if (e) {
      const c = new URLSearchParams();
      Object.entries(e).forEach(([d, v]) => {
        v != null && c.append(d, String(v));
      });
      const h = c.toString();
      h && (l += (o.includes("?") ? "&" : "?") + h);
    }
    const n = await fetch(this.baseURL + l, {
      method: "GET",
      headers: {
        ...t ? { Authorization: `Bearer ${t}` } : {}
      }
    });
    if (n.status === 401)
      throw ve.clear(), (r = this.onUnauthorized) == null || r.call(this), new Error("登录已过期，请重新登录");
    if ((n.headers.get("content-type") || "").includes("application/json")) {
      const c = await n.json().catch(() => ({})), h = new Error((c == null ? void 0 : c.message) || (c == null ? void 0 : c.msg) || `请求失败 (${n.status})`);
      throw h.status = n.status, h;
    }
    if (!n.ok) {
      const c = new Error(`请求失败 (${n.status})`);
      throw c.status = n.status, c;
    }
    return await n.blob();
  }
  /**
   * POST 下载文件（导出）
   */
  async download(o, e, t) {
    var r;
    const l = this.getToken(), n = await fetch(this.baseURL + o, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...l ? { Authorization: `Bearer ${l}` } : {}
      },
      body: JSON.stringify(e)
    });
    if (n.status === 401)
      throw ve.clear(), (r = this.onUnauthorized) == null || r.call(this), new Error("登录已过期，请重新登录");
    if (!n.ok) {
      const c = await n.json().catch(() => ({}));
      throw new Error(c.message || c.msg || "下载失败");
    }
    const i = await n.blob();
    this.triggerDownload(i, t);
  }
  /**
   * GET 下载文件（模板下载）
   */
  async downloadGet(o, e) {
    var i;
    const t = this.getToken(), l = await fetch(this.baseURL + o, {
      method: "GET",
      headers: {
        ...t ? { Authorization: `Bearer ${t}` } : {}
      }
    });
    if (l.status === 401)
      throw ve.clear(), (i = this.onUnauthorized) == null || i.call(this), new Error("登录已过期，请重新登录");
    if (!l.ok) {
      const r = await l.json().catch(() => ({}));
      throw new Error(r.message || r.msg || "下载失败");
    }
    const n = await l.blob();
    this.triggerDownload(n, e);
  }
  /**
   * 上传文件（导入）
   */
  async upload(o, e) {
    var n;
    const t = this.getToken(), l = await fetch(this.baseURL + o, {
      method: "POST",
      headers: {
        ...t ? { Authorization: `Bearer ${t}` } : {}
      },
      body: e
    });
    if (l.status === 401)
      throw ve.clear(), (n = this.onUnauthorized) == null || n.call(this), new Error("登录已过期，请重新登录");
    return await l.json();
  }
  /**
   * 触发浏览器下载
   */
  triggerDownload(o, e) {
    const t = URL.createObjectURL(o), l = document.createElement("a");
    l.href = t, l.download = e, document.body.appendChild(l), l.click(), document.body.removeChild(l), URL.revokeObjectURL(t);
  }
}
const Ne = new ot({
  baseURL: (Oe == null ? void 0 : Oe.VITE_API_BASE) || "http://127.0.0.1:9992",
  onUnauthorized: () => {
    console.warn("[YzhApi] 401 未授权，请重新登录");
  }
}), Be = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  YzhApiClient: ot,
  tokenStore: ve,
  yzhApi: Ne
}, Symbol.toStringTag, { value: "Module" })), De = "/api/file-storage";
function Wa(a, o) {
  const e = new FormData();
  return e.append("file", a), Ne.post(`${De}/upload`, e, {
    params: o,
    headers: { "Content-Type": "multipart/form-data" }
  });
}
function qa(a, o) {
  const e = new FormData();
  return a.forEach((t) => e.append("files", t)), Ne.post(`${De}/upload-batch`, e, {
    params: o,
    headers: { "Content-Type": "multipart/form-data" }
  });
}
function Ga(a) {
  const o = Ne.baseURL || "", e = localStorage.getItem("token") || "";
  return `${o}${De}/download?path=${encodeURIComponent(a)}&token=${e}`;
}
function Ha(a) {
  return Ne.post(`${De}/delete`, null, { params: { path: a } });
}
function Xa(a) {
  return Ne.get(`${De}/exists`, { path: a });
}
function Ja(a) {
  return Ne.get(`${De}/list`, { prefix: a });
}
function Za() {
  const a = k(ve.get() || ""), o = k(null), e = G(() => !!a.value);
  function t(r) {
    a.value = r, ve.set(r);
  }
  function l() {
    a.value = "", o.value = null, ve.clear();
  }
  function n(r, c) {
    return Promise.resolve();
  }
  function i() {
    l();
  }
  return {
    token: a,
    userInfo: o,
    isAuthenticated: e,
    setToken: t,
    clearToken: l,
    login: n,
    logout: i
  };
}
function Qa() {
  const a = k(!1), o = k([]), e = k(0), t = k(1), l = k(20), n = be({});
  async function i(h) {
    a.value = !0;
    try {
      const d = {
        page: t.value,
        rows: l.value,
        ...n
      }, v = await h(d);
      o.value = v.rows || [], e.value = v.total || 0;
    } finally {
      a.value = !1;
    }
  }
  function r(h) {
    Object.assign(n, h), t.value = 1;
  }
  function c() {
    Object.keys(n).forEach((h) => delete n[h]), t.value = 1;
  }
  return {
    loading: a,
    rows: o,
    total: e,
    page: t,
    pageSize: l,
    searchParams: n,
    loadData: i,
    setSearchParams: r,
    resetSearchParams: c
  };
}
function el() {
  async function a(o) {
    try {
      return await xe.confirm(o.message, o.title ?? "操作确认", {
        type: o.type ?? "warning",
        confirmButtonText: o.confirmButtonText ?? "确定",
        cancelButtonText: o.cancelButtonText ?? "取消"
      }), !0;
    } catch {
      return !1;
    }
  }
  return { confirm: a };
}
function tl(a, ...o) {
  const e = new a(...o), t = k(null);
  return Re(async () => {
    await e.init(), await _e(), e.setTableRef(t.value);
  }), { logic: e, tableRef: t };
}
function ol(a, ...o) {
  const e = new a(...o), t = k(null), l = k(null);
  return Re(async () => {
    await e.init(), await _e(), e.setTableRef(t.value), e.setTreeTableRef(l.value);
  }), { logic: e, tableRef: t, treeTableRef: l };
}
function al(a, ...o) {
  const e = new a(...o);
  return Re(async () => {
    await e.init();
  }), { logic: e };
}
function ll(a, ...o) {
  const e = new a(...o);
  return Re(async () => {
    await e.init();
  }), { logic: e };
}
function at(a) {
  return !a || a[0] >= "a" && a[0] <= "z" ? a : a[0].toLowerCase() + a.slice(1);
}
function la(a) {
  return !a || a[0] >= "A" && a[0] <= "Z" ? a : a[0].toUpperCase() + a.slice(1);
}
function Ye(a) {
  const o = {};
  for (const [e, t] of Object.entries(a))
    o[la(e)] = t;
  return o;
}
function nl(a) {
  const o = {};
  for (const [e, t] of Object.entries(a))
    o[at(e)] = t;
  return o;
}
class na {
  constructor() {
    // ──── 后端配置 ────
    /** 后端页面配置（工具栏+表格+表单+搜索栏） */
    F(this, "config", k(null));
    // ──── 表格状态 ────
    /** 表格数据行（PascalCase 字段） */
    F(this, "rows", k([]));
    /** 表格加载状态 */
    F(this, "loading", k(!1));
    /** 选中行集合 */
    F(this, "selectedRows", k([]));
    // ──── 分页状态 ────
    /** 分页参数 */
    F(this, "pagination", be({ page: 1, pageSize: 20, total: 0 }));
    // ──── 搜索过滤状态 ────
    /** 搜索参数（PascalCase key，与业务实体字段名一致） */
    F(this, "searchParams", be({}));
    // ──── 排序状态 ────
    /** 排序字段（PascalCase） */
    F(this, "sortField", k());
    /** 排序方向 */
    F(this, "sortOrder", k());
    // ──── 弹窗状态 ────
    /** 弹窗可见性 */
    F(this, "dialogVisible", k(!1));
    /** 弹窗模式 */
    F(this, "dialogMode", k("add"));
    /**
     * 表单编辑模式（GroupIndex 控制）
     *
     * - '0'：新增/编辑模式，GroupIndex="0" 的字段可编辑
     * - '99'：详情模式，仅 GroupIndex="99" 的字段可编辑（JSON 通常不配 → 全部只读）
     */
    F(this, "formGroupIndex", k("0"));
    /** 提交中状态 */
    F(this, "submitting", k(!1));
    // ──── ShowDisabled 开关（基类统一管理，子类无需手动实现） ────
    /** 显示已禁用记录开关 */
    F(this, "showDisabled", k(!1));
    /**
     * 表单数据：PascalCase key（与 formFields[].prop、NewEntity、实体属性名一致）
     * 例：{ Code: "", UserName: "", Enable: 1 }
     */
    F(this, "formData", be({}));
    // ──── 表格引用（局部刷新） ────
    F(this, "_tableRef", null);
    // ========================================================
    // 动作统一（ST-7 dispatch + registerHandler）
    // ========================================================
    F(this, "handlers", /* @__PURE__ */ new Map());
    /** 行动作入口（绑定 @row-action="logic.onRowAction"；箭头属性自动绑定 this，模板引用式传参不丢上下文） */
    F(this, "onRowAction", async (o, e, t) => {
      await this.dispatch(o, e, t);
    });
    /** 工具栏动作入口（绑定 @toolbar-action="logic.onToolbarAction"） */
    F(this, "onToolbarAction", async (o, e) => {
      await this.dispatch(o, void 0, e);
    });
    /** @deprecated 兼容旧命名，等价 onToolbarAction */
    F(this, "onToolbarClick", async (o) => {
      await this.dispatch(o);
    });
    /** @deprecated 兼容旧命名，等价 onRowAction */
    F(this, "onRowClick", async (o, e) => {
      await this.dispatch(o, e);
    });
    // ========================================================
    // 事件处理（表格原生事件；箭头属性自动绑定 this，供模板引用式绑定）
    // ========================================================
    F(this, "onSearch", async (o) => {
      this.resetObject(this.searchParams), Object.assign(this.searchParams, o), this.pagination.page = 1, await this.loadPage();
    });
    F(this, "onPageChange", async (o) => {
      this.pagination.page = o, await this.loadPage();
    });
    F(this, "onSizeChange", async (o) => {
      this.pagination.pageSize = o, this.pagination.page = 1, await this.loadPage();
    });
    F(this, "onSortChange", async (o, e) => {
      this.sortField.value = o, this.sortOrder.value = e, await this.loadPage();
    });
    F(this, "onSelectionChange", (o) => {
      this.selectedRows.value = o;
    });
    /** 当前编辑行（ST-8：提交后与后端返回合并，避免表格行丢字段） */
    F(this, "editingRow", k(null));
  }
  /** 切换 ShowDisabled 并刷新表格 */
  async toggleShowDisabled() {
    this.showDisabled.value = !this.showDisabled.value, await this.refresh();
  }
  /** 设置表格引用（模板中调用，或由 useSingleTable 注入） */
  setTableRef(o) {
    this._tableRef = o;
  }
  /** 刷新表格数据（触发 dataLoader 重新加载） */
  async refresh() {
    var o;
    await ((o = this._tableRef) == null ? void 0 : o.refresh());
  }
  // ========================================================
  // Computed: 从 config 派生 UI 结构（经 adapters/，业务字段不出内核）
  // ========================================================
  /** 表格列配置（AD-1） */
  get columns() {
    return ea(this.config.value);
  }
  /** 表单布局列数（从后端 EntityConfig.FormCols 读取，0=自动） */
  get formLayoutCols() {
    return qe(this.config.value);
  }
  /** 表单字段配置（AD-2） */
  get formFields() {
    return ta(this.config.value, this.formGroupIndex.value);
  }
  /** 搜索栏字段（config.SearchFields 优先；为空时走 fallbackSearchFields 钩子再走列推导） */
  get searchFields() {
    var t;
    const o = (t = this.config.value) == null ? void 0 : t.SearchFields;
    if (o && o.length > 0)
      return Ze(this.config.value);
    const e = this.fallbackSearchFields;
    return e.length > 0 ? e : Ze(this.config.value);
  }
  /**
   * 后端 SearchFields 缺失时的业务兜底（如 treepconfig 未映射历史的场景）
   * 子类可覆盖；默认空（走列推导）
   */
  get fallbackSearchFields() {
    return [];
  }
  /** 工具栏按钮（YzhAction[]，声明式，绑定 :toolbar-actions + @toolbar-action） */
  get toolbarActions() {
    return oa(this.config.value);
  }
  /** @deprecated 兼容旧形状（对象数组），等价 toolbarActions 的字段子集 */
  get toolbarButtons() {
    return this.toolbarActions.map((o) => ({
      key: o.key,
      text: o.text,
      type: o.type ?? "primary"
    }));
  }
  /**
   * 行操作按钮（YzhAction[] 或按行解析函数 —— 主形状，绑定 :row-action-buttons）
   *
   * 子类可覆盖为函数式：(row) => YzhAction[]（按行状态动态显隐/禁用）
   */
  get rowActions() {
    return tt(this.config.value, this.enableField);
  }
  /** 行操作按钮字典（兼容旧 Record 消费方，由 rowActions 派生） */
  get rowActionButtons() {
    const o = typeof this.rowActions == "function" ? this.rowActions({}) : this.rowActions, e = {};
    for (const t of o) e[t.key] = t.text;
    return e;
  }
  /** @deprecated 兼容旧形状（数组），由 rowActions 派生 */
  get rowButtons() {
    return (typeof this.rowActions == "function" ? this.rowActions({}) : this.rowActions).map((e) => ({ key: e.key, text: e.text, type: e.type ?? "primary" }));
  }
  /** 启用/禁用字段名（从 EntityConfig.EnableField 读取，null 表示不支持启用/禁用） */
  get enableField() {
    var o;
    return ((o = this.config.value) == null ? void 0 : o.EnableField) || null;
  }
  /** 主键字段名（PascalCase），统一使用 Code */
  get primaryKey() {
    return "Code";
  }
  // ========================================================
  // 覆盖点（ST-3/ST-4/ST-9）
  // ========================================================
  /** 新增默认值（合并到 NewEntity 之后；PascalCase key） */
  get defaultValues() {
    return {};
  }
  /** 确认弹窗中显示的实体名称字段（默认 Name；子类覆盖如 'RoleName'） */
  get entityNameField() {
    return "Name";
  }
  /** 读取行显示名称（需要拼接多个字段的页面覆盖此方法） */
  entityName(o) {
    const e = o == null ? void 0 : o[this.entityNameField];
    return e == null ? "" : String(e);
  }
  /** 提交前归一化钩子（如 Decimal 字符串→数值） */
  normalizeBeforeSubmit(o) {
    return o;
  }
  /** 数据加载后处理钩子（如编码→名称翻译） */
  postprocessRows(o) {
    return o;
  }
  // ========================================================
  // 初始化（ST-5）
  // ========================================================
  /** 初始化页面：加载配置 → onAfterInit（表格数据由 YzhTable dataLoader 自行加载） */
  async init() {
    await this.loadConfig(), await this.onAfterInit();
  }
  /** 加载页面配置（/api/{controller}/config） */
  async loadConfig() {
    const o = await this.apiGet("/config");
    this.config.value = o.data;
  }
  /** 配置加载完成后的钩子（子类在此做额外初始化，不再覆盖 init） */
  async onAfterInit() {
  }
  // ========================================================
  // 数据查询（/filter API）
  // ========================================================
  /** 分页查询（/filter API） */
  async loadPage() {
    this.loading.value = !0;
    try {
      const o = {
        Page: this.pagination.page,
        PageSize: this.pagination.pageSize,
        SortField: this.sortField.value,
        SortOrder: this.sortOrder.value,
        Filters: this.buildFilters()
      }, t = (await this.apiPost("/filter", o)).data;
      t && (this.rows.value = this.postprocessRows(t.Items ?? []), this.pagination.total = t.TotalCount ?? 0, this.onDataLoaded(this.rows.value));
    } catch {
      this.rows.value = [], this.pagination.total = 0;
    } finally {
      this.loading.value = !1;
    }
  }
  /**
   * YzhTable 数据加载器（页面直接绑定：`:data-loader="logic.dataLoader.bind(logic)"`）
   *
   * 入参由 YzhTable 传入：{ page, rows, sort, order, ...搜索条件 }
   * 搜索条件的 key = EntityConfig.SearchFields[].Field（PascalCase）
   * Operator 取自 SearchFields 配置（未配置时默认 eq）
   */
  async dataLoader(o) {
    const {
      page: e = 1,
      rows: t = this.pagination.pageSize,
      sort: l,
      order: n,
      ...i
    } = o;
    this.loading.value = !0;
    try {
      const r = {
        Page: e,
        PageSize: t,
        SortField: l,
        SortOrder: n,
        Filters: this.buildFilters(i)
      }, c = await this.apiPost("/filter", r), h = c == null ? void 0 : c.data, d = this.postprocessRows(((h == null ? void 0 : h.Items) ?? []).slice());
      return this.pagination.page = e, this.pagination.pageSize = t, this.pagination.total = (h == null ? void 0 : h.TotalCount) ?? 0, this.rows.value = d, this.onDataLoaded(d), { rows: d, total: this.pagination.total };
    } finally {
      this.loading.value = !1;
    }
  }
  /** 构建过滤条件（从 searchParams + 额外条件 + 自动 ShowDisabled） */
  buildFilters(o) {
    var n, i;
    const e = { ...this.searchParams, ...o || {} }, t = /* @__PURE__ */ new Map();
    if ((n = this.config.value) != null && n.SearchFields)
      for (const r of this.config.value.SearchFields)
        r.Operator && t.set(r.Field, r.Operator);
    const l = Object.entries(e).filter(
      ([, r]) => r != null && r !== "" && !(Array.isArray(r) && r.length === 0)
    ).map(([r, c]) => ({
      Field: r,
      Value: Array.isArray(c) ? c.join(",") : String(c),
      Operator: t.get(r) || "eq"
    }));
    return (i = this.config.value) != null && i.EnableField && this.showDisabled.value && l.push({ Field: "ShowDisabled", Value: "true", Operator: "eq" }), l;
  }
  // ========================================================
  // 写入操作
  // ========================================================
  /** 新增实体（/api/{controller}/add） */
  async add(o) {
    return (await this.apiPost("/add", o)).data;
  }
  /** 修改实体（/api/{controller}/update） */
  async update(o) {
    return (await this.apiPost("/update", o)).data;
  }
  /** 批量删除（/api/{controller}/delete） */
  async delete(o) {
    await this.apiPost("/delete", o);
  }
  /** 行操作（/api/{controller}/action/{methodName}） */
  async executeAction(o, e) {
    await this.apiPost(`/action/${o}`, e), await this.loadPage();
  }
  /**
   * 切换有效标志（IsValid: 0 ↔ 1）
   */
  async toggleIsValid(o) {
    const e = await this.apiPost(
      "/toggle-valid",
      { Code: o }
    );
    return e.success ? (J.success(e.data.IsValid === 1 ? "已启用" : "已禁用"), e.data) : null;
  }
  /**
   * 切换行有效标志（完整流程：确认弹窗 → API → 本地更新）
   */
  async toggleRowIsValidWithConfirm(o, e) {
    const t = (e == null ? void 0 : e.field) ?? this.enableField ?? "IsValid", n = (o[t] ?? 1) === 1 ? "禁用" : "启用", i = (e == null ? void 0 : e.entityName) ?? this.entityName(o);
    await xe.confirm(
      i ? `确定${n}【${i}】？` : `确定${n}该记录？`,
      `${n}确认`,
      {
        type: "warning",
        confirmButtonText: `确定${n}`,
        cancelButtonText: "取消"
      }
    );
    const r = await this.toggleIsValid(o.Code);
    r && this.replaceRowByCode(o.Code, { ...o, [t]: r.IsValid });
  }
  // ========================================================
  // 导出导入
  // ========================================================
  /** 导出 */
  async exportData(o = "excel", e) {
    const t = {
      Filters: this.buildFilters(),
      format: o,
      fields: e
    };
    await this.apiPostAndDownload("/export", t, `export_${Date.now()}.${o}`);
  }
  /** 导入 */
  async importData(o) {
    const e = new FormData();
    return e.append("file", o), (await this.apiUpload("/import", e)).data;
  }
  /** 下载导入模板 */
  async downloadImportTemplate() {
    await this.apiGetAndDownload("/import/template", "import_template.xlsx");
  }
  // ========================================================
  // Split 数据方法（增量更新，不重新请求）
  // ========================================================
  /** 删除行（按主键 Code） */
  removeRowByCode(o) {
    if (this._tableRef)
      this._tableRef.removeRow((e) => String(e.Code) === String(o));
    else {
      const e = this.rows.value.findIndex((t) => t.Code === o);
      e >= 0 && (this.rows.value.splice(e, 1), this.pagination.total = Math.max(0, this.pagination.total - 1));
    }
  }
  /** 替换行（按主键 Code） */
  replaceRowByCode(o, e) {
    if (this._tableRef)
      this._tableRef.replaceRow((t) => String(t.Code) === String(o), e);
    else {
      const t = this.rows.value.findIndex((l) => l.Code === o);
      t >= 0 && this.rows.value.splice(t, 1, e);
    }
  }
  /** 插入行 */
  insertRow(o, e = "top") {
    this._tableRef ? this._tableRef.insertRow(o, e) : (e === "top" ? this.rows.value.unshift(o) : this.rows.value.push(o), this.pagination.total++);
  }
  // ========================================================
  // 钩子方法（子类可覆盖；与后端 OnBeforeAdd/OnAfterAdd/… 对齐）
  // ========================================================
  onDataLoaded(o) {
  }
  onBeforeAdd(o) {
  }
  onAfterAdd(o) {
  }
  onBeforeUpdate(o) {
  }
  onAfterUpdate(o) {
  }
  onDelete(o) {
    return !0;
  }
  onAfterDelete(o) {
  }
  onPrepareAdd(o) {
  }
  /** 注册自定义动作处理器（覆盖内置同名动作） */
  registerHandler(o, e) {
    this.handlers.set(o, e);
  }
  /**
   * 动作统一入口：行按钮 / 工具栏按钮 / 树节点动作都汇聚到这里。
   *
   * 内置分支：add / edit / delete / toggle-valid / export / import / batch-delete / custom:{method}
   */
  async dispatch(o, e, t) {
    const l = this.handlers.get(o);
    if (l) {
      await l(e, t);
      return;
    }
    switch (o) {
      case "add":
        this.openAddDialog();
        return;
      case "edit":
        e && this.openEditDialog(e);
        return;
      case "detail":
        e && this.openDetailDialog(e);
        return;
      case "delete":
        await this.confirmDelete(e ? [e] : void 0);
        return;
      case "batch-delete":
        await this.confirmDelete();
        return;
      case "toggle-valid":
        e && await this.toggleRowIsValidWithConfirm(e);
        return;
      case "export":
        await this.exportData();
        return;
      case "import":
        return;
      default:
        if (o.startsWith("custom:")) {
          const n = o.slice(7);
          e ? await this.executeAction(n, e) : await this.executeCustomToolbarAction(n);
        }
    }
  }
  // ========================================================
  // 弹窗操作
  // ========================================================
  openAddDialog() {
    this.dialogMode.value = "add", this.formGroupIndex.value = "0", this.initFormData(), this.onPrepareAdd(this.formData), this.dialogVisible.value = !0;
  }
  openEditDialog(o) {
    this.dialogMode.value = "edit", this.formGroupIndex.value = "0", this.editingRow.value = o, this.initFormData(o), this.dialogVisible.value = !0;
  }
  /** 打开详情弹窗（只读模式，formGroupIndex='99' → 所有字段只读） */
  openDetailDialog(o) {
    this.dialogMode.value = "detail", this.formGroupIndex.value = "99", this.editingRow.value = o, this.initFormData(o), this.dialogVisible.value = !0;
  }
  /**
   * 打开指定编辑模式的弹窗
   * @param row 行数据（null=新增）
   * @param groupIndex 编辑模式：'0'=全部可编辑, '1'=仅 GroupIndex=1 字段可编辑, '99'=全部只读
   */
  openDialogWithMode(o, e) {
    this.formGroupIndex.value = e, o ? (this.dialogMode.value = e === "99" ? "detail" : "edit", this.editingRow.value = o, this.initFormData(o)) : (this.dialogMode.value = "add", this.initFormData(), this.onPrepareAdd(this.formData)), this.dialogVisible.value = !0;
  }
  /**
   * 初始化表单数据（ST-3：NewEntity → defaultValues / 编辑行）
   */
  initFormData(o) {
    var l;
    const t = { ...((l = this.config.value) == null ? void 0 : l.NewEntity) || {} };
    o ? Object.assign(t, o) : Object.assign(t, this.defaultValues), this.resetObject(this.formData), Object.assign(this.formData, t);
  }
  async cancelDialog() {
    this.dialogVisible.value = !1;
  }
  async submitForm() {
    this.submitting.value = !0;
    try {
      if (this.dialogMode.value === "add") {
        this.onBeforeAdd(this.formData);
        const o = this.normalizeBeforeSubmit({ ...this.formData }), e = await this.add(o);
        this.onAfterAdd(this.formData), this.insertRow(e);
      } else {
        this.onBeforeUpdate(this.formData);
        const o = this.normalizeBeforeSubmit({ ...this.formData }), e = await this.update(o);
        this.onAfterUpdate(this.formData);
        const t = this.primaryKey;
        this.replaceRowByCode(
          e[t],
          { ...this.editingRow.value || {}, ...e }
        );
      }
      J.success("保存成功"), this.dialogVisible.value = !1;
    } finally {
      this.submitting.value = !1;
    }
  }
  /**
   * 删除确认（ST-10：逐行名称）
   * @param rows 待删行（缺省取选中行）
   */
  async confirmDelete(o) {
    const e = o || this.selectedRows.value;
    if (e.length === 0) {
      J.warning("请先选择要删除的记录");
      return;
    }
    const t = this.primaryKey, l = e.map((c) => String(c[t] || "")).filter(Boolean);
    if (!await this.onDelete(l)) return;
    const i = e.map((c) => this.entityName(c)).filter(Boolean);
    let r;
    i.length === 1 ? r = `确定删除【${i[0]}】？` : i.length > 1 && i.length <= 3 ? r = `确定删除 ${i.length} 条记录（${i.join("、")}）？` : r = `确定删除 ${l.length} 条记录？`, await xe.confirm(r, "删除确认", {
      type: "warning",
      confirmButtonText: "确定删除",
      cancelButtonText: "取消"
    }), await this.delete(l), J.success("删除成功");
    for (const c of l)
      this.removeRowByCode(c);
    this.selectedRows.value = [], this.onAfterDelete(l);
  }
  async executeCustomToolbarAction(o) {
  }
  // ========================================================
  // API 调用
  // ========================================================
  async apiGet(o) {
    const e = `/api/${this.controllerName}${o}`, { yzhApi: t } = await Promise.resolve().then(() => Be);
    return t.get(e);
  }
  async apiPost(o, e) {
    const t = `/api/${this.controllerName}${o}`, { yzhApi: l } = await Promise.resolve().then(() => Be);
    return l.post(t, e);
  }
  async apiPostAndDownload(o, e, t) {
    const l = `/api/${this.controllerName}${o}`, { yzhApi: n } = await Promise.resolve().then(() => Be);
    return n.download(l, e, t);
  }
  async apiGetAndDownload(o, e) {
    const t = `/api/${this.controllerName}${o}`, { yzhApi: l } = await Promise.resolve().then(() => Be);
    return l.downloadGet(t, e);
  }
  async apiUpload(o, e) {
    const t = `/api/${this.controllerName}${o}`, { yzhApi: l } = await Promise.resolve().then(() => Be);
    return l.upload(t, e);
  }
  // ========================================================
  // 私有工具方法
  // ========================================================
  resetObject(o) {
    Object.keys(o).forEach((e) => delete o[e]);
  }
}
class sa {
  constructor() {
    /** 树数据（PascalCase，与后端 DTO 保持一致） */
    F(this, "treeData", k([]));
    /** 树加载状态 */
    F(this, "treeLoading", k(!1));
    /** 当前选中节点 */
    F(this, "selectedNode", k(null));
    /** 节点索引：Code → { node, parent }（O(1) 查找/替换/删除） */
    F(this, "index", /* @__PURE__ */ new Map());
  }
  /** 整树替换并重建索引 */
  setNodes(o) {
    this.treeData.value = o, this.rebuildIndex();
  }
  /** 重建索引（懒加载追加后调用） */
  rebuildIndex() {
    this.index.clear();
    const o = (e, t) => {
      var l;
      for (const n of e)
        this.index.set(n.Code, { node: n, parent: t }), (l = n.Children) != null && l.length && o(n.Children, n);
    };
    o(this.treeData.value, null);
  }
  /** 注册单个节点（append 后调用） */
  register(o, e) {
    this.index.set(o.Code, { node: o, parent: e });
  }
  /** O(1) 查找节点 */
  findNode(o) {
    var e;
    return ((e = this.index.get(o)) == null ? void 0 : e.node) ?? null;
  }
  /** O(1) 查找父节点 */
  findParent(o) {
    var e;
    return ((e = this.index.get(o)) == null ? void 0 : e.parent) ?? null;
  }
  /** 追加子节点（不触发 API，仅更新本地树 + 索引） */
  appendChild(o, e) {
    if (o) {
      const t = this.findNode(o);
      if (t) {
        t.Children = t.Children || [], t.Children.push(e), t.IsLeaf = !1, this.register(e, t);
        return;
      }
    }
    this.treeData.value.push(e), this.register(e, null);
  }
  /** 删除节点（含整个子树），返回是否删除成功 */
  removeNode(o) {
    var i;
    const e = this.index.get(o);
    if (!e) return !1;
    const t = e.parent ? (i = e.parent).Children ?? (i.Children = []) : this.treeData.value, l = t.findIndex((r) => r.Code === o);
    if (l < 0) return !1;
    t.splice(l, 1);
    const n = (r) => {
      this.index.delete(r.Code);
      for (const c of r.Children ?? []) n(c);
    };
    return n(e.node), !0;
  }
  /** 替换节点（O(1) 定位） */
  replaceNode(o, e) {
    var i;
    const t = this.index.get(o);
    if (!t) return !1;
    const l = t.parent ? (i = t.parent).Children ?? (i.Children = []) : this.treeData.value, n = l.findIndex((r) => r.Code === o);
    return n < 0 ? !1 : (l.splice(n, 1, e), this.index.delete(o), this.register(e, t.parent), !0);
  }
  /** 展开到指定节点（返回节点是否存在） */
  has(o) {
    return this.index.has(o);
  }
}
function ra(a) {
  return {
    TextBox: "text",
    TextArea: "textarea",
    NumberBox: "number",
    Decimal: "number",
    DatePicker: "date",
    DateTimePicker: "datetime",
    ComboBox: "select",
    DropDownList: "select",
    RadioButtonList: "radio",
    CheckBox: "checkbox",
    Switch: "switch",
    Upload: "upload",
    TreeSelect: "treeSelect",
    Cascader: "cascader",
    PasswordBox: "password",
    Memo: "textarea"
  }[a] || "text";
}
class sl extends na {
  constructor() {
    super(...arguments);
    // ──── 树能力混入（TT-2：状态 + 索引 + 增量变更） ────
    F(this, "treeSide", new sa());
    /** 完整树表配置（PascalCase，YZH.Core.Stand/TreeTableConfigDto） */
    F(this, "treeTableConfig", k(null));
    // ──── 树节点表单弹窗状态 ────
    F(this, "treeDialogVisible", k(!1));
    F(this, "treeDialogMode", k("add"));
    F(this, "treeSubmitting", k(!1));
    /** 树节点表单数据：PascalCase key（与 treeFormFields prop 一致） */
    F(this, "treeFormData", be({}));
    /** 当前新增节点的父节点 */
    F(this, "treeParentNode", k(null));
    /** 当前编辑的节点 */
    F(this, "treeEditingNode", k(null));
    // ──── 树表组件引用（用于 appendNode 等直接操作） ────
    F(this, "_treeTableRef", null);
    // ========================================================
    // 树→表格联动（TT-6/TT-7）
    // ========================================================
    /** 节点点击 → 表格联动刷新（dataLoader 已自动注入 RelateField；箭头属性自动绑定 this） */
    F(this, "onNodeClick", async (e) => {
      var t;
      this.treeSide.selectedNode.value = e, this.pagination.page = 1, !((t = this.treeConfig) != null && t.OnlyLeafSelectable && !e.IsLeaf) && (this._tableRef ? await this._tableRef.refresh() : await this.refreshTable());
    });
    // ========================================================
    // dispatch 扩展（TT-10）：树节点动作路由
    // ========================================================
    /** 树节点动作入口（绑定 @tree-node-action="logic.onNodeAction"；箭头属性自动绑定 this） */
    F(this, "onNodeAction", async (e, t) => {
      var n, i;
      const l = (i = (n = this.handlers) == null ? void 0 : n.get) == null ? void 0 : i.call(n, e);
      if (l) {
        await l(t, void 0);
        return;
      }
      switch (e) {
        case "add-child":
          this.openTreeNodeDialog(null, t);
          return;
        case "add-root":
          this.openTreeNodeDialog(null, null);
          return;
        case "edit":
        case "node-edit":
          this.openTreeNodeDialog(t);
          return;
        case "delete":
        case "node-delete":
          await this.deleteTreeNodeWithConfirm(t);
          return;
        case "toggle-valid":
        case "node-toggle-valid":
          await this.toggleTreeNodeWithConfirm(t);
          return;
        default:
          e.startsWith("custom:") && await this.executeTreeAction(e.slice(7), t);
      }
    });
  }
  /** 树数据（PascalCase） */
  get treeData() {
    return this.treeSide.treeData.value;
  }
  set treeData(e) {
    this.treeSide.treeData.value = e;
  }
  /** 树加载状态 */
  get treeLoading() {
    return this.treeSide.treeLoading;
  }
  /** 当前选中节点 */
  get selectedNode() {
    return this.treeSide.selectedNode.value;
  }
  set selectedNode(e) {
    this.treeSide.selectedNode.value = e;
  }
  /** 设置树表组件引用（模板中调用，或由 useTreeTable 注入） */
  setTreeTableRef(e) {
    this._treeTableRef = e;
  }
  // ──── 树配置快捷访问 ────
  /** 树行为配置（YZH.Core.Stand/TreeBehaviorConfigDto） */
  get treeConfig() {
    var e;
    return ((e = this.treeTableConfig.value) == null ? void 0 : e.TreeConfig) ?? null;
  }
  /** 启用/禁用字段名（优先 TreeConfig.EnableField，fallback TableConfig.EnableField） */
  get enableField() {
    var e, t;
    return ((e = this.treeConfig) == null ? void 0 : e.EnableField) ?? ((t = this.config.value) == null ? void 0 : t.EnableField) ?? null;
  }
  /** 树节点表单配置（EntityConfigDto） */
  get treeFormConfig() {
    var e;
    return ((e = this.treeTableConfig.value) == null ? void 0 : e.TreeFormConfig) ?? null;
  }
  /** 未选中树节点时的表格行为 */
  get noSelectionBehavior() {
    var e;
    return ((e = this.treeConfig) == null ? void 0 : e.NoSelectionBehavior) ?? "empty";
  }
  /** 关联字段名（PascalCase） */
  get relateField() {
    var e;
    return ((e = this.treeConfig) == null ? void 0 : e.RelateField) || "ParentCode";
  }
  // ──── 自动注入的操作按钮（来自后端 /treepconfig） ────
  /** 树节点自定义操作按钮：{ 方法名: 显示文字 }（后端自动注入） */
  get treeCustomActions() {
    var e;
    return ((e = this.treeConfig) == null ? void 0 : e.CustomActions) ?? {};
  }
  /** 表格行自定义操作按钮：{ 方法名: 显示文字 }（后端自动注入） */
  get rowCustomButtons() {
    var e, t;
    return ((t = (e = this.config.value) == null ? void 0 : e.RowButtons) == null ? void 0 : t.CustomButtons) ?? {};
  }
  /**
   * 树节点操作按钮（YzhAction[]，TT-9：完全由后端 TreeConfig 配置驱动）
   *
   * AllowEdit → 编辑（+ 新增下级，取决于 AllowAddChild）；AllowDelete → 删除；
   * EnableField → 禁用/启用（按节点状态动态显示单个）；CustomActions → 自定义动作。前端零硬编码。
   */
  get nodeActions() {
    return (e) => this.resolveTreeActions(e);
  }
  /** 树动作解析（子类可覆盖以追加自定义动作） */
  resolveTreeActions(e) {
    const t = [], l = this.treeConfig;
    if (!l) return t;
    if (l.AllowEdit && (this.allowAddChild && t.push({ key: "add-child", text: "新增下级" }), t.push({ key: "edit", text: "编辑" })), l.AllowDelete && t.push({ key: "delete", text: "删除", type: "danger", danger: !0 }), l.EnableField || this.enableField) {
      const n = l.EnableField ?? this.enableField;
      if (n) {
        const i = e.Extra || {}, r = n.charAt(0).toLowerCase() + n.slice(1);
        (i[n] ?? i[r] ?? 1) === 1 ? t.push({ key: "toggle-disable", text: "禁用", type: "warning" }) : t.push({ key: "toggle-enable", text: "启用", type: "warning" });
      }
    }
    if (!l.EnableField && !this.enableField && l.CustomActions)
      for (const [n, i] of Object.entries(l.CustomActions))
        t.push({ key: `custom:${n}`, text: i, type: "info" });
    return t;
  }
  /**
   * 获取树节点操作按钮的显示文字（toggle 按节点状态动态显示）
   */
  getNodeActionLabel(e, t) {
    if (e === "toggle-disable" || e === "toggle-enable")
      return e === "toggle-disable" ? "禁用" : "启用";
    const l = this.nodeActions(t).find((n) => n.key === e);
    return (l == null ? void 0 : l.text) ?? e;
  }
  /** 树节点表单布局列数（从 TreeFormConfig.FormCols 读取） */
  get treeFormLayoutCols() {
    return qe(this.treeFormConfig);
  }
  /** 树节点表单字段配置（保留 TreeFormConfig 专用布局规则：ColSpan>1 占满整行） */
  get treeFormFields() {
    var i, r;
    const e = (i = this.treeFormConfig) == null ? void 0 : i.Columns, t = (r = this.treeFormConfig) == null ? void 0 : r.Schema;
    if (!e) return [];
    const l = this.treeFormLayoutCols, n = Math.floor(24 / l);
    return e.filter((c) => c.BcFlag && c.Type !== "Other").map((c) => {
      var C;
      const h = c.FieldName, d = at(h), v = t == null ? void 0 : t[d];
      return {
        prop: h,
        label: c.DesName,
        type: ra(c.Type),
        required: !c.Yxk,
        disabled: c.Enable === !1,
        span: (c.ColSpan ?? 0) > 1 ? 24 : n,
        dictCode: c.DictCode || void 0,
        options: void 0,
        placeholder: (C = c.Type) != null && C.includes("Picker") ? `请选择${c.DesName}` : `请输入${c.DesName}`,
        defaultValue: c.Mrz ? c.Type === "Switch" ? Number(c.Mrz) : c.Mrz : v == null ? void 0 : v.Default,
        fieldSchema: v
      };
    });
  }
  // ========================================================
  // 覆盖点（TT-8）
  // ========================================================
  /** 是否允许「新增下级」：默认读后端 TreeConfig.AllowAddChild（ISO-9，扁平树由后端配置 false） */
  get allowAddChild() {
    var e;
    return ((e = this.treeConfig) == null ? void 0 : e.AllowAddChild) ?? !0;
  }
  /** 新增行是否要求先选中树节点（无层级树可覆盖为 false，ROL-2） */
  get requireTreeSelectionForAdd() {
    return !0;
  }
  /** 是否允许在指定节点下新增（organization=仅叶子；返回 false 时给出提示） */
  canAddUnderNode(e) {
    return !0;
  }
  /** 指定节点不可新增时的提示文案 */
  canAddUnderNodeMessage(e) {
    return "该节点不允许新增";
  }
  /** 树节点新增默认值（合并到 TreeFormConfig.NewEntity 之后） */
  get defaultTreeValues() {
    return {};
  }
  /** 树节点确认弹窗名称字段（organization=OrgName 等） */
  get treeEntityNameField() {
    var e;
    return ((e = this.treeConfig) == null ? void 0 : e.NameField) ?? "Name";
  }
  /** 关联过滤值：虚拟节点返回 null，其余返回选中节点 Code */
  relatedValue() {
    const e = this.selectedNode;
    return !e || this.isVirtualNode(e) ? null : e.Code;
  }
  /** 是否对表格查询应用树过滤（dictionary 全量模式等可覆盖） */
  shouldApplyTreeFilter() {
    return !!this.selectedNode && !this.isVirtualNode(this.selectedNode);
  }
  /** 虚拟节点判定（skill-manage 的 __all__ 等） */
  isVirtualNode(e) {
    return e.NodeType === "virtual";
  }
  /** 树加载完成后钩子（skill-manage 注入"全部"虚拟节点） */
  async afterTreeLoaded() {
  }
  /** 树加载后自动选中第一个节点（skill-manage=true） */
  get autoSelectFirstNode() {
    return !1;
  }
  // ──── 树节点生命周期钩子（与后端 OnBeforeAddTree/… 对齐，BE-5） ────
  onBeforeAddTree(e, t) {
  }
  onAfterAddTree(e, t) {
  }
  onBeforeUpdateTree(e, t) {
  }
  onAfterUpdateTree(e, t) {
  }
  onBeforeDeleteTree(e) {
    return !0;
  }
  onAfterDeleteTree(e) {
  }
  // ========================================================
  // 配置加载（覆盖：获取 TreeTableConfig）
  // ========================================================
  async loadConfig() {
    const e = await this.apiGet("/treepconfig");
    this.treeTableConfig.value = e.data, this.config.value = e.data.TableConfig;
  }
  // ========================================================
  // 生命周期（TT-3）
  // ========================================================
  /** 初始化：配置 → 树 → afterTreeLoaded → 自动选中 → onAfterInit */
  async init() {
    if (await this.loadConfig(), await this.loadTreeRoot(), await this.afterTreeLoaded(), this.autoSelectFirstNode && !this.selectedNode) {
      const e = this.treeData[0];
      e && await this.onNodeClick(e);
    }
    await this.onAfterInit();
  }
  // ========================================================
  // 树加载
  // ========================================================
  /** 加载根节点（/api/{controller}/tree/root） */
  async loadTreeRoot() {
    this.treeSide.treeLoading.value = !0;
    try {
      const t = (await this.apiPost("/tree/root", {})).data ?? [];
      this.treeSide.setNodes(t.map((l) => this.dtoToNode(l)));
    } finally {
      this.treeSide.treeLoading.value = !1;
    }
  }
  /** 懒加载子节点（/api/{controller}/tree/children） */
  async loadChildren(e, t) {
    var v;
    const n = Array.isArray(e == null ? void 0 : e.data) && e.data.length === 0 ? e : (e == null ? void 0 : e.data) ?? e, i = n == null ? void 0 : n.Code, r = ((v = n == null ? void 0 : n.Extra) == null ? void 0 : v.level) ?? 0;
    if (!i)
      return t && t([]), [];
    const d = ((await this.apiPost("/tree/children", {
      ParentCode: i,
      Level: r
    })).data ?? []).map((C) => this.dtoToNode(C, n));
    for (const C of d) this.treeSide.register(C, n);
    return t && t(d), n && typeof n == "object" && (n.children = d), d;
  }
  /**
   * 覆盖 buildFilters：自动注入 RelateField 树过滤（TT-6）
   */
  buildFilters(e) {
    const t = super.buildFilters(e);
    return this.shouldApplyTreeFilter() && t.push({
      Field: this.relateField,
      Value: this.relatedValue(),
      Operator: "eq"
    }), t;
  }
  /**
   * 覆盖 dataLoader：未选中节点且 NoSelectionBehavior='empty' 时不发请求
   */
  async dataLoader(e) {
    return !this.shouldApplyTreeFilter() && this.noSelectionBehavior === "empty" ? (this.pagination.total = 0, { rows: [], total: 0 }) : super.dataLoader(e);
  }
  /** 带树条件的分页查询（兼容保留；新代码走统一 dataLoader） */
  async loadPageWithTree(e) {
    this.loading.value = !0;
    try {
      const t = [
        ...super.buildFilters(),
        { Field: this.relateField, Value: e, Operator: "eq" }
      ], l = {
        Page: this.pagination.page,
        PageSize: this.pagination.pageSize,
        SortField: this.sortField.value,
        SortOrder: this.sortOrder.value,
        Filters: t
      }, i = (await this.apiPost("/filter", l)).data;
      i && (this.rows.value = this.postprocessRows(i.Items ?? []), this.pagination.total = i.TotalCount ?? 0);
    } catch {
      this.rows.value = [], this.pagination.total = 0;
    } finally {
      this.loading.value = !1;
    }
  }
  /** 无树条件的表格加载 */
  async loadPageWithoutTree() {
    this.noSelectionBehavior === "empty" ? (this.rows.value = [], this.pagination.total = 0) : await this.loadPage();
  }
  /** 刷新右侧表格（保持当前选中节点） */
  async refreshTable() {
    this.selectedNode && this.shouldApplyTreeFilter() ? this._tableRef ? await this._tableRef.refresh() : await this.loadPageWithTree(this.selectedNode.Code) : await this.loadPageWithoutTree();
  }
  // ========================================================
  // 行 CRUD 泛型流（TT-4）
  // ========================================================
  /**
   * 打开行弹窗（新增需满足树选中/叶子约束）
   * @returns 是否成功打开（失败时已给出提示）
   */
  openRowDialog(e) {
    if (!e) {
      const t = this.selectedNode;
      return this.requireTreeSelectionForAdd && !t ? (J.warning("请先在左侧选择节点"), !1) : t && !this.isVirtualNode(t) && !this.canAddUnderNode(t) ? (J.warning(this.canAddUnderNodeMessage(t)), !1) : (this.dialogMode.value = "add", this.formGroupIndex.value = "0", this.initFormData(), this.onPrepareAdd(this.formData), this.dialogVisible.value = !0, !0);
    }
    return this.openEditDialog(e), !0;
  }
  /** 提交行表单（= submitForm 别名，语义化入口） */
  async submitRowForm() {
    await this.submitForm();
  }
  /** 删除单行（带确认，名称取 entityName） */
  async deleteRow(e) {
    await this.confirmDelete([e]);
  }
  /** 批量删除选中行（带确认） */
  async batchDeleteRows(e) {
    await this.confirmDelete(e);
  }
  // ========================================================
  // 树节点 CRUD 泛型流（TT-5）
  // ========================================================
  /**
   * 打开树节点弹窗
   * @param node 编辑目标（null=新增）
   * @param parent 新增时的父节点（缺省取当前选中节点）
   */
  openTreeNodeDialog(e = null, t = null) {
    var i, r, c;
    if (e) {
      this.treeDialogMode.value = "edit", this.treeEditingNode.value = e, this.treeParentNode.value = null, this.resetObject(this.treeFormData);
      const h = ((i = this.treeFormConfig) == null ? void 0 : i.NewEntity) || {}, d = e.Extra || {}, v = {};
      for (const C of this.treeFormFields)
        C.prop in d && (v[C.prop] = d[C.prop]);
      return Object.assign(this.treeFormData, h, v, {
        Code: e.Code,
        ParentCode: e.ParentCode,
        [this.treeEntityNameField]: e.Name
      }), this.treeDialogVisible.value = !0, !0;
    }
    const l = t ?? this.selectedNode;
    if (this.requireTreeSelectionForAdd && !l)
      return J.warning("请先在左侧选择节点"), !1;
    if (l && !this.canAddUnderNode(l))
      return J.warning(this.canAddUnderNodeMessage(l)), !1;
    this.treeDialogMode.value = "add", this.treeEditingNode.value = null, this.treeParentNode.value = l, this.resetObject(this.treeFormData);
    const n = ((r = this.treeFormConfig) == null ? void 0 : r.NewEntity) || {};
    return Object.assign(this.treeFormData, n, this.defaultTreeValues, {
      [this.treeEntityNameField]: "",
      ParentCode: (l == null ? void 0 : l.Code) ?? ((c = this.treeConfig) == null ? void 0 : c.RootParentCode) ?? null
    }), this.treeDialogVisible.value = !0, !0;
  }
  /** 提交树节点表单 */
  async submitTreeNodeForm() {
    this.treeSubmitting.value = !0;
    try {
      const e = this.normalizeBeforeSubmit({ ...this.treeFormData });
      if (this.treeDialogMode.value === "add") {
        this.onBeforeAddTree(e, this.treeParentNode.value);
        const t = await this.addTreeNode(this.treeParentNode.value, e);
        t && this.onAfterAddTree(t, this.treeParentNode.value);
      } else {
        const t = this.treeEditingNode.value;
        this.onBeforeUpdateTree(t, e), await this.updateTreeNode(
          t,
          e[this.treeEntityNameField] ?? "",
          e
        ), this.onAfterUpdateTree(t, e);
      }
      this.treeDialogVisible.value = !1, J.success(this.treeDialogMode.value === "add" ? "创建成功" : "修改成功");
    } finally {
      this.treeDialogMode.value = "add", this.treeEditingNode.value = null, this.treeSubmitting.value = !1;
    }
  }
  /** 删除树节点（完整流程：确认弹窗 → API → 本地更新 → 表格联动） */
  async deleteTreeNodeWithConfirm(e) {
    const t = e.Name;
    await this.onBeforeDeleteTree(e) && (await xe.confirm(`确定删除【${t}】？`, "删除确认", {
      type: "warning",
      confirmButtonText: "确定删除",
      cancelButtonText: "取消"
    }), await this.deleteTreeNode(e, !0), this.onAfterDeleteTree(e), J.success("已删除"));
  }
  // ========================================================
  // 树节点底层操作（兼容保留）
  // ========================================================
  /** 新增树节点（/api/{controller}/tree/add） */
  async addTreeNode(e, t) {
    var d, v, C, _;
    const l = ((d = this.treeConfig) == null ? void 0 : d.CodeField) ?? "Code", n = {
      ...Ye(t),
      [((v = this.treeConfig) == null ? void 0 : v.ParentCodeField) ?? "ParentCode"]: (e == null ? void 0 : e.Code) ?? ((C = this.treeConfig) == null ? void 0 : C.RootParentCode) ?? null
    }, i = await this.apiPost("/tree/add", n), c = (((_ = i.data) == null ? void 0 : _[l]) ?? "") || n[l], h = this.dtoToNode(
      i.data ?? { Code: c, Name: n.Name ?? "", ParentCode: (e == null ? void 0 : e.Code) ?? null },
      e ?? void 0
    );
    return c && !i.data && (h.Code = c), this._treeTableRef ? (this._treeTableRef.appendNode((e == null ? void 0 : e.Code) ?? null, h), this.treeSide.register(h, e)) : this.treeSide.appendChild((e == null ? void 0 : e.Code) ?? null, h), h;
  }
  /** 修改树节点（/api/{controller}/tree/update） */
  async updateTreeNode(e, t, l) {
    var h, d;
    const n = l ? Ye(l) : {}, i = {
      [((h = this.treeConfig) == null ? void 0 : h.CodeField) ?? "Code"]: e.Code,
      [((d = this.treeConfig) == null ? void 0 : d.NameField) ?? "Name"]: t,
      ...n
    }, r = await this.apiPost("/tree/update", i), c = this.dtoToNode(
      r.data ?? { ...e, Name: t },
      this.treeSide.findParent(e.Code)
    );
    this.treeSide.replaceNode(e.Code, c) || (e.Name = t);
  }
  /** 删除树节点（skipConfirm=true 时由调用方负责确认） */
  async deleteTreeNode(e, t = !1) {
    var n, i, r;
    if (!((n = this.treeConfig) != null && n.AllowDeleteWithChildren) && e.Children && e.Children.length > 0) {
      J.warning("该节点包含子节点，请先删除子节点");
      return;
    }
    t || await xe.confirm(`确定删除节点 "${e.Name}"？`, "删除确认", {
      type: "warning",
      confirmButtonText: "确定",
      cancelButtonText: "取消"
    });
    const l = await this.apiPost("/tree/delete", [e.Code]);
    if (!l.success) {
      J.error(l.message || "删除失败");
      return;
    }
    if (this.treeSide.removeNode(e.Code), (i = this._treeTableRef) != null && i.removeNode)
      try {
        this._treeTableRef.removeNode(null, e.Code);
      } catch {
        await this.loadTreeRoot();
      }
    else
      await this.loadTreeRoot();
    ((r = this.selectedNode) == null ? void 0 : r.Code) === e.Code && (this.treeSide.selectedNode.value = null, await this.loadPageWithoutTree());
  }
  /** 树节点执行自定义操作 */
  async executeTreeAction(e, t, l) {
    var r;
    const n = l ? Ye(l) : {}, i = await this.apiPost(`/tree/action/${e}`, {
      [((r = this.treeConfig) == null ? void 0 : r.CodeField) ?? "Code"]: t.Code,
      ...n
    });
    return await this.loadTreeRoot(), i.data;
  }
  /** 切换树节点有效标志（自动更新 node.Extra[enableField]） */
  async toggleTreeNodeIsValid(e) {
    var n;
    const t = this.enableField ?? "IsValid", l = await this.apiPost(
      "/tree/toggle-valid",
      { [((n = this.treeConfig) == null ? void 0 : n.CodeField) ?? "Code"]: e.Code }
    );
    if (l.success) {
      const i = e.Extra || {};
      i[t] = l.data.IsValid;
      const r = t.charAt(0).toLowerCase() + t.slice(1);
      return r !== t && (i[r] = l.data.IsValid), e.Extra = { ...i }, J.success(l.data.IsValid === 1 ? "已启用" : "已禁用"), l.data;
    }
    return null;
  }
  /** 切换树节点有效标志（完整流程：确认弹窗 → API → 本地更新） */
  async toggleTreeNodeWithConfirm(e, t) {
    const l = this.enableField ?? "IsValid", n = e.Extra || {}, i = l.charAt(0).toLowerCase() + l.slice(1), c = (n[l] ?? n[i] ?? 1) === 1 ? "禁用" : "启用", h = (t == null ? void 0 : t.entityName) ?? e.Name;
    await xe.confirm(`确定${c}【${h}】？`, `${c}确认`, {
      type: "warning",
      confirmButtonText: `确定${c}`,
      cancelButtonText: "取消"
    }), await this.toggleTreeNodeIsValid(e);
  }
  // ========================================================
  // DTO → TreeNode 映射（AD-5）
  // ========================================================
  /** TreeItemDto → TreeNode（PascalCase，附 level 计算并注册索引） */
  dtoToNode(e, t) {
    var n;
    const l = (((n = t == null ? void 0 : t.Extra) == null ? void 0 : n.level) ?? -1) + 1;
    return {
      Code: e.Code,
      Name: e.Name,
      ParentCode: e.ParentCode ?? null,
      NodeType: e.NodeType,
      IsLeaf: e.IsLeaf,
      Extra: { ...e.Extra, level: l },
      Children: []
    };
  }
  // ========================================================
  // 树 Split 方法（O(1)，基于 TreeSide 索引）
  // ========================================================
  /** @deprecated 兼容旧命名，等价 treeSide.removeNode */
  removeNodeFromTree(e) {
    this.treeSide.removeNode(e);
  }
  /** @deprecated 兼容旧命名，等价 treeSide.findNode（O(1)） */
  findNode(e) {
    return this.treeSide.findNode(e);
  }
  /** @deprecated 兼容旧命名，等价 treeSide.replaceNode */
  replaceTreeNode(e, t) {
    this.treeSide.replaceNode(e, t);
  }
  async refreshChildren(e) {
    var i;
    const n = ((await this.apiPost("/tree/children", {
      ParentCode: e.Code,
      Level: ((i = e.Extra) == null ? void 0 : i.level) ?? 0
    })).data ?? []).map((r) => this.dtoToNode(r, e));
    for (const r of n) this.treeSide.register(r, e);
    e.Children = n, e.IsLeaf = n.length === 0;
  }
  async refreshTree() {
    await this.loadTreeRoot();
  }
  // ========================================================
  // 兼容便捷方法
  // ========================================================
  onCheckChange(e, t) {
  }
  async addRootNode(e) {
    return this.addTreeNode(null, e);
  }
  async addChildNode(e, t) {
    return this.addTreeNode(e, t);
  }
  async renameNode(e, t) {
    await this.updateTreeNode(e, t);
  }
}
class lt {
  constructor(o) {
    // ──── 左树 ────
    F(this, "treeData", k([]));
    F(this, "selectedNode", k(null));
    // ──── 右侧关联数据 ────
    F(this, "associationData", k([]));
    // ──── 加载状态 ────
    F(this, "loading", k(!1));
    F(this, "saving", k(!1));
    // ──── 本地关联缓存（badge / 差集保存依据） ────
    F(this, "associationCache", be(/* @__PURE__ */ new Map()));
    F(this, "cacheLoaded", !1);
    // ──── API 注入 ────
    F(this, "api");
    this.api = o;
  }
  // ========================================================
  // 初始化：加载本地缓存 + 左树
  // ========================================================
  async init() {
    await this.initCache(), await this.loadTreeRoot();
  }
  async initCache() {
    if (!this.cacheLoaded)
      try {
        const o = await this.api.getAll();
        this.buildCache(o), this.cacheLoaded = !0;
      } catch (o) {
        console.error(`[${this.constructor.name}] 加载关联缓存失败:`, o);
      }
  }
  buildCache(o) {
    this.associationCache.clear();
    for (const e of o)
      this.associationCache.has(e.ContextCode) || this.associationCache.set(e.ContextCode, /* @__PURE__ */ new Set()), this.associationCache.get(e.ContextCode).add(e.TargetCode);
  }
  // ========================================================
  // 左树
  // ========================================================
  async loadTreeRoot() {
    try {
      const o = await this.api.getTreeRoot();
      return this.treeData.value = o, o;
    } catch (o) {
      return J.error(o.message || "加载树失败"), [];
    }
  }
  async loadChildren(o, e) {
    try {
      const t = await this.api.getTreeChildren(o.data.Code, o.level ?? 0);
      e(t);
    } catch (t) {
      J.error(t.message || "加载子节点失败"), e([]);
    }
  }
  // ========================================================
  // Badge（来自本地缓存，局部更新）
  // ========================================================
  getCountForNode(o) {
    var e;
    return ((e = this.associationCache.get(o)) == null ? void 0 : e.size) ?? 0;
  }
  getNodeBadge(o) {
    const e = this.getCountForNode(o);
    return e > 0 ? String(e) : void 0;
  }
  // ========================================================
  // 节点选择 → 加载关联态
  // ========================================================
  async handleNodeSelect(o) {
    if (!(!o || !o.Code)) {
      this.selectedNode.value = o, this.loading.value = !0;
      try {
        const e = await this.api.getAssociations(o.Code);
        for (const l of e)
          l.Extra && Object.assign(l, l.Extra);
        const t = this.associationCache.get(o.Code) ?? /* @__PURE__ */ new Set();
        for (const l of e)
          l.CheckFlag = t.has(l.Code);
        this.afterAssociationsLoaded(e, t), this.associationData.value = e;
      } catch (e) {
        J.error(e.message || "加载数据失败"), this.associationData.value = [];
      } finally {
        this.loading.value = !1;
      }
    }
  }
  /** 子类覆盖：关联态加载后处理（如 role-api 的分组跟随） */
  afterAssociationsLoaded(o, e) {
  }
  // ========================================================
  // 增量保存（乐观更新：先本地后端，失败提示）
  // ========================================================
  async handleCheckChange(o) {
    if (!this.selectedNode.value) {
      J.warning("请先选择左侧节点");
      return;
    }
    const e = this.selectedNode.value.Code;
    this.saving.value = !0;
    try {
      if (o.added.length > 0) {
        const t = this.buildSelections(o.added);
        if (t.length > 0) {
          const l = await this.api.add(e, t);
          this.syncCacheAdd(e, l.Applied ?? t.map((n) => n.Code));
        }
      }
      if (o.removed.length > 0) {
        const t = this.buildSelections(o.removed);
        t.length > 0 && (await this.api.remove(e, t), this.syncCacheRemove(e, t.map((l) => l.Code)));
      }
      J.success("保存成功");
    } catch (t) {
      J.error(t.message || "保存失败");
    } finally {
      this.saving.value = !1;
    }
  }
  syncCacheAdd(o, e) {
    let t = this.associationCache.get(o);
    t || (t = /* @__PURE__ */ new Set(), this.associationCache.set(o, t));
    for (const l of e) t.add(l);
  }
  syncCacheRemove(o, e) {
    const t = this.associationCache.get(o);
    if (t)
      for (const l of e) t.delete(l);
  }
  // ========================================================
  // 选择项构建（子类定义可勾选的 NodeType）
  // ========================================================
  /** 子类定义：哪些 NodeType 可被勾选（空数组 = 全部） */
  get selectableNodeTypes() {
    return [];
  }
  buildSelections(o) {
    const e = [], t = this.associationData.value;
    for (const l of o) {
      const n = t.find((i) => i.Code === l);
      n && (this.selectableNodeTypes.length === 0 || this.selectableNodeTypes.includes(n.NodeType)) && e.push({ Code: l, NodeType: n.NodeType });
    }
    return e;
  }
}
class rl extends lt {
  /** 可勾选的节点类型（如 ['menu']）—— 子类必须声明 */
  get selectableNodeTypes() {
    return [];
  }
}
class il extends lt {
  /** 构造：linkApi 负责树/列表/保存；baseApi 可传 null 走空实现 */
  constructor(e) {
    super({
      getTreeRoot: e.treeRoot,
      getTreeChildren: async () => [],
      getAssociations: e.list,
      add: async () => ({ Updated: 0 }),
      remove: async () => ({ Updated: 0 }),
      getAll: async () => []
    });
    F(this, "linkApi");
    /** 搜索关键字（页面可绑定本地过滤） */
    F(this, "searchKey", k(""));
    this.linkApi = e;
  }
  /** 左侧节点的主键字段名（默认 Code） */
  get leftKeyField() {
    return "Code";
  }
  /** 右表行中承载关联键的字段名（如 StandardCode / PhaseCode） */
  get linkedKeyField() {
    return "Code";
  }
  /** 关联行数据（含 Linked） */
  get linkRows() {
    return this.associationData.value;
  }
  /** 节点选择 → list + 同步勾选快照 */
  async handleNodeSelect(e) {
    if (!(!e || !e.Code)) {
      this.selectedNode.value = e, this.loading.value = !0;
      try {
        const t = await this.linkApi.list(e.Code);
        this.associationData.value = t;
      } catch (t) {
        J.error(t.message || "加载关联数据失败"), this.associationData.value = [];
      } finally {
        this.loading.value = !1;
      }
    }
  }
  /**
   * 差集保存：与「变更前 Linked 快照」比对后逐条提交。
   *
   * 由页面的 selection-change 调用：
   *   onSelectionChange(selection) → 差集计算 → saveLinked()
   *
   * @param currentCodes 当前勾选的 code 集合
   * @param itemName 行显示名（失败提示用）
   */
  async saveLinkedDiff(e, t) {
    const l = this.selectedNode.value;
    if (!l) return;
    const n = new Set(
      this.associationData.value.filter((d) => d.Linked).map((d) => d[this.linkedKeyField])
    ), i = [], r = [];
    for (const d of this.associationData.value) {
      const v = d[this.linkedKeyField], C = e.has(v), _ = n.has(v);
      C && !_ && i.push(d), !C && _ && r.push(d);
    }
    for (const d of this.associationData.value)
      d.Linked = e.has(d[this.linkedKeyField]);
    const c = [
      ...i.map((d) => this.buildSavePayload(l, d, !0)),
      ...r.map((d) => this.buildSavePayload(l, d, !1))
    ];
    if (c.length === 0) return;
    const h = [];
    for (const d of c)
      try {
        await this.linkApi.save(d);
      } catch {
        const v = this.associationData.value.find(
          (C) => C[this.linkedKeyField] === d[this.linkedKeyField]
        );
        v && (v.Linked = !v.Linked), h.push(t(v ?? d));
      }
    h.length > 0 && J.error(`保存失败：${h.join("、")}`);
  }
  /** 组装 save 请求体（子类可覆盖以适配后端字段） */
  buildSavePayload(e, t, l) {
    return {
      [this.leftKeyField]: e.Code,
      [this.linkedKeyField]: t[this.linkedKeyField],
      Linked: l
    };
  }
}
const dl = [
  {
    type: "root",
    label: "根机构",
    icon: "OfficeBuilding",
    allowedActions: ["add"],
    allowedChildTypes: ["company"],
    isRelatable: !1,
    onlyRoot: !0,
    onlyLeaf: !1,
    maxChildren: 1
  },
  {
    type: "company",
    label: "公司",
    icon: "Company",
    allowedActions: ["add", "edit", "delete", "select"],
    allowedChildTypes: ["company", "department"],
    isRelatable: !0,
    onlyRoot: !1,
    onlyLeaf: !1,
    maxChildren: 0
  },
  {
    type: "department",
    label: "部门",
    icon: "User",
    allowedActions: ["add", "edit", "delete", "select"],
    allowedChildTypes: ["department", "team"],
    isRelatable: !0,
    onlyRoot: !1,
    onlyLeaf: !1,
    maxChildren: 0
  },
  {
    type: "team",
    label: "小组",
    icon: "Avatar",
    allowedActions: ["edit", "delete", "select"],
    allowedChildTypes: [],
    isRelatable: !0,
    onlyRoot: !1,
    onlyLeaf: !0,
    maxChildren: 0
  }
];
function ia(a, o, e) {
  if (o === null || o === "")
    return [...a, e];
  const t = (l) => l.map((n) => n.Code === o ? { ...n, Children: [...n.Children ?? [], e] } : n.Children && n.Children.length > 0 ? { ...n, Children: t(n.Children) } : n);
  return t(a);
}
function da(a, o) {
  const e = [], t = (l) => {
    const n = [];
    for (const i of l)
      i.Code === o ? (e.push(i), ua(i).forEach((r) => e.push(r))) : i.Children && i.Children.length > 0 ? n.push({ ...i, Children: t(i.Children) }) : n.push(i);
    return n;
  };
  return { tree: t(a), removed: e };
}
function cl(a, o, e, t) {
  const l = je(a, o);
  if (!l)
    return { tree: a, error: `节点 "${o}" 不存在` };
  if (o === e)
    return { tree: a, error: "不能移动到自己" };
  if (e !== null && e !== "") {
    if (!je(a, e))
      return { tree: a, error: `目标父节点 "${e}" 不存在` };
    if (nt(l, e))
      return { tree: a, error: "不能移动到自己的子树下（会形成循环）" };
  }
  if (t !== void 0 && t > 0) {
    const r = st(l);
    if ((e === null || e === "" ? 0 : ha(a, e)) + 1 + r > t)
      return { tree: a, error: `移动后深度将超过限制 (${t})` };
  }
  const { tree: n } = da(a, o);
  return { tree: ia(n, e, {
    ...l,
    ParentCode: e
  }) };
}
function ul(a, o, e) {
  const t = (l) => l.map((n) => n.Code === o ? { ...n, ...e } : n.Children && n.Children.length > 0 ? { ...n, Children: t(n.Children) } : n);
  return t(a);
}
function hl(...a) {
  const o = [];
  for (const e of a)
    o.push(...e);
  return o;
}
function fl(a, o) {
  const e = Qe(a), t = Qe(o), l = new Map(e.map((h) => [h.node.Code, h])), n = new Map(t.map((h) => [h.node.Code, h])), i = [], r = [], c = [];
  for (const [, h] of n) {
    const d = l.get(h.node.Code);
    if (!d)
      i.push(h.node);
    else if (!pa(d.node, h.node)) {
      const v = ma(d.node, h.node);
      c.push({ code: h.node.Code, changes: v });
    }
  }
  for (const [h] of l)
    n.has(h) || r.push(h);
  return { added: i, removed: r, updated: c };
}
function pl(a) {
  const o = [], e = ca(a), t = new Set(e.map((n) => n.Code)), l = /* @__PURE__ */ new Map();
  for (const n of e)
    l.set(n.Code, (l.get(n.Code) ?? 0) + 1);
  for (const [n, i] of l)
    i > 1 && o.push(`Code "${n}" 重复 ${i} 次`);
  for (const n of e)
    n.ParentCode != null && n.ParentCode !== "" && !t.has(n.ParentCode) && o.push(`节点 "${n.Name}" 的 ParentCode "${n.ParentCode}" 不存在`);
  return fa(a) && o.push("树存在循环引用"), {
    valid: o.length === 0,
    errors: o
  };
}
function ca(a) {
  const o = [], e = (t) => {
    for (const l of t)
      o.push(l), l.Children && l.Children.length > 0 && e(l.Children);
  };
  return e(a), o;
}
function je(a, o) {
  for (const e of a) {
    if (e.Code === o) return e;
    if (e.Children && e.Children.length > 0) {
      const t = je(e.Children, o);
      if (t) return t;
    }
  }
  return null;
}
function ua(a) {
  const o = [], e = (t) => {
    o.push(t);
    for (const l of t.Children ?? []) e(l);
  };
  return e(a), o;
}
function nt(a, o) {
  for (const e of a.Children ?? [])
    if (e.Code === o || nt(e, o)) return !0;
  return !1;
}
function st(a) {
  if (!a.Children || a.Children.length === 0) return 1;
  let o = 0;
  for (const e of a.Children)
    o = Math.max(o, st(e));
  return o + 1;
}
function ha(a, o) {
  const e = (t, l) => {
    for (const n of t) {
      if (n.Code === o) return l;
      if (n.Children && n.Children.length > 0) {
        const i = e(n.Children, l + 1);
        if (i >= 0) return i;
      }
    }
    return -1;
  };
  return e(a, 0);
}
function fa(a) {
  const o = /* @__PURE__ */ new Set(), e = /* @__PURE__ */ new Set(), t = (l) => {
    if (e.has(l.Code)) return !0;
    if (o.has(l.Code)) return !1;
    o.add(l.Code), e.add(l.Code);
    for (const n of l.Children ?? [])
      if (t(n)) return !0;
    return e.delete(l.Code), !1;
  };
  for (const l of a)
    if (t(l)) return !0;
  return !1;
}
function Qe(a) {
  const o = [], e = (t, l) => {
    for (const n of t)
      o.push({ node: n, level: l }), n.Children && n.Children.length > 0 && e(n.Children, l + 1);
  };
  return e(a, 0), o;
}
function pa(a, o) {
  return a.Code === o.Code && a.Name === o.Name && a.ParentCode === o.ParentCode && a.NodeType === o.NodeType && a.IsLeaf === o.IsLeaf && a.Sort === o.Sort;
}
function ma(a, o) {
  const e = {};
  return a.Name !== o.Name && (e.Name = o.Name), a.ParentCode !== o.ParentCode && (e.ParentCode = o.ParentCode), a.NodeType !== o.NodeType && (e.NodeType = o.NodeType), a.IsLeaf !== o.IsLeaf && (e.IsLeaf = o.IsLeaf), a.Sort !== o.Sort && (e.Sort = o.Sort), e;
}
function ya(a, o, e) {
  if (!a || a.length === 0) return [];
  const {
    codeField: t,
    nameField: l,
    parentCodeField: n,
    typeField: i,
    leafField: r,
    sortField: c,
    extraFields: h,
    rootParentCode: d
  } = o, v = (e == null ? void 0 : e.maxLevel) ?? 0, C = e == null ? void 0 : e.startFromCode, _ = (e == null ? void 0 : e.currentLevel) ?? 0;
  if (v > 0 && _ >= v) return [];
  const g = /* @__PURE__ */ new Map(), E = [];
  for (const f of a) {
    const x = ce(f, t), q = ce(f, l), ee = ce(f, n) ?? null, ae = i ? ce(f, i) : void 0, se = r ? ce(f, r) : void 0, le = c ? ce(f, c) : void 0;
    let S;
    if (h && h.length > 0) {
      S = {};
      for (const K of h)
        S[K] = ce(f, K);
    }
    const L = {
      Code: x,
      Name: q,
      ParentCode: ee,
      NodeType: ae,
      IsLeaf: se,
      Sort: le,
      Extra: S,
      Children: [],
      Raw: f
    };
    g.set(x, L);
  }
  for (const f of g.values())
    if (C && f.Code === C)
      E.push(f);
    else if (!C && (f.ParentCode === d || f.ParentCode === null || f.ParentCode === ""))
      E.push(f);
    else {
      const x = g.get(f.ParentCode ?? "");
      x && (x.Children = [...x.Children ?? [], f]);
    }
  return E.sort((f, x) => (f.Sort ?? 0) - (x.Sort ?? 0)), E;
}
function ga(a, o, e) {
  const {
    codeField: t,
    nameField: l,
    parentCodeField: n,
    typeField: i,
    leafField: r,
    sortField: c,
    extraFields: h
  } = o, d = ce(a, t), v = ce(a, l), C = ce(a, n) ?? null, _ = i ? ce(i, i) : void 0, g = r ? ce(r, r) : void 0, E = c ? ce(a, c) : void 0;
  let f;
  if (h && h.length > 0) {
    f = {};
    for (const x of h)
      f[x] = ce(a, x);
  }
  return {
    Code: d,
    Name: v,
    ParentCode: C,
    NodeType: _,
    IsLeaf: g,
    Sort: E,
    Extra: f,
    Children: [],
    Raw: a
  };
}
function va(a, o) {
  const e = {
    [o.codeField]: a.Code,
    [o.nameField]: a.Name,
    [o.parentCodeField]: a.ParentCode
  };
  return o.typeField && a.NodeType && (e[o.typeField] = a.NodeType), o.sortField && a.Sort !== void 0 && (e[o.sortField] = a.Sort), e;
}
function Ge(a) {
  const o = [], e = (t) => {
    for (const l of t)
      o.push(l), l.Children && l.Children.length > 0 && e(l.Children);
  };
  return e(a), o;
}
function He(a) {
  const o = [], e = (t) => {
    for (const l of t)
      o.push(l), l.Children && l.Children.length > 0 && e(l.Children);
  };
  return e(a.Children ?? []), o;
}
function ba(a) {
  return [a, ...He(a)];
}
function Me(a, o) {
  const e = [];
  let t = a;
  for (; t && t.ParentCode; ) {
    const l = Ue(o, t.ParentCode);
    if (l)
      e.unshift(l), t = l;
    else
      break;
  }
  return e;
}
function Ca(a, o) {
  return [...Me(o, a).map((t) => t.Code), o.Code];
}
function wa(a, o) {
  return [...Me(o, a).map((t) => t.Name), o.Name];
}
function Ue(a, o) {
  for (const e of a) {
    if (e.Code === o) return e;
    if (e.Children && e.Children.length > 0) {
      const t = Ue(e.Children, o);
      if (t) return t;
    }
  }
  return null;
}
function rt(a, o) {
  for (const e of a) {
    if (o(e)) return e;
    if (e.Children && e.Children.length > 0) {
      const t = rt(e.Children, o);
      if (t) return t;
    }
  }
  return null;
}
function Xe(a, o) {
  const e = [], t = (l) => {
    for (const n of l)
      o(n) && e.push(n), n.Children && n.Children.length > 0 && t(n.Children);
  };
  return t(a), e;
}
function ka(a, o) {
  return o.ParentCode ? Ue(a, o.ParentCode) : null;
}
function Ta(a, o) {
  return Xe(a, (e) => e.NodeType === o);
}
function it(a, o) {
  if (!o || !o.trim()) return [];
  const e = o.toLowerCase();
  return Xe(a, (t) => t.Name.toLowerCase().includes(e));
}
function _a(a, o) {
  const e = it(a, o), t = /* @__PURE__ */ new Set();
  for (const l of e)
    t.add(l), Me(l, a).forEach((i) => t.add(i));
  return Array.from(t);
}
function xa(a, o) {
  const e = (t) => {
    const l = [];
    for (const n of t) {
      const i = e(n.Children ?? []);
      (o(n) || i.length > 0) && l.push({
        ...n,
        Children: i
      });
    }
    return l;
  };
  return e(a);
}
function Sa(a) {
  return He(a).length;
}
function Fa(a) {
  const o = (e, t) => {
    if (e.length === 0) return t;
    let l = t;
    for (const n of e)
      n.Children && n.Children.length > 0 && (l = Math.max(l, o(n.Children, t + 1)));
    return l;
  };
  return o(a, 0);
}
function Aa(a) {
  return Ge(a).length;
}
function za(a, o) {
  const e = [], t = (l, n) => {
    for (const i of l)
      n === o && e.push(i), i.Children && i.Children.length > 0 && t(i.Children, n + 1);
  };
  return t(a, 0), e;
}
function Na(a) {
  const o = [], e = Ge(a), t = new Set(e.map((n) => n.Code));
  for (const n of e)
    !n.Code && n.Code !== null && o.push(`节点 ${n.Name} 的 Code 为空`);
  const l = /* @__PURE__ */ new Map();
  for (const n of e)
    l.set(n.Code, (l.get(n.Code) ?? 0) + 1);
  for (const [n, i] of l)
    i > 1 && o.push(`Code "${n}" 重复 ${i} 次`);
  for (const n of e)
    n.ParentCode != null && n.ParentCode !== "" && !t.has(n.ParentCode) && o.push(`节点 "${n.Name}" 的 ParentCode "${n.ParentCode}" 不存在`);
  return dt(a) && o.push("树存在循环引用"), {
    valid: o.length === 0,
    errors: o
  };
}
function dt(a) {
  const o = /* @__PURE__ */ new Set(), e = /* @__PURE__ */ new Set(), t = (l) => {
    if (e.has(l.Code)) return !0;
    if (o.has(l.Code)) return !1;
    o.add(l.Code), e.add(l.Code);
    for (const n of l.Children ?? [])
      if (t(n)) return !0;
    return e.delete(l.Code), !1;
  };
  for (const l of a)
    if (t(l)) return !0;
  return !1;
}
function ce(a, o) {
  if (!a || !o) return;
  const e = o.split(".");
  let t = a;
  for (const l of e) {
    if (t == null) return;
    t = t[l];
  }
  return t;
}
const ml = {
  // 构造
  buildTree: ya,
  entityToNode: ga,
  nodeToEntity: va,
  flattenTree: Ge,
  // 遍历/查询
  getDescendants: He,
  getDescendantsWithSelf: ba,
  getAncestors: Me,
  getPath: Ca,
  getPathNames: wa,
  findNode: Ue,
  findNodeBy: rt,
  findNodesBy: Xe,
  getParent: ka,
  // 过滤/搜索
  filterByType: Ta,
  search: it,
  searchWithAncestors: _a,
  filterTree: xa,
  // 统计
  getChildrenCount: Sa,
  getDepth: Fa,
  getTotalCount: Aa,
  getNodesAtLevel: za,
  // 验证
  validate: Na,
  hasCycle: dt
};
export {
  lt as AssociationTreeCore,
  rl as CheckTreeCore,
  il as LinkTableCore,
  dl as OrgNodeTypeExamples,
  na as SingleTableCore,
  sa as TreeSide,
  sl as TreeTableCore,
  sl as TreeTableLogic,
  ot as YzhApiClient,
  Oa as YzhCard,
  Pa as YzhDialog,
  Ua as YzhEmptyState,
  Ft as YzhForm,
  Ra as YzhFormDialog,
  Va as YzhPageLayout,
  Ot as YzhPagination,
  Vt as YzhSearchBar,
  Ia as YzhStatusBadge,
  xo as YzhTable,
  Ut as YzhToolbar,
  et as YzhTree,
  Ma as YzhTreeTableCheckSelector,
  La as YzhTreeTableLayout,
  Ea as YzhTreeTableSelector,
  ia as addNode,
  ya as buildTree,
  Ha as deleteStorageFile,
  fl as diff,
  ga as entityToNode,
  Xa as fileExists,
  Ta as filterByType,
  xa as filterTree,
  Ue as findNode,
  rt as findNodeBy,
  Xe as findNodesBy,
  ca as flatten,
  Ge as flattenTree,
  Me as getAncestors,
  Sa as getChildrenCount,
  Fa as getDepth,
  He as getDescendants,
  ba as getDescendantsWithSelf,
  Ga as getFileUrl,
  za as getNodesAtLevel,
  ka as getParent,
  Ca as getPath,
  wa as getPathNames,
  Aa as getTotalCount,
  dt as hasCycle,
  Ja as listFiles,
  Jo as mapControlType,
  Qo as mapSearchControlType,
  Zo as mapSearchType,
  hl as mergeRoots,
  cl as moveSubtree,
  va as nodeToEntity,
  Ye as pascalCaseFormData,
  da as removeSubtree,
  nl as rowToFormData,
  it as search,
  _a as searchWithAncestors,
  at as toCamelCase,
  ta as toFormFields,
  qe as toFormLayoutCols,
  la as toPascalCase,
  Ka as toRowActionButtons,
  tt as toRowActions,
  Ze as toSearchFields,
  ea as toTableColumns,
  oa as toToolbarActions,
  Ya as toTreeActions,
  ve as tokenStore,
  ja as treeItemToNode,
  ml as treeUtils,
  ul as updateNode,
  Wa as uploadFile,
  qa as uploadFileBatch,
  Za as useAuth,
  al as useCheckTree,
  el as useConfirm,
  ll as useLinkTable,
  tl as useSingleTable,
  Qa as useTable,
  ol as useTreeTable,
  Na as validate,
  pl as validateTreeOps,
  Ne as yzhApi
};
