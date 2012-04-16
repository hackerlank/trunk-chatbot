﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using System.Net;
/******************************************************************************************
AltAIMLBot -- Copyright (c) 2011-2012,Kino Courssey, Daxtron Labs

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
**************************************************************************************************/

namespace AltAIMLbot.AIMLTagHandlers
{
    public class refserver : AltAIMLbot.Utils.AIMLTagHandler
    {

        public refserver(AltAIMLbot.AltBot bot,
                AltAIMLbot.User user,
                AltAIMLbot.Utils.SubQuery query,
                AltAIMLbot.Request request,
                AltAIMLbot.Result result,
                XmlNode templateNode)
            : base(bot, user, query, request, result, templateNode)
        {
        }

        protected override String ProcessChange()
        {
            if (this.templateNode.Name.ToLower() == "refserver")
            {
                // Simply push the filled in tag contents onto the stack
                try
                {
                    String templateNodeInnerValue = this.templateNode.InnerText;
                    string myUrl = this.templateNode.Attributes["url"].Value;
                    string query = templateNodeInnerValue;
                    WebClient client = new WebClient();
                    string webAsk = myUrl + query;
                    string response = client.DownloadString(webAsk);
                    string[] responseSet = response.Split('\n');
                    string webAns = responseSet[0];
                    if (webAns.Contains("rcmd:"))
                    {
                        webAns = webAns.Substring(webAns.IndexOf("rcmd:"));
                        webAns = webAns.Replace("rcmd:", "");
                        webAns = webAns.Replace("say-", "");
                        webAns = webAns.Replace('"', ' ');
                    }
                    else
                    {
                        webAns = "Sorry, I don't understand.";
                    }
                    //webAns = "refserv sez " + webAns;
                    webAns = webAns.Replace("LSA sez", "");
                    Console.WriteLine("refserver :" + webAns);
                    return webAns;
                }
                catch
                {
                }

            }
            return String.Empty;

        }

    }
}