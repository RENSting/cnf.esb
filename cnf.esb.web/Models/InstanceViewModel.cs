using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace cnf.esb.web.Models
{
    public enum MappingType
    {
        [Display(Name = "固定值")]
        Constant,

        [Display(Name = "形式参数")]
        Path
    }

    public class ParameterMapping
    {
        [Display(Name="来源")]
        public string Source{get;set;}

        [Display(Name = "服务端参数路径")]
        public string ServerPath { get; set; }

        [Display(Name = "映射方式")]
        public MappingType MappingType { get; set; }

        [Display(Name = "映射模板")]
        public string ClientPath { get; set; }
    }

    /// <summary>
    /// 可以从EsbInstance直接隐式类型转换到本类型
    /// </summary>
    public class InstanceViewModel
    {
        [Display(Name = "ID")]
        public int InstanceID { get; set; }

        [Display(Name = "客户程序")]
        public int ClientID { get; set; }

        [Display(Name = "客户程序")]
        public string ClientName { get; set; }

        [Display(Name = "服务")]
        public int ServiceID { get; set; }

        [Display(Name = "服务名称")]
        public string ServiceName { get; set; }

        [Required, Display(Name = "实例名称")]
        public string InstanceName { get; set; }

        [Display(Name = "详细说明")]
        public string Description { get; set; }

        [Display(Name = "已启用")]
        public bool ActiveStatus { get; set; }

        [Display(Name = "创建时间")]
        public DateTime CreatedOn { get; set; }

        public List<ParameterMapping> ParameterMappings { get; set; }

        public static IEnumerable<InstanceViewModel> ConvertFrom(IEnumerable<EsbInstance> instances)
        {
            List<InstanceViewModel> list = new List<InstanceViewModel>();
            foreach (var instance in instances)
            {
                InstanceViewModel viewModel = instance;
                list.Add(viewModel);
            }
            return list;
        }
        public static implicit operator InstanceViewModel(EsbInstance instance)
        {
            var instanceViewModel = new InstanceViewModel();

            instanceViewModel.ActiveStatus = instance.ActiveStatus != 0;
            instanceViewModel.ClientID = instance.ClientID;
            instanceViewModel.ServiceID = instance.ServiceID;
            instanceViewModel.CreatedOn = instance.CreatedOn;
            instanceViewModel.Description = instance.Description;
            instanceViewModel.InstanceID = instance.ID;
            instanceViewModel.InstanceName = instance.Name;
            if (instance.Client != null)
            {
                instanceViewModel.ClientName = instance.Client.Name;
            }
            if (instance.Service != null)
            {
                instanceViewModel.ServiceName = instance.Service.Name;
            }
            if (instance.InstanceMapping != null)
            {
                if (string.IsNullOrWhiteSpace(instance.InstanceMapping.ParameterMappings))
                {
                    instanceViewModel.ParameterMappings = new List<ParameterMapping>();
                }
                else
                {
                    instanceViewModel.ParameterMappings = JsonConvert
                        .DeserializeObject<List<ParameterMapping>>(
                            instance.InstanceMapping.ParameterMappings);
                }
            }
            else
            {
                instanceViewModel.ParameterMappings = new List<ParameterMapping>();
            }
            return instanceViewModel;
        }
    }
}