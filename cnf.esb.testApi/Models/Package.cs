using System;
using System.Collections.Generic;

namespace cnf.esb.testApi.Models
{
    public class Organization
    {
        public string orgtype{get;set;}
        public string unique_id{get;set;}
        public string orderno{get;set;}
        public string orgname{get;set;}
        public string orgcode{get;set;}
        public DateTime updatetime{get;set;}
        public string parentorgcode{get;set;}
        public string orglevel{get;set;}
        public string status{get;set;}
        public string D010F{get;set;} 

    }
    public class Package
    {
        public List<Organization> data{get;set;}
    }

    public class ReturnObject
    {
        public bool success {get;set;}
        public string msg {get;set;}
        public dynamic data{get;set;}
    }
}