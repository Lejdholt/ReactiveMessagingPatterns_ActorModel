using Akka.Actor;
using Akka.Event;
using Xunit;

namespace ReactiveMessagingPatterns.ActorModel.co.vaughnvernon.reactiveenterprise.wiretap
{
    public class WireTapDiver : CompletableApp
    {
        public WireTapDiver() : base(2)
        {

        }

        [Fact]
        public void Main()
        {
            var order = new Order("123");

            var orderProcessor = Sys.ActorOf(Props.Create(() => new OrderProcessor()), "orderProcessor");

            var logger = Sys.ActorOf(Props.Create(() => new MessageLogger(orderProcessor)), "logger");

            var orderProcessorWireTap = logger;

            orderProcessorWireTap.Tell(new ProcessOrder(order));

            AwaitCompletion();
        }
    }

    public class MessageLogger : UntypedActor
    {
        private readonly IActorRef messageReceiver;

        public MessageLogger(IActorRef messageReceiver)
        {
            this.messageReceiver = messageReceiver;
        }

        protected override void OnReceive(object message)
        {
            Context.GetLogger().Info($"LOG: {message}");
            messageReceiver.Tell(message);
            WireTapDiver.CompletedStep();
        }
    }

    public class Order : NicePrint
    {
        public string OrderId { get; }

        public Order(string orderId) { OrderId = orderId; }
    }

    public class ProcessOrder : NicePrint
    {
        public Order Order { get; }

        public ProcessOrder(Order order) { Order = order; }
    }

    public class OrderProcessor : ReceiveActor
    {
        public OrderProcessor()
        {
            Receive<ProcessOrder>(command =>
            {
                Context.GetLogger().Info($"OrderProcessor: received: {command}");
                WireTapDiver.CompletedStep();
            });
        }
    }
}