using Akka.Actor;
using Akka.Event;
using ReactiveMessagingPatterns.ActorModel;
using ReactiveMessagingPatterns.ActorModel.co.vaughnvernon.reactiveenterprise;
using Xunit;
using Xunit.Abstractions;

namespace ReactiveMessagingPatterns.ActorModel1.co.vaughnvernon.reactiveenterprise.publishsubscribe
{
    public class SubClassificationDriver : CompletableApp
    {
        public SubClassificationDriver(ITestOutputHelper helper) : base(6, helper)
        {
        }

        [Fact]
        public void Main()
        {
            var allSubscriber = Sys.ActorOf(Props.Create<AllMarketsSubscriber>(), "AllMarketsSubscriber");
            var nasdaqSubscriber = Sys.ActorOf(Props.Create<NASDAQSubscriber>(), "NASDAQSubscriber");
            var nyseSubscriber = Sys.ActorOf(Props.Create<NYSESubscriber>(), "NYSESubscriber");

            var quotesBus = new QuotesEventBus();

            quotesBus.Subscribe(allSubscriber, new Market("quotes"));
            quotesBus.Subscribe(nasdaqSubscriber, new Market("quotes/NASDAQ"));
            quotesBus.Subscribe(nyseSubscriber, new Market("quotes/NYSE"));

            quotesBus.Publish(new PriceQuoted(new Market("quotes/NYSE"), "ORCL", new Money(37.84m)));
            quotesBus.Publish(new PriceQuoted(new Market("quotes/NASDAQ"), "MSFT", new Money(37.16m)));
            quotesBus.Publish(new PriceQuoted(new Market("quotes/DAX"), "SAP:GR", new Money(61.95m)));
            quotesBus.Publish(new PriceQuoted(new Market("quotes/NKY"), "6701:JP", new Money(237m)));

            AwaitCompletion();
        }
    }

    class Money
    {
        public decimal Amount { get; }

        public Money(decimal amount)
        {
            Amount = amount;
        }
    }

    class Market
    {
        public string Name { get; }

        public Market(string name)
        {
            Name = name;
        }
    }

    class PriceQuoted : NicePrint
    {
        public Market Market { get; }
        public string Ticker { get; }
        public Money Price { get; }

        public PriceQuoted(Market market, string ticker, Money price)
        {
            Market = market;
            Ticker = ticker;
            Price = price;
        }
    }

    class QuotesEventBus : EventBus<PriceQuoted, Market, IActorRef>
    {
        protected override bool Classify(PriceQuoted @event, Market classifier)
        {
            return @event.Market.Name.StartsWith(classifier.Name);
        }

        protected override bool IsSubClassification(Market parent, Market child)
        {
            return child.Name.StartsWith(parent.Name);
        }

        protected override void Publish(PriceQuoted @event, IActorRef subscriber)
        {
            subscriber.Tell(@event);
        }

        protected override Market GetClassifier(PriceQuoted @event)
        {
            return @event.Market;
        }
    }

    public class AllMarketsSubscriber : ReceiveActor
    {
        public AllMarketsSubscriber()
        {
            Receive<PriceQuoted>(quote =>
            {
                Context.GetLogger().Info($"AllMarketsSubscriber received: {quote}");
                SubClassificationDriver.CompletedStep();
            });
        }
    }

    public class NASDAQSubscriber : ReceiveActor
    {
        public NASDAQSubscriber()
        {
            Receive<PriceQuoted>(quote =>
            {
                Context.GetLogger().Info($"NASDAQSubscriber received: {quote}");
                SubClassificationDriver.CompletedStep();
            });
        }
    }

    public class NYSESubscriber : ReceiveActor
    {
        public NYSESubscriber()
        {
            Receive<PriceQuoted>(quote =>
            {
                Context.GetLogger().Info($"NYSESubscriber received: {quote}");
                SubClassificationDriver.CompletedStep();
            });
        }
    }
}