using System;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.ServiceBus;

namespace Globomatics.MessageProcessor
{
    class Program
    {
        static void Main(string[] args)
        {
            var hubName = "iothub-ehub-ps-demo-hu-375407-6fb5327dad";
            var iotHubConnectionString = "Endpoint=sb://ihsuprodblres002dednamespace.servicebus.windows.net/;SharedAccessKeyName=iothubowner;SharedAccessKey=9E1KqYZankE1dYx3oRW6V6ioUxeJgUrJ8uHrFD9fmTA=";
            var storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=psdemostorage01;AccountKey=c94Pe+2fxXC0lWqiUeDa+RWrSWVGGlU2EAQzc0pBJViOjlwQiM1aU6qdzhU2MyMw9KDIq6JK6GWR1p3UqRQeGA==;EndpointSuffix=core.windows.net";
            var storageContainerName = "message-processor-host";
            var consumerGroupName = PartitionReceiver.DefaultConsumerGroupName;

            var processor = new EventProcessorHost(
            hubName,
            consumerGroupName,
            iotHubConnectionString,
            storageConnectionString,
            storageContainerName);

            processor.RegisterEventProcessorAsync<LoggingEventProcessor>().Wait();

            var eventHubConfig = new EventHubConfiguration();
            eventHubConfig.AddEventProcessorHost(hubName, processor);

            var configuration = new JobHostConfiguration(storageConnectionString);
            configuration.UseEventHub(eventHubConfig);

            Console.WriteLine("Starting job host…");
            var host = new JobHost(configuration);
            host.RunAndBlock();

        }
    }
}
