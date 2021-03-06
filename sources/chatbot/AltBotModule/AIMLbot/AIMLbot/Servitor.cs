﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using AIMLbot;
using AltAIMLParser;
using AltAIMLbot.Utils;
using DcBus;
using System.Runtime.Serialization.Formatters.Binary;

#if (COGBOT_LIBOMV || USE_STHREADS)
using Enyim.Caching;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using MushDLR223.Utilities;
using MushDLR223.Virtualization;
using ThreadPoolUtil;
using ThreadPoolUtil;
using ThreadStart = System.Threading.ThreadStart;
using AutoResetEvent = System.Threading.AutoResetEvent;
using ManualResetEvent = System.Threading.ManualResetEvent;
using TimerCallback = System.Threading.TimerCallback;
using Timer = System.Threading.Timer;
using Interlocked = System.Threading.Interlocked;
#else
using System.Threading;
#endif
using Aima.Core.Logic.Propositional.Algorithms;
using Aima.Core.Logic.Propositional.Parsing;
using Aima.Core.Logic.Propositional.Parsing.AST;
using LAIR.ResourceAPIs.WordNet;
using AltAIMLbot;
using MushDLR223.Utilities;
using MushDLR223.Virtualization;
using VDS.RDF.Parsing;
using LogicalParticleFilter1;
using CAMeRAVUEmotion;
using RoboKindAvroQPID;

/******************************************************************************************
AltAIMLBot -- Copyright (c) 2011-2012,Kino Coursey, Daxtron Labs

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

namespace AltAIMLbot
{
    public class Servitor
    {
        // Connects a AltBot with some world, real or virtual
        // Creates multiple threads so it can have an independent existance

        // To be used with AltBot
        // see 
        //   - Bot.cs AltBot(),
        //   - BotConsole.cs Prepare(), 
        //   - WorldObjectsForAimLBot.cs StartupListener00()

        /// <summary>
        /// dmiles - set 0-2 for verboseness of ExternDB from node
        /// 
        ///     search on this field to see what it covers
        /// </summary>
        public static int DebugLevelExternalDb = 1;

        // True was the way it worked before
        public static bool RunAllTreesIfRootIsMissing = true;

        public AltBot curBot;

        public bool NeedsLoad = true;

        public bool useAMQP = true;

        public MasterUser curUser
        {
            get
            {
                if (_curUser != null) return _curUser;
                return (MasterUser) curBot.LastUser;
            }
            set
            {
                _curUser = value;
                curBot.LastUser = value;
            }
        }

        public Thread tmTalkThread = null;
        private bool _tmTalkEnabled = true;

        public bool tmTalkEnabled
        {
            get
            {
                if (IsBackgroundDisabled) return false;
                return _tmTalkEnabled;
            }
            set { _tmTalkEnabled = value; }
        }

        public bool IsBackgroundDisabled
        {
            get
            {
                //return false; //KHC DEBUG MONOBOT

                if (ChatOptions.ServitorInAIMLOnlyTest) return true;
                if (curBot != null && curBot.GlobalSettings != null)
                {
                    string NBGC = curBot.GlobalSettings.grabSetting("noBackgroundChat");
                    if (NBGC != null && NBGC.ToLower().StartsWith("t"))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public bool mLoadCompleteAndPersonalityShouldBeDefined = false;
        public Thread tmFSMThread = null;
        public bool tmFSMEnabled = true;
        public Thread tmBehaveThread = null;
        private bool _tmBehaveEnabled = true;

        public bool tmBehaveEnabled
        {
            get { return _tmBehaveEnabled; }
            set { _tmBehaveEnabled = value; }
        }

        public Thread myCronThread = null;
        public string lastAIMLInstance = "";
        public bool traceServitor = true;
        public bool skiploadingAimlFiles = false;
        public bool skiploadingServitorState = true;

        public void DontSkiploading(System.Action act)
        {
            var sl = skiploadingAimlFiles;
            skiploadingAimlFiles = false;
            try
            {
                act();
            }
            finally
            {
                skiploadingAimlFiles = sl;
            }
        }

        public bool savedServitor = false;
        public bool skipPersonalityCheck = false;
        public bool initialCritical = false;
        public Scheduler myScheduler = null;
        public InvertedIndex myIndex = null;

        private string _rapStoreDirectoryStem;
        public int _rapStoreSlices;
        public int _rapStoreTrunkLevel;
        public static Servitor LastServitor;

        public SIProlog prologEngine
        {
            get { return curBot.prologEngine; }
        }

        [NonSerialized] public Dictionary<string, SymbolicParticleFilter> partFilterDict =
            new Dictionary<string, SymbolicParticleFilter>();

        //[NonSerialized]
        //public SymbolicParticleFilter partFilter = new SymbolicParticleFilter();
        [NonSerialized] public ServitorEndpoint myServitorEndpoint;
        [NonSerialized] private HumanAgent a2;
        [NonSerialized] private Agent a1;

        [NonSerialized] public Dictionary<string, Agent> CoppeliaAgentDictionary = new Dictionary<string, Agent>();
        public Dictionary<string, AgentAction> CoppeliaActionDictionary = new Dictionary<string, AgentAction>();
        public Dictionary<string, int> CoppeliaStateDictionary = new Dictionary<string, int>();
        public Dictionary<string, int> CoppeliaMoralsDictionary = new Dictionary<string, int>();

        public string rapStoreDirectory
        {
            get
            {
                if (curBot != null) return curBot.rapStoreDirectoryStem;
                return _rapStoreDirectoryStem;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    Console.WriteLine("Error! setting Servitor rapStoreDirectory Empty");
                }
                if (curBot != null)
                {
                    curBot.rapStoreDirectoryStem = value;
                }
                _rapStoreDirectoryStem = value;
            }
        }

        public int rapStoreSlices
        {
            get { return _rapStoreSlices; }
            set { _rapStoreSlices = value; }
        }

        public int rapStoreTrunkLevel
        {
            get { return _rapStoreTrunkLevel; }
            set { _rapStoreTrunkLevel = value; }
        }

        public ChatOptions ChatOptions = new ChatOptions();

        public int PruneSize = 1024;

        public void messageProcessor0(string topic,string prefix, Dictionary<string, object> map)
        {
            string chan = topic.ToLower();

            if (chan == "#") chan = "hashmark";
            if (chan == "hashmark") return;

            this.prologEngine.FindOrCreateKB(chan);
            this.prologEngine.FindOrCreateKB("QPIDMT");
            this.prologEngine.connectMT("QPIDMT", chan);
            string deKB = "";
            foreach (string key in map.Keys)
            {
                string attrib = key.ToLower();
                string value ="null";
                if (map[key] !=null) value= map[key].ToString().ToLower();
                if (value.Contains(" ")) value = "'" + value + "'";
                if (value == "#") value = "hashmark";
                deKB += String.Format("map({0},{1},{2}).\n", chan, attrib, value);
            }
             this.prologEngine.insertKB(deKB, chan);
        }

        public void messageProcessor(string topic, string prefix, Dictionary<string, object> map)
        {
            this.prologEngine.FindOrCreateKB(topic.ToLower());
            this.prologEngine.FindOrCreateKB("QPIDMT");
            this.prologEngine.connectMT("QPIDMT", topic.ToLower());
            string deKB = "";
            string lTopic = topic.ToLower();
            string chan = lTopic + prefix;
            this.prologEngine.FindOrCreateKB(chan);
            this.prologEngine.connectMT("QPIDMT", chan);
            foreach (string key in map.Keys)
            {
                string attrib = key.ToLower();
                if (map[key] != null)
                {
                    string value = map[key].ToString().ToLower();
                    value = value.Replace("\\", "_");
                    value = value.Replace("/", "_");
                    if (!value.StartsWith("-")) value = value.Replace("-", "_");
                    if (value.Contains(" ")) value = "'" + value + "'";
                    if (value == "#") value = "hashmark";

                    string statement = String.Format("map({0},{1},{2}).", chan, attrib, value);
                    deKB += statement + "\n";
                    // Give names
                    if (attrib == "emotionid")
                    {
                        switch (value)
                        {
                            case "0": deKB += String.Format("map({0},emotion,joy).\n", chan, attrib, value);  break;
                            case "1": deKB += String.Format("map({0},emotion,sadness).\n", chan, attrib, value);  break;
                            case "2": deKB += String.Format("map({0},emotion,anger).\n", chan, attrib, value);  break;
                            case "3": deKB += String.Format("map({0},emotion,surprise).\n", chan, attrib, value);  break;
                            case "4": deKB += String.Format("map({0},emotion,fear).\n", chan, attrib, value);  break;
                            case "5": deKB += String.Format("map({0},emotion,contempt).\n", chan, attrib, value);  break;
                            case "6": deKB += String.Format("map({0},emotion,disgust).\n", chan, attrib, value);  break;
                            case "7": deKB += String.Format("map({0},emotion,neutral).\n", chan, attrib, value);  break;
                            case "8": deKB += String.Format("map({0},emotion,positive).\n", chan, attrib, value);  break;
                            case "9": deKB += String.Format("map({0},emotion,negative).\n", chan, attrib, value);  break;
                            default : deKB += String.Format("map({0},emotion,unknown).\n", chan, attrib, value);  break;
                        }
                    }
                    // Compute areas
                    if (attrib == "value_faces_rectangle_width")
                    {
                        if (map.ContainsKey("Value_Faces_Rectangle_Height"))
                        {
                            int width = (int)map["Value_Faces_Rectangle_Width"];
                            int height = (int)map["Value_Faces_Rectangle_Height"];
                            int area = width * height;
                            deKB += String.Format("map({0},value_faces_rectangle_area,{1}).\n", chan, area); 
                        }
                    }
                    //this.prologEngine.appendKB(statement, chan);
                }
                if (map[key] is System.Object[])
                {
                    string nextpath = "";
                    if (prefix.Length > 0)
                    {
                        nextpath = prefix + "_" + key.ToLower();
                    }
                    else
                    {
                        nextpath = "_" + key.ToLower();
                    }
                    messageObjectSubProcessor(topic, nextpath, (System.Object[])map[key]);
                }
            }
            // Connect
            if (lTopic != chan)
            {
                this.prologEngine.connectMT(lTopic, chan);
                deKB += String.Format("map({0},childof,{1}).\n", chan,  lTopic);
            }
            this.prologEngine.insertKB(deKB, chan);

        }
        public void messageObjectSubProcessor(string topic, string prefix, System.Object[] subArry)
        {
            int count = 0;
            foreach (System.Object obj in subArry)
            {
                count++;
                if (obj is Dictionary<string, object>)
                {
                    string nextpath = prefix + "_" + count.ToString();
                    messageProcessor(topic, nextpath, (Dictionary<string, object>)obj);
                }
                else
                {
                    Console.WriteLine("SubObject is type:{0}", obj.GetType());
                }
            }
        }

        public  RoboKindEventModule _theRoboKindEventModule;

        public void initQPIDInterface()
        {
            if (useAMQP)
            {
                _theRoboKindEventModule = new RoboKindEventModule();
                _theRoboKindEventModule.QPIDProcessor = new QPIDMessageDelegate(messageProcessor); ;
                _theRoboKindEventModule.Spy();
            }

        }
        public Servitor(AltBot aimlBot, sayProcessorDelegate outputDelegate)
        {
            curBot = aimlBot;
            curBot.myServitor = this;
            initQPIDInterface();
        }

        public Servitor(AltBot aimlBot, sayProcessorDelegate outputDelegate, bool skipLoading, bool skippersonalitycheck,
                        bool initialcritical)
        {
            curBot = aimlBot;
            curBot.myServitor = this;
            skiploadingServitorState = skipLoading;
            skipPersonalityCheck = skippersonalitycheck;
            initialCritical = initialcritical;
            initQPIDInterface();
        }

        public string GetCoppeliaAgentNameByID(int queryID)
        {
            foreach (string name in CoppeliaAgentDictionary.Keys)
            {
                if (CoppeliaAgentDictionary[name].AgentID == queryID)
                {
                    return name;
                }
            }
            return "unknown";
        }

        public void InitCoppelia()
        {
            //Create new agents
            a1 = new Agent(0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f);
            a2 = new HumanAgent();
            CoppeliaAgentDictionary["self"] = a1;
            CoppeliaAgentDictionary["other"] = a2;
            CoppeliaAgentDictionary["human"] = a2;

            //Register the RequestingInput function (see below) as a function that needs to be called when Agent a2 (which is the HumanAgent) needs to respond to input
            //a2.input += new HumanAgent.InputRequest(RequestingInput);
            a2.input += new HumanAgent.InputRequest(RequestingInputFromMt);

            //Register the GlobalActionReceived function (see below) as a function that needs to be called whenever any action is performed by any agent.
            Global.actionBroadcast += new Global.ActionListener(GlobalActionReceived);
            //Register both agents with the model, so they will be updated
            //You'll most likely always need to do this for all agents
            CAMeRAVUEmotion.Model.RegisterAgent(a1);
            CAMeRAVUEmotion.Model.RegisterAgent(a2);

            //Register the agents with each other, so they know they exist
            //Agents will not target other agents with actions unless they know they exist
            a1.AddAgent(a2.AgentID);
            a2.AddAgent(a1.AgentID);

            // Need to 
            // - Creat actions with positivity/negativity +
            // - define the responses between actions +
            // - define the states +
            // - define ambition for each actor and state+
            // - defeine Action->State facilitation for each actor +
            // - define State->State facilitation for each actor
            // - define Actor features
            // - define manual actions


            //Start running the model
            //This will run the Model in a separate thread, until it is stopped
            //Stopping the model will in essense pause the simulation, so you can do this whenever necessary
            //The Model will pause when a HumanAgent needs to respond to input
            CAMeRAVUEmotion.Model.Start();
        }

        /// <summary>
        /// This function is called when the HumanAgent a1's input member is fired (which happens when it receives an action from another agent)
        /// </summary>
        /// <returns></returns>
        private int RequestingInput()
        {
            //Some display so users know what's going on

            Console.WriteLine("Select Response");

            //display possible responses
            for (int i = 0; i < a2.PossibleResponses.Count; ++i)
            {
                Console.WriteLine("" + i + ": " + Global.GetActionByID(a2.PossibleResponses[i]).Name);
            }

            //Request input from users
            int num = -1;
            bool failedParse = false;
            do
            {
                failedParse = false;
                string input = Console.In.ReadLine();
                if (!int.TryParse(input, out num))
                {
                    failedParse = true;
                }
            } while (!failedParse && num < 0 || num >= a2.PossibleResponses.Count);

            int responseID = -1;

            //Check if the input of the user is valid
            if (num >= 0 && num < a2.PossibleResponses.Count)
                responseID = a2.PossibleResponses[num];

            //Return the selected response
            //-1 is an invalid ActionID and will constitute "no action"
            return responseID;
        }

        private int RequestingInputFromMt()
        {
            // System has a preference for those actions it expects
            //  but is able to recognize others it knows about

            int responseID = -1;
            for (int i = 0; i < a2.PossibleResponses.Count; ++i)
            {
                string query = String.Format("performed({0})", Global.GetActionByID(a2.PossibleResponses[i]).Name);
                if (this.prologEngine.isTrueIn(query, "coppeliaInputMt"))
                {
                    responseID = a2.PossibleResponses[i];
                }
                //Console.WriteLine("" + i + ": " + Global.GetActionByID(a2.PossibleResponses[i]).Name);
            }
            if (responseID == -1)
            {
                //Not expected but maybe an unexpected reaction
                // iterative deepening
                int testdepth = 64;
                string query = "performed(ACTION)";
                List<Dictionary<string, string>> bingingsList = new List<Dictionary<string, string>>();
                while ((bingingsList.Count == 0) && (testdepth < 256))
                {
                    testdepth = (int) (testdepth*1.5);
                    //Console.WriteLine("Trying depth {0}", testdepth);
                    //prologEngine.maxdepth = testdepth;
                    this.prologEngine.askQuery(query, "coppeliaInputMt", out bingingsList);
                }
                if (bingingsList.Count > 0)
                {
                    string finalAction = "";
                    // Pick one at random
                    Random rgen = new Random();
                    int randomBinding = rgen.Next(0, bingingsList.Count);
                    Dictionary<string, string> bindings = bingingsList[randomBinding];
                    foreach (string k in bindings.Keys)
                    {
                        string v = bindings[k];
                        //Console.WriteLine("BINDING {0} = {1}", k, v);
                        if (k == "ACTION")
                        {
                            v = v.Replace("\"", "");
                            finalAction = v;
                            Console.WriteLine("ACTION = '{1}'", k, v);

                        }
                    }
                    if (CoppeliaActionDictionary.ContainsKey(finalAction))
                    {
                        responseID = CoppeliaActionDictionary[finalAction].GlobalIndex;
                    }
                }

            }
            postCoppeliaAgentsMts();
            //Return the selected response
            //-1 is an invalid ActionID and will constitute "no action"
            if (responseID == -1)
            {
                // since we're looking an an MT we may want to give some time back
                // so someone else can post to it
                Thread.Sleep(1000);
            }
            else
            {
                Thread.Sleep(1000);
            }
            return responseID;
        }

        /// <summary>
        /// This function is called when any agent performs any action.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="action"></param>
        /// <param name="target"></param>
        private void GlobalActionReceived(int sender, int action, int target)
        {
            Console.WriteLine("Caught Action: Agent " + sender + " performed action " +
                              Global.GetActionByID(action).Name + " on Agent " + target);

            //if the OK action is performed, update STATE_LOST_THE_GAME to true
            // if (action == OK.GlobalIndex)
            // {
            //     Console.WriteLine("Setting state STATE_LOST_THE_GAME to true");
            //     Global.SetState(STATE_LOST_THE_GAME, true);
            // }
            string mt = "coppeliaLastOutputMt";
            string actionReport = "";
            if (GetCoppeliaAgentNameByID(sender) == "self")
            {
                this.prologEngine.connectMT("coppeliaOutputMt", "coppeliaLastOutputMt");
                this.prologEngine.insertKB("", mt);
                this.prologEngine.insertKB("", "lastinputMt");
                this.prologEngine.insertKB("", "coppeliaInputMt");
                actionReport = String.Format("selfAct({0},{1}).", Global.GetActionByID(action).Name,
                                             GetCoppeliaAgentNameByID(target));
                this.prologEngine.appendKB(actionReport, mt);
                this.prologEngine.appendKB(actionReport, "lastCoppeliaActMt");
            }
            actionReport = String.Format("performedAction({0},{1},{2}).", GetCoppeliaAgentNameByID(sender),
                                         Global.GetActionByID(action).Name, GetCoppeliaAgentNameByID(target));
            this.prologEngine.appendKB(actionReport, mt);
            //postCoppeliaAgentsMts();
        }

        public void postCoppeliaAgentsMts()
        {
            //Actions
            foreach (string ak in CoppeliaActionDictionary.Keys)
            {
                AgentAction a1 = CoppeliaActionDictionary[ak];
                string actionDescMt = "coppeliaActionDescription_" + ak;
                string actionDescription = this.prologEngine.describeObject(ak, a1);
                this.prologEngine.connectMT("coppeliaActionDescriptionMt", actionDescMt);
                this.prologEngine.insertKB(actionDescription, actionDescMt);
            }
            Thread.Sleep(10);
            //State
            foreach (string sk in CoppeliaStateDictionary.Keys)
            {
                int cstate = CoppeliaStateDictionary[sk];
                bool stateV = Global.GetState(cstate);
                string stateDescMt = "coppeliaStateDescription_" + sk;
                //string stateDescription = this.prologEngine.describeObject(ak, a1);
                string stateDescription = String.Format("triple({0},{1},{2}).", sk, "value", stateV);
                this.prologEngine.connectMT("coppeliaStateDescriptionMt", stateDescMt);
                this.prologEngine.insertKB(stateDescription, stateDescMt);
            }
            Thread.Sleep(10);
            //Agents
            foreach (string ak in CoppeliaAgentDictionary.Keys)
            {
                Thread.Sleep(10);
                Agent a1 = CoppeliaAgentDictionary[ak];
                string agentMt = "coppeliaAgent_" + ak;

                string agentDescMt = "coppeliaAgentDescription_" + ak;
                string agentDescription = this.prologEngine.describeObject(ak, a1);
                this.prologEngine.connectMT("coppeliaAgentDescriptionMt", agentDescMt);
                this.prologEngine.insertKB(agentDescription, agentDescMt);

                this.prologEngine.connectMT("coppeliaAgentEmotionsMt", agentMt);
                string gaf = "";
                this.prologEngine.insertKB("", agentMt);
                gaf = String.Format("agentID({0},{1}).", ak, a1.AgentID);
                this.prologEngine.appendKB(gaf, agentMt);

                for (int e = 0; e < AgentEmotions.NUM_VALUES; e++)
                {
                    float v = a1.GetEmotion(e);
                    string emotionSymbol = AgentEmotions.StringFor(e);
                    gaf = String.Format("agentEmotion({0},{1},{2}).", ak, emotionSymbol, v);
                    this.prologEngine.appendKB(gaf, agentMt);
                }
                for (int e = 0; e < AgentEmotions.NUM_VALUES; e++)
                {
                    float v = a1.GetDesired(e);
                    string emotionSymbol = AgentEmotions.StringFor(e);
                    gaf = String.Format("agentEmotionalDesire({0},{1},{2}).", ak, emotionSymbol, v);
                    this.prologEngine.appendKB(gaf, agentMt);
                }
                for (int i = 0; i < a1.PossibleResponses.Count; ++i)
                {
                    gaf = String.Format("possibleResponse({0},{1}).", ak,
                                        Global.GetActionByID(a1.PossibleResponses[i]).Name);
                    this.prologEngine.appendKB(gaf, agentMt);
                }
                foreach (string otherName in CoppeliaAgentDictionary.Keys)
                {
                    Agent otherAgent = CoppeliaAgentDictionary[otherName];
                    float v = 0;
                    v = a1.GetAnger(otherAgent.AgentID);
                    gaf = String.Format("anger({0},{1},{2}).", ak, otherName, v);
                    this.prologEngine.appendKB(gaf, agentMt);
                    v = a1.GetPraiseworthy(otherAgent.AgentID);
                    gaf = String.Format("praiseworthy({0},{1},{2}).", ak, otherName, v);
                    this.prologEngine.appendKB(gaf, agentMt);
                    foreach (string actName in CoppeliaActionDictionary.Keys)
                    {
                        AgentAction act = CoppeliaActionDictionary[actName];

                        v = a1.GetAT(otherAgent.AgentID, act.GlobalIndex);
                        gaf = String.Format("actionTendency({0},{1},{2},{3}).", ak, otherName, actName, v);
                        this.prologEngine.appendKB(gaf, agentMt);

                        v = a1.GetExpectedSatisfaction(otherAgent.AgentID, act.GlobalIndex);
                        gaf = String.Format("expectedSatisfaction({0},{1},{2},{3}).", ak, otherName, actName, v);
                        this.prologEngine.appendKB(gaf, agentMt);

                        v = a1.GetGMoralityAction(otherAgent.AgentID, act.GlobalIndex);
                        gaf = String.Format("moralityAction({0},{1},{2},{3}).", ak, otherName, actName, v);
                        this.prologEngine.appendKB(gaf, agentMt);


                    }
                    foreach (string sk in CoppeliaStateDictionary.Keys)
                    {
                        int cstate = CoppeliaStateDictionary[sk];

                        v = a1.GetStateLikelihood(cstate);
                        gaf = String.Format("stateLikelihood({0},{1},{2}).", ak, sk, v);
                        this.prologEngine.appendKB(gaf, agentMt);

                        v = a1.GetStateBelieved(cstate);
                        gaf = String.Format("stateBelieved({0},{1},{2}).", ak, sk, v);
                        this.prologEngine.appendKB(gaf, agentMt);

                        v = a1.GetStateBelief(cstate);
                        gaf = String.Format("stateBelief({0},{1},{2}).", ak, sk, v);
                        this.prologEngine.appendKB(gaf, agentMt);

                        v = a1.GetAmbition(cstate);
                        gaf = String.Format("ambition({0},{1},{2}).", ak, sk, v);
                        this.prologEngine.appendKB(gaf, agentMt);

                    }
                }

            }
        }

        public void Start(sayProcessorDelegate outputDelegate)
        {
            DoWithServitorLock(() => Start0(outputDelegate));
        }

        public void Start0(sayProcessorDelegate outputDelegate)
        {
            if (!NeedsStarted) return;
            NeedsStarted = false;
            Servitor.LastServitor = this;
            Console.WriteLine("RealBot operating in :" + Environment.CurrentDirectory);
            Console.WriteLine("       ProcessorCount:" + Environment.ProcessorCount);
            Console.WriteLine("             UserName:" + Environment.UserName);
            Console.WriteLine("            TickCount:" + Environment.TickCount);
            User curUser = this.curUser;
            if ((curUser != null) && (curUser.UserID != null))
            {
                Console.WriteLine("            UserID:" + curUser.UserID);
            }
            else
            {
                Console.WriteLine("            UserID: UNDEFINED");
            }
            curBot.myServitor = this;

            myServitorEndpoint = new ServitorEndpoint(curBot, this, prologEngine);

            myScheduler = myScheduler ?? new Scheduler(this);
            myIndex = myIndex ?? new InvertedIndex();
            rapStoreDirectory = ".//rapstore//";
            curBot.bbSafe = true;
            outputDelegate = outputDelegate ?? curBot.sayProcessor;
            if (outputDelegate == null || outputDelegate == sayResponseToBlackboard)
            {
                curBot.sayProcessor = new sayProcessorDelegate(sayResponseToBlackboard);
                Console.WriteLine(" using default sayProcessorDelegate (sayResponseToBlackboard)");
            }
            else
            {
                curBot.sayProcessor = outputDelegate;
                Console.WriteLine(" using external sayProcessorDelegate");
            }
            //Console.WriteLine("Servitor loadSettings");

            //curBot.loadSettings();

            //Console.WriteLine("Servitor User");
            //var myUser = new MasterUser(UserID, myBot);

            //curUser = myUser;
            //myBot.isAcceptingUserInput = false;
            curBot.inCritical = initialCritical;

            Console.WriteLine("Servitor startMtalkWatcher");
            startMtalkWatcher();

            Console.WriteLine("Servitor startFSMEngine");
            startFSMEngine();
            Console.WriteLine("Servitor startBehaviorEngine");
            startBehaviorEngine();
            Console.WriteLine(" Servitor beginning Coppelia");
            InitCoppelia();
            Console.WriteLine("Servitor startCronEngine");
            startCronEngine();
            curBot.myBehaviors.keepTime("activation", RunStatus.Success);
            curBot.myBehaviors.activationTime("activation", RunStatus.Success);

            Console.WriteLine(" Servitor startup complete");
        }

        public bool LoadCompleteOnce
        {
            get
            {
                lock (ServitorStartStopLoadLock)
                {
                    return _loadCompleteOnce;
                }
            }
        }

        private bool _loadCompleteOnce = false;
        public object ServitorStartStopLoadLock = new object();
        public bool NeedsStarted = true;
        private MasterUser _curUser;

        private void LogException(Exception e)
        {
            if (!DLRConsole.Trace("ERROR " + e))
            {
                Console.WriteLine("{0}\n{1}", e.Message, e.StackTrace);
            }
        }

        public void DoWithServitorLock(Action withLock)
        {
            lock (ServitorStartStopLoadLock)
            {
                WaitUntilCompletedGlobals();
                withLock();
            }
        }

        public void loadComplete()
        {
            DoWithServitorLock(loadComplete0);
        }

        private void loadComplete0()
        {
            curBot.isAcceptingUserInput = true;
            lock (ServitorStartStopLoadLock)
            {
                if (_loadCompleteOnce) return;
                _loadCompleteOnce = true;
            }
            curBot.loadGlobalBotSettings();
            curBot.startServitor();
            string servRoot = curBot.GlobalSettings.grabSetting("serverRoot", false);
            if ((servRoot != null) && (servRoot.Length > 7))
            {
                WebServitor.serverRoot = servRoot;
            }
            string servPort = curBot.GlobalSettings.grabSetting("serverPort", false);
            if (servPort != null)
            {
                try
                {
                    WebServitor.serverPort = int.Parse(servPort);
                }
                catch
                {
                }
            }

            ThreadPool.QueueUserWorkItem((o) =>
                                             {
                                                 Console.WriteLine("Servitor WebServitor.beginService");
                                                 WebServitor.beginService(this);
                                             });
            Thread.Sleep(600);
            Console.WriteLine("Servitor checkNewPersonality");

            bool personDefined = false;
            if (!skipPersonalityCheck) personDefined = checkNewPersonality();

            lock (curBot)
            {
                if ((personDefined == false) && (lastAIMLInstance.Length == 0))
                {
                    curBot.LoadPersonality();
                }
            }

            mLoadCompleteAndPersonalityShouldBeDefined = true;
            curBot.useMemcache = true;
            // FOR DEBUG
            curBot.inCritical = true;
            curBot.isAcceptingUserInput = true;
            // FOR TESTING
            curBot.inCritical = false;
            curBot.blockCron = false;

            if (AltBot.MemcachedServerKnownDead)
            {
                Console.WriteLine("*** WARNING Memcached Server Known DEAD  ***");
                curBot.useMemcache = false;
            }
            var bctx = curBot.BotBehaving;
            bctx.exportBBBot();
            bctx.importBBBot();
            bctx.exportBBUser(curUser);
            bctx.importBBUser(curUser);
            if ((myScheduler != null) && curBot.myBehaviors.definedBehavior("startup"))
            {
                myScheduler.ActivateBehaviorTask("startup", bctx);
                Console.WriteLine("*** ActivateBehaviorTask startup ***");
            }
            else
            {
                Console.WriteLine("*** ActivateBehaviorTask startup NOT EXECUTED ***");
                if (myScheduler == null)
                    Console.WriteLine("** ERROR (myScheduler == null)");

                if (!curBot.myBehaviors.definedBehavior("startup"))
                    Console.WriteLine("** WARNING 'startup' not in definedBehavior");
            }
            curBot.StampRaptstoreValid(true);
            myServitorEndpoint.StartServer();
            curBot.loadChanging = false;
            curBot.RunOnBotCreatedHooks();
        }

        public bool setGuestEvalObject(object guestObj)
        {
            if (curBot == null) return false;
            curBot.guestEvalObject = guestObj;
            return true;
        }

        /// <summary>
        ///  respondToChat returns a string response so it is expected 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="UserID"></param>
        /// <returns></returns>
        public string respondToChat(string input, User user)
        {
            return respondToChat(input, user, true, RequestKind.ChatForString);
        }

        public string respondToChat(string input, string UserID)
        {
            return respondToChat(input, curBot.FindOrCreateUser(UserID));
        }

        public string respondToChat(string input, User curUser, bool isToplevel, RequestKind requestType)
        {
            return respondToChat(input, curUser, isToplevel, requestType, null);
        }
        public string respondToChat(string input, User curUser, bool isToplevel, RequestKind requestType, TextWriter extra)
        {
            TextWriter writer = new StringWriter();

            sayProcessorDelegate ourSayProcessor = (str) =>
                                                       {
                                                           if (extra != null) extra.WriteLine(str);
                                                           writer.WriteLine(str);
                                                       };
            chatIntoDelegate(input, curUser, isToplevel, requestType, ourSayProcessor);
            return writer.ToString();
        }

        public void chatIntoDelegate(string input, User curUser, bool isToplevel, RequestKind requestType, sayProcessorDelegate writer)
        {
            sayProcessorDelegate prev = curBot.sayProcessor;
            bool[] resetSayProcessor = new bool[] { false };
            Action onComplete = () =>
            {
                if (!resetSayProcessor[0])
                {
                    curBot.sayProcessor = prev;
                    resetSayProcessor[0] = true;
                }
            };
            try
            {
                curBot.sayProcessor = writer;
                chatWithOutputProcessor(input, curUser, isToplevel, requestType, true, onComplete);
            }
            finally
            {
                /// if chatWithOutputProcessor can guarentee it will run 'onComplete' we should comment this next line out
                onComplete();
            }
        }
        public void chatWithOutputProcessor(string input, User curUser, bool isToplevel, RequestKind requestType,
                                     bool waitUntilDone, Action onCompleted)
        {
            var curBot = this.curBot.BotBehaving;
            bool doHaviours = tmBehaveEnabled && !ChatOptions.ServitorInAIMLOnlyTest;
            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine("** WARNING NO INPUT");
                if (waitUntilDone)
                {
                    // do something to wait for mtalk of behavour trees to do somehting
                }
                onCompleted();
                return;
            }
            input = input.TrimStart();
            if (input.StartsWith("@"))
            {
                this.curBot.AcceptInput((f, a) =>
                                            {
                                                curBot.postOutput(DLRConsole.SafeFormat(f, a));
                                            }, input, curUser, isToplevel, requestType, false);
                onCompleted();
                return;
            }
            if (input.StartsWith("<"))
            {
                RunStatus rs = curBot.myBehaviors.runBTXML(input, curBot);
                curBot.postOutput(string.Format("<!-- runstatus was {0} -->", rs));
                onCompleted();
                return;
            }
            curBot.isPerformingOutput = true;
            if (curBot.myBehaviors.waitingForChat && isToplevel)
            {
                Console.WriteLine(" ************ FOUND waitingForChat ************");
                MaybeUpdateUserJustSaidLastInput(isToplevel, requestType, curUser, input, true);
                //curBot.lastBehaviorChatInput = input;
                curBot.myBehaviors.logText("waitingForChat USER INPUT: " + input);
                curBot.chatInputQueue.Clear();
                curBot.chatInputQueue.Enqueue(input);
                curBot.lastBehaviorUser = curUser;
                //curBot.myBehaviors.runEventHandler("onchat");
                curBot.flushOutputQueue();

                //curBot.myBehaviors.queueEvent("onchat");
                //curBot.processOutputQueue();

                curBot.lastBehaviorChatOutput = "";
                myScheduler.SleepAllTasks(30000);
                curBot.isPerformingOutput = true;
                //Console.WriteLine("Waiting for chat");
                onCompleted();
                return;
            }
            // Try the event first
            string fnd;
            if (doHaviours && isToplevel && curBot.myBehaviors.hasEventHandler("onchat", out fnd))
            {
                try
                {
                    Console.WriteLine(" ************ FOUND ONCHAT ************");
                    MaybeUpdateUserJustSaidLastInput(isToplevel, requestType, curUser, input, true);
                    //curBot.lastBehaviorChatInput = input;
                    curBot.isPerformingOutput = false;
                    curBot.myBehaviors.logText("ONCHAT USER INPUT:" + input);
                    curBot.chatInputQueue.Clear();
                    curBot.chatInputQueue.Enqueue(input);
                    curBot.lastBehaviorUser = curUser;
                    curBot.flushOutputQueue();

                    //curBot.myBehaviors.queueEvent("onchat");
                    //curBot.processOutputQueue();

                    curBot.lastBehaviorChatOutput = "";
                    myScheduler.SleepAllTasks(30000);
                    myScheduler.EnqueueEvent("onchat", curBot.BotBehaving);
                    if (waitUntilDone)
                    {
                        myScheduler.WaitUntilComplete(fnd);
                        string chatOutput = curBot.lastBehaviorChatOutput;
                        if (!string.IsNullOrEmpty(chatOutput))
                        {
                            curBot.isPerformingOutput = true;
                            curBot.myBehaviors.logText("ONCHAT IMMED RETURN:" + chatOutput);
                            MaybeUpdateBotJustSaidLastOutput(isToplevel, requestType, curUser, chatOutput, true, false,
                                                             false);
                        }
                        curBot.postOutput(chatOutput);
                    }
                }
                catch (Exception e)
                {
                    LogException(e);
                    curBot.isPerformingOutput = true;
                }
                onCompleted();
                return;
            }
            // else try the named behavio}
            if (doHaviours && isToplevel && curBot.myBehaviors.definedBehavior("chatRoot"))
            {
                try
                {
                    // we need a clean slate right?
                    curBot.processOutputQueue();

                    // we need to take over for a few so sleep all tasks
                    myScheduler.SleepAllTasks(30000);

                    MaybeUpdateUserJustSaidLastInput(isToplevel, requestType, curUser, input, true);
                    //curBot.lastBehaviorChatInput = input;
                    curBot.isPerformingOutput = false;
                    curBot.myBehaviors.logText("CHATROOT USER INPUT:" + curBot.lastBehaviorChatOutput);

                    curBot.chatInputQueue.Clear();
                    curBot.chatInputQueue.Enqueue(input);
                    curBot.lastBehaviorUser = curUser;
                    //curBot.myBehaviors.runBotBehavior("chatRoot", curBot);
                    curBot.flushOutputQueue();
                    //curBot.myBehaviors.queueEvent("chatRoot");
                    curBot.processOutputQueue();
                    curBot.ClearLastOutput(true);
                    myScheduler.SleepAllTasks(30000);
                    //myScheduler.ActivateBehaviorTask("chatRoot", true);
                    myScheduler.ActivateBehaviorTask("chatRoot", false, curBot.BotBehaving);

                    if (waitUntilDone)
                    {
                        myScheduler.WaitUntilComplete("chatRoot");
                        string chatOutput = curBot.lastBehaviorChatOutput;
                        if (!string.IsNullOrEmpty(chatOutput))
                        {
                            curBot.isPerformingOutput = true;
                            curBot.myBehaviors.logText("CHATROOT IMMED RETURN:" + chatOutput);
                            MaybeUpdateBotJustSaidLastOutput(isToplevel, requestType, curUser, chatOutput, true, false,
                                                             false);
                        }
                        curBot.postOutput(chatOutput);
                    }

                    //while (!myScheduler.empty())
                    //{
                    //    myScheduler.Run();
                    //}
                }
                catch (Exception e)
                {
                    LogException(e);
                    curBot.isPerformingOutput = true;
                }
                onCompleted();
                return;
            }
            // else just do it (no other behavior is defined)
            if (!waitUntilDone)
            {
                new Thread(respondToChatThruBasicAIMLNoWait(input, curUser, isToplevel, requestType, onCompleted)).Start();
                return;
            }
            respondToChatThruBasicAIMLNoWait(input, curUser, isToplevel, requestType, onCompleted).Invoke();
        }

        public ThreadStart respondToChatThruBasicAIMLNoWait(string input, User curUser, bool isToplevel,
                                                     RequestKind requestType, Action onCompleted)
        {
            var curBot = this.curBot.BotBehaving;
            MaybeUpdateUserJustSaidLastInput(isToplevel, requestType, curUser, input, false);
            Request r = new Request(input, curUser, curUser.That, curBot, isToplevel, requestType);
            curBot.isPerformingOutput = false;

            //curBot.lastBehaviorChatInput = input;
            r.OnResultComplete = (res) =>
                               {
                                   // get output from result or else use lastBehavouirChatOutput
                                   Unifiable output = res.Output;
                                   string outputS = (string) output;
                                   if (string.IsNullOrEmpty(outputS))
                                   {
                                       outputS = (string) curBot.lastBehaviorChatOutput;
                                       if (string.IsNullOrEmpty(outputS))
                                       {
                                           curBot.Logger.Warn("cant get an output for " + r);
                                           onCompleted();
                                           return;
                                       }
                                       output = (Unifiable) outputS;
                                   }
                                   if (traceServitor)
                                   {
                                       Console.WriteLine("SERVITOR: respondToChat({0})={1}", input, output);
                                   }
                                   curBot.ClearLastOutput(true);
                                   curBot.lastBehaviorChatOutput = outputS;
                                   curBot.isPerformingOutput = true;
                                   curBot.myBehaviors.logText("CHAT IMMED RETURN:" + curBot.lastBehaviorChatOutput);
                                   MaybeUpdateBotJustSaidLastOutput(isToplevel, requestType, curUser, outputS, true,
                                                                    false,
                                                                    false);
                                   curBot.isPerformingOutput = true;
                                   onCompleted();
                               };
            return (() => { Result res = curBot.Chat(r); });
        }


        public string respondToChatThruBasicAIML(string input, User curUser, bool isToplevel, RequestKind requestType)
        {
            var curBot = this.curBot.BotBehaving;
            MaybeUpdateUserJustSaidLastInput(isToplevel, requestType, curUser, input, false);
            Request r = new Request(input, curUser, curUser.That, curBot, isToplevel, requestType);
            curBot.isPerformingOutput = false;
            try
            {
                //curBot.lastBehaviorChatInput = input;
                Result res = curBot.Chat(r);
                r.result = res;
                // get output from result or else use lastBehavouirChatOutput
                Unifiable output = res.Output;
                string outputS = (string)output;
                if (string.IsNullOrEmpty(outputS))
                {
                    outputS = (string)curBot.lastBehaviorChatOutput;
                    if (string.IsNullOrEmpty(outputS))
                    {
                        curBot.Logger.Warn("cant get an output for " + r);
                        return "....";
                    } 
                    output = (Unifiable)outputS;
                }
                if (traceServitor)
                {
                    Console.WriteLine("SERVITOR: respondToChat({0})={1}", input, output);
                }
                curBot.lastBehaviorChatOutput = outputS;
                curBot.isPerformingOutput = true;
                curBot.myBehaviors.logText("CHAT IMMED RETURN:" + curBot.lastBehaviorChatOutput);
                MaybeUpdateBotJustSaidLastOutput(isToplevel, requestType, curUser, outputS, true, false,
                                 false);
                return outputS;
            }
            catch(Exception e)
            {
                LogException(e);
                curBot.isPerformingOutput = true;
                return "...";
            }
        }

        public void MaybeUpdateUserJustSaidLastInput(bool isToplevel, RequestKind requestType, User curUser, string input, bool respondingDoneFromQueue)
        {
            if (Request.IsToplevelRealtimeChat(isToplevel, requestType))
            {
                curBot.updateRTP2Sevitor(curUser);
                if (!respondingDoneFromQueue)
                {
                    curUser.JustSaid = input;
                }
                curUser.Predicates.updateSetting("lastinput", input);
                string confirm0 = curUser.Predicates.grabSetting("lastinput");
                string confirm = curUser.JustSaid;
                prologEngine.postListPredToMt("lastinput", input, "lastinputMt");
            }            
        }

        public void MaybeUpdateBotJustSaidLastOutput(bool isToplevel, RequestKind requestType, User curUser, string answer,
            bool respondingDoneFromQueue, bool asumeUserHeard, bool sayItPhysically)
        {
            var curBot = this.curBot.BotBehaving; 
            if (!isToplevel)
            {
                curBot.isPerformingOutput = false;
            }
            else
            {
                curBot.isPerformingOutput = true;
            }
            if (Request.IsToplevelRealtimeChat(isToplevel, requestType))
            {
                if (!respondingDoneFromQueue || sayItPhysically)
                {
                    if (isToplevel)
                    {
                        curBot.BotAsUser.JustSaid = answer;
                    }
                }
                if (asumeUserHeard || sayItPhysically)
                {
                    if (isToplevel)
                    {
                        curUser.That = answer;
                        curUser.Predicates.updateSetting("that", answer);
                    }
                }
                if (sayItPhysically)
                {
                    sayResponseToBlackboard(answer);
                    // Mark the output time
                    curBot.myBehaviors.keepTime("lastchatoutput", RunStatus.Success);
                    curBot.myBehaviors.activationTime("lastchatoutput", RunStatus.Success);
                }
                curBot.lastBehaviorChatOutput = answer;
                prologEngine.postListPredToMt("lastoutput", answer, "lastoutputMt");
            }
        }

        /// <summary>
        ///  reactToChat returns void so it expects that sayDelegate is registerd somewhere 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="UserID"></param>
        /// <returns></returns>
        public void reactToChat(string input)
        {
            reactToChat(input, curUser);
        }
        public void reactToChat(string input, string UserID)
        {
            reactToChat(input, curBot.FindOrCreateUser(UserID));
        }
        public void reactToChat(string input, User curUser)
        {
            reactToChat(input, curUser, true, RequestKind.ChatRealTime);
        }
        public void reactToChat(string input, User curUser, bool isToplevel, RequestKind requestType)
        {
            chatWithOutputProcessor(input, curUser, isToplevel, requestType, false, () => { });
        }
        public void Main(string[] args)
        {
            Start(new sayProcessorDelegate(sayResponseToBlackboard));

            while (true)
            {
                User curUser = this.curUser;
                try
                {
                    Console.Write("You (" + curUser.UserNameAndID + "): ");
                    string input = Console.ReadLine();
                    if (input.ToLower() == "quit")
                    {
                        break;
                    }
                    else
                    {
                        MaybeUpdateUserJustSaidLastInput(true, RequestKind.ChatRealTime, curUser, input, false);
                        string answer = respondToChat(input, curUser);
                        Console.WriteLine("Bot: " + answer);
                        sayResponseToBlackboard(answer);
                        MaybeUpdateBotJustSaidLastOutput(true, RequestKind.ChatRealTime, curUser, answer, false, true, false);
                    }
                }
                catch
                { }

            }
        }


        public void startCronEngine()
        {
            DoWithServitorLock(startCronEngine0);
        }
        private void startCronEngine0()
        {
            try
            {
                if (IsRunning(myCronThread)) return; 
                if (myCronThread == null)
                {
                    myCronThread = new Thread(curBot.myCron.start);
                }
                myCronThread.Name = "cron";
                myCronThread.IsBackground = true;
                myCronThread.Start();
            }
            catch (Exception e)
            {
                LogException(e);
            }
        }
        #region FSM
        public  void startFSMEngine()
        {
            if (IsRunning(tmFSMThread)) return;
            //Start our own chem thread
            try
            {
                if (tmFSMThread == null)
                {
                    tmFSMThread = new Thread(memFSMThread);
                }
                tmFSMThread.IsBackground = true;
                tmFSMThread.Start();
            }
            catch (Exception e)
            {
                LogException(e);
            }

        }

        public void memFSMThread()
        {
            WaitUntilLoadCompleted();
            int interval = 1000;
            int tickrate = interval;
            while (true)
            {
                WaitUntilCompletedGlobals();
                Thread.Sleep(interval);
                if (!tmFSMEnabled) continue;
                try
                {
                    if ((curBot.myFSMS != null) && (curBot.isAcceptingUserInput) && mLoadCompleteAndPersonalityShouldBeDefined)
                    {
                        curBot.myFSMS.runBotMachines(curBot);
                    }
                    string tickrateStr = getBBHash("tickrate");
                    tickrate = interval;

                    if (!string.IsNullOrEmpty(tickrateStr)) tickrate = int.Parse(tickrateStr);
                }
                catch
                {
                    tickrate = interval;
                }
                interval = tickrate;
            }

        }
        #endregion
       #region behavior
        public  void startBehaviorEngine()
        {
            //Start our own chem thread
            if (IsRunning(tmBehaveThread)) return;
            try
            {
                if (tmBehaveThread == null)
                {
                    tmBehaveThread = new Thread(memBehaviorThread);
                }
                tmBehaveThread.IsBackground = true;
                tmBehaveThread.Start();
            }
            catch (Exception e)
            {
                LogException(e);
            }

        }

        public  void memBehaviorThread()
        {
            WaitUntilLoadCompleted();
            int interval = 1000;
            int tickrate = interval;
            while (true)
            {
                WaitUntilCompletedGlobals();
                Thread.Sleep(interval);
                if (!tmBehaveEnabled) continue;
                try
                {

                    if ((curBot.myBehaviors != null) && (curBot.isAcceptingUserInput) && mLoadCompleteAndPersonalityShouldBeDefined)
                    {
                        try
                        {
                            //curBot.myBehaviors.runBotBehaviors(curBot);
                            curBot.performBehaviors();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("ERR: {0}\n{1}", e.Message, e.StackTrace);
                        }
                    }
                    string tickrateStr = getBBHash("tickrate");
                    tickrate = interval;

                    if (!string.IsNullOrEmpty(tickrateStr)) tickrate = int.Parse(tickrateStr);
                }
                catch
                {
                    tickrate = interval;
                }
                interval = tickrate;
            }

        }
        #endregion




        public  void startMtalkWatcher()
        {
            if (curBot.myChemistry == null)
            {
                try
                {
                    if (!IsMemcachedRunning(myConst.MEMHOST))
                    {
                        curBot.useMemcache = false;
                        Console.WriteLine("MEMCACHED SERVER IS NOT RUNNING");
                        return;
                    }
                    curBot.realChem = new Qchem(myConst.MEMHOST);
                    curBot.myChemistry = new RChem(myConst.MEMHOST, true);
                    curBot.realChem.prologEngine = curBot.prologEngine;
                    curBot.realChem.prologEngine.chemSysCommandProcessor = curBot.realChem.interepretCmdList;
                    //curBot.realChem.watchMt = "bioLogMt";

                    curBot.myChemTrace = new ChemTrace(curBot.realChem);
                    curBot.myChemTrace.prologEngine = curBot.prologEngine;
                    curBot.realChem.tracer = curBot.myChemTrace;
                    
                }
                catch (Exception e)
                {
                    Console.WriteLine(" - myChem is not operational " + e);
                }
            }
            //Start our own chem thread
            try
            {
                if (IsRunning(tmTalkThread)) return;
                if (tmTalkThread == null)
                {
                    tmTalkThread = new Thread(memTalkThread);
                }
                tmTalkThread.IsBackground = true;
                tmTalkThread.Start();
            }
            catch (Exception e)
            {
                LogException(e);
            }
        }

        private bool IsMemcachedRunning(string ipaddress)
        {
            try
            {
                if (AltBot.MemcachedServerKnownDead) return false;
                var endpoint = new IPEndPoint(IPAddress.Parse(ipaddress), 11211);
                TcpClient tcpClient = new TcpClient();
                tcpClient.Client.Connect(endpoint);
                tcpClient.Close();

                var m_config = new MemcachedClientConfiguration();
                m_config.Servers.Add(endpoint);
                m_config.Protocol = MemcachedProtocol.Text;
                m_config.Transcoder = (ITranscoder)new DCBTranscoder();
                var mc = new MemcachedTestClient((IMemcachedClientConfiguration)m_config);
                var sp = mc.ServerPool;
                MemcachedNode wn = sp.GetWorkingNodes().First() as MemcachedNode;
                if (!wn.Ping()) return false;
                if (!wn.IsAlive) return false;
                if (wn.Acquire() == null)
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogException(e);
                return false;
            }
            return true;
        }

        private class MemcachedTestClient : MemcachedClient
        {
            public MemcachedTestClient(IMemcachedClientConfiguration config)
                : base(config)
            {

            }

            public IServerPool ServerPool   
            {
                get { return Pool; }
            }
        }

        private static bool IsRunning(Thread thread)
        {
            if (thread == null) return false;
            return thread.IsAlive;
        }

        public  bool checkNewPersonality()
        {
            WaitUntilLoadCompleted();
            bool loadedCore = false;
            //return loadedCore; // KHC DEBUG MONOBOT

            lock (curBot)
            {
                if (safeBB())
                {
                    try
                    {
                        WaitUntilCompletedGlobals();
                        string curAIMLClass = getBBHash("aimlclassdir");
                        string curAIMLInstance = getBBHash("aimlinstancedir");
                        if (!(lastAIMLInstance.Contains(curAIMLInstance)))
                        {
                            Console.WriteLine("loadAIMLFromFiles: " + curAIMLClass);
                            Console.WriteLine("loadAIMLFromFiles: " + curAIMLInstance);
                            //curBot.isAcceptingUserInput = false;
                            //myCronThread.Abort(); //.Suspend();
                            //tmFSMThread.Abort(); //.Suspend();
                            //tmBehaveThread.Abort(); //.Suspend();

                            lastAIMLInstance = curAIMLInstance;
                            if (curBot.myCron != null) curBot.myCron.clear();
                            curBot.SizeC = 0;
                            curBot.loadAIMLFromFiles();
                            loadedCore = true;
                            if (Directory.Exists(curAIMLClass)) curBot.loadAIMLFromFiles(curAIMLClass);
                            if (Directory.Exists(curAIMLInstance)) curBot.loadAIMLFromFiles(curAIMLInstance);

                            Console.WriteLine("Load Complete.");

                            curBot.isAcceptingUserInput = true;
                            //startMtalkWatcher();
                            //startFSMEngine();
                            //startBehaviorEngine();
                            //startCronEngine();

                            //myCronThread.Resume();
                            //tmFSMThread.Resume();
                            //tmBehaveThread.Resume();

                            return loadedCore;
                        }
                    }
                    catch (Exception e)
                    {
                        curBot.isAcceptingUserInput = true;
                        Console.WriteLine("Warning:*** Load Incomplete. *** \n {0}\n{1}", e.Message, e.StackTrace);
                    }

                }
                return loadedCore;
            }
        }

        public  void memTalkThread()
        {
            WaitUntilLoadCompleted();
            int interval = 200;
            int lastuutid = 0;
            int uutid = 0;
            //string utterance = "";

            string lastUtterance = "";
            Console.WriteLine("");
            Console.WriteLine("******* MTALK ACTIVE *******");
            Console.WriteLine("");

            while (true)
            {
                WaitUntilCompletedGlobals();
                User LastCurUser = null;
                try
                {
                    Thread.Sleep(interval);
                    //updateTime(); // KHC:DEBUG OFF FOR MONO
                    // Tick the microThreader

                    if (!mLoadCompleteAndPersonalityShouldBeDefined) continue;
                    if (myScheduler != null && tmTalkEnabled)
                    {
                        myScheduler.Run();
                    }
                    var curBot = this.curBot.BotBehaving;
                    if ((curBot != null) && (curBot._outputQueue.Count > 0))
                    {
                        curBot.processOutputQueue();
                        if (this.curBot.isAcceptingUserInput) { curBot.isPerformingOutput = true; }

                    }

                    if (safeBB())
                    {
                        bool newPerson = checkNewPersonality();


                        string sv = null;
                        try
                        {
                            //sv = myChemistry.m_cBus.getHash("mdollhearduuid");
                            sv = getBBHash("uttid");
                            if (!string.IsNullOrEmpty(sv)) uutid = int.Parse(sv);
                        }
                        catch (Exception e) { }
                        if (uutid == lastuutid) { continue; }
                        if (!this.curBot.isAcceptingUserInput) { continue; }
                        try
                        {
                            var curUser = this.curUser;
                            LastCurUser = curUser;

                            lastuutid = uutid;
                            //string myInput = (myChemistry.m_cBus.getHash("mdollheard"));
                            string myInput = (getBBHash("speechhyp"));

                            //Get lastTTS output as <that>
                            // Other output sources may post a short acknowledgement
                            // We want only real sentences or direct yes/no/ok
                            string myThat = (getBBHash("TTSText"));
                            if ((myThat.Length > 4)
                                || (myThat.ToLower().Contains("yes"))
                                || (myThat.ToLower().Contains("no"))
                                || (myThat.ToLower().Contains("ok"))
                                )
                            {
                                curUser.blackBoardThat = myThat;
                            }
                            else
                            {
                                curUser.blackBoardThat = "";
                            }
                            //Get fsmstate output as <state>
                            string myState = (getBBHash("fsmstate"));
                            curUser.Predicates.updateSetting("state", myState);

                            // get values off the blackboard
                            curBot.importBBBot();
                            curBot.importBBUser(curUser);

                            if ((myInput.Length > 0) && (!myInput.Equals(lastUtterance)))
                            {
                                Console.WriteLine("Heard: " + myInput);
                                Request r = new Request(myInput, curUser, curUser.That, curBot, true, RequestKind.MTalkThread);
                                Result res = curBot.Chat(r);
                                string myResp = res.Output;
                                Console.WriteLine("Response: " + myResp);
                                if (myResp == null)
                                {
                                    myResp = "I don't know how to respond.";
                                }
                                myResp = myResp.Replace("_", " ");
                                Console.WriteLine("*** AIMLOUT = '{0}'", myResp);
                                if (!myResp.ToUpper().Contains("IGNORENOP"))
                                {
                                    sayResponseToBlackboard(myResp);
                                    setBBHash("lsaprior", myInput);
                                }

                                lastUtterance = myInput;
                            }
                        }
                        catch (Exception e)
                        {
                            LogException(e);
                        }


                    }
                }
                catch (Exception e)
                {
                    LogException(e);
                }
            }
        }

        public void WaitUntilLoadCompleted()
        {
            lock (ServitorStartStopLoadLock)
            {
                return;
            }
        }
        public void WaitUntilCompletedGlobals()
        {
            WaitUntilLoadCompleted();
        }

        public  void updateTime()
        {
            WaitUntilCompletedGlobals();
            setBBHash("userdate", DateTime.Now.Date.ToString());
            setBBHash("useryear", DateTime.Now.Year.ToString());
            setBBHash("usermonth", DateTime.Now.Month.ToString());
            setBBHash("userday", DateTime.Now.Day.ToString());
            setBBHash("userdayofweek", DateTime.Now.DayOfWeek.ToString());
            setBBHash("usertimeofday", DateTime.Now.TimeOfDay.ToString());
            setBBHash("userhhour", DateTime.Now.Hour.ToString());
            setBBHash("userminute", DateTime.Now.Minute.ToString());
        }

        public bool safeBB()
        {
            //bbSafe=(curBot.myChemistry != null) && (curBot.myChemistry.m_cBus != null);
            return curBot.bbSafe;
        }
        public void setBBHash(string key, string data)
        {
            var curBot = this.curBot.BotBehaving;
            //curbot.bbSetHash(key,data);
            curBot.setBBHash(key, data);
        }
        public string getBBHash(string key)
        {
            try
            {
                var curBot = this.curBot.BotBehaving;
                //BBDict[key] =curbot.bbGetHash(key)
                return curBot.getBBHash(key) ?? "";
            }
            catch
            {
                return "";
            }
        }

        public  void sayResponseToBlackboard(string message)
        {
            if (message == null) return;
            if (!message.ToUpper().Contains("IGNORENOP"))
            {
                //myChemistry.m_cBus.setHash("mdollsay", myResp);
                //myChemistry.m_cBus.setHash("mdollsayuttid", lastuutid.ToString());
                string lastOut = getBBHash("TTSText");
                // avoid repeats
                if (message == lastOut) return;

                setBBHash("TTSText", message);
                Random Rgen = new Random();
           
                int myUUID = Rgen.Next(Int32.MaxValue);
                //curbot.bbSetHash("TTSuuid", lastuutid.ToString());
                setBBHash("TTSuuid", myUUID.ToString());
                Console.WriteLine("sayResponse :{0}:{1}", myUUID.ToString(), message);

                String lsaThat = message.Replace(" ", " TTX");
                lsaThat = lsaThat.Replace(".", " ");
                lsaThat = lsaThat.Replace("?", " ");
                lsaThat = lsaThat.Replace("!", " ");
                lsaThat = lsaThat.Replace(",", " ");
                lsaThat += " " + message;
                setBBHash("lsathat", lsaThat);

            }

        }


        #region Serialization
        public void loadAIMLFromFile(string path)
        {
            Thread.Sleep(10);
            if (skiploadingAimlFiles)
            {
                Warn(" - WARNING: SERVITOR SKIPLOADING: {0}", path);
                return;
            }
            if (curBot != null)
            {
                curBot.loadAIMLFromFiles(path);
            }
            else
            {
                Warn(" - WARNING: SERVITOR NO BOT TO LOAD: {0}", path);
            }
        }
        public void loadAIMLFromFiles(string path)
        {
            Thread.Sleep(10);
            if (skiploadingAimlFiles)
            {
                Warn(" - WARNING: SERVITOR SKIPLOADING: {0}", path);
                return;
            }
            if (curBot != null)
            {
                curBot.loadAIMLFromFiles(path);
            }
            else
            {
                Warn(" - WARNING: SERVITOR NO BOT TO LOAD: {0}", path);
            }
        }

        private static void Warn(string fmt, params object[] args)
        {
            string bad = DLRConsole.SafeFormat(fmt,args);
            Console.WriteLine(bad);
            if (ChatOptions.WarningsAsErrors)
            {
                throw new NullReferenceException(bad);
            }
        }

        /// <summary>
        /// Saves the whole bot to a binary file to avoid processing the AIML each time the 
        /// bot starts
        /// </summary>
        /// <param name="path">the path to the file for saving</param>
        public void saveToBinaryFile(string path)
        {
            Console.WriteLine("START SERVITOR BINARY SAVE:{0}", path);
            if (savedServitor)
            {
                Warn(" - WARNING: PREVIOUS SAVE TO:{0}", path);
            }
            // check to delete an existing version of the file
            FileInfo fi = new FileInfo(path);
            if (fi.Exists)
            {
                Console.WriteLine(" - BINARY SAVE OVERWRITE:{0}", path);
                fi.Delete();
            }
            curBot.saveToBinaryFile(path);
            savedServitor = true;
            Console.WriteLine("SERVITOR BINARY SAVED:{0}", path);
        }

        public void saveToBinaryFile0(string path)
        {
            Console.WriteLine("START SERVITOR BINARY SAVE:{0}", path);
            if (savedServitor)
            {
                Warn(" - WARNING: PREVIOUS SAVE TO:{0}", path);
            }
            // check to delete an existing version of the file
            FileInfo fi = new FileInfo(path);
            if (fi.Exists)
            {
                Console.WriteLine(" - BINARY SAVE OVERWRITE:{0}", path);
                fi.Delete();
            }
            if (curBot.noSerialzation) return;
            FileStream saveFile = File.Create(path);
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(saveFile, curBot);
            saveFile.Close();
            savedServitor = true;
            Console.WriteLine("SERVITOR BINARY SAVED:{0}", path);
        }

        /// <summary>
        /// Loads a dump of whole bot into memory so avoiding processing the AIML files again
        /// </summary>
        /// <param name="path">the path to the dump file</param>
        public void loadFromBinaryFile(string path)
        {
            Console.WriteLine("START SERVITOR BINARY LOAD:{0}", path);
            curBot.loadFromBinaryFile(path);
            Console.WriteLine("SERVITOR BINARY LOAD:{0}", path);
        }
        public void loadFromBinaryFile0(string path)
        {
            Console.WriteLine("START SERVITOR BINARY LOAD:{0}", path);
            FileStream loadFile = File.OpenRead(path);
            BinaryFormatter bf = new BinaryFormatter();
            curBot = (AltBot)bf.Deserialize(loadFile);
            loadFile.Close();
            Console.WriteLine("SERVITOR BINARY LOAD:{0}", path);
        }

        #endregion

        public void shutdown()
        {
            mLoadCompleteAndPersonalityShouldBeDefined = false;
            if (tmTalkThread != null) tmTalkThread.Abort();
            if (tmFSMThread != null) tmFSMThread.Abort();
            if (tmBehaveThread != null) tmBehaveThread.Abort();
            if (myCronThread != null) myCronThread.Abort();
        }

        public static void EnsureBTX(string official)
        {
            string btxStore = Path.Combine(official, "bstore");
            Directory.CreateDirectory(btxStore);
            bool needBtx = true;
            foreach (string fileName in HostSystem.GetFiles(btxStore, "*.btx"))
            {
                needBtx = false;
                break;
            }
            if (needBtx)
            {
                foreach (string fileName in HostSystem.GetFiles(official, "*.aiml"))
                {
                    System.IO.File.SetLastWriteTimeUtc(fileName, DateTime.UtcNow);
                }
            }
        }

        internal bool IsServitorThread(System.Threading.Thread currentThread)
        {
            if(this.tmBehaveThread == currentThread || this.tmFSMThread == currentThread ||
                   this.tmTalkThread == currentThread || myCronThread == currentThread) return true;
            if (WebServitor.listenerThread == currentThread) return true;
            if (myServitorEndpoint != null && myServitorEndpoint.myServer != null)
            {
                if (myServitorEndpoint.myServer.tmPFEndpointThread == currentThread)
                    return true;
            }
            return false;
        }
    }


    /// <summary>
    ///  In a live bot most options should be disgned to be set false or null unless otherwise noted per option comment
    ///     (this is to facilitate that options might be turned on 'true' in a [ThreadStatic] manner)
    /// </summary>
    public class ChatOptions
    {
        public bool SqueltchRepeatedLastOutput = false;
        public static int DebugMicrothreader = 0;

        /// <summary>
        ///  In a live bot these next should should not matter (yet)
        ///     it would be that some behaviour tags that change the users "topic" setting might be set back to original value when the behaviour exits
        ///     of course this means an adoption of a design that allows us to *mark* some settings to remain local to the behavior
        /// </summary>
        public static bool UnwindSomeBotChangesInBehaviors = false;
        public static bool UnwindSomeUserChangesInBehaviors = false;

        public static bool ServitortUserSwitchingLock = false;
        public static bool ServitorInAIMLOnlyTest = false;

        /// <summary>
        /// @TODO @WORKAROUND Currently adding some padding around Template expanded tags
        /// </summary>
        public static bool PadAroundTemplateTags = false;

        /// <summary>
        /// If THINK_RETURN_NULLOK == null it should return THINK_RETURN_DEFAULT;
        /// </summary>
        [ThreadStatic]
        public static string THINK_RETURN_NULLOK = null;
        public static string THINK_RETURN_DEFAULT = "";
        public static string THINK_RETURN
        {
            get
            {
                if (THINK_RETURN_NULLOK == null)
                {
                    return THINK_RETURN_DEFAULT;
                }
                return THINK_RETURN_NULLOK;
            }
        }

        static ChatOptions()
        {
            if (DLRConsole.IsDougsMachine)
            {                
                ServitorInAIMLOnlyTest = true;
                UnwindSomeUserChangesInBehaviors = true;
                AllowRuntimeErrors = false;
                WarningsAsErrors = !ServitorInAIMLOnlyTest;
                WantsToRetraceInAIML = ServitorInAIMLOnlyTest;
            }
        }

        [ThreadStatic]
        public static bool AIML_MAY_USE_FAILURE = false;

        public static string AIML_FAILURE_INDICATOR = null;

        /// <summary>
        ///  In a live bot this should be set false
        /// </summary>
        public static bool UseSraiLimitersBasedOnTextContent = false;

        [ThreadStatic]
        public static bool AIML_TEMPLATE_REEVAL = false;
        [ThreadStatic]
        public static bool AIML_TEMPLATE_CALLS_IMMEDIATE = false;

        /// <summary>
        ///  In a live bot this should be set null
        /// </summary>
        public static GraphMaster OnlyOneGM = null;//new GraphMaster("default");

        /// <summary>
        ///  In a live bot this should be set false
        /// </summary>
        public static bool DeferingSaves = false;

        /// <summary>
        ///  In a live bot this should be set true (a bug requires this for now)
        /// </summary>
        public static bool AlwaysReload = false; //KHC:test true;

        /// <summary>
        ///  In a live bot this should be set false
        /// </summary>
        public static bool AllowRuntimeErrors = false;

        public static bool WarningsAsErrors = false;
        public static bool WantsToRetraceInAIML = false;


        public static bool SeekOutAndRepairAIML { get; set; }
    }
}
