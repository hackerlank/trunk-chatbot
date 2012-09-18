using System;
using System.Runtime;
using System.Text;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using System.IO;
//using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using AltAIMLbot;
using AltAIMLbot.Utils;
using AltAIMLParser;
using RTParser;
using RTParser.Utils;

namespace RTParser.AIMLTagHandlers
{
    public class setevidence : RTParser.Utils.AIMLTagHandler
    {

        public setevidence(RTParser.AltBot bot,
                User user,
                SubQuery query,
                Request request,
                Result result,
                XmlNode templateNode)
            : base(bot, user, query, request, result, templateNode)
        {
        }



        protected override Unifiable ProcessChange()
        {
            if (this.templateNode.Name.ToLower() == "setevidence")
            {
                try

                {
                    var varMSM = this.botActionMSM;
                    string name = GetAttribValue("evidence", null);
                    string cur_prob_str = GetAttribValue("prob", "0.1");
                    double cur_prob = double.Parse(cur_prob_str);

                    MachineSideEffect(() => varMSM.setEvidence(name, cur_prob));
                }
                catch (Exception e)
                {
                    writeToLogWarn("MSMWARN: " + e);
                }
            }
            return Unifiable.Empty;

        }
    }
}