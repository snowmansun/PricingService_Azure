using PricingApi;
using PricingApiData;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.ServiceModel.Web;
using System.Text;

namespace WCFServiceWebRole1
{
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class Pricing : IPricing
    {
      
        public CompositeType GetDataUsingDataContract(CompositeType composite)
        {
            if (composite == null)
            {
                throw new ArgumentNullException("composite");
            }
            if (composite.BoolValue)
            {
                composite.StringValue += "Suffix";
            }
            return composite;
        }

        public static Model model = null;
        public static ModelController modelController = null;
        public static CustomerCache customerCache = null;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pricingCond"></param>
        /// <returns></returns>
        public string CalcPricing(PricingCond pricingCond)
        {
            string jsonResult = string.Empty;
            try
            {
                string customerCode = pricingCond.customerCode;
                string date = pricingCond.calcDate;
                List<Products> productList = pricingCond.products;


                int keyid = Convert.ToInt32(ConfigurationManager.AppSettings["keyid"]);
                if (modelController == null)
                {
                    modelController = new ModelController(
                       ConfigurationManager.AppSettings["provider"],
                       ConfigurationManager.AppSettings["connstring"],
                       "yyyy-MM-dd");
                    modelController.CustomerExtraFields = "CCC_DISC_IND,CCC_ROUNDRLS,ALAND/TAXK1";
                    modelController.ProductExtraFields = "MTART,SCL_RET";

                    model = null;

                    if (model == null)
                    {
                        model = new Model();
                        modelController.LoadConfiguration(model);
                    }

                    modelController.LoadCustomerCache(model,
                         keyid,
                         DateTime.Today, Convert.ToDateTime("9999-01-01"));

                    modelController.LoadCustomer(model, "*", "*", customerCode,
                     DateTime.Today, Convert.ToDateTime("9999-01-01"));

                    // Get Customer (Cache contains single customer)
                    customerCache = model.CustomerCache[384852];
                }

                modelController.LoadProductPricingIndex(model,
                 keyid,
                 23695,
                DateTime.Today, Convert.ToDateTime("9999-01-01"));

                // Get Process
                PricingProcess process = model.Configuration.PricingProcesses[2];


                PricingDoc pricingDoc = new PricingDoc();
                pricingDoc.Customer = customerCache.Customers[23695];
                pricingDoc.PricingDate = Convert.ToDateTime(date);

                //json = @"ProductId":"0010651731","Quantity":1.04167,"UnitOfMeasure":"CS"},{"ProductId":"0010681531","Quantity":2.16667,"UnitOfMeasure":"CS"}";

                int i = 10;
                foreach (Products pdu in productList)
                {
                    PricingItem pricingItem = new PricingItem(pricingDoc, 10);
                    pricingItem.Product = pricingDoc.Customer.ProductsByRef[pdu.ProductId];
                    pricingItem.Uom = model.Configuration.UnitOfMeasure[pdu.UnitOfMeasure];
                    pricingItem.Quantity = pdu.Quantity;

                    pricingDoc.PricingItems.Add(i, pricingItem);
                    i += 10;
                }

                bool isAssort = modelController.GetAssortmentInfo(process, pricingDoc, model);

                // Execute Calculation
                process.ExecutePricing(pricingDoc, isAssort);


                string jsonHead = "\"Summary\": {";
                string jsonDetail = "\"Products\":[";

                foreach (var item in pricingDoc.Outputs)
                {
                    putJson(ref jsonHead, item.Value.PricingOutput.OutputName, item.Value.Amount != 0 ? item.Value.Amount.ToString("f" + item.Value.PricingOutput.Decimals) : item.Value.Amount.ToString());
                }
                decimal summary_freeGoodsQty = 0;

                foreach (var item in pricingDoc.PricingItems)
                {
                    jsonDetail += "{";
                    PricingItem pi = item.Value;
                    putJson(ref jsonDetail, "Key", item.Key.ToString());
                    putJson(ref jsonDetail, "ProductRef", pi.Product.ProductRef);
                    putJson(ref jsonDetail, "ProductId", pi.Product.ProductId.ToString());
                    putJson(ref jsonDetail, "ProductName", pi.Product.ProductDesc);
                    putJson(ref jsonDetail, "Uom", pi.Uom.Uom);
                    putJson(ref jsonDetail, "Quantity", pi.Quantity.ToString());
                    putJson(ref jsonDetail, "TotalQuantity", pi.TotalQuantity.ToString());

                    if (pi.ItemType == PricingItem.ITEM_TYPE_FREE_GOOD)
                    {
                        putJson(ref jsonDetail, "IsFreeGoods", "true");
                    }
                    else
                    {
                        putJson(ref jsonDetail, "IsFreeGoods", "false");
                    }

                    var listNormalMarkdtScopeCode = new Dictionary<string, string>();//本品
                    var listFreeMarkdtScopeCode = new Dictionary<string, string>();//赠品

                    string PriceListNo = string.Empty;
                    string SchemaNo = string.Empty;

                    foreach (var priceItem in pi.Outputs)
                    {
                        putJson(ref jsonDetail, priceItem.Value.PricingOutput.OutputName, priceItem.Value.Amount != 0 ? priceItem.Value.Amount.ToString("f" + priceItem.Value.PricingOutput.Decimals) : "0");

                        //2.PriceListNo
                        if (!string.IsNullOrEmpty(priceItem.Value.MarketScopeCode) &&
                            priceItem.Value.MarketScopeCode.Equals("21"))
                        {
                            PriceListNo = priceItem.Value.RecordRef.ToString();
                        }
                    }

                    decimal freeGoodQty = pi.ExclFreeGoodQuantity + pi.InclFreeGoodQuantity;
                    putJson(ref jsonDetail, "FreeGoodsQty", freeGoodQty.ToString());
                    putJson(ref jsonDetail, "IsInclFreeGoods", pi.InclFreeGoodQuantity != 0 ? "true" : "false");
                    jsonDetail = jsonDetail.TrimEnd(',');

                    summary_freeGoodsQty += freeGoodQty;
                    jsonDetail += "},";
                }
                putJson(ref jsonHead, "SummaryFreeGoodsQty", summary_freeGoodsQty.ToString());

                jsonHead = jsonHead.TrimEnd(',') + "},";
                jsonDetail = jsonDetail.TrimEnd(',') + "]";
                jsonResult = "{" + jsonHead + jsonDetail + "}";
            }catch(Exception ex)
            {
                return ex.Message;
            }

            return jsonResult;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="JsonScr"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        private void putJson(ref string JsonScr, string key, string value)
        {
            JsonScr += "\"" + key + "\":\"" + value + "\",";
        }

        public string GetData()
        {
            throw new NotImplementedException();
        }
    }
}
