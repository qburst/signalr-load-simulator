using System;

namespace QBurst.SignalR.LoadSimulator.Client
{
    class Program
    {     
        static void Main(string[] args)
        {
            Console.WriteLine("Running loadtest. Press enter key to exit...");

            int START_NUMBER_OF_CLIENTS = 50;
            int MAX_NUMBER_OF_CLIENTS = 500;
            int STEP_NUMBER_OF_CLIENTS = 10;
            int STEP_TIME = 2;
            int MESSAGE_INTERVAL = 10;
            int RUN_DURATION = 600;

            ConnectionManager instance=new ConnectionManager();
            instance.Init("http://localhost:3932/", "timeHub", "broadCastTime", null);            
            instance.GenerateLoad(START_NUMBER_OF_CLIENTS, MAX_NUMBER_OF_CLIENTS, STEP_NUMBER_OF_CLIENTS, 
                STEP_TIME, MESSAGE_INTERVAL, RUN_DURATION);

            Console.ReadLine();
        }

    }    
}
