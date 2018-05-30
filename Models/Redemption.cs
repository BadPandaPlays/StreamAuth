using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace StreamAuth.Models
{
    //http://json2csharp.com/#
    [JsonObject]
    public class Redeemer
    {
        public string _id { get; set; }
        public string username { get; set; }
        public string avatar { get; set; }
        public bool inactive { get; set; }
    }
    [JsonObject]
    public class Doc
    {
        public string _id { get; set; }
        public DateTime updatedAt { get; set; }
        public DateTime createdAt { get; set; }
        public string channel { get; set; }
        public Redeemer redeemer { get; set; }
        public object item { get; set; }
        public List<object> input { get; set; }
        public bool completed { get; set; }
        public string redeemerType { get; set; }
    }
    [JsonObject]
    public class RootObject
    {
        public int _total { get; set; }
        public List<Doc> docs { get; set; }
    }
    [JsonObject]
    public class complete
    {
        public bool Complete { get; set; }
    }
}
