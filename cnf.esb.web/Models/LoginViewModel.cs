using System.ComponentModel.DataAnnotations;

namespace cnf.esb.web.Models
{
    public class LoginViewModel
    {
        [Required, Display(Name="登录名")]
        public string UserName{get;set;}

        [DataType(DataType.Password), Required, Display(Name="口令")]
        public string Password{get;set;}
    }

    public class AdminSettings
    {
        public string UserName{get;set;}
        public string Password{get;set;}
    }
}