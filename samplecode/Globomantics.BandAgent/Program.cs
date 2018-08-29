using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Globomantics.Common;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;

namespace Globomantics.BandAgent
{
    class Program
    {
        private static DeviceClient _device;
        private static TwinCollection _reportedProperties;

        private const string DeviceConnectionString =
            "HostName=ps-demo-hub.azure-devices.net;DeviceId=device-01;SharedAccessKey=RyWAluGf0ag7GQLwM3b2bX7np53CiafBXwLzSZ65BEY=";

        static async Task Main(string[] args)
        {
            Console.WriteLine("Initializing Band Agent...");

            _device = DeviceClient.CreateFromConnectionString(DeviceConnectionString);

            await _device.OpenAsync();
            var receiveEventsTask = ReceiveEvents(_device);
            await _device.SetMethodDefaultHandlerAsync(OtherDeviceMethod, null);
            await _device.SetMethodHandlerAsync("showMessage", ShowMessage, null);

            Console.WriteLine("Device is connected!");

            await UpdateTwin(_device);
            await _device.SetDesiredPropertyUpdateCallbackAsync(UpdateProperties, null);

            Console.WriteLine("Press a key to perform an action:");
            Console.WriteLine("q: quits");
            Console.WriteLine("h: send happy feedback");
            Console.WriteLine("u: send unhappy feedback");
            Console.WriteLine("e: request emergency help");

            var random = new Random();
            var quitRequested = false;
            while (!quitRequested)
            {
                Console.Write("Action? ");
                var input = Console.ReadKey().KeyChar;
                Console.WriteLine();

                var status = StatusType.NotSpecified;
                var latitude = random.Next(0, 100);
                var longitude = random.Next(0, 100);

                switch (Char.ToLower(input))
                {
                    case 'q':
                        quitRequested = true;
                        break;
                    case 'h':
                        status = StatusType.Happy;
                        break;
                    case 'u':
                        status = StatusType.Unhappy;
                        break;
                    case 'e':
                        status = StatusType.Emergency;
                        break;
                }

                var telemetry = new Telemetry
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    Status = status
                };

                var payload = JsonConvert.SerializeObject(telemetry);

                var message = new Message(Encoding.ASCII.GetBytes(payload));

                await _device.SendEventAsync(message);

                Console.WriteLine("Message sent!");
            }

            Console.WriteLine("Disconnecting...");

        }

        private static async Task UpdateTwin(DeviceClient device)
        {
            _reportedProperties = new TwinCollection();
            _reportedProperties["firmwareVersion"] = "1.0";
            _reportedProperties["firmwareUpdateStatus"] = "n/a";

            await device.UpdateReportedPropertiesAsync(_reportedProperties);
        }

        private static async Task ReceiveEvents(DeviceClient device)
        {
            while (true)
            {
                var message = await device.ReceiveAsync();

                if (message == null)
                {
                    continue;
                }

                var messageBody = message.GetBytes();

                var payload = Encoding.ASCII.GetString(messageBody);

                Console.WriteLine($"Received message from cloud: '{payload}'");

                await device.CompleteAsync(message);
            }
        }

        private static Task<MethodResponse> ShowMessage(
            MethodRequest methodRequest,
            object userContext)
        {
            Console.WriteLine("***MESSAGE RECEIVED***");
            Console.WriteLine(methodRequest.DataAsJson);

            var responsePayload = Encoding.ASCII.GetBytes("{\"response\": \"Message shown!\"}");

            return Task.FromResult(new MethodResponse(responsePayload, 200));
        }

        private static Task<MethodResponse> OtherDeviceMethod(
            MethodRequest methodRequest, 
            object userContext)
        {
            Console.WriteLine("****OTHER DEVICE METHOD CALLED****");
            Console.WriteLine($"Method: {methodRequest.Name}");
            Console.WriteLine($"Payload: {methodRequest.DataAsJson}");

            var responsePayload = Encoding.ASCII.GetBytes("{\"response\": \"The method is not implemented!\"}");

            return Task.FromResult(new MethodResponse(responsePayload, 404));
        }

        private static Task UpdateProperties(
            TwinCollection desiredProperties, 
            object userContext)
        {
            var currentFirmwareVersion = (string) _reportedProperties["firmwareVersion"];
            var desiredFirmwareVersion = (string) desiredProperties["firmwareVersion"];

            if (currentFirmwareVersion != desiredFirmwareVersion)
            {
                Console.WriteLine($"Firmware update requested.  Current version: '{currentFirmwareVersion}', " +
                                  $"requested version: '{desiredFirmwareVersion}'");

                ApplyFirmwareUpdate(desiredFirmwareVersion);
            }

            return Task.CompletedTask;
        }

        private static async Task ApplyFirmwareUpdate(string targetVersion)
        {
            Console.WriteLine("Beginning firmware update...");

            _reportedProperties["firmwareUpdateStatus"] = 
                $"Downloading zip file for firmware {targetVersion}...";
            await _device.UpdateReportedPropertiesAsync(_reportedProperties);
            Thread.Sleep(5000);

            _reportedProperties["firmwareUpdateStatus"] = "Unzipping package...";
            await _device.UpdateReportedPropertiesAsync(_reportedProperties);
            Thread.Sleep(5000);

            _reportedProperties["firmwareUpdateStatus"] = "Applying update...";
            await _device.UpdateReportedPropertiesAsync(_reportedProperties);
            Thread.Sleep(5000);

            Console.WriteLine("Firmware update complete!");

            _reportedProperties["firmwareUpdateStatus"] = "n/a";
            _reportedProperties["firmwareVersion"] = targetVersion;
            await _device.UpdateReportedPropertiesAsync(_reportedProperties);
        }
    }
}
