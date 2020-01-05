using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System;

namespace cnf.esb.testApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController:ControllerBase
    {
        [HttpGet("Get")]
        public ActionResult<string> Get()
        {
            return "hello world";
        }

        [HttpGet("Get2")]
        public ActionResult<string> Get2()
        {
            return "Hello world, 2.0";
        }

        [HttpPost("CreateOrgs")]
        public async Task<IActionResult> CreateOrganizations()
        {
            using(StreamReader reader = new StreamReader(Request.Body))
            {
                string postData = await reader.ReadToEndAsync();
                try
                {
                    var data = JsonConvert.DeserializeObject<Models.Package>(postData);
                    int processCount = data.data.Count;
                    var result = new Models.ReturnObject
                    {
                        data = $"{processCount}个组织被处理。",
                        msg = "all right",
                        success = true
                    };
                    return new JsonResult(result);
                }
                catch(Exception ex)
                {
                    var result = new Models.ReturnObject
                    {
                        data = null,
                        msg = ex.Message,
                        success = false
                    };
                    return new JsonResult(result);
                }
            }
        }
    }
}