using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;

namespace QBurst.SignalR.LoadSimulator
{
    /// <summary>
    /// Class that manages the SignalR Hub based connections, created for simulating load.
    /// </summary>
    public class ConnectionManager
    {
        private string url = string.Empty;
        private string hubName = string.Empty;
        private string hubFunction = string.Empty;
        private string[] hubFunctionParamJson;
        private string callbackMethodName = string.Empty;
        private Action<dynamic> callbackMethodAction;
        private int messagingInterval = 30;

        private bool isStopped = false;
        private ConcurrentBag<SignalRHubConnection> connections = new ConcurrentBag<SignalRHubConnection>();

        private readonly string CONNECTIONID = "%%ConnectionId%%";

        public ConnectionManager()
        {
            Debug.WriteLine("Instantiating ConnectionManager");
        }

        /// <summary>
        /// Initialize the ConnectionManager instance with the connection parameters
        /// </summary>
        /// <param name="url">The SignalR Hub url to connect to (no need to provide the ending '/signalr' portion in the url)</param>
        /// <param name="hubName">Name of the Hub to connect to</param>
        /// <param name="hubFunction">Name of the Hub function to invoke</param>
        /// <param name="hubFunctionParamJson">An array of JSON strings that will be passed as input(s) to the Hub function</param>
        public void Init(string url, string hubName, string hubFunction, string[] hubFunctionParamJson)
        {
            Initialize(url, hubName, hubFunction, hubFunctionParamJson, string.Empty, null);
        }

        /// <summary>
        /// Initialize the ConnectionManager instance with the connection parameters
        /// </summary>
        /// <param name="url">The SignalR Hub url to connect to (no need to provide the ending '/signalr' portion in the url)</param>
        /// <param name="hubName">Name of the Hub to connect to</param>
        /// <param name="hubFunction">Name of the Hub function to invoke</param>
        /// <param name="hubFunctionParamJson">An array of JSON strings that will be passed as input(s) to the Hub function</param>       
        /// <param name="callbackMethodName">Name of the method to be invoked on the client, by the Hub (This is optional and need to be provided only in cases where the Hub callback values need to be verified)</param>
        /// <param name="callbackMethodAction">An action delegate which is a pointer to the callback method defined on the client (This is optional and need to be provided only in cases where the Hub callback values need to be verified)</param>
        public void Init(string url, string hubName, string hubFunction, string[] hubFunctionParamJson,
            string callbackMethodName, Action<dynamic> callbackMethodAction)
        {
            Initialize(url, hubName, hubFunction, hubFunctionParamJson,
                callbackMethodName, callbackMethodAction);
        }

        /// <summary>
        /// Generates load as per the inputs specified. Init should be called prior to this call.
        /// </summary>
        /// <param name="startNumberOfClients">Initial number of client connections to start the load simulation</param>
        /// <param name="maxNumberOfClients">Final maximum number of client connections to simulate</param>
        /// <param name="stepClientSize">Step size for increasing the client connections</param>
        /// <param name="stepTimeInSeconds">Time interval between increasing client connections based on stepClientSize parameter</param>
        /// <param name="messagingInterval">A time interval in seconds to say how often the Hub function should be invoked</param>
        /// <param name="loadTestDurationInSeconds">The total duration for running the test</param>
        public async void GenerateLoad(int startNumberOfClients, int maxNumberOfClients,
            int stepClientSize, int stepTimeInSeconds, int messagingInterval, int loadTestDurationInSeconds)
        {
            this.messagingInterval = messagingInterval;

            ValidateInput(startNumberOfClients, maxNumberOfClients,
                stepClientSize, stepTimeInSeconds, loadTestDurationInSeconds);

            Debug.WriteLine(string.Format("GenerateLoad called at {0}", DateTime.Now));
            GenerateLoad(startNumberOfClients, maxNumberOfClients, stepClientSize, stepTimeInSeconds);

            await Task.Delay(loadTestDurationInSeconds * 1000);
            this.Stop();
        }

        /// <summary>
        /// Method to interrupt and stop the load test
        /// </summary>
        public void Stop()
        {
            Debug.WriteLine("ConnectionManager.Stop called");
            this.isStopped = true;
        }

        internal bool IsStopped
        { 
            get { return isStopped; } 
        }

        private void Initialize(string url, string hubName, string hubFunction, string[] hubFunctionParamJson,
            string callbackMethodName, Action<dynamic> callbackMethodAction)
        {
            this.url = url;
            this.hubName = hubName;
            this.hubFunction = hubFunction;
            this.hubFunctionParamJson = hubFunctionParamJson;
            if (!string.IsNullOrEmpty(callbackMethodName))
            {
                this.callbackMethodName = callbackMethodName;
            }
            if(callbackMethodAction != null)
            {
                this.callbackMethodAction = callbackMethodAction;
            }

            Debug.WriteLine("Init : \n\t{0}\n\t{1}\n\t{2}\n\t{3}", this.url, this.hubName,
                this.hubFunction, this.callbackMethodName);
            if (hubFunctionParamJson != null)
            {
                foreach (string parameter in hubFunctionParamJson)
                    Debug.WriteLine("\n\t" + parameter);
            }
        }

        private void ValidateInput(int startNumberOfClients, int maxNumberOfClients,
            int stepClientSize, int stepTimeInSeconds, int loadTestDurationInSeconds)
        {
            StringBuilder validationMessage = new StringBuilder();
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(hubName) || string.IsNullOrEmpty(hubFunction))
                validationMessage.AppendLine("Init should be called before calling GenerateLoad");
            if (!string.IsNullOrEmpty(callbackMethodName))
                if (callbackMethodAction == null)
                {
                    validationMessage.AppendLine("A callback method action delegate should be provided " +
                        "if a callback method name is provided");
                }
            if (startNumberOfClients <= 0)
                validationMessage.AppendLine("Starting number of client connections cannot be less than 1");
            if (maxNumberOfClients <= 0)
                validationMessage.AppendLine("Maximum number of client connections cannot be less than 1");
            if (maxNumberOfClients < startNumberOfClients)
                validationMessage.AppendLine("Maximum number of client connections cannot be less than starting number");
            if (stepClientSize < 0)
                validationMessage.AppendLine("Step size to increase client connections cannot be less than 0");
            if (stepTimeInSeconds < 2)
                validationMessage.AppendLine("Step time to increase client connections cannot be less than 2 second");
            if (loadTestDurationInSeconds < 10)
                validationMessage.AppendLine("Load test duration cannot be less than 10 seconds");

            if(validationMessage.Length>0)
            {
                Debug.WriteLine(validationMessage.ToString());
                throw new Exception(validationMessage.ToString());
            }
        }

        private async void GenerateLoad(int startNumberOfClients, int maxNumberOfClients,
            int stepClientSize, int stepTimeInSeconds)
        {
            ThreadPool.SetMinThreads(maxNumberOfClients, 2);

            Debug.WriteLine("Creating {0} initial connections at {1}", startNumberOfClients, DateTime.Now);
            CreateConnections(startNumberOfClients);

            while (!this.isStopped && stepClientSize > 0 && connections.Count + stepClientSize <= maxNumberOfClients)
            {
                Debug.WriteLine("Wait for {0} seconds before adding more connections", stepTimeInSeconds);
                await Task.Delay(stepTimeInSeconds * 1000);

                Debug.WriteLine("Creating {0} step connections at {1}", stepClientSize, DateTime.Now);
                CreateConnections(stepClientSize);
            }
        }

        private void CreateConnections(int numberOfClients)
        {
            Parallel.For(0, numberOfClients, (i) =>
            {
                try
                {
                    if (this.isStopped)
                        return;

                    SignalRHubConnection connection = null;
                    if (string.IsNullOrEmpty(callbackMethodName))
                        connection = new SignalRHubConnection(this, url, hubName);
                    else
                        connection = new SignalRHubConnection(this, url, hubName, callbackMethodName, callbackMethodAction);

                    string connectionId = connection.ConnectionId;

                    object[] parameters = new object[] { };
                    if (hubFunctionParamJson != null && hubFunctionParamJson.Length > 0)
                    {
                        parameters = new object[hubFunctionParamJson.Length];
                        for (int index = 0; index < hubFunctionParamJson.Length; index++)
                        {
                            hubFunctionParamJson[index] = hubFunctionParamJson[index].Replace(CONNECTIONID, connectionId);
                            dynamic input;
                            if (hubFunctionParamJson[index].Contains("{"))
                                input = JsonConvert.DeserializeObject(hubFunctionParamJson[index]);
                            else
                                input = hubFunctionParamJson[index];
                            parameters[index] = input;
                        }
                    }

                    Debug.WriteLine(string.Format("Connection created : {0} ({1})", connection.ConnectionId,
                        Thread.CurrentThread.GetHashCode()));

                    if (!this.isStopped)
                    {
                        connections.Add(connection);

                        if (messagingInterval > 0)
                            connection.Invoke(hubFunction, messagingInterval, parameters);
                        else
                            connection.Invoke(hubFunction, parameters);
                    }
                }
                catch (Exception exc)
                {
                    Debug.WriteLine("=====Error in CreateConnection=====");
                    do
                    {
                        Debug.WriteLine(string.Format("{0}\n{1}", exc.Message, exc.StackTrace));
                        exc = exc.InnerException;
                    }
                    while (exc != null);
                }
            });
        }
    }
}
