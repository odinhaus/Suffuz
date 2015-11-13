using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion
{
    class Program
    {
        static void Main(string[] args)
        {
            DoIt();
        }

        static async void DoIt()
        {
            //// executes the request directly and returns the first result provided from any respondant
            //var result = await new Op<TestRequest, TestResponse>("Chan1", new TestRequest())
            //                        .ExecuteAsync();

            //// executes the request directly and aggregates N enumerated responses
            //var enumerableResult1 = await new Op<TestRequest, TestResponse>("Chan1", new TestRequest())
            //                        .Aggregate(responses => responses.Where(r => r.Size > 2))
            //                        .ExecuteAsync();

            //// executes the request on respondants whose capacity exceeds an arbitrary threshold
            //// for simple predicate expressions without closures, the expression will be evaluated within the respondants' process
            //var enumerableResult2 = await new Op<TestRequest, TestResponse>("Chan1", new TestRequest())
            //                        .Delegate(responses => responses.Where(r => r.Score > 0.2))
            //                        .ExecuteAsync();

            //// executes the request on respondants whose capacity exceeds an arbitrary threshold
            //// results are aggregated
            //var enumerableResult3 = await new Op<TestRequest, TestResponse>("Chan1", new TestRequest())
            //                        .Delegate(responses => responses.Where(r => r.Score > 0.2))
            //                        .Aggregate(responses => responses.Where(r => r.Size > 2))
            //                        .ExecuteAsync();

            // executes the request respondants whose capacity exceeds an arbitrary threshold
            // returns the result from the first matching respondant
            var scalarResult2 = await new Op<TestRequest, TestResponse>("Chan1", new TestRequest())
                                    .Delegate(response => response.Score > TestResponse.SomeNumber() && response.Current < response.Maximum)
                                    .ExecuteAsync();
        }
    }

    class TestRequest
    {

    }

    class TestResponse
    {
        public int Size { get; set; }

        public static int SomeNumber() { return 2; }
    }
}
