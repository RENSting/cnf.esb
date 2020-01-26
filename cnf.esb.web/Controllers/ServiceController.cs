using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cnf.esb.web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace cnf.esb.web.Controllers
{
    [Authorize]
    public class ServiceController : Controller
    {
        public const string STATE_ADD_HEADER_ERROR = "AddHeaderError";
        public const string STATE_NEW_HEADER_KEY = "NewHeaderKey";
        public const string STATE_NEW_HEADER_VALUE = "NewHeaderValue";
        public const string STATE_RESTJSON_ERROR = "JsonTemplateError";
        public const string STATE_RESTRETURN_ERROR = "ReturnJsonError";
        public const string STATE_NCPARAMETER_ERROR = "NCParameterError";
        public const string STATE_NCRETURN_ERROR = "NCReturnError";
        public const string EDIT_JSON_CROSS_ACTION_DATA_KEY = "CrossActionJson";
        public const string EDIT_SERVICE_CROSS_ACTION_DATA_KEY = "CrossActionService";

        readonly EsbModelContext _esbModelContext;

        public ServiceController(EsbModelContext esbModelContext)
        {
            _esbModelContext = esbModelContext;
        }

        #region Service base information management

        public async Task<IActionResult> Index()
        {
            var services = from s in _esbModelContext.Services
                           select s;
            return View(await services.ToListAsync());
        }

        public IActionResult CreateService()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateService(
            [Bind("Name, Type, FullDescription")] EsbService newService)
        {
            if (ModelState.IsValid)
            {
                newService.ActiveStatus = 1;
                newService.CreatedOn = DateTime.Now;
                newService.ServiceDescriptor = "";

                _esbModelContext.Services.Add(newService);
                await _esbModelContext.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            return View(newService);
        }

        public async Task<IActionResult> EditService(int? id)
        {
            EditServiceViewModel viewModel = await _esbModelContext.Services.FindAsync(id);
            if (viewModel == null)
            {
                return NotFound();
            }
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditService(EditServiceViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var service = await _esbModelContext.Services.FindAsync(viewModel.ServiceID);
                if (service == null)
                {
                    return NotFound();
                }
                service.ActiveStatus = viewModel.ActiveStatus ? 1 : 0;
                service.FullDescription = viewModel.FullDescription;
                service.GroupName = viewModel.GroupName;
                service.Name = viewModel.Name;
                if (service.Type != viewModel.Type)
                {
                    //TODO: 服务协定类型改变，清空协定定义，将来可以设置转换功能。
                    service.Type = viewModel.Type;
                    service.FullDescription = "";
                }
                _esbModelContext.Update(service);
                await _esbModelContext.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            else
            {
                return View(viewModel);
            }
        }

        #endregion

        #region According service type, configure methods here

        T RestoreAndUpdateService<T>(T uiViewModel, string savedViewModelJson)
            where T : IServiceDescriptorViewModel
        {
            IServiceDescriptorViewModel savedViewModel;
            if(uiViewModel.ServiceType == ServiceType.NCWebService)
            {
                savedViewModel = JsonConvert.DeserializeObject<NCDescriptorViewModel>(savedViewModelJson);
            }
            else if(uiViewModel.ServiceType == ServiceType.SimpleRESTful)
            {
                savedViewModel = JsonConvert.DeserializeObject<SimpleRestfulDescriptorViewModel>(savedViewModelJson);
            }
            else
            {
                throw new Exception("not implemented");
            }
            savedViewModel.UpdateFromUI(uiViewModel);
            return (T)savedViewModel;
        }

        public IActionResult EditSimpleRestfulService()
        {
            if (TempData[EDIT_SERVICE_CROSS_ACTION_DATA_KEY] == null)
            {
                throw new Exception("页面只能被内部访问，无法直接调用");
            }
            var viewModel = TempData.Get<SimpleRestfulDescriptorViewModel>(EDIT_SERVICE_CROSS_ACTION_DATA_KEY);
            if (TempData.ContainsKey(STATE_ADD_HEADER_ERROR))
            {
                ViewData[STATE_ADD_HEADER_ERROR] = TempData[STATE_ADD_HEADER_ERROR];
            }
            else
            {
                viewModel.NewHeaderKey = "";
                viewModel.NewHeaderValue = "";
            }

            if (TempData.ContainsKey(STATE_RESTJSON_ERROR))
            {
                ViewData[STATE_RESTJSON_ERROR] = TempData[STATE_RESTJSON_ERROR];
            }
            if (TempData.ContainsKey(STATE_RESTRETURN_ERROR))
            {
                ViewData[STATE_RESTRETURN_ERROR] = TempData[STATE_RESTRETURN_ERROR];
            }
            return View(viewModel);
        }

        public IActionResult EditNCWebService()
        {
            if (TempData[EDIT_SERVICE_CROSS_ACTION_DATA_KEY] == null)
            {
                throw new Exception("页面只能被内部访问，无法直接调用");
            }
            var viewModel = TempData.Get<NCDescriptorViewModel>(EDIT_SERVICE_CROSS_ACTION_DATA_KEY);

            if (TempData.ContainsKey(STATE_NCPARAMETER_ERROR))
            {
                ViewData[STATE_NCPARAMETER_ERROR] = TempData[STATE_NCPARAMETER_ERROR];
            }
            if (TempData.ContainsKey(STATE_NCRETURN_ERROR))
            {
                ViewData[STATE_NCRETURN_ERROR] = TempData[STATE_NCRETURN_ERROR];
            }
            return View(viewModel);
        }

        //GET: /Service/ConfigureService/5

        public async Task<IActionResult> ConfigureService(int id)
        {
            var service = await _esbModelContext.Services.FindAsync(id);
            if (service == null)
            {
                return NotFound();
            }

            if (service.Type == ServiceType.SimpleRESTful)
            {
                var viewModel = SimpleRestfulDescriptorViewModel.CreateFrom(service);
                if (viewModel.JsonBodyTemplate != null)
                {
                    viewModel.SelectedTab = "nav-json";
                }
                else if (!string.IsNullOrWhiteSpace(viewModel.RouteDataTemplate))
                {
                    viewModel.SelectedTab = "nav-route";
                }
                else if (!string.IsNullOrWhiteSpace(viewModel.QueryStringTemplate))
                {
                    viewModel.SelectedTab = "nav-query";
                }
                else if (!string.IsNullOrWhiteSpace(viewModel.FormBodyTemplate))
                {
                    viewModel.SelectedTab = "nav-form";
                }
                TempData.Put(EDIT_SERVICE_CROSS_ACTION_DATA_KEY, viewModel);
                return RedirectToAction(nameof(EditSimpleRestfulService));
            }
            else if (service.Type == ServiceType.NCWebService)
            {
                var viewModel = NCDescriptorViewModel.CreateFrom(service);
                TempData.Put(EDIT_SERVICE_CROSS_ACTION_DATA_KEY, viewModel);
                return RedirectToAction(nameof(EditNCWebService));
            }
            else
            {
                throw new Exception($"Not yet impletement API of type:{service.Type.ToString()}");
            }
        }

        async Task<IActionResult> SaveServiceAction(IServiceDescriptorViewModel viewModel, string serviceJson)
        {
            IServiceDescriptorViewModel originalViewModel = RestoreAndUpdateService(viewModel, serviceJson);
            var service = await _esbModelContext.Services.FindAsync(originalViewModel.ServiceID);
            if (service == null)
            {
                throw new Exception($"视图包含的服务协定所对应的服务本身[ServiceID={originalViewModel.ServiceID}]已经在数据库中不存在");
            }
            if (originalViewModel.UpdateToService(ref service, out var error) == false)
            {
                string serviceViewName;
                if (originalViewModel.ServiceType == ServiceType.SimpleRESTful)
                {
                    TempData[STATE_RESTRETURN_ERROR] = error;
                    serviceViewName = nameof(EditSimpleRestfulService);
                }
                else if (originalViewModel.ServiceType == ServiceType.NCWebService)
                {
                    TempData[STATE_NCRETURN_ERROR] = error;
                    serviceViewName = nameof(EditNCWebService);
                }
                else
                {
                    throw new Exception("not implemented");
                }
                TempData.Put(EDIT_SERVICE_CROSS_ACTION_DATA_KEY, originalViewModel);
                return RedirectToAction(serviceViewName);
            }
            _esbModelContext.Update(service);
            await _esbModelContext.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region NC service definition


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditNCParameter(NCDescriptorViewModel viewModel, string serviceJson)
        {
            return EditJsonAction(JsonTemplateNames.NCParameter, viewModel, serviceJson);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteNCParameter(NCDescriptorViewModel viewModel, string serviceJson)
        {
            return DeleteJsonAction(JsonTemplateNames.NCParameter, viewModel, serviceJson);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditNCReturn(NCDescriptorViewModel viewModel, string serviceJson)
        {
            return EditJsonAction(JsonTemplateNames.NCReturn, viewModel, serviceJson);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteNCReturn(NCDescriptorViewModel viewModel, string serviceJson)
        {
            return DeleteJsonAction(JsonTemplateNames.NCReturn, viewModel, serviceJson);
        }

        // REST: save the service
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveNCWebService(NCDescriptorViewModel viewModel, string serviceJson)
        {
            return await SaveServiceAction(viewModel, serviceJson);
        }

        #endregion

        #region RESTful service definition

        // REST: Add request header
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddHeader(SimpleRestfulDescriptorViewModel viewModel, string serviceJson)
        {
            var originalViewModel = RestoreAndUpdateService(viewModel, serviceJson);

            try
            {
                if (string.IsNullOrWhiteSpace(viewModel.NewHeaderKey)
                    || string.IsNullOrWhiteSpace(viewModel.NewHeaderValue))
                {
                    throw new Exception("请求头的Key和Value都不能是空白");
                }

                var duplics = from key in originalViewModel.Headers.Keys
                              where key.Equals(viewModel.NewHeaderKey.Trim(),
                                            StringComparison.OrdinalIgnoreCase)
                              select key;
                if (duplics.Count() > 0)
                {
                    throw new Exception($"请求头已经包含{originalViewModel.NewHeaderKey}，Key是大小写不敏感的");
                }

                originalViewModel.Headers.Add(viewModel.NewHeaderKey.Trim(), viewModel.NewHeaderValue.Trim());
            }
            catch (Exception ex)
            {
                originalViewModel.NewHeaderKey = viewModel.NewHeaderKey;
                originalViewModel.NewHeaderValue = viewModel.NewHeaderValue;
                TempData[STATE_ADD_HEADER_ERROR] = ex.Message;
            }
            TempData.Put(EDIT_SERVICE_CROSS_ACTION_DATA_KEY, originalViewModel);
            return RedirectToAction(nameof(EditSimpleRestfulService));
        }

        // REST: Remove request header
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveHeader(SimpleRestfulDescriptorViewModel viewModel,
            string serviceJson, string headerKey)
        {
            var originalViewModel = RestoreAndUpdateService(viewModel, serviceJson);
            originalViewModel.Headers.Remove(headerKey);
            TempData.Put(EDIT_SERVICE_CROSS_ACTION_DATA_KEY, originalViewModel);
            return RedirectToAction(nameof(EditSimpleRestfulService));
        }

        // REST: create json body
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateJson(SimpleRestfulDescriptorViewModel viewModel, string serviceJson)
        {
            var originalViewModel = RestoreAndUpdateService(viewModel, serviceJson);
            if (originalViewModel.JsonBodyTemplate == null)
            {
                originalViewModel.JsonBodyTemplate = new JsonTemplate();
                originalViewModel.JsonBodyTemplate.ValueType = Models.ValueType.Integer;
            }
            originalViewModel.SelectedTab = "nav-json";
            TempData.Put(EDIT_SERVICE_CROSS_ACTION_DATA_KEY, originalViewModel);
            return RedirectToAction(nameof(EditSimpleRestfulService));
        }

        // REST： delete json body
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteJson(SimpleRestfulDescriptorViewModel viewModel, string serviceJson)
        {
            return DeleteJsonAction(JsonTemplateNames.RESTParameter, viewModel, serviceJson);
        }

        // REST: edit json body
        /// <summary>
        /// 编辑API输入参数JSON模板
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditJson(SimpleRestfulDescriptorViewModel viewModel, string serviceJson)
        {
            return EditJsonAction(JsonTemplateNames.RESTParameter, viewModel, serviceJson);
        }

        // REST: edit return json 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditReturnJson(SimpleRestfulDescriptorViewModel viewModel, string serviceJson)
        {
            return EditJsonAction(JsonTemplateNames.RESTReturnValue, viewModel, serviceJson);
        }

        // REST： delete return json
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteReturnJson(SimpleRestfulDescriptorViewModel viewModel, string serviceJson)
        {
            return DeleteJsonAction(JsonTemplateNames.RESTReturnValue, viewModel, serviceJson);
        }

        // REST: save the service
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSimpleRestfulService(
            SimpleRestfulDescriptorViewModel viewModel, string serviceJson)
        {
            return await SaveServiceAction(viewModel, serviceJson);
        }

        #endregion

        #region EDIT JSON TEMPLATE

        ServiceType GetServiceType(JsonTemplateNames partName)
        {
            switch (partName)
            {
                case JsonTemplateNames.RESTParameter:
                case JsonTemplateNames.RESTReturnValue:
                    return ServiceType.SimpleRESTful;
                case JsonTemplateNames.NCParameter:
                case JsonTemplateNames.NCReturn:
                    return ServiceType.NCWebService;
                default:
                    throw new Exception("not impleted json part.");
            }
        }

        IActionResult DeleteJsonAction(JsonTemplateNames partName, IServiceDescriptorViewModel viewModel, string serviceJson)
        {
            IServiceDescriptorViewModel originalViewModel = RestoreAndUpdateService(viewModel, serviceJson);
            string serviceViewName;
            if (originalViewModel.ServiceType == ServiceType.NCWebService)
            {
                serviceViewName = nameof(EditNCWebService);
            }
            else if (originalViewModel.ServiceType == ServiceType.SimpleRESTful)
            {
                serviceViewName = nameof(EditSimpleRestfulService);
            }
            else
            {
                throw new Exception("not implemented");
            }
            if (partName == JsonTemplateNames.NCParameter)
            {
                ((NCDescriptorViewModel)originalViewModel).ParameterBody = null;
            }
            else if (partName == JsonTemplateNames.NCReturn)
            {
                ((NCDescriptorViewModel)originalViewModel).ReturnBody = null;
            }
            else if (partName == JsonTemplateNames.RESTParameter)
            {
                ((SimpleRestfulDescriptorViewModel)originalViewModel).JsonBodyTemplate = null;
                ((SimpleRestfulDescriptorViewModel)originalViewModel).SelectedTab = "nav-json";
            }
            else if (partName == JsonTemplateNames.RESTReturnValue)
            {
                ((SimpleRestfulDescriptorViewModel)originalViewModel).ReturnJsonTemplate = null;
            }
            TempData.Put(EDIT_SERVICE_CROSS_ACTION_DATA_KEY, originalViewModel);
            return RedirectToAction(serviceViewName);
        }

        IActionResult EditJsonAction(JsonTemplateNames partName, IServiceDescriptorViewModel viewModel, string serviceJson)
        {
            IServiceDescriptorViewModel originalViewModel = RestoreAndUpdateService(viewModel, serviceJson);
            //string errorKey = "";
            if (partName == JsonTemplateNames.NCParameter)
            {
                //errorKey = STATE_NCPARAMETER_ERROR;
                if (((NCDescriptorViewModel)originalViewModel).ParameterBody == null)
                {
                    ((NCDescriptorViewModel)originalViewModel).ParameterBody = new JsonTemplate();
                    ((NCDescriptorViewModel)originalViewModel).ParameterBody.ValueType = Models.ValueType.Object;
                }
            }
            else if (partName == JsonTemplateNames.NCReturn)
            {
                //errorKey = STATE_NCRETURN_ERROR;
                if (((NCDescriptorViewModel)originalViewModel).ReturnBody == null)
                {
                    ((NCDescriptorViewModel)originalViewModel).ReturnBody = new JsonTemplate();
                    ((NCDescriptorViewModel)originalViewModel).ReturnBody.ValueType = Models.ValueType.Object;
                }
            }
            else if (partName == JsonTemplateNames.RESTParameter)
            {
                //errorKey = STATE_RESTJSON_ERROR;
                if (((SimpleRestfulDescriptorViewModel)originalViewModel).JsonBodyTemplate == null)
                {
                    ((SimpleRestfulDescriptorViewModel)originalViewModel).JsonBodyTemplate = new JsonTemplate();
                    ((SimpleRestfulDescriptorViewModel)originalViewModel).JsonBodyTemplate.ValueType = Models.ValueType.Integer;
                }
            }
            else if (partName == JsonTemplateNames.RESTReturnValue)
            {
                //errorKey = STATE_RESTRETURN_ERROR;
                if (((SimpleRestfulDescriptorViewModel)originalViewModel).ReturnJsonTemplate == null)
                {
                    ((SimpleRestfulDescriptorViewModel)originalViewModel).ReturnJsonTemplate = new JsonTemplate();
                    ((SimpleRestfulDescriptorViewModel)originalViewModel).ReturnJsonTemplate.ValueType = Models.ValueType.Integer;
                }
            }
            EditServiceJson postedJson;
            if (originalViewModel.ServiceType == ServiceType.NCWebService)
            {
                postedJson = Models.EditServiceJson.CreateFrom((NCDescriptorViewModel)originalViewModel, partName);
            }
            else if (originalViewModel.ServiceType == ServiceType.SimpleRESTful)
            {
                postedJson = Models.EditServiceJson.CreateFrom((SimpleRestfulDescriptorViewModel)originalViewModel, partName);
            }
            else
            {
                throw new Exception("not implemented");
            }
            TempData.Put(EDIT_JSON_CROSS_ACTION_DATA_KEY, postedJson);
            return RedirectToAction(nameof(EditServiceJson));

            // Exception Catch Block (old)
            // TempData[errorKey] = ex.Message;
            // TempData.Put(EDIT_SERVICE_CROSS_ACTION_DATA_KEY, originalViewModel);
            // if(partName == JsonTemplateNames.RESTParameter)
            // {
            //     ((SimpleRestfulDescriptorViewModel)originalViewModel).SelectedTab = "nav-json";
            // }
            // string serviceViewName;
            // if(originalViewModel.ServiceType == ServiceType.NCWebService)
            // {
            //     serviceViewName = nameof(EditNCWebService);
            // }
            // else if(originalViewModel.ServiceType == ServiceType.SimpleRESTful)
            // {
            //     serviceViewName = nameof(EditSimpleRestfulService);
            // }
            // else
            // {
            //     throw new Exception("not implemented");
            // }
            // return RedirectToAction(serviceViewName);
        }

        public IActionResult EditServiceJson()
        {
            if (!TempData.ContainsKey(EDIT_JSON_CROSS_ACTION_DATA_KEY))
            {
                throw new Exception("资源不可以直接访问");
            }

            EditServiceJson postedJson = TempData.Get<Models.EditServiceJson>(EDIT_JSON_CROSS_ACTION_DATA_KEY);
            var model = new EditServiceJsonViewModel(postedJson);
            ViewBag.ErrorMessage = postedJson.ErrorMessage;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddProperty(EditServiceJsonViewModel viewModel)
        {
            var crossActionData = Models.EditServiceJson.CreateFrom(viewModel);
            var originalTemplate = JsonConvert.DeserializeObject<Models.JsonTemplate>(viewModel.CurrentJson);
            if (originalTemplate.ValueType != Models.ValueType.Object)
            {
                originalTemplate = new JsonTemplate();
                originalTemplate.ValueType = Models.ValueType.Object;
            }
            originalTemplate.IsArray = viewModel.CurrentTemplate.IsArray;

            if (string.IsNullOrWhiteSpace(viewModel.NewPropertyName))
            {
                crossActionData.ErrorMessage = "必须为JSON成员指定有效的属性名称";
            }
            else
            {
                var duplics = from key in originalTemplate.ObjectProperties.Keys
                              where key.Equals(viewModel.NewPropertyName.Trim(), StringComparison.OrdinalIgnoreCase)
                              select key;
                if (duplics.Count() > 0)
                {
                    crossActionData.ErrorMessage = "当前JSON对象已经包含了同名成员，属性名称是大小写不敏感的";
                }
            }
            if (string.IsNullOrWhiteSpace(crossActionData.ErrorMessage))
            {
                var newProperty = new JsonTemplate();
                newProperty.ValueType = viewModel.NewPropertyValue;
                newProperty.IsArray = viewModel.NewPropertyIsArray;
                originalTemplate.ObjectProperties.Add(viewModel.NewPropertyName, newProperty);
            }

            crossActionData.CurrentJson = JsonConvert.SerializeObject(originalTemplate);
            TempData.Put(EDIT_JSON_CROSS_ACTION_DATA_KEY, crossActionData);
            return RedirectToAction(nameof(EditServiceJson));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteProperty(EditServiceJsonViewModel viewModel, string propertyName)
        {
            var crossActionData = Models.EditServiceJson.CreateFrom(viewModel);
            var originalTemplate = JsonConvert.DeserializeObject<Models.JsonTemplate>(viewModel.CurrentJson);
            originalTemplate.IsArray = viewModel.CurrentTemplate.IsArray;

            if (originalTemplate.ValueType != Models.ValueType.Object)
            {
                crossActionData.ErrorMessage = "当前编辑的JSON模板不是对象类型";
            }
            else if (originalTemplate.ObjectProperties == null
                || !originalTemplate.ObjectProperties.ContainsKey(propertyName))
            {
                crossActionData.ErrorMessage = "JSON模板不包含要删除的属性成员";
            }
            if (string.IsNullOrWhiteSpace(crossActionData.ErrorMessage))
            {
                originalTemplate.ObjectProperties.Remove(propertyName);
            }

            crossActionData.CurrentJson = JsonConvert.SerializeObject(originalTemplate);
            TempData.Put(EDIT_JSON_CROSS_ACTION_DATA_KEY, crossActionData);
            return RedirectToAction(nameof(EditServiceJson));
        }

        JsonTemplate ProcessBeforeJump(ref EditServiceJson tempData, ref EditServiceJsonViewModel viewModel)
        {
            var processedTemplate = JsonConvert.DeserializeObject<Models.JsonTemplate>(viewModel.CurrentJson);
            bool uiChanged = false;
            if (processedTemplate.IsArray != viewModel.CurrentTemplate.IsArray)
            {
                //用户修改了IsArray。
                processedTemplate.IsArray = viewModel.CurrentTemplate.IsArray;
                uiChanged = true;
            }
            if (processedTemplate.ValueType != viewModel.CurrentTemplate.ValueType)
            {
                processedTemplate = new JsonTemplate();
                processedTemplate.ValueType = viewModel.CurrentTemplate.ValueType;
                processedTemplate.IsArray = viewModel.CurrentTemplate.IsArray;
                uiChanged = true;
            }
            if (uiChanged)
            {
                tempData.CurrentJson = JsonConvert.SerializeObject(processedTemplate);
            }
            return processedTemplate;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditProperty(EditServiceJsonViewModel viewModel, string propertyName)
        {
            var crossActionData = Models.EditServiceJson.CreateFrom(viewModel);
            var originalTemplate = ProcessBeforeJump(ref crossActionData, ref viewModel);

            if (originalTemplate.ValueType != Models.ValueType.Object)
            {
                crossActionData.ErrorMessage = "当前编辑的JSON模板不是对象类型";
            }
            else if (originalTemplate.ObjectProperties == null
                || !originalTemplate.ObjectProperties.ContainsKey(propertyName))
            {
                crossActionData.ErrorMessage = $"JSON模板不包含要编辑的属性成员{propertyName}";
            }
            if (string.IsNullOrWhiteSpace(crossActionData.ErrorMessage))
            {
                //Edit属性会导致CurrentJson整个对象发生改变，也就是CurrentPath发生改变，
                //  因此需要首先将当前的CurrentJson更新到WholeJson
                crossActionData.UpdateWholeJson();
                crossActionData.CurrentPath = string.IsNullOrWhiteSpace(crossActionData.CurrentPath) ?
                    propertyName : crossActionData.CurrentPath + '.' + propertyName;
                crossActionData.CurrentJson = JsonConvert.SerializeObject(originalTemplate.ObjectProperties[propertyName]);
            }

            TempData.Put(EDIT_JSON_CROSS_ACTION_DATA_KEY, crossActionData);
            return RedirectToAction(nameof(EditServiceJson));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveJson(EditServiceJsonViewModel viewModel)
        {
            var crossActionData = Models.EditServiceJson.CreateFrom(viewModel);
            ProcessBeforeJump(ref crossActionData, ref viewModel);

            crossActionData.UpdateWholeJson();

            TempData.Put(EDIT_JSON_CROSS_ACTION_DATA_KEY, crossActionData);
            TempData[nameof(SaveJson)] = nameof(SaveJson);
            return RedirectToAction(nameof(EditServiceJson));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveJsonAndGoBack(EditServiceJsonViewModel viewModel)
        {
            var crossActionData = Models.EditServiceJson.CreateFrom(viewModel);
            ProcessBeforeJump(ref crossActionData, ref viewModel);
            crossActionData.UpdateWholeJson();
            if (!string.IsNullOrWhiteSpace(crossActionData.CurrentPath))
            {
                object wholeDescriptor = crossActionData.DeserializeServiceDescriptor();
                int lastIndexOfDot = crossActionData.CurrentPath.LastIndexOf('.');
                if (lastIndexOfDot < 0)
                {
                    //返回到了根
                    crossActionData.CurrentPath = string.Empty;
                    switch (crossActionData.CurrentName)
                    {
                        case JsonTemplateNames.RESTParameter:
                            crossActionData.CurrentJson = JsonConvert.SerializeObject(
                                ((SimpleRestfulDescriptorViewModel)wholeDescriptor).JsonBodyTemplate);
                            break;
                        case JsonTemplateNames.RESTReturnValue:
                            crossActionData.CurrentJson = JsonConvert.SerializeObject(
                                ((SimpleRestfulDescriptorViewModel)wholeDescriptor).ReturnJsonTemplate);
                            break;
                        case JsonTemplateNames.NCParameter:
                            crossActionData.CurrentJson = JsonConvert.SerializeObject(
                                ((NCDescriptorViewModel)wholeDescriptor).ParameterBody);
                            break;
                        case JsonTemplateNames.NCReturn:
                            crossActionData.CurrentJson = JsonConvert.SerializeObject(
                                ((NCDescriptorViewModel)wholeDescriptor).ReturnBody);
                            break;
                        default:
                            throw new Exception("not implemented json part for this service");
                    }
                }
                else
                {
                    //返回到上一级
                    crossActionData.CurrentPath = crossActionData.CurrentPath.Substring(0, lastIndexOfDot);
                    switch (crossActionData.CurrentName)
                    {
                        case JsonTemplateNames.RESTParameter:
                            crossActionData.CurrentJson = JsonConvert.SerializeObject(
                                ((SimpleRestfulDescriptorViewModel)wholeDescriptor)
                                .JsonBodyTemplate.FindTemplate(crossActionData.CurrentPath));
                            break;
                        case JsonTemplateNames.RESTReturnValue:
                            crossActionData.CurrentJson = JsonConvert.SerializeObject(
                                ((SimpleRestfulDescriptorViewModel)wholeDescriptor)
                                .ReturnJsonTemplate.FindTemplate(crossActionData.CurrentPath));
                            break;
                        case JsonTemplateNames.NCParameter:
                            crossActionData.CurrentJson = JsonConvert.SerializeObject(
                                ((NCDescriptorViewModel)wholeDescriptor)
                                .ParameterBody.FindTemplate(crossActionData.CurrentPath));
                            break;
                        case JsonTemplateNames.NCReturn:
                            crossActionData.CurrentJson = JsonConvert.SerializeObject(
                                ((NCDescriptorViewModel)wholeDescriptor)
                                .ReturnBody.FindTemplate(crossActionData.CurrentPath));
                            break;
                        default:
                            throw new Exception("not implemented json part for this service");
                    }
                }
            }
            TempData.Put(EDIT_JSON_CROSS_ACTION_DATA_KEY, crossActionData);
            return RedirectToAction(nameof(EditServiceJson));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveJsonAndReturnService(EditServiceJsonViewModel viewModel)
        {
            var crossActionData = Models.EditServiceJson.CreateFrom(viewModel);
            ProcessBeforeJump(ref crossActionData, ref viewModel);
            crossActionData.UpdateWholeJson();
            object serviceDescriptor = crossActionData.DeserializeServiceDescriptor();
            if (crossActionData.ServiceType == ServiceType.SimpleRESTful)
            {
                ((SimpleRestfulDescriptorViewModel)serviceDescriptor).SelectedTab = "nav-json";
                TempData.Put(EDIT_SERVICE_CROSS_ACTION_DATA_KEY, serviceDescriptor);
                return RedirectToAction(nameof(EditSimpleRestfulService));
            }
            else if (crossActionData.ServiceType == ServiceType.NCWebService)
            {
                TempData.Put(EDIT_SERVICE_CROSS_ACTION_DATA_KEY, serviceDescriptor);
                return RedirectToAction(nameof(EditNCWebService));
            }
            else
            {
                throw new Exception("not implemented service type");
            }
        }

        #endregion
    }
}