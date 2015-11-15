# Suffūz
#### Pronounced: */səˈfyo͞oz/*
#### Meaning: *gradually spread through or over*

A simple and fast API for distributed computing with .Net.

##Sample Usage
####Remote Execution
```
// executes the default call on the CHANNEL, with no arguments or response
Op.New(CHANNEL).Execute();

// executes with no result on the CHANNEL
Op.New(CHANNEL, new CommandRequest()).Execute();

// executes a TestRequest call on the CHANNEL, and returns the first result returned from any respondant
// blocks for the default timeout (Op.DefaultTimeout)
var result1 = Op<TestResponse>.New(CHANNEL, new TestRequest()).Execute();

// executes the request on respondants whose capacity exceeds an arbitrary threshold
// the first respondant passing the nomination predicate test and returning a signaling message to the caller
// is then sent the actual request to be processed, ensuring the actual request is only processed on a single remote agent
// nomination scoring is configured as part of the route definition on the recipient
// if no nomination scoring is defined on the recipient, a maximum value of 1.0 is returned for the score
// the Delegate expression is evaluated within the respondants' process, such that failing 
// test prevent the request from beng dispatched to their corresponding handlers, 
// thus preventing both evaluation and responses
var result2 = Op<TestResponse>.New(CHANNEL, new TestRequest())
                            .Nominate(response => response.Score > TestResponse.SomeNumber())
                            .Execute();

// executes a TestRequest call on the CHANNEL, and returns the first result returned from any respondant
// blocks for the default timeout (Op.DefaultTimeout)
// because this combination of request/response type is not mapped, no response will return within the timeout period specified (500ms)
// a timeout exception is throw in this case, or if none of the respondant results are received in time
try
{
    var result3 = Op<TestResponse>.New(CHANNEL, new CommandRequest()).Execute(500);
}
catch(TimeoutException)
{
    Logger.LogInfo("Handled Timeout");
}

// executes the request on respondants whose capacity exceeds and arbitrary threshold
// returns enumerable results from all respondants where responses meet an arbitrary predicate (Size > 2) which is evaluated locally
// and blocks for 2000 milliseconds while waiting for responses
// if no responses are received within the timeout, and empty set is returned
// any responses received after the timeout are ignored
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

// in this case, because we're executing and aggregation for an unmapped request/response pair, the call will simply block for the 
// timeout period, and return no results.  Aggregations DO NOT through timeout exceptions in the absence of any responses, only scalar
// execution calls can produce timeout exceptions.
var enResult3 = Op<TestResponse>.New(CHANNEL, new CommandRequest())
                                .Aggregate(responses => responses)
                                .Execute(500)
                                .ToArray();
```
