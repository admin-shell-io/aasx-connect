using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using static AasConnect.Aas;

namespace AasConnect
{
    internal static class cdeConnect
    {
        private static Action<TransmitFrame> OnFrameReceived;

        public static bool StartCDEngine(string scope, string route = null, Action<TransmitFrame> pFrameEvent=null)
        {
            if (pFrameEvent != null)
                OnFrameReceived += pFrameEvent;
            TheScopeManager.SetApplicationID("/cVjzPfjlO;{@QMj:jWpW]HKKEmed[llSlNUAtoE`]G?");
            var tBase = new TheBaseApplication();
            TheBaseAssets.MyServiceHostInfo = new TheServiceHostInfo(cdeHostType.Application) //Tells the C-DEngine what host type is used.
            {
                ApplicationName = "AASX CDE Connect", //Friendly Name of Application
                cdeMID = TheCommonUtils.CGuid("{D4377EC0-0E6A-4D6B-BA85-3AC565304732}"),
                Title = "(C) 2020 Connectivity-Labs LLC",
                Description = "CDE Connector for AASX Connect",
                ApplicationTitle = "AASX Portal",
                CurrentVersion = TheCommonUtils.GetAssemblyVersion(tBase),
                MyStationPort=8800,
                MyStationWSPort=8801
            };
            if (!string.IsNullOrEmpty(route))
                TheBaseAssets.MyServiceHostInfo.ServiceRoute = route;
            if (!tBase.StartBaseApplication(null, null))
            {
                Console.WriteLine("Failed to start");
                return false;
            }
            TheScopeManager.SetScopeIDFromEasyID(scope);
            TheBaseEngine.WaitForEnginesStartedAsync().ContinueWith(t =>
            {

                try
                {
                    TheCDEngines.RegisterNewMiniRelay("AASXconnect");
                    var chatEngine = TheThingRegistry.GetBaseEngine("AASXconnect");
                    chatEngine.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
                }
                catch
                {
                }

            });
            return true;
        }

        public static void Publish(TransmitFrame pFrame)
        {
            var tsm = new TSM("AASXconnect", $"PUBLISH:{TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID}:{Guid.NewGuid()}", TheCommonUtils.SerializeObjectToJSONString(pFrame));
            TheCommCore.PublishCentral(tsm);
        }
        public static void Publish(string pFrame)
        {
            if (TheBaseAssets.MyServiceHostInfo?.MyDeviceInfo == null)
                return;
            var tsm = new TSM("AASXconnect", $"PUBLISH:{TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID}:{Guid.NewGuid()}", pFrame);
            TheCommCore.PublishCentral(tsm);
        }

        private static void HandleMessage(ICDEThing arg1, object arg2)
        {
            var msg = (arg2 as TheProcessMessage).Message;
            if (msg?.TXT == null)
                return;

            switch (msg.TXT.Split(':')[0])
            {
                case "PUBLISH":
                        OnFrameReceived?.Invoke(TheCommonUtils.DeserializeJSONStringToObject<TransmitFrame>(msg.PLS));
                    break;
            }
        }
    }
}