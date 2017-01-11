using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace WCFServiceWebRole1
{
    [DataContract]
    public class PricingCond
    {
        [DataMember]
        public string customerCode { get; set; }

        [DataMember]
        public string calcDate { get; set; }

        [DataMember]
        public string recordRef { get; set; }

        [DataMember]
        public List<Products> products { get; set; }
    }
}