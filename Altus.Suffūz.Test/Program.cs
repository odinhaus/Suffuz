using Altus.Suffūz.Collections;
using Altus.Suffūz.Collections.IO;
using Altus.Suffūz.Collections.Tests;
using Altus.Suffūz.Diagnostics;
using Altus.Suffūz.Messages;
using Altus.Suffūz.Protocols;
using Altus.Suffūz.Protocols.Udp;
using Altus.Suffūz.Routing;
using Altus.Suffūz.Scheduling;
using Altus.Suffūz.Serialization;
using Altus.Suffūz.Serialization.Binary;
using Altus.Suffūz.Test;
using Altus.Suffūz.Tests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz
{
    class Program
    { 
        static void Main(string[] args)
        {
            //FileIO();
            //PerfTest();
            CollectionPerfTest();
            //Console.Read();
            ConfigureApp();
            ConfigureRoutes();
            DoIt();
        }

        private static void FileIO()
        {
            File.Delete("Journal.bin");
            var file = new FileStream("Journal.bin", FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, 1024 * 8, false);
            var len = 1024 * 1024 * 10;
            //file.SetLength(len);
            //SparseFile.MakeSparse(file);
            //SparseFile.SetZero(file, 0, file.Length);

            var sampleBlock = new byte[512 * 1];
            sampleBlock[0] = 65; // A
            for(int i = 1; i < sampleBlock.Length - 2; i++)
            {
                sampleBlock[i] = 48; // 0
            }

            //sampleBlock[512] = 66; // B
            ///sampleBlock[1024] = 67; // C

            sampleBlock[sampleBlock.Length - 2] = 13; // new line
            sampleBlock[sampleBlock.Length - 1] = 10;



            var count = len / sampleBlock.Length;
            var sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < count; i++)
            {
                file.Write(sampleBlock, 0, sampleBlock.Length);
                file.Flush();
                //file.WriteAsync(sampleBlock, 0, sampleBlock.Length).Wait();
            }
            
            file.Flush(true);
            sw.Stop();
            var writeRate = (float)count / (sw.ElapsedMilliseconds / 1000f);
            Console.WriteLine("Rate: {0}", writeRate);

            var hasher = MD5.Create();
            sw.Reset();
            sw.Start();
            for (int i = 0; i < count; i++)
            {
                hasher.ComputeHash(sampleBlock);
            }

            file.Flush(true);
            sw.Stop();
            var hashRate = (float)count / (sw.ElapsedMilliseconds / 1000f);
            Console.WriteLine("Hash: {0}", hashRate);
            Console.Read();
        }

        private static void CollectionPerfTest()
        {
            var fileName = "Dictionary.dic";
            var keyName = Path.GetFileNameWithoutExtension(fileName) + "_keys.bin";
            //File.Delete(fileName);
            //File.Delete(keyName);
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
                        if (i == 11)
                        {
                            Process.GetCurrentProcess().Kill();
                        }
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

            //File.Delete(fileName);
            //File.Delete(keyName);

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

            //router.Route<Handler, TestRequest, TestResponse>(Channels.BESTEFFORT_CHANNEL, (handler, request) => handler.HandleBE(request));
        }


        /// <summary>
        /// Where the magic happens ;)
        /// </summary>
        static void DoIt()
        {
            Console.WriteLine("Press ENTER to start");
            Console.Read();

            var channelService = App.Resolve<IChannelService>();
            var channel = channelService.Create("channel1");
            var count = 10000f;
            var sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < count; i++)
            {
                var message = new Message(StandardFormats.BINARY, "channel1", ServiceType.RequestResponse, App.InstanceName)
                {
                    Payload = new RoutablePayload(new TestRequest(), typeof(TestRequest), typeof(TestResponse)),
                    Recipients = new string[] { "*" }
                };

                var udpMessage = new UdpMessage(channel, message);
            }
            sw.Stop();

            var serializationRate = count / (sw.ElapsedMilliseconds / 1000f);
            Console.WriteLine("Serialization Rate: {0} message/sec", serializationRate);

            var buffer = new ChannelBuffer();
            buffer.Initialize(channel);


            sw.Reset();
            sw.Start();
            using (var scope = new FlushScope())
            {
                for (int i = 0; i < count; i++)
                {
                    var message = new Message(StandardFormats.BINARY, "channel1", ServiceType.RequestResponse, App.InstanceName)
                    {
                        Payload = new RoutablePayload(new TestRequest(), typeof(TestRequest), typeof(TestResponse)),
                        Recipients = new string[] { "*" }
                    };

                    var udpMessage = new UdpMessage(channel, message);
                    buffer.AddInboundSegment(udpMessage.UdpHeaderSegment);
                    for (int x = 0; x < udpMessage.UdpSegments.Length; x++)
                    {
                        buffer.AddInboundSegment(udpMessage.UdpSegments[x]);
                    }
                }
            }
            sw.Stop();
            var bufferRate = count / (sw.ElapsedMilliseconds / 1000f);
            Console.WriteLine("Buffer Rate: {0} message/sec", bufferRate);


            //sw.Reset();
            //sw.Start();
            //using (var scope = new FlushScope())
            //{
            //    for (int i = 0; i < count; i++)
            //    {
            //        var message = new Message(StandardFormats.BINARY, "channel1", ServiceType.RequestResponse, App.InstanceName)
            //        {
            //            Payload = new RoutablePayload(new TestRequest(), typeof(TestRequest), typeof(TestResponse)),
            //            Recipients = new string[] { "*" }
            //        };

            //        ((MulticastChannel)channel).Send(message);
            //    }
            //}
            //sw.Stop();
            //var sendRate = count / (sw.ElapsedMilliseconds / 1000f);
            //Console.WriteLine("Send Rate: {0} message/sec", bufferRate);

            
            Console.Read();
            sw.Reset();
            sw.Start();
            using (var scope = new FlushScope())
            {
                for (int i = 0; i < count / 10; i++)
                {

                    var r = Get<TestResponse>.From(Channels.CHANNEL, new TestRequest()).Execute();

                    //Debug.Assert(r.Size > 0);
                    //Get.From(Channels.CHANNEL, new CommandRequest()).Execute();
                }
            }
            sw.Stop();
            Console.WriteLine("Mean Call Time: {0} ms", sw.ElapsedMilliseconds / (count/10f));
            sw.Reset();


            //// executes the default call on the CHANNEL, with no arguments or response
            Get.From(Channels.CHANNEL).Execute();
            //Console.Read();

            //// executes with no result on the CHANNEL
            Get.From(Channels.CHANNEL, new CommandRequest()).Execute();
            //Console.Read();

            // executes a TestRequest call on the CHANNEL, and returns the first result returned from any respondant
            // blocks for the default timeout (Get.DefaultTimeout)
            var result1 = Get<TestResponse>.From(Channels.CHANNEL, new TestRequest()).Execute();
            Debug.Assert(result1 != null);
            //Console.Read();


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
            Debug.Assert(result2 != null);
            //Console.Read();

            // executes a TestRequest call on the CHANNEL, and returns the first result returned from any respondant
            // blocks for the default timeout (Get.DefaultTimeout)
            // because this combination of request/response type is not mapped, no response will return within the timeout period specified (500ms)
            // a timeout exception is throw in this case, or if none of the respondant results are received in time
            try
            {
                var result3 = Get<TestResponse>.From(Channels.CHANNEL, new CommandRequest()).Execute(500);
            }
            catch (TimeoutException)
            {
                Logger.LogInfo("Handled Timeout");
            }
            //Console.Read();

            // executes a directed TestRequest call on the CHANNEL, for a specific recipient (App.InstanceName), 
            // and returns the first result returned from any respondant
            // blocks for up to 500ms
            var result4 = Get<TestResponse>.From(Channels.CHANNEL, new TestRequest()).Execute(500, App.InstanceName);
            Debug.Assert(result4 != null);
            //Console.Read();

            // executes a TestRequest call on the CHANNEL, and returns all responses received within one second
            var enResult1 = Get<TestResponse>.From(Channels.CHANNEL, new TestRequest())
                                            .All()
                                            .Execute(1000)
                                            .ToArray();
            Debug.Assert(enResult1.Length > 0);
            //Console.Read();

            // executes a TestRequest call on the CHANNEL, and returns the first two responses received within the Get.DefaultTimeout time period
            // if the terminator condition is not met within the timeout period, the result will contain the responses received up to that time
            var enResult2 = Get<TestResponse>.From(Channels.CHANNEL, new TestRequest())
                                            .Take(2)
                                            .Execute()
                                            .ToArray();
            Debug.Assert(enResult2.Length > 0);
            //Console.Read();

            // executes the request on respondants whose capacity exceeds and arbitrary threshold
            // returns enumerable results from all respondants where responses meet an arbitrary predicate (Size > 2) which is evaluated locally
            // and blocks for 2000 milliseconds while waiting for responses
            // if no responses are received within the timeout, and empty set is returned
            // any responses received after the timeout are ignored
            var enResult3 = Get<TestResponse>.From(Channels.CHANNEL, new TestRequest())
                                            .Nominate(cr => cr.Score > 0.9)
                                            .Take(r => r.Size > 2)
                                            .Execute(2000)
                                            .ToArray();
            Debug.Assert(enResult3.Length > 0);
            //Console.Read();

            // executes the request on respondants whose capacity exceeds and arbitrary threshold
            // returns enumerable results from all respondants where responses meet an arbitrary predicate (Size > 2) which is evaluated locally
            // and blocks until the terminal condition is met (ChannelContext.Current.Count > 1)
            var enResult4 = Get<TestResponse>.From(Channels.CHANNEL, new TestRequest())
                                            .Nominate(cr => cr.Score > 0.9)
                                            .Take(r => r.Size > 2)
                                            .Until(r => ChannelContext.Current.Count > 1)
                                            .Execute()
                                            .ToArray();
            Debug.Assert(enResult4.Length > 0);
            //Console.Read();

            // in this case, because we're executing an enumeration for an unmapped request/response pair, the call will simply block for the 
            // timeout period, and return no results.  Enumerations DO NOT throw timeout exceptions in the absence of any responses, only scalar
            // execution calls can produce timeout exceptions.
            var enResult5 = Get<TestResponse>.From(Channels.CHANNEL, new CommandRequest())
                                            .All()
                                            .Execute(500)
                                            .ToArray();
            Debug.Assert(enResult5.Length == 0);

            // give the scheduler time to clean up
            System.Threading.Thread.Sleep(20);

            var scheduler = App.Resolve<IScheduler>();
            Console.WriteLine("Scheduled Tasks: {0}", scheduler.Count());

            Console.WriteLine("Tests Complete");
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

        public bool IsBestEffort { get; set; }
    }

    /// <summary>
    /// Sample class that handles dispatched calls, based on routing configuration
    /// </summary>
    class Handler
    {
        public TestResponse Handle(TestRequest request)
        {
            //Logger.LogInfo("Handled TestRequest with TestResponse");
            return new TestResponse() { Size = Environment.TickCount };
        }

        public TestResponse Handle()
        {
            //Logger.LogInfo("Handled NoArgs with TestResponse");
            return new TestResponse() { Size = 4 };
        }

        public void HandleNoArgs()
        {
            //Logger.LogInfo("Handled NoArgs");
        }

        public void Handle(CommandRequest request)
        {
            //Logger.LogInfo("Handled CommandRequest");
        }

        public TestResponse HandleBE(TestRequest request)
        {
            //Logger.LogInfo("Handled BE TestRequest with TestResponse");
            return new TestResponse() { Size = Environment.TickCount };
        }
    }
}
