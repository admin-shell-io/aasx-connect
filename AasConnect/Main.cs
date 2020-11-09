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
using System.Runtime.InteropServices;
using Jose;

/*
Copyright(c) 2020 PHOENIX CONTACT GmbH & Co. KG <opensource@phoenixcontact.com>, author: Andreas Orzelski
AASX Connect is licensed under the Apache License 2.0 (Apache-2.0, see below).
*/

namespace AasConnect
{
    static public class Aas
    {
        [RestResource]
        public class AuthResource
        {
            [RestRoute(HttpMethod = Grapevine.Core.Shared.HttpMethod.POST, PathInfo = "^/publish(/|)$")]
            public IHttpContext EvalPostPublish(IHttpContext context)
            {
                PostPublish(context);
                return context;
            }

            [RestRoute(HttpMethod = Grapevine.Core.Shared.HttpMethod.POST, PathInfo = "^/connect(/|)$")]
            public IHttpContext EvalPostConnect(IHttpContext context)
            {
                PostConnect(context);
                return context;
            }

            [RestRoute(HttpMethod = Grapevine.Core.Shared.HttpMethod.GET, PathInfo = "^/directory(/|)$")]
            public IHttpContext EvalGetDirectory(IHttpContext context)
            {
                GetDirectory(context);
                return context;
            }

            [RestRoute(HttpMethod = Grapevine.Core.Shared.HttpMethod.GET, PathInfo = @"^/getaasx/([^/]+)/(\d+)(/|)$")]
            public IHttpContext GetAASX2(IHttpContext context)
            {
                GetAasx(context);
                return context;
            }

            [RestRoute(HttpMethod = Grapevine.Core.Shared.HttpMethod.POST, PathInfo = "^/connectDown(/|)$")]
            public IHttpContext EvalPostConnectDown(IHttpContext context)
            {
                PostConnectDown(context);
                return context;
            }

            [RestRoute(HttpMethod = Grapevine.Core.Shared.HttpMethod.POST, PathInfo = "^/disconnect(/|)$")]
            public IHttpContext EvalPostDisconnect(IHttpContext context)
            {
                PostDisconnect(context);
                return context;
            }

            [RestRoute(HttpMethod = Grapevine.Core.Shared.HttpMethod.POST, PathInfo = "^/publishUp(/|)$")]
            public IHttpContext EvalPostPublishUp(IHttpContext context)
            {
                PostPublishUp(context);
                return context;
            }

            [RestRoute(HttpMethod = Grapevine.Core.Shared.HttpMethod.POST, PathInfo = "^/publishDown(/|)$")]
            public IHttpContext EvalPostPublishDown(IHttpContext context)
            {
                PostPublishDown(context);
                return context;
            }
        }

        // Just publish, no /connect before needed
        public static void PostPublish(IHttpContext context)
        {
            string source = "";
            string responseJson = "";

            try
            {
                TransmitFrame tf = new TransmitFrame();
                tf = Newtonsoft.Json.JsonConvert.DeserializeObject<TransmitFrame>(context.Request.Payload);
                source = tf.source;

                if (!childs.Contains(source))
                    childs.Add(source);

                responseJson = executeTransmitFrames(tf);

                DateTime now = DateTime.UtcNow;
                if (!childsTimeStamps.ContainsKey(source))
                {
                    childsTimeStamps.Add(source, now);
                }
                else
                {
                    childsTimeStamps[source] = now;
                }

                Console.WriteLine(countWriteLine++ + " PostPublish " + source + " " + now);
            }
            catch
            {
            }

            context.Response.ContentType = ContentType.JSON;
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = responseJson.Length;
            context.Response.SendResponse(responseJson);
        }

        public static void checkChildsTimeStamps()
        {
            while (true)
            {
                foreach (string c in childs)
                {
                    if (childsTimeStamps.ContainsKey(c))
                    {
                        DateTime now = DateTime.UtcNow;
                        DateTime last = childsTimeStamps[c];
                        TimeSpan difference = now.Subtract(last);
                        if (difference.TotalSeconds > 20)
                        {
                            Console.WriteLine(countWriteLine++ + " Remove Child " + c + " " + now + "," + last + "," + difference);
                            childs.Remove(c);
                            break;
                        }
                    }
                }

                Thread.Sleep(10000);
            }
        }

        public static void PostConnect(IHttpContext context)
        {
            string payload = context.Request.Payload;
            var parsed = JObject.Parse(payload);
            string source = "";

            string ret = "ERROR";

            try
            {
                source = parsed.SelectToken("source").Value<string>();
            }
            catch
            {

            }

            if (source != "")
            {
                string connected = "";
                foreach (string value in childs)
                {
                    connected += value + " ";
                }
                childs.Add(source);
                Console.WriteLine(countWriteLine++ + " Connect new: " + source + ", already connected: " + connected);

                ret = "OK";
            }

            context.Response.ContentType = ContentType.TEXT;
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = ret.Length;
            context.Response.SendResponse(ret);
        }

        public static void GetDirectory(IHttpContext context)
        {
            // string payload = context.Request.Payload;
            // var parsed = JObject.Parse(payload);

            string responseJson = JsonConvert.SerializeObject(aasDirectory, Formatting.Indented);

            context.Response.ContentType = ContentType.JSON;
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = responseJson.Length;
            context.Response.SendResponse(responseJson);
        }

        public static void GetAasx(IHttpContext context)
        {
            string ret = "ERROR";

            if (context.Request.PathParameters.Count == 3)
            {
                string path = context.Request.PathInfo;
                string[] split = path.Split('/');
                string node = split[2];
                string aasIndex = split[3];

                TransmitData td = new TransmitData();
                td.source = sourceName;
                td.destination = node;
                td.type = "getaasx";
                td.extensions = aasIndex;

                // publishRequest.Add(td);

                if (parentDomain != "GLOBALROOT")
                {
                    publishRequest.Add(td);
                }
                else
                {
                    for (int i = 0; i < publishResponse.Length; i++)
                    {
                        if (publishResponse[i] == null)
                        {
                            publishResponse[i] = new List<TransmitData> { };
                            publishResponse[i].Add(td);
                            if (childs.Count != 0)
                            {
                                publishResponseChilds[i] = new List<string> { };
                                foreach (string value in childs)
                                {
                                    publishResponseChilds[i].Add(value);
                                }
                            }
                            break;
                        }
                    }
                }


                ret = "Downloaded .AASX from node " + node + " at index " + aasIndex + " will be stored in user download directory!";
            }

            context.Response.ContentType = ContentType.TEXT;
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = ret.Length;
            context.Response.SendResponse(ret);
        }

        public static void PostConnectDown(IHttpContext context)
        {
            string payload = context.Request.Payload;
            var parsed = JObject.Parse(payload);
            string source = "";
            string ret = "**ERROR**";

            try
            {
                source = parsed.SelectToken("source").Value<string>();
            }
            catch
            {

            }

            if (source != "")
            {
                Console.WriteLine(countWriteLine++ + " ConnectDown: " + source);

                ret = sourceName;
            }

            context.Response.ContentType = ContentType.TEXT;
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = ret.Length;
            context.Response.SendResponse(ret);
        }

        public static void PostDisconnect(IHttpContext context)
        {
            string payload = context.Request.Payload;
            var parsed = JObject.Parse(payload);
            string source = "";
            string ret = "ERROR";

            try
            {
                source = parsed.SelectToken("source").Value<string>();
            }
            catch
            {

            }

            if (source != "")
            {
                childs.Remove(source);
                Console.WriteLine(countWriteLine++ + " Disonnect " + source);

                ret = "OK";
            }

            context.Response.ContentType = ContentType.TEXT;
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = ret.Length;
            context.Response.SendResponse(ret);
        }

        public static string secretString = "Industrie4.0-Asset-Administration-Shell";

        public static void PostPublishUp(IHttpContext context)
        {
            string source = "";
            string responseJson = "";

            try
            {
                TransmitFrame tf = new TransmitFrame();
                tf = Newtonsoft.Json.JsonConvert.DeserializeObject<TransmitFrame>(context.Request.Payload);
                source = tf.source;

                Console.WriteLine(countWriteLine++ + " PostPublishUp " + source);

                responseJson = executeTransmitFrames(tf);
            }
            catch
            {
            }

            context.Response.ContentType = ContentType.JSON;
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = responseJson.Length;
            context.Response.SendResponse(responseJson);
        }

        public static string executeTransmitFrames(TransmitFrame tf)
        {
            string source = tf.source;

            if (source != "" && tf.data.Count != 0)
            {
                foreach (TransmitData td in tf.data)
                {
                    if (td.type == "getaasxFile" && td.destination == sourceName)
                    {
                        var parsed3 = JObject.Parse(td.publish[0]);

                        string fileName = parsed3.SelectToken("fileName").Value<string>();
                        string fileData = parsed3.SelectToken("fileData").Value<string>();

                        var enc = new System.Text.ASCIIEncoding();
                        var fileString4 = Jose.JWT.Decode(fileData, enc.GetBytes(secretString), JwsAlgorithm.HS256);
                        var parsed4 = JObject.Parse(fileString4);

                        string binaryBase64_4 = parsed4.SelectToken("file").Value<string>();
                        Byte[] fileBytes4 = Convert.FromBase64String(binaryBase64_4);

                        string downloadDir = Environment.GetEnvironmentVariable("USERPROFILE") + @"\" + "Downloads";
                        Console.WriteLine("Writing file: " + downloadDir + "/" + fileName);
                        File.WriteAllBytes(downloadDir + "/" + fileName, fileBytes4);

                        tf.data.Remove(td);
                    }
                    else
                    {
                        if (parentDomain != "GLOBALROOT")
                        {
                            publishRequest.Add(td);
                        }
                        if (parentDomain == "GLOBALROOT")
                        {
                            if (td.type == "directory")
                            {
                                aasDirectoryParameters adp = new aasDirectoryParameters();

                                try
                                {
                                    adp = Newtonsoft.Json.JsonConvert.DeserializeObject<aasDirectoryParameters>(td.publish[0]);
                                }
                                catch
                                {
                                }
                                aasDirectory.Add(adp);
                                tf.data.Remove(td);
                            }
                            // copy publish request into response
                            for (int i = 0; i < publishResponse.Length; i++)
                            {
                                if (publishResponse[i] == null)
                                {
                                    publishResponse[i] = tf.data;
                                    if (childs.Count != 0)
                                    {
                                        publishResponseChilds[i] = new List<string> { };
                                        foreach (string value in childs)
                                        {
                                            publishResponseChilds[i].Add(value);
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            TransmitFrame response = new TransmitFrame();
            response.source = sourceName;

            for (int i = 0; i < publishResponse.Length; i++)
            {
                if (publishResponse[i] != null)
                {
                    if (publishResponseChilds[i] != null)
                    {
                        if (publishResponseChilds[i].Contains(source))
                        {
                            foreach (TransmitData td in publishResponse[i])
                            {
                                response.data.Add(td);
                            }
                            publishResponseChilds[i].Remove(source);
                            if (publishResponseChilds[i].Count == 0)
                            {
                                publishResponse[i] = null;
                            }
                        }
                    }
                }
            }

            string responseJson = "";
            if (response.data.Count != 0)
            {
                responseJson = JsonConvert.SerializeObject(response, Formatting.Indented);
            }

            return responseJson;
        }

        public static void PostPublishDown(IHttpContext context)
        {
            string payload = context.Request.Payload;
            // payload: source, publish

            string source = "";
            // string publish = "";

            try
            {
                TransmitFrame tf = new TransmitFrame();
                tf = Newtonsoft.Json.JsonConvert.DeserializeObject<TransmitFrame>(context.Request.Payload);
                source = tf.source;

                Console.WriteLine(countWriteLine++ + " PostPublishDown " + source + " -> " + sourceName);

                if (source != "" && tf.data.Count != 0 && childs.Count != 0)
                {
                    for (int i = 0; i < publishResponse.Length; i++)
                    {
                        if (publishResponse[i] == null)
                        {
                            publishResponse[i] = tf.data;
                            if (childs.Count != 0)
                            {
                                publishResponseChilds[i] = new List<string> { };
                                foreach (string value in childs)
                                {
                                    publishResponseChilds[i].Add(value);
                                }
                            }
                            break;
                        }
                    }
                }
            }
            catch
            {
            }

            string responseJson = "";
            if (publishRequest.Count != 0)
            {
                TransmitFrame response = new TransmitFrame();
                response.source = sourceName;
                response.data = publishRequest;

                responseJson = JsonConvert.SerializeObject(response, Formatting.Indented);
                publishRequest.Clear();
            }

            context.Response.ContentType = ContentType.JSON;
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = responseJson.Length;
            context.Response.SendResponse(responseJson);
        }

        public static void ThreadLoop()
        {
            while (loop)
            {
                // testing
                if (test)
                {
                    string testPublish = "{ \"source\" : \"" + sourceName + "\" , \"count\" : \"" + count + "\" }";
                    TransmitData td = new TransmitData();
                    td.source = sourceName;
                    td.publish.Add(testPublish);

                    if (childs.Count == 0)
                    {
                        if (++newData == 4)
                        {
                            newData = 0;
                            if (sourceName == "TEST1" || sourceName == "TEST2" || sourceName == "TEST3")
                            {
                                publishRequest.Add(td);
                            }
                        }
                    }

                    count += 5;
                }

                if (parentDomain != "GLOBALROOT" && parentDomain != "LOCALROOT")
                {
                    TransmitFrame tf = new TransmitFrame();
                    tf.source = sourceName;

                    string publish = JsonConvert.SerializeObject(tf, Formatting.Indented);

                    if (publishRequest.Count != 0)
                    {
                        tf.data = publishRequest;
                        publish = JsonConvert.SerializeObject(tf, Formatting.Indented);
                        publishRequest.Clear();
                    }

                    HttpClient httpClient2;
                    if (clientHandler != null)
                    {
                        httpClient2 = new HttpClient(clientHandler);
                    }
                    else
                    {
                        httpClient2 = new HttpClient();
                    }

                    var contentJson = new StringContent(publish, System.Text.Encoding.UTF8, "application/json"); ;

                    string content = "";
                    try
                    {
                        var result = httpClient2.PostAsync(parentDomain + "/publishUp", contentJson).Result;
                        content = ContentToString(result.Content);
                    }
                    catch
                    {

                    }

                    if (content != "")
                    {
                        string source = "";
                        // string response = "";

                        try
                        {
                            TransmitFrame tf2 = new TransmitFrame();
                            tf2 = Newtonsoft.Json.JsonConvert.DeserializeObject<TransmitFrame>(content);

                            source = tf2.source;

                            // if (source == sourceName)
                            {
                                Console.WriteLine(countWriteLine++ + " RECEIVE PostPublishUp " + source + " : " + content);
                                if (childs.Count != 0)
                                {
                                    for (int i = 0; i < publishResponse.Length; i++)
                                    {
                                        if (publishResponse[i] == null)
                                        {
                                            publishResponse[i] = tf2.data;
                                            if (childs.Count != 0)
                                            {
                                                publishResponseChilds[i] = new List<string> { };
                                                foreach (string value in childs)
                                                {
                                                    publishResponseChilds[i].Add(value);
                                                }
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

                // pass data down to/from local root childs
                if (childDomainsCount != 0)
                {
                    TransmitFrame response = new TransmitFrame();
                    response.source = sourceName;

                    for (int i = 0; i < publishResponse.Length; i++)
                    {
                        if (publishResponse[i] != null)
                        {
                            response.data = publishResponse[i];
                            for (int j = 0; j < childDomainsCount; j++)
                            {
                                if (publishResponseChilds[i] != null)
                                {
                                    if (publishResponseChilds[i].Contains(childDomainsNames[j]))
                                    {
                                        publishResponseChilds[i].Remove(childDomainsNames[j]);
                                        if (publishResponseChilds[i].Count == 0)
                                        {
                                            publishResponse[i] = null;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    string responseJson = "";
                    responseJson = JsonConvert.SerializeObject(response, Formatting.Indented);

                    HttpClient httpClient;
                    httpClient = new HttpClient();
                    /*
                    if (clientHandler != null)
                    {
                        httpClient = new HttpClient(clientHandler);
                    }
                    else
                    {
                        httpClient = new HttpClient();
                    }
                    */

                    for (int i = 0; i < childDomainsCount; i++)
                    {
                        var contentJson = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json"); ;
                        
                        string content = "";
                        try
                        {
                            var result = httpClient.PostAsync(childDomains[i] + "/publishDown", contentJson).Result;
                            content = ContentToString(result.Content);
                        }
                        catch
                        {

                        }

                        if (content != "")
                        {
                            try
                            {
                                TransmitFrame tf1 = new TransmitFrame();
                                tf1 = Newtonsoft.Json.JsonConvert.DeserializeObject<TransmitFrame>(content);

                                string source = tf1.source;
                                // Console.WriteLine(countWriteLine++ + " RECEIVE PostPublishDown " + source + " : " + content);
                                Console.WriteLine(countWriteLine++ + " RECEIVE PostPublishDown " + source);

                                if (tf1.data.Count != 0)
                                {
                                    foreach (TransmitData td in tf1.data)
                                    {
                                        publishRequest.Add(td);
                                    }
                                    if (parentDomain == "GLOBALROOT")
                                    {
                                        // copy publish request into response
                                        for (int j = 0; j < publishResponse.Length; j++)
                                        {
                                            if (publishResponse[j] == null)
                                            {
                                                publishResponse[j] = tf1.data;
                                                if (childs.Count != 0)
                                                {
                                                    publishResponseChilds[j] = new List<string> { };
                                                    foreach (string value in childs)
                                                    {
                                                        publishResponseChilds[j].Add(value);
                                                    }
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
                }

                Thread.Sleep(500);
            }
        }

        public static string ContentToString(this HttpContent httpContent)
        {
            var readAsStringAsync = httpContent.ReadAsStringAsync();
            return readAsStringAsync.Result;
        }

        public class TransmitData
        {
            public string source;
            public string destination;
            public string type;
            public string encrypt;
            public string extensions;
            public List<string> publish;
            public TransmitData()
            {
                publish = new List<string> { };
            }
        }

        public class TransmitFrame
        {
            public string source;
            public List<TransmitData> data;
            public TransmitFrame()
            {
                data = new List<TransmitData> { };
            }
        }
        public class aasListParameters
        {
            public int index;
            public string idShort;
            public string identification;
            public string fileName;
        }
        public class aasDirectoryParameters
        {
            public string source;
            public List<aasListParameters> aasList;
            public aasDirectoryParameters()
            {
                aasList = new List<aasListParameters> { };
            }
        }

        static List<aasDirectoryParameters> aasDirectory = new List<aasDirectoryParameters> { };

        static List<string> childs = new List<string> { };
        static Dictionary<string, DateTime> childsTimeStamps = new Dictionary<string, DateTime>();

        public static List<TransmitData> publishRequest = new List<TransmitData> { };
        public static List<TransmitData>[] publishResponse = new List<TransmitData>[1000];
        public static List<string>[] publishResponseChilds = new List<string>[1000];

        static bool loop = true;
        static long count = 1;
        static long countWriteLine = 0;

        static bool test = false;
        static int newData = 0;

        public static string sourceName = "";
        public static string domainName = "";
        public static string parentDomain = "";
        public static string[] childDomains = new string[100];
        public static string[] childDomainsNames = new string[100];
        public static int childDomainsCount = 0;

        public static WebProxy proxy = null;
        public static HttpClientHandler clientHandler = null;

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

            if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                AppContext.SetSwitch("System.Net.Http.UseSocketsHttpHandler", false);
            }

            // default command line options
            bool debugwait = false;
            string sourceFile = "";
            string proxyFile = "";
            Boolean help = false;

            int i = 0;
            while (i < args.Length)
            {
                var x = args[i].Trim().ToLower();

                if (x == "-node")
                {
                    sourceFile = args[i + 1];
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

                if (x == "-proxy")
                {
                    proxyFile = args[i + 1];
                    Console.WriteLine(args[i] + " " + args[i + 1]);
                    i += 2;
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
                Console.WriteLine("-node Name of node file:\n" + 
                                        "   Line1 = SourceName,\n" + 
                                        "   L2 = Domainname,\n" +
                                        "   L3 = ParentDomain or GLOBALROOT or LOCALROOT,\n" +
                                        "   L4* = ChildDomains to be called from above"
                                  );
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

            // Proxy
            string proxyAddress = "";
            string username = "";
            string password = "";

            if (proxyFile != "")
            {
                if (!File.Exists(proxyFile))
                {
                    Console.WriteLine(proxyFile + " not found!");
                    Console.ReadLine();
                    return;
                }

                try
                {
                    using (StreamReader sr = new StreamReader(proxyFile))
                    {
                        proxyAddress = sr.ReadLine();
                        username = sr.ReadLine();
                        password = sr.ReadLine();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(proxyFile + " not found!");
                }

                if (proxyAddress != "")
                {
                    proxy = new WebProxy();
                    Uri newUri = new Uri(proxyAddress);
                    proxy.Address = newUri;
                    proxy.Credentials = new NetworkCredential(username, password);
                    // proxy.BypassProxyOnLocal = true;
                    Console.WriteLine("Using proxy: " + proxyAddress);

                    clientHandler = new HttpClientHandler
                    {
                        Proxy = proxy,
                        UseProxy = true
                    };
                }
            };

            if (!File.Exists(sourceFile))
            {
                Console.WriteLine(sourceFile + " not found!");
                Console.ReadLine();
                return;
            }

            try
            {
                using (StreamReader sr = new StreamReader(sourceFile))
                {
                    // sourceNumber = Convert.ToInt32(sr.ReadLine());
                    sourceName = sr.ReadLine();
                    domainName = sr.ReadLine();
                    parentDomain = sr.ReadLine();

                    string localChild = "";
                    do
                    {
                        localChild = sr.ReadLine();
                        if (localChild != "")
                        {
                            childDomains[childDomainsCount++] = localChild;
                        }
                    }
                    while (localChild != "");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(sourceFile + " not found!");
                Console.ReadLine();
                return;
            }

            bool https = false;
            string[] split1 = domainName.Split(new Char[] { '/' });
            if (split1[0].ToLower() == "https:")
            {
                https = true;
            }
            string[] split2 = split1[2].Split(new Char[] { ':' });
            var serverSettings = new ServerSettings
            {
                Host = split2[0],
                Port = split2[1],
                UseHttps = https
            };

            Console.WriteLine("Waiting for client on " + domainName);

            RestServer rs = new RestServer(serverSettings);
            rs.Start();

            HttpClient httpClient;
            string payload = "{ \"source\" : \"" + sourceName + "\" }";
            var contentJson = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            if (parentDomain != "GLOBALROOT" && parentDomain != "LOCALROOT")
            {
                if (clientHandler != null)
                {
                    httpClient = new HttpClient(clientHandler);
                }
                else
                {
                    httpClient = new HttpClient();
                }

                try
                {
                    var result = httpClient.PostAsync(parentDomain + "/connect", contentJson).Result;
                    string content = ContentToString(result.Content);
                }
                catch
                {
                    Console.WriteLine("Can not /connect");
                }
            }

            for (i = 0; i < childDomainsCount; i++)
            {
                httpClient = new HttpClient();

                payload = "{ \"source\" : \"" + sourceName + "\" }";
                contentJson = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

                string content = "";
                try
                {
                    var result = httpClient.PostAsync(childDomains[i] + "/connectDown", contentJson).Result;
                    content = ContentToString(result.Content);
                }
                catch
                {

                }

                if (content != "")
                {
                    Console.WriteLine("ConnectDown " + childDomains[i] + " : " + content);
                    childDomainsNames[i] = content;
                    childs.Add(content);
                }
            }

            Thread t = new Thread(new ThreadStart(ThreadLoop));
            t.Start();

            Thread t2 = new Thread(new ThreadStart(checkChildsTimeStamps));
            t2.Start();

            Console.WriteLine("Press CTRL-C to STOPP");
            // Console.ReadLine();
            ManualResetEvent quitEvent = new ManualResetEvent(false);
            try
            {
                Console.CancelKeyPress += (sender, eArgs) =>
                {
                    quitEvent.Set();
                    eArgs.Cancel = true;
                };
            }
            catch
            {
            }
            // wait for timeout or Ctrl-C
            quitEvent.WaitOne(Timeout.Infinite);

            loop = false;

            if (parentDomain != "GLOBALROOT" && parentDomain != "LOCALROOT")
            {
                if (clientHandler != null)
                {
                    httpClient = new HttpClient(clientHandler);
                }
                else
                {
                    httpClient = new HttpClient();
                }

                try
                {
                    contentJson = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                    httpClient.PostAsync(parentDomain + "/disconnect", contentJson).Wait();
                }
                catch
                {
                    Console.WriteLine("Can not /disconnect. Press ENTER");
                    Console.ReadLine();
                }
            }

            rs.Stop();
        }
    }
}
