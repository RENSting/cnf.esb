using System;
using System.Net;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.IO;
using System.Text;

namespace cnf.esb.web.Models
{
    /// <summary>
    /// 客户程序调用ESB服务时，发送的body中json成员名称
    /// </summary>
    public static class EsbPostBodySections
    {
        public const string ROUTE = "route";
        public const string QUERY = "query";
        public const string FORM = "form";
        public const string JSON = "json";
    }

    public enum ResponseType
    {
        RestSharpResponse,
        WebResponse
    }

    public class RawResponse
    {
        private string _requestUrl;
        private object _response;

        private ResponseType _type;

        public RawResponse(IRestResponse response, string requestUrl)
        {
            _requestUrl = requestUrl;
            _response = response;
            _type = ResponseType.RestSharpResponse;
        }

        public RawResponse(WebResponse response, string requestUrl)
        {
            _requestUrl = requestUrl;
            _response = response;
            _type = ResponseType.WebResponse;
        }

        public string RequestUrl => _requestUrl;

        public async System.Threading.Tasks.Task<string> ReadContentAsync()
        {
            if (_type == ResponseType.WebResponse)
            {
                using (StreamReader apiResponseReader =
                    new StreamReader(((WebResponse)_response).GetResponseStream(), Encoding.UTF8))
                {
                    return await apiResponseReader.ReadToEndAsync();
                }
            }
            else if (_type == ResponseType.RestSharpResponse)
            {
                return ((IRestResponse)_response).Content;
            }
            else
            {
                throw new Exception("not implemented yet");
            }
        }
    }
    public interface IServiceDescriptorViewModel
    {
        int ServiceID { get; set; }
        string ServiceName { get; set; }
        bool ActiveStatus { get; set; }
        ServiceType ServiceType { get; }
        bool IsDefined { get; }
        bool UpdateToService(ref EsbService service, out string error);
        void UpdateFromUI(IServiceDescriptorViewModel uiViewModel);
        /// <summary>
        /// get a json body sample of this type of service
        /// </summary>
        /// <returns></returns>
        string GetPostSample();
        string GetReturnSample();

        /// <summary>
        /// Invoke the service and get response
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        System.Threading.Tasks.Task<RawResponse> GetResponse(JObject source);

        bool CheckResponse(string rawResponse, out string apiResponse);
    }

    public enum ServiceType
    {
        [Display(Name = "简单RESTful")]
        SimpleRESTful,
        [Display(Name = "用友NC系统Web服务")]
        NCWebService,
    }

    [Table("EsbService")]
    public class EsbService
    {
        [Display(Name = "ID")]
        public int ID { get; set; }

        [Required, MaxLength(50)]
        [Display(Name = "服务名称")]
        public string Name { get; set; }

        [Display(Name = "分组")]
        public string GroupName { get; set; }

        [Display(Name = "服务说明")]
        public string FullDescription { get; set; }

        /// <summary>
        /// 接口类型决定了服务协定的格式
        /// </summary>
        /// <value></value>
        [Required, Display(Name = "接口类型")]
        public ServiceType Type { get; set; }

        /// <summary>
        /// JSON序列化的API接口协议，包括URI， 输入参数， 返回值等
        /// </summary>
        /// <value></value>
        [Display(Name = "服务协定")]
        public string ServiceDescriptor { get; set; }

        [Display(Name = "创建日期")]
        [DataType(DataType.Date)]
        public DateTime CreatedOn { get; set; }

        [Display(Name = "服务状态")]
        public int ActiveStatus { get; set; }

        public ICollection<EsbInstance> Instances { get; set; }
    }

    public class EditServiceViewModel
    {
        public int ServiceID { get; set; }

        [Required, MaxLength(50)]
        [Display(Name = "服务名称")]
        public string Name { get; set; }

        [Display(Name = "分组")]
        public string GroupName { get; set; }

        [Display(Name = "服务说明")]
        public string FullDescription { get; set; }

        /// <summary>
        /// 接口类型决定了服务协定的格式
        /// </summary>
        /// <value></value>
        [Required, Display(Name = "接口类型")]
        public ServiceType Type { get; set; }

        [Display(Name = "服务状态")]
        public bool ActiveStatus { get; set; }

        public static implicit operator EditServiceViewModel(EsbService service)
        {
            return new EditServiceViewModel
            {
                ActiveStatus = service.ActiveStatus != 0,
                FullDescription = service.FullDescription,
                GroupName = service.GroupName,
                Name = service.Name,
                ServiceID = service.ID,
                Type = service.Type
            };
        }
    }

    public enum HttpMethod
    {
        GET, POST
    }

    /// <summary>
    /// response out
    /// </summary>
    public class ResponseBody
    {
        public int ReturnCode { get; set; }
        public string ErrorMessage { get; set; }
        public string Response { get; set; }
    }
}