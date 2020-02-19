using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using cnf.esb.web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Security.Claims;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using Microsoft.EntityFrameworkCore;
using RestSharp;

namespace cnf.esb.web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        readonly EsbModelContext _esbModelContext;

        public IActionResult TestNCRest()
        {
            var client = new RestClient("http://10.1.100.131:8081/uapws/service/syncdept");
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/xml");
            request.AddParameter("application/xml", "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" \n    xmlns:test=\"http://www.w3.org/2001/XMLSchema\" \n    xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">\n　　<soap:Body>\n　　　　<test:syncDept>\n　　　　　　<string>\n  <![CDATA[\n   {\n   \"data\": {\n    \"org_dept\": [{\n     \"status\": \"2\",\n     \"pk_org\": \"机械化施工分公司\",\n     \"name\": \"测试新增12_3\",\n     \"createdate\": \"2020-02-10\",\n     \"code\": \"02100\",\n     \"pk_dept\": \"202001130003\"\n     }]\n    }\n   }\n  ]]>\n　　　　　　</string>\n　　　　</test:syncDept>\n　　</soap:Body>\n</soap:Envelope>\n", ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);
            return Content(response.Content);
        }

        public IActionResult Get()
        {
            string soapXml = @"
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <soap:Fault>
            <faultcode>soap:Client</faultcode>
            <faultstring>NO_PART_FOUND</faultstring>
        </soap:Fault>
    </soap:Body>
</soap:Envelope>
";

            XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
            using (var reader = new StringReader(soapXml))
            {
                var document = XDocument.Load(reader);
                var faultNode = (from e in document.Descendants(soap + "Fault")
                                 select e).SingleOrDefault();
                if (faultNode == null)
                {
                    //no fault,
                    return Content(soapXml);
                }
                else
                {
                    return Content($"FaultCode={faultNode.Element("faultcode").Value},"
                            + $"FaultString={faultNode.Element("faultstring").Value}");
                }
            }
        }

        public IActionResult Get2()
        {
            string soapXml = @"
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Header>
       <ns0:Urc xmlns:ns0=""http://ws.uap.nc/lang"">
	  <ns0:datasource>design</ns0:datasource>
	  <ns0:userCode>#UAP#</ns0:userCode>
	  <ns0:langCode>simpchn</ns0:langCode>
       </ns0:Urc>
    </soap:Header>
    <soap:Body>
       <ns1:syncDeptResponse xmlns:ns1=""http://ws.deptsync.itf.nc/IDeptSyncWSMessage"">
	        <return>{""message"":""部门编码重复；部门名称重复；"",""data"":""[]"",""success"":false}</return>
       </ns1:syncDeptResponse>
    </soap:Body>
</soap:Envelope>
            ";
            XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
            XNamespace ns0 = "http://ws.uap.nc/lang";
            XNamespace ns1 = "http://ws.deptsync.itf.nc/IDeptSyncWSMessage";
            using (var reader = new StringReader(soapXml))
            {
                var document = XDocument.Load(reader);
                var faultNode = (from e in document.Descendants(soap + "Fault")
                                 select e).SingleOrDefault();
                if (faultNode == null)
                {
                    //no fault,
                    var returnNode = (from e in document.Descendants("return")
                                      select e).SingleOrDefault();
                    if (returnNode != null)
                    {
                        return Content(returnNode.Value);
                    }
                    else
                    {
                        return Content(soapXml);
                    }
                }
                else
                {
                    return Content($"FaultCode={faultNode.Element("faultcode").Value},"
                            + $"FaultString={faultNode.Element("faultstring").Value}");
                }
            }
        }

        readonly AdminSettings _admin;
        public HomeController(IOptionsSnapshot<AdminSettings> adminSettings,
                EsbModelContext modelContext)
        {
            _admin = adminSettings.Value;
            _esbModelContext = modelContext;
        }

        [AllowAnonymous]
        public IActionResult Login()
        {
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return View();
        }

        [HttpPost, AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel viewModel)
        {
            if (viewModel.UserName == _admin.UserName && viewModel.Password == _admin.Password)
            {
                var claims = new List<Claim>
                {
                    new Claim("userid", viewModel.UserName)
                };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var properties = new AuthenticationProperties
                {
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(20),
                };
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
                return RedirectToAction(nameof(Index));
            }
            else
            {
                ViewData["ErrorMessage"] = "用户名或者口令错误";
                return View(viewModel);
            }
        }

        public IActionResult Logout()
        {
            HttpContext.SignOutAsync();
            return RedirectToAction(nameof(Index));
        }

        [AllowAnonymous]
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Dashboard()
        {
            ViewBag.ConsumerCount = _esbModelContext.Consumers.Count();
            ViewBag.ServiceCount = _esbModelContext.Services.Count();
            ViewBag.InstanceCount = _esbModelContext.Instances.Count();

            return View();
        }

        [AllowAnonymous]
        public async Task<JsonResult> CalcFlow(DateTime? start, DateTime? end)
        {
            var dateGroupedLogs = _esbModelContext.Logs
                    .Where(l => (start == null || l.CreatedOn.Date >= start.Value)
                            && (end == null || l.CreatedOn.Date <= end))
                    .GroupBy(l => l.CreatedOn.Date);
            var dayFlow = from g in dateGroupedLogs
                          select new
                          {
                              Day = g.Key.ToString("yyyyMMdd"),
                              Tasks = g.Select(l => l.TaskIdentity).Distinct().Count(),
                              Errors = g.Where(l => l.LogLevel == EsbLogLevel.Failure).Count(),
                              In = g.Sum(l => l.RequestLength),
                              Out = g.Sum(l => l.ResponseLength)
                          };
            return Json(await dayFlow.ToListAsync());
        }

        [AllowAnonymous]
        public async Task<ContentResult> GetFlowTable(DateTime? start, DateTime? end)
        {
            var dayFlow = _esbModelContext.Logs
                    .Where(l => (start == null || l.CreatedOn.Date >= start.Value)
                            && (end == null || l.CreatedOn.Date <= end))
                    .GroupBy(l => l.CreatedOn.Date)
                    .Select(g => new
                    {
                        Day = g.Key.ToString("yyyyMMdd"),
                        Tasks = g.Select(l => l.TaskIdentity).Distinct().Count(),
                        Errors = g.Where(l => l.LogLevel == EsbLogLevel.Failure).Count(),
                        In = g.Sum(l => l.RequestLength),
                        Out = g.Sum(l => l.ResponseLength)

                    });
            System.Text.StringBuilder tableBuilder = new System.Text.StringBuilder();

            tableBuilder.AppendLine("<table class='table table-sm table-border'>");
            tableBuilder.AppendLine("<tr>");
            tableBuilder.AppendLine($"<td>日期</td>");
            tableBuilder.AppendLine($"<td>请求数</td>");
            tableBuilder.AppendLine($"<td>错误数</td>");
            tableBuilder.AppendLine($"<td>入栈字节数</td>");
            tableBuilder.AppendLine($"<td>出栈字节数</td>");
            tableBuilder.AppendLine("</tr>");
            foreach (var r in await dayFlow.ToListAsync())
            {
                tableBuilder.AppendLine("<tr>");
                tableBuilder.AppendLine($"<td>{r.Day}</td>");
                tableBuilder.AppendLine($"<td>{r.Tasks}</td>");
                tableBuilder.AppendLine($"<td>{r.Errors}</td>");
                tableBuilder.AppendLine($"<td>{r.In}</td>");
                tableBuilder.AppendLine($"<td>{r.Out}</td>");
                tableBuilder.AppendLine("</tr>");
            }
            tableBuilder.AppendLine("</table>");
            return Content(tableBuilder.ToString());
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
