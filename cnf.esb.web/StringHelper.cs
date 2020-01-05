using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using cnf.esb.web.Models;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace cnf.esb.web
{
    public static class StringHelper
    {
        public static void WriteRouteQueryFormJson(JsonWriter writer, string propertyName, 
            string pattern, char separator, string sample)
        {
            writer.WritePropertyName(propertyName);
            writer.WriteStartObject();
            string[] parameters = pattern.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            foreach (string p in parameters)
            {
                writer.WritePropertyName(p.Trim().TrimEnd('='));
                writer.WriteValue(sample);
            }
            writer.WriteEndObject();

        }

        /// <summary>
        /// 将简单数据类型字符串解析成系统类型
        /// </summary>
        /// <param name="type"></param>
        /// <param name="primeValueString"></param>
        /// <returns></returns>
        public static object ParsePrimeValue(Models.ValueType type, string primeValueString)
        {
            if (type == Models.ValueType.Boolean)
            {
                return bool.Parse(primeValueString.ToLower());
            }
            else if (type == Models.ValueType.Date)
            {
                return DateTime.Parse(primeValueString);
            }
            else if (type == Models.ValueType.Float)
            {
                return double.Parse(primeValueString);
            }
            else if (type == Models.ValueType.Integer)
            {
                return int.Parse(primeValueString);
            }
            else if (type == Models.ValueType.String)
            {
                return primeValueString;
            }
            else
            {
                throw new Exception("JSON类型不是简单数据类型");
            }
        }

        /// <summary>
        /// 对于数组形式的形参，返回其中定义的元素部分的JSON Path
        /// </summary>
        /// <param name="clientPath"></param>
        /// <returns></returns>
        public static string ParseElementPath(string clientPath)
        {
            if (clientPath.EndsWith("[]"))
            {
                return string.Empty;
            }
            else
            {
                var lastArrayDelimit = clientPath.LastIndexOf("[]");
                if (lastArrayDelimit < 0) throw new Exception("服务器要求传入数组，但是映射路径并没有定义数组形参");
                return clientPath.Substring(lastArrayDelimit + 2);
            }
        }

        /// <summary>
        /// 使用完整的JSON Path从JObject中读取字符串
        /// </summary>
        /// <returns></returns>
        public static string ReadJsonValueString(JObject requestJson, string fullPath, out bool notFound)
        {
            string jsonPath = (fullPath.StartsWith('[') ? "$" : "$.") + fullPath.Trim();
            JToken token = requestJson.SelectToken(jsonPath);
            if (token == null)
            {
                notFound = true;
                return null;
            }
            else
            {
                notFound = false;
                return token.ToString();
            }
        }

        public static string ReadConstantValueString(string raw)
        {
            return raw.Trim().TrimStart(new char[] { '\'', '"' }).TrimEnd(new char[] { '\'', '"' });
        }

        /// <summary>
        /// 从JObject中读取需要的字符串，适用于Route，Query和Form Body，但不适用于JSON模板
        /// </summary>
        /// <param name="requestJson"></param>
        /// <param name="mapping"></param>
        /// <returns></returns>
        public static string ReadMappingValueString(JObject requestJson, ParameterMapping mapping)
        {
            string valueString;
            if (mapping.MappingType == MappingType.Constant)
            {
                valueString = ReadConstantValueString(mapping.ClientPath);
            }
            else if (mapping.MappingType == MappingType.Path)
            {
                valueString = ReadJsonValueString(requestJson, mapping.ClientPath, out var notFound);
            }
            else
            {
                throw new Exception("不支持的参数映射形式");
            }
            return System.Web.HttpUtility.UrlEncode(valueString);
        }

        public static string GetEnumDisplayName(Type enumType, Enum value)
        {
            MemberInfo[] memInfo = enumType.GetMember(value.ToString());
            if (memInfo != null && memInfo.Length > 0)
            {
                object[] attrs = memInfo[0].GetCustomAttributes(typeof(DisplayAttribute), false);
                if (attrs != null && attrs.Length > 0)
                    return ((DisplayAttribute)attrs[0]).Name;
            }
            return enumType.ToString();
        }

        public static string GetEnumDisplayName(Type enumType, string name)
        {
            MemberInfo[] memInfo = enumType.GetMember(name);
            if (memInfo != null && memInfo.Length > 0)
            {
                object[] attrs = memInfo[0].GetCustomAttributes(typeof(DisplayAttribute), false);
                if (attrs != null && attrs.Length > 0)
                    return ((DisplayAttribute)attrs[0]).Name;
            }
            return enumType.ToString();
        }

        public static void MapQueryFormParametersInto(List<ParameterMapping> mappings, string soure, string pattern)
        {
            pattern = pattern.Trim();
            if (pattern.StartsWith('?'))
            {
                pattern = pattern.Substring(1);
            }

            string[] parts = pattern.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var s in parts)
            {
                string parameter = s.Trim();
                if (parameter.EndsWith('='))
                {
                    parameter = parameter.Substring(0, parameter.Length - 1);
                }
                mappings.Add(new ParameterMapping
                {
                    Source = soure,
                    ServerPath = parameter,
                    MappingType = MappingType.Path,
                    ClientPath = parameter
                });
            }
        }

        /// <summary>
        /// 生成length长度的大小写字符和数字组成的随机字符串
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string GetRandomString(int length)
        {
            var characters = new List<char>();
            for (int i = 48; i <= 57; i++)
            {
                // 0-9
                characters.Add((char)i);
            }
            for (int i = 65; i <= 90; i++)
            {
                // A-Z
                characters.Add((char)i);
            }
            for (int i = 97; i <= 122; i++)
            {
                //a-z
                characters.Add((char)i);
            }

            return GetRandomString(characters.ToArray(), length);
        }

        /// <summary>
        /// 从options字符集合中随机生成length长度的字符串
        /// </summary>
        /// <param name="options"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string GetRandomString(char[] options, int length)
        {
            Random random = new Random();
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                int index = random.Next(0, options.Length - 1);
                builder.Append(options[index]);
            }

            return builder.ToString();
        }
    }
}