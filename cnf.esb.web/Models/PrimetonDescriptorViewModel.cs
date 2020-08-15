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
        public PrimetonDescriptorViewModel()
        {
            InputBody = new JsonTemplate();
            InputBody.ValueType = ValueType.Object;
            InputBody.IsArray = false;

            ReturnBody = new JsonTemplate();
            ReturnBody.ValueType = ValueType.Object;
            ReturnBody.IsArray = false;
            ReturnBody.ObjectProperties.Add("flag", JsonTemplate.Create(ValueType.String, false));
            ReturnBody.ObjectProperties.Add("msg", JsonTemplate.Create(ValueType.String, false));
        }

        [JsonIgnore]
        public ServiceType ServiceType { get { return ServiceType.PrimetonService; } }

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

        [Required, Display(Name = "空间前缀")]
        public string Prefix { get; set; }

        [Required, Display(Name = "命名空间")]
        public string Namespace { get; set; }

        /// <summary>
        /// 普元 web service要求的传入json对象参数模板定义
        /// </summary>
        /// <value></value>
        public JsonTemplate InputBody { get; set; }

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
                    || string.IsNullOrWhiteSpace(Operation)
                    || string.IsNullOrWhiteSpace(Prefix)
                    || string.IsNullOrWhiteSpace(Namespace))
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
            this.Prefix = viewModel.Prefix;
            this.Namespace = viewModel.Namespace;
        }

        /// <summary>
        /// 从一个EsbService定义中创建一个视图模型并返回。
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public static PrimetonDescriptorViewModel CreateFrom(EsbService service)
        {
            if (service.Type != ServiceType.PrimetonService)
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
            if (service.Type != ServiceType.PrimetonService)
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

        internal string generatePostXml(JObject requestBody)
        {
            JToken soapBodyJson = requestBody.SelectToken("$.body");

            XNamespace ns0 = Namespace;
            XNamespace soapenv = "http://schemas.xmlsoap.org/soap/envelope/";

            XElement body = new XElement(soapenv + "Body");

            if (soapBodyJson != null && soapBodyJson.Type == JTokenType.Object)
            {
                JObject parameters = (JObject)soapBodyJson;
                foreach (var inputItem in parameters)
                {
                    /*/ {
                            "insertProject": {
                                "jsons": [{
                                        "cmpCode": "111",
                                        "cmpName": "Some"
                                    },{
                                        "cmpCode": "112",
                                        "cmpName": "Time"
                                    }
                                ]
                            }
                        }
                    */
                    //lev 1: insertProject (Input Name == parameter name)
                    XElement inputNode = new XElement(ns0 + inputItem.Key);
                    if (inputItem.Value != null && inputItem.Value.Type == JTokenType.Object)
                    {
                        var inputObject = (JObject)inputItem.Value;
                        foreach (var inputType in inputObject)
                        {
                            //lev 2: jsons (parameter type, 只能有一个属性就是好类型名称)
                            //如果后面是数组，就生成一系列 <ns0:jsons>
                            foreach (var typeObject in inputType.Value)
                            {
                                XElement typeNode = new XElement(ns0 + inputType.Key);
                                if (typeObject != null && typeObject.Type == JTokenType.Object)
                                {
                                    foreach (var prop in (JObject)typeObject)
                                    {
                                        typeNode.Add(new XElement(prop.Key, prop.Value?.ToString()));
                                    }
                                }
                                else
                                {
                                    typeNode.Value = typeObject?.ToString();
                                }
                                inputNode.Add(typeNode);
                            }
                        }
                    }
                    body.Add(inputNode);
                }
            }
            else
            {
                body.Value = soapBodyJson?.ToString();
            }

            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(soapenv + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", soapenv.NamespaceName),
                new XAttribute(XNamespace.Xmlns + Prefix, ns0.NamespaceName),
                new XElement(soapenv + "Header"),
                body));

            return document.Declaration + Environment.NewLine + document.ToString();
        }

        public async System.Threading.Tasks.Task<RawResponse> GetResponse(JObject source)
        {
            string fullUrl = ServiceAddress.TrimEnd(new char[] { '/', ' ' });
            StringBuilder bodyBuilder = new StringBuilder();
            //直接将客户发送来的body中的json作为SOAP Body传递，
            // 不验证它与服务api定义的符合性。

            string postXml = generatePostXml(source);

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
            if (!response.IsSuccessful)
            {
                if (response.ErrorException != null)
                {
                    throw response.ErrorException;
                }
                else if (!string.IsNullOrWhiteSpace(response.ErrorMessage))
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
            //XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
            XNamespace ns1 = Namespace; //"http://www.primeton.com/ProjectInfoManService";
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

            #region comments a sample response
            //              string data = @"
            // <soapenv:Envelope
            //     xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"">
            //     <soapenv:Body>
            //         <_tns_:saveContractServiceResponse
            //             xmlns:_tns_=""http://www.primeton.com/ProjectInfoManService"">
            //             <ns1:flag xsi:nil=""true""
            //                 xmlns:ns1=""http://www.primeton.com/ProjectInfoManService""
            //                 xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""/>
            //             <ns1:msg xsi:nil=""error message""
            //                 xmlns:ns1=""http://www.primeton.com/ProjectInfoManService""
            //                 xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""/>
            //         </_tns_:saveContractServiceResponse>
            //     </soapenv:Body>
            // </soapenv:Envelope>
            //             ";
            #endregion

            var envelope = XElement.Parse(rawResponse);
            var flag = envelope.Descendants(ns1 + "flag")
                    .FirstOrDefault().Attribute(xsi + "nil").Value;
            var msg = envelope.Descendants(ns1 + "msg")
                    .FirstOrDefault().Attribute(xsi + "nil").Value;

            StringBuilder returnJsonBuilder = new StringBuilder();
            using (JsonWriter jwriter = new JsonTextWriter(new StringWriter(returnJsonBuilder)))
            {
                jwriter.Formatting = Formatting.Indented;
                jwriter.WriteStartObject();
                jwriter.WritePropertyName("flag");
                jwriter.WriteValue(flag);
                jwriter.WritePropertyName("msg");
                jwriter.WriteValue(msg);
                jwriter.WriteEndObject();
                jwriter.Close();
            }

            type = SimpleRESTfulReturn.Json;
            apiResponse = returnJsonBuilder.ToString();
            return true;
        }
    }
}