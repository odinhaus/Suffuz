﻿using Altus.Suffūz.Collections;
using Altus.Suffūz.Collections.Tests;
using Altus.Suffūz.Diagnostics;
using Altus.Suffūz.Messages;
using Altus.Suffūz.Protocols;
using Altus.Suffūz.Protocols.Udp;
using Altus.Suffūz.Routing;
using Altus.Suffūz.Serialization.Binary;
using Altus.Suffūz.Test;
using Altus.Suffūz.Tests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz
{
    class Program
    {
        
        private static IChannelService _channelService;

        static void Main(string[] args)
        {
            //PerfTest();
            CollectionPerfTest();
            Console.Read();
            ConfigureApp();
            ConfigureRoutes();
            OpenChannels();
            DoIt();
        }

        private static void CollectionPerfTest()
        {
            var fileName = "Dictionary.dic";
            var keyName = Path.GetFileNameWithoutExtension(fileName) + "_keys.bin";
            File.Delete(fileName);
            File.Delete(keyName);
            float writeRate, readRate, loadRate, enumerateRate;
            var count = 1000000;
            var sw = new Stopwatch();
            var item = new CustomItem() { A = 12, B = "some text here" };
            using (var scope = new FlushScope())
            {
                using (var heap = new PersistentDictionary<string, CustomItem>(fileName, 1024 * 1024 * 1000))
                {
                    var addresses = new ulong[count];
                    sw.Start();
                    for (int i = 0; i < count; i++)
                    {
                        heap.Add(i.ToString(), item);
                    }
                    sw.Stop();
                    writeRate = (float)count / (sw.ElapsedMilliseconds / 1000f);

                    sw.Reset();
                    sw.Start();
                    for (int i = 0; i < count; i++)
                    {
                        item = heap[i.ToString()];
                    }
                    sw.Stop();
                    readRate = (float)count / (sw.ElapsedMilliseconds / 1000f);

                    sw.Reset();
                    sw.Start();
                    foreach (var thing in heap)
                    {
                    }
                    sw.Stop();
                    enumerateRate = (float)count / (sw.ElapsedMilliseconds / 1000f);
                }
            }

            sw.Start();
            using (var scope = new FlushScope())
            {
                sw.Stop();
            }
            loadRate = (float)count / (sw.ElapsedMilliseconds / 1000f);

            File.Delete(fileName);
            File.Delete(keyName);

            Console.WriteLine("Write Rate: {0}, Read Rate: {1}, Load Rate: {2}, Enumerate Rate: {3}", writeRate, readRate, loadRate, enumerateRate);
        }

        private static void PerfTest()
        {
            var builder = new ILSerializerBuilder();
            var instance = builder.CreateSerializerType<SimplePOCO>();

            var testPoco = new SimplePOCO()
            {
                //A = true,
                //B = 1,
                //C = 1,
                //D = (char)1,
                //E = 1,
                //F = 1,
                //G = 1,
                //H = 1,
                //I = 1,
                //J = 1,
                //K = 1,
                //L = 1,
                //M = 1,
                N = new byte[] { 1, 2, 3 },
                //O = "Foo".ToCharArray()
            };

            var serialized = instance.Serialize(testPoco);
            var poco = instance.Deserialize(serialized);

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < 1000000; i++)
            {
                instance.Serialize(testPoco);
            }

            stopwatch.Stop();

            var bandwidth = (double)(serialized.Length * 1000000);
            var serializationRate = (bandwidth / (stopwatch.ElapsedMilliseconds / 1000d)) / (1024 * 1000);
            Logger.LogInfo("Suffūz Bandwidth [Kb]: {0}", bandwidth / (1024d));
            Logger.LogInfo("Suffūz Throughput [Mb/s]: {0}", serializationRate);
            Logger.LogInfo("Suffūz Rate [Hz]: {0}", 1000000d / (stopwatch.ElapsedMilliseconds / 1000d));
            stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < 1000000; i++)
            {
                instance.Deserialize(serialized);
            }

            stopwatch.Stop();
            serializationRate = (bandwidth / (stopwatch.ElapsedMilliseconds / 1000d)) / (1024 * 1000);
            Logger.LogInfo("Suffūz Throughput [Mb/s]: {0}", serializationRate);
            Logger.LogInfo("Suffūz Rate [Hz]: {0}", 1000000d / (stopwatch.ElapsedMilliseconds / 1000d));

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(testPoco);
            stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < 1000000; i++)
            {
                Newtonsoft.Json.JsonConvert.SerializeObject(testPoco);
            }

            stopwatch.Stop();

            var jbandwidth = (double)(json.Length * 1000000);
            var jserializationRate = (jbandwidth / (stopwatch.ElapsedMilliseconds / 1000d)) / (1024 * 1000);
            Logger.LogInfo("Json Bandwidth [Kb]: {0}", jbandwidth / (1024d));
            Logger.LogInfo("Json Throughput [Mb/s]: {0}", jserializationRate);
            Logger.LogInfo("Json Rate [Hz]: {0}", 1000000d / (stopwatch.ElapsedMilliseconds / 1000d));
            stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < 1000000; i++)
            {
                Newtonsoft.Json.JsonConvert.DeserializeObject<SimplePOCO>(json);
            }

            stopwatch.Stop();
            jserializationRate = (jbandwidth / (stopwatch.ElapsedMilliseconds / 1000d)) / (1024 * 1000);
            Logger.LogInfo("Json Throughput [Mb/s]: {0}", jserializationRate);
            Logger.LogInfo("Json Rate [Hz]: {0}", 1000000d / (stopwatch.ElapsedMilliseconds / 1000d));
        }

        /// <summary>
        /// Configures the DI system
        /// </summary>
        private static void ConfigureApp()
        {
            // sets the DI container adapter to TypeRegistry
            App<TypeRegistry>.Initialize();
        }

        /// <summary>
        /// Creates communication channels to listen for incoming messages
        /// </summary>
        private static void OpenChannels()
        {
            // creates the local channel instance for the CHANNEL service
            _channelService = App.Resolve<IChannelService>();
            _channelService.Create(Channels.CHANNEL);
        }

        /// <summary>
        /// Creates the incoming message routing rules
        /// </summary>
        private static void ConfigureRoutes()
        {
            var router = App.Resolve<IServiceRouter>();
            // set a default handler for CHANNEL for requests with no arguments and no responses
            router.Route<Handler>(Channels.CHANNEL, (handler) => handler.HandleNoArgs());

            // route incoming requests on CHANNEL of type CommandRequest to handler with no result
            router.Route<Handler, CommandRequest>(Channels.CHANNEL, (handler, request) => handler.Handle(request));

            // route incoming requests on CHANNEL of type TestRequest to an instance of type Handler, returning a TestResponse result
            // additionally, set a capacity limit on this request
            // and delay responses for up to 5 seconds for this request proportional to its current capacity score
            router.Route<Handler, TestRequest, TestResponse>(Channels.CHANNEL, (handler, request) => handler.Handle(request))
                  .Nominate(() => new NominateResponse()
                  {
                      Score = CostFunctions.CapacityCost(25d, 0d, 100d)
                  })
                  .Delay((capacity) => TimeSpan.FromMilliseconds(5000d * (1d - capacity.Score)));

            // route incoming requests on CHANNEL with no arguments to an instance of Handler, returning a TestResponse result
            // additionally, set a nominatoion score on this request to a double
            // and delay responses for up to 5 seconds for this request proportional to its current capacity score
            router.Route<Handler, TestResponse>(Channels.CHANNEL, (handler) => handler.Handle())
                  .Nominate(() => CostFunctions.CapacityCost(25d, 0d, 100d))
                  .Delay((capacity) => TimeSpan.FromMilliseconds(5000d * (1d - capacity.Score)));
        }


        /// <summary>
        /// Where the magic happens ;)
        /// </summary>
        static void DoIt()
        {
            // executes the default call on the CHANNEL, with no arguments or response
            Get.From(Channels.CHANNEL).Execute();


            // executes with no result on the CHANNEL
            Get.From(Channels.CHANNEL, new CommandRequest()).Execute();


            // executes a TestRequest call on the CHANNEL, and returns the first result returned from any respondant
            // blocks for the default timeout (Get.DefaultTimeout)
            var result1 = Get<TestResponse>.From(Channels.CHANNEL, new TestRequest()).Execute();


            // executes the request on respondants whose capacity exceeds an arbitrary threshold
            // the first respondant passing the nomination predicate test and returning a signaling message to the caller
            // is then sent the actual request to be processed, ensuring the actual request is only processed on a single remote agent
            // nomination scoring is configured as part of the route definition on the recipient
            // if no nomination scoring is defined on the recipient, a maximum value of 1.0 is returned for the score
            // the Delegate expression is evaluated within the respondants' process, such that failing 
            // test prevent the request from beng dispatched to their corresponding handlers, 
            // thus preventing both evaluation and responses
            var result2 = Get<TestResponse>.From(Channels.CHANNEL, new TestRequest())
                                            .Nominate(response => response.Score > TestResponse.SomeNumber())
                                            .Execute();


            // executes a TestRequest call on the CHANNEL, and returns the first result returned from any respondant
            // blocks for the default timeout (Get.DefaultTimeout)
            // because this combination of request/response type is not mapped, no response will return within the timeout period specified (500ms)
            // a timeout exception is throw in this case, or if none of the respondant results are received in time
            try
            {
                var result3 = Get<TestResponse>.From(Channels.CHANNEL, new CommandRequest()).Execute(500);
            }
            catch(TimeoutException)
            {
                Logger.LogInfo("Handled Timeout");
            }

            // executes a directed TestRequest call on the CHANNEL, for a specific recipient (App.InstanceName), 
            // and returns the first result returned from any respondant
            // blocks for up to 500ms
            var result4 = Get<TestResponse>.From(Channels.CHANNEL, new TestRequest()).Execute(500, App.InstanceName);


            // executes a TestRequest call on the CHANNEL, and returns all responses received within one second
            var enResult1 = Get<TestResponse>.From(Channels.CHANNEL, new TestRequest())
                                            .Enumerate()
                                            .Execute(1000)
                                            .ToArray();

            // executes a TestRequest call on the CHANNEL, and returns the first two responses received within the Get.DefaultTimeout time period
            // if the terminator condition is not met within the timeout period, the result will contain the responses received up to that time
            var enResult2 = Get<TestResponse>.From(Channels.CHANNEL, new TestRequest())
                                            .Enumerate((responses) => responses.Count() > 2)
                                            .Execute()
                                            .ToArray();


            // executes the request on respondants whose capacity exceeds and arbitrary threshold
            // returns enumerable results from all respondants where responses meet an arbitrary predicate (Size > 2) which is evaluated locally
            // and blocks for 2000 milliseconds while waiting for responses
            // if no responses are received within the timeout, and empty set is returned
            // any responses received after the timeout are ignored
            var enResult3 = Get<TestResponse>.From(Channels.CHANNEL, new TestRequest())
                                            .Nominate(cr => cr.Score > 0.9)
                                            .Enumerate((responses) => responses.Where(r => r.Size > 2))
                                            .Execute(2000)
                                            .ToArray();


            // executes the request on respondants whose capacity exceeds and arbitrary threshold
            // returns enumerable results from all respondants where responses meet an arbitrary predicate (Size > 2) which is evaluated locally
            // and blocks until the terminal condition is met (responses.Count() > 0)
            var enResult4 = Get<TestResponse>.From(Channels.CHANNEL, new TestRequest())
                                            .Nominate(cr => cr.Score > 0.9)
                                            .Enumerate((responses) => responses.Where(r => r.Size > 2),
                                                       (reponses) => reponses.Count() > 0)
                                            .Execute()
                                            .ToArray();


            // in this case, because we're executing an enumeration for an unmapped request/response pair, the call will simply block for the 
            // timeout period, and return no results.  Enumerations DO NOT through timeout exceptions in the absence of any responses, only scalar
            // execution calls can produce timeout exceptions.
            var enResult5 = Get<TestResponse>.From(Channels.CHANNEL, new CommandRequest())
                                            .Enumerate(responses => responses)
                                            .Execute(500)
                                            .ToArray();

            Console.Read();
        }
    }

    public class TestRequest
    {

    }

    public class CommandRequest {}

    public class TestResponse
    {
        [BinarySerializable(0)]
        public int Size { get; set; }

        public static double SomeNumber()
        {
            return 0.8d;
        }
    }

    /// <summary>
    /// Sample class that handles dispatched calls, based on routing configuration
    /// </summary>
    class Handler
    {
        public TestResponse Handle(TestRequest request)
        {
            Logger.LogInfo("Handled TestRequest with TestResponse");
            return new TestResponse() { Size = Environment.TickCount };
        }

        public TestResponse Handle()
        {
            Logger.LogInfo("Handled NoArgs with TestResponse");
            return new TestResponse() { Size = 4 };
        }

        public void HandleNoArgs()
        {
            Logger.LogInfo("Handled NoArgs");
        }

        public void Handle(CommandRequest request)
        {
            Logger.LogInfo("Handled CommandRequest");
        }
    }
}
