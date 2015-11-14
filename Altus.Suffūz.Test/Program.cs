using Altus.Suffūz.Diagnostics;
using Altus.Suffūz.Messages;
using Altus.Suffūz.Protocols;
using Altus.Suffūz.Protocols.Udp;
using Altus.Suffūz.Routing;
using Altus.Suffūz.Serialization.Binary;
using Altus.Suffūz.Test;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz
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
            _channelService.Create(CHANNEL);
        }

        /// <summary>
        /// Creates the incoming message routing rules
        /// </summary>
        private static void ConfigureRoutes()
        {
            var router = App.Resolve<IServiceRouter>();
            // route incoming requests on CHANNEL of type TestRequest to an instance of type Handler, returning a TestResponse result
            // additionally, set a capacity limit on this request
            // and delay responses for up to 5 seconds for this request proportional to its current capacity score
            router.Route<Handler, TestRequest, TestResponse>(CHANNEL, (handler, request) => handler.Handle(request))
                  .Nominate(() => new NominateResponse()
                  {
                      Score = CostFunctions.CapacityCost(25d, 0d, 100d)
                  })
                  .Delay((capacity) => TimeSpan.FromMilliseconds(5000d * (1d - capacity.Score)));

            // route incoming requests on CHANNEL with no arguments to an instance of Handler, returning a TestResponse result
            // additionally, set a nominatoion score on this request to a double
            // and delay responses for up to 5 seconds for this request proportional to its current capacity score
            router.Route<Handler, TestResponse>(CHANNEL, (handler) => handler.Handle())
                  .Nominate(() => CostFunctions.CapacityCost(25d, 0d, 100d))
                  .Delay((capacity) => TimeSpan.FromMilliseconds(5000d * (1d - capacity.Score)));

            // route incoming requests on CHANNEL of type CommandRequest to handler with no result
            router.Route<Handler, CommandRequest>(CHANNEL, (handler, request) => handler.Handle(request));

            // set a default handler for CHANNEL for requests with no arguments and no responses
            router.Route<Handler>(CHANNEL, (handler) => handler.HandleNoArgs());
        }


        /// <summary>
        /// Where the magic happens ;)
        /// </summary>
        static void DoIt()
        {
            // executes the default call on the CHANNEL, with no arguments or response
            Op.New(CHANNEL).Execute();

            // executes with no result on the CHANNEL
            Op.New(CHANNEL, new CommandRequest()).Execute();

            // executes a TestRequest call on the CHANNEL, and returns the first result returned from any respondant
            var result1 = Op<TestResponse>.New(CHANNEL, new TestRequest()).Execute();

            // executes the request on respondants whose capacity exceeds an arbitrary threshold
            // returns the result from the first matching respondant
            // the Delegate expression is evaluated within the respondants' process, such that failing 
            // test prevent the request from beng dispatched to their corresponding handlers, 
            // thus preventing both evaluation and responses
            var result2 = Op<TestResponse>.New(CHANNEL, new TestRequest())
                                        .Nominate(response => response.Score > TestResponse.SomeNumber())
                                        .Execute();

            // executes the request on respondants whose capacity exceeds and arbitrary threshold
            // returns enumerable results from all respondants where responses meet an arbitrary predicate (Size > 2) which is evaluated locally
            // and blocks for 2000 milliseconds while waiting for responses
            var enResult1 = Op<TestResponse>.New(CHANNEL, new TestRequest())
                                        .Nominate(cr => cr.Score > 0.9)
                                        .Aggregate((responses) => responses.Where(r => r.Size > 2))
                                        .Execute(2000)
                                        .ToArray();

            // executes the request on respondants whose capacity exceeds and arbitrary threshold
            // returns enumerable results from all respondants where responses meet an arbitrary predicate (Size > 2) which is evaluated locally
            // and blocks until the terminal condition is met (responses.Count() > 0)
            var enResult2 = Op<TestResponse>.New(CHANNEL, new TestRequest())
                                        .Nominate(cr => cr.Score > 0.9)
                                        .Aggregate((responses) => responses.Where(r => r.Size > 2),
                                                   (reponses) => reponses.Count() > 0)
                                        .Execute()
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
