using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace cnf.esb.web.Models
{
    public enum ServiceType
    {
        [Display(Name="简单RESTful")]
        SimpleRESTful
    }

    [Table("EsbService")]
    public class EsbService
    {
        [Display(Name="ID")]
        public int ID{get;set;}

        [Required, MaxLength(50)]
        [Display(Name="服务名称")]
        public string Name{get;set;}

        [Display(Name="分组")]
        public string GroupName{get;set;}

        [Display(Name="服务说明")]
        public string FullDescription{get;set;}

        /// <summary>
        /// 接口类型决定了服务协定的格式
        /// </summary>
        /// <value></value>
        [Required, Display(Name="接口类型")]
        public ServiceType Type{get;set;}

        /// <summary>
        /// JSON序列化的API接口协议，包括URI， 输入参数， 返回值等
        /// </summary>
        /// <value></value>
        [Display(Name="服务协定")]
        public string ServiceDescriptor{get;set;}

        [Display(Name="创建日期")]
        [DataType(DataType.Date)]
        public DateTime CreatedOn{get;set;}

        [Display(Name="服务状态")]
        public int ActiveStatus{get;set;}

        public ICollection<EsbInstance> Instances{get;set;}
    }

    public class EditServiceViewModel
    {
        public int ServiceID{get;set;}

        [Required, MaxLength(50)]
        [Display(Name="服务名称")]
        public string Name{get;set;}

        [Display(Name="分组")]
        public string GroupName{get;set;}

        [Display(Name="服务说明")]
        public string FullDescription{get;set;}

        /// <summary>
        /// 接口类型决定了服务协定的格式
        /// </summary>
        /// <value></value>
        [Required, Display(Name="接口类型")]
        public ServiceType Type{get;set;}

        [Display(Name="服务状态")]
        public bool ActiveStatus{get;set;}

        public static implicit operator EditServiceViewModel(EsbService service)
        {
            return new EditServiceViewModel{
                ActiveStatus = service.ActiveStatus != 0,
                FullDescription = service.FullDescription,
                GroupName = service.GroupName,
                Name = service.Name,
                ServiceID = service.ID,
                Type = service.Type
            };
        }
    }

    public enum HttpMethod
    {
        GET, POST
    }
}