using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cnf.esb.web.Models
{
    public enum EsbLogLevel
    {
        Message,
        Warning,
        Failure
    }

    public enum EsbOperation
    {
        Incoming,
        Checking,
        Preparation,
        Invoking,
        Respond
    }

    [Table("EsbLog")]
    public class EsbLog
    {
        [Display(Name="Unique Id")]
        public int ID{get;set;}

        [Display(Name="Task")]
        public string TaskIdentity{get;set;}

        
        [Display(Name="Log Level")]
        public EsbLogLevel LogLevel{get;set;}

        [Display(Name="API Instance")]
        public int InstanceID{get;set;}

        public EsbInstance Instance{get;set;}

        [Display(Name="Operaion Period")]
        public EsbOperation Operation{get;set;}

        [Display(Name="Incoming Call IP")]
        public string FromIP{get;set;}

        [Display(Name="Invoked URI")]
        public string InvokedUrl{get;set;}

        [Display(Name="Request Length")]
        public int RequestLength{get;set;}

        [Display(Name="Response Length")]
        public int ResponseLength{get;set;}

        [Display(Name="Message")]
        public string Message{get;set;}

        [Display(Name="Log Time")]
        public DateTime CreatedOn{get;set;}


        public static EsbLog Create(string taskIdentity, EsbLogLevel level, EsbOperation operation,
            string fromIp, int instanceId, string message, 
            string invokedUrl="", int inLength=0, int outLength=0)
        {
            var log = new EsbLog{
                TaskIdentity = taskIdentity,
                CreatedOn = DateTime.Now,
                FromIP = fromIp,
                InstanceID = instanceId,
                InvokedUrl = invokedUrl,
                LogLevel = level,
                Message = message,
                Operation = operation,
                RequestLength = inLength,
                ResponseLength = outLength
            };
            return log;
        }
    }
}