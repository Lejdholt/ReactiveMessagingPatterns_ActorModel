using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Akka.Actor;
using Akka.Event;
using ReactiveMessagingPatterns.ActorModel;
using ReactiveMessagingPatterns.ActorModel.co.vaughnvernon.reactiveenterprise;
using Xunit;
using Xunit.Abstractions;

namespace ReactiveMessagingPatterns.ActorModel1.co.vaughnvernon.reactiveenterprise.aggregator
{
    public class RequestForQuotation
    {
        public RequestForQuotation(string rfqId, params RetailItem[] retailItems)
        {
            RfqId = rfqId;
            RetailItems = retailItems;
        }

        public string RfqId { get; set; }
        public RetailItem[] RetailItems { get; set; }

        public double TotalRetailPrice => RetailItems.Select(item => item.RetailPrice).Sum();
    }

    public class RetailItem
    {
        public RetailItem(string itemId, double retailPrice)
        {
            ItemId = itemId;
            RetailPrice = retailPrice;
        }

        public string ItemId { get; }
        public double RetailPrice { get; }
    }

    public class PriceQuoteInterest
    {
        public PriceQuoteInterest(string quoterId, IActorRef quoteProcessor, double lowTotalRetail, double highTotalRetail)
        {
            QuoterId = quoterId;
            QuoteProcessor = quoteProcessor;
            LowTotalRetail = lowTotalRetail;
            HighTotalRetail = highTotalRetail;
        }

        public string QuoterId { get; }
        public IActorRef QuoteProcessor { get; }
        public double LowTotalRetail { get; }
        public double HighTotalRetail { get; }
    }

    public class RequestPriceQuote
    {
        public RequestPriceQuote(string rfqId, string itemId, double retailPrice, double orderTotalRetailPrice)
        {
            RfqId = rfqId;
            ItemId = itemId;
            RetailPrice = retailPrice;
            OrderTotalRetailPrice = orderTotalRetailPrice;
        }

        public string RfqId { get; }
        public string ItemId { get; }
        public double RetailPrice { get; }
        public double OrderTotalRetailPrice { get; }
    }

    public class PriceQuote : NicePrint
    {
        public PriceQuote(string quoterId, string rfqId, string itemId, double retailPrice, double discountPrice)
        {
            QuoterId = quoterId;
            RfqId = rfqId;
            ItemId = itemId;
            RetailPrice = retailPrice;
            DiscountPrice = discountPrice;
        }

        public string QuoterId { get; }
        public string RfqId { get; }
        public string ItemId { get; }
        public double RetailPrice { get; }
        public double DiscountPrice { get; }
    }

    public class PriceQuoteFulfilled : NicePrint
    {
        public PriceQuoteFulfilled(PriceQuote priceQuote)
        {
            PriceQuote = priceQuote;
        }

        public PriceQuote PriceQuote { get; }
    }

    public class RequiredPriceQuotesForFulfillment
    {
        public RequiredPriceQuotesForFulfillment(string rfqId, int quotesRequested)
        {
            RfqId = rfqId;
            QuotesRequested = quotesRequested;
        }

        public string RfqId { get; }
        public int QuotesRequested { get; }
    }

    public class QuotationFulfillment : NicePrint
    {
        public QuotationFulfillment(string rfqId, int quotesRequested, PriceQuote[] priceQuotes, IActorRef requester)
        {
            RfqId = rfqId;
            QuotesRequested = quotesRequested;
            PriceQuotes = priceQuotes;
            Requester = requester;
        }

        public string RfqId { get; }
        public int QuotesRequested { get; }
        public PriceQuote[] PriceQuotes { get; }
        public IActorRef Requester { get; }
    }

    public class AggregatorDriver : CompletableApp
    {
        public AggregatorDriver(ITestOutputHelper helper) : base(5, helper)
        {
        }

        [Fact]
        public void Main()
        {
            var priceQuoteAggregator = Sys.ActorOf(Props.Create(() => new PriceQuoteAggregator()), "priceQuoteAggregator");
            var orderProcessor = Sys.ActorOf(Props.Create(() => new MountaineeringSuppliesOrderProcessor(priceQuoteAggregator)), "orderProcessor");

            Sys.ActorOf(Props.Create(() => new BudgetHikersPriceQuotes(orderProcessor)), "budgetHikers");
            Sys.ActorOf(Props.Create(() => new HighSierraPriceQuotes(orderProcessor)), "highSierra");
            Sys.ActorOf(Props.Create(() => new MountainAscentPriceQuotes(orderProcessor)), "mountainAscent");
            Sys.ActorOf(Props.Create(() => new PinnacleGearPriceQuotes(orderProcessor)), "pinnacleGear");
            Sys.ActorOf(Props.Create(() => new RockBottomOuterwearPriceQuotes(orderProcessor)), "rockBottomOuterwear");


            Thread.Sleep(1); //required to ensure that price quoters are registered with order processor


            orderProcessor.Tell(new RequestForQuotation("123",
                new RetailItem("1", 29.95),
                new RetailItem("2", 99.95),
                new RetailItem("3", 14.95)));

            orderProcessor.Tell(new RequestForQuotation("125",
                new RetailItem("4", 39.99),
                new RetailItem("5", 199.95),
                new RetailItem("6", 149.95),
                new RetailItem("7", 724.99)));

            orderProcessor.Tell(new RequestForQuotation("129",
                new RetailItem("8", 119.99),
                new RetailItem("9", 499.95),
                new RetailItem("10", 519.00),
                new RetailItem("11", 209.50)));

            orderProcessor.Tell(new RequestForQuotation("135",
                new RetailItem("12", 0.97),
                new RetailItem("13", 9.50),
                new RetailItem("14", 1.99)));

            orderProcessor.Tell(new RequestForQuotation("140",
                new RetailItem("15", 107.50),
                new RetailItem("16", 9.50),
                new RetailItem("17", 599.99),
                new RetailItem("18", 249.95),
                new RetailItem("19", 789.99)));

            AwaitCompletion();
        }
    }

