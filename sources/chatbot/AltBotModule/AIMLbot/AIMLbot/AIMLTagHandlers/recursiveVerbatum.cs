using System;
using System.Runtime.Serialization;
using System.Xml;
using System.Text;
using AltAIMLParser;
using RTParser;

namespace AltAIMLbot.AIMLTagHandlers
{
    /// <summary>
    /// The formal element tells the AIML interpreter to render the contents of the element 
    /// such that the first letter of each word is in uppercase, as defined (if defined) by 
    /// the locale indicated by the specified language (if specified). This is similar to methods 
    /// that are sometimes called "Title Case". 
    /// 
    /// If no character in this string has a different uppercase version, based on the Unicode 
    /// standard, then the original string is returned.
    /// </summary>
    public class recursiveVerbatum : Utils.AIMLTagHandler
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="bot">The bot involved in this request</param>
        /// <param name="user">The user making the request</param>
        /// <param name="query">The query that originated this node</param>
        /// <param name="request">The request inputted into the system</param>
        /// <param name="result">The result to be passed to the user</param>
        /// <param name="templateNode">The node to be processed</param>
        public recursiveVerbatum(XmlNode show,
                        AltBot bot,
                        User user,
                        Utils.SubQuery query,
                        Request request,
                        Result result,
                        XmlNode templateNode, bool isRecurse)
            : base(bot, user, query, request, result, templateNode)
        {
            data = show;
            //RecurseResult = data;
            isRecursive = isRecurse;
        }
        public override bool isVerbatum
        {
            get { return true; }
        }
        readonly XmlNode data;
        protected override string ProcessChange()
        {
            return data.OuterXml;
        }

        private readonly string Text;
    }
}
