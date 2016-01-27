![alt tag](https://raw.github.com/odinhaus/Suffuz/master/Media/Suffuze_Logo.png)
#### Pronounced: */səˈfyo͞oz/*
#### Meaning: *gradually spread through or over*

#####*A simple and fast API for distributed computing with .Net.*

##NuGet
####PM> Install-Package Altus.Suffuz
[Altus.Suffuz on NuGet.org](https://www.nuget.org/packages/Altus.Suffuz/)

##Description
Suffūz is an API allowing .Net applications to easily request services from and distribute units of work or share synchronized state  with a group of disparate processes over any IP network.

Out of the box, it utilizes Multicast UDP messaging groups, allowing clients to establish arbitrary "channels" for communication endpoints, over which POCO .Net types can be updated, synchronized and exchanged.  By incorporating optional channel providers for SignalR and mobile push services, then web and mobile clients can also participate in the distributed computing system.

In addition to the basic request/response scenarios supported, the system also allows for general broadcast messaging and direct messaging as well as load distribution through the application of arbitrary cost functions and availability delays that can be configured on a route-specific basis, allowing worker agents with more capacity to handle units of work to respond more quickly to requests for service, and then be allocated the work requested.  

The API uses simple fluent-styled syntax to create both calling and routing patterns, and it is built to easily adapt to your choice of Dependency Injection platform, supporting rich extensibility and testability.

The platform provides several independently useful core services, which are described in some detail following:
####1. Synchronized Observable State Objects
####2. Remote Unit of Work Execution
####3. Persistable Transactional Collections
####4. High Performance Serialization


##Sample Use Cases
###SCADA
![alt tag](https://raw.github.com/odinhaus/Suffuz/master/Media/SCADA.png)

In a typical SCADA application, you have a variety of computing nodes either observing or directly participating in an industrial control process.  Many times, nodes can join and leave throughout the lifecycle of the process.  A messaging infrastructure that allows for seamless group membership, with both broadcast messaging and directed messaging is paramount for development of such systems.  

Suffūz is a great fit for these types of processes by leveraging fast, compiled binary serialization and UDP messaging, Suffūz can achieve sub-millisecond message latencies, which is critical for high-speed industrial processes.  It is also sufficient for most high-fidelity industrial data logging applications as well.


###Shared Memory
![alt tag](https://raw.github.com/odinhaus/Suffuz/master/Media/SharedMemory.png)

Often times when scaling web applications across multiple web and service nodes, there is a need for relatively fast access to shared state across those devices.  Suffūz would provide the correct pieces for those situations as well, where a common stateful node would maintain a dictionary of shared items in memory, which could be accessed and updated by N dependant nodes, that might come and go randomly over the lifecycle of the shared state.

In these scenarios, fast access, and zero-touch group membership are of the utmost importance.  Suffūz provides the platform to make this simple.

Extending this model to include remote clients, a system can define object instances with globally unique keys, and strongly typed structure to provide a framework for shared state sessions spanning all tiers of a heterogenous computing environment.  Participants in a shared state instantiate a local copy, and subscribe to changes which are distributed and synchronized across all peers sharing the instance.  By modeling state objects in shapes that make sense to the consumer, and abstracting away the details of messaging systems, channels, and synchronization, developers can easily develop distributed event-based solutions in a wide variety of forms.


###Worker Pool (With or Without Nomination Constraints)
![alt tag](https://raw.github.com/odinhaus/Suffuz/master/Media/WorkerPool.png)

In some designs, you need a designated node to act as a dispatcher of queued work requests where you also have N worker nodes, all capable of processing the request.

In some situations you might want the work to be dispatched to a single node, and other cases you might want the work dispatched to arbitrary number of nodes, based on some logical condition as determined by the dispatcher.

In the former case, worker selection can be achieved by a simple matter of asking for workers capable of processing a given request to identify themselves, and then dispatching the request to the first respondant, and ignoring the others.  All things being equal, the fastest worker to respond, should also be the fastest worker to process the request.

But things aren't always equal, so sometimes you need a way to balance the field. Suffūz accomplishes this by allowing the dispatcher to determine a nomination selection threshold which is evaluated on each worker, for each request route.  Each worker can be configured with a simple delegate to compute a score, between 0.0 and 1.0 representing the worker's current ability to handle the request, with 1.0 being the most capable, and 0.0 meaning least.  That score is compared to the threshold function provided by the dispatcher, and if it is sufficient, the worker indicates its ability to process the request with a nomination response.  

Additionally, the route can also be configured with a nomination response delay based on the nomination score which server to directly support worker candidate selection by the dispatcher.  By making low scores respond more slowly than high scores, it increases the chance that high scorers nomination responses will be received by the dispatcher first, allowing them to be chosen for the work, rather than more burdened workers.

Obviously, imposing response delays won't be appropriate for all systems, as it could have the side-effect of reducing overall system throughput, so additional mechanisms might be required to determine how to compute the delay factor, based on the system-wide average capacity, for example, which could be broadcast regularly over a designated channel, and incorporated into the delay computation function.  That's all up to you, and your use cases.  Either way, Suffūz supports it in any form that you need.

In the latter case, where the same work request is distributed across N concurrent nodes, delaying responses probably makes less sense, so you'd likely only want to optionally configure the nomination threshold for those routes, and allow all workers that pass the nomination test to complete and return the results of their work as soon as possible.

###Whatever Else You Can Dream Up....
Any other simple distributed computing scenarios you might imagine, Suffūz can probably give you a good place to start.

#Synchronized Observable State Instances
The main idea here is to be able to create globally identified instances of simple types that define properties and methods that describe some sharable state of a system.  Examples might include an Inbox state, pinned to a specific user of group of users, having a MessageCount property, and MessageArrived() and MessageRead() and MessageDeleted() methods.

Participants would instantiate a local instance of the Inbox object, and potentially subscribe to changes to the MessageCount property, or subscribe to notifications when the various methods are called, such that it can take action to update a user interface, make a service call or update local database, etc.

As changes occur on local nodes, they are collected and distributed over the peer group of current subscribes, where the changes are reconcilled and local subscribers are notified if the state has changed. 

###Sample Code
```C#
// simple demonstration of "normal" consumer code, where you get/create an instance of a shared state
// apply any change or method call subscriptions that are relevant, and then just start using the instance
// as normal to apply your local updates.


// get (or create) an observable instance of type StateClass with a global key of "some key"
var observable = Observe.Get(() => new StateClass { Size = 2, Score = 3 }, "some key");


// subscribe to this instance, receiving notifications when anything changes on the instance
// the handler will examine the notification properties to determine the significance/relevance of the change
var subscription2 = Observe.BeforeAny((e) => this.BeforeAny(e), observable);


// update a value, triggering a change event to be replicated across the distribution channels configured previously
// calling all subscribers on each channel for this shared instance
observable.Size = 4;
```

Subscriptions can also be defined more globally, rather than on a specific shared instance, as follows.

```C#
// here we demonstrate all the possible global subscription overloads
var subscription1 = Observe<StateClass> 
        .BeforeCreated((e) => this.BeforeCreated(e))
        .BeforeCreated((e) => this.BeforeCreated(e), "some key")
        .BeforeCreated((e) => this.BeforeCreated(e), (e) => e.GlobalKey == "some key")
  
        .AfterCreated((e) => this.AfterCreated(e))
        .AfterCreated((e) => this.AfterCreated(e), "some key")
        .AfterCreated((e) => this.AfterCreated(e), (e) => e.GlobalKey == "some key")
  
        .BeforeDisposed((e) => this.BeforeDisposed(e))
        .BeforeDisposed((e) => this.BeforeDisposed(e), "some key")
        .BeforeDisposed((e) => this.BeforeDisposed(e), (e) => e.GlobalKey == "some key")
  
        .AfterDisposed((e) => this.AfterDisposed(e))
        .AfterDisposed((e) => this.AfterDisposed(e), "some key")
        .AfterDisposed((e) => this.AfterDisposed(e), (e) => e.GlobalKey == "some key")
  
        .BeforeAny((e) => this.BeforeAny(e))
        .BeforeAny((e) => this.BeforeAny(e), "some key")
        .BeforeAny((e) => this.BeforeAny(e), (e) => e.GlobalKey == "some key")
  
        .AfterAny((e) => this.AfterAny(e))
        .AfterAny((e) => this.AfterAny(e), "some key")
        .AfterAny((e) => this.AfterAny(e), (e) => e.GlobalKey == "some key")
  
        .BeforeCalled<int, string>((s) => s.Hello, (e) => this.BeforeHello(e))
        .BeforeCalled<int, string>((s) => s.Hello, (e) => this.BeforeHello(e), "some key")
        .BeforeCalled<int, string>((s) => s.Hello, (e) => this.BeforeHello(e), (e) => e.GlobalKey == "some key")
  
        .AfterCalled<int, string>((s) => s.Hello, (e) => this.AfterHello(e))
        .AfterCalled<int, string>((s) => s.Hello, (e) => this.AfterHello(e), "some key")
        .AfterCalled<int, string>((s) => s.Hello, (e) => this.AfterHello(e), (e) => e.GlobalKey == "some key")
  
        .BeforeChanged((s) => s.Size, (e) => this.BeforeSizeChanged(e))
        .BeforeChanged((s) => s.Size, (e) => this.BeforeSizeChanged(e), "some key")
        .BeforeChanged((s) => s.Size, (e) => this.BeforeSizeChanged(e), (e) => e.GlobalKey == "some key")
  
        .AfterChanged((s) => s.Size, (e) => this.AfterSizeChanged(e))
        .AfterChanged((s) => s.Size, (e) => this.AfterSizeChanged(e), "some key")
        .AfterChanged((s) => s.Size, (e) => this.AfterSizeChanged(e), (e) => e.GlobalKey == "some key")
  
        .Subscribe();
```

Out of the box, the base Suffūz library only supports distribution over Multicast networks, but by adding various channel providers that participate in the message exchange, the supported networking environment can be augmented as needed to incorporate more diverse environments.  Additional assemblies have already been developed for adding SignalR and iOS push-enabled devices.  The configuration to include additional providers is trivial.

```C#
IObservableChannelProvider 
      cp1 = new BestEffortObservableChannelProvider(), 
      cp2 = new SignalRObservableChannelProvider(),
      cp3 = new iOSObservableChannelProvider();

// setup the infrastructure to replicate event messages across your system
// this example would provide synchronization across windows servers over Multicast, javascript (and other) clients 
// over SignalR, and iOS devices using APNS
var registration1 = Observe.RegisterChannelProvider(cp1); // register for best-effort multicast event synchronization
var registration2 = Observe.RegisterChannelProvider(cp2); // register for SignalR javascript client notifications
var registration3 = Observe.RegisterChannelProvider(cp3); // register for iOS push notifications
```

Channel providers have their own implementations and configuration requirements, but one of the main jobs is to determine how to route specific messages onto relevant channels to dispatch the message.  In some cases, all messages might be exchanged over a single physical channel, or they may be split up into a channel per shared object, or any combination in between.  Consult the configuration of the specific channel provider for options.

When change messages arrive, local and remote changes need to be reconciled in order to synchronize state across devices.  In order to facilitate automated merging, state object definitions are attributed to define how conflicting changes should be addressed.

The two main types of properties are Commutative properties and Explicit properties.

Commutative properties are numeric properties whose values can be updated by either by a series of additions or multiplications to the current value.  In this sense, the order in which the updates are applied is irrelevant, as both addition and multiplication are commutative operations.  In the event of conflict during an update merge, the two parties merging simply apply all local and remote updates to their own local value, to achieve the same result.  Obviously, explicit values are not applied to the properties, but rather the mathmatical delta between current state and changed state is tracked and applied.  For example, if the current value is 6, and the new value is 10, and the property is attributed as Additive, then a delta of 4 is recorded, and would be exchanged with other parties in the synchronization framework.  Likewise for Multiplication, a value of 10/6 would be recorded for the delta.

Explicit properties represent values that cannot be updated through mathmatical deltas, and instead, they are applied either by a logical or temporal clock timestamp.  Temporal clocks can be subject to error when clocks are not synchronized, so the choice to use use literal timestamps depends on your environment, and the consequences that synchronized timing errors can bring to your system.  An alternative to literal clocks is a logical clock implementation, whereby local state values are mapped to update epochs whenever a remote synchronization takes place. Participants determine the logical age of an update by how many merge updates it has already been subjected to.  If their change's epoch is less than the epoch of the merge target, their local change is ignored, and remote (older) change is taken.  The idea here is, systems that are offline, or slow to respond to changes are viewed as "behind" the rest of the system, so their updates are not applied to explicit values until the catch up and exceed the greates epoch.  In the event that two targets have the same epoch, a true conflict occurs, at which point some policy is determined to break the tie.  

Generally, it's best to avoid explicit properties where multiple writers will be updating the same property on the same shared state if you want to avoid unreconcilable conflicts.

```C#
// a simple observable base class with a single Commutative property
public abstract class BaseState
{
    [CommutativeEvent(CommutativeEventType.Additive)]
    public virtual int Age { get; set; }
}
```




#Remote Execution
In many cases, you may have a group of processes (which may also be distrubuted across multiple hosts) participating in a system.  In this case, you need to be able to request services from, and delegate work across the peer network.

Suffūz's remote execution framework allows you to do just that.

There are two types of remote execution calls - those that return a response, and those that do not.  The latter are referred to as Put calls, and the former as Post calls by the Suffūz framework.

In either case, the calling patterns are very similar, wherein you execute a request on a selected channel, optionally providing an input argument in the form of a custom POCO/DTO, and exectue the request.

Because the system is distributed, and the message will generally by multicast to all peers, there are also means by which you can nomiate which peer should participate in the execution of the request.  The simplest means is by providing one or more instance names as the intended recipient(s) of the request.  

Another option is to specify what is termed a Nomination predicate which will be used to filter respondants that do not pass the nomination check.  Typically, the nomination predicate will compare a capacity value provided by the nominee (the determination of which is configured in the nomiee's routing defintion) to a threshold.  Those nominees passing the test, are then elected to process the request.  This situation is most useful when you're looking to load-balance requests across a pool of workers, and you want the most available worker to handle the request.

Each of these situations is illustrated by the sample code shown below.

###Sample Code
```C#
var testRequest = new TestRequest();
var commandRequest = new CommandRequest();

//// executes the default call on the CHANNEL, with no arguments or response
Put.Via(Channels.CHANNEL).Execute();

//// executes with no result on the CHANNEL
Put.Via(Channels.CHANNEL, commandRequest).Execute();

// executes a TestRequest call on the CHANNEL, and returns the first result returned from any respondant
// blocks for the default timeout (Get.DefaultTimeout)
var result1 = Post<TestRequest, TestResponse>.Via(Channels.CHANNEL, testRequest).Execute();


// executes the request on respondants whose capacity exceeds an arbitrary threshold
// the first respondant passing the nomination predicate test and returning a signaling message to the caller
// is then sent the actual request to be processed, ensuring the actual request is only processed on a single remote agent
// nomination scoring is configured as part of the route definition on the recipient
// if no nomination scoring is defined on the recipient, a maximum value of 1.0 is returned for the score
// the Delegate expression is evaluated within the respondants' process, such that failing 
// test prevent the request from beng dispatched to their corresponding handlers, 
// thus preventing both evaluation and responses
TestResponse result2 = Post<TestRequest, TestResponse>
                                .Via(Channels.CHANNEL, testRequest)
                                .Nominate(response => response.Score > TestResponse.SomeNumber())
                                .Execute();

// executes a TestRequest call on the CHANNEL, and returns the first result returned from any respondant
// blocks for the default timeout (Get.DefaultTimeout)
// because this combination of request/response type is not mapped, no response will return within the timeout period specified (500ms)
// a timeout exception is throw in this case, or if none of the respondant results are received in time
try
{
    var result3 = Post<CommandRequest, TestResponse>.Via(Channels.CHANNEL, commandRequest).Execute(500);
}
catch (TimeoutException)
{
    Logger.LogInfo("Handled Timeout");
}

// executes a directed TestRequest call on the CHANNEL, for a specific recipient (App.InstanceName), 
// and returns the first result returned from any respondant
// blocks for up to 500ms
var result4 = Post<TestRequest, TestResponse>.Via(Channels.CHANNEL, testRequest).Execute(500, App.InstanceName);

// executes a TestRequest call on the CHANNEL, and returns all responses received within one second
var enResult1 = Post<TestRequest, TestResponse>.Via(Channels.CHANNEL, testRequest)
                                .All()
                                .Execute(1000)
                                .ToArray();
Debug.Assert(enResult1.Length > 0);

// executes a TestRequest call on the CHANNEL, and returns the first two responses received within the Get.DefaultTimeout time period
// if the terminator condition is not met within the timeout period, the result will contain the responses received up to that time
var enResult2 = Post<TestRequest, TestResponse>
                                .Via(Channels.CHANNEL, testRequest)
                                .Take(2)
                                .Execute()
                                .ToArray();

// executes the request on respondants whose capacity exceeds and arbitrary threshold
// returns enumerable results from all respondants where responses meet an arbitrary predicate (Size > 2) which is evaluated locally
// and blocks for 2000 milliseconds while waiting for responses
// if no responses are received within the timeout, and empty set is returned
// any responses received after the timeout are ignored
var enResult3 = Post<TestRequest, TestResponse>
                                .Via(Channels.CHANNEL, testRequest)
                                .Nominate(cr => cr.Score > 0.9)
                                .Take(r => r.Size > 2)
                                .Execute(2000)
                                .ToArray();

// executes the request on respondants whose capacity exceeds and arbitrary threshold
// returns enumerable results from all respondants where responses meet an arbitrary predicate (Size > 2) which is evaluated locally
// and blocks until the terminal condition is met (ChannelContext.Current.Count > 1)
var enResult4 = Post<TestRequest, TestResponse>
                                .Via(Channels.CHANNEL, testRequest)
                                .Nominate(cr => cr.Score > 0.9)
                                .Take(r => r.Size > 2)
                                .Until(r => ChannelContext.Current.Count > 1)
                                .Execute()
                                .ToArray();

// in this case, because we're executing an enumeration for an unmapped request/response pair, the call will simply block for the 
// timeout period, and return no results.  Enumerations DO NOT throw timeout exceptions in the absence of any responses, only scalar
// execution calls can produce timeout exceptions.
var enResult5 = Post<CommandRequest, TestResponse>
                                .Via(Channels.CHANNEL, commandRequest)
                                .All()
                                .Execute(500)
                                .ToArray();

// an alternative calling pattern, allowing the response type to be deferred until after the channel designation.
// for factory calling patterns, where the construction of the call in terms of what types to return may differ 
// from one channel to the next for the same request type, but your pipeline determines the channel to call 
// before it determines the response.
var ebResult6 = Post.Via(Channels.CHANNEL, testRequest)
                    .Return<TestResponse>()
                    .Nominate(cr => cr.Score > 0.5)
                    .Take(r => r.Size > 2)
                    .Until(r => ChannelContext.Current.Count > 1)
                    .Execute()
                    .ToArray(); 
```

####Message Routing
```C#
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

// route BestEffort messages
router.Route<Handler, TestRequest, TestResponse>(Channels.BESTEFFORT_CHANNEL, (handler, request) => handler.HandleBE(request));
```

####Setting Up DI and Creating Channels
```C#
// sets the DI container adapter to TypeRegistry
App<TypeRegistry>.Initialize();
// get the channel service from the DU container
var channelService = App.Resolve<IChannelService>();
// creates the local channel instance for the CHANNEL service and starts listening
channelService.Create(CHANNEL);
```

####Bootstrapping and Dependency Injection
```C#
/// <summary>
/// Sample Bootstrapper reading from configuration, and providing a dependency resolver, with basic DI Mappings for StructureMap
/// </summary>
public class TypeRegistry : IBootstrapper
{
    /// <summary>
    /// Any byte[] that can be used in the creation of message hashes when communicating with other nodes
    /// </summary>
    public byte[] InstanceCryptoKey
    {
        get
        {
            return Convert.FromBase64String(ConfigurationManager.AppSettings["instanceCryptoKey"]);
        }
    }
    /// <summary>
    /// Globally unique Id for this node
    /// </summary>
    public ulong InstanceId
    {
        get
        {
            return ulong.Parse(ConfigurationManager.AppSettings["instanceId"]);
        }
    }
    /// <summary>
    /// Globally unique Name for this node
    /// </summary>
    public string InstanceName
    {
        get
        {
            return ConfigurationManager.AppSettings["instanceName"];
        }
    }
    /// <summary>
    /// Returns the DI type resolver adapter
    /// </summary>
    /// <returns></returns>
    public IResolveTypes Initialize()
    {
        var channelService = App.ResolveAll<IChannelService>().Single(cs => cs.AvailableServiceLevels == ServiceLevels.Default);
            // create our channel mappings
            channelService.Register(Channels.CHANNEL, Channels.CHANNEL_EP);

            var beChannelService = App.ResolveAll<IChannelService>().Single(cs => cs.AvailableServiceLevels == ServiceLevels.BestEffort);
            // create our best-effort mcast channel mappings
            beChannelService.Register(
                Channels.BESTEFFORT_CHANNEL,
                Channels.BESTEFFORT_CHANNEL_EP,
                Channels.BESTEFFORT_CHANNEL_TTL);

            return new TypeResolver(
                new Container(c =>
            {
                // define additional mappings here
            }));
    }
}

/// <summary>
/// Simple StructureMap DI adapter for Suffūz 
/// </summary>
public class TypeResolver : IResolveTypes
{
    private IContainer _container;

    public TypeResolver(IContainer container)
    {
        _container = container;
    }
    public T Resolve<T>()
    {
        return _container.GetInstance<T>();
    }

    public IEnumerable<T> ResolveAll<T>()
    {
        return _container.GetAllInstances<T>();
    }
}

/// <summary>
/// Simple Channel mapping constants
/// </summary>
public class Channels
{
    /// <summary>
    /// channel1
    /// </summary>
    public static readonly string CHANNEL = "channel1";
    /// <summary>
    /// 224.0.0.0
    /// </summary>
    public static readonly IPEndPoint CHANNEL_EP = new IPEndPoint(IPAddress.Parse("224.0.0.0"), 5000);
    /// <summary>
    /// channel2
    /// </summary>
    public static readonly string BESTEFFORT_CHANNEL = "channel2";
    /// <summary>
    /// 224.0.0.1
    /// </summary>
    public static readonly IPEndPoint BESTEFFORT_CHANNEL_EP = new IPEndPoint(IPAddress.Parse("224.0.0.1"), 5000);
    /// <summary>
    /// 30 seconds
    /// </summary>
    public static readonly TimeSpan BESTEFFORT_CHANNEL_TTL = TimeSpan.FromSeconds(30);
}
```

#Serialization
To support the communications platform, Suffūz includes its own high-speed binary serialization system which can be used with or without the remote execution components.

To serialize a type, you must first decorate the public Properties or Fields with an  Altus.Suffūz.Serialization.Binary.BinarySerializableAttribute (Property members must also have both public Get and Set accessors) and the type must have a public parameterless constructor.

```C#
public class CustomItem
{
    [BinarySerializable(0)]
    public int A { get; set; }
    [BinarySerializable(1)]
    public string B { get; set; }
}
```

The numeric values represent serialization sort order, which must be preserved when serializing over heterogenous type system environments.

To access an instance capable of serializing your type, obtain a reference to Altus.Suffūz.Serialization.ISerializationContext from the DI system, from which you can obtain an ISerializer for your type, as follows:

```C#
var serializationContext = App.Resolve<ISerializationContext>();
var serializer = serializationContext.GetSerializer<ComplexPOCO>(StandardFormats.BINARY);
var bytes = serializer.Serialize(new ComplexPOCO());
var poco = serializer.Deserialize(bytes);
```

##Performance
As a point of comparison, we benchmarked our compiled binary serializer against the latest version of JSON.Net, in both terms of throughput and payload sizes.  On the whole, our serializer outperformed JSON.Net by about an order of magnitude (10x) on throughput, with a bandwidth reduction of around 40%.

Operation         | Suffūz    | JSON.Net
------------------|-----------|---------
Serialization     |           |
Throughput [Mb/s] | 96        | 15
Rate [MHz]        | 1.335     | 0.129
Deserialization   |           | 
Throughput [Mb/s] | 85        | 12
Rate [MHz]        | 1.180     | 0.100


#Persistent Collections
Another significant component system built to support the communications framework consists of a RDMS-Free persistence platform for storing serializable types in an efficient and transacted manner.

To that end, the Altus.Suffūz.Collections namespace supports a number of core ICollection types, that have built-in support for serialization and persistence to disk for collected instances.

Transacted disk writes can also be opted into/out of using familiar System.Transactions.TransactionScope sematics.  Obviously, transacted write operations cause considerable impact to write performance, which is why we allow you to determine the transaction scope in your application.  You can also create collection types where transaction scope is completely disabled.  In those situations, you can influence disk I/O latency by deferring disk flushing behavior by the use of a IDisposable FlushScope, allowing you to nest tight looping updates inside a flush scope, and then flushing them all to disk together in a single operation.

To further enhance performance, we also allow your individual collections to share a common PersistentHeap.  This places all of your serialized types into a single file, reducing disk seeking across many separate files scattered across the disk.

Taken together, the persistent collection types can provide extremely efficient options to replace cumbersome RDMS or NO-SQL alternatives, when you really just need to store a list of serializable types to disk.

##Sample Code
###PersistentHeap
A PersistentHeap places new items at the unwritten end of a file, and allows random access to those items by a numeric key returned after the write operation.

```C#
using (var heap = new PersistentHeap("MyHeap", 1024 * 64))
{
    var item = new CustomItem() { A = 12, B = "Foo" };
    var key1 = heap.Add(item);
    var key2 = heap.Add(item);
}
```

The sample above creates a new heap called "MyHeap", of a 64 KB fixed size, and adds two items to the heap.  By default, the heap is constructed with implicit transactional writing supported, and fixed length.  The file management system uses NTFS Sparse File support when available, allocating actual disk usage in 64 KB chunks, as needed.  So even though you may commit a 10 MB file to disk, initially, in reality, the file will initially only occupy 64 KB on disk, adding 64 KB as you continue to add to the file.

If you needed both of the Add operations shown above to commit as an atomic unit, then simply wrap the operations in a TransactionScope and Complete() them when finished, as shown below.

```C#
using (var heap = new PersistentHeap("MyHeap", 1024 * 64))
{
    var item = new CustomItem() { A = 12, B = "Foo" };
    using (var tx = new TransactionScope())
    {
        var key1 = heap.Add(item);
        var key2 = heap.Add(item);
        tx.Complete();
    }
}
```

If either Add fails in the example above, they will both be rolled back from the Heap's file store.

TransactionScopes can also span multiple PersistentHeaps, like so:

```C#
using (var heap1 = new PersistentHeap("MyHeap1", 1024 * 64))
using (var heap2 = new PersistentHeap("MyHeap2", 1024 * 64))
{
    var item = new CustomItem() { A = 12, B = "Foo" };
    using (var tx = new TransactionScope())
    {
        var key1 = heap1.Add(item);
        var key2 = heap2.Add(item);
        tx.Complete();
    }
}
```

If either Add fails, both heap changes will rollback together.

If you don't want transaction support for your heap, then you can specify an optional constructor argument to disable it, like so:

```C#
using (var heap = new PersistentHeap("MyHeap", 1024 * 64, false))
{
    var item = new CustomItem() { A = 12, B = "Foo" };
    var key1 = heap.Add(item);
    var key2 = heap.Add(item);
}
```

For these heap types, records will be flushed to disk after each write, unless the flush is suppressed by a FlushScope instance, as follows:
```C#
using (var heap = new PersistentHeap("MyHeap", 1024 * 64))
{
    var item = new CustomItem() { A = 12, B = "Foo" };
    using (var tx = new FlushScope())
    {
        var key1 = heap.Add(item);
        var key2 = heap.Add(item);
    }
}
```

The FlushScope instance will trigger a disk Flush when it disposes, for all the heaps contained within its scope.  There is no guarantee that one or more disk flushes won't occur during the write, as those are goverened by lower-level operating system behaviors.  It will only ensure that any write operations within its scope do not explicitly trigger a buffer flush.  In practice (as you will see in the following benchmark section), wrapping nested write operations inside a flush scope can increase write throuput by several orders of magnitude, and likewise for transacted write operations.


####Writing 
PersistentHeaps support the following writing operations:
- Add()
- Write()
- WriteUnsafe()
- Free()

#####Add()
Add() takes an instance to store, appends it to the heap, and returns a ulong key that can be used to access the stored item.

#####Write()
Write() takes an instance to store, plus an existing key value, and, if the new value's serialized length exactly matches the length of the existing item in the storage location provided by the key, then it overwites the current location with the new item, otherwise it marks the current item's storage location as no longer in use, and then Adds the item to the heap, returning a new key.

#####Free()
Free() simply marks the stored item as no longer in use and invalidates its key.

#####WriteUnsafe()
WriteUnsafe() performs an overwrite operation at the address specified by key, without bounds checking.  If you know that the serialized size of your items will ALWAYS be the same length throughout the life of your application, then WriteUnsafe offers a higher performance option for storing items.  However, if the size of the item changes, the heap will be corrupted and will become unusuable.

####Reading
PersistenHeaps support the following reading operations:
- Read()
- Read<TValue>()
- GetEnumerable()
- GetEnumerable\<TValue\>()

#####Read()
Read() returns the deserialized item at the specified key as Object, allowing you to persist mixed types in the non-generic form of PersistentHeap.

#####Read\<TValue\>()
Read\<TValue\>() returns the deserialized item at the specified key as TValue, assuming the cast operation is successful for non-generic PersistentHeaps.  For generic PersistentHeap<TValue>, there is no cast operation, as only TValue can be serialized into the heap, and the heap will only use the serializer that is associated with TValue.

#####GetEnumerable()
GetEnumerable() essentially calls Read() for all items in the heap, and returns them as Object in their storage order on disk.


#####GetEnumerable\<TValue\>()
GetEnumerable\<TValue\>() essentially calls Read<TValue>() for all items in the heap, and returns them as TValue in their storage order on disk.  The collection supports all the Linq to Objects extensions provided for IEnumerable<TValue> types.

####Management
Over time, for non-fixed sized instances, heap sizes will grow.  Calling Free() does not reclaim the memory used by the item, it simply marks it as no longer valid, and therefore available for collection.  Compacting the heap can be expensive, so it is therefore left up to the application to determine when the heap should be compacted (if ever).

#####Compact()
All IPersistentCollection types provide a Compact() method that will scan the entire storage system, shrinking all relevant files to their minimum size.

#####Flush()
All IPersistentCollection types provide a Flush() method allowing the caller to explicity force the collection to write its buffered content to disk.

#####Grow()
Grow() allows your application to explicitly increase the size of your collection by a fixed amount.

#####AutoGrowSize
The AutoGrowSize property allows your application to specify a size to automatizally grow your collection by, in the event that it runs out of capacity.  Setting this to 0 disable auto-growth (default), and causes your collection to maintain a fixed size on disk.


####Performance
To give some qualitative impact of using the TransactionScope and FlushScope objects describe previously, we benchmarked a simple operation that wrote and read thousands of 32-bit integer values to a PersistentHeap using various combinations of scopes and transactional/non-transactional settings. The results are shown below for a relatively modern i7 laptop with SSD storage.


Operation       | Write Rate      | Read Rate      | Flush Scoped | Transaction Scoped
----------------|-----------------|----------------|--------------|-----------------------
Int32 Type      | 2,751 op/sec    | 632,911 op/sec | No           | No (Non-Transactional)
Int32 Type      | 194,931 op/sec  | 662,251 op/sec | Yes          | No (Non-Transactional)
Int32 Type      | 1,611 op/sec    | 625,000 op/sec | No           | No
Int32 Type      | 1,612 op/sec    | 625,000 op/sec | Yes/No       | No
Int32 Type      | 68,493 op/sec   | 625,000 op/sec | Yes/No       | Yes    

As you can see, you can gain or lose much as two orders of magnitude of performance by applying the FlushScope and TransactionScope items to the nested updates.  Read speed was primarily the same as the transaction system allows for uncommitted reads, and uses the same reading technique as the non-transactional collection. 




