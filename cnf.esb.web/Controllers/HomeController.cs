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

namespace cnf.esb.web.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
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
                    if(returnNode != null)
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
        public HomeController(IOptionsSnapshot<AdminSettings> adminSettings)
        {
            _admin = adminSettings.Value;
        }
        public IActionResult Login()
        {
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return View();
        }

        [HttpPost]
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
            return RedirectToAction(nameof(Login));
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
