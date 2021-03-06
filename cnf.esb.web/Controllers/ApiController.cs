using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using cnf.esb.web.Models;
using Newtonsoft.Json.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace cnf.esb.web.Controllers
{
    [AllowAnonymous]
    public class ApiController : Controller
    {
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

        async Task LogPreparation(string task, int instanceId, string message)
        {
            var log = EsbLog.Create(task, EsbLogLevel.Message, EsbOperation.Preparation,
                HttpContext.Connection.RemoteIpAddress.ToString(), instanceId, message);
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
        async Task<ContentResult> Error(int code, string message, string task, int instanceId,
            EsbOperation period, string invokedUrl = "")
        {
            var log = EsbLog.Create(task, EsbLogLevel.Failure, period,
                HttpContext.Connection.RemoteIpAddress.ToString(), instanceId, message, invokedUrl);
            _esbModelContext.Add(log);
            await _esbModelContext.SaveChangesAsync();
            ResponseBody response = new ResponseBody(code, message, "", SimpleRESTfulReturn.Empty);
            await LogEnd(task, instanceId, invokedUrl, message.Length + 4);
            return Content(response.Value);
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
            ViewBag.Url = "/api/Invoke/" + id.ToString();
            IServiceDescriptorViewModel api;
            Models.SimpleRESTfulReturn returnType;
            if (instance.Service.Type == ServiceType.SimpleRESTful)
            {
                api = SimpleRestfulDescriptorViewModel.CreateFrom(instance.Service);
                returnType = ((SimpleRestfulDescriptorViewModel)api).ReturnType;
            }
            else if (instance.Service.Type == ServiceType.NCWebService)
            {
                api = NCDescriptorViewModel.CreateFrom(instance.Service);
                returnType = Models.SimpleRESTfulReturn.Json;
            }
            else if(instance.Service.Type == ServiceType.PrimetonService)
            {
                api = PrimetonDescriptorViewModel.CreateFrom(instance.Service);
                returnType = Models.SimpleRESTfulReturn.Json;
            }
            else
            {
                ViewBag.ErrorMessage = $"尚未实现的服务协定类型：{instance.Service.Type}";
                return View();
            }
            ViewBag.BodyExample = api.GetPostSample();
            ViewBag.ReturnType = StringHelper.GetEnumDisplayName(typeof(Models.SimpleRESTfulReturn), returnType);
            switch (returnType)
            {
                case Models.SimpleRESTfulReturn.Empty:
                    ViewBag.ReturnExample = "服务不返回任何值，或者返回值没有意义。";
                    break;
                case Models.SimpleRESTfulReturn.PlainText:
                    ViewBag.ReturnExample = "服务返回无固定格式文本，您需要具体联系服务提供方获得说明。";
                    break;
                case Models.SimpleRESTfulReturn.Json:
                    ViewBag.ReturnExample = api.GetReturnSample();
                    break;
                default:
                    break;
            }
            return View();
        }

        [HttpPost]
        [Route("api/Invoke/{id}")]
        public async Task<ContentResult> CallInstance(int id)
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
            string[] availableIps = instance.Client.HostIP.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (availableIps == null || availableIps.Length <= 0)
            {
                return await Error(-1, "没有定义客户端有效IP地址列表", task, id, EsbOperation.Checking);
            }
            bool hasFound = false;
            foreach (string ip in availableIps)
            {
                if (requestIp.EndsWith(ip))
                {
                    hasFound = true;
                    break;
                }
            }
            if (!hasFound) //(instance.Client.HostIP != requestIp)
            {
                return await Error(-1, "请求的客户端IP地址不在API客户程序注册IP地址清单中", task, id, EsbOperation.Checking);
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
                    #endregion

                    await LogPreparation(task, id, "已准备好输入参数");

                    IServiceDescriptorViewModel api;
                    if (instance.Service.Type == ServiceType.SimpleRESTful)
                    {
                        api = SimpleRestfulDescriptorViewModel.CreateFrom(instance.Service);
                    }
                    else if (instance.Service.Type == ServiceType.NCWebService)
                    {
                        api = NCDescriptorViewModel.CreateFrom(instance.Service);
                    }
                    else if(instance.Service.Type == ServiceType.PrimetonService)
                    {
                        api = PrimetonDescriptorViewModel.CreateFrom(instance.Service);
                    }
                    else
                    {
                        throw new Exception($"尚未实现的服务协定:{instance.Service.Type}");
                    }

                    await LogInvoking(task, id, "", "API请求已就绪，正在发出...");

                    RawResponse rawResponse = await api.GetResponse(originalRequest);

                    await LogInvoking(task, id, rawResponse.RequestUrl, "API请求已经返回，正在检查服务器状态");

                    try
                    {
                        string apiReturnRaw = await rawResponse.ReadContentAsync();

                        if (api.CheckResponse(apiReturnRaw, out string response, out var type))
                        {
                            await LogEnd(task, id, rawResponse.RequestUrl, response.Length + 4);
                            return Content((new ResponseBody(0, "", response, type)).Value);
                        }
                        else
                        {
                            await LogApiError(task, id, rawResponse.RequestUrl,
                                $"in-params={originalRequest.ToString()} response={apiReturnRaw}");
                            return await Error(1, "服务器返回错误。具体消息需要查看日志。", task, id,
                                EsbOperation.Respond, rawResponse.RequestUrl);
                        }
                    }
                    catch (Exception ex)
                    {
                        return await Error(1, ex.ToString(), task, id, EsbOperation.Checking);
                    }
                }
                catch (Exception ex)
                {
                    return await Error(-100, ex.ToString(), task, id, EsbOperation.Invoking);
                }
            }
        }

        [HttpPost]
        [Route("api/Test/{id}")]
        public async Task<ContentResult> TestInstance(int id)
        {
            var instance = await _esbModelContext.Instances.Where(i => i.ID == id)
                    .Include(i => i.Client)
                    .Include(i => i.Service).SingleOrDefaultAsync();
            //        .Include(i => i.InstanceMapping).SingleAsync();
            #region 检查API实例定义的有效性
            if (instance == null || instance.ActiveStatus == 0)
            {
                return Content("ERROR: 指定的API调用实例不存在，或者已经被停用。");
            }
            if (instance.Service == null || instance.Service.ActiveStatus == 0)
            {
                return Content("ERROR: 请求的服务不存在，或者尚未启用");
            }
            #endregion

            using (StreamReader reader = new StreamReader(Request.Body))
            {
                try
                {
                    //RequestBody request = JsonConvert.DeserializeObject<RequestBody>(await reader.ReadToEndAsync());
                    JObject originalRequest = JObject.Parse(await reader.ReadToEndAsync());

                    IServiceDescriptorViewModel api;
                    if(instance.Service.Type == ServiceType.PrimetonService)
                    {
                        api = PrimetonDescriptorViewModel.CreateFrom(instance.Service);
                    }
                    else
                    {
                        throw new Exception($"服务协定:{instance.Service.Type}没有设计测试功能");
                    }

                    return Content(((PrimetonDescriptorViewModel)api).generatePostXml(originalRequest));
                }
                catch (Exception ex)
                {
                    return Content(ex.ToString());
                }
            }
        }
    }
}