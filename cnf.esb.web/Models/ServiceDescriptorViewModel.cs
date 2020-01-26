using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace cnf.esb.web.Models
{
    public enum SimpleRESTfulReturn
    {
        [Display(Name = "不返回任何值")]
        Empty,
        [Display(Name = "返回普通文本或字符串")]
        PlainText,
        [Display(Name = "返回JSON格式序列化的字符串")]
        Json
    }

    public enum SimpleRESTfulSuccess
    {
        [Display(Name = "只要未发生异常就说明成功")]
        NoException,
        [Display(Name = "返回值能匹配正则表达式表明成功")]
        MatchRegEx,
        [Display(Name = "使用JPath查找并能与正则表达式匹配表明成功")]
        JsonPath
    }

    /// <summary>
    /// 用来编辑Simple RESTful类型服务协定的类型
    /// </summary>
    public class SimpleRestfulDescriptorViewModel : IServiceDescriptorViewModel
    {
        public SimpleRestfulDescriptorViewModel()
        {
            Method = HttpMethod.POST;
            Headers = new Dictionary<string, string>();
        }

        [JsonIgnore]
        public ServiceType ServiceType { get { return ServiceType.SimpleRESTful; } }

        /// <summary>
        /// 假定uiViewModel参数是来自用户视图的数据已绑定模型，
        /// 而当前实例是一个从数据库或视图中恢复出来的老模型，
        /// 那么，应当运行本方法完成用户可能发生的输入更改。
        /// 注意：本方法仅更新那些绑定到了视图上的form control的值。
        /// </summary>
        /// <param name="uiViewModel"></param>
        public void UpdateFromUI(IServiceDescriptorViewModel uiViewModel)
        {
            SimpleRestfulDescriptorViewModel viewModel = (SimpleRestfulDescriptorViewModel)uiViewModel;
            this.BaseApiUrl = viewModel.BaseApiUrl;
            this.FormBodyTemplate = viewModel.FormBodyTemplate;
            this.Method = viewModel.Method;
            this.QueryStringTemplate = viewModel.QueryStringTemplate;
            this.RouteDataTemplate = viewModel.RouteDataTemplate;
            this.SelectedTab = viewModel.SelectedTab;
            this.ReturnType = viewModel.ReturnType;
            this.SuccessRule = viewModel.SuccessRule;
            this.SuccessPath = viewModel.SuccessPath;
            this.SuccessRegx = viewModel.SuccessRegx;
            this.IgnoreCase = viewModel.IgnoreCase;
        }

        #region 服务基本信息（用于在视图上显示信息）
        /// <summary>
        /// 直接来自Service数据库，服务的ID
        /// </summary>
        /// <value></value>
        public int ServiceID { get; set; }

        /// <summary>
        /// 直接来自Service数据库，服务的名称
        /// </summary>
        /// <value></value>
        [Display(Name = "服务名称")]
        public string ServiceName { get; set; }

        /// <summary>
        /// 直接来自Service数据库，API是否可用
        /// </summary>
        /// <value></value>
        public bool ActiveStatus { get; set; }
        #endregion

        #region HTTP请求路径和头部定义
        /// <summary>
        /// 来自服务协定JSON串，HTTP请求方法
        /// </summary>
        /// <value></value>
        [Display(Name = "调用方法")]
        public HttpMethod Method { get; set; }

        /// <summary>
        /// 来自服务协定JSON串，不包括路由（route data）和查询字符串（query string）的URL
        /// </summary>
        /// <value></value>
        [Url, Display(Name = "API地址")]
        public string BaseApiUrl { get; set; }

        /// <summary>
        /// 来自服务协定JSON串，HTTP请求头
        /// </summary>
        /// <value></value>
        public Dictionary<string, string> Headers { get; set; }
        #endregion

        public string NewHeaderKey { get; set; }
        public string NewHeaderValue { get; set; }

        /// <summary>
        /// 页面上当前显示的Tab标签页ID
        /// </summary>
        /// <value></value>
        public string SelectedTab { get; set; }

        #region 输入参数
        /// <summary>
        /// 来自服务协定JSON串，来自路由的请求参数模板定义，如：param1/param2
        /// </summary>
        /// <value></value>
        [Display(Name = "路由参数模板")]
        [RegularExpression("^\\w+[\\w/]+\\w+$")]
        public string RouteDataTemplate { get; set; }

        /// <summary>
        /// 来自服务协定JSON串，来自查询字符串的请求参数模板定义，如：param1=&amp;param2=&amp;param3=
        /// </summary>
        [Display(Name = "查询字符串参数模板")]
        public string QueryStringTemplate { get; set; }

        /// <summary>
        /// 来自服务协定JSON串，来自body中表单数据的请求参数模板定义，如：param1=&amp;param2=&amp;param3=
        /// </summary>
        [Display(Name = "表单参数模板")]
        public string FormBodyTemplate { get; set; }

        /// <summary>
        /// 来自服务协定JSON串，来自body中的JSON数据请求参数模板定义，和表单数据是互斥的。
        /// 如果是null表示没有，如果非null，则表单数据就失效了。
        /// </summary>
        /// <value></value>
        [Display(Name = "JSON参数模板")]
        public JsonTemplate JsonBodyTemplate { get; set; }
        #endregion

        /// <summary>
        /// 是否已经有效定义了服务协定
        /// </summary>
        /// <value></value>
        [JsonIgnore]
        public bool IsDefined
        {
            get
            {
                if (string.IsNullOrWhiteSpace(BaseApiUrl)
                    && JsonBodyTemplate == null
                    && string.IsNullOrWhiteSpace(FormBodyTemplate)
                    && string.IsNullOrWhiteSpace(QueryStringTemplate)
                    && string.IsNullOrWhiteSpace(RouteDataTemplate))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        #region 定义返回值
        [Display(Name = "返回类型")]
        public SimpleRESTfulReturn ReturnType { get; set; }

        [Display(Name = "成功判定")]
        public SimpleRESTfulSuccess SuccessRule { get; set; }

        [Display(Name = "返回状态JPath")]
        public string SuccessPath { get; set; }

        [Display(Name = "表示成功的正则表达式")]
        public string SuccessRegx { get; set; }

        [Display(Name = "忽略大小写")]
        public bool IgnoreCase { get; set; }

        [Display(Name = "返回值JSON模板")]
        public JsonTemplate ReturnJsonTemplate { get; set; }
        #endregion

        /// <summary>
        /// 从一个EsbService定义中创建一个视图模型并返回。
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public static SimpleRestfulDescriptorViewModel CreateFrom(EsbService service)
        {
            if (service.Type != ServiceType.SimpleRESTful)
                throw new Exception("服务不是Simple RESTful类型的。");

            var model = JsonConvert.DeserializeObject<SimpleRestfulDescriptorViewModel>(service.ServiceDescriptor);
            if (model == null)
            {
                model = new SimpleRestfulDescriptorViewModel();
            }
            model.ServiceID = service.ID;
            model.ServiceName = service.Name;
            model.ActiveStatus = service.ActiveStatus == 1;
            if (model.Headers == null) model.Headers = new Dictionary<string, string>();

            return model;
        }

        /// <summary>
        /// 将当前实例作为Descriptor保存到Service中。
        /// </summary>
        /// <param name="service"></param>
        public bool UpdateToService(ref EsbService service, out string error)
        {
            if (service.Type != ServiceType.SimpleRESTful)
                throw new Exception("要更新的服务不是Simple RESTful类型的。");

            if (ReturnJsonTemplate != null)
            {
                ReturnType = SimpleRESTfulReturn.Json;
            }
            if (SuccessRule == SimpleRESTfulSuccess.JsonPath
                && ReturnJsonTemplate == null)
            {
                error = "定义了JPath验证，但是没有定义返回的JSON模板。";
                return false;
            }
            if (ReturnType == SimpleRESTfulReturn.Json
                && ReturnJsonTemplate == null)
            {
                error = "定义了JSON类型返回值，但是没有定义返回JSON模板";
                return false;
            }
            service.ServiceDescriptor = JsonConvert.SerializeObject(this);
            error = "";
            return true;
        }

        public string GetPostSample()
        {
            StringBuilder bodyBuilder = new StringBuilder();
            using (JsonWriter writer = new JsonTextWriter(new StringWriter(bodyBuilder)))
            {
                writer.Formatting = Formatting.Indented;
                writer.WriteStartObject();
                if (!string.IsNullOrWhiteSpace(RouteDataTemplate))
                {
                    StringHelper.WriteRouteQueryFormJson(writer, EsbPostBodySections.ROUTE, RouteDataTemplate.Trim(), '/', "路由值");
                }
                if (!string.IsNullOrWhiteSpace(QueryStringTemplate))
                {
                    StringHelper.WriteRouteQueryFormJson(writer, EsbPostBodySections.QUERY, QueryStringTemplate.Trim().TrimStart('?'), '&', "查询字符串值");
                }
                if (JsonBodyTemplate != null)
                {
                    //向api提交请求的RequestBody中JSON优先
                    writer.WritePropertyName(EsbPostBodySections.JSON);
                    JsonBodyTemplate.WriteJsonBody(writer);
                }
                else if (!string.IsNullOrWhiteSpace(FormBodyTemplate))
                {
                    //如果没有JSON但是有FORM
                    StringHelper.WriteRouteQueryFormJson(writer, EsbPostBodySections.FORM, FormBodyTemplate.Trim().TrimStart('?'), '&', "表单数据值");
                }
                writer.WriteEndObject();
                writer.Flush();
            }
            return bodyBuilder.ToString();
        }

        public string GetReturnSample()
        {
            StringBuilder returnJsonBuilder = new StringBuilder();
            using (JsonWriter jwriter = new JsonTextWriter(new StringWriter(returnJsonBuilder)))
            {
                jwriter.Formatting = Formatting.Indented;
                if (ReturnJsonTemplate == null)
                    jwriter.WriteNull();
                else
                    ReturnJsonTemplate.WriteJsonBody(jwriter);
                jwriter.Close();
            }
            return returnJsonBuilder.ToString();
        }

        public WebRequest GetWebRequest(JObject source)
        {
            string fullUrl = BaseApiUrl.TrimEnd(new char[] { '/', ' ' });
            StringBuilder bodyBuilder = new StringBuilder();
            JToken requestJson = source.SelectToken("$.body");
            if (requestJson != null)
            {
                #region 准备调用所需要的各类输入参数
                if (!string.IsNullOrWhiteSpace(RouteDataTemplate))
                {
                    string[] routeParameters = RouteDataTemplate.Trim().Split(
                        '/', StringSplitOptions.RemoveEmptyEntries);
                    foreach (string route in routeParameters)
                    {
                        fullUrl = fullUrl + "/" + System.Web.HttpUtility.UrlEncode(
                            requestJson.SelectToken("$." + EsbPostBodySections.ROUTE +
                            "." + route).Value<string>());
                    }
                }
                if (!string.IsNullOrWhiteSpace(QueryStringTemplate))
                {
                    string[] queryParameters = QueryStringTemplate.Trim()
                        .TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
                    fullUrl = fullUrl + "?";
                    for (int i = 0; i < queryParameters.Length; i++)
                    {
                        string query = queryParameters[i].Trim().TrimEnd('=');
                        fullUrl = fullUrl + query + "=" + System.Web.HttpUtility.UrlEncode(
                            requestJson.SelectToken("$." + EsbPostBodySections.QUERY +
                            "." + query).Value<string>());
                    }
                    fullUrl.TrimEnd('&');
                }
                if (JsonBodyTemplate != null)
                {
                    //向api提交请求的RequestBody中JSON优先
                    using (JsonWriter writer = new JsonTextWriter(new StringWriter(bodyBuilder)))
                    {
                        writer.Formatting = Formatting.Indented;
                        using (JTokenReader jtReader = new JTokenReader(requestJson.SelectToken("$." + EsbPostBodySections.JSON)))
                        {
                            writer.WriteToken(jtReader);
                        }
                        writer.Close();
                    }
                }
                else if (!string.IsNullOrWhiteSpace(FormBodyTemplate))
                {
                    //如果没有JSON但是有FORM
                    string[] formParameters = FormBodyTemplate.Trim()
                        .TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < formParameters.Length; i++)
                    {
                        string parameter = formParameters[i].Trim().TrimEnd('=');
                        bodyBuilder.Append(parameter);
                        bodyBuilder.Append('=');
                        bodyBuilder.Append(System.Web.HttpUtility.UrlEncode(
                            requestJson.SelectToken("$." + EsbPostBodySections.FORM +
                            "." + parameter).Value<string>()));
                        bodyBuilder.Append('&');
                    }
                    if (bodyBuilder.Length > 0)
                    {
                        bodyBuilder.Remove(bodyBuilder.Length - 1, 1);
                    }
                }
                #endregion
            }

            #region 准备请求
            WebRequest apiRequest;
            if (fullUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                apiRequest = HttpWebRequest.CreateHttp(fullUrl);
                ((HttpWebRequest)apiRequest).ServerCertificateValidationCallback
                    = (s, c, e, t) => true;
            }
            else
            {
                apiRequest = WebRequest.Create(fullUrl);
            }
            apiRequest.Method = Method.ToString();
            foreach (var header in Headers)
            {
                apiRequest.Headers.Add(header.Key, header.Value);
            }
            byte[] buffer = Encoding.UTF8.GetBytes(bodyBuilder.ToString());
            apiRequest.ContentLength = buffer.Length;
            using (Stream apiStream = apiRequest.GetRequestStream())
            {
                apiStream.Write(buffer, 0, buffer.Length);
                apiStream.Close();
            }
            #endregion

            return apiRequest;
        }

        public bool CheckResponse(string rawResponse, out string apiResponse)
        {
            apiResponse = rawResponse;
            
            #region 对响应进行正确性检查
            if (SuccessRule == Models.SimpleRESTfulSuccess.NoException)
            {
                return true;
            }//肯定不是NoException规则了。
            string checkSegment;
            if (SuccessRule == Models.SimpleRESTfulSuccess.JsonPath)
            {
                JObject returnJson = JObject.Parse(rawResponse);
                checkSegment = returnJson.SelectToken(SuccessPath).ToString();
            }
            else
            {
                checkSegment = rawResponse;
            }
            if (string.IsNullOrWhiteSpace(SuccessRegx?.Trim()))
            {
                //没有正则表达式，那么表示可以任意匹配
                return true;
            }
            RegexOptions regexOptions = IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
            if (Regex.IsMatch(checkSegment, SuccessRegx, regexOptions))
            {
                return true;
            }
            else
            {
                return false;
            }
            #endregion
        }
    }
}