using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Configuration;
using System.Data;
using Newtonsoft.Json;
using Microsoft.Azure.Devices.Client;

namespace VeraIoTIntegration
{
    class Program
    {
        static DeviceClient deviceClient;
        static void Main(string[] args)
        {

            // vera uri should like "http://10.1.1.150:3480/data_request?id=status&output_format=json"

            string veraip = ConfigurationSettings.AppSettings.Get("veraip");
            string devicesfile = ConfigurationSettings.AppSettings.Get("devicesFile");
            string iotHubUri = ConfigurationSettings.AppSettings.Get("iotHubUri");
            string deviceKey = ConfigurationSettings.AppSettings.Get("deviceKey");
            string deviceName = ConfigurationSettings.AppSettings.Get("deviceName");
            int frequency = int.Parse(ConfigurationSettings.AppSettings.Get("frequency"));
            JArray devicesData;
            DataTable devicesList;

            Console.WriteLine("Loading Application...");

            try
            {
                deviceClient = DeviceClient.Create(iotHubUri, new DeviceAuthenticationWithRegistrySymmetricKey(deviceName, deviceKey));
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to connect to Azure IoT Service!");
                Console.WriteLine(e.ToString());
            }
            try
            {
                devicesList = getDevicesFromCsv(devicesfile);


                JObject msg = new JObject();


                while (true)
                {
                    Console.WriteLine("Getting data from Vera...");
                    try
                    {
                        devicesData = getDeviceDatafromVera(veraip);

                        msg = new JObject();
                        msg.Add("id", deviceName);
                        msg.Add("Time", DateTime.Now.ToLocalTime().ToString());
                        msg.Add("UtcTime", DateTime.UtcNow.ToString("o"));
                        //// 

                        foreach (DataRow row in devicesList.Rows)
                        {
                            int deviceid = int.Parse(row[0].ToString());
                            string sensorDeviceName = row[1].ToString().Replace(' ', '_');
                            string sensorDeviceType = row[2].ToString();

                            switch (sensorDeviceType)
                            {
                                case "MotionSensor":
                                    msg.Add(sensorDeviceName, getMotionSensorFromDevice(devicesData, deviceid));
                                    break;
                                case "TemperatureSensor":
                                    msg.Add(sensorDeviceName, getTemperatureFromDevice(devicesData, deviceid));
                                    break;
                                case "SmartSwitch":
                                    msg.Add(sensorDeviceName + "_Status", getSwitchStatusFromSmartSwitch(devicesData, deviceid));
                                    msg.Add(sensorDeviceName + "_Watts", getPowerConsumptiomFromSmartSwitch(devicesData, deviceid));
                                    break;
                                case "FibaroDimmer":
                                    msg.Add(sensorDeviceName + "_Status", getFibaroStatus(devicesData, deviceid));
                                    msg.Add(sensorDeviceName + "_Watts", getFibaroConsumption(devicesData, deviceid));
                                    break;
                                case "DoorSensor":
                                    msg.Add(sensorDeviceName, getDoorSensorStatusFromDevice(devicesData, deviceid));
                                    break;
                                case "HueLight":
                                    msg.Add(sensorDeviceName, getHueLightStatusFromDevice(devicesData, deviceid));
                                    break;
                                case "LightSensor":
                                    msg.Add(sensorDeviceName, getLightSensorFromDevice(devicesData, deviceid));
                                    break;
                                case "HumiditySensor":
                                    msg.Add(sensorDeviceName, getHumidityFromDevice(devicesData, deviceid));
                                    break;
                                default:

                                    break;
                            }

                        }

                        msg.Add("Home_Status", getHomeStatus(devicesData));

                        sendDeviceToCloudMessagesAsync(msg);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Unable to Read data from Vera Device! Make sure Vera is responsive!");
                        Console.WriteLine(e.ToString());
                    }

                    Task.Delay(frequency).Wait();

                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to Read the Devices from the CSV file! Make sure the file is formatted correctly");
                Console.WriteLine(e.ToString());
            }

            Console.ReadLine();

        }

        // get home status
        static string getHomeStatus(JArray devices)
        {
            int deviceid = 32;
            string status = "Home";
            int trip = 0;
            foreach (JObject device in devices)
            {
                if ((int)device["id"] == deviceid)
                {

                    foreach (JObject state in (JArray)device["states"])
                    {
                        if (state["variable"].ToString() == "Armed")
                        {

                            trip = int.Parse(state["value"].ToString());
                            if (trip == 0)
                            {
                                status = "Home";
                            }
                            else
                            {
                                status = "Armed";
                            }
                        }
                    }
                    break;
                }
            }
            // return the trip value
            return status;
        }

        // get door sensor status
        static int getDoorSensorStatusFromDevice(JArray devices, int deviceid)
        {
            int trip = 0;
            foreach (JObject device in devices)
            {
                if ((int)device["id"] == deviceid)
                {
                    foreach (JObject state in (JArray)device["states"])
                    {
                        if (state["variable"].ToString() == "Tripped")
                        {
                            trip = int.Parse(state["value"].ToString());
                        }
                    }
                    break;
                }
            }
            // return the trip value
            return trip;
        }

        // get door sensor status
        static int getHueLightStatusFromDevice(JArray devices, int deviceid)
        {
            int status = 0;
            foreach (JObject device in devices)
            {
                if ((int)device["id"] == deviceid)
                {
                    foreach (JObject state in (JArray)device["states"])
                    {
                        if (state["variable"].ToString() == "Status")
                        {
                            status = int.Parse(state["value"].ToString());
                        }
                    }
                    break;
                }
            }
            // return the trip value
            return status;
        }

        // get the temperature from the sensor device
        static double getTemperatureFromDevice(JArray devices, int deviceid)
        {
            double temperature = 0.0;
            foreach (JObject device in devices)
            {
                if ((int)device["id"] == deviceid)
                {
                    foreach (JObject state in (JArray)device["states"])
                    {
                        if (state["variable"].ToString() == "CurrentTemperature")
                        {
                            temperature = double.Parse(state["value"].ToString());
                        }
                    }
                    break;
                }
            }
            // return the temperature value
            return temperature;
        }

        // get the motion trip from the sensor device
        static int getMotionSensorFromDevice(JArray devices, int deviceid)
        {
            int trip = 0;
            foreach (JObject device in devices)
            {
                if ((int)device["id"] == deviceid)
                {

                    foreach (JObject state in (JArray)device["states"])
                    {
                        if (state["variable"].ToString() == "Tripped")
                        {

                            trip = int.Parse(state["value"].ToString());
                        }
                    }
                    break;
                }
            }
            // return the trip value
            return trip;
        }

        // get the light value from the sensor device
        static int getLightSensorFromDevice(JArray devices, int deviceid)
        {
            int lux = 0;
            foreach (JObject device in devices)
            {
                if ((int)device["id"] == deviceid)
                {

                    foreach (JObject state in (JArray)device["states"])
                    {
                        if (state["variable"].ToString() == "CurrentLevel")
                        {

                            lux = int.Parse(state["value"].ToString());
                        }
                    }
                    break;
                }
            }
            // return the lux value
            return lux;
        }

        // get the humidity from the sensor device
        static int getHumidityFromDevice(JArray devices, int deviceid)
        {
            int humdity = 0;
            foreach (JObject device in devices)
            {
                if ((int)device["id"] == deviceid)
                {

                    foreach (JObject state in (JArray)device["states"])
                    {
                        if (state["variable"].ToString() == "CurrentLevel")
                        {

                            humdity = int.Parse(state["value"].ToString());
                            break;
                        }
                    }
                    break;
                }
            }
            // return the humidity value
            return humdity;
        }

        // get the power consumption from smart switch 6
        static double getPowerConsumptiomFromSmartSwitch(JArray devices, int deviceid)
        {
            double watts = 0.0;
            foreach (JObject device in devices)
            {
                if ((int)device["id"] == deviceid)
                {

                    foreach (JObject state in (JArray)device["states"])
                    {
                        if (state["variable"].ToString() == "Watts")
                        {

                            watts = double.Parse(state["value"].ToString());
                        }
                    }
                    break;
                }
            }
            // return the temperature value
            return watts;
        }

        // get the switch status from smart switch 6
        static int getSwitchStatusFromSmartSwitch(JArray devices, int deviceid)
        {
            int switchstatus = 0;
            foreach (JObject device in devices)
            {
                if ((int)device["id"] == deviceid)
                {

                    foreach (JObject state in (JArray)device["states"])
                    {
                        if (state["variable"].ToString() == "Status")
                        {

                            switchstatus = int.Parse(state["value"].ToString());
                        }
                    }
                    break;
                }
            }
            // return the status of the switch 
            return switchstatus;
        }

        // get the fibaro status from device
        static int getFibaroStatus(JArray devices, int deviceid)
        {
            int switchstatus = 0;
            foreach (JObject device in devices)
            {
                if ((int)device["id"] == deviceid)
                {

                    foreach (JObject state in (JArray)device["states"])
                    {
                        if (state["variable"].ToString() == "Status")
                        {

                            switchstatus = int.Parse(state["value"].ToString());
                        }
                    }
                    break;
                }
            }
            // return the status of the switch 
            return switchstatus;
        }

        // get the fibaro power consumption from fibaro device. 
        static double getFibaroConsumption(JArray devices, int deviceid)
        {
            double switchstatus = 0;
            foreach (JObject device in devices)
            {
                if ((int)device["id"] == deviceid)
                {

                    foreach (JObject state in (JArray)device["states"])
                    {
                        if (state["variable"].ToString() == "Watts")
                        {

                            switchstatus = double.Parse(state["value"].ToString());
                        }
                    }
                    break;
                }
            }
            // return the status of the switch 
            return switchstatus;
        }

        // read devices list from CSV file
        static DataTable getDevicesFromCsv(string filename)
        {

            DataTable importedData = new DataTable();
            string header = null;

            try
            {
                using (StreamReader sr = new StreamReader(filename))
                {
                    if (string.IsNullOrEmpty(header))
                    {
                        header = sr.ReadLine();
                    }

                    string[] headerColumns = header.Split(new string[] { "\",\"" }, StringSplitOptions.None);
                    foreach (string headerColumn in headerColumns)
                    {
                        importedData.Columns.Add(headerColumn.Replace("\"", ""));
                    }

                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        if (string.IsNullOrEmpty(line)) continue;
                        string[] fields = line.Split(new string[] { "\",\"" }, StringSplitOptions.None);
                        DataRow importedRow = importedData.NewRow();

                        for (int i = 0; i < fields.Count(); i++)
                        {

                            importedRow[i] = fields[i].Replace("\"", "");

                        }

                        importedData.Rows.Add(importedRow);
                    }
                }


            }
            catch (Exception e)
            {
                Console.WriteLine("the file could not be read:");
                Console.WriteLine(e.Message);
            }

            return importedData;
        }

        // send message to IoTcloud 
        static async void sendDeviceToCloudMessagesAsync(JObject data)
        {
            var message = new Message(Encoding.ASCII.GetBytes(data.ToString(Formatting.None)));

            await deviceClient.SendEventAsync(message);
            Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, data.ToString(Formatting.None));
        }

        // read data from vera
        static JArray getDeviceDatafromVera(string veraip)
        {
            Console.WriteLine("Calling the Vera web service...");
            string verauri = "http://" + veraip + ":3480/data_request?id=status&output_format=json";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(verauri);
            JArray array = new JArray();
            using (var twitpicResponse = (HttpWebResponse)request.GetResponse())
            {
                Console.WriteLine("Calling the Vera web service...");
                using (var reader = new StreamReader(twitpicResponse.GetResponseStream()))
                {

                    var objText = reader.ReadToEnd();
                    Console.WriteLine(reader.ToString());
                    JObject joResponse = JObject.Parse(objText);
                    JObject result = (JObject)joResponse.Root;
                    array = (JArray)result["devices"];
                    string statu = array[0]["id"].ToString();
                    //Console.WriteLine(joResponse);

                }
            }
            return array;
        }
    }


}
