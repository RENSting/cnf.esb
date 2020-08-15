using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace cnf.esb.web.Models
{
    /// <summary>
    /// Enumerator all types of the value which is held by a JSON item
    /// </summary>
    public enum ValueType
    {
        [Display(Name = "字符串")]
        String = 0,
        [Display(Name = "整数")]
        Integer = 1,
        [Display(Name = "小数")]
        Float = 2,
        [Display(Name = "日期")]
        Date = 3,
        [Display(Name = "布尔型")]
        Boolean = 4,
        [Display(Name = "对象类型")]
        Object = 5,
    }

    public class JsonTemplate
    {
        const int SAMPLE_ARRAY_LENGTH = 2;

        /// <summary>
        /// 递归处理JsonTemplate，输出一个示例JSON Body
        /// </summary>
        public void WriteJsonBody(JsonWriter writer)
        {
            if (IsArray)
            {
                writer.WriteStartArray();
                for (int i = 0; i < SAMPLE_ARRAY_LENGTH; i++)
                {
                    WriteJsonIgnoreArray(writer);
                }
                writer.WriteEndArray();
            }
            else
            {
                WriteJsonIgnoreArray(writer);
            }
        }

        void WriteJsonIgnoreArray(JsonWriter writer)
        {
            switch (ValueType)
            {
                case Models.ValueType.Boolean:
                    writer.WriteValue(true);
                    break;
                case Models.ValueType.Date:
                    writer.WriteValue(DateTime.Now);
                    break;
                case Models.ValueType.Float:
                    writer.WriteValue(3.14F);
                    break;
                case Models.ValueType.Integer:
                    writer.WriteValue(108);
                    break;
                case Models.ValueType.String:
                    writer.WriteValue("hello world");
                    break;
                case Models.ValueType.Object:
                    writer.WriteStartObject();
                    foreach (var property in ObjectProperties)
                    {
                        writer.WritePropertyName(property.Key);
                        property.Value.WriteJsonBody(writer);
                    }
                    writer.WriteEndObject();
                    break;
                default:
                    throw new Exception("没有定义的JSON值类型");
            }
        }

        internal void ReplaceWith(string path, JsonTemplate replacement)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new Exception("根路径不适合于替换子模板的导航");
            }
            int indexOfDot = path.IndexOf('.');
            string currentLocator = indexOfDot < 0 ? path : path.Substring(0, indexOfDot);
            string restLocator = indexOfDot < 0 ? string.Empty : path.Substring(indexOfDot + 1);
            if (ValueType == ValueType.Object)
            {
                if (!ObjectProperties.ContainsKey(currentLocator))
                {
                    throw new Exception($"路径是错误的，当前成员不包含{currentLocator}属性");
                }
                if (string.IsNullOrWhiteSpace(restLocator))
                {
                    //将替换为对象元素
                    ObjectProperties[currentLocator] = replacement;
                }
                else
                {
                    ObjectProperties[currentLocator].ReplaceWith(restLocator, replacement);
                }
            }
            else
            {
                throw new Exception($"{ValueType.ToString()}没有成员可以替换");
            }
        }

        internal JsonTemplate FindTemplate(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return this;
            }
            int indexOfDot = path.IndexOf('.');
            string currentLocator = indexOfDot < 0 ? path : path.Substring(0, indexOfDot);
            string restLocator = indexOfDot < 0 ? string.Empty : path.Substring(indexOfDot + 1);
            if (ValueType == ValueType.Object)
            {
                if (!ObjectProperties.ContainsKey(currentLocator))
                {
                    throw new Exception($"路径是错误的，当前成员不包含{currentLocator}属性");
                }
                else
                {
                    return ObjectProperties[currentLocator].FindTemplate(restLocator);
                }
            }
            else
            {
                throw new Exception($"{ValueType.ToString()}没有成员可以替换");
            }
        }

        public void GenerateMappingInto(List<ParameterMapping> mappings, string parentPath, string parentPattern)
        {
            if (string.IsNullOrWhiteSpace(parentPattern)) parentPattern = "(root)";
            if (IsArray) parentPattern = parentPattern + "[]";

            switch (ValueType)
            {
                case ValueType.Boolean:
                case ValueType.Date:
                case ValueType.Float:
                case ValueType.Integer:
                case ValueType.String:
                    mappings.Add(new ParameterMapping
                    {
                        Source = "json",
                        ServerPath = parentPath,
                        MappingType = MappingType.Path,
                        ClientPath = parentPattern
                    });
                    break;
                case ValueType.Object:
                    foreach (var property in ObjectProperties)
                    {
                        string currentPath = string.IsNullOrWhiteSpace(parentPath) ? property.Key : $"{parentPath}.{property.Key}";
                        string currentPattern = $"{parentPattern}.{property.Key}";
                        property.Value.GenerateMappingInto(mappings, currentPath, currentPattern);
                    }
                    break;
                default:
                    break;
            }
        }

        public JsonTemplate()
        {
            ObjectProperties = new Dictionary<string, JsonTemplate>();
        }

        public static JsonTemplate Create(ValueType jsonType, bool isArray) =>
            new JsonTemplate { IsArray = isArray, ValueType = jsonType };

        [Display(Name = "JSON类型")]
        public ValueType ValueType { get; set; }

        /// <summary>
        /// 这个模板定义的是一个数组，元素的类型是ValueType
        /// </summary>
        /// <value></value>
        [Display(Name = "是数组")]
        public bool IsArray { get; set; }

        /// <summary>
        /// JSON对象类型的属性字典，仅针对Object类型的JSON模板才有意义。
        /// </summary>
        /// <value></value>
        public Dictionary<string, JsonTemplate> ObjectProperties { get; set; }

    }

}