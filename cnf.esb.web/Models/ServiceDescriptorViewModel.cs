using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;
using Newtonsoft.Json;

namespace cnf.esb.web.Models
{
    public enum SimpleRESTfulReturn
    {
        [Display(Name="不返回任何值")]
        Empty,
        [Display(Name="返回普通文本或字符串")]
        PlainText,
        [Display(Name="返回JSON格式序列化的字符串")]
        Json
    }

    public enum SimpleRESTfulSuccess
    {
        [Display(Name="只要未发生异常就说明成功")]
        NoException,
        [Display(Name="返回值能匹配正则表达式表明成功")]
        MatchRegEx,
        [Display(Name="使用JPath查找并能与正则表达式匹配表明成功")]
        JsonPath
    }

    /// <summary>
    /// 用来编辑Simple RESTful类型服务协定的类型
    /// </summary>
    public class SimpleRestfulDescriptorViewModel
    {
        public SimpleRestfulDescriptorViewModel()
        {
            Method = HttpMethod.POST;
            Headers = new Dictionary<string, string>();
        }

        /// <summary>
        /// 假定uiViewModel参数是来自用户视图的数据已绑定模型，
        /// 而当前实例是一个从数据库或视图中恢复出来的老模型，
        /// 那么，应当运行本方法完成用户可能发生的输入更改。
        /// 注意：本方法仅更新那些绑定到了视图上的form control的值。
        /// </summary>
        /// <param name="uiViewModel"></param>
        public void UpdateFromUI(SimpleRestfulDescriptorViewModel uiViewModel)
        {
            this.BaseApiUrl = uiViewModel.BaseApiUrl;
            this.FormBodyTemplate = uiViewModel.FormBodyTemplate;
            this.Method = uiViewModel.Method;
            this.QueryStringTemplate = uiViewModel.QueryStringTemplate;
            this.RouteDataTemplate = uiViewModel.RouteDataTemplate;
            this.SelectedTab = uiViewModel.SelectedTab;
            this.ReturnType = uiViewModel.ReturnType;
            this.SuccessRule = uiViewModel.SuccessRule;
            this.SuccessPath = uiViewModel.SuccessPath;
            this.SuccessRegx = uiViewModel.SuccessRegx;
            this.IgnoreCase = uiViewModel.IgnoreCase;
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
        [Display(Name="返回类型")]
        public SimpleRESTfulReturn ReturnType{get;set;}
        
        [Display(Name="成功判定")]
        public SimpleRESTfulSuccess SuccessRule{get;set;}

        [Display(Name="返回状态JPath")]
        public string SuccessPath{get;set;}

        [Display(Name="表示成功的正则表达式")]
        public string SuccessRegx{get;set;}

        [Display(Name="忽略大小写")]
        public bool IgnoreCase{get;set;}

        [Display(Name="返回值JSON模板")]
        public JsonTemplate ReturnJsonTemplate{get;set;}
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

            if(ReturnJsonTemplate != null)
            {
                ReturnType = SimpleRESTfulReturn.Json;
            }
            if(SuccessRule == SimpleRESTfulSuccess.JsonPath
                && ReturnJsonTemplate == null)
            {
                error = "定义了JPath验证，但是没有定义返回的JSON模板。";
                return false;
            }
            if(ReturnType == SimpleRESTfulReturn.Json
                && ReturnJsonTemplate == null)
            {
                error = "定义了JSON类型返回值，但是没有定义返回JSON模板";
                return false;
            }
            service.ServiceDescriptor = JsonConvert.SerializeObject(this);
            error = "";
            return true;
        }
    }
}