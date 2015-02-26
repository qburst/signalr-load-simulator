using System;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;
using System.Diagnostics;
using System.Threading;

namespace QBurst.SignalR.LoadSimulator
{
    internal class SignalRHubConnection
    {
        private IHubProxy proxy = null;
        private HubConnection connection = null;

        private string url = string.Empty;
        private string hubName = string.Empty;
        private string callbackMethodName = string.Empty;
        private Action<dynamic> callbackMethodAction;
        private ConnectionManager connectionManagerInstance;

        internal SignalRHubConnection(ConnectionManager instance, string url, string hubName)
        {
            this.connectionManagerInstance = instance;
            this.url = url;
            this.hubName = hubName;
            Initialize();
        }

        internal SignalRHubConnection(ConnectionManager instance, string url, string hubName, 
            string callbackMethodName, Action<dynamic> callbackMethodAction)
        {
            this.connectionManagerInstance = instance;
            this.url = url;
            this.hubName = hubName;
            this.callbackMethodName = callbackMethodName;
            this.callbackMethodAction = callbackMethodAction;
            Initialize();
        }

        internal string ConnectionId
        {
            get { return connection.ConnectionId; }
        }

        internal void Invoke(string hubMethodName, params object[] hubMethodParams)
        {
            CallHubMethod(hubMethodName, hubMethodParams);
        }

        internal async void Invoke(string hubMethodName, int interval, params object[] hubMethodParams)
        {
            while (!connectionManagerInstance.IsStopped)
            {
                CallHubMethod(hubMethodName, hubMethodParams);

                Debug.WriteLine("Wait for {0} seconds before invoking again on Connection : {1} (2)",
                    interval, this.ConnectionId, Thread.CurrentThread.GetHashCode());
                await Task.Delay(interval * 1000);
            }
        }

        private void CallHubMethod(string hubMethodName, params object[] hubMethodParams)
        {
            try
            {
                if (!connectionManagerInstance.IsStopped)
                {
                    if (this.connection.State == ConnectionState.Connected)
                    {
                        Debug.WriteLine("Invoking '{0}' on Connection : {1} ({2}) at {3}", hubMethodName,
                            this.ConnectionId, Thread.CurrentThread.GetHashCode(), DateTime.Now);
                        proxy.Invoke(hubMethodName, hubMethodParams);
                    }
                }
            }
            catch (Exception exc)
            {
                Debug.WriteLine("Exception '{0}' occured on Connection : {1}", exc.Message, this.ConnectionId);
            }
        }

        private void Initialize()
        {
            connection = new HubConnection(url, true);         
            proxy = connection.CreateHubProxy(hubName);
            if (callbackMethodName != string.Empty && callbackMethodAction != null)
                proxy.On(callbackMethodName, x => callbackMethodAction(x));
            connection.Start().Wait();
        }
    }
}