    public class MountaineeringSuppliesOrderProcessor : ReceiveActor
    {
        private readonly Dictionary<string, PriceQuoteInterest> interestRegistry = new Dictionary<string, PriceQuoteInterest>();

        public MountaineeringSuppliesOrderProcessor(IActorRef priceQuoteAggregator)
        {
            Receive<PriceQuoteInterest>(interest =>
            {
                interestRegistry[interest.QuoterId] = interest;
            });
            Receive<PriceQuote>(priceQuote =>
            {
                priceQuoteAggregator.Tell(new PriceQuoteFulfilled(priceQuote));
                Context.GetLogger().Info($"OrderProcessor: received {priceQuote}");
            });
            Receive<RequestForQuotation>(rfq =>
            {
                var recipientList = CalculateRecipientList(rfq);
                priceQuoteAggregator.Tell(new RequiredPriceQuotesForFulfillment(rfq.RfqId, recipientList.Count()*rfq.RetailItems.Length));
                DispatchTo(rfq, recipientList);
            });
            Receive<QuotationFulfillment>(fulfillment =>
            {
                Context.GetLogger().Info($"OrderProcessor: received {fulfillment}");
                CompletableApp.CompletedStep();
            });
        }

        private IEnumerable<IActorRef> CalculateRecipientList(RequestForQuotation rfq)
        {
            return from interest in interestRegistry.Values
                where rfq.TotalRetailPrice >= interest.LowTotalRetail &&
                      rfq.TotalRetailPrice <= interest.HighTotalRetail
                select interest.QuoteProcessor;
        }

        private void DispatchTo(RequestForQuotation rfq, IEnumerable<IActorRef> recipientList)
        {
            foreach (var recipient in recipientList)
            {
                foreach (var retailItem in rfq.RetailItems)
                {
                    Context.GetLogger().Info($"OrderProcessor: {rfq.RfqId} item: {retailItem.ItemId} to: {recipient.Path}");
                    recipient.Tell(new RequestPriceQuote(rfq.RfqId, retailItem.ItemId, retailItem.RetailPrice, rfq.TotalRetailPrice));
                }
            }
        }
    }

    public class PriceQuoteAggregator : ReceiveActor
    {
        private readonly Dictionary<string, QuotationFulfillment> fulfilledPriceQuotes = new Dictionary<string, QuotationFulfillment>();

        public PriceQuoteAggregator()
        {
            Receive<RequiredPriceQuotesForFulfillment>(required =>
            {
                var sender = Sender;
                fulfilledPriceQuotes[required.RfqId] = new QuotationFulfillment(required.RfqId,required.QuotesRequested, new PriceQuote[0], sender);
            });

            Receive<PriceQuoteFulfilled>(priceQuoteFulfilled =>
            {
                var previousFulfillment = fulfilledPriceQuotes[priceQuoteFulfilled.PriceQuote.RfqId];
                var currentPriceQuotes = previousFulfillment.PriceQuotes.With(priceQuoteFulfilled.PriceQuote);
                var currentFulfillment = new QuotationFulfillment(
                    previousFulfillment.RfqId,
                    previousFulfillment.QuotesRequested,
                    currentPriceQuotes,
                    previousFulfillment.Requester);

                if (currentPriceQuotes.Length >= currentFulfillment.QuotesRequested)
                {
                    currentFulfillment.Requester.Tell(currentFulfillment);
                    fulfilledPriceQuotes.Remove(priceQuoteFulfilled.PriceQuote.RfqId);
                }
                else
                {
                    fulfilledPriceQuotes[priceQuoteFulfilled.PriceQuote.RfqId] = currentFulfillment;
                }

                Context.GetLogger().Info($"PriceQuoteAggregator: fulfilled price quote: {priceQuoteFulfilled}");
            });
        }
    }

    public abstract class PriceQuotes : ReceiveActor
    {
        protected PriceQuotes(IActorRef interestRegistrar)
        {
            interestRegistrar.Tell(new PriceQuoteInterest(QuoterId, Self, LowTotalRetail, HighTotalRetail));
            Receive<RequestPriceQuote>(rpq =>
            {
                var sender = Sender;
                var discount = DiscountPercentage(rpq.OrderTotalRetailPrice) * rpq.RetailPrice;
                sender.Tell(new PriceQuote(QuoterId, rpq.RfqId, rpq.ItemId, rpq.RetailPrice, rpq.RetailPrice - discount));
            });
        }

        protected string QuoterId => Self.Path.ToString().Split('/').Last();
        protected abstract double LowTotalRetail { get; }
        protected abstract double HighTotalRetail { get; }

        protected abstract double DiscountPercentage(double orderTotalRetailPrice);
    }

    public class BudgetHikersPriceQuotes : PriceQuotes
    {
        public BudgetHikersPriceQuotes(IActorRef interestRegistrar) : base(interestRegistrar)
        {
        }

        protected override double LowTotalRetail => 1;
        protected override double HighTotalRetail => 1000;

        protected override double DiscountPercentage(double orderTotalRetailPrice)
        {
            if (orderTotalRetailPrice <= 100.00) return 0.02;
            if (orderTotalRetailPrice <= 399.99) return 0.03;
            if (orderTotalRetailPrice <= 499.99) return 0.05;
            if (orderTotalRetailPrice <= 799.99) return 0.07;
            return 0.075;
        }
    }

    public class HighSierraPriceQuotes : PriceQuotes
    {
        public HighSierraPriceQuotes(IActorRef interestRegistrar) : base(interestRegistrar)
        {
        }

        protected override double LowTotalRetail => 100;
        protected override double HighTotalRetail => 10000;

        protected override double DiscountPercentage(double orderTotalRetailPrice)
        {
            if (orderTotalRetailPrice <= 150.00) return 0.015;
            if (orderTotalRetailPrice <= 499.99) return 0.02;
            if (orderTotalRetailPrice <= 999.99) return 0.03;
            if (orderTotalRetailPrice <= 4999.99) return 0.04;
            return 0.05;
        }
    }

    public class MountainAscentPriceQuotes : PriceQuotes
    {
        public MountainAscentPriceQuotes(IActorRef interestRegistrar) : base(interestRegistrar)
        {
        }

        protected override double LowTotalRetail => 70;
        protected override double HighTotalRetail => 5000;

        protected override double DiscountPercentage(double orderTotalRetailPrice)
        {
            if (orderTotalRetailPrice <= 99.99) return 0.01;
            if (orderTotalRetailPrice <= 199.99) return 0.02;
            if (orderTotalRetailPrice <= 499.99) return 0.03;
            if (orderTotalRetailPrice <= 799.99) return 0.04;
            if (orderTotalRetailPrice <= 999.99) return 0.045;
            if (orderTotalRetailPrice <= 2999.99) return 0.0475;
            return 0.05;
        }
    }

    public class PinnacleGearPriceQuotes : PriceQuotes
    {
        public PinnacleGearPriceQuotes(IActorRef interestRegistrar) : base(interestRegistrar)
        {
        }

        protected override double LowTotalRetail => 250;
        protected override double HighTotalRetail => 500000;

        protected override double DiscountPercentage(double orderTotalRetailPrice)
        {
            if (orderTotalRetailPrice <= 299.99) return 0.015;
            if (orderTotalRetailPrice <= 399.99) return 0.0175;
            if (orderTotalRetailPrice <= 499.99) return 0.02;
            if (orderTotalRetailPrice <= 999.99) return 0.03;
            if (orderTotalRetailPrice <= 1199.99) return 0.035;
            if (orderTotalRetailPrice <= 4999.99) return 0.04;
            if (orderTotalRetailPrice <= 7999.99) return 0.05;
            return 0.06;
        }
    }


    public class RockBottomOuterwearPriceQuotes : PriceQuotes
    {
        public RockBottomOuterwearPriceQuotes(IActorRef interestRegistrar) : base(interestRegistrar)
        {
        }

        protected override double LowTotalRetail => 0.5;
        protected override double HighTotalRetail => 7500;

        protected override double DiscountPercentage(double orderTotalRetailPrice)
        {
            if (orderTotalRetailPrice <= 100.00) return 0.015;
            if (orderTotalRetailPrice <= 399.99) return 0.02;
            if (orderTotalRetailPrice <= 499.99) return 0.03;
            if (orderTotalRetailPrice <= 799.99) return 0.04;
            if (orderTotalRetailPrice <= 999.99) return 0.05;
            if (orderTotalRetailPrice <= 2999.99) return 0.06;
            if (orderTotalRetailPrice <= 4999.99) return 0.07;
            if (orderTotalRetailPrice <= 5999.99) return 0.075;
            return 0.08;
        }
    }


  
}