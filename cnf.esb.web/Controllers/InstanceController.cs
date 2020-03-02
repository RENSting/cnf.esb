using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cnf.esb.web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace cnf.esb.web.Controllers
{
    [Authorize]
    public class InstanceController : Controller
    {
        public const string CROSS_VIEWMODEL_KEY = "CrossViewModel";
        public const string CROSS_ERROR_KEY = "CrossError";

        readonly EsbModelContext _esbModelContext;

        public InstanceController(EsbModelContext esbModelContext)
        {
            _esbModelContext = esbModelContext;
        }

        public async Task<IActionResult> Show(int id, int? pageIndex, EsbLogLevel? level, DateTime? logStart, DateTime? logEnd)
        {
            var instance = await _esbModelContext.Instances.Where(m=>m.ID==id)
                                    .Include(m=>m.Client)
                                    .Include(m=>m.Service).SingleOrDefaultAsync();
            if(instance == null)
            {
                return NotFound();
            }
            int pageSize = 20;
            if(pageIndex == null)pageIndex = 0;
            var logs = from log in _esbModelContext.Logs
                        where log.InstanceID == id
                            && (level == null || log.LogLevel==level.Value)
                            && (logStart == null || log.CreatedOn>= logStart.Value)
                            && (logEnd == null || log.CreatedOn <= logEnd.Value.AddDays(1.0D))
                        orderby log.CreatedOn descending
                        select log;
            ViewBag.Filter = (level == null?"": $"{level.ToString()}  ")
                    + (logStart==null?"":$"晚于{logStart.Value.ToShortDateString()}  ")
                    + (logEnd == null?"":$"早于{logEnd.Value.ToShortDateString()}");

            int totalCount = await logs.CountAsync();
            ViewBag.Pages = (int)Math.Ceiling(totalCount * 1.0D / pageSize);
            ViewBag.PageIndex = pageIndex;
            int skipCount = pageIndex.Value * pageSize;
            if(skipCount > totalCount) skipCount = totalCount;
            ViewBag.Logs = await logs.Skip(skipCount).Take(pageSize).ToListAsync();
            return View(instance);
        }

        public async Task<IActionResult> LogDetails(int id)
        {
            var log = await _esbModelContext.Logs.SingleOrDefaultAsync(
                    m => m.ID == id);
            if(log == null)
            {
                return NotFound();
            }
            else
            {
                return View(log);
            }
        }

        public async Task<IActionResult> Index(int? consumer, string group, string name)
        {
            IQueryable<KeyValuePair<int,string>> consumers = from c in _esbModelContext.Consumers
                            orderby c.Name
                            select  new KeyValuePair<int, string>(c.ID, c.Name);

            var groups = from s in _esbModelContext.Services
                         orderby s.GroupName
                         select s.GroupName;

            var instances = _esbModelContext.Instances.Include(i=>i.Client).Include(i=>i.Service)
                    .Where(i => string.IsNullOrWhiteSpace(name) 
                            || i.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    .Where(i=> string.IsNullOrWhiteSpace(group)
                            || i.Service.GroupName.Equals(group, StringComparison.OrdinalIgnoreCase))
                    .Where(i=> consumer==null || i.ClientID==consumer.Value);

            ViewBag.Consumers = await consumers.ToListAsync();
            ViewBag.Groups = await groups.Distinct().ToListAsync();
            ViewBag.SelectedConsumer = consumer;
            ViewBag.SelectedGroup = group;
            ViewBag.Name = name;
            return View(InstanceViewModel.ConvertFrom(await instances.ToListAsync()));
        }

        [ActionName("IndexOfService")]
        public async Task<IActionResult> Index(int serviceId)
        {
            var instances = _esbModelContext.Instances.Where(i => i.ServiceID == serviceId)
                    .Include(i => i.Client)
                    .Include(i => i.Service)
                    .Include(i => i.InstanceMapping);
            ViewBag.ServiceMode = true;
            return View(nameof(Index), InstanceViewModel.ConvertFrom(await instances.ToListAsync()));
        }

        [ActionName("CreateForService")]
        public async Task<IActionResult> Create(int serviceId)
        {
            EsbService service = await _esbModelContext.Services.FindAsync(serviceId);
            if (service == null)
            {
                return RedirectToAction(nameof(Create));
            }
            InstanceViewModel viewModel = new InstanceViewModel
            {
                ActiveStatus = true,
                Description = service.FullDescription,
                InstanceName = service.Name,
                ServiceID = service.ID,
                ServiceName = service.Name,
                ParameterMappings = new List<ParameterMapping>()
            };
            // create default mappings
            if (service.Type == ServiceType.SimpleRESTful)
            {
                var descriptor = JsonConvert.DeserializeObject<SimpleRestfulDescriptorViewModel>(service.ServiceDescriptor);
                if (descriptor == null)
                {
                    throw new Exception($"选择的服务{service.Name}尚未定义服务协定");
                }
                if (!string.IsNullOrWhiteSpace(descriptor.RouteDataTemplate))
                {
                    string[] parts = descriptor.RouteDataTemplate.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        string s = parts[i].Trim();
                        viewModel.ParameterMappings.Add(new ParameterMapping
                        {
                            Source = "route",
                            ServerPath = s,
                            MappingType = MappingType.Path,
                            ClientPath = s
                        });
                    }
                }
                if (!string.IsNullOrWhiteSpace(descriptor.QueryStringTemplate))
                {
                    StringHelper.MapQueryFormParametersInto(viewModel.ParameterMappings, "query", descriptor.QueryStringTemplate);
                }
                if (!string.IsNullOrWhiteSpace(descriptor.FormBodyTemplate))
                {
                    StringHelper.MapQueryFormParametersInto(viewModel.ParameterMappings, "form", descriptor.FormBodyTemplate);
                }
                if (descriptor.JsonBodyTemplate != null)
                {
                    //优先级最高的是JSON模板
                    descriptor.JsonBodyTemplate.GenerateMappingInto(viewModel.ParameterMappings, "", "");
                }
            }
            else if(service.Type == ServiceType.NCWebService)
            {
                var descriptor = JsonConvert.DeserializeObject<NCDescriptorViewModel>(service.ServiceDescriptor);
                if (descriptor == null)
                {
                    throw new Exception($"选择的服务{service.Name}尚未定义服务协定");
                }
                if(descriptor.ParameterBody != null)
                {
                    descriptor.ParameterBody.GenerateMappingInto(viewModel.ParameterMappings, "", "");
                }
            }
            else
            {
                throw new Exception("尚未实现除Simple RESTful外的其它服务类型");
            }

            TempData.Put(CROSS_VIEWMODEL_KEY, viewModel);
            return RedirectToAction(nameof(Edit));
        }

        public async Task<IActionResult> Edit()
        {
            InstanceViewModel viewModel = null;
            if (TempData.ContainsKey(CROSS_VIEWMODEL_KEY))
            {
                viewModel = TempData.Get<InstanceViewModel>(CROSS_VIEWMODEL_KEY);
                if (TempData.ContainsKey(CROSS_ERROR_KEY))
                {
                    ModelState.AddModelError(string.Empty, TempData[CROSS_ERROR_KEY].ToString());
                }
            }
            else
            {
                viewModel = new InstanceViewModel();
                ModelState.AddModelError(string.Empty, "实例必须从注册服务创建。");
            }

            var consumers = from c in _esbModelContext.Consumers
                            where c.ActiveStatus > 0
                            orderby c.Name
                            select new { ClientID = c.ID, ClientName = c.Name };
            List<SelectListItem> consumerList = new List<SelectListItem>();
            foreach (var c in await consumers.ToListAsync())
            {
                consumerList.Add(new SelectListItem(c.ClientName, c.ClientID.ToString()));
            }
            ViewData["Consumers"] = consumerList;

            return View(viewModel);
        }

        [ActionName("EditInstance")]
        public async Task<IActionResult> Edit(int id)
        {
            var instance = await _esbModelContext.Instances.Where(i => i.ID == id)
                    .Include(i => i.Client)
                    .Include(i => i.Service)
                    .Include(i => i.InstanceMapping).SingleAsync();
            if (instance == null)
            {
                return NotFound();
            }
            InstanceViewModel viewModel = instance;
            TempData.Put(CROSS_VIEWMODEL_KEY, viewModel);
            return RedirectToAction(nameof(Edit));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(InstanceViewModel viewModel)
        {
            viewModel.ParameterMappings = new List<ParameterMapping>();
            if (Request.Form.ContainsKey("item.ServerPath"))
            {
                var sources = Request.Form["item.Source"];
                var serverPaths = Request.Form["item.ServerPath"];
                var mappingTypes = Request.Form["item.MappingType"];
                var clientPaths = Request.Form["item.ClientPath"];
                for (int i = 0; i < serverPaths.Count; i++)
                {
                    ParameterMapping mapping = new ParameterMapping();
                    mapping.Source = sources[i];
                    mapping.ServerPath = serverPaths[i];
                    mapping.MappingType = Enum.Parse<MappingType>(mappingTypes[i]);
                    //(MappingType)int.Parse(mappingTypes[i]);
                    mapping.ClientPath = clientPaths[i];
                    viewModel.ParameterMappings.Add(mapping);
                }
            }
            if (ModelState.IsValid)
            {
                if (viewModel.ClientID <= 0
                    || viewModel.ServiceID <= 0)
                {
                    TempData[CROSS_ERROR_KEY] = "没有选择客户或者服务";
                    TempData.Put(CROSS_VIEWMODEL_KEY, viewModel);
                    RedirectToAction(nameof(Edit));
                }
                try
                {
                    EsbInstance instance;
                    if (viewModel.InstanceID > 0)
                    {
                        instance = await _esbModelContext.Instances.Where(m => m.ID == viewModel.InstanceID)
                                        .Include(m => m.InstanceMapping).SingleAsync();
                        if (instance == null)
                        {
                            TempData[CROSS_ERROR_KEY] = "没有找到要修改的API实例（是否已被删除？）";
                            TempData.Put(CROSS_VIEWMODEL_KEY, viewModel);
                            RedirectToAction(nameof(Edit));
                        }
                    }
                    else
                    {
                        instance = new EsbInstance();
                    }
                    instance.ActiveStatus = viewModel.ActiveStatus ? 1 : 0;
                    instance.ClientID = viewModel.ClientID;
                    instance.Description = viewModel.Description;
                    instance.Name = viewModel.InstanceName;
                    instance.ServiceID = viewModel.ServiceID;
                    if (viewModel.InstanceID > 0)
                    {
                        _esbModelContext.Update(instance);
                    }
                    else
                    {
                        _esbModelContext.Add(instance);
                    }
                    await _esbModelContext.SaveChangesAsync();

                    if (instance.InstanceMapping == null)
                    {
                        instance.InstanceMapping = new InstanceMapping
                        {
                            InstanceID = instance.ID,
                            ParameterMappings = JsonConvert.SerializeObject(viewModel.ParameterMappings)
                        };
                        _esbModelContext.Add(instance.InstanceMapping);
                    }
                    else
                    {
                        instance.InstanceMapping.InstanceID = instance.ID;
                        instance.InstanceMapping.ParameterMappings = JsonConvert.SerializeObject(viewModel.ParameterMappings);
                        _esbModelContext.Update(instance.InstanceMapping);
                    }

                    await _esbModelContext.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData[CROSS_ERROR_KEY] = ex.Message;
                }
            }
            else
            {
                TempData[CROSS_ERROR_KEY] = "输入数据有错误";
            }
            TempData.Put(CROSS_VIEWMODEL_KEY, viewModel);
            return RedirectToAction(nameof(Edit));
        }
    }
}