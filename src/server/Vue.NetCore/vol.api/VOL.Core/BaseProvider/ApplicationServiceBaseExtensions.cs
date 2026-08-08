using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VOL.Core.Configuration;
using VOL.Core.Extensions;
using VOL.Core.Utilities;
using VOL.Core.WorkFlow;

namespace VOL.Core.BaseProvider
{
    public static class ApplicationServiceBaseExtensions
    {
        /// <summary>
        /// 设置主键默认值
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="dic"></param>
        public static Dictionary<string, object> SetPrimaryKeyDefaultValue<TEntity>(this Dictionary<string, object> dic,
            string mainKeyName = null,
            object mainKeyValue = null) where TEntity : class
        {
            PropertyInfo keyProperty = typeof(TEntity).GetKeyProperty();
            //给明细表设置主表主键字段
            if (mainKeyName != null && keyProperty != null)
            {
                dic[mainKeyName] = mainKeyValue;
            }

            if (keyProperty.PropertyType == typeof(Guid))
            {
                dic[keyProperty.Name] = Guid.NewGuid();
            }
            else if (keyProperty.PropertyType == typeof(long) && AppSetting.EnableSnowFlakeID)
            {
                dic[keyProperty.Name] = new IdWorker().NextId();
            }
            else if (keyProperty.PropertyType == typeof(string))
            {
                //字符串主键没有值的使用默认guid类型
                if (dic.TryGetValue(keyProperty.Name, out object keyValue))
                {
                    if (keyValue == null || keyValue?.ToString()?.Trim() == "")
                    {
                        dic[keyProperty.Name] = Guid.NewGuid();
                    }
                }
            }
            else
            {
                dic.Remove(keyProperty.Name);
            }
            return dic;
        }
        /// <summary>
        /// 判断实体是否为新增数据
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static bool IsAdd<TEntity>(this TEntity entity) where TEntity : class
        {
            var keyProperty = typeof(TEntity).GetKeyProperty();
            var value = keyProperty.GetValue(entity);

            if (value == null) return true;
            if (keyProperty.PropertyType == typeof(string) && value?.ToString()?.Trim() == "")
            {
                return true;
            }
            var type = keyProperty.PropertyType;
            var defaultValue = type.IsValueType ? Activator.CreateInstance(type) : null;
            return Equals(value, defaultValue);
        }
        /// <summary>
        /// 判断提交的字典是否为新增数据
        /// </summary>
        /// <param name="dic"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsTypeAdd(this Dictionary<string, object> dic, Type type)
        {
            var keyProperty = type.GetKeyProperty();
            if (!dic.TryGetValue(keyProperty.Name, out object value) || value?.ToString()?.Trim() == "")
            {
                return true;
            }
            if (keyProperty.PropertyType == typeof(string))
            {
                return value?.ToString()?.Trim() == "";
            }
            var keyType = keyProperty.PropertyType;
            var defaultValue = keyType.IsValueType ? Activator.CreateInstance(keyType) : null;
            return Equals(value.ChangeType(keyType), defaultValue);
        }
        /// <summary>
        /// 设置主键默认值
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entity"></param>
        public static TEntity SetPrimaryKeyDefaultValue<TEntity>(this TEntity entity,
            string mainKeyName = null,
            object mainKeyValue = null) where TEntity : class
        {
            PropertyInfo keyProperty = typeof(TEntity).GetKeyProperty();
            //给明细表设置主表主键字段
            if (mainKeyName != null && keyProperty != null)
            {
                typeof(TEntity).GetProperty(mainKeyName).SetValue(entity, mainKeyValue);
            }

            if (keyProperty.PropertyType == typeof(Guid))
            {
                keyProperty.SetValue(entity, Guid.NewGuid());
            }
            else if (keyProperty.PropertyType == typeof(long) && AppSetting.EnableSnowFlakeID)
            {
                keyProperty.SetValue(entity, new IdWorker().NextId());
            }
            else if (keyProperty.PropertyType == typeof(string))
            {
                keyProperty.SetValue(entity, Guid.NewGuid().ToString());
            }
            return entity;
        }
        public static List<TEntity> SetPrimaryKeyDefaultListValue<TEntity>(this List<TEntity> list) where TEntity : class
        {
            foreach (TEntity entity in list)
            {
                entity.SetPrimaryKeyDefaultValue();
            }
            return list;
        }

