using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace cnf.esb.web.Models
{
    public class NCDescriptorViewModel : IServiceDescriptorViewModel
    {
        [JsonIgnore]
        public ServiceType ServiceType { get { return ServiceType.NCWebService; } }

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
        [Display(Name = "服务名称"), Required]
        public string ServiceName { get; set; }

        /// <summary>
        /// 直接来自Service数据库，API是否可用
        /// </summary>
        /// <value></value>
        [Display(Name = "已启用")]
        public bool ActiveStatus { get; set; }
        #endregion

        #region NC web service特有的信息段

        [Required, Display(Name = "Web服务基地址")]
        public string WebServiceUrl { get; set; }

        [Required, RegularExpression("^[a-zA-Z_][a-zA-Z0-9_]*$"), Display(Name = "API端点")]
        public string EndPoint { get; set; }

        /// <summary>
        /// NC web service要求的传入json对象参数模板定义
        /// </summary>
        /// <value></value>
        public JsonTemplate ParameterBody { get; set; }

        /// <summary>
        /// NC web service返回的json对象的模板定义
        /// </summary>
        /// <value></value>
        public JsonTemplate ReturnBody { get; set; }

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
                if (string.IsNullOrWhiteSpace(WebServiceUrl)
                    || string.IsNullOrWhiteSpace(EndPoint)
                    || ParameterBody == null
                    || ReturnBody == null)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// 假定uiViewModel参数是来自用户视图的数据控件绑定模型，
        /// 而当前实例是一个从数据库或视图状态中恢复出来的老模型，
        /// 那么，应当运行本方法完成用户可能发生的输入更改。
        /// 注意：本方法仅更新那些绑定到了视图上的form control的值。
        /// </summary>
        /// <param name="uiViewModel"></param>
        public void UpdateFromUI(IServiceDescriptorViewModel uiViewModel)
        {
            NCDescriptorViewModel viewModel = (NCDescriptorViewModel)uiViewModel;
            this.ActiveStatus = viewModel.ActiveStatus;
            this.EndPoint = viewModel.EndPoint;
            this.ServiceName = viewModel.ServiceName;
            this.WebServiceUrl = viewModel.WebServiceUrl;
        }

        /// <summary>
        /// 从一个EsbService定义中创建一个视图模型并返回。
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public static NCDescriptorViewModel CreateFrom(EsbService service)
        {
            if (service.Type != ServiceType.NCWebService)
                throw new Exception("服务不是用友NC系统Web服务类型的。");

            var model = JsonConvert.DeserializeObject<NCDescriptorViewModel>(service.ServiceDescriptor);
            if (model == null)
            {
                model = new NCDescriptorViewModel();
            }
            model.ServiceID = service.ID;
            model.ServiceName = service.Name;
            model.ActiveStatus = service.ActiveStatus == 1;

            return model;
        }

        /// <summary>
        /// 将当前实例作为Descriptor保存到Service中。
        /// </summary>
        /// <param name="service"></param>
        public bool UpdateToService(ref EsbService service, out string error)
        {
            if (service.Type != ServiceType.NCWebService)
                throw new Exception("服务不是用友NC系统Web服务类型的。");

            service.Name = this.ServiceName;
            service.ActiveStatus = this.ActiveStatus ? 1 : 0;
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
                if (ParameterBody != null)
                {
                    ParameterBody.WriteJsonBody(writer);
                }
                else
                {
                    writer.WriteNull();
                }
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
                if (ReturnBody == null)
                    jwriter.WriteNull();
                else
                    ReturnBody.WriteJsonBody(jwriter);
                jwriter.Close();
            }
            return returnJsonBuilder.ToString();
        }

        public async System.Threading.Tasks.Task<RawResponse> GetResponse(JObject source)
        {
            string fullUrl = WebServiceUrl.TrimEnd(new char[] { '/', ' ' });
            StringBuilder bodyBuilder = new StringBuilder();
            JToken requestJson = source.SelectToken("$.body");
            //直接将客户发送来的body中的json作为web service的CDATA传递，
            // 不验证它与服务api定义的符合性。
            string data = requestJson?.ToString();
            
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
            XNamespace test = "http://www.w3.org/2001/XMLSchema";
            XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
            
            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(soap + "Envelope",
                    new XAttribute(XNamespace.Xmlns + "xsi", xsi.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "test", test.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "soap", soap.NamespaceName),
                    new XElement(soap + "Body",
                        new XElement(test + EndPoint, //"syncPsndoc",
                            new XElement("string",
                                new XCData(data)
                            )
                        )
                    )
                )
            );
            
            string postXml = document.Declaration + Environment.NewLine + document.ToString();

            var client = new RestClient(fullUrl);
            var request = new RestRequest(Method.POST);
            if (fullUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                client.RemoteCertificateValidationCallback = (s, c, e, t) => true;
            }
            request.AddHeader("Content-Type", "application/xml");
            request.AddParameter("application/xml", postXml, ParameterType.RequestBody);
            IRestResponse response = await client.ExecuteAsync(request);
            if(!response.IsSuccessful)
            {
                if(response.ErrorException != null)
                {
                    throw response.ErrorException;
                }
                else
                {
                    throw new Exception(response.ErrorMessage);
                }
            }
            return new RawResponse(response, fullUrl);
        }

        public bool CheckResponse(string rawResponse, out string apiResponse, out SimpleRESTfulReturn type)
        {
            XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
            using(var reader = new StringReader(rawResponse))
            {
                XDocument document = XDocument.Load(reader);
                var faultNode = (from e in document.Descendants(soap + "Fault")
                                select e).SingleOrDefault();
                if(faultNode == null)
                {
                    //no fault,
                    var returnNode = (from e in document.Descendants("return")
                                        select e).SingleOrDefault();
                    if(returnNode != null)
                    {
                        type = SimpleRESTfulReturn.Json;
                        apiResponse = returnNode.Value;
                        return true;
                    }
                    else
                    {
                        type = SimpleRESTfulReturn.PlainText;
                        apiResponse = rawResponse;
                        return false;
                    }
                }
                else
                {
                    type = SimpleRESTfulReturn.PlainText;
                    apiResponse = $"FaultCode={faultNode.Element("faultcode").Value},"
                            + $"FaultString={faultNode.Element("faultstring").Value}";
                    return false;
                }
            }
        }
    }
}