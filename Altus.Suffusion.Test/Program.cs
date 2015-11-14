using Altus.Suffusion.Diagnostics;
using Altus.Suffusion.Messages;
using Altus.Suffusion.Protocols;
using Altus.Suffusion.Protocols.Udp;
using Altus.Suffusion.Routing;
using Altus.Suffusion.Serialization.Binary;
using Altus.Suffusion.Test;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion
{
    class Program
    {
        public const string CHANNEL = "channel1";
        private static IChannelService _channelService;

        static void Main(string[] args)
        {
            ConfigureApp();
            ConfigureRoutes();
            OpenChannels();
            DoIt();
        }

        private static void ConfigureApp()
        {
            App<TypeRegistry>.Initialize();
        }

        private static void OpenChannels()
        {
            _channelService = App.Resolve<IChannelService>();
            _channelService.Create(CHANNEL);
        }

        private static void ConfigureRoutes()
        {
            var router = App.Resolve<IServiceRouter>();

            router.Route<Handler, TestRequest, TestResponse>(CHANNEL, (handler, request) => handler.Handle(request))
                  .Capacity(() => new CapacityResponse()
                                    {
                                        Minimum = 0,
                                        Maximum = 100,
                                        Current = 25,
                                        Score = CostFunctions.CapacityCost(25d, 0d, 100d)
                                    })
                  .Delay((capacity) => TimeSpan.FromMilliseconds(5000d * (1d - capacity.Score)));

            router.Route<Handler, TestResponse>(CHANNEL, (handler) => handler.Handle())
                  .Capacity(() => new CapacityResponse() { Minimum = 0, Maximum = 100, Current = 25, Score = CostFunctions.CapacityCost(25d, 0d, 100d) })
                  .Delay((capacity) => TimeSpan.FromMilliseconds(5000d * (1d - capacity.Score)));

            router.Route<Handler>(CHANNEL, (handler) => handler.HandleNoArgs());
        }

        static void DoIt()
        {
            //// executes the request directly and returns the first result provided from any respondant
            //var result = await new Op<TestRequest, TestResponse>(CHANNEL, new TestRequest())
            //                        .ExecuteAsync();

            //// executes the request directly and aggregates N enumerated responses
            //var enumerableResult1 = await new Op<TestRequest, TestResponse>(CHANNEL, new TestRequest())
            //                        .Aggregate(responses => responses.Where(r => r.Size > 2))
            //                        .ExecuteAsync();

            //// executes the request on respondants whose capacity exceeds an arbitrary threshold
            //// for simple predicate expressions without closures, the expression will be evaluated within the respondants' process
            //var enumerableResult2 = await new Op<TestRequest, TestResponse>(CHANNEL, new TestRequest())
            //                        .Delegate(responses => responses.Where(r => r.Score > 0.2))
            //                        .ExecuteAsync();

            //// executes the request on respondants whose capacity exceeds an arbitrary threshold
            //// results are aggregated
            //var enumerableResult3 = await new Op<TestRequest, TestResponse>(CHANNEL, new TestRequest())
            //                        .Delegate(responses => responses.Where(r => r.Score > 0.2))
            //                        .Aggregate(responses => responses.Where(r => r.Size > 2))
            //                        .ExecuteAsync();

            // executes the request respondants whose capacity exceeds an arbitrary threshold
            // returns the result from the first matching respondant

            var scalarResult2 = Op<TestResponse>.New(CHANNEL, new TestRequest())
                                        .Delegate(response => response.Score > TestResponse.SomeNumber())
                                        .Execute();

            var scalarResult3 = Op<TestResponse>.New(CHANNEL, new TestRequest())
                                        .Execute();

            var scalarResult4 = Op<TestResponse>.New(CHANNEL, new TestRequest())
                                        .Execute();

            Op.New(CHANNEL).Execute();


            Console.Read();
        }
    }

    public class TestRequest
    {

    }

    public class TestResponse
    {
        [BinarySerializable(0)]
        public int Size { get; set; }

        public static double SomeNumber()
        {
            return 0.8d;
        }
    }

    class Handler
    {
        public TestResponse Handle(TestRequest request)
        {
            return new TestResponse() { Size = Environment.TickCount };
        }

        public TestResponse Handle()
        {
            return new TestResponse() { Size = 4 };
        }

        public void HandleNoArgs()
        {
            Logger.LogInfo("No Args Handled");
        }
    }
}