        /// <summary>
        /// 设置审批字段默认值
        /// </summary>
        /// <param name="entity"></param>
        public static Dictionary<string, object> SetAuditDefaultValue<TEntity>(this Dictionary<string, object> dic) where TEntity : class
        {
            var propertyInfo = GetAuditFieldPropertyInfo<TEntity>();
            if (propertyInfo != null)
            {
                dic[propertyInfo.Name] = (int)AuditStatus.待审核;
            }
            return dic;
        }
        public static TEntity SetAuditDefaultValue<TEntity>(this TEntity entity) where TEntity : class
        {
            var propertyInfo = GetAuditFieldPropertyInfo<TEntity>();
            propertyInfo?.SetValue(entity, (int)AuditStatus.待审核);
            return entity;
        }
        public static PropertyInfo GetAuditFieldPropertyInfo<TEntity>()
        {
            return typeof(TEntity).GetAuditFieldPropertyInfo();
        }
        public static PropertyInfo GetAuditFieldPropertyInfo(this Type type)
        {
            string field = AppSetting.GetSettingString("AuditStatusField");
            if (string.IsNullOrEmpty(field))
            {
                field = "AuditStatus";
            }
            var propertyInfo = type.GetProperties().Where(x => x.Name.Equals(field, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
            return propertyInfo;
        }
        /// <summary>
        ///数据版本号管理
        /// </summary>
        /// <param name="model"></param>
        public static Dictionary<string, object> SetDataVersionDefaultValue(this Dictionary<string, object> model, string dataVersionField)
        {
            if (!string.IsNullOrEmpty(dataVersionField))
            {
                model[dataVersionField] = Guid.NewGuid().ToString();
            }
            return model;
        }
        /// <summary>
        /// 设置逻辑删除
        /// </summary>
        /// <typeparam name="TLogicEntity"></typeparam>
        /// <param name="dic"></param>
        public static Dictionary<string, object> SetLogicDelValue<TLogicEntity>(this Dictionary<string, object> dic) where TLogicEntity : class
        {
            var property = typeof(TLogicEntity).GetLogicDelPropertyWithType();
            if (property != null)
            {
                dic[property.Name] = (int)DelStatus.正常;
            }
            return dic;
        }
        public static TLogicEntity SetLogicDelValue<TLogicEntity>(this TLogicEntity entity) where TLogicEntity : class
        {
            var property = typeof(TLogicEntity).GetLogicDelPropertyWithType();
            property?.SetValue(entity, ((int)DelStatus.正常).ChangeType(property?.PropertyType));
            return entity;
        }

        public static List<TLogicEntity> SetLogicDelListValue<TLogicEntity>(this List<TLogicEntity> list) where TLogicEntity : class
        {
            foreach (var entity in list)
            {
                entity.SetLogicDelValue();
            }
            return list;
        }

        /// <summary>
        /// 获取实体的逻辑删除属性
        /// 
        /// YZH 扩展（2026-08-07）：
        /// - 原始实现返回 null，导致所有实体都走物理删除
        /// - 现在识别 YZHBaseEntity 及其子类的 Enable 属性（bool 类型）
        /// - 有 Enable 属性 → 软删除（ExecuteUpdate SET Enable = 0）
        /// - 无 Enable 属性 → 物理删除（原始行为）
        /// </summary>
        public static PropertyInfo GetLogicDelPropertyWithType(this Type type)
        {
            if (type == null) return null;

            // YZH 扩展：检测 YZHBaseEntity 子类的 Enable 字段
            // 判断条件：类型本身或其基类链中有 bool 类型的 Enable 属性
            var currentType = type;
            while (currentType != null && currentType != typeof(object))
            {
                var enableProp = currentType.GetProperty("Enable",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.DeclaredOnly);

                if (enableProp != null && enableProp.PropertyType == typeof(bool))
                {
                    return enableProp;
                }
                currentType = currentType.BaseType;
            }

            return null;
        }

        public static bool IsTableActionLog(this Type type)
        {
            return false;
        }
        public static Type GetActionLogEntityType(this Type type)
        {
            if (type?.IsGenericType != true)
                return type;

            Type def = type.GetGenericTypeDefinition();
            if (def == typeof(List<>) || def == typeof(IList<>))
            {
                Type[] args = type.GenericTypeArguments;
                if (args.Length == 1)
                    return args[0];
            }
            return type;
        }


        public static object ListObjectTypeToObject(this List<object> list, Type type)
        {
            return typeof(ApplicationServiceBaseExtensions)
                    .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                    .First(m => m.Name == nameof(ListObjectToObject))
                    .MakeGenericMethod(type).Invoke(null, [list]);
        }

        private static List<TEntity> ListObjectToObject<TEntity>(List<object> list) where TEntity : class
        {
            return list.Select(s => (TEntity)s).ToList();
        }
    }
}
