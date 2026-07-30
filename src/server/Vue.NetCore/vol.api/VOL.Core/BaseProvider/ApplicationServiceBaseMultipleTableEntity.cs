using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VOL.Core.BaseProvider
{
    public class ApplicationServiceBaseMultipleTableEntity
    {
        public ApplicationServiceBaseMultipleTableEntity()
        {
            Data = [];
        }
        public Dictionary<Type, EntityData> Data { get; set; }
        public Type FirstType()
        {
            return Data.FirstOrDefault().Key;
        }

        /// <summary>
        /// 只返回主从明细表
        /// </summary>
        /// <returns></returns>
        public EntityData FirstData()
        {
            if (Data.Count == 1)
            {
                return Data.FirstOrDefault().Value;
            }
            return new EntityData();
        }
        /// <summary>
        /// 获取添加的表
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public object GetAddList(Type type,Type mainType)
        {
            if (type!=null&&Data.TryGetValue(type, out EntityData data) && data?.AddList?.Count > 0)
            {
                //类型转换
                return data.AddList.ListObjectTypeToObject(type);
            }
            //注意返回的null as 转换确认
            return GetDefaultObjectList(mainType);
        }

        public ApplicationServiceBaseMultipleTableEntity SetAddList(Type type, object list)
        {
            if (type != null && Data.TryGetValue(type, out EntityData data) && list != null)
            {
                Data[type].AddList = (list as IEnumerable<object>).Select(s => s).ToList();
            }
            return this;
        }

        public object GetAddList<T>() where T : class
        {
            return GetAddList(typeof(T),null);
        }
        public object GetUpdateList<T>() where T : class
        {
            return GetUpdateList(typeof(T), null);
        }
        /// <summary>
        /// 获取编辑的表
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public object GetUpdateList(Type type,Type mainType)
        {
            if (type != null && Data.TryGetValue(type, out EntityData data) && data?.UpdateList?.Count > 0)
            {
                //类型转换
                return data.UpdateList.ListObjectTypeToObject(type);
            }
            return GetDefaultObjectList(mainType);
        }
        public ApplicationServiceBaseMultipleTableEntity SetUpdateList(Type type, object list)
        {
            if (type != null && Data.TryGetValue(type, out EntityData data) && list != null)
            {
                Data[type].UpdateList = (list as IEnumerable<object>).Select(s => s).ToList();
            }
            return this;
        }
        private object GetDefaultObjectList(Type mainType)
        {
            if (mainType==null)
            {
                return null;
            }
            //获取主从表，主表默认明细表空list(兼容原旧版本代码参数)
           var detailTypes= mainType.GetDetailTypes();
            if (detailTypes.Length==1)
            {
                Type listType = typeof(List<>).MakeGenericType(detailTypes[0]);
                return (IList)Activator.CreateInstance(listType);
            }
            return null;
        }
        /// <summary>
        /// 获取删除的数据id
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public List<object> GetDelKeys(Type type)
        {
            if (type != null && Data.TryGetValue(type, out EntityData data))
            {
                //类型转换
                return data.DelKeys;
            }
            return null;
        }

        public List<object> GetDelKeys<T>() where T : class
        {
            return GetDelKeys(typeof(T));
        }
    }
    public class EntityData
    {
        public EntityData()
        {
            AddList = [];
            UpdateList = [];
            UpdateFields = [];
        }
        /// <summary>
        /// 添加的实体数据
        /// </summary>
        public List<object> AddList { get; set; }
        /// <summary>
        /// 添加的数据是否手动写入数据库
        /// </summary>
        public bool InsertDb = true;
        /// <summary>
        /// 更新的实体数据
        /// </summary>
        public List<object> UpdateList { get; set; }
        /// <summary>
        /// 更新的字段
        /// </summary>
        public List<string> UpdateFields { get; set; }
        /// <summary>
        /// 删除的主键字段
        /// </summary>
        public List<object> DelKeys { get; set; }
    }
}
