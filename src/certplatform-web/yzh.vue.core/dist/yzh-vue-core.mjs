var St = Object.defineProperty;
var _t = (a, o, e) => o in a ? St(a, o, { enumerable: !0, configurable: !0, writable: !0, value: e }) : a[o] = e;
var z = (a, o, e) => _t(a, typeof o != "symbol" ? o + "" : o, e);
import { defineComponent as le, ref as T, computed as j, reactive as Ce, watch as _e, resolveComponent as $, openBlock as y, createBlock as N, withCtx as k, createVNode as L, createElementBlock as R, Fragment as ae, renderList as ce, mergeProps as ve, createTextVNode as V, toDisplayString as I, renderSlot as W, createCommentVNode as H, createElementVNode as P, unref as Fe, normalizeStyle as ze, withKeys as At, normalizeClass as ke, createSlots as at, resolveDynamicComponent as Ee, withModifiers as Ft, getCurrentInstance as zt, onMounted as Be, resolveDirective as Nt, withDirectives as Dt, nextTick as xe, normalizeProps as $t, guardReactiveProps as Rt } from "vue";
import { ElInput as Ge, ElTree as Bt, ElMessageBox as Se, ElMessage as J } from "element-plus";
const Pt = {
  key: 0,
  class: "yzh-form__actions"
}, Vt = /* @__PURE__ */ le({
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
    const t = a, l = e, n = T(), i = j(() => 24 / t.cols), s = j(() => {
      if (t.rules) return t.rules;
      const h = {};
      return t.fields.forEach((x) => {
        if (x.hidden) return;
        const K = [];
        x.required && K.push({
          required: !0,
          message: `请${x.type === "select" || x.type === "radio" || x.type === "switch" ? "选择" : "输入"}${x.label}`,
          trigger: x.trigger || (x.type === "select" || x.type === "switch" ? "change" : "blur")
        }), x.validator && K.push({ validator: x.validator, trigger: x.trigger || "blur" }), K.length && (h[x.prop] = K);
      }), h;
    }), c = Ce({});
    async function f(h) {
      if (h.options) return h.options;
      if (!h.loadOptions) return [];
      if (c[h.prop]) return c[h.prop];
      const x = await h.loadOptions();
      return c[h.prop] = x, x;
    }
    (async () => {
      for (const h of t.fields)
        if (h.loadOptions && !h.options)
          try {
            await f(h);
          } catch {
          }
    })();
    const d = Ce({});
    function b() {
      Object.keys(d).forEach((h) => delete d[h]), Object.assign(d, t.modelValue || {}), t.fields.forEach((h) => {
        d[h.prop] === void 0 && h.defaultValue !== void 0 && (d[h.prop] = h.defaultValue);
      });
    }
    b(), _e(
      () => t.modelValue,
      () => b(),
      { deep: !0 }
    ), _e(
      d,
      (h) => {
        l("update:modelValue", { ...h });
      },
      { deep: !0 }
    );
    async function w() {
      if (n.value)
        try {
          await n.value.validate(), l("submit", { ...d }), l("validate", !0);
        } catch (h) {
          l("validate", !1, h);
        }
    }
    function m() {
      var h;
      b(), (h = n.value) == null || h.clearValidate(), l("reset");
    }
    async function p() {
      var h;
      return (h = n.value) == null ? void 0 : h.validate();
    }
    async function A() {
      var h;
      (h = n.value) == null || h.resetFields();
    }
    return o({ validate: p, resetFields: A, formRef: n }), (h, x) => {
      const K = $("el-input"), ee = $("el-input-number"), ne = $("el-option"), re = $("el-select"), se = $("el-radio"), F = $("el-radio-group"), E = $("el-checkbox"), q = $("el-checkbox-group"), G = $("el-switch"), X = $("el-date-picker"), te = $("el-tree-select"), Z = $("el-cascader"), me = $("el-form-item"), fe = $("el-col"), pe = $("el-row"), we = $("el-button"), ie = $("el-form");
      return y(), N(ie, {
        ref_key: "formRef",
        ref: n,
        model: d,
        rules: s.value,
        "label-width": a.labelWidth,
        "label-position": a.labelPosition,
        size: a.size,
        class: "yzh-form"
      }, {
        default: k(() => [
          L(pe, { gutter: 20 }, {
            default: k(() => [
              (y(!0), R(ae, null, ce(a.fields, (r) => (y(), R(ae, {
                key: r.prop
              }, [
                r.hidden ? H("", !0) : (y(), N(fe, {
                  key: 0,
                  span: r.span || i.value
                }, {
                  default: k(() => [
                    L(me, {
                      label: r.label,
                      prop: r.prop
                    }, {
                      default: k(() => [
                        !r.type || r.type === "text" || r.type === "textarea" || r.type === "password" ? (y(), N(K, ve({
                          key: 0,
                          modelValue: d[r.prop],
                          "onUpdate:modelValue": (u) => d[r.prop] = u,
                          type: r.type === "textarea" ? "textarea" : r.type === "password" ? "password" : "text",
                          placeholder: r.placeholder || `请输入${r.label}`,
                          disabled: r.disabled,
                          rows: r.type === "textarea" ? 3 : void 0,
                          autocomplete: r.type === "password" ? "new-password" : "off"
                        }, { ref_for: !0 }, r.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "type", "placeholder", "disabled", "rows", "autocomplete"])) : r.type === "number" ? (y(), N(ee, ve({
                          key: 1,
                          modelValue: d[r.prop],
                          "onUpdate:modelValue": (u) => d[r.prop] = u,
                          placeholder: r.placeholder,
                          disabled: r.disabled,
                          style: { width: "100%" }
                        }, { ref_for: !0 }, r.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : r.type === "select" ? (y(), N(re, ve({
                          key: 2,
                          modelValue: d[r.prop],
                          "onUpdate:modelValue": (u) => d[r.prop] = u,
                          placeholder: r.placeholder || `请选择${r.label}`,
                          multiple: r.multiple,
                          filterable: r.filterable,
                          disabled: r.disabled,
                          style: { width: "100%" }
                        }, { ref_for: !0 }, r.fieldProps), {
                          default: k(() => [
                            (y(!0), R(ae, null, ce(r.options || c[r.prop] || [], (u) => (y(), N(ne, {
                              key: u.value,
                              label: u.label,
                              value: u.value,
                              disabled: u.disabled
                            }, null, 8, ["label", "value", "disabled"]))), 128))
                          ]),
                          _: 2
                        }, 1040, ["modelValue", "onUpdate:modelValue", "placeholder", "multiple", "filterable", "disabled"])) : r.type === "radio" ? (y(), N(F, {
                          key: 3,
                          modelValue: d[r.prop],
                          "onUpdate:modelValue": (u) => d[r.prop] = u,
                          disabled: r.disabled
                        }, {
                          default: k(() => [
                            (y(!0), R(ae, null, ce(r.options || [], (u) => (y(), N(se, {
                              key: u.value,
                              value: u.value
                            }, {
                              default: k(() => [
                                V(I(u.label), 1)
                              ]),
                              _: 2
                            }, 1032, ["value"]))), 128))
                          ]),
                          _: 2
                        }, 1032, ["modelValue", "onUpdate:modelValue", "disabled"])) : r.type === "checkbox" ? (y(), N(q, {
                          key: 4,
                          modelValue: d[r.prop],
                          "onUpdate:modelValue": (u) => d[r.prop] = u,
                          disabled: r.disabled
                        }, {
                          default: k(() => [
                            (y(!0), R(ae, null, ce(r.options || [], (u) => (y(), N(E, {
                              key: u.value,
                              value: u.value
                            }, {
                              default: k(() => [
                                V(I(u.label), 1)
                              ]),
                              _: 2
                            }, 1032, ["value"]))), 128))
                          ]),
                          _: 2
                        }, 1032, ["modelValue", "onUpdate:modelValue", "disabled"])) : r.type === "switch" ? (y(), N(G, ve({
                          key: 5,
                          modelValue: d[r.prop],
                          "onUpdate:modelValue": (u) => d[r.prop] = u,
                          disabled: r.disabled,
                          "active-value": 1,
                          "inactive-value": 0
                        }, { ref_for: !0 }, r.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "disabled"])) : r.type === "date" ? (y(), N(X, ve({
                          key: 6,
                          modelValue: d[r.prop],
                          "onUpdate:modelValue": (u) => d[r.prop] = u,
                          type: "date",
                          placeholder: r.placeholder || `请选择${r.label}`,
                          disabled: r.disabled,
                          "value-format": "YYYY-MM-DD",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, r.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : r.type === "datetime" ? (y(), N(X, ve({
                          key: 7,
                          modelValue: d[r.prop],
                          "onUpdate:modelValue": (u) => d[r.prop] = u,
                          type: "datetime",
                          placeholder: r.placeholder || `请选择${r.label}`,
                          disabled: r.disabled,
                          "value-format": "YYYY-MM-DD HH:mm:ss",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, r.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : r.type === "dateRange" ? (y(), N(X, ve({
                          key: 8,
                          modelValue: d[r.prop],
                          "onUpdate:modelValue": (u) => d[r.prop] = u,
                          type: "daterange",
                          placeholder: r.placeholder || `请选择${r.label}`,
                          disabled: r.disabled,
                          "value-format": "YYYY-MM-DD",
                          "range-separator": "至",
                          "start-placeholder": "开始日期",
                          "end-placeholder": "结束日期",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, r.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "placeholder", "disabled"])) : r.type === "treeSelect" ? (y(), N(te, ve({
                          key: 9,
                          modelValue: d[r.prop],
                          "onUpdate:modelValue": (u) => d[r.prop] = u,
                          data: r.options || [],
                          placeholder: r.placeholder || `请选择${r.label}`,
                          disabled: r.disabled,
                          "check-strictly": "",
                          clearable: "",
                          style: { width: "100%" }
                        }, { ref_for: !0 }, r.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "data", "placeholder", "disabled"])) : r.type === "cascader" ? (y(), N(Z, ve({
                          key: 10,
                          modelValue: d[r.prop],
                          "onUpdate:modelValue": (u) => d[r.prop] = u,
                          options: r.options || [],
                          placeholder: r.placeholder || `请选择${r.label}`,
                          disabled: r.disabled,
                          style: { width: "100%" }
                        }, { ref_for: !0 }, r.fieldProps), null, 16, ["modelValue", "onUpdate:modelValue", "options", "placeholder", "disabled"])) : r.type === "custom" && r.slot ? W(h.$slots, r.slot, {
                          value: d[r.prop],
                          field: r,
                          data: d
                        }, void 0, !0, 11) : W(h.$slots, `field-${r.prop}`, {
                          value: d[r.prop],
                          field: r,
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
          a.showActions ? (y(), R("div", Pt, [
            W(h.$slots, "actions", {
              submit: w,
              reset: m
            }, () => [
              L(we, { onClick: m }, {
                default: k(() => [
                  V(I(a.resetText), 1)
                ]),
                _: 1
              }),
              L(we, {
                type: "primary",
                loading: a.loading,
                onClick: w
              }, {
                default: k(() => [
                  V(I(a.submitText), 1)
                ]),
                _: 1
              }, 8, ["loading"])
            ], !0)
          ])) : H("", !0)
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
}, Lt = /* @__PURE__ */ he(Vt, [["__scopeId", "data-v-0464ba24"]]), Ka = /* @__PURE__ */ le({
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
    const e = a, t = o, l = j({
      get: () => e.visible,
      set: (d) => t("update:visible", d)
    }), n = j({
      get: () => e.modelValue,
      set: (d) => t("update:modelValue", d)
    }), i = j(() => {
      if (e.title) return e.title;
      const d = e.entityName || "";
      return e.mode === "add" ? d ? `新增${d}` : "新增" : e.mode === "detail" ? d ? `${d}详情` : "详情" : d ? `编辑${d}` : "编辑";
    }), s = j(() => e.submitText ?? (e.mode === "detail" ? "关闭" : "保存"));
    function c() {
      t("submit");
    }
    function f() {
      t("cancel"), t("update:visible", !1);
    }
    return (d, b) => {
      const w = $("el-button"), m = $("el-dialog");
      return y(), N(m, {
        modelValue: l.value,
        "onUpdate:modelValue": b[1] || (b[1] = (p) => l.value = p),
        title: i.value,
        width: a.width,
        "close-on-click-modal": !1,
        "destroy-on-close": a.destroyOnClose,
        onClosed: b[2] || (b[2] = (p) => t("closed"))
      }, {
        footer: k(() => [
          W(d.$slots, "footer", {}, () => [
            L(w, { onClick: f }, {
              default: k(() => [...b[3] || (b[3] = [
                V("取消", -1)
              ])]),
              _: 1
            }),
            L(w, {
              type: "primary",
              loading: a.loading,
              onClick: c
            }, {
              default: k(() => [
                V(I(s.value), 1)
              ]),
              _: 1
            }, 8, ["loading"])
          ])
        ]),
        default: k(() => [
          W(d.$slots, "default", {}, () => [
            W(d.$slots, "prepend"),
            L(Lt, {
              modelValue: n.value,
              "onUpdate:modelValue": b[0] || (b[0] = (p) => n.value = p),
              fields: a.fields,
              loading: a.loading,
              cols: a.cols,
              "label-width": a.labelWidth,
              "show-actions": !1,
              onSubmit: c,
              onReset: f
            }, null, 8, ["modelValue", "fields", "loading", "cols", "label-width"])
          ])
        ]),
        _: 3
      }, 8, ["modelValue", "title", "width", "destroy-on-close"]);
    };
  }
}), Et = { class: "yzh-search-bar" }, Mt = { class: "yzh-search-bar__inner" }, Ut = { class: "yzh-search-bar__fields" }, It = { class: "yzh-search-bar__field-row" }, Ot = { class: "yzh-search-bar__label" }, Kt = { class: "yzh-search-bar__actions" }, jt = /* @__PURE__ */ le({
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
    const e = a, t = o, l = Ce({});
    _e(
      () => e.defaultValues,
      (c) => {
        c && (Object.keys(l).forEach((f) => delete l[f]), Object.assign(l, c));
      },
      { immediate: !0, deep: !0 }
    );
    const n = e.fields.slice(0, e.maxFields);
    function i() {
      const c = {};
      n.forEach((f) => {
        const d = l[f.prop];
        d !== void 0 && d !== "" && !(Array.isArray(d) && d.length === 0) && (c[f.prop] = d);
      }), t("search", c);
    }
    function s() {
      n.forEach((c) => {
        delete l[c.prop];
      }), t("reset");
    }
    return (c, f) => {
      const d = $("el-input"), b = $("el-input-number"), w = $("el-option"), m = $("el-select"), p = $("el-date-picker"), A = $("el-button");
      return y(), R("div", Et, [
        P("div", Mt, [
          P("div", Ut, [
            (y(!0), R(ae, null, ce(Fe(n), (h) => (y(), R("div", {
              key: h.prop,
              class: "yzh-search-bar__field"
            }, [
              P("div", It, [
                P("label", Ot, I(h.label), 1),
                P("div", {
                  class: "yzh-search-bar__input-wrap",
                  style: ze({ width: a.inputWidth })
                }, [
                  !h.type || h.type === "text" ? (y(), N(d, {
                    key: 0,
                    modelValue: l[h.prop],
                    "onUpdate:modelValue": (x) => l[h.prop] = x,
                    placeholder: h.placeholder || `请输入${h.label}`,
                    clearable: "",
                    onKeyup: At(i, ["enter"])
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : h.type === "number" ? (y(), N(b, {
                    key: 1,
                    modelValue: l[h.prop],
                    "onUpdate:modelValue": (x) => l[h.prop] = x,
                    placeholder: h.placeholder || `请输入${h.label}`
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : h.type === "select" ? (y(), N(m, {
                    key: 2,
                    modelValue: l[h.prop],
                    "onUpdate:modelValue": (x) => l[h.prop] = x,
                    placeholder: h.placeholder || `请选择${h.label}`,
                    clearable: "",
                    filterable: ""
                  }, {
                    default: k(() => [
                      (y(!0), R(ae, null, ce(h.options || [], (x) => (y(), N(w, {
                        key: x.value,
                        label: x.label,
                        value: x.value
                      }, null, 8, ["label", "value"]))), 128))
                    ]),
                    _: 2
                  }, 1032, ["modelValue", "onUpdate:modelValue", "placeholder"])) : h.type === "date" ? (y(), N(p, {
                    key: 3,
                    modelValue: l[h.prop],
                    "onUpdate:modelValue": (x) => l[h.prop] = x,
                    type: "date",
                    placeholder: h.placeholder || `请选择${h.label}`,
                    "value-format": "YYYY-MM-DD"
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : h.type === "dateRange" ? (y(), N(p, {
                    key: 4,
                    modelValue: l[h.prop],
                    "onUpdate:modelValue": (x) => l[h.prop] = x,
                    type: "daterange",
                    placeholder: h.placeholder || `请选择${h.label}`,
                    "value-format": "YYYY-MM-DD",
                    "range-separator": "至",
                    "start-placeholder": "开始日期",
                    "end-placeholder": "结束日期"
                  }, null, 8, ["modelValue", "onUpdate:modelValue", "placeholder"])) : H("", !0)
                ], 4)
              ])
            ]))), 128)),
            f[0] || (f[0] = P("div", { class: "yzh-search-bar__spacer" }, null, -1))
          ]),
          P("div", Kt, [
            L(A, {
              type: "primary",
              onClick: i
            }, {
              default: k(() => [...f[1] || (f[1] = [
                P("i", { class: "bi bi-search" }, null, -1),
                V(" 查询 ", -1)
              ])]),
              _: 1
            }),
            L(A, { onClick: s }, {
              default: k(() => [...f[2] || (f[2] = [
                P("i", { class: "bi bi-arrow-counterclockwise" }, null, -1),
                V(" 重置 ", -1)
              ])]),
              _: 1
            })
          ])
        ])
      ]);
    };
  }
}), Yt = /* @__PURE__ */ he(jt, [["__scopeId", "data-v-d0360654"]]), Wt = { class: "yzh-toolbar" }, qt = { class: "yzh-toolbar__left" }, Gt = { class: "yzh-toolbar__right" }, Ht = /* @__PURE__ */ le({
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
      const i = $("el-button");
      return y(), R("div", Wt, [
        P("div", qt, [
          (y(!0), R(ae, null, ce(a.buttons.filter((s) => s.group !== "right"), (s) => (y(), N(i, {
            key: s.key,
            type: s.type ?? "default",
            disabled: s.disabled,
            onClick: (c) => t(s)
          }, {
            default: k(() => [
              V(I(s.text), 1)
            ]),
            _: 2
          }, 1032, ["type", "disabled", "onClick"]))), 128)),
          W(l.$slots, "left", {}, void 0, !0)
        ]),
        P("div", Gt, [
          (y(!0), R(ae, null, ce(a.buttons.filter((s) => s.group === "right"), (s) => (y(), N(i, {
            key: s.key,
            type: s.type ?? "default",
            disabled: s.disabled,
            onClick: (c) => t(s)
          }, {
            default: k(() => [
              V(I(s.text), 1)
            ]),
            _: 2
          }, 1032, ["type", "disabled", "onClick"]))), 128)),
          W(l.$slots, "right", {}, void 0, !0)
        ])
      ]);
    };
  }
}), Xt = /* @__PURE__ */ he(Ht, [["__scopeId", "data-v-95dfd97a"]]), Jt = /* @__PURE__ */ le({
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
    const e = a, t = o, l = j({
      get: () => e.page,
      set: (i) => t("update:page", i)
    }), n = j({
      get: () => e.pageSize,
      set: (i) => t("update:pageSize", i)
    });
    return (i, s) => {
      const c = $("el-pagination");
      return y(), N(c, {
        "current-page": l.value,
        "onUpdate:currentPage": s[0] || (s[0] = (f) => l.value = f),
        "page-size": n.value,
        "onUpdate:pageSize": s[1] || (s[1] = (f) => n.value = f),
        total: a.total,
        "page-sizes": a.pageSizes,
        layout: a.layout,
        background: a.background,
        size: a.size
      }, null, 8, ["current-page", "page-size", "total", "page-sizes", "layout", "background", "size"]);
    };
  }
}), Zt = /* @__PURE__ */ he(Jt, [["__scopeId", "data-v-13879d57"]]), Qt = { class: "yzh-page-layout" }, eo = {
  key: 0,
  class: "yzh-page-layout__search"
}, to = {
  key: 1,
  class: "yzh-page-layout__toolbar"
}, oo = { class: "yzh-page-layout__toolbar-left" }, ao = { class: "yzh-page-layout__toolbar-right" }, lo = {
  key: 2,
  class: "yzh-page-layout__footer"
}, no = /* @__PURE__ */ le({
  __name: "YzhPageLayout",
  props: {
    pageTitle: {},
    helpText: {},
    showTitle: { type: Boolean },
    noPadding: { type: Boolean },
    hideToolbar: { type: Boolean }
  },
  setup(a) {
    return (o, e) => (y(), R("div", Qt, [
      o.$slots.search ? (y(), R("div", eo, [
        W(o.$slots, "search", {}, void 0, !0)
      ])) : H("", !0),
      !a.hideToolbar && (o.$slots.toolbar || o.$slots["toolbar-left"] || o.$slots["toolbar-right"]) ? (y(), R("div", to, [
        W(o.$slots, "toolbar", {}, () => [
          P("div", oo, [
            W(o.$slots, "toolbar-left", {}, void 0, !0)
          ]),
          P("div", ao, [
            W(o.$slots, "toolbar-right", {}, void 0, !0)
          ])
        ], !0)
      ])) : H("", !0),
      P("div", {
        class: ke(["yzh-page-layout__content", { "yzh-page-layout__content--no-padding": a.noPadding }])
      }, [
        W(o.$slots, "default", {}, void 0, !0)
      ], 2),
      o.$slots.pagination ? (y(), R("div", lo, [
        W(o.$slots, "pagination", {}, void 0, !0)
      ])) : H("", !0)
    ]));
  }
}), ja = /* @__PURE__ */ he(no, [["__scopeId", "data-v-756b5466"]]), so = { class: "yzh-dialog__body" }, ro = { class: "yzh-dialog__footer" }, io = /* @__PURE__ */ le({
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
    const e = a, t = o, l = j(() => typeof e.width == "number" ? `${e.width}px` : e.width);
    function n() {
      t("update:modelValue", !1), t("close");
    }
    function i() {
      e.confirmDisabled || e.confirmLoading || t("confirm");
    }
    function s() {
      t("cancel"), n();
    }
    return _e(
      () => e.modelValue,
      (c) => {
        c && t("open");
      }
    ), (c, f) => {
      const d = $("el-button"), b = $("el-dialog");
      return y(), N(b, {
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
        "onUpdate:modelValue": f[0] || (f[0] = (w) => t("update:modelValue", w))
      }, at({
        default: k(() => [
          P("div", so, [
            W(c.$slots, "default", {}, void 0, !0)
          ])
        ]),
        _: 2
      }, [
        a.showFooter ? {
          name: "footer",
          fn: k(() => [
            W(c.$slots, "footer", {
              confirm: i,
              cancel: s
            }, () => [
              P("div", ro, [
                L(d, { onClick: s }, {
                  default: k(() => [
                    V(I(a.cancelText), 1)
                  ]),
                  _: 1
                }),
                L(d, {
                  type: a.confirmType,
                  disabled: a.confirmDisabled,
                  loading: a.confirmLoading,
                  onClick: i
                }, {
                  default: k(() => [
                    V(I(a.confirmText), 1)
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
}), Ya = /* @__PURE__ */ he(io, [["__scopeId", "data-v-dbdcca59"]]);
/*! Element Plus Icons Vue v2.3.2 */
var co = /* @__PURE__ */ le({
  name: "Document",
  __name: "document",
  setup(a) {
    return (o, e) => (y(), R("svg", {
      xmlns: "http://www.w3.org/2000/svg",
      viewBox: "0 0 1024 1024"
    }, [
      P("path", {
        fill: "currentColor",
        d: "M832 384H576V128H192v768h640zm-26.496-64L640 154.496V320zM160 64h480l256 256v608a32 32 0 0 1-32 32H160a32 32 0 0 1-32-32V96a32 32 0 0 1 32-32m160 448h384v64H320zm0-192h160v64H320zm0 384h384v64H320z"
      })
    ]));
  }
}), uo = co, ho = /* @__PURE__ */ le({
  name: "Folder",
  __name: "folder",
  setup(a) {
    return (o, e) => (y(), R("svg", {
      xmlns: "http://www.w3.org/2000/svg",
      viewBox: "0 0 1024 1024"
    }, [
      P("path", {
        fill: "currentColor",
        d: "M128 192v640h768V320H485.76L357.504 192zm-32-64h287.872l128.384 128H928a32 32 0 0 1 32 32v576a32 32 0 0 1-32 32H96a32 32 0 0 1-32-32V160a32 32 0 0 1 32-32"
      })
    ]));
  }
}), fo = ho;
const po = { class: "yzh-tree" }, mo = {
  key: 0,
  class: "yzh-tree__search"
}, yo = ["onMouseenter"], go = {
  key: 1,
  class: "yzh-tree__icon"
}, vo = {
  key: 4,
  class: "yzh-tree__badge"
}, bo = /* @__PURE__ */ le({
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
    function t(r) {
      return /[\u{1F300}-\u{1F9FF}]|[\u{2600}-\u{26FF}]|[\u{2700}-\u{27BF}]/u.test(r);
    }
    function l(r, u) {
      if (!r) return;
      const g = u.charAt(0).toLowerCase() + u.slice(1);
      return r[u] ?? r[g];
    }
    const n = a, i = e, s = T(), c = T(""), f = T(null);
    function d(r) {
      return String(l(r, n.nodeKey) ?? "");
    }
    function b(r) {
      return String(l(r, n.labelField) ?? "");
    }
    function w(r) {
      return l(r, n.childrenField) ?? [];
    }
    function m(r) {
      return l(r, n.isLeafField) === !0;
    }
    function p(r) {
      return l(r, n.extraField) ?? {};
    }
    const A = j(() => ({
      label: n.labelField,
      children: n.childrenField,
      // 必须读叶子字段（后端 TreeControllerBase.FillIsLeafBatch 批量计算）。
      // 读错字段会让末端节点也长出展开箭头并白跑一次 tree/children。
      isLeaf: (r) => m(r),
      disabled: (r) => p(r).disabled ?? !1
    }));
    function h(r, u) {
      return r ? (b(u) || "").toLowerCase().includes(String(r).toLowerCase()) : !0;
    }
    function x(r) {
      return c.value ? (b(r) || "").toLowerCase().includes(c.value.toLowerCase()) : !1;
    }
    let K = null;
    _e(c, (r) => {
      K && clearTimeout(K), K = setTimeout(() => {
        var u;
        (u = s.value) == null || u.filter(r);
      }, 200);
    });
    function ee(r) {
      let u;
      typeof n.nodeActions == "function" ? u = n.nodeActions(r) || [] : u = n.nodeActions;
      const g = Object.entries(n.legacyNodeActions || {}).map(([C, S]) => ({
        key: C,
        text: n.getActionLabel ? n.getActionLabel(C, r) : S
      }));
      return [...u, ...g].filter((C) => C.visible !== !1);
    }
    function ne(r) {
      return r.danger ? "yzh-tree__action-danger" : r.type === "warning" ? "yzh-tree__action-toggle" : "";
    }
    function re(r) {
      i("node-click", r);
    }
    function se() {
      if (!s.value) return;
      const r = s.value.getCheckedNodes();
      i("check-change", r);
    }
    function F(r) {
      i("node-expand", r);
    }
    function E(r) {
      i("node-collapse", r);
    }
    function q(r, u) {
      i("node-action", r, u);
    }
    function G() {
      var r;
      return ((r = s.value) == null ? void 0 : r.getCheckedNodes()) ?? [];
    }
    function X(r) {
      var u;
      (u = s.value) == null || u.setCheckedNodes(r);
    }
    function te(r, u) {
      var g;
      (g = s.value) == null || g.setChecked(r, u, !1);
    }
    function Z() {
      const r = (u) => {
        var g;
        for (const C of u) {
          const S = (g = s.value) == null ? void 0 : g.store;
          S && S.nodesMap[d(C)] && (S.nodesMap[d(C)].expanded = !0), w(C).length && r(w(C));
        }
      };
      r(n.data);
    }
    function me() {
      const r = (u) => {
        var g;
        for (const C of u) {
          const S = (g = s.value) == null ? void 0 : g.store;
          S && S.nodesMap[d(C)] && (S.nodesMap[d(C)].expanded = !1), w(C).length && r(w(C));
        }
      };
      r(n.data);
    }
    function fe(r) {
      var u;
      (u = s.value) == null || u.setCurrentKey(r);
    }
    function pe(r, u) {
      var g;
      if (s.value) {
        if (r) {
          try {
            s.value.append(u, r);
            return;
          } catch {
          }
          const C = s.value.store, S = (g = C == null ? void 0 : C.nodesMap) == null ? void 0 : g[r];
          if (S && typeof S.append == "function") {
            S.append(u);
            return;
          }
          if (we(n.data, r, u)) return;
        }
        n.data.push(u);
      }
    }
    function we(r, u, g) {
      for (const C of r) {
        if (d(C) === u) {
          const S = w(C);
          return S.push(g), C[n.childrenField] = S, C[n.isLeafField] = !1, !0;
        }
        if (w(C).length && we(w(C), u, g))
          return !0;
      }
      return !1;
    }
    o({
      getCheckedNodes: G,
      setCheckedNodes: X,
      setChecked: te,
      expandAll: Z,
      collapseAll: me,
      setCurrentNode: fe,
      appendNode: pe,
      /** 从树中移除指定节点（不触发 API，仅更新本地树 UI） */
      removeNode: (r, u) => {
        var S;
        if (!s.value) return;
        try {
          s.value.remove(u);
          return;
        } catch {
        }
        const g = s.value.store, C = (S = g == null ? void 0 : g.nodesMap) == null ? void 0 : S[u];
        if (C && C.parentNode) {
          C.parentNode.remove(C);
          return;
        }
        ie(n.data, u);
      }
    });
    function ie(r, u) {
      for (let g = 0; g < r.length; g++) {
        if (d(r[g]) === u)
          return r.splice(g, 1), !0;
        if (w(r[g]).length && ie(w(r[g]), u))
          return !0;
      }
      return !1;
    }
    return (r, u) => {
      const g = $("el-icon"), C = $("el-button"), S = $("el-dropdown-item"), M = $("el-dropdown-menu"), Q = $("el-dropdown");
      return y(), R("div", po, [
        a.searchable ? (y(), R("div", mo, [
          L(Fe(Ge), {
            modelValue: c.value,
            "onUpdate:modelValue": u[0] || (u[0] = (D) => c.value = D),
            placeholder: a.searchPlaceholder,
            clearable: "",
            "prefix-icon": "Search",
            size: "small"
          }, null, 8, ["modelValue", "placeholder"])
        ])) : H("", !0),
        L(Fe(Bt), {
          ref_key: "treeRef",
          ref: s,
          data: a.data,
          props: A.value,
          "show-checkbox": a.showCheckbox,
          "check-strictly": a.checkStrictly,
          lazy: a.lazy,
          load: a.loadData,
          "default-expand-all": a.defaultExpandAll,
          "expand-on-click-node": a.expandOnClickNode,
          "highlight-current": a.highlightCurrent,
          "node-key": a.nodeKey,
          "current-node-key": a.currentKey,
          "filter-node-method": h,
          "empty-text": "暂无数据",
          class: "yzh-tree__inner",
          onNodeClick: re,
          onCheckChange: se,
          onNodeExpand: F,
          onNodeCollapse: E
        }, {
          default: k(({ data: D }) => [
            P("div", {
              class: "yzh-tree__node",
              onMouseenter: (Y) => f.value = d(D),
              onMouseleave: u[2] || (u[2] = (Y) => f.value = null)
            }, [
              p(D).icon && !t(p(D).icon) ? (y(), N(g, {
                key: 0,
                class: "yzh-tree__icon"
              }, {
                default: k(() => [
                  (y(), N(Ee(p(D).icon)))
                ]),
                _: 2
              }, 1024)) : p(D).icon ? (y(), R("span", go, I(p(D).icon), 1)) : m(D) ? (y(), N(g, {
                key: 2,
                class: "yzh-tree__icon yzh-tree__icon--leaf"
              }, {
                default: k(() => [
                  L(Fe(uo))
                ]),
                _: 1
              })) : (y(), N(g, {
                key: 3,
                class: "yzh-tree__icon yzh-tree__icon--folder"
              }, {
                default: k(() => [
                  L(Fe(fo))
                ]),
                _: 1
              })),
              P("span", {
                class: ke(["yzh-tree__label", { "is-highlight": a.highlightKeyword && x(D) }])
              }, I(b(D)), 3),
              p(D).badge ? (y(), R("span", vo, I(p(D).badge), 1)) : H("", !0),
              ee(D).length ? (y(), N(Q, {
                key: 5,
                trigger: "click",
                onCommand: (Y) => q(Y, D),
                onClick: u[1] || (u[1] = Ft(() => {
                }, ["stop"]))
              }, {
                dropdown: k(() => [
                  L(M, null, {
                    default: k(() => [
                      (y(!0), R(ae, null, ce(ee(D), (Y) => (y(), N(S, {
                        key: Y.key,
                        command: Y.key,
                        disabled: Y.disabled,
                        class: ke(ne(Y))
                      }, {
                        default: k(() => [
                          V(I(Y.text), 1)
                        ]),
                        _: 2
                      }, 1032, ["command", "disabled", "class"]))), 128))
                    ]),
                    _: 2
                  }, 1024)
                ]),
                default: k(() => [
                  L(C, {
                    link: "",
                    size: "small",
                    class: "yzh-tree__more-btn"
                  }, {
                    default: k(() => [...u[3] || (u[3] = [
                      V(" ⋯ ", -1)
                    ])]),
                    _: 1
                  })
                ]),
                _: 2
              }, 1032, ["onCommand"])) : H("", !0)
            ], 40, yo)
          ]),
          _: 1
        }, 8, ["data", "props", "show-checkbox", "check-strictly", "lazy", "load", "default-expand-all", "expand-on-click-node", "highlight-current", "node-key", "current-node-key"])
      ]);
    };
  }
}), lt = /* @__PURE__ */ he(bo, [["__scopeId", "data-v-ef58d20f"]]), Co = { class: "yzh-tree-table" }, wo = { class: "yzh-tree-table__main" }, ko = {
  key: 0,
  class: "yzh-tree-table__tree-toolbar"
}, To = {
  key: 1,
  class: "yzh-tree-table__tree-footer"
}, xo = { class: "yzh-tree-table__table-panel" }, So = /* @__PURE__ */ le({
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
    const t = a, l = e, n = T(), i = T(""), s = j(() => i.value ? m(t.treeData, i.value) : t.treeData);
    function c(p) {
      l("tree-node-click", p);
    }
    function f(p) {
      l("tree-check-change", p);
    }
    function d(p, A) {
      l("tree-node-action", p, A);
    }
    function b() {
      var p;
      (p = n.value) == null || p.expandAll();
    }
    function w() {
      var p;
      (p = n.value) == null || p.collapseAll();
    }
    function m(p, A) {
      const h = A.toLowerCase(), x = [];
      for (const K of p) {
        const ne = String(K[t.labelField] ?? "").toLowerCase().includes(h), re = K[t.childrenField] ?? [], se = m(re, A);
        (ne || se.length > 0) && x.push({ ...K, [t.childrenField]: se });
      }
      return x;
    }
    return o({
      treeRef: n,
      getCheckedNodes: () => {
        var p;
        return ((p = n.value) == null ? void 0 : p.getCheckedNodes()) ?? [];
      },
      expandAll: b,
      collapseAll: w,
      appendNode: (p, A) => {
        var h;
        return (h = n.value) == null ? void 0 : h.appendNode(p, A);
      },
      removeNode: (p, A) => {
        var h;
        return (h = n.value) == null ? void 0 : h.removeNode(p, A);
      }
    }), (p, A) => (y(), R("div", Co, [
      P("div", wo, [
        P("div", {
          class: "yzh-tree-table__tree-panel",
          style: ze({ width: a.treeWidth + "px" })
        }, [
          a.treeToolbar ? (y(), R("div", ko, [
            a.treeSearchable ? (y(), N(Fe(Ge), {
              key: 0,
              modelValue: i.value,
              "onUpdate:modelValue": A[0] || (A[0] = (h) => i.value = h),
              placeholder: "搜索节点",
              clearable: "",
              "prefix-icon": "Search"
            }, null, 8, ["modelValue"])) : H("", !0)
          ])) : H("", !0),
          L(lt, {
            ref_key: "treeRef",
            ref: n,
            data: s.value,
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
            onCheckChange: f,
            onNodeAction: d
          }, null, 8, ["data", "node-key", "label-field", "children-field", "is-leaf-field", "extra-field", "show-checkbox", "check-strictly", "lazy", "load-data", "default-expand-all", "node-actions", "legacy-node-actions", "get-action-label"]),
          p.$slots.treeFooter ? (y(), R("div", To, [
            W(p.$slots, "treeFooter", {}, void 0, !0)
          ])) : H("", !0)
        ], 4),
        P("div", xo, [
          W(p.$slots, "default", {}, void 0, !0)
        ])
      ])
    ]));
  }
}), Wa = /* @__PURE__ */ he(So, [["__scopeId", "data-v-d15619e1"]]), _o = { class: "yzh-table" }, Ao = { class: "yzh-column-settings" }, Fo = { class: "yzh-column-settings__body" }, zo = { class: "yzh-column-settings__footer" }, No = { key: 1 }, Do = { class: "yzh-table__empty" }, $o = {
  key: 1,
  class: "yzh-table__error"
}, Ro = {
  key: 2,
  class: "yzh-table__pagination"
}, Bo = /* @__PURE__ */ le({
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
    defaultExpandAll: { type: Boolean, default: !1 },
    treeProps: { default: void 0 }
  },
  emits: ["selection-change", "row-click", "refresh", "row-action", "toolbar-action", "expand-change"],
  setup(a, { expose: o, emit: e }) {
    const t = a, l = e, n = T(!1), i = T(""), s = T([]), c = T(0), f = T([]), d = T(1), b = T(t.pageSize), w = T(t.defaultSort || null), m = Ce({}), p = T(/* @__PURE__ */ new Set()), A = j(() => t.selectMode ? t.selectMode : t.selectable ? "multiple" : "none"), h = j(() => A.value === "multiple"), x = j(
      () => t.columns.filter((v) => v.label && v.prop !== "__yzh_action")
    ), K = j(
      () => t.columns.filter((v) => !(v.hidden || p.value.has(v.prop)))
    );
    function ee(v) {
      return Object.entries(v).map(([_, U]) => ({ key: _, text: U }));
    }
    function ne(v) {
      const _ = typeof t.rowActionButtons == "function" ? t.rowActionButtons(v) : t.rowActionButtons;
      return (Array.isArray(_) ? _ : ee(_ || {})).filter((oe) => oe.visible !== !1);
    }
    const re = j(() => {
      const v = t.columns.some((U) => U.prop === "actions");
      return (typeof t.rowActionButtons == "function" || (Array.isArray(t.rowActionButtons) ? t.rowActionButtons.length : Object.keys(t.rowActionButtons || {}).length) > 0) && !v;
    }), se = j(() => t.actionMaxInline > 0);
    function F(v) {
      return !se.value || v.length <= t.actionMaxInline ? { inline: v, overflow: [] } : { inline: v.slice(0, t.actionMaxInline), overflow: v.slice(t.actionMaxInline) };
    }
    const E = j(
      () => t.toolbarActions.filter((v) => v.visible !== !1)
    );
    async function q(v, _) {
      if (!v.disabled) {
        if (v.confirm)
          try {
            await Se.confirm(v.confirm, "操作确认", { type: "warning" });
          } catch {
            return;
          }
        l("row-action", v.key, _, v);
      }
    }
    async function G(v) {
      if (!v.disabled) {
        if (v.confirm)
          try {
            await Se.confirm(v.confirm, "操作确认", { type: "warning" });
          } catch {
            return;
          }
        l("toolbar-action", v.key, v);
      }
    }
    function X(v, _) {
      _ ? p.value.delete(v.prop) : p.value.add(v.prop), p.value = new Set(p.value);
    }
    function te(v) {
      if (v.sortable === !1) return;
      const _ = v.prop;
      w.value && w.value.prop === _ ? w.value = { ...w.value, order: w.value.order === "asc" ? "desc" : "asc" } : w.value = { prop: _, order: "asc" };
    }
    function Z(v) {
      const _ = v.prop;
      return !w.value || w.value.prop !== _ ? "排序" : w.value.order === "asc" ? "↑ 升序" : "↓ 降序";
    }
    function me() {
      p.value = /* @__PURE__ */ new Set(), w.value = t.defaultSort || null;
    }
    function fe() {
      ie();
    }
    const pe = j(() => t.toolbar === !1 ? {} : t.toolbar === !0 ? { columnSetting: !0 } : t.toolbar), we = j(() => Object.keys(pe.value).length > 0 || E.value.length > 0);
    async function ie() {
      n.value = !0, i.value = "";
      try {
        const v = new Set(f.value.map((oe) => oe[t.rowKey])), _ = {
          page: d.value,
          rows: b.value,
          ...w.value ? { sort: w.value.prop, order: w.value.order } : {},
          ...m
        }, U = await t.dataLoader(_);
        if (s.value = U.rows || [], c.value = U.total || 0, v.size > 0) {
          const oe = [];
          for (const Ve of s.value)
            v.has(Ve[t.rowKey]) && oe.push(Ve);
          f.value = oe;
        }
      } catch (v) {
        i.value = (v == null ? void 0 : v.message) || "数据加载失败", s.value = [], c.value = 0, J.error(i.value);
      } finally {
        n.value = !1;
      }
    }
    function r({ prop: v, order: _ }) {
      _ ? w.value = {
        prop: v,
        order: _ === "ascending" ? "asc" : "desc"
      } : w.value = null, ie();
    }
    function u(v) {
      d.value = v, ie();
    }
    function g(v) {
      b.value = v, d.value = 1, ie();
    }
    function C(v) {
      Object.assign(m, v), d.value = 1, ie();
    }
    function S() {
      Object.keys(m).forEach((v) => delete m[v]), t.searchFields && t.searchFields.slice(0, t.searchMaxFields).forEach((v) => {
        v.defaultValue !== void 0 && (m[v.prop] = v.defaultValue);
      }), d.value = 1, ie();
    }
    function M(v) {
      f.value = v, l("selection-change", v);
    }
    function Q(v, _) {
      l("row-click", v, _);
    }
    zt();
    let D = !1;
    const Y = j(() => {
      if (typeof t.rowActionButtons == "function")
        return 4 * 70 + 40;
      const v = Array.isArray(t.rowActionButtons) ? t.rowActionButtons.length : Object.keys(t.rowActionButtons || {}).length;
      return v > 0 ? v * 70 + 40 : 140;
    });
    _e(
      () => typeof t.rowActionButtons == "function" ? 1 : Array.isArray(t.rowActionButtons) ? t.rowActionButtons.length : Object.keys(t.rowActionButtons || {}).length,
      (v) => {
      },
      { immediate: !0 }
    );
    function ye() {
      d.value = 1, ie(), l("refresh");
    }
    Be(() => {
      t.searchFields && t.searchFields.slice(0, t.searchMaxFields).forEach((v) => {
        v.defaultValue !== void 0 && (m[v.prop] = v.defaultValue);
      }), ie();
    });
    function $e(v, _ = "top") {
      _ === "top" ? s.value.unshift(v) : s.value.push(v), c.value++;
    }
    function de(v, _) {
      const U = s.value.findIndex((oe) => v(oe));
      U >= 0 && s.value.splice(U, 1, _);
    }
    function Ae(v) {
      const _ = s.value.findIndex((U) => v(U));
      _ >= 0 && (s.value.splice(_, 1), c.value = Math.max(0, c.value - 1));
    }
    function Te() {
      return s.value.length;
    }
    function Pe(v, _) {
      if (_) {
        const U = new Set(f.value);
        for (const oe of s.value)
          v(oe) && !U.has(oe) && f.value.push(oe);
      } else
        f.value = f.value.filter((U) => !v(U));
      l("selection-change", [...f.value]);
    }
    const Ie = T(), Qe = j(() => {
      var v;
      return ((v = t.treeProps) == null ? void 0 : v.children) || "children";
    }), mt = j(() => {
      var v;
      return {
        children: Qe.value,
        hasChildren: ((v = t.treeProps) == null ? void 0 : v.hasChildren) || "hasChildren",
        ...t.treeProps
      };
    });
    function Oe(v, _) {
      for (const U of v) {
        _(U);
        const oe = U[Qe.value];
        Array.isArray(oe) && oe.length && Oe(oe, _);
      }
    }
    function yt() {
      Oe(s.value, (v) => {
        var _, U;
        return (U = (_ = Ie.value) == null ? void 0 : _.toggleRowExpansion) == null ? void 0 : U.call(_, v, !0);
      });
    }
    function gt() {
      Oe(s.value, (v) => {
        var _, U;
        return (U = (_ = Ie.value) == null ? void 0 : _.toggleRowExpansion) == null ? void 0 : U.call(_, v, !1);
      });
    }
    function vt(v, _) {
      l("expand-change", v, _);
    }
    return o({
      refresh: ye,
      loadData: ie,
      insertRow: $e,
      replaceRow: de,
      removeRow: Ae,
      getRowCount: Te,
      getSelectedRows: () => f.value,
      setCheckedRows: Pe,
      clearSelection: () => {
        f.value = [], l("selection-change", []);
      },
      expandAll: yt,
      collapseAll: gt
    }), (v, _) => {
      const U = $("el-button"), oe = $("el-checkbox"), Ve = $("el-popover"), Ke = $("el-table-column"), et = $("el-tag"), bt = $("el-dropdown-item"), Ct = $("el-dropdown-menu"), wt = $("el-dropdown"), kt = $("el-empty"), Tt = $("el-table"), xt = Nt("loading");
      return y(), R("div", _o, [
        a.searchFields && a.searchFields.length ? (y(), N(Yt, {
          key: 0,
          fields: a.searchFields,
          "default-values": m,
          cols: 2,
          "max-fields": a.searchMaxFields,
          onSearch: C,
          onReset: S
        }, null, 8, ["fields", "default-values", "max-fields"])) : H("", !0),
        we.value ? (y(), N(Xt, {
          key: 1,
          buttons: E.value,
          onAction: _[0] || (_[0] = (B, O) => G(O))
        }, {
          left: k(() => [
            W(v.$slots, "toolbar-left", {}, void 0, !0)
          ]),
          right: k(() => [
            W(v.$slots, "toolbar-right", {
              selected: f.value,
              refresh: ye
            }, () => [
              pe.value.columnSetting ? (y(), N(Ve, {
                key: 0,
                trigger: "click",
                placement: "bottom-end",
                width: 200
              }, {
                reference: k(() => [
                  L(U, { text: "" }, {
                    default: k(() => [..._[1] || (_[1] = [
                      P("i", { class: "bi bi-columns" }, null, -1),
                      V(" 列设置 ", -1)
                    ])]),
                    _: 1
                  })
                ]),
                default: k(() => [
                  P("div", Ao, [
                    _[4] || (_[4] = P("div", { class: "yzh-column-settings__header" }, "列筛选与排序", -1)),
                    P("div", Fo, [
                      (y(!0), R(ae, null, ce(x.value, (B) => {
                        var O;
                        return y(), R("div", {
                          key: B.prop,
                          class: "yzh-column-settings__item"
                        }, [
                          L(oe, {
                            "model-value": !p.value.has(B.prop) && !B.hidden,
                            onChange: (ge) => X(B, ge)
                          }, {
                            default: k(() => [
                              V(I(B.label), 1)
                            ]),
                            _: 2
                          }, 1032, ["model-value", "onChange"]),
                          L(U, {
                            class: ke(["yzh-column-settings__sort-btn", { "is-active": ((O = w.value) == null ? void 0 : O.prop) === B.prop }]),
                            disabled: B.sortable === !1,
                            onClick: (ge) => te(B)
                          }, {
                            default: k(() => [
                              V(I(Z(B)), 1)
                            ]),
                            _: 2
                          }, 1032, ["class", "disabled", "onClick"])
                        ]);
                      }), 128))
                    ]),
                    P("div", zo, [
                      L(U, {
                        size: "small",
                        onClick: me
                      }, {
                        default: k(() => [..._[2] || (_[2] = [
                          V("重置", -1)
                        ])]),
                        _: 1
                      }),
                      L(U, {
                        size: "small",
                        type: "primary",
                        onClick: fe
                      }, {
                        default: k(() => [..._[3] || (_[3] = [
                          V("确定", -1)
                        ])]),
                        _: 1
                      })
                    ])
                  ])
                ]),
                _: 1
              })) : H("", !0)
            ], !0)
          ]),
          _: 3
        }, 8, ["buttons"])) : H("", !0),
        P("div", {
          class: ke(["yzh-table__wrapper", { "yzh-table__wrapper--no-padding": a.noPadding }])
        }, [
          P("div", {
            class: "yzh-table__body",
            style: ze(a.height ? { height: typeof a.height == "number" ? a.height + "px" : a.height } : {})
          }, [
            Dt((y(), N(Tt, {
              ref_key: "tableRef",
              ref: Ie,
              data: s.value,
              "row-key": a.rowKey,
              "default-expand-all": a.defaultExpandAll,
              "tree-props": mt.value,
              height: a.height !== void 0 && a.height !== null ? a.height : "100%",
              "highlight-current-row": A.value === "single",
              stripe: "",
              border: "",
              onSelectionChange: M,
              onSortChange: r,
              onRowClick: Q,
              onExpandChange: vt
            }, {
              empty: k(() => [
                P("div", Do, [
                  !n.value && !i.value ? (y(), N(kt, {
                    key: 0,
                    description: a.emptyText
                  }, null, 8, ["description"])) : i.value ? (y(), R("div", $o, [
                    _[7] || (_[7] = P("i", { class: "bi bi-exclamation-triangle" }, null, -1)),
                    P("span", null, I(i.value), 1),
                    L(U, {
                      text: "",
                      type: "primary",
                      onClick: ye
                    }, {
                      default: k(() => [..._[6] || (_[6] = [
                        V("重试", -1)
                      ])]),
                      _: 1
                    })
                  ])) : H("", !0)
                ])
              ]),
              default: k(() => [
                h.value ? (y(), N(Ke, {
                  key: 0,
                  type: "selection",
                  width: "48",
                  "reserve-selection": !1
                })) : H("", !0),
                (y(!0), R(ae, null, ce(K.value, (B) => (y(), N(Ke, {
                  key: B.prop,
                  prop: B.prop,
                  label: B.label,
                  width: B.width,
                  "min-width": B.minWidth,
                  fixed: B.fixed,
                  sortable: B.sortable,
                  align: B.align || "left",
                  "show-overflow-tooltip": !B.slot,
                  "class-name": B.className
                }, {
                  default: k(({ row: O, $index: ge }) => {
                    var Le;
                    return [
                      B.slot ? W(v.$slots, `column-${String(B.prop)}`, {
                        row: O,
                        index: ge,
                        value: O[B.prop]
                      }, () => [
                        V(I(B.formatter ? B.formatter(O[B.prop], O, ge) : O[B.prop]), 1)
                      ], !0, 0) : B.dictCode ? (y(), R(ae, { key: 1 }, [
                        B.tagType ? (y(), N(et, {
                          key: 0,
                          type: B.tagType,
                          "disable-transitions": ""
                        }, {
                          default: k(() => [
                            V(I(O[B.prop]), 1)
                          ]),
                          _: 2
                        }, 1032, ["type"])) : (y(), R("span", No, I(O[B.prop]), 1))
                      ], 64)) : B.tagMap ? (y(), N(et, {
                        key: 2,
                        type: ((Le = B.tagTypeMap) == null ? void 0 : Le[O[B.prop]]) ?? "info",
                        size: "small",
                        "disable-transitions": ""
                      }, {
                        default: k(() => [
                          V(I(B.tagMap[O[B.prop]] ?? O[B.prop]), 1)
                        ]),
                        _: 2
                      }, 1032, ["type"])) : (y(), R(ae, { key: 3 }, [
                        V(I(B.formatter ? B.formatter(O[B.prop], O, ge) : O[B.prop]), 1)
                      ], 64))
                    ];
                  }),
                  _: 2
                }, 1032, ["prop", "label", "width", "min-width", "fixed", "sortable", "align", "show-overflow-tooltip", "class-name"]))), 128)),
                re.value ? (y(), N(Ke, {
                  key: 1,
                  label: "操作",
                  width: Y.value,
                  fixed: "right",
                  align: "center"
                }, {
                  default: k(({ row: B }) => [
                    (y(!0), R(ae, null, ce(F(ne(B)).inline, (O) => (y(), N(U, {
                      key: O.key,
                      link: a.rowActionLink,
                      size: "small",
                      type: O.type ?? "primary",
                      disabled: O.disabled,
                      onClick: (ge) => q(O, B)
                    }, {
                      default: k(() => [
                        V(I(O.text), 1)
                      ]),
                      _: 2
                    }, 1032, ["link", "type", "disabled", "onClick"]))), 128)),
                    F(ne(B)).overflow.length > 0 ? (y(), N(wt, {
                      key: 0,
                      trigger: "click",
                      onCommand: (O) => {
                        const ge = F(ne(B)).overflow.find((Le) => Le.key === O);
                        ge && q(ge, B);
                      }
                    }, {
                      dropdown: k(() => [
                        L(Ct, null, {
                          default: k(() => [
                            (y(!0), R(ae, null, ce(F(ne(B)).overflow, (O) => (y(), N(bt, {
                              key: O.key,
                              command: O.key,
                              disabled: O.disabled,
                              class: ke({ "yzh-row-action-danger": O.type === "danger" })
                            }, {
                              default: k(() => [
                                V(I(O.text), 1)
                              ]),
                              _: 2
                            }, 1032, ["command", "disabled", "class"]))), 128))
                          ]),
                          _: 2
                        }, 1024)
                      ]),
                      default: k(() => [
                        L(U, {
                          link: "",
                          size: "small"
                        }, {
                          default: k(() => [..._[5] || (_[5] = [
                            V("更多", -1)
                          ])]),
                          _: 1
                        })
                      ]),
                      _: 2
                    }, 1032, ["onCommand"])) : H("", !0)
                  ]),
                  _: 1
                }, 8, ["width"])) : H("", !0)
              ]),
              _: 3
            }, 8, ["data", "row-key", "default-expand-all", "tree-props", "height", "highlight-current-row"])), [
              [xt, n.value]
            ])
          ], 4)
        ], 2),
        a.showPagination ? (y(), R("div", Ro, [
          L(Zt, {
            page: d.value,
            "page-size": b.value,
            total: c.value,
            "onUpdate:page": u,
            "onUpdate:pageSize": g
          }, null, 8, ["page", "page-size", "total"])
        ])) : H("", !0)
      ]);
    };
  }
}), nt = /* @__PURE__ */ he(Bo, [["__scopeId", "data-v-38f64d87"]]), Po = { class: "yzh-tree-table-selector" }, Vo = {
  key: 0,
  class: "yzh-tree-table-selector__tree-search"
}, Lo = { class: "yzh-tree-table-selector__tree-actions" }, Eo = {
  key: 1,
  class: "yzh-tree-table-selector__tree-footer"
}, Mo = { class: "yzh-tree-table-selector__table-panel" }, Uo = { class: "yzh-tree-table-selector__table-toolbar" }, Io = { class: "yzh-tree-table-selector__selection-info" }, Oo = /* @__PURE__ */ le({
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
    const t = a, l = e, n = T(), i = T(), s = T(""), c = T([]), f = T([]), d = T(/* @__PURE__ */ new Map()), b = j(() => s.value ? se(t.treeData, s.value) : t.treeData);
    function w() {
      var F;
      (F = n.value) == null || F.expandAll();
    }
    function m() {
      var F;
      (F = n.value) == null || F.collapseAll();
    }
    function p() {
      const F = (E) => {
        var q;
        for (const G of E)
          (q = n.value) == null || q.setChecked(G.Code, !0), G.Children && G.Children.length > 0 && F(G.Children);
      };
      F(t.treeData);
    }
    function A() {
      var F;
      (F = n.value) == null || F.setCheckedNodes([]);
    }
    function h(F) {
      K(F.Code);
    }
    async function x() {
      if (!n.value) return;
      const F = n.value.getCheckedNodes();
      c.value = F;
      const E = F.map((te) => te.Code), q = [];
      for (const te of E) {
        const Z = await K(te);
        Z && q.push(...Z);
      }
      const G = /* @__PURE__ */ new Set(), X = q.filter((te) => {
        const Z = te[t.rowKey];
        return G.has(Z) ? !1 : (G.add(Z), !0);
      });
      f.value = X, i.value && i.value.setCheckedRows(
        (te) => X.some((Z) => Z[t.rowKey] === te[t.rowKey]),
        !0
      ), l("update:checkedTreeNodes", F), l("update:checkedTableRows", X), l("tree-check-change", F);
    }
    async function K(F) {
      if (d.value.has(F))
        return d.value.get(F);
      try {
        const q = (await t.loadTableData(F)).rows ?? [];
        return d.value.set(F, q), q;
      } catch (E) {
        return J.error(E.message || "加载表格数据失败"), null;
      }
    }
    async function ee(F) {
      if (c.value.length === 0)
        return { rows: [], total: 0 };
      const E = [];
      for (const me of c.value) {
        const fe = await K(me.Code);
        fe && E.push(...fe);
      }
      const q = /* @__PURE__ */ new Set(), G = E.filter((me) => {
        const fe = me[t.rowKey];
        return q.has(fe) ? !1 : (q.add(fe), !0);
      }), X = (F.page - 1) * F.rows, te = X + F.rows;
      return { rows: G.slice(X, te), total: G.length };
    }
    function ne(F) {
      f.value = F, l("update:checkedTableRows", F), l("selection-change", F);
    }
    function re() {
      var F;
      (F = i.value) == null || F.clearSelection(), A(), c.value = [], f.value = [], d.value.clear(), l("update:checkedTreeNodes", []), l("update:checkedTableRows", []);
    }
    function se(F, E) {
      const q = E.toLowerCase(), G = [];
      for (const X of F) {
        const te = (X.Name || "").toLowerCase().includes(q), Z = se(X.Children ?? [], E);
        (te || Z.length > 0) && G.push({ ...X, Children: Z });
      }
      return G;
    }
    return o({
      getCheckedTreeNodes: () => c.value,
      getCheckedTableRows: () => f.value,
      clearSelection: re,
      refreshTable: () => {
        var F;
        return (F = i.value) == null ? void 0 : F.refresh();
      }
    }), (F, E) => {
      const q = $("el-input"), G = $("el-button");
      return y(), R("div", Po, [
        P("div", {
          class: "yzh-tree-table-selector__tree-panel",
          style: ze({ width: a.treeWidth + "px" })
        }, [
          a.treeSearchable ? (y(), R("div", Vo, [
            L(q, {
              modelValue: s.value,
              "onUpdate:modelValue": E[0] || (E[0] = (X) => s.value = X),
              placeholder: "搜索节点",
              clearable: "",
              "prefix-icon": "Search",
              size: "small"
            }, null, 8, ["modelValue"])
          ])) : H("", !0),
          P("div", Lo, [
            L(G, {
              size: "small",
              onClick: w
            }, {
              default: k(() => [...E[1] || (E[1] = [
                V("展开全部", -1)
              ])]),
              _: 1
            }),
            L(G, {
              size: "small",
              onClick: m
            }, {
              default: k(() => [...E[2] || (E[2] = [
                V("折叠全部", -1)
              ])]),
              _: 1
            }),
            L(G, {
              size: "small",
              onClick: p
            }, {
              default: k(() => [...E[3] || (E[3] = [
                V("全选", -1)
              ])]),
              _: 1
            }),
            L(G, {
              size: "small",
              onClick: A
            }, {
              default: k(() => [...E[4] || (E[4] = [
                V("取消全选", -1)
              ])]),
              _: 1
            })
          ]),
          L(lt, {
            ref_key: "treeRef",
            ref: n,
            data: b.value,
            "show-checkbox": !0,
            "check-strictly": a.checkStrictly,
            lazy: a.treeLazy,
            "load-data": a.treeLoadData,
            "default-expand-all": a.treeDefaultExpandAll,
            "node-key": a.nodeKey,
            onCheckChange: x,
            onNodeClick: h
          }, null, 8, ["data", "check-strictly", "lazy", "load-data", "default-expand-all", "node-key"]),
          F.$slots.treeFooter ? (y(), R("div", Eo, [
            W(F.$slots, "treeFooter", {}, void 0, !0)
          ])) : H("", !0)
        ], 4),
        P("div", Mo, [
          P("div", Uo, [
            P("div", Io, [
              E[5] || (E[5] = V(" 已选择 ", -1)),
              P("strong", null, I(f.value.length), 1),
              E[6] || (E[6] = V(" 条记录 ", -1))
            ]),
            L(G, {
              size: "small",
              type: "danger",
              onClick: re,
              disabled: f.value.length === 0
            }, {
              default: k(() => [...E[7] || (E[7] = [
                V(" 清空选择 ", -1)
              ])]),
              _: 1
            }, 8, ["disabled"])
          ]),
          L(nt, {
            ref_key: "tableRef",
            ref: i,
            columns: a.tableColumns,
            "data-loader": ee,
            selectable: !0,
            "show-pagination": a.showPagination,
            "page-size": a.pageSize,
            "row-key": a.rowKey,
            onSelectionChange: ne
          }, null, 8, ["columns", "show-pagination", "page-size", "row-key"])
        ])
      ]);
    };
  }
}), qa = /* @__PURE__ */ he(Oo, [["__scopeId", "data-v-8e5e846d"]]), Ko = { class: "yzh-tree-table-check-selector" }, jo = { class: "yzh-tree-table-check-selector__toolbar" }, Yo = { class: "yzh-tree-table-check-selector__selection-info" }, Wo = {
  key: 0,
  class: "yzh-tree-table-check-selector__search"
}, qo = { class: "yzh-tree-table-check-selector__toolbar-actions" }, Go = /* @__PURE__ */ le({
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
      var g;
      return ((g = t.typeLabels) == null ? void 0 : g[u]) ?? u;
    }
    function n(u) {
      var g;
      return ((g = t.typeTagTypes) == null ? void 0 : g[u]) ?? "info";
    }
    const i = e, s = T(), c = T([]), f = T(""), d = T(/* @__PURE__ */ new Set()), b = T(/* @__PURE__ */ new Map()), w = T(/* @__PURE__ */ new Set()), m = T(!1), p = T(/* @__PURE__ */ new Set()), A = j(() => {
      var g;
      if (!t.countType) return d.value.size;
      let u = 0;
      for (const C of d.value)
        ((g = b.value.get(C)) == null ? void 0 : g[t.nodeTypeField]) === t.countType && u++;
      return u;
    }), h = j(() => {
      var M, Q;
      const u = f.value.trim().toLowerCase();
      if (!u) return t.flatData;
      const g = /* @__PURE__ */ new Set();
      for (const D of t.flatData)
        t.searchFields.some(
          (ye) => String(D[ye] ?? "").toLowerCase().includes(u)
        ) && g.add(String(D[t.nodeKey]));
      const C = new Map(t.flatData.map((D) => [String(D[t.nodeKey]), D])), S = new Set(g);
      for (const D of g) {
        let Y = (M = C.get(D)) == null ? void 0 : M[t.parentKey];
        for (; Y && !S.has(String(Y)); )
          S.add(String(Y)), Y = (Q = C.get(String(Y))) == null ? void 0 : Q[t.parentKey];
      }
      return t.flatData.filter((D) => S.has(String(D[t.nodeKey])));
    });
    function x(u) {
      const g = /* @__PURE__ */ new Map(), C = [];
      for (const S of u) {
        const M = {
          ...S,
          children: []
        };
        g.set(S[t.nodeKey], M), b.value.set(S[t.nodeKey], M);
      }
      for (const S of u) {
        const M = g.get(S[t.nodeKey]), Q = S[t.parentKey];
        Q && g.has(Q) ? g.get(Q).children.push(M) : C.push(M);
      }
      return C;
    }
    function K(u) {
      const g = /* @__PURE__ */ new Set();
      function C(S) {
        for (const M of S)
          M[t.checkField] && g.add(M[t.nodeKey]), M.children && M.children.length > 0 && C(M.children);
      }
      C(u), d.value = g;
    }
    function ee() {
      if (!s.value) return;
      m.value = !0, s.value.clearSelection();
      const u = /* @__PURE__ */ new Set();
      for (const g of d.value) {
        const C = b.value.get(g);
        C && (s.value.toggleRowSelection(C, !0), u.add(g));
      }
      p.value = u, xe(() => {
        m.value = !1;
      });
    }
    function ne(u, g) {
      if (m.value = !0, b.value.clear(), !u || u.length === 0) {
        c.value = [], g && (d.value = /* @__PURE__ */ new Set()), xe(() => {
          ee(), re();
        });
        return;
      }
      c.value = x(u), g && K(c.value), t.defaultExpandAll && (w.value.clear(), se(c.value)), xe(() => {
        ee(), re();
      });
    }
    function re() {
      xe(() => {
        m.value = !1;
      });
    }
    _e(
      () => t.flatData,
      (u) => {
        ne(u, !0);
      },
      { immediate: !0 }
    ), _e(f, () => {
      ne(h.value, !1);
    });
    function se(u) {
      for (const g of u)
        g.children && g.children.length > 0 && (w.value.add(g[t.nodeKey]), se(g.children));
    }
    function F() {
      se(c.value);
    }
    function E() {
      w.value.clear(), m.value = !0;
      const u = c.value;
      c.value = [], xe(() => {
        c.value = u, re();
      });
    }
    function q() {
      if (!s.value) return;
      m.value = !0;
      const u = X(c.value), g = new Set(d.value);
      for (const C of u)
        g.add(C[t.nodeKey]), s.value.toggleRowSelection(C, !0);
      d.value = g, p.value = new Set(g), m.value = !1, pe([], u.map((C) => C[t.nodeKey]));
    }
    function G() {
      if (!s.value) return;
      m.value = !0;
      const u = Array.from(d.value);
      d.value = /* @__PURE__ */ new Set(), p.value = /* @__PURE__ */ new Set(), s.value.clearSelection(), m.value = !1, pe(u, []);
    }
    function X(u) {
      const g = t.checkAllExcludeTypes ?? [], C = [];
      for (const S of u)
        g.includes(S[t.nodeTypeField]) || C.push(S), S.children && S.children.length > 0 && C.push(...X(S.children));
      return C;
    }
    function te(u) {
      const g = [], C = (S) => {
        var M;
        for (const Q of S)
          g.push(Q), (M = Q.children) != null && M.length && C(Q.children);
      };
      return C(u.children ?? []), g;
    }
    function Z(u, g) {
      const C = [];
      for (const S of g) u.has(S) || C.push(S);
      return C;
    }
    function me(u, g, C) {
      const S = new Set(C), M = (de, Ae) => {
        var Pe;
        const Te = String(de[t.nodeKey]);
        Ae ? S.add(Te) : S.delete(Te), (Pe = s.value) == null || Pe.toggleRowSelection(de, Ae);
      };
      m.value = !0, M(u, g);
      for (const de of te(u)) M(de, g);
      const Q = t.checkAllExcludeTypes ?? [];
      let D = u[t.parentKey];
      for (; D; ) {
        const de = b.value.get(String(D));
        if (!de) break;
        const Ae = de.children.filter(
          (Te) => !Q.includes(String(Te[t.nodeTypeField]))
        );
        M(de, Ae.length > 0 && Ae.every((Te) => S.has(String(Te[t.nodeKey])))), D = de[t.parentKey];
      }
      m.value = !1;
      const Y = Z(S, C), ye = Z(C, S), $e = new Set(d.value);
      for (const de of ye) $e.add(de);
      for (const de of Y) $e.delete(de);
      d.value = $e, p.value = new Set(S), pe(Y, ye);
    }
    function fe(u) {
      if (m.value) return;
      const g = new Set(u.map((D) => String(D[t.nodeKey]))), C = p.value, S = Z(C, g), M = Z(g, C);
      if (S.length === 0 && M.length === 0) return;
      if (t.cascade) {
        const Y = S.length + M.length === 1 ? S[0] ?? M[0] : void 0, ye = Y ? b.value.get(Y) : void 0;
        if (ye) {
          me(ye, S.length > 0, C);
          return;
        }
      }
      p.value = g;
      const Q = new Set(d.value);
      for (const D of S) Q.add(D);
      for (const D of M) Q.delete(D);
      d.value = Q, S.length > 0 && pe([], S), M.length > 0 && pe(M, []);
    }
    function pe(u, g) {
      i("check-change", { added: g, removed: u });
    }
    function we() {
      return Array.from(d.value);
    }
    function ie(u) {
      d.value = new Set(u), xe(() => {
        ee();
      });
    }
    function r() {
      const u = [];
      for (const g of d.value) {
        const C = b.value.get(g);
        C && u.push(C);
      }
      return u;
    }
    return o({
      getCheckedKeys: we,
      setCheckedKeys: ie,
      getCheckedNodes: r,
      expandAll: F,
      collapseAll: E,
      checkAll: q,
      uncheckAll: G
    }), (u, g) => {
      const C = $("el-button"), S = $("el-table-column"), M = $("el-tag"), Q = $("el-table");
      return y(), R("div", Ko, [
        P("div", jo, [
          P("div", Yo, [
            g[1] || (g[1] = V(" 已选择 ", -1)),
            P("strong", null, I(A.value), 1),
            g[2] || (g[2] = V(" 条记录 ", -1))
          ]),
          a.searchable ? (y(), R("div", Wo, [
            L(Fe(Ge), {
              modelValue: f.value,
              "onUpdate:modelValue": g[0] || (g[0] = (D) => f.value = D),
              placeholder: a.searchPlaceholder,
              clearable: "",
              size: "small",
              "prefix-icon": "Search"
            }, null, 8, ["modelValue", "placeholder"])
          ])) : H("", !0),
          P("div", qo, [
            L(C, {
              size: "small",
              onClick: F
            }, {
              default: k(() => [...g[3] || (g[3] = [
                V("展开全部", -1)
              ])]),
              _: 1
            }),
            L(C, {
              size: "small",
              onClick: E
            }, {
              default: k(() => [...g[4] || (g[4] = [
                V("折叠全部", -1)
              ])]),
              _: 1
            }),
            L(C, {
              size: "small",
              onClick: q
            }, {
              default: k(() => [...g[5] || (g[5] = [
                V("全选", -1)
              ])]),
              _: 1
            }),
            L(C, {
              size: "small",
              onClick: G
            }, {
              default: k(() => [...g[6] || (g[6] = [
                V("取消全选", -1)
              ])]),
              _: 1
            })
          ])
        ]),
        L(Q, {
          ref_key: "tableRef",
          ref: s,
          data: c.value,
          "row-key": a.nodeKey,
          "tree-props": { children: "children", checkStrictly: !0 },
          onSelectionChange: fe,
          "default-expand-all": a.defaultExpandAll,
          style: { width: "100%" },
          class: "yzh-tree-table-check-selector__table"
        }, {
          default: k(() => [
            L(S, {
              type: "selection",
              width: "50"
            }),
            (y(!0), R(ae, null, ce(a.columns, (D) => (y(), N(S, {
              key: D.prop,
              prop: D.prop,
              label: D.label,
              width: D.width,
              "min-width": D.minWidth,
              fixed: D.fixed,
              "show-overflow-tooltip": D.showOverflowTooltip !== !1
            }, {
              default: k(({ row: Y }) => [
                W(u.$slots, `column-${D.prop}`, {
                  row: Y,
                  column: D
                }, () => [
                  D.prop === a.nodeTypeField ? (y(), N(M, {
                    key: 0,
                    type: n(Y[a.nodeTypeField]),
                    size: "small"
                  }, {
                    default: k(() => [
                      V(I(l(Y[a.nodeTypeField])), 1)
                    ]),
                    _: 2
                  }, 1032, ["type"])) : (y(), R(ae, { key: 1 }, [
                    V(I(Y[D.prop]), 1)
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
}), Ga = /* @__PURE__ */ he(Go, [["__scopeId", "data-v-0aab416f"]]), Ha = /* @__PURE__ */ le({
  __name: "YzhTreeTable",
  props: {
    columns: {},
    dataLoader: {},
    searchFields: { default: void 0 },
    selectable: { type: Boolean, default: void 0 },
    selectMode: { default: void 0 },
    showPagination: { type: Boolean, default: !1 },
    pageSize: { default: 20 },
    defaultSort: { default: void 0 },
    height: { default: void 0 },
    rowKey: { default: "Code" },
    emptyText: { default: "暂无数据" },
    toolbar: { type: [Boolean, Object], default: !0 },
    toolbarActions: { default: () => [] },
    searchMaxFields: { default: 2 },
    noPadding: { type: Boolean, default: !1 },
    rowActionButtons: { type: [Object, Array, Function], default: () => [] },
    rowActionLink: { type: Boolean, default: !0 },
    actionMaxInline: { default: 0 },
    defaultExpandAll: { type: Boolean, default: !0 },
    childrenField: { default: "children" },
    allowAddChild: { type: [Boolean, Function], default: !1 },
    addChildText: { default: "新增下级" }
  },
  emits: ["selection-change", "row-click", "refresh", "row-action", "toolbar-action", "expand-change"],
  setup(a, { expose: o, emit: e }) {
    const t = a, l = e, n = T(null);
    function i(m) {
      return typeof t.allowAddChild == "function" ? !!t.allowAddChild(m) : !!t.allowAddChild;
    }
    const s = j(() => {
      const m = t.rowActionButtons;
      if (!t.allowAddChild) return m;
      const p = (A) => {
        const h = Array.isArray(A) ? [...A] : Object.entries(A || {}).map(([K, ee]) => ({ key: K, text: ee }));
        return h.some((K) => K.key === "add-child") ? h : [{ key: "add-child", text: t.addChildText, type: "primary" }, ...h];
      };
      return typeof m == "function" ? (A) => {
        const h = p(m(A));
        return Array.isArray(h) && !i(A) ? h.map((x) => x.key === "add-child" ? { ...x, visible: !1 } : x) : h;
      } : p(m);
    }), c = j(() => ({
      children: t.childrenField,
      hasChildren: "hasChildren"
    }));
    function f() {
      var m;
      (m = n.value) == null || m.refresh();
    }
    function d() {
      var m;
      (m = n.value) == null || m.loadData();
    }
    function b() {
      var m;
      (m = n.value) == null || m.expandAll();
    }
    function w() {
      var m;
      (m = n.value) == null || m.collapseAll();
    }
    return o({
      refresh: f,
      loadData: d,
      expandAll: b,
      collapseAll: w,
      insertRow: (...m) => {
        var p;
        return (p = n.value) == null ? void 0 : p.insertRow(...m);
      },
      replaceRow: (...m) => {
        var p;
        return (p = n.value) == null ? void 0 : p.replaceRow(...m);
      },
      removeRow: (...m) => {
        var p;
        return (p = n.value) == null ? void 0 : p.removeRow(...m);
      },
      getRowCount: () => {
        var m, p;
        return ((p = (m = n.value) == null ? void 0 : m.getRowCount) == null ? void 0 : p.call(m)) ?? 0;
      },
      getSelectedRows: () => {
        var m, p;
        return ((p = (m = n.value) == null ? void 0 : m.getSelectedRows) == null ? void 0 : p.call(m)) ?? [];
      },
      setCheckedRows: (...m) => {
        var p;
        return (p = n.value) == null ? void 0 : p.setCheckedRows(...m);
      },
      clearSelection: () => {
        var m, p;
        return (p = (m = n.value) == null ? void 0 : m.clearSelection) == null ? void 0 : p.call(m);
      },
      tableRef: n
    }), (m, p) => (y(), N(nt, ve({
      ref_key: "tableRef",
      ref: n,
      columns: a.columns,
      "data-loader": a.dataLoader,
      "search-fields": a.searchFields,
      selectable: a.selectable,
      "select-mode": a.selectMode,
      "show-pagination": a.showPagination,
      "page-size": a.pageSize,
      "default-sort": a.defaultSort,
      height: a.height,
      "row-key": a.rowKey,
      "empty-text": a.emptyText,
      toolbar: a.toolbar,
      "toolbar-actions": a.toolbarActions,
      "search-max-fields": a.searchMaxFields,
      "no-padding": a.noPadding,
      "row-action-buttons": s.value,
      "row-action-link": a.rowActionLink,
      "action-max-inline": a.actionMaxInline,
      "default-expand-all": a.defaultExpandAll,
      "tree-props": c.value
    }, m.$attrs, {
      onSelectionChange: p[0] || (p[0] = (A) => l("selection-change", A)),
      onRowClick: p[1] || (p[1] = (A, h) => l("row-click", A, h)),
      onRefresh: p[2] || (p[2] = (A) => l("refresh")),
      onRowAction: p[3] || (p[3] = (A, h, x) => l("row-action", A, h, x)),
      onToolbarAction: p[4] || (p[4] = (A, h) => l("toolbar-action", A, h)),
      onExpandChange: p[5] || (p[5] = (A, h) => l("expand-change", A, h))
    }), at({ _: 2 }, [
      ce(m.$slots, (A, h) => ({
        name: h,
        fn: k((x) => [
          W(m.$slots, h, $t(Rt(x)))
        ])
      }))
    ]), 1040, ["columns", "data-loader", "search-fields", "selectable", "select-mode", "show-pagination", "page-size", "default-sort", "height", "row-key", "empty-text", "toolbar", "toolbar-actions", "search-max-fields", "no-padding", "row-action-buttons", "row-action-link", "action-max-inline", "default-expand-all", "tree-props"]));
  }
}), Ho = { class: "yzh-empty-state__inner" }, Xo = { class: "yzh-empty-state__title" }, Jo = {
  key: 2,
  class: "yzh-empty-state__description"
}, Zo = {
  key: 3,
  class: "yzh-empty-state__action"
}, Qo = /* @__PURE__ */ le({
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
      const t = $("el-icon"), l = $("el-button");
      return y(), R("div", {
        class: ke(["yzh-empty-state", { "is-compact": a.compact, "is-icon-bg": a.iconBackgroundColor }])
      }, [
        P("div", Ho, [
          a.iconBackgroundColor ? (y(), R("div", {
            key: 0,
            class: "yzh-empty-state__icon-wrap",
            style: ze({ backgroundColor: a.iconBackgroundColor })
          }, [
            L(t, {
              class: "yzh-empty-state__icon",
              style: ze({ fontSize: a.iconSize + "px", color: a.iconColor })
            }, {
              default: k(() => [
                (y(), N(Ee(a.icon)))
              ]),
              _: 1
            }, 8, ["style"])
          ], 4)) : (y(), N(t, {
            key: 1,
            class: "yzh-empty-state__icon",
            style: ze({ fontSize: a.iconSize + "px", color: a.iconColor })
          }, {
            default: k(() => [
              (y(), N(Ee(a.icon)))
            ]),
            _: 1
          }, 8, ["style"])),
          P("div", Xo, I(a.title), 1),
          a.description ? (y(), R("div", Jo, I(a.description), 1)) : H("", !0),
          a.actionLabel && a.onAction ? (y(), R("div", Zo, [
            W(o.$slots, "action", {}, () => [
              L(l, {
                size: "small",
                onClick: a.onAction
              }, {
                default: k(() => [
                  V(I(a.actionLabel), 1)
                ]),
                _: 1
              }, 8, ["onClick"])
            ], !0)
          ])) : H("", !0)
        ])
      ], 2);
    };
  }
}), Xa = /* @__PURE__ */ he(Qo, [["__scopeId", "data-v-33c080f4"]]), ea = /* @__PURE__ */ le({
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
    const o = a, e = j(() => ({
      success: null,
      // 后续引入图标
      warning: null,
      danger: null,
      info: null
    })[o.type] || null);
    return (t, l) => {
      const n = $("el-icon");
      return y(), R("span", {
        class: ke(["yzh-status-badge", [`is-${a.type}`, `is-${a.size}`]])
      }, [
        a.icon || e.value ? (y(), N(n, {
          key: 0,
          class: "yzh-status-badge__icon"
        }, {
          default: k(() => [
            (y(), N(Ee(a.icon || e.value)))
          ]),
          _: 1
        })) : H("", !0),
        W(t.$slots, "default", {}, () => [
          V(I(a.text), 1)
        ], !0)
      ], 2);
    };
  }
}), Ja = /* @__PURE__ */ he(ea, [["__scopeId", "data-v-d7402330"]]), ta = { class: "yzh-card" }, oa = {
  key: 0,
  class: "yzh-card__header"
}, aa = { class: "yzh-card__body" }, la = {
  key: 1,
  class: "yzh-card__footer"
}, na = /* @__PURE__ */ le({
  __name: "YzhCard",
  props: {
    title: { type: String, default: "" }
  },
  setup(a) {
    return (o, e) => (y(), R("div", ta, [
      o.$slots.header || a.title ? (y(), R("div", oa, [
        W(o.$slots, "header", {}, () => [
          V(I(a.title), 1)
        ], !0)
      ])) : H("", !0),
      P("div", aa, [
        W(o.$slots, "default", {}, void 0, !0)
      ]),
      o.$slots.footer ? (y(), R("div", la, [
        W(o.$slots, "footer", {}, void 0, !0)
      ])) : H("", !0)
    ]));
  }
}), Za = /* @__PURE__ */ he(na, [["__scopeId", "data-v-245f071f"]]);
function sa(a) {
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
function ra(a) {
  return {
    NumberBox: "number",
    DatePicker: "date",
    DateTimePicker: "dateRange",
    ComboBox: "select",
    DropDownList: "select",
    RadioButtonList: "select"
  }[a] || "text";
}
function ia(a) {
  return {
    input: "text",
    select: "select",
    date: "date",
    cascader: "cascader"
  }[a] || "text";
}
function da(a) {
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
function He(a) {
  var t;
  const o = a == null ? void 0 : a.FormCols;
  return o && o > 0 ? o : (((t = a == null ? void 0 : a.Columns) == null ? void 0 : t.filter((l) => l.BcFlag).length) ?? 0) <= 10 ? 1 : 2;
}
function ca(a, o = "0", e) {
  const t = a == null ? void 0 : a.Columns, l = a == null ? void 0 : a.Schema;
  if (!t) return [];
  const n = He(a), i = Math.floor(24 / n), s = (e == null ? void 0 : e.withDefaults) ?? !1;
  return t.filter((c) => c.BcFlag && c.Type !== "Other").map((c) => {
    var p;
    const f = c.FieldName, d = ha(f), b = l == null ? void 0 : l[d], w = c.GroupIndex || "0", m = o !== "0" && w !== o;
    return {
      prop: f,
      label: c.DesName,
      type: sa(c.Type),
      required: !c.Yxk,
      disabled: c.Enable === !1 || m,
      span: i,
      dictCode: c.DictCode || void 0,
      options: void 0,
      placeholder: (p = c.Type) != null && p.includes("Picker") ? `请选择${c.DesName}` : `请输入${c.DesName}`,
      defaultValue: s ? c.Mrz ? c.Type === "Switch" ? Number(c.Mrz) : c.Mrz : b == null ? void 0 : b.Default : void 0,
      fieldSchema: b
    };
  });
}
function tt(a) {
  const o = a == null ? void 0 : a.SearchFields;
  if (o && o.length > 0)
    return o.map((l) => ({
      prop: l.Field,
      label: l.Label,
      type: ia(l.ControlType),
      placeholder: `请输入${l.Label}`,
      options: l.Options ?? void 0
    }));
  const e = a == null ? void 0 : a.Columns;
  if (!e) return [];
  const t = ["Upload", "TreeSelect", "Cascader", "CheckBox"];
  return e.filter((l) => l.XsFlag && l.Type !== "Other" && !t.includes(l.Type) && l.BcFlag).slice(0, 4).map((l) => ({
    prop: l.FieldName,
    label: l.DesName,
    type: ra(l.Type),
    placeholder: `请输入${l.DesName}`
  }));
}
function ua(a) {
  const o = a == null ? void 0 : a.Toolbar;
  if (!o) return [];
  const e = [];
  if (o.Add !== !1 && e.push({ key: "add", text: "新增", type: "primary" }), o.Delete !== !1 && e.push({ key: "delete", text: "批量删除", type: "danger" }), o.Export !== !1 && e.push({ key: "export", text: "导出", type: "success" }), o.Import !== !1 && e.push({ key: "import", text: "导入", type: "warning" }), o.CustomButtons)
    for (const [t, l] of Object.entries(o.CustomButtons))
      e.push({ key: `custom:${l}`, text: t, type: "info" });
  return e;
}
function st(a, o) {
  const e = (a == null ? void 0 : a.RowButtons) ?? {}, t = [];
  if (e.Edit !== !1 && t.push({ key: "edit", text: "编辑", type: "primary" }), e.Delete !== !1 && t.push({ key: "delete", text: "删除", type: "danger" }), e.Enable === !0 && o && t.push({ key: "toggle-valid", text: "禁用/启用", type: "warning" }), e.CustomButtons)
    for (const [l, n] of Object.entries(e.CustomButtons))
      t.push({ key: `custom:${n}`, text: l, type: "info" });
  return t;
}
function Qa(a, o) {
  const e = {};
  for (const t of st(a, o)) e[t.key] = t.text;
  return e;
}
function el(a, o, e) {
  const t = [], l = a;
  if (!l) return t;
  if (l.AllowEdit && ((e == null ? void 0 : e.allowAddChild) !== !1 && t.push({ key: "add-child", text: "新增下级" }), t.push({ key: "edit", text: "编辑" })), l.AllowDelete && t.push({ key: "delete", text: "删除", type: "danger", danger: !0 }), o && t.push({ key: "toggle-valid", text: "禁用/启用", type: "warning" }), l.CustomActions)
    for (const [n, i] of Object.entries(l.CustomActions))
      t.push({ key: `custom:${n}`, text: i, type: "info" });
  return t;
}
function tl(a, o) {
  var t;
  const e = ((t = o == null ? void 0 : o.Extra) == null ? void 0 : t.level) ?? (o == null ? void 0 : o.level) ?? -1;
  return {
    ...a,
    Extra: { ...a.Extra, level: e + 1 },
    Children: []
  };
}
function ha(a) {
  return !a || a[0] >= "a" && a[0] <= "z" ? a : a[0].toLowerCase() + a.slice(1);
}
const je = {}, Ye = "YZH_TOKEN", be = {
  get: () => localStorage.getItem(Ye),
  set: (a) => localStorage.setItem(Ye, a),
  clear: () => localStorage.removeItem(Ye)
};
class rt {
  constructor(o) {
    /** 服务根地址（文件下载等场景需要读取） */
    z(this, "baseURL");
    z(this, "getToken");
    z(this, "onUnauthorized");
    z(this, "onError");
    this.baseURL = o.baseURL.replace(/\/$/, ""), this.getToken = o.getToken || (() => be.get()), this.onUnauthorized = o.onUnauthorized, this.onError = o.onError;
  }
  /**
   * 通用请求方法
   * 原样透传：返回后端 JSON，不做 key 转换
   */
  async request(o, e = {}) {
    var b, w;
    const {
      method: t = "POST",
      params: l,
      body: n,
      headers: i = {},
      requireAuth: s = !0,
      raw: c = !1
    } = e;
    let f = o;
    const d = {
      method: t,
      headers: {
        "Content-Type": "application/json",
        ...i
      }
    };
    if (s !== !1) {
      const m = this.getToken();
      m && (d.headers.Authorization = `Bearer ${m}`);
    }
    if (l) {
      let m = l;
      const p = Object.keys(l), A = l.params;
      p.length === 1 && p[0] === "params" && A && typeof A == "object" && (console.warn(
        "[YzhApi] 查询参数多包了一层 params（应为 get(url, { a, b }) 而非 get(url, { params: { a, b } })），已自动解包：",
        A
      ), m = A);
      const h = new URLSearchParams();
      Object.entries(m).forEach(([K, ee]) => {
        ee != null && h.append(K, String(ee));
      });
      const x = h.toString();
      x && (f += (o.includes("?") ? "&" : "?") + x);
    }
    n !== void 0 ? d.body = JSON.stringify(n) : t !== "GET" && !l && (d.body = "{}");
    try {
      const m = await fetch(this.baseURL + f, d);
      if (m.status === 401)
        throw be.clear(), (b = this.onUnauthorized) == null || b.call(this), new Error("登录已过期，请重新登录");
      const p = await m.json();
      if (!m.ok) {
        const A = (p == null ? void 0 : p.message) || (p == null ? void 0 : p.msg) || `请求失败 (${m.status})`, h = new Error(A);
        throw h.status = m.status, h.data = p, h;
      }
      return p;
    } catch (m) {
      throw (w = this.onError) == null || w.call(this, m), m;
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
    var s;
    const t = this.getToken();
    let l = o;
    if (e) {
      const c = new URLSearchParams();
      Object.entries(e).forEach(([d, b]) => {
        b != null && c.append(d, String(b));
      });
      const f = c.toString();
      f && (l += (o.includes("?") ? "&" : "?") + f);
    }
    const n = await fetch(this.baseURL + l, {
      method: "GET",
      headers: {
        ...t ? { Authorization: `Bearer ${t}` } : {}
      }
    });
    if (n.status === 401)
      throw be.clear(), (s = this.onUnauthorized) == null || s.call(this), new Error("登录已过期，请重新登录");
    if ((n.headers.get("content-type") || "").includes("application/json")) {
      const c = await n.json().catch(() => ({})), f = new Error((c == null ? void 0 : c.message) || (c == null ? void 0 : c.msg) || `请求失败 (${n.status})`);
      throw f.status = n.status, f;
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
    var s;
    const l = this.getToken(), n = await fetch(this.baseURL + o, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...l ? { Authorization: `Bearer ${l}` } : {}
      },
      body: JSON.stringify(e)
    });
    if (n.status === 401)
      throw be.clear(), (s = this.onUnauthorized) == null || s.call(this), new Error("登录已过期，请重新登录");
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
      throw be.clear(), (i = this.onUnauthorized) == null || i.call(this), new Error("登录已过期，请重新登录");
    if (!l.ok) {
      const s = await l.json().catch(() => ({}));
      throw new Error(s.message || s.msg || "下载失败");
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
      throw be.clear(), (n = this.onUnauthorized) == null || n.call(this), new Error("登录已过期，请重新登录");
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
const Ne = new rt({
  baseURL: (je == null ? void 0 : je.VITE_API_BASE) || "http://127.0.0.1:9992",
  onUnauthorized: () => {
    console.warn("[YzhApi] 401 未授权，请重新登录");
  }
}), Re = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  YzhApiClient: rt,
  tokenStore: be,
  yzhApi: Ne
}, Symbol.toStringTag, { value: "Module" })), De = "/api/file-storage";
function ol(a, o) {
  const e = new FormData();
  return e.append("file", a), Ne.post(`${De}/upload`, e, {
    params: o,
    headers: { "Content-Type": "multipart/form-data" }
  });
}
function al(a, o) {
  const e = new FormData();
  return a.forEach((t) => e.append("files", t)), Ne.post(`${De}/upload-batch`, e, {
    params: o,
    headers: { "Content-Type": "multipart/form-data" }
  });
}
function ll(a) {
  const o = Ne.baseURL || "", e = localStorage.getItem("token") || "";
  return `${o}${De}/download?path=${encodeURIComponent(a)}&token=${e}`;
}
function nl(a) {
  return Ne.post(`${De}/delete`, null, { params: { path: a } });
}
function sl(a) {
  return Ne.get(`${De}/exists`, { path: a });
}
function rl(a) {
  return Ne.get(`${De}/list`, { prefix: a });
}
function il() {
  const a = T(be.get() || ""), o = T(null), e = j(() => !!a.value);
  function t(s) {
    a.value = s, be.set(s);
  }
  function l() {
    a.value = "", o.value = null, be.clear();
  }
  function n(s, c) {
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
function dl() {
  const a = T(!1), o = T([]), e = T(0), t = T(1), l = T(20), n = Ce({});
  async function i(f) {
    a.value = !0;
    try {
      const d = {
        page: t.value,
        rows: l.value,
        ...n
      }, b = await f(d);
      o.value = b.rows || [], e.value = b.total || 0;
    } finally {
      a.value = !1;
    }
  }
  function s(f) {
    Object.assign(n, f), t.value = 1;
  }
  function c() {
    Object.keys(n).forEach((f) => delete n[f]), t.value = 1;
  }
  return {
    loading: a,
    rows: o,
    total: e,
    page: t,
    pageSize: l,
    searchParams: n,
    loadData: i,
    setSearchParams: s,
    resetSearchParams: c
  };
}
function cl() {
  async function a(o) {
    try {
      return await Se.confirm(o.message, o.title ?? "操作确认", {
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
function ul(a, ...o) {
  const e = new a(...o), t = T(null);
  return Be(async () => {
    await e.init(), await xe(), e.setTableRef(t.value);
  }), { logic: e, tableRef: t };
}
function hl(a, ...o) {
  const e = new a(...o), t = T(null), l = T(null);
  return Be(async () => {
    await e.init(), await xe(), e.setTableRef(t.value), e.setTreeTableRef(l.value);
  }), { logic: e, tableRef: t, treeTableRef: l };
}
function fl(a, ...o) {
  const e = new a(...o);
  return Be(async () => {
    await e.init();
  }), { logic: e };
}
function pl(a, ...o) {
  const e = new a(...o);
  return Be(async () => {
    await e.init();
  }), { logic: e };
}
function it(a) {
  return !a || a[0] >= "a" && a[0] <= "z" ? a : a[0].toLowerCase() + a.slice(1);
}
function fa(a) {
  return !a || a[0] >= "A" && a[0] <= "Z" ? a : a[0].toUpperCase() + a.slice(1);
}
function We(a) {
  const o = {};
  for (const [e, t] of Object.entries(a))
    o[fa(e)] = t;
  return o;
}
function ml(a) {
  const o = {};
  for (const [e, t] of Object.entries(a))
    o[it(e)] = t;
  return o;
}
class pa {
  constructor() {
    // ──── 后端配置 ────
    /** 后端页面配置（工具栏+表格+表单+搜索栏） */
    z(this, "config", T(null));
    // ──── 表格状态 ────
    /** 表格数据行（PascalCase 字段） */
    z(this, "rows", T([]));
    /** 表格加载状态 */
    z(this, "loading", T(!1));
    /** 选中行集合 */
    z(this, "selectedRows", T([]));
    // ──── 分页状态 ────
    /** 分页参数 */
    z(this, "pagination", Ce({ page: 1, pageSize: 20, total: 0 }));
    // ──── 搜索过滤状态 ────
    /** 搜索参数（PascalCase key，与业务实体字段名一致） */
    z(this, "searchParams", Ce({}));
    // ──── 排序状态 ────
    /** 排序字段（PascalCase） */
    z(this, "sortField", T());
    /** 排序方向 */
    z(this, "sortOrder", T());
    // ──── 弹窗状态 ────
    /** 弹窗可见性 */
    z(this, "dialogVisible", T(!1));
    /** 弹窗模式 */
    z(this, "dialogMode", T("add"));
    /**
     * 表单编辑模式（GroupIndex 控制）
     *
     * - '0'：新增/编辑模式，GroupIndex="0" 的字段可编辑
     * - '99'：详情模式，仅 GroupIndex="99" 的字段可编辑（JSON 通常不配 → 全部只读）
     */
    z(this, "formGroupIndex", T("0"));
    /** 提交中状态 */
    z(this, "submitting", T(!1));
    // ──── ShowDisabled 开关（基类统一管理，子类无需手动实现） ────
    /** 显示已禁用记录开关 */
    z(this, "showDisabled", T(!1));
    /**
     * 表单数据：PascalCase key（与 formFields[].prop、NewEntity、实体属性名一致）
     * 例：{ Code: "", UserName: "", Enable: 1 }
     */
    z(this, "formData", Ce({}));
    // ──── 表格引用（局部刷新） ────
    z(this, "_tableRef", null);
    // ========================================================
    // 动作统一（ST-7 dispatch + registerHandler）
    // ========================================================
    z(this, "handlers", /* @__PURE__ */ new Map());
    /** 行动作入口（绑定 @row-action="logic.onRowAction"；箭头属性自动绑定 this，模板引用式传参不丢上下文） */
    z(this, "onRowAction", async (o, e, t) => {
      await this.dispatch(o, e, t);
    });
    /** 工具栏动作入口（绑定 @toolbar-action="logic.onToolbarAction"） */
    z(this, "onToolbarAction", async (o, e) => {
      await this.dispatch(o, void 0, e);
    });
    /** @deprecated 兼容旧命名，等价 onToolbarAction */
    z(this, "onToolbarClick", async (o) => {
      await this.dispatch(o);
    });
    /** @deprecated 兼容旧命名，等价 onRowAction */
    z(this, "onRowClick", async (o, e) => {
      await this.dispatch(o, e);
    });
    // ========================================================
    // 事件处理（表格原生事件；箭头属性自动绑定 this，供模板引用式绑定）
    // ========================================================
    z(this, "onSearch", async (o) => {
      this.resetObject(this.searchParams), Object.assign(this.searchParams, o), this.pagination.page = 1, await this.loadPage();
    });
    z(this, "onPageChange", async (o) => {
      this.pagination.page = o, await this.loadPage();
    });
    z(this, "onSizeChange", async (o) => {
      this.pagination.pageSize = o, this.pagination.page = 1, await this.loadPage();
    });
    z(this, "onSortChange", async (o, e) => {
      this.sortField.value = o, this.sortOrder.value = e, await this.loadPage();
    });
    z(this, "onSelectionChange", (o) => {
      this.selectedRows.value = o;
    });
    /** 当前编辑行（ST-8：提交后与后端返回合并，避免表格行丢字段） */
    z(this, "editingRow", T(null));
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
    return da(this.config.value);
  }
  /** 表单布局列数（从后端 EntityConfig.FormCols 读取，0=自动） */
  get formLayoutCols() {
    return He(this.config.value);
  }
  /** 表单字段配置（AD-2） */
  get formFields() {
    return ca(this.config.value, this.formGroupIndex.value);
  }
  /** 搜索栏字段（config.SearchFields 优先；为空时走 fallbackSearchFields 钩子再走列推导） */
  get searchFields() {
    var t;
    const o = (t = this.config.value) == null ? void 0 : t.SearchFields;
    if (o && o.length > 0)
      return tt(this.config.value);
    const e = this.fallbackSearchFields;
    return e.length > 0 ? e : tt(this.config.value);
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
    return ua(this.config.value);
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
    return st(this.config.value, this.enableField);
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
      const s = {
        Page: e,
        PageSize: t,
        SortField: l,
        SortOrder: n,
        Filters: this.buildFilters(i)
      }, c = await this.apiPost("/filter", s), f = c == null ? void 0 : c.data, d = this.postprocessRows(((f == null ? void 0 : f.Items) ?? []).slice());
      return this.pagination.page = e, this.pagination.pageSize = t, this.pagination.total = (f == null ? void 0 : f.TotalCount) ?? 0, this.rows.value = d, this.onDataLoaded(d), { rows: d, total: this.pagination.total };
    } finally {
      this.loading.value = !1;
    }
  }
  /** 构建过滤条件（从 searchParams + 额外条件 + 自动 ShowDisabled） */
  buildFilters(o) {
    var n, i;
    const e = { ...this.searchParams, ...o || {} }, t = /* @__PURE__ */ new Map();
    if ((n = this.config.value) != null && n.SearchFields)
      for (const s of this.config.value.SearchFields)
        s.Operator && t.set(s.Field, s.Operator);
    const l = Object.entries(e).filter(
      ([, s]) => s != null && s !== "" && !(Array.isArray(s) && s.length === 0)
    ).map(([s, c]) => ({
      Field: s,
      Value: Array.isArray(c) ? c.join(",") : String(c),
      Operator: t.get(s) || "eq"
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
    await Se.confirm(
      i ? `确定${n}【${i}】？` : `确定${n}该记录？`,
      `${n}确认`,
      {
        type: "warning",
        confirmButtonText: `确定${n}`,
        cancelButtonText: "取消"
      }
    );
    const s = await this.toggleIsValid(o.Code);
    s && this.replaceRowByCode(o.Code, { ...o, [t]: s.IsValid });
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
    let s;
    i.length === 1 ? s = `确定删除【${i[0]}】？` : i.length > 1 && i.length <= 3 ? s = `确定删除 ${i.length} 条记录（${i.join("、")}）？` : s = `确定删除 ${l.length} 条记录？`, await Se.confirm(s, "删除确认", {
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
    const e = `/api/${this.controllerName}${o}`, { yzhApi: t } = await Promise.resolve().then(() => Re);
    return t.get(e);
  }
  async apiPost(o, e) {
    const t = `/api/${this.controllerName}${o}`, { yzhApi: l } = await Promise.resolve().then(() => Re);
    return l.post(t, e);
  }
  async apiPostAndDownload(o, e, t) {
    const l = `/api/${this.controllerName}${o}`, { yzhApi: n } = await Promise.resolve().then(() => Re);
    return n.download(l, e, t);
  }
  async apiGetAndDownload(o, e) {
    const t = `/api/${this.controllerName}${o}`, { yzhApi: l } = await Promise.resolve().then(() => Re);
    return l.downloadGet(t, e);
  }
  async apiUpload(o, e) {
    const t = `/api/${this.controllerName}${o}`, { yzhApi: l } = await Promise.resolve().then(() => Re);
    return l.upload(t, e);
  }
  // ========================================================
  // 私有工具方法
  // ========================================================
  resetObject(o) {
    Object.keys(o).forEach((e) => delete o[e]);
  }
}
class ma {
  constructor() {
    /** 树数据（PascalCase，与后端 DTO 保持一致） */
    z(this, "treeData", T([]));
    /** 树加载状态 */
    z(this, "treeLoading", T(!1));
    /** 当前选中节点 */
    z(this, "selectedNode", T(null));
    /** 节点索引：Code → { node, parent }（O(1) 查找/替换/删除） */
    z(this, "index", /* @__PURE__ */ new Map());
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
    const t = e.parent ? (i = e.parent).Children ?? (i.Children = []) : this.treeData.value, l = t.findIndex((s) => s.Code === o);
    if (l < 0) return !1;
    t.splice(l, 1);
    const n = (s) => {
      this.index.delete(s.Code);
      for (const c of s.Children ?? []) n(c);
    };
    return n(e.node), !0;
  }
  /** 替换节点（O(1) 定位） */
  replaceNode(o, e) {
    var i;
    const t = this.index.get(o);
    if (!t) return !1;
    const l = t.parent ? (i = t.parent).Children ?? (i.Children = []) : this.treeData.value, n = l.findIndex((s) => s.Code === o);
    return n < 0 ? !1 : (l.splice(n, 1, e), this.index.delete(o), this.register(e, t.parent), !0);
  }
  /** 展开到指定节点（返回节点是否存在） */
  has(o) {
    return this.index.has(o);
  }
}
function ya(a) {
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
class yl extends pa {
  constructor() {
    super(...arguments);
    // ──── 树能力混入（TT-2：状态 + 索引 + 增量变更） ────
    z(this, "treeSide", new ma());
    /** 完整树表配置（PascalCase，YZH.Core.Stand/TreeTableConfigDto） */
    z(this, "treeTableConfig", T(null));
    // ──── 树节点表单弹窗状态 ────
    z(this, "treeDialogVisible", T(!1));
    z(this, "treeDialogMode", T("add"));
    z(this, "treeSubmitting", T(!1));
    /** 树节点表单数据：PascalCase key（与 treeFormFields prop 一致） */
    z(this, "treeFormData", Ce({}));
    /** 当前新增节点的父节点 */
    z(this, "treeParentNode", T(null));
    /** 当前编辑的节点 */
    z(this, "treeEditingNode", T(null));
    // ──── 树表组件引用（用于 appendNode 等直接操作） ────
    z(this, "_treeTableRef", null);
    // ========================================================
    // 树→表格联动（TT-6/TT-7）
    // ========================================================
    /** 节点点击 → 表格联动刷新（dataLoader 已自动注入 RelateField；箭头属性自动绑定 this） */
    z(this, "onNodeClick", async (e) => {
      var t;
      this.treeSide.selectedNode.value = e, this.pagination.page = 1, !((t = this.treeConfig) != null && t.OnlyLeafSelectable && !e.IsLeaf) && (this._tableRef ? await this._tableRef.refresh() : await this.refreshTable());
    });
    // ========================================================
    // dispatch 扩展（TT-10）：树节点动作路由
    // ========================================================
    /** 树节点动作入口（绑定 @tree-node-action="logic.onNodeAction"；箭头属性自动绑定 this） */
    z(this, "onNodeAction", async (e, t) => {
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
        const i = e.Extra || {}, s = n.charAt(0).toLowerCase() + n.slice(1);
        (i[n] ?? i[s] ?? 1) === 1 ? t.push({ key: "toggle-disable", text: "禁用", type: "warning" }) : t.push({ key: "toggle-enable", text: "启用", type: "warning" });
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
    return He(this.treeFormConfig);
  }
  /** 树节点表单字段配置（保留 TreeFormConfig 专用布局规则：ColSpan>1 占满整行） */
  get treeFormFields() {
    var i, s;
    const e = (i = this.treeFormConfig) == null ? void 0 : i.Columns, t = (s = this.treeFormConfig) == null ? void 0 : s.Schema;
    if (!e) return [];
    const l = this.treeFormLayoutCols, n = Math.floor(24 / l);
    return e.filter((c) => c.BcFlag && c.Type !== "Other").map((c) => {
      var w;
      const f = c.FieldName, d = it(f), b = t == null ? void 0 : t[d];
      return {
        prop: f,
        label: c.DesName,
        type: ya(c.Type),
        required: !c.Yxk,
        disabled: c.Enable === !1,
        span: (c.ColSpan ?? 0) > 1 ? 24 : n,
        dictCode: c.DictCode || void 0,
        options: void 0,
        placeholder: (w = c.Type) != null && w.includes("Picker") ? `请选择${c.DesName}` : `请输入${c.DesName}`,
        defaultValue: c.Mrz ? c.Type === "Switch" ? Number(c.Mrz) : c.Mrz : b == null ? void 0 : b.Default,
        fieldSchema: b
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
    var b;
    const n = Array.isArray(e == null ? void 0 : e.data) && e.data.length === 0 ? e : (e == null ? void 0 : e.data) ?? e, i = n == null ? void 0 : n.Code, s = ((b = n == null ? void 0 : n.Extra) == null ? void 0 : b.level) ?? 0;
    if (!i)
      return t && t([]), [];
    const d = ((await this.apiPost("/tree/children", {
      ParentCode: i,
      Level: s
    })).data ?? []).map((w) => this.dtoToNode(w, n));
    for (const w of d) this.treeSide.register(w, n);
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
    var i, s, c;
    if (e) {
      this.treeDialogMode.value = "edit", this.treeEditingNode.value = e, this.treeParentNode.value = null, this.resetObject(this.treeFormData);
      const f = ((i = this.treeFormConfig) == null ? void 0 : i.NewEntity) || {}, d = e.Extra || {}, b = {};
      for (const w of this.treeFormFields)
        w.prop in d && (b[w.prop] = d[w.prop]);
      return Object.assign(this.treeFormData, f, b, {
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
    const n = ((s = this.treeFormConfig) == null ? void 0 : s.NewEntity) || {};
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
    await this.onBeforeDeleteTree(e) && (await Se.confirm(`确定删除【${t}】？`, "删除确认", {
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
    var d, b, w, m;
    const l = ((d = this.treeConfig) == null ? void 0 : d.CodeField) ?? "Code", n = {
      ...We(t),
      [((b = this.treeConfig) == null ? void 0 : b.ParentCodeField) ?? "ParentCode"]: (e == null ? void 0 : e.Code) ?? ((w = this.treeConfig) == null ? void 0 : w.RootParentCode) ?? null
    }, i = await this.apiPost("/tree/add", n), c = (((m = i.data) == null ? void 0 : m[l]) ?? "") || n[l], f = this.dtoToNode(
      i.data ?? { Code: c, Name: n.Name ?? "", ParentCode: (e == null ? void 0 : e.Code) ?? null },
      e ?? void 0
    );
    return c && !i.data && (f.Code = c), this._treeTableRef ? (this._treeTableRef.appendNode((e == null ? void 0 : e.Code) ?? null, f), this.treeSide.register(f, e)) : this.treeSide.appendChild((e == null ? void 0 : e.Code) ?? null, f), f;
  }
  /** 修改树节点（/api/{controller}/tree/update） */
  async updateTreeNode(e, t, l) {
    var f, d;
    const n = l ? We(l) : {}, i = {
      [((f = this.treeConfig) == null ? void 0 : f.CodeField) ?? "Code"]: e.Code,
      [((d = this.treeConfig) == null ? void 0 : d.NameField) ?? "Name"]: t,
      ...n
    }, s = await this.apiPost("/tree/update", i), c = this.dtoToNode(
      s.data ?? { ...e, Name: t },
      this.treeSide.findParent(e.Code)
    );
    this.treeSide.replaceNode(e.Code, c) || (e.Name = t);
  }
  /** 删除树节点（skipConfirm=true 时由调用方负责确认） */
  async deleteTreeNode(e, t = !1) {
    var n, i, s;
    if (!((n = this.treeConfig) != null && n.AllowDeleteWithChildren) && e.Children && e.Children.length > 0) {
      J.warning("该节点包含子节点，请先删除子节点");
      return;
    }
    t || await Se.confirm(`确定删除节点 "${e.Name}"？`, "删除确认", {
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
    ((s = this.selectedNode) == null ? void 0 : s.Code) === e.Code && (this.treeSide.selectedNode.value = null, await this.loadPageWithoutTree());
  }
  /** 树节点执行自定义操作 */
  async executeTreeAction(e, t, l) {
    var s;
    const n = l ? We(l) : {}, i = await this.apiPost(`/tree/action/${e}`, {
      [((s = this.treeConfig) == null ? void 0 : s.CodeField) ?? "Code"]: t.Code,
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
      const s = t.charAt(0).toLowerCase() + t.slice(1);
      return s !== t && (i[s] = l.data.IsValid), e.Extra = { ...i }, J.success(l.data.IsValid === 1 ? "已启用" : "已禁用"), l.data;
    }
    return null;
  }
  /** 切换树节点有效标志（完整流程：确认弹窗 → API → 本地更新） */
  async toggleTreeNodeWithConfirm(e, t) {
    const l = this.enableField ?? "IsValid", n = e.Extra || {}, i = l.charAt(0).toLowerCase() + l.slice(1), c = (n[l] ?? n[i] ?? 1) === 1 ? "禁用" : "启用", f = (t == null ? void 0 : t.entityName) ?? e.Name;
    await Se.confirm(`确定${c}【${f}】？`, `${c}确认`, {
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
    })).data ?? []).map((s) => this.dtoToNode(s, e));
    for (const s of n) this.treeSide.register(s, e);
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
class dt {
  constructor(o) {
    // ──── 左树 ────
    z(this, "treeData", T([]));
    z(this, "selectedNode", T(null));
    // ──── 右侧关联数据 ────
    z(this, "associationData", T([]));
    // ──── 加载状态 ────
    z(this, "loading", T(!1));
    z(this, "saving", T(!1));
    // ──── 本地关联缓存（badge / 差集保存依据） ────
    z(this, "associationCache", Ce(/* @__PURE__ */ new Map()));
    z(this, "cacheLoaded", !1);
    // ──── API 注入 ────
    z(this, "api");
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
class gl extends dt {
  /** 可勾选的节点类型（如 ['menu']）—— 子类必须声明 */
  get selectableNodeTypes() {
    return [];
  }
}
class vl extends dt {
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
    z(this, "linkApi");
    /** 搜索关键字（页面可绑定本地过滤） */
    z(this, "searchKey", T(""));
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
    ), i = [], s = [];
    for (const d of this.associationData.value) {
      const b = d[this.linkedKeyField], w = e.has(b), m = n.has(b);
      w && !m && i.push(d), !w && m && s.push(d);
    }
    for (const d of this.associationData.value)
      d.Linked = e.has(d[this.linkedKeyField]);
    const c = [
      ...i.map((d) => this.buildSavePayload(l, d, !0)),
      ...s.map((d) => this.buildSavePayload(l, d, !1))
    ];
    if (c.length === 0) return;
    const f = [];
    for (const d of c)
      try {
        await this.linkApi.save(d);
      } catch {
        const b = this.associationData.value.find(
          (w) => w[this.linkedKeyField] === d[this.linkedKeyField]
        );
        b && (b.Linked = !b.Linked), f.push(t(b ?? d));
      }
    f.length > 0 && J.error(`保存失败：${f.join("、")}`);
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
const bl = [
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
function ga(a, o, e) {
  if (o === null || o === "")
    return [...a, e];
  const t = (l) => l.map((n) => n.Code === o ? { ...n, Children: [...n.Children ?? [], e] } : n.Children && n.Children.length > 0 ? { ...n, Children: t(n.Children) } : n);
  return t(a);
}
function va(a, o) {
  const e = [], t = (l) => {
    const n = [];
    for (const i of l)
      i.Code === o ? (e.push(i), Ca(i).forEach((s) => e.push(s))) : i.Children && i.Children.length > 0 ? n.push({ ...i, Children: t(i.Children) }) : n.push(i);
    return n;
  };
  return { tree: t(a), removed: e };
}
function Cl(a, o, e, t) {
  const l = qe(a, o);
  if (!l)
    return { tree: a, error: `节点 "${o}" 不存在` };
  if (o === e)
    return { tree: a, error: "不能移动到自己" };
  if (e !== null && e !== "") {
    if (!qe(a, e))
      return { tree: a, error: `目标父节点 "${e}" 不存在` };
    if (ct(l, e))
      return { tree: a, error: "不能移动到自己的子树下（会形成循环）" };
  }
  if (t !== void 0 && t > 0) {
    const s = ut(l);
    if ((e === null || e === "" ? 0 : wa(a, e)) + 1 + s > t)
      return { tree: a, error: `移动后深度将超过限制 (${t})` };
  }
  const { tree: n } = va(a, o);
  return { tree: ga(n, e, {
    ...l,
    ParentCode: e
  }) };
}
function wl(a, o, e) {
  const t = (l) => l.map((n) => n.Code === o ? { ...n, ...e } : n.Children && n.Children.length > 0 ? { ...n, Children: t(n.Children) } : n);
  return t(a);
}
function kl(...a) {
  const o = [];
  for (const e of a)
    o.push(...e);
  return o;
}
function Tl(a, o) {
  const e = ot(a), t = ot(o), l = new Map(e.map((f) => [f.node.Code, f])), n = new Map(t.map((f) => [f.node.Code, f])), i = [], s = [], c = [];
  for (const [, f] of n) {
    const d = l.get(f.node.Code);
    if (!d)
      i.push(f.node);
    else if (!Ta(d.node, f.node)) {
      const b = xa(d.node, f.node);
      c.push({ code: f.node.Code, changes: b });
    }
  }
  for (const [f] of l)
    n.has(f) || s.push(f);
  return { added: i, removed: s, updated: c };
}
function xl(a) {
  const o = [], e = ba(a), t = new Set(e.map((n) => n.Code)), l = /* @__PURE__ */ new Map();
  for (const n of e)
    l.set(n.Code, (l.get(n.Code) ?? 0) + 1);
  for (const [n, i] of l)
    i > 1 && o.push(`Code "${n}" 重复 ${i} 次`);
  for (const n of e)
    n.ParentCode != null && n.ParentCode !== "" && !t.has(n.ParentCode) && o.push(`节点 "${n.Name}" 的 ParentCode "${n.ParentCode}" 不存在`);
  return ka(a) && o.push("树存在循环引用"), {
    valid: o.length === 0,
    errors: o
  };
}
function ba(a) {
  const o = [], e = (t) => {
    for (const l of t)
      o.push(l), l.Children && l.Children.length > 0 && e(l.Children);
  };
  return e(a), o;
}
function qe(a, o) {
  for (const e of a) {
    if (e.Code === o) return e;
    if (e.Children && e.Children.length > 0) {
      const t = qe(e.Children, o);
      if (t) return t;
    }
  }
  return null;
}
function Ca(a) {
  const o = [], e = (t) => {
    o.push(t);
    for (const l of t.Children ?? []) e(l);
  };
  return e(a), o;
}
function ct(a, o) {
  for (const e of a.Children ?? [])
    if (e.Code === o || ct(e, o)) return !0;
  return !1;
}
function ut(a) {
  if (!a.Children || a.Children.length === 0) return 1;
  let o = 0;
  for (const e of a.Children)
    o = Math.max(o, ut(e));
  return o + 1;
}
function wa(a, o) {
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
function ka(a) {
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
function ot(a) {
  const o = [], e = (t, l) => {
    for (const n of t)
      o.push({ node: n, level: l }), n.Children && n.Children.length > 0 && e(n.Children, l + 1);
  };
  return e(a, 0), o;
}
function Ta(a, o) {
  return a.Code === o.Code && a.Name === o.Name && a.ParentCode === o.ParentCode && a.NodeType === o.NodeType && a.IsLeaf === o.IsLeaf && a.Sort === o.Sort;
}
function xa(a, o) {
  const e = {};
  return a.Name !== o.Name && (e.Name = o.Name), a.ParentCode !== o.ParentCode && (e.ParentCode = o.ParentCode), a.NodeType !== o.NodeType && (e.NodeType = o.NodeType), a.IsLeaf !== o.IsLeaf && (e.IsLeaf = o.IsLeaf), a.Sort !== o.Sort && (e.Sort = o.Sort), e;
}
function Sa(a, o, e) {
  if (!a || a.length === 0) return [];
  const {
    codeField: t,
    nameField: l,
    parentCodeField: n,
    typeField: i,
    leafField: s,
    sortField: c,
    extraFields: f,
    rootParentCode: d
  } = o, b = (e == null ? void 0 : e.maxLevel) ?? 0, w = e == null ? void 0 : e.startFromCode, m = (e == null ? void 0 : e.currentLevel) ?? 0;
  if (b > 0 && m >= b) return [];
  const p = /* @__PURE__ */ new Map(), A = [];
  for (const h of a) {
    const x = ue(h, t), K = ue(h, l), ee = ue(h, n) ?? null, ne = i ? ue(h, i) : void 0, re = s ? ue(h, s) : void 0, se = c ? ue(h, c) : void 0;
    let F;
    if (f && f.length > 0) {
      F = {};
      for (const q of f)
        F[q] = ue(h, q);
    }
    const E = {
      Code: x,
      Name: K,
      ParentCode: ee,
      NodeType: ne,
      IsLeaf: re,
      Sort: se,
      Extra: F,
      Children: [],
      Raw: h
    };
    p.set(x, E);
  }
  for (const h of p.values())
    if (w && h.Code === w)
      A.push(h);
    else if (!w && (h.ParentCode === d || h.ParentCode === null || h.ParentCode === ""))
      A.push(h);
    else {
      const x = p.get(h.ParentCode ?? "");
      x && (x.Children = [...x.Children ?? [], h]);
    }
  return A.sort((h, x) => (h.Sort ?? 0) - (x.Sort ?? 0)), A;
}
function _a(a, o, e) {
  const {
    codeField: t,
    nameField: l,
    parentCodeField: n,
    typeField: i,
    leafField: s,
    sortField: c,
    extraFields: f
  } = o, d = ue(a, t), b = ue(a, l), w = ue(a, n) ?? null, m = i ? ue(i, i) : void 0, p = s ? ue(s, s) : void 0, A = c ? ue(a, c) : void 0;
  let h;
  if (f && f.length > 0) {
    h = {};
    for (const x of f)
      h[x] = ue(a, x);
  }
  return {
    Code: d,
    Name: b,
    ParentCode: w,
    NodeType: m,
    IsLeaf: p,
    Sort: A,
    Extra: h,
    Children: [],
    Raw: a
  };
}
function Aa(a, o) {
  const e = {
    [o.codeField]: a.Code,
    [o.nameField]: a.Name,
    [o.parentCodeField]: a.ParentCode
  };
  return o.typeField && a.NodeType && (e[o.typeField] = a.NodeType), o.sortField && a.Sort !== void 0 && (e[o.sortField] = a.Sort), e;
}
function Xe(a) {
  const o = [], e = (t) => {
    for (const l of t)
      o.push(l), l.Children && l.Children.length > 0 && e(l.Children);
  };
  return e(a), o;
}
function Je(a) {
  const o = [], e = (t) => {
    for (const l of t)
      o.push(l), l.Children && l.Children.length > 0 && e(l.Children);
  };
  return e(a.Children ?? []), o;
}
function Fa(a) {
  return [a, ...Je(a)];
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
function za(a, o) {
  return [...Me(o, a).map((t) => t.Code), o.Code];
}
function Na(a, o) {
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
function ht(a, o) {
  for (const e of a) {
    if (o(e)) return e;
    if (e.Children && e.Children.length > 0) {
      const t = ht(e.Children, o);
      if (t) return t;
    }
  }
  return null;
}
function Ze(a, o) {
  const e = [], t = (l) => {
    for (const n of l)
      o(n) && e.push(n), n.Children && n.Children.length > 0 && t(n.Children);
  };
  return t(a), e;
}
function Da(a, o) {
  return o.ParentCode ? Ue(a, o.ParentCode) : null;
}
function $a(a, o) {
  return Ze(a, (e) => e.NodeType === o);
}
function ft(a, o) {
  if (!o || !o.trim()) return [];
  const e = o.toLowerCase();
  return Ze(a, (t) => t.Name.toLowerCase().includes(e));
}
function Ra(a, o) {
  const e = ft(a, o), t = /* @__PURE__ */ new Set();
  for (const l of e)
    t.add(l), Me(l, a).forEach((i) => t.add(i));
  return Array.from(t);
}
function Ba(a, o) {
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
function Pa(a) {
  return Je(a).length;
}
function Va(a) {
  const o = (e, t) => {
    if (e.length === 0) return t;
    let l = t;
    for (const n of e)
      n.Children && n.Children.length > 0 && (l = Math.max(l, o(n.Children, t + 1)));
    return l;
  };
  return o(a, 0);
}
function La(a) {
  return Xe(a).length;
}
function Ea(a, o) {
  const e = [], t = (l, n) => {
    for (const i of l)
      n === o && e.push(i), i.Children && i.Children.length > 0 && t(i.Children, n + 1);
  };
  return t(a, 0), e;
}
function Ma(a) {
  const o = [], e = Xe(a), t = new Set(e.map((n) => n.Code));
  for (const n of e)
    !n.Code && n.Code !== null && o.push(`节点 ${n.Name} 的 Code 为空`);
  const l = /* @__PURE__ */ new Map();
  for (const n of e)
    l.set(n.Code, (l.get(n.Code) ?? 0) + 1);
  for (const [n, i] of l)
    i > 1 && o.push(`Code "${n}" 重复 ${i} 次`);
  for (const n of e)
    n.ParentCode != null && n.ParentCode !== "" && !t.has(n.ParentCode) && o.push(`节点 "${n.Name}" 的 ParentCode "${n.ParentCode}" 不存在`);
  return pt(a) && o.push("树存在循环引用"), {
    valid: o.length === 0,
    errors: o
  };
}
function pt(a) {
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
function ue(a, o) {
  if (!a || !o) return;
  const e = o.split(".");
  let t = a;
  for (const l of e) {
    if (t == null) return;
    t = t[l];
  }
  return t;
}
const Sl = {
  // 构造
  buildTree: Sa,
  entityToNode: _a,
  nodeToEntity: Aa,
  flattenTree: Xe,
  // 遍历/查询
  getDescendants: Je,
  getDescendantsWithSelf: Fa,
  getAncestors: Me,
  getPath: za,
  getPathNames: Na,
  findNode: Ue,
  findNodeBy: ht,
  findNodesBy: Ze,
  getParent: Da,
  // 过滤/搜索
  filterByType: $a,
  search: ft,
  searchWithAncestors: Ra,
  filterTree: Ba,
  // 统计
  getChildrenCount: Pa,
  getDepth: Va,
  getTotalCount: La,
  getNodesAtLevel: Ea,
  // 验证
  validate: Ma,
  hasCycle: pt
};
export {
  dt as AssociationTreeCore,
  gl as CheckTreeCore,
  vl as LinkTableCore,
  bl as OrgNodeTypeExamples,
  pa as SingleTableCore,
  ma as TreeSide,
  yl as TreeTableCore,
  yl as TreeTableLogic,
  rt as YzhApiClient,
  Za as YzhCard,
  Ya as YzhDialog,
  Xa as YzhEmptyState,
  Lt as YzhForm,
  Ka as YzhFormDialog,
  ja as YzhPageLayout,
  Zt as YzhPagination,
  Yt as YzhSearchBar,
  Ja as YzhStatusBadge,
  nt as YzhTable,
  Xt as YzhToolbar,
  lt as YzhTree,
  Ha as YzhTreeTable,
  Ga as YzhTreeTableCheckSelector,
  Wa as YzhTreeTableLayout,
  qa as YzhTreeTableSelector,
  ga as addNode,
  Sa as buildTree,
  nl as deleteStorageFile,
  Tl as diff,
  _a as entityToNode,
  sl as fileExists,
  $a as filterByType,
  Ba as filterTree,
  Ue as findNode,
  ht as findNodeBy,
  Ze as findNodesBy,
  ba as flatten,
  Xe as flattenTree,
  Me as getAncestors,
  Pa as getChildrenCount,
  Va as getDepth,
  Je as getDescendants,
  Fa as getDescendantsWithSelf,
  ll as getFileUrl,
  Ea as getNodesAtLevel,
  Da as getParent,
  za as getPath,
  Na as getPathNames,
  La as getTotalCount,
  pt as hasCycle,
  rl as listFiles,
  sa as mapControlType,
  ia as mapSearchControlType,
  ra as mapSearchType,
  kl as mergeRoots,
  Cl as moveSubtree,
  Aa as nodeToEntity,
  We as pascalCaseFormData,
  va as removeSubtree,
  ml as rowToFormData,
  ft as search,
  Ra as searchWithAncestors,
  it as toCamelCase,
  ca as toFormFields,
  He as toFormLayoutCols,
  fa as toPascalCase,
  Qa as toRowActionButtons,
  st as toRowActions,
  tt as toSearchFields,
  da as toTableColumns,
  ua as toToolbarActions,
  el as toTreeActions,
  be as tokenStore,
  tl as treeItemToNode,
  Sl as treeUtils,
  wl as updateNode,
  ol as uploadFile,
  al as uploadFileBatch,
  il as useAuth,
  fl as useCheckTree,
  cl as useConfirm,
  pl as useLinkTable,
  ul as useSingleTable,
  dl as useTable,
  hl as useTreeTable,
  Ma as validate,
  xl as validateTreeOps,
  Ne as yzhApi
};
