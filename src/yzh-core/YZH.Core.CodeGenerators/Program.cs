using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using YZH.Entity.Admin.Platform.Sys;

namespace YZH.Core.CodeGenerators
{
    /// <summary>
    /// 实体类 → TypeScript 类型生成器
    /// 运行方式：dotnet run --project YZH.Core.CodeGenerators
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            var outputDir = args.Length > 0 ? args[0] : "../../../../../src/certplatform-web/share/src/types/generated";
            Directory.CreateDirectory(outputDir);

            // 生成所有实体的 TypeScript 类型
            var entities = new[]
            {
                typeof(Sys_User),
                typeof(Sys_Organization),
                typeof(Sys_Role),
                typeof(Sys_Menu),
                typeof(CertStage),
                typeof(CertOrgStage)
            };

            foreach (var entity in entities)
            {
                var content = EntityToTsGenerator.Generate(entity);
                var fileName = $"{entity.Name}.ts";
                File.WriteAllText(Path.Combine(outputDir, fileName), content);
                Console.WriteLine($"Generated: {fileName}");
            }

            // 生成统一的导出文件
            var exportContent = GenerateExportFile(entities);
            File.WriteAllText(Path.Combine(outputDir, "index.ts"), exportContent);
            Console.WriteLine($"Generated: index.ts");

            Console.WriteLine($"\nAll TypeScript types generated to: {outputDir}");
        }

        static string GenerateExportFile(Type[] entities)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// 自动生成的 TypeScript 类型导出文件");
            sb.AppendLine("// 来源：YZH.Core.CodeGenerators");
            sb.AppendLine("// 不要手动修改此文件！");
            sb.AppendLine();

            foreach (var entity in entities)
            {
                var className = entity.Name;
                sb.AppendLine($"export type {className}Db = import('./{className}').{className}Db;");
                sb.AppendLine($"export type {className}Config = import('./{className}').{className}Config;");
                sb.AppendLine($"export type {className}Column = import('./{className}').{className}Column;");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// 实体类 → TypeScript 类型生成器
    /// </summary>
    public static class EntityToTsGenerator
    {
        public static string Generate(Type type)
        {
            var sb = new StringBuilder();
            var className = type.Name;

            sb.AppendLine(`/** 自动生成的 TypeScript 类型（来源：${className}） */`);
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
            sb.AppendLine(`/** 实体配置类型（从 ${className} 反射生成） */`);
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
            sb.AppendLine($"  Enable?: boolean;");
            sb.AppendLine($"  Width?: number;");
            sb.AppendLine($"  Sortable?: boolean;");
            sb.AppendLine("}");
            sb.AppendLine();

            return sb.ToString();
        }

        private static string ToTsType(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(int) || type == typeof(long) || type == typeof(short)) return "number";
            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return "number";
            if (type == typeof(bool)) return "boolean";
            if (type == typeof(DateTime)) return "string";
            if (type == typeof(Guid)) return "string";
            if (type == typeof(byte[])) return "string";

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

        private static bool IsNullableType(Type type)
        {
            if (type == typeof(string)) return true;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) return true;
            return false;
        }

        private static string ToCamelCase(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return char.ToLowerInvariant(str[0]) + str.Substring(1);
        }
    }
}
