using PricingApi;
using PricingApiData;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.ServiceModel.Web;
using System.Text;
using System.Web.Script.Serialization;

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

        #region 变量定义
        public static Model model = null;
        public static ModelController modelController = null;
        public static CustomerCache customerCache = null;
        #endregion

        #region 接口方法
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pricingCond"></param>
        /// <returns></returns>
        public string CalcPricing(PricingCond pricingCond)
        {
            string debug = "";
            string jsonResult = string.Empty;
            try
            {
                string customerCode = pricingCond.customerCode;
                string date = pricingCond.calcDate;
                List<Products> productList = pricingCond.products;

                int keyid = Convert.ToInt32(ConfigurationManager.AppSettings["keyid"]);
                debug = "1";
                if (modelController == null)
                {
                    debug = "1-1";
                    //modelController = new ModelController(
                    //   ConfigurationManager.AppSettings["provider"],
                    //   ConfigurationManager.AppSettings["connstring"],
                    //   "yyyy-MM-dd");
                    //modelController.CustomerExtraFields = "CCC_DISC_IND,CCC_ROUNDRLS,ALAND/TAXK1";
                    //modelController.ProductExtraFields = "MTART,SCL_RET";

                    modelController = Singleton.CreateModelControllerInstance();
                    debug = "2";
                    //model = new Model();
                    model = Singleton.CreateModelInstance();
                    modelController.LoadConfiguration(model);
                    debug = "3";
                    modelController.LoadCustomerCache(model, keyid,
                         DateTime.Today, Convert.ToDateTime("9999-01-01"));
                    debug = "4";
                    // Get Customer (Cache contains single customer)
                    customerCache = model.CustomerCache[keyid];
                    debug = "5";
                }
                debug = "1-2";
                modelController.LoadCustomer(model, "*", "*", customerCode,
                       DateTime.Today, Convert.ToDateTime("9999-01-01"));
                debug = "6";
                modelController.LoadProductPricingIndex(model, keyid, customerCode,
                    DateTime.Today, Convert.ToDateTime("9999-01-01"));
                debug = "7";
                // Get Process
                PricingProcess process = model.Configuration.PricingProcesses[2];

                //
                PricingDoc pricingDoc = new PricingDoc();
                pricingDoc.Customer = customerCache.CustomersByRef[customerCode];
                pricingDoc.PricingDate = Convert.ToDateTime(date);
                debug = "8";
                //
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
                debug = "9";
                bool isAssort = modelController.GetAssortmentInfo(process, pricingDoc, model);
                debug = "10";
                // Execute Calculation
                process.ExecutePricing(pricingDoc, isAssort);
                debug = "11";

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
                modelController = null;
                return ex.Message + " no" + debug;
            }
            

            return jsonResult;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="customerCode"></param>
        /// <returns></returns>
        public string GetComboList(PricingCond pricingCond)
        {
            if (modelController == null)
            {
                modelController = new ModelController(
                   ConfigurationManager.AppSettings["provider"],
                   ConfigurationManager.AppSettings["connstring"],
                   "yyyy-MM-dd");
            }

            string comboListJson = string.Empty;
            DataTable dtA = modelController.GetComboList(pricingCond.customerCode);
            comboListJson = Dtb2Json(dtA);
            return comboListJson;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pricingCond"></param>
        /// <returns></returns>
        public string GetComboDetail(PricingCond pricingCond)
        {
            if (modelController == null)
            {
                modelController = new ModelController(
                   ConfigurationManager.AppSettings["provider"],
                   ConfigurationManager.AppSettings["connstring"],
                   "yyyy-MM-dd");
            }

            string comboDetailJson = string.Empty;
            DataTable dtA = modelController.GetComboListDetail(pricingCond.recordRef);
            comboDetailJson = Dtb2Json(dtA);
            return comboDetailJson;
        }

        /// <summary>
        /// GetBasePriceInfoByCusProd
        /// </summary>
        /// <param name="customerCode"></param>
        /// <param name="products"></param>
        /// <returns></returns>
        public string GetBasePriceInfoByCusProd(PricingCond pricingCond)
        {
            string customerCode = pricingCond.customerCode;
            List<Products> products = pricingCond.products;
            // select all group data
            DataSet dtPrice = new DataSet();
            string sqlPrice = @"
                                            SELECT * FROM 
                                            (
                                            SELECT  dense_rank()over (partition by productref ORDER BY ss.Sequence) num
                                                    ,pr.StepTypeId
                                                    ,RecordUom
                                                    ,CustomerKeyId
                                                    ,ProductKeyId
            		                                ,p.productref
                                                    ,Value
                                            FROM PricingRecord pr 
                                                    JOIN CustomerKey ck ON pr.CustomerKeyId=ck.KeyId
            		                                JOIN CustomerKeyValue ckv ON pr.CustomerKeyId=ckv.KeyId
                                                    JOIN ProductKey pk ON pr.ProductKeyId=pk.KeyId
                                                    JOIN Customer c ON ck.CustomerId=c.CustomerId
                                                    JOIN Product p ON pk.ProductId=p.ProductId
                                                    JOIN StepType s on s.StepTypeId = pr.StepTypeId
            		                                JOIN StepTypeSequence ss ON ss.StepTypeId =pr.StepTypeId AND ss.CustomerKeyTypeId = ckv.TypeId
                                            WHERE c.CustomerRef= '{0}'
                                              AND pr.RecordRef <> -1 and pr.ActiveFlag = 1
            		                            --and productref in ('{1}')
            		                            and s.StepTypeId = 93
                                              and DateFrom <= GETDATE()
            		                            and DateTo >= GETDATE()
                                            ) a 
                                            WHERE a.num=1";

            //string sqlPrice = @"SELECT  
            //                      pr.StepTypeId
            //                      ,RecordUom
            //                      ,CustomerKeyId
            //                      ,ProductKeyId
            //                      ,p.productref
            //                      ,Value
            //                    FROM PricingRecord pr 
            //                            JOIN CustomerKey ck ON pr.CustomerKeyId=ck.KeyId
            //                      JOIN CustomerKeyValue ckv ON pr.CustomerKeyId=ckv.KeyId
            //                            JOIN ProductKey pk ON pr.ProductKeyId=pk.KeyId
            //                            JOIN Customer c ON ck.CustomerId=c.CustomerId
            //                            JOIN Product p ON pk.ProductId=p.ProductId
            //                            JOIN StepType s on s.StepTypeId = pr.StepTypeId
            //                      JOIN StepTypeSequence ss ON ss.StepTypeId =pr.StepTypeId AND ss.[CustomerKeyTypeId] = ckv.TypeId
            //                      JOIN (
            //                     SELECT  p.productref,min(ss.Sequence) MinSequence
            //                    FROM PricingRecord pr 
            //                            JOIN CustomerKey ck ON pr.CustomerKeyId=ck.KeyId
            //                      JOIN CustomerKeyValue ckv ON pr.CustomerKeyId=ckv.KeyId
            //                            JOIN ProductKey pk ON pr.ProductKeyId=pk.KeyId
            //                            JOIN Customer c ON ck.CustomerId=c.CustomerId
            //                            JOIN Product p ON pk.ProductId=p.ProductId
            //                            JOIN StepType s on s.StepTypeId = pr.StepTypeId
            //                      JOIN StepTypeSequence ss ON ss.StepTypeId =pr.StepTypeId AND ss.[CustomerKeyTypeId] = ckv.TypeId
            //                    WHERE c.CustomerRef='{0}'
            //                            AND pr.RecordRef <> -1 and pr.ActiveFlag = 1
            //                      {1}
            //                      and s.StepTypeId = 93
            //                    group by productref
            //                    ) tt on tt.productref=p.ProductRef and tt.MinSequence=ss.Sequence 
            //                    WHERE c.CustomerRef='{0}'
            //                            AND pr.RecordRef <> -1 and pr.ActiveFlag = 1
            //                      {1}
            //                      and s.StepTypeId = 93
            //";

            string strProduct = string.Empty;
            foreach (Products p in products)
            {
                strProduct += ("'" + p.ProductId + "',");
            }
            if (strProduct != string.Empty) sqlPrice = string.Format(sqlPrice, customerCode, " and p.productref in ( " + strProduct.TrimEnd(',') + ")");
            else sqlPrice = string.Format(sqlPrice, customerCode, " ");

            if (modelController == null)
            {
                modelController = new ModelController(
                   ConfigurationManager.AppSettings["provider"],
                   ConfigurationManager.AppSettings["connstring"],
                   "yyyy-MM-dd");
            }
            modelController.LoadData(dtPrice, "PricingRecord", sqlPrice);
            string basePriceJson = string.Empty;
            if (dtPrice.Tables[0].Rows.Count > 0)
                basePriceJson = Dtb2Json(dtPrice.Tables[0]);

            return basePriceJson;
        }

        /// <summary>
        /// GetBasePriceInfoByCusProd
        /// </summary>
        /// <param name="customerCode"></param>
        /// <param name="products"></param>
        /// <returns></returns>
        public string GetPromInfoByCusProd(PricingCond pricingCond)
        {
            DataSet dtProm = new DataSet();
            string returnJson = string.Empty;
            string sqlProm = @"SELECT distinct p.productref
                              FROM PricingRecord pr 
                              JOIN CustomerKey ck ON pr.CustomerKeyId=ck.KeyId
                              JOIN ProductKey pk ON pr.ProductKeyId=pk.KeyId
                              JOIN Customer c ON ck.CustomerId=c.CustomerId
                              JOIN Product p ON pk.ProductId=p.ProductId
                              JOIN StepType s on s.StepTypeId = pr.StepTypeId
                              WHERE c.CustomerRef='{0}'
                              AND pr.RecordRef<>-1
                              AND s.UserExitName like '%Prom=S%'
                              AND datefrom <= '{1}' 
							  AND dateto >= '{1}' ";

            modelController.LoadData(dtProm, "PricingRecord", string.Format(sqlProm, pricingCond.customerCode, pricingCond.deliverDay));

            if (dtProm.Tables[0].Rows.Count > 0) returnJson = Dtb2Json(dtProm.Tables[0]);

            return returnJson;
        }

        // update by chen 
        /// <summary>
        ///  Get Prom with prom by customercode
        /// </summary>
        /// <param name="process"></param>
        /// <param name="pricingDoc"></param>
        /// <returns></returns>
        public string GetPromationDetailSingle(PricingCond pricingCond)
        {
            DataSet dtPromDetail = new DataSet();

            string sqlProm = @"SELECT distinct 
                                             pr.RecordRef, p.productref,s.UserExitName as UserExitName_S
                                            ,pr.StepTypeId
                                            ,RecordUom
                                            ,CustomerKeyId
                                            ,ProductKeyId
                                            ,DateFrom
                                            ,DateTo
                                            ,MinQtyUom
                                            ,MinQty
                                            ,MaxQty
                                            ,CalcType
                                            ,Percentage
                                            ,ValuePerQty
                                            ,ValueUom
                                            ,Value
                                            ,MinValue
                                            ,MaxValue
                                            ,FgType
                                            ,FgCalcType
                                            ,FgCalcQty
                                            ,FgFreeQty
                                            ,FgMaxFreeQty
                                            ,ConditionLogic
                                            ,ActiveFlag
                                            ,GroupRef
                                            ,pr.UserExitName, '' as promDesc
                                      FROM PricingRecord pr 
                                      JOIN CustomerKey ck ON pr.CustomerKeyId=ck.KeyId
                                      JOIN ProductKey pk ON pr.ProductKeyId=pk.KeyId
                                      JOIN Customer c ON ck.CustomerId=c.CustomerId
                                      JOIN Product p ON pk.ProductId=p.ProductId
                                      JOIN StepType s on s.StepTypeId = pr.StepTypeId
                                      WHERE c.CustomerRef='{0}'
                                      AND pr.RecordRef <> -1 and pr.ActiveFlag = 1
                                      AND s.UserExitName like '%Prom=S%'
                                      AND datefrom <= '{1}' 
							          AND dateto >= '{1}' 
                                      AND Groupref = ''
                                      AND productref  = '{2}'";

            if (modelController == null)
            {
                modelController = new ModelController(
                   ConfigurationManager.AppSettings["provider"],
                   ConfigurationManager.AppSettings["connstring"],
                   "yyyy-MM-dd");
            }
            modelController.LoadData(dtPromDetail, "PricingRecord", string.Format(sqlProm, pricingCond.customerCode, pricingCond.deliverDay, pricingCond.productCode));

            //foreach (DataColumn col in dtPromDetail.Tables[0].Columns)
            //{
            //    if (col.ColumnName == "DateFrom" || col.ColumnName == "DateTo")
            //    {
            //        //修改列类型
            //        col.DataType = typeof(DateTime);
            //    }
            //}

            foreach (DataRow dr in dtPromDetail.Tables[0].Rows)
            {
                string userExitName = dr["UserExitName_S"].ToString();
                string promDesc = string.Empty;
                if (userExitName.IndexOf(';') != -1)
                    promDesc = userExitName.Split(';')[1];
                else
                    promDesc = userExitName;
                if (dr["CalcType"].ToString() == "AMNT")
                {
                    promDesc = string.Format(promDesc, dr["ValuePerQty"].ToString() + " " + dr["RecordUom"].ToString(), dr["Value"].ToString());
                }
                else if (dr["CalcType"].ToString() == "PERC" || dr["CalcType"].ToString() == "PINC")
                {
                    promDesc = string.Format(promDesc, dr["ValuePerQty"].ToString() + " " + dr["RecordUom"].ToString(), dr["Percentage"].ToString());
                }
                else if (dr["CalcType"].ToString() == "FEEE")
                {
                    promDesc = string.Format(promDesc, dr["FgCalcQty"].ToString() + " " + pricingCond.productCode, dr["FgCalcQty"].ToString() + " " + pricingCond.productCode);
                }
                dr["promDesc"] = promDesc.TrimStart("Desc=".ToCharArray());
            }

            return Dtb2Json(dtPromDetail.Tables[0]);
        }

        // update by chen 
        /// <summary>
        ///  Get Prom with prom by customercode
        /// </summary>
        /// <param name="process"></param>
        /// <param name="pricingDoc"></param>
        /// <returns></returns>
        public string GetPromationDetailAssort(PricingCond pricingCond)
        {
            DataSet dtPromID = new DataSet();

            string sqlProm = @"SELECT distinct recordref
                              FROM PricingRecord pr 
                              JOIN CustomerKey ck ON pr.CustomerKeyId=ck.KeyId
                              JOIN ProductKey pk ON pr.ProductKeyId=pk.KeyId
                              JOIN Customer c ON ck.CustomerId=c.CustomerId
                              JOIN Product p ON pk.ProductId=p.ProductId
                              JOIN StepType s on s.StepTypeId = pr.StepTypeId
                              WHERE c.CustomerRef='{0}'
                              AND productref = '{2}'
                              AND pr.RecordRef <> -1 AND pr.ActiveFlag = 1
                              AND s.UserExitName like '%Prom=S%'
                              AND Datefrom <= '{1}' 
							  AND Dateto >= '{1}' 
                              AND Groupref <> ''
                              AND CalcType = 'ATOT'";

            if (modelController == null)
            {
                modelController = new ModelController(
                   ConfigurationManager.AppSettings["provider"],
                   ConfigurationManager.AppSettings["connstring"],
                   "yyyy-MM-dd");
            }
            modelController.LoadData(dtPromID, "PricingRecord", string.Format(sqlProm, pricingCond.customerCode, pricingCond.deliverDay, pricingCond.productCode));

            DataSet dtReturn = new DataSet();
            foreach (DataRow dr in dtPromID.Tables[0].Rows)
            {
                DataTable dtAssortmentDesc = new DataTable();
                dtAssortmentDesc.Columns.Add("RecordRef");
                dtAssortmentDesc.Columns.Add("AssortType");
                dtAssortmentDesc.Columns.Add("AssortProducts");
                dtAssortmentDesc.Columns.Add("AssortDesc");
                dtAssortmentDesc.Columns.Add("Qty");
                dtAssortmentDesc.Columns.Add("Uom");
                dtAssortmentDesc.Columns.Add("PromDesc");
                dtAssortmentDesc.Columns.Add("DateFrom");
                dtAssortmentDesc.Columns.Add("DateTo");
                DataSet dtAssort = new DataSet();
                string sqlAssort = string.Format(@"SELECT distinct p.ProductRef, pr.*
                                                    FROM PricingRecord pr 
                                                    JOIN CustomerKey ck ON pr.CustomerKeyId=ck.KeyId
                                                    JOIN ProductKey pk ON pr.ProductKeyId=pk.KeyId
                                                    JOIN Customer c ON ck.CustomerId=c.CustomerId
                                                    JOIN Product p ON pk.ProductId=p.ProductId
                                                    JOIN StepType s on s.StepTypeId = pr.StepTypeId
                                                    WHERE recordref = '{0}'
                                                    ORDER BY RecordId", dr["recordref"].ToString());

                modelController.LoadData(dtAssort, "PricingRecord", sqlAssort);
                string oldRecordid = string.Empty;
                string productRef = string.Empty;
                int index = 1;
                foreach (DataRow drAssort in dtAssort.Tables[0].Rows)
                {
                    string recordId = drAssort["RecordId"].ToString();
                    productRef = drAssort["ProductRef"].ToString();
                    string scaleData = "";
                    string qty = "";
                    string promDesc = "";
                    string[] userExitNameM = (drAssort["UserExitName"] + "").Split(';');
                    foreach (string keyvalue in userExitNameM)
                    {
                        string[] type = keyvalue.Split('=');
                        if (type[0] == "AS_RATIO")
                        {
                            qty = type[1];
                            continue;
                        }
                        if (type[0] == "AS_DESC")
                        {
                            promDesc = type[1];
                            continue;
                        }
                        if (type[0] == "SC_DATA")
                        {
                            scaleData = type[1];
                            continue;
                        }
                    }

                    if (recordId != oldRecordid)
                    {
                        DataRow drNew = dtAssortmentDesc.NewRow();
                        drNew["RecordRef"] = drAssort["RecordRef"].ToString();
                        //
                        if (drAssort["CalcType"].ToString() == "ATOT")
                        {
                            drNew["AssortType"] = "REQU";
                        }
                        else
                        {
                            drNew["AssortType"] = "REWA";
                        }
                        drNew["AssortProducts"] = productRef.TrimEnd(',');
                        // 
                        if (scaleData != string.Empty)
                            drNew["AssortDesc"] = scaleData + " " + drNew["AssortProducts"];
                        else
                            drNew["AssortDesc"] = qty + drAssort["RecordUom"] + " " + productRef.TrimEnd(',');
                        drNew["Qty"] = qty;
                        drNew["Uom"] = drAssort["RecordUom"] + "";
                        drNew["PromDesc"] = promDesc;
                        drNew["DateFrom"] = drAssort["DateFrom"] + "";
                        drNew["DateTo"] = drAssort["DateTo"] + "";

                        dtAssortmentDesc.Rows.Add(drNew);
                        productRef = string.Empty;
                    }
                    else
                    {
                        dtAssortmentDesc.Rows[dtAssortmentDesc.Rows.Count - 1]["AssortProducts"] = dtAssortmentDesc.Rows[dtAssortmentDesc.Rows.Count - 1]["AssortProducts"] + "," + productRef;
                        //if (scaleData == string.Empty)
                        dtAssortmentDesc.Rows[dtAssortmentDesc.Rows.Count - 1]["AssortDesc"] = dtAssortmentDesc.Rows[dtAssortmentDesc.Rows.Count - 1]["AssortDesc"] + "," + productRef;
                    }

                    oldRecordid = recordId;
                    index++;
                }
                dtReturn.Tables.Add(dtAssortmentDesc);
            }

            return Ds2Json(dtReturn);
        }

        #endregion

        #region 自定义方法
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

        /// <summary>    
        /// DataSet转换为Json   
        /// </summary>    
        /// <param name="dataSet">DataSet对象</param>   
        /// <returns>Json字符串</returns>    
        public static string Ds2Json(DataSet dataSet)
        {
            string jsonString = "{";
            foreach (DataTable table in dataSet.Tables)
            {
                jsonString += "\"" + table.TableName + "\":" + Dtb2Json(table) + ",";
            }
            jsonString = jsonString.TrimEnd(',');
            return jsonString + "}";
        }

        ///// <summary>
        ///// 将datatable转换为json  
        ///// </summary>
        ///// <param name="dtb">Dt</param>
        ///// <returns>JSON字符串</returns>
        //public static string Dtb2Json(DataTable dtb)
        //{
        //    JavaScriptSerializer jss = new JavaScriptSerializer();
        //    System.Collections.ArrayList dic = new System.Collections.ArrayList();
        //    foreach (DataRow dr in dtb.Rows)
        //    {
        //        System.Collections.Generic.Dictionary<string, object> drow = new System.Collections.Generic.Dictionary<string, object>();
        //        foreach (DataColumn dc in dtb.Columns)
        //        {
        //            drow.Add(dc.ColumnName, dr[dc.ColumnName]);
        //        }
        //        dic.Add(drow);

        //    }
        //    //序列化  
        //    return jss.Serialize(dic);
        //}

        /// <summary>
        /// 将datatable转换为json  
        /// </summary>
        /// <param name="dtb">Dt</param>
        /// <returns>JSON字符串</returns>
        private static string Dtb2Json(DataTable dtb)
        {
            JavaScriptSerializer jss = new JavaScriptSerializer();
            System.Collections.ArrayList dic = new System.Collections.ArrayList();
            foreach (DataRow dr in dtb.Rows)
            {
                System.Collections.Generic.Dictionary<string, object> drow = new System.Collections.Generic.Dictionary<string, object>();
                foreach (DataColumn dc in dtb.Columns)
                {
                    drow.Add(dc.ColumnName, dr[dc.ColumnName]);
                }
                dic.Add(drow);

            }
            //序列化  
            string jsonStr = jss.Serialize(dic);
            jsonStr = System.Text.RegularExpressions.Regex.Replace(jsonStr, @"\\/Date\((\d+)\)\\/", match =>
            {
                DateTime dt = new DateTime(1970, 1, 1);
                dt = dt.AddMilliseconds(long.Parse(match.Groups[1].Value));
                dt = dt.ToLocalTime();
                return dt.ToString("yyyy-MM-dd HH:mm:ss");
            });
            return jsonStr;
        }

        //public static string Dtb2Json(DataTable dt)
        //{
        //    StringBuilder jsonBuilder = new StringBuilder();
        //    jsonBuilder.Append("{\"");
        //    jsonBuilder.Append(dt.TableName);
        //    jsonBuilder.Append("\":[");
        //    jsonBuilder.Append("[");
        //    for (int i = 0; i < dt.Rows.Count; i++)
        //    {
        //        jsonBuilder.Append("{");
        //        for (int j = 0; j < dt.Columns.Count; j++)
        //        {
        //            jsonBuilder.Append("\"");
        //            jsonBuilder.Append(dt.Columns[j].ColumnName);
        //            jsonBuilder.Append("\":\"");
        //            jsonBuilder.Append(dt.Rows[i][j].ToString());
        //            jsonBuilder.Append("\",");
        //        }
        //        jsonBuilder.Remove(jsonBuilder.Length - 1, 1);
        //        jsonBuilder.Append("},");
        //    }
        //    jsonBuilder.Remove(jsonBuilder.Length - 1, 1);
        //    jsonBuilder.Append("]");
        //    jsonBuilder.Append("}");
        //    return jsonBuilder.ToString();
        //}

        public string GetData()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
