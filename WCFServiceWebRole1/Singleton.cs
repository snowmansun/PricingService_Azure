using PricingApi;
using PricingApiData;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace WCFServiceWebRole1
{
    public class Singleton
    {
        private volatile static ModelController _instance = null;
        private volatile static Model _instanceM = null;
        private static readonly object lockHelper = new object();
        private Singleton() { }
        public static ModelController CreateModelControllerInstance()
        {
            if (_instance == null)
            {
                lock (lockHelper)
                {
                    if (_instance == null)
                    {
                        _instance = new ModelController(
                         ConfigurationManager.AppSettings["provider"],
                         ConfigurationManager.AppSettings["connstring"],
                         "yyyy-MM-dd");
                        _instance.CustomerExtraFields = "CCC_DISC_IND,CCC_ROUNDRLS,ALAND/TAXK1";
                        _instance.ProductExtraFields = "MTART,SCL_RET";

                    }
                }
            }
            return _instance;
        }

        public static Model CreateModelInstance()
        {
            if (_instanceM == null)
            {
                lock (lockHelper)
                {
                    if (_instanceM == null)
                    {
                        _instanceM = new Model();

                    }
                }
            }
            return _instanceM;

        }
    }
}