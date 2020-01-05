using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using cnf.esb.web.Models;
using System.Linq.Expressions;
using Newtonsoft.Json.Linq;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;

namespace cnf.esb.web.Controllers
{
    [AllowAnonymous]
    public class ApiController : Controller
    {
        const string REQ_JSON_ROUTE = "route";
        const string REQ_JSON_QUERY = "query";
        const string REQ_JSON_FORM = "form";
        const string REQ_JSON_JSON = "json";

        // /// <summary>
        // /// parameter in
        // /// </summary>
        // class RequestBody
        // {
        //     public int Id { get; set; }
        //     public string Token { get; set; }
        //     public string Body { get; set; }
        // }

        /// <summary>
        /// response out
        /// </summary>
        public class ResponseBody
        {
            public int ReturnCode { get; set; }
            public string ErrorMessage { get; set; }
            public string Response { get; set; }
        }

        readonly EsbModelContext _esbModelContext;

        public ApiController(EsbModelContext modelContext)
        {
            _esbModelContext = modelContext;
        }

        async Task LogStart(string task, int instanceId)
        {
            var log = EsbLog.Create(task, EsbLogLevel.Message, EsbOperation.Incoming,
                HttpContext.Connection.RemoteIpAddress.ToString(), instanceId, string.Empty,
                string.Empty, (int)Request.ContentLength);
            _esbModelContext.Add(log);
            await _esbModelContext.SaveChangesAsync();
        }

        async Task LogPreparation(string task, int instanceId, string invokedUrl, string message)
        {
            var log = EsbLog.Create(task, EsbLogLevel.Message, EsbOperation.Preparation,
                HttpContext.Connection.RemoteIpAddress.ToString(), instanceId, message, invokedUrl);
            _esbModelContext.Add(log);
            await _esbModelContext.SaveChangesAsync();
        }

        async Task LogInvoking(string task, int instanceId, string invokedUrl, string message)
        {
            var log = EsbLog.Create(task, EsbLogLevel.Message, EsbOperation.Invoking,
                HttpContext.Connection.RemoteIpAddress.ToString(), instanceId, message, invokedUrl);
            _esbModelContext.Add(log);
            await _esbModelContext.SaveChangesAsync();
        }

        async Task LogApiError(string task, int instanceId, string invokedUrl, string message)
        {
            var log = EsbLog.Create(task, EsbLogLevel.Warning, EsbOperation.Respond,
                HttpContext.Connection.RemoteIpAddress.ToString(), instanceId, message, invokedUrl);
            _esbModelContext.Add(log);
            await _esbModelContext.SaveChangesAsync();
        }

        async Task LogEnd(string task, int instanceId, string invokedUrl, int outLength)
        {
            var log = EsbLog.Create(task, EsbLogLevel.Message, EsbOperation.Respond,
                HttpContext.Connection.RemoteIpAddress.ToString(), instanceId, string.Empty,
                invokedUrl, 0, outLength);
            _esbModelContext.Add(log);
            await _esbModelContext.SaveChangesAsync();
        }

        /// <summary>
        /// 自动记录错误日志， 记录退出返回日志。
        /// 因此，调用该方法应该直接Return，防止在调用后又执行代码。
        /// </summary>
        async Task<JsonResult> Error(int code, string message, string task, int instanceId, 
            EsbOperation period, string invokedUrl = "")
        {
            var log = EsbLog.Create(task, EsbLogLevel.Failure, period, 
                HttpContext.Connection.RemoteIpAddress.ToString(), instanceId, message, invokedUrl);
            _esbModelContext.Add(log);
            await _esbModelContext.SaveChangesAsync();
            ResponseBody response = new ResponseBody
            {
                ErrorMessage = message,
                Response = string.Empty,
                ReturnCode = code
            };
            await LogEnd(task, instanceId, invokedUrl, message.Length + 4);
            return Json(response);
        }

        [Route("api/Help/{id}")]
        public async Task<IActionResult> GetHelp(int id)
        {
            var instance = await _esbModelContext.Instances.Where(i => i.ID == id)
                    .Include(i => i.Client)
                    .Include(i => i.Service).SingleOrDefaultAsync();
            //.Include(i => i.InstanceMapping).SingleOrDefaultAsync();
            if (instance == null || instance.ActiveStatus == 0)
            {
                ViewBag.ErrorMessage = "指定的API调用实例不存在，或者已经被停用。";
                return View();
            }
            if (instance.Client == null || instance.Client.ActiveStatus == 0)
            {
                ViewBag.ErrorMessage = "客户程序没有注册，或者已经被禁用。";
                return View();
            }
            if (instance.Service == null || instance.Service.ActiveStatus == 0)
            {
                ViewBag.ErrorMessage = "请求的服务不存在，或者尚未启用";
                return View();
            }
            if (instance.Service.Type != ServiceType.SimpleRESTful)
            {
                ViewBag.ErrorMessage = "尚未实现的服务协定";
                return View();
            }
            ViewBag.Url = "/api/Invoke/" + id.ToString();
            var api = SimpleRestfulDescriptorViewModel.CreateFrom(instance.Service);
            StringBuilder bodyBuilder = new StringBuilder();
            using (JsonWriter writer = new JsonTextWriter(new StringWriter(bodyBuilder)))
            {
                writer.Formatting = Formatting.Indented;
                writer.WriteStartObject();
                if (!string.IsNullOrWhiteSpace(api.RouteDataTemplate))
                {
                    StringHelper.WriteRouteQueryFormJson(writer, REQ_JSON_ROUTE, api.RouteDataTemplate.Trim(), '/', "路由值");
                }
                if (!string.IsNullOrWhiteSpace(api.QueryStringTemplate))
                {
                    StringHelper.WriteRouteQueryFormJson(writer, REQ_JSON_QUERY, api.QueryStringTemplate.Trim().TrimStart('?'), '&', "查询字符串值");
                }
                if (api.JsonBodyTemplate != null)
                {
                    //向api提交请求的RequestBody中JSON优先
                    writer.WritePropertyName(REQ_JSON_JSON);
                    api.JsonBodyTemplate.WriteJsonBody(writer);
                }
                else if (!string.IsNullOrWhiteSpace(api.FormBodyTemplate))
                {
                    //如果没有JSON但是有FORM
                    StringHelper.WriteRouteQueryFormJson(writer, REQ_JSON_FORM, api.FormBodyTemplate.Trim().TrimStart('?'), '&', "表单数据值");
                }
                writer.WriteEndObject();
                writer.Flush();
            }
            ViewBag.BodyExample = bodyBuilder.ToString();
            ViewBag.ReturnType = StringHelper.GetEnumDisplayName(typeof(Models.SimpleRESTfulReturn), api.ReturnType);
            switch (api.ReturnType)
            {
                case Models.SimpleRESTfulReturn.Empty:
                    ViewBag.ReturnExample = "服务不返回任何值，或者返回值没有意义。";
                    break;
                case Models.SimpleRESTfulReturn.PlainText:
                    ViewBag.ReturnExample = "服务返回无固定格式文本，您需要具体联系服务提供方获得说明。";
                    break;
                case Models.SimpleRESTfulReturn.Json:
                    StringBuilder returnJsonBuilder = new StringBuilder();
                    using (JsonWriter jwriter = new JsonTextWriter(new StringWriter(returnJsonBuilder)))
                    {
                        jwriter.Formatting = Formatting.Indented;
                        if (api.ReturnJsonTemplate == null)
                            jwriter.WriteNull();
                        else
                            api.ReturnJsonTemplate.WriteJsonBody(jwriter);
                        jwriter.Close();
                    }
                    ViewBag.ReturnExample = returnJsonBuilder.ToString();
                    break;
                default:
                    break;
            }
            return View();
        }

