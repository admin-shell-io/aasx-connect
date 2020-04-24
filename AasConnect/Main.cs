using System;
using System.Net;
using System.Text;
using Grapevine.Core;
using Grapevine.Core.Server.Attributes;
using Grapevine.Core.Shared;
using Grapevine.Core.Interfaces.Server;
using Grapevine.Core.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Net.Http;
using System.Threading;
// using Jose;

namespace AasConnect
{
    static public class Aas
    {
        [RestResource]
        public class AuthResource
        {
            [RestRoute(HttpMethod = Grapevine.Core.Shared.HttpMethod.POST, PathInfo = "^/connect(/|)$")]
            public IHttpContext EvalPostConnect(IHttpContext context)
            {
                PostConnect(context);
                return context;
            }

            [RestRoute(HttpMethod = Grapevine.Core.Shared.HttpMethod.POST, PathInfo = "^/disconnect(/|)$")]
            public IHttpContext EvalPostDisconnect(IHttpContext context)
            {
                PostDisconnect(context);
                return context;
            }

            [RestRoute(HttpMethod = Grapevine.Core.Shared.HttpMethod.POST, PathInfo = "^/publish(/|)$")]
            public IHttpContext EvalPostPublish(IHttpContext context)
            {
                PostPublish(context);
                return context;
            }
        }

        static List<string> childs = new List<string> { };

        public static void PostConnect(IHttpContext context)
        {
            string payload = context.Request.Payload;
            var parsed = JObject.Parse(payload);
            string node = "";

            try
            {
                node = parsed.SelectToken("node").Value<string>();
            }
            catch
            {

            }

            if (node != "")
            {
                string connected = "";
                foreach (string value in childs)
                {
                    connected += value + " ";
                }
                childs.Add(node);
                Console.WriteLine("Connect new: " + node + ", already connected: " + connected);
            }
        }

        public static void PostDisconnect(IHttpContext context)
        {
            string payload = context.Request.Payload;
            var parsed = JObject.Parse(payload);
            string node = "";

            try
            {
                node = parsed.SelectToken("node").Value<string>();
            }
            catch
            {

            }

            if (node != "")
            {
                childs.Remove(node);
                Console.WriteLine("Disonnect " + node);
            }
        }

        public static List<string>[] publishRequest = new List<string>[100];
        public static List<string>[] publishResponse = new List<string>[100];
        public static List<string>[] publishResponseChilds = new List<string>[100];

        public class transmit
        {
            public string node;
            public List<string> publish;
            public transmit()
            {
                publish = new List<string> { };
            }
        }

        public static void PostPublish(IHttpContext context)
        {
            string payload = context.Request.Payload;
            // payload: node, publish

            string node = "";
            // string publish = "";

            try
            {
                transmit t1;
                t1 = Newtonsoft.Json.JsonConvert.DeserializeObject<transmit>(context.Request.Payload);

                node = t1.node;
                List<string> publish = t1.publish;
                Console.WriteLine("PostPublish " + node);

                if (node != "" && publish.Count != 0)
                {
                    if (parentDomain != "")
                    {
                        for (int i = 0; i < publishRequest.Length; i++)
                        {
                            if (publishRequest[i] == null)
                            {
                                publishRequest[i] = publish;
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < publishResponse.Length; i++)
                        {
                            if (publishResponse[i] == null)
                            {
                                publishResponse[i] = publish;
                                publishResponseChilds[i] = new List<string> { };
                                foreach (string value in childs)
                                {
                                    publishResponseChilds[i].Add(value);
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            List<string> response = new List<string> { };

            for (int i = 0; i < publishResponse.Length; i++)
            {
                if (publishResponse[i] != null)
                {
                    if (publishResponseChilds[i].Contains(node))
                    {
                        foreach (string value in publishResponse[i])
                        {
                            response.Add(value);
                        }
                        publishResponseChilds[i].Remove(node);
                        if (publishResponseChilds[i].Count == 0)
                        {
                            publishResponse[i] = null;
                        }
                    }
                }
            }

            string responseJson = "";
            if (response.Count != 0)
            {
                transmit t2 = new transmit
                {
                    node = node
                };
                t2.publish = response;

                responseJson = JsonConvert.SerializeObject(t2, Formatting.Indented);
            }

            context.Response.ContentType = ContentType.JSON;
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = responseJson.Length;
            context.Response.SendResponse(responseJson);
        }

        static bool loop = true;
        static long count = 1;

        static int newData = 0;
        public static void ThreadLoop()
        {
            while (loop)
            {
                string publish = "{ \"node\" : \"" + nodeName + "\" , \"count\" : \"" + count + "\" }";

                if (childs.Count == 0)
                {
                    if (++newData == 4)
                    {
                        newData = 0;

                        for (int j = 0; j < publishRequest.Length; j++)
                        {
                            if (publishRequest[j] == null)
                            {
                                publishRequest[j] = new List<string> { };
                                if (nodeNumber == 1)
                                {
                                    publishRequest[j].Add(publish);
                                    break;
                                }
                                if (nodeNumber == 2)
                                {
                                    publishRequest[j].Add(publish);
                                    break;
                                }
                                if (nodeNumber == 3)
                                {
                                    publishRequest[j].Add(publish);
                                    break;
                                }
                            }
                        }
                    }
                }

                if (parentDomain != "")
                {
                    publish = "";
                    transmit t = new transmit
                    {
                        node = nodeName
                    };

                    for (int j = 0; j < publishRequest.Length; j++)
                    {
                        if (publishRequest[j] != null)
                        {
                            foreach (string value in publishRequest[j])
                            {
                                t.publish.Add(value);
                            }
                            publishRequest[j] = null;
                        }
                    }

                    publish = JsonConvert.SerializeObject(t, Formatting.Indented);

                    HttpClient httpClient = new HttpClient();
                    var contentJson = new StringContent(publish, System.Text.Encoding.UTF8, "application/json");

                    var result = httpClient.PostAsync("http://" + parentDomain + "/publish", contentJson).Result;
                    string content = ContentToString(result.Content);

                    if (content != "")
                    {
                        string node = "";
                        string response = "";

                        try
                        {
                            transmit t2;
                            t2 = Newtonsoft.Json.JsonConvert.DeserializeObject<transmit>(content);

                            node = t2.node;
                            List<string> publish2 = t2.publish;

                            // if (node == nodeName)
                            {
                                Console.WriteLine("RECEIVE " + node + " : " + content);
                                if (childs.Count != 0)
                                {
                                    for (int i = 0; i < publishResponse.Length; i++)
                                    {
                                        if (publishResponse[i] == null)
                                        {
                                            publishResponse[i] = publish2;
                                            publishResponseChilds[i] = new List<string> { };
                                            foreach (string value in childs)
                                            {
                                                publishResponseChilds[i].Add(value);
                                            }
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                count += nodeNumber;

                Thread.Sleep(500);
            }
        }

        public static string ContentToString(this HttpContent httpContent)
        {
            var readAsStringAsync = httpContent.ReadAsStringAsync();
            return readAsStringAsync.Result;
        }

        public static int nodeNumber = 0;
        public static string nodeName = "";
        public static string domainName = "";
        public static string parentDomain = "";
        public static void Main(string[] args)
        {
            Console.WriteLine(
            "Copyright(c) 2020 PHOENIX CONTACT GmbH & Co.KG <opensource@phoenixcontact.com>, author: Andreas Orzelski\n" +
            "This software is licensed under the Eclipse Public License 2.0 (EPL - 2.0)\n" +
            "The Newtonsoft.JSON serialization is licensed under the MIT License (MIT)\n" +
            "The Grapevine REST server framework is licensed under Apache License 2.0 (Apache - 2.0)\n" +
            "Jose-JWT is licensed under the MIT license (MIT)\n" +
            "This application is a sample application for demonstration of the features of the Administration Shell.\n" +
            "It is not allowed for productive use. The implementation uses the concepts of the document Details of the Asset\n" +
            "Administration Shell published on www.plattform-i40.de which is licensed under Creative Commons CC BY-ND 3.0 DE."
            );
            Console.WriteLine("--help for available switches.");
            Console.WriteLine("");

            // default command line options
            bool debugwait = false;
            string nodeFile = "NODE.DAT";
            Boolean help = false;

            int i = 0;
            while (i < args.Length)
            {
                var x = args[i].Trim().ToLower();

                if (x == "-node")
                {
                    nodeFile = args[i + 1];
                    Console.WriteLine(args[i] + " " + args[i + 1]);
                    i += 2;
                    continue;
                }

                if (x == "-debugwait")
                {
                    debugwait = true;
                    Console.WriteLine(args[i]);
                    i++;
                    continue;
                }

                if (x == "--help")
                {
                    help = true;
                    break;
                }
            }

            if (help)
            {
                Console.WriteLine("-node Name of Nodefile:\n" + 
                                        "   Line1 = Node#, L2 = Node name,\n" + 
                                        "   L3 = Domainname,\n" + 
                                        "   L4 = Parent Domain");
                Console.WriteLine("-debugwait = wait for Debugger to attach");
                Console.WriteLine("Press ENTER");
                Console.ReadLine();
                return;
            }
            Console.WriteLine("");

            // auf Debugger warten
            if (debugwait)
            {
                Console.WriteLine("Please attach debugger now!");
                while (!System.Diagnostics.Debugger.IsAttached)
                    System.Threading.Thread.Sleep(100);
                Console.WriteLine("Debugger attached");
            }

            try
            {
                using (StreamReader sr = new StreamReader(nodeFile))
                {
                    nodeNumber = Convert.ToInt32(sr.ReadLine());
                    nodeName = sr.ReadLine();
                    domainName = sr.ReadLine();
                    parentDomain = sr.ReadLine();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(nodeFile + " not found!");
                Console.ReadLine();
                return;
            }

            string[] split = domainName.Split(new Char[] { ':' });
            var serverSettings = new ServerSettings
            {
                Host = split[0],
                Port = split[1]
            };

            int j = 0;
            /*
            while (j < publishRequest.Length)
            {
                publishRequest[j] = new List<string> { };
                j++;
            }
            j = 0;
            while (j < publishResponse.Length)
            {
                publishResponse[j] = new List<string> { };
                j++;
            }
            j = 0;
            while (j < publishResponseChilds.Length)
            {
                publishResponseChilds[j] = new List<string> { };
                j++;
            }
            */

            Console.WriteLine("Waiting for client on " + domainName);

            RestServer rs = new RestServer(serverSettings);
            rs.Start();

            HttpClient httpClient = new HttpClient();
            string payload = "{ \"node\" : \"" + nodeName + "\" }";
            var contentJson = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            if (parentDomain != "")
            {
                var result = httpClient.PostAsync("http://" + parentDomain + "/connect", contentJson).Result;
                string content = ContentToString(result.Content);
            }

            Thread t = new Thread(new ThreadStart(ThreadLoop));
            t.Start();

            Console.WriteLine("Press ENTER to STOPP");
            Console.ReadLine();

            loop = false;

            if (parentDomain != "")
            {
                contentJson = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                httpClient.PostAsync("http://" + parentDomain + "/disconnect", contentJson).Wait();
            }

            rs.Stop();
        }
    }
}
