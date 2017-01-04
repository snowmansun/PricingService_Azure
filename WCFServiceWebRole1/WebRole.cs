using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace WCFServiceWebRole1
{
    public class WebRole : RoleEntryPoint
    {
        public override bool OnStart()
        {
            // 有关处理配置更改的信息，
            // 请在 https://go.microsoft.com/fwlink/?LinkId=166357 参阅 MSDN 主题。

            return base.OnStart();
        }
    }
}