        [HttpPost]
        [Route("api/Invoke/{id}")]
        public async Task<JsonResult> CallInstance(int id)
        {
            string task = Guid.NewGuid().ToString();

            await LogStart(task, id);

            var instance = await _esbModelContext.Instances.Where(i => i.ID == id)
                    .Include(i => i.Client)
                    .Include(i => i.Service).SingleOrDefaultAsync();
            //        .Include(i => i.InstanceMapping).SingleAsync();
            #region 检查API实例定义的有效性
            if (instance == null || instance.ActiveStatus == 0)
            {
                return await Error(-100, "指定的API调用实例不存在，或者已经被停用。", task, id, EsbOperation.Checking);
            }
            if (instance.Client == null || instance.Client.ActiveStatus == 0)
            {
                return await Error(-5, "客户程序没有注册，或者已经被禁用。", task, id, EsbOperation.Checking);
            }
            string requestIp = HttpContext.Connection.RemoteIpAddress.ToString();
            if (instance.Client.HostIP != requestIp)
            {
                return await Error(-1, "请求的客户端IP地址与API客户程序注册IP地址不一致", task, id, EsbOperation.Checking);
            }
            #endregion

            using (StreamReader reader = new StreamReader(Request.Body))
            {
                try
                {
                    //RequestBody request = JsonConvert.DeserializeObject<RequestBody>(await reader.ReadToEndAsync());
                    JObject originalRequest = JObject.Parse(await reader.ReadToEndAsync());
                    #region 检查请求和服务是否正确
                    int clientId = originalRequest.SelectToken("$.id").Value<int>();
                    string clientToken = originalRequest.SelectToken("$.token").Value<string>();
                    if (clientId != instance.Client.ID || clientToken != instance.Client.Token)
                    {
                        return await Error(-4, "请求客户端提供的客户程序鉴权错误", task, id, EsbOperation.Checking);
                    }
                    if (instance.Service == null || instance.Service.ActiveStatus == 0)
                    {
                        return await Error(1, "请求的服务不存在，或者尚未启用", task, id, EsbOperation.Checking);
                    }
                    if (instance.Service.Type != ServiceType.SimpleRESTful)
                    {
                        throw new Exception("尚未实现的服务协定");
                    }
                    #endregion

                    var api = SimpleRestfulDescriptorViewModel.CreateFrom(instance.Service);
                    string fullUrl = api.BaseApiUrl.TrimEnd(new char[] { '/', ' ' });
                    StringBuilder bodyBuilder = new StringBuilder();
                    JToken requestJson = originalRequest.SelectToken("$.body");
                    if (requestJson != null)
                    {
                        #region 准备调用所需要的各类输入参数
                        if (!string.IsNullOrWhiteSpace(api.RouteDataTemplate))
                        {
                            string[] routeParameters = api.RouteDataTemplate.Trim().Split(
                                '/', StringSplitOptions.RemoveEmptyEntries);
                            foreach (string route in routeParameters)
                            {
                                fullUrl = fullUrl + "/" + requestJson.SelectToken("$." + REQ_JSON_ROUTE).Value<string>();
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(api.QueryStringTemplate))
                        {
                            string[] queryParameters = api.QueryStringTemplate.Trim()
                                .TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
                            fullUrl = fullUrl + "?";
                            for (int i = 0; i < queryParameters.Length; i++)
                            {
                                string query = queryParameters[i].Trim().TrimEnd('=');
                                fullUrl = fullUrl + query + "=" + requestJson.SelectToken("$." + REQ_JSON_QUERY).Value<string>();
                            }
                            fullUrl.TrimEnd('&');
                        }
                        if (api.JsonBodyTemplate != null)
                        {
                            //向api提交请求的RequestBody中JSON优先
                            using (JsonWriter writer = new JsonTextWriter(new StringWriter(bodyBuilder)))
                            {
                                writer.Formatting = Formatting.Indented;
                                using (JTokenReader jtReader = new JTokenReader(requestJson.SelectToken("$." + REQ_JSON_JSON)))
                                {
                                    writer.WriteToken(jtReader);
                                }
                                writer.Close();
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(api.FormBodyTemplate))
                        {
                            //如果没有JSON但是有FORM
                            string[] formParameters = api.FormBodyTemplate.Trim()
                                .TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 0; i < formParameters.Length; i++)
                            {
                                bodyBuilder.Append(formParameters[i].Trim().TrimEnd('='));
                                bodyBuilder.Append('=');
                                bodyBuilder.Append(requestJson.SelectToken("$." + REQ_JSON_FORM).Value<string>());
                                bodyBuilder.Append('&');
                            }
                            if (bodyBuilder.Length > 0)
                            {
                                bodyBuilder.Remove(bodyBuilder.Length - 1, 1);
                            }
                        }
                        #endregion
                    }

                    await LogPreparation(task, id, fullUrl, "已完成参数准备");

                    #region 准备请求
                    WebRequest apiRequest;
                    if(fullUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                    {
                        apiRequest = HttpWebRequest.CreateHttp(fullUrl);
                        ((HttpWebRequest)apiRequest).ServerCertificateValidationCallback
                            = (s, c, e, t) => true;
                    }
                    else
                    {
                        apiRequest = WebRequest.Create(fullUrl);
                    }
                    apiRequest.Method = api.Method.ToString();
                    foreach (var header in api.Headers)
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

                    await LogInvoking(task, id, fullUrl, "API请求已就绪，正在发出请求");
                    try
                    {
                        string apiReturnRaw;
                        #region 向API服务器发送请求并得到响应
                        using (WebResponse apiResponse = await apiRequest.GetResponseAsync())
                        {
                            // foreach (var apiResponseKey in apiResponse.Headers.AllKeys)
                            // {
                            //     if (!Response.Headers.ContainsKey(apiResponseKey))
                            //     {
                            //         Response.Headers.Add(apiResponseKey, apiResponse.Headers[apiResponseKey]);
                            //     }
                            // }
                            using (StreamReader apiResponseReader = new StreamReader(apiResponse.GetResponseStream(), Encoding.UTF8))
                            {
                                apiReturnRaw = await apiResponseReader.ReadToEndAsync();
                            }
                        }
                        #endregion

                        await LogInvoking(task, id, fullUrl, "API请求已经返回，正在检查服务器状态");

                        #region 对响应进行正确性检查
                        if(api.SuccessRule == Models.SimpleRESTfulSuccess.NoException)
                        {
                            await LogEnd(task, id, fullUrl, apiReturnRaw.Length + 4);
                            return Json(new ResponseBody{
                                ReturnCode = 0, Response = apiReturnRaw
                            });
                        }//肯定不是NoException规则了。
                        string checkSegment;
                        if(api.SuccessRule == Models.SimpleRESTfulSuccess.JsonPath)
                        {
                            JObject returnJson = JObject.Parse(apiReturnRaw);
                            checkSegment = returnJson.SelectToken(api.SuccessPath).ToString();
                        }
                        else
                        {
                            checkSegment = apiReturnRaw;
                        }
                        if(string.IsNullOrWhiteSpace(api.SuccessRegx?.Trim()))
                        {
                            //没有正则表达式，那么表示可以任意匹配
                            await LogEnd(task, id, fullUrl, apiReturnRaw.Length + 4);
                            return Json(new ResponseBody{
                                ReturnCode = 0, Response = apiReturnRaw
                            });
                        }
                        RegexOptions regexOptions = api.IgnoreCase? RegexOptions.IgnoreCase: RegexOptions.None;
                        if(Regex.IsMatch(checkSegment, api.SuccessRegx, regexOptions))
                        {
                            await LogEnd(task, id, fullUrl, apiReturnRaw.Length + 4);
                            return Json(new ResponseBody{
                                ReturnCode = 0, Response = apiReturnRaw
                            });
                        }
                        else
                        {//TODO: 记录错误日志。
                            await LogApiError(task, id, fullUrl, 
                                $"in-params={requestJson.ToString()} response={apiReturnRaw}");
                            return await Error(1, "服务器返回错误。具体消息需要查看日志。", task, id, EsbOperation.Respond, fullUrl);
                        }
                        #endregion
                    }
                    catch (Exception ex)
                    {
                        return await Error(1, ex.Message, task, id, EsbOperation.Invoking);
                    }
                }
                catch (Exception ex)
                {
                    return await Error(-100, "未知的异常" + ex.Message, task, id, EsbOperation.Checking);
                }
            }
        }
    }
}