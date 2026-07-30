using System.Collections.Generic;
using System.Linq;
using VOL.Core.Configuration;
using VOL.Core.WorkFlow;

namespace VOL.Core.BaseProvider
{
    public static class ApplicationServiceBaseConfig
    {
        /// <summary>
        /// 获取配置的创建人ID创建时间创建人,修改人ID修改时间、修改人与数据相同的字段
        /// </summary>
        private static string[] _userIgnoreFields { get; set; }

        public static string[] UserIgnoreFields
        {
            get
            {
                if (_userIgnoreFields != null) return _userIgnoreFields;
                List<string> fields = new List<string>();

                string LogicDelField=  AppSetting.GetSettingString("LogicDelField");
                //逻辑删除字段
                if (!string.IsNullOrEmpty(LogicDelField))
                {
                    fields.Add(LogicDelField);
                }
                fields.AddRange(CreateFields);
                fields.AddRange(ModifyFields);
                _userIgnoreFields = fields.ToArray();
                return _userIgnoreFields;
            }
        }
        private static string[] _createFields { get; set; }
        public static string[] CreateFields
        {
            get
            {
                if (_createFields != null) return _createFields;
                _createFields = AppSetting.CreateMember.GetType().GetProperties()
                    .Select(x => x.GetValue(AppSetting.CreateMember)?.ToString())
                    .Where(w => !string.IsNullOrEmpty(w)).ToArray();
                return _createFields;
            }
        }

        private static string[] _modifyFields { get; set; }
        public static string[] ModifyFields
        {
            get
            {
                if (_modifyFields != null) return _modifyFields;
                _modifyFields = AppSetting.ModifyMember.GetType().GetProperties()
                    .Select(x => x.GetValue(AppSetting.ModifyMember)?.ToString())
                    .Where(w => !string.IsNullOrEmpty(w)).ToArray();
                return _modifyFields;
            }
        }

        public static List<string> IgnoreTemplate()
        {
            //忽略创建人、修改人、审核等字段
            List<string> ignoreTemplate = UserIgnoreFields.Select(s=>s).ToList();
            ignoreTemplate.AddRange(WorkFlowGeneric.AuditFields);
            return ignoreTemplate;
        }
    }

    public enum DelStatus
    {
        正常 = 0,
        已删除 = 1
    }
}
