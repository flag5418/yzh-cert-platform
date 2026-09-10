using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace YZH.Core.CodeGenerators
{
    /// <summary>
    /// 实体类 → TypeScript 类型生成器
    /// 在构建时运行，生成前端类型定义
    /// </summary>
    public static class EntityToTsGenerator
    {
        /// <summary>
        /// 生成单个实体的 TypeScript 类型
        /// </summary>
        public static string Generate<T>()
        {
            return Generate(typeof(T));
        }

        /// <summary>
        /// 生成单个实体的 TypeScript 类型（反射）
        /// </summary>
        public static string Generate(Type type)
        {
            var sb = new StringBuilder();
            var className = type.Name;

            sb.AppendLine($"/** 自动生成的 TypeScript 类型（来源：{className}） */");
            sb.AppendLine($"export interface {className}Db {{");

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propName = ToCamelCase(prop.Name);
                var propType = ToTsType(prop.PropertyType);
                var isRequired = !IsNullableType(prop.PropertyType);
                sb.AppendLine($"  {propName}{(isRequired ? "" : "?")}: {propType};");
            }

            sb.AppendLine("}");
            sb.AppendLine();

            // 生成 EntityConfig 类型（从属性生成）
            sb.AppendLine($"/** 实体配置类型（从 {className} 反射生成） */");
            sb.AppendLine($"export interface {className}Config {{");
            sb.AppendLine($"  Title: string;");
            sb.AppendLine($"  Columns: {className}Column[];");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine($"export interface {className}Column {{");
            sb.AppendLine($"  FieldName: string;");
            sb.AppendLine($"  DesName: string;");
            sb.AppendLine($"  Type: string;");
            sb.AppendLine($"  XsFlag: boolean;");
            sb.AppendLine($"  BcFlag: boolean;");
            sb.AppendLine($"  Yxk: boolean;");
            sb.AppendLine($"  Width?: number;");
            sb.AppendLine("}");
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// 批量生成多个实体的 TypeScript 类型
        /// </summary>
        public static string GenerateAll(params Type[] types)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// 自动生成的 TypeScript 类型定义");
            sb.AppendLine("// 来源：YZH.Core.CodeGenerators.EntityToTsGenerator");
            sb.AppendLine("// 不要手动修改此文件！");
            sb.AppendLine();

            foreach (var type in types)
            {
                sb.AppendLine(Generate(type));
                sb.AppendLine("---");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// 生成 TypeScript 类型字符串
        /// </summary>
        private static string ToTsType(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(int) || type == typeof(long) || type == typeof(short)) return "number";
            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return "number";
            if (type == typeof(bool)) return "boolean";
            if (type == typeof(DateTime) || type == typeof(DateTime?)) return "string";
            if (type == typeof(Guid) || type == typeof(Guid?)) return "string";

            // 可空类型
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingType = type.GetGenericArguments()[0];
                return $"{ToTsType(underlyingType)} | null";
            }

            // 集合类型
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var itemType = type.GetGenericArguments()[0];
                return $"{ToTsType(itemType)}[]";
            }

            if (type.IsArray)
            {
                var itemType = type.GetElementType();
                return $"{ToTsType(itemType)}[]";
            }

            // 字典类型
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var keyType = type.GetGenericArguments()[0];
                var valueType = type.GetGenericArguments()[1];
                return `Record<${ToTsType(keyType)}, ${ToTsType(valueType)}>`;
            }

            // 自定义类型（返回类型名）
            return type.Name;
        }

        /// <summary>
        /// 判断类型是否可空
        /// </summary>
        private static bool IsNullableType(Type type)
        {
            if (type == typeof(string)) return true;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) return true;
            return false;
        }

        /// <summary>
        /// PascalCase → camelCase
        /// </summary>
        private static string ToCamelCase(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return char.ToLowerInvariant(str[0]) + str.Substring(1);
        }
    }
}
