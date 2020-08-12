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
    public class PrimetonDescriptorViewModel : IServiceDescriptorViewModel
    {
        [JsonIgnore]
        public ServiceType ServiceType { get { return ServiceType.PrimetonWebService; } }

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

        #region Primeton web service特有的信息段

        [Required, Display(Name = "服务地址")]
        public string ServiceAddress { get; set; }

        [Required, RegularExpression("^[a-zA-Z_][a-zA-Z0-9_]*$"), Display(Name = "操作名称")]
        public string Operation { get; set; }

        /// <summary>
        /// Literal Input Name
        /// </summary>
        /// <value></value>
        [Required, Display(Name="输入参数")]
        public string InputName { get; set; }

        /// <summary>
        /// 普元 web service要求的传入json对象参数模板定义
        /// </summary>
        /// <value></value>
        public JsonTemplate InputBody { get; set; }

        /// <summary>
        /// Literal Output Name, 并非必要
        /// </summary>
        /// <value></value>
        [Display(Name="输出参数")]
        public string OutputName { get; set; }

        /// <summary>
        /// 普元 web service返回的json对象的模板定义
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
                if (string.IsNullOrWhiteSpace(ServiceAddress)
                    || string.IsNullOrWhiteSpace(Operation))
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
            PrimetonDescriptorViewModel viewModel = (PrimetonDescriptorViewModel)uiViewModel;
            this.ActiveStatus = viewModel.ActiveStatus;
            this.Operation = viewModel.Operation;
            this.ServiceName = viewModel.ServiceName;
            this.ServiceAddress = viewModel.ServiceAddress;
            this.InputName = viewModel.InputName;
            this.OutputName = viewModel.OutputName;
        }

        /// <summary>
        /// 从一个EsbService定义中创建一个视图模型并返回。
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public static PrimetonDescriptorViewModel CreateFrom(EsbService service)
        {
            if (service.Type != ServiceType.PrimetonWebService)
                throw new Exception("服务不是股份普元服务类型的。");

            var model = JsonConvert.DeserializeObject<PrimetonDescriptorViewModel>(service.ServiceDescriptor);
            if (model == null)
            {
                model = new PrimetonDescriptorViewModel();
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
            if (service.Type != ServiceType.PrimetonWebService)
                throw new Exception("服务不是股份普元服务类型的。");

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
                if (InputBody != null)
                {
                    InputBody.WriteJsonBody(writer);
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
            string fullUrl = ServiceAddress.TrimEnd(new char[] { '/', ' ' });
            StringBuilder bodyBuilder = new StringBuilder();
            JToken requestJson = source.SelectToken("$.body");
            //直接将客户发送来的body中的json作为SOAP Body传递，
            // 不验证它与服务api定义的符合性。
            //TODO: string data = requestJson?.ToString();
                        
            XNamespace proj = "http://www.primeton.com/ProjectInfoManService";
            XNamespace soapenv = "http://schemas.xmlsoap.org/soap/envelope/";

            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(soapenv + "Envelope",
                    new XAttribute(XNamespace.Xmlns + "soapenv", soapenv.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "proj", proj.NamespaceName),
                    new XElement(soapenv + "Header"),
                    new XElement(soapenv + "Body",
                        new XElement(proj + InputName,
                            new XElement(proj + "jsons", ""),
                            new XElement(proj + "jsons", ""),
                            new XElement(proj + "jsons", "")
                        )
                    )
                )
            );
            
            string postXml = document.Declaration + Environment.NewLine + document.ToString();

            var client = new RestClient(fullUrl);
            var request = new RestRequest(Method.POST);
            request.Timeout = 1800000;   //1800秒=30分钟
            request.ReadWriteTimeout = 1800000; //1800秒=30分钟
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
                else if(!string.IsNullOrWhiteSpace(response.ErrorMessage))
                {
                    throw new Exception(response.ErrorMessage);
                }
                else
                {
                    StringBuilder errorBuilder = new StringBuilder();
                    errorBuilder.AppendLine($"向 {fullUrl} 发送数据出现错误。");
                    errorBuilder.AppendLine($"StatusCode={response.StatusCode}");
                    errorBuilder.AppendLine($"Response:{response.Content}");
                    errorBuilder.AppendLine("Xml Parameter Posted Is:");
                    errorBuilder.AppendLine(postXml);
                    throw new Exception(errorBuilder.ToString());
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