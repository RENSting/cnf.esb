using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;
using Newtonsoft.Json;

namespace cnf.esb.web.Models
{
    /// <summary>
    /// 不同于EditServiceJson，EditServiceJsonViewModel是视图模型。
    /// 它不用于在action之间传递数据，而是用来和视图执行模型绑定。
    /// 在每一个post操作中：
    /// 1. 通过模型绑定机制，EditServiceJsonViewModel从View中接收数据；
    /// 2. 使用修改后的新EditServiceJsonViewModel更新EditServiceJson对象；
    /// 3. 通过TempData传递到EditServiceJson，重新定向GET EditServiceJson操作。
    /// </summary>
    public class EditServiceJsonViewModel
    {
        //public ServiceType ServiceType{get;set;}
        public string ServiceDescriptor { get; set; }
        public JsonTemplateNames CurrentName{get;set;}
        public string CurrentPath { get; set; }
        public string CurrentJson { get; set; }
        public JsonTemplate CurrentTemplate { get; set; }
        public string NewPropertyName { get; set; }
        public ValueType NewPropertyValue { get; set; }
        public bool NewPropertyIsArray{get;set;}

        public EditServiceJsonViewModel() { }

        public EditServiceJsonViewModel(EditServiceJson serviceJson)
        {
            //ServiceType = serviceJson.ServiceType;
            ServiceDescriptor = serviceJson.ServiceDescriptor;
            CurrentName = serviceJson.CurrentName;
            CurrentPath = serviceJson.CurrentPath;
            CurrentJson = serviceJson.CurrentJson;
            if (string.IsNullOrWhiteSpace(serviceJson.CurrentJson))
            {
                CurrentTemplate = new JsonTemplate();
                CurrentTemplate.ValueType = ValueType.Integer;
            }
            else
            {
                CurrentTemplate = JsonConvert.DeserializeObject<JsonTemplate>(serviceJson.CurrentJson);
            }
        }
    }

    public enum JsonTemplateNames
    {
        [Display(Name="RESTful-API参数")]
        RESTParameter,
        [Display(Name="RESTful-API返回值")]
        RESTReturnValue,
        [Display(Name="NC-API参数")]
        NCParameter,
        [Display(Name="NC-API返回值")]
        NCReturn,
    }

    /// <summary>
    /// 这个对象用来在编辑服务定义的JSON模板时，在不同的action方法之间传递数据。
    /// </summary>
    public class EditServiceJson
    {
        public ServiceType ServiceType
        {
            get
            {
                switch (CurrentName)
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
        }
        /// <summary>
        /// 服务协定定义时使用的Service Descriptor ViewModel视图模型
        /// 用于编辑JSON模板过程中保存中间数据，返回服务定义编辑页面后用它更新服务定义。
        /// </summary>
        /// <value></value>
        public string ServiceDescriptor { get; set; }

        /// <summary>
        /// 正在编辑的是哪一个JSON模板
        /// </summary>
        /// <value></value>
        public JsonTemplateNames CurrentName{get;set;}

        /// <summary>
        /// 当前正在编辑器里编辑的JSON模板对象的路径。
        /// 使用路径可以从WholeJson中定位到正在编辑的JSON模板。
        /// </summary>
        /// <value></value>
        public string CurrentPath { get; set; }

        /// <summary>
        /// 当前正在编辑的JSON模板的序列化json串。
        /// 在中间过程中不断改变，并用来根据CurrentPath读取和写入WholeJson。
        /// </summary>
        /// <value></value>
        public string CurrentJson { get; set; }

        public string ErrorMessage { get; set; }

        /// <summary>
        /// 根据服务类型，将ServiceDescriptor反序列化成合适的Descriptor对象。
        /// </summary>
        /// <returns></returns>
        public object DeserializeServiceDescriptor()
        {
            if(ServiceType == ServiceType.SimpleRESTful)
            {
                return JsonConvert.DeserializeObject<SimpleRestfulDescriptorViewModel>(ServiceDescriptor);
            }
            else if(ServiceType == ServiceType.NCWebService)
            {
                return JsonConvert.DeserializeObject<NCDescriptorViewModel>(ServiceDescriptor);
            }
            else
            {
                throw new Exception("Not impleted service type:" + ServiceType.ToString());
            }
        }

        /// <summary>
        /// 更新WholeJson。更新WholeJson的原因如下：
        ///     Json模板是嵌套的，当CurrentJson是复杂类型包含成员的时候，为了编辑这些成员，
        /// Json模板编辑器页面被重用，但页面仅包含CurrentJson用于就地编辑。于是，整个
        /// CurrentPath路径上的其他上下文JSON如果不保存下来，就会丢失。
        ///     所以UpdateWholeJson方法应用的情形是，当CurrentPath发生改变的时候。
        /// </summary>
        public void UpdateWholeJson()
        {
            object originDescriptor = DeserializeServiceDescriptor();
            var originCurrent = JsonConvert.DeserializeObject<JsonTemplate>(CurrentJson);
            switch (CurrentName)
            {
                case JsonTemplateNames.RESTParameter:
                    if (string.IsNullOrWhiteSpace(CurrentPath))
                    {
                        ((SimpleRestfulDescriptorViewModel)originDescriptor).JsonBodyTemplate = originCurrent;
                    }
                    else
                    {
                        ((SimpleRestfulDescriptorViewModel)originDescriptor).JsonBodyTemplate.ReplaceWith(CurrentPath, originCurrent);
                    }
                    break;
                case JsonTemplateNames.RESTReturnValue:
                    if (string.IsNullOrWhiteSpace(CurrentPath))
                    {
                        ((SimpleRestfulDescriptorViewModel)originDescriptor).ReturnJsonTemplate = originCurrent;
                    }
                    else
                    {
                        ((SimpleRestfulDescriptorViewModel)originDescriptor).ReturnJsonTemplate.ReplaceWith(CurrentPath, originCurrent);
                    }
                    break;
                case JsonTemplateNames.NCParameter:
                    if (string.IsNullOrWhiteSpace(CurrentPath))
                    {
                        ((NCDescriptorViewModel)originDescriptor).ParameterBody = originCurrent;
                    }
                    else
                    {
                        ((NCDescriptorViewModel)originDescriptor).ParameterBody.ReplaceWith(CurrentPath, originCurrent);
                    }
                    break;
                case JsonTemplateNames.NCReturn:
                    if (string.IsNullOrWhiteSpace(CurrentPath))
                    {
                        ((NCDescriptorViewModel)originDescriptor).ReturnBody = originCurrent;
                    }
                    else
                    {
                        ((NCDescriptorViewModel)originDescriptor).ReturnBody.ReplaceWith(CurrentPath, originCurrent);
                    }
                    break;
                default:
                    throw new Exception("没有在该部位实现JSON模板");
            }
            ServiceDescriptor = JsonConvert.SerializeObject(originDescriptor);
        }

        public static EditServiceJson CreateFrom(EsbService service, JsonTemplateNames partName)
        {
            if(service.Type == ServiceType.SimpleRESTful)
            {
                return EditServiceJson.CreateFrom(SimpleRestfulDescriptorViewModel.CreateFrom(service), partName);
            }
            else if(service.Type == ServiceType.NCWebService)
            {
                return EditServiceJson.CreateFrom(NCDescriptorViewModel.CreateFrom(service), partName);
            }
            else
            {
                throw new Exception("not impleted service type");
            }
        }

        public static EditServiceJson CreateFrom(EditServiceJsonViewModel model)
        {
            var serviceJson = new EditServiceJson
            {
                ServiceDescriptor = model.ServiceDescriptor,
                CurrentName = model.CurrentName,
                CurrentPath = model.CurrentPath,
                CurrentJson = model.CurrentJson,
            };
            return serviceJson;
        }

        public static EditServiceJson CreateFrom(SimpleRestfulDescriptorViewModel model, JsonTemplateNames partName)
        {
            var serviceJson = new EditServiceJson
            {
                ServiceDescriptor = JsonConvert.SerializeObject(model),
                CurrentPath = "",
                CurrentName = partName
            };
            switch (partName)
            {
                case JsonTemplateNames.RESTParameter:
                    serviceJson.CurrentJson = JsonConvert.SerializeObject(model.JsonBodyTemplate);
                    break;
                case JsonTemplateNames.RESTReturnValue:
                    serviceJson.CurrentJson = JsonConvert.SerializeObject(model.ReturnJsonTemplate);
                    break;
                default:
                    throw new Exception("传入了非RESTful服务类型的部位参数"+ partName.ToString());
            }
            
            return serviceJson;
        }

        public static EditServiceJson CreateFrom(NCDescriptorViewModel model, JsonTemplateNames partName)
        {
            var serviceJson = new EditServiceJson
            {
                ServiceDescriptor = JsonConvert.SerializeObject(model),
                CurrentPath = "",
                CurrentName = partName
            };
            switch (partName)
            {
                case JsonTemplateNames.NCParameter:
                    serviceJson.CurrentJson = JsonConvert.SerializeObject(model.ParameterBody);
                    break;
                case JsonTemplateNames.NCReturn:
                    serviceJson.CurrentJson = JsonConvert.SerializeObject(model.ReturnBody);
                    break;
                default:
                    throw new Exception("传入了非NC系统Web服务类型的部位参数"+ partName.ToString());
            }
            
            return serviceJson;
        }
    }
}