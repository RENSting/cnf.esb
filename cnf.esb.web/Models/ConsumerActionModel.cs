using System;
using System.ComponentModel.DataAnnotations;

namespace cnf.esb.web.Models
{
    /// <summary>
    /// 用于传递针对客户进行操作的ViewModel
    /// </summary>
    public class ConsumerActionModel
    {
        public enum ActionEnum
        {
            Startup=1, Disable=2, Delete=3, ResetToken=4
        }

        public int ConsumerID { get; set; }

        [Display(Name="操作")]
        public ActionEnum Action { get; set; }

        [Display(Name="操作内容描述")]
        public string ActionDescription{get;set;}

    }
}