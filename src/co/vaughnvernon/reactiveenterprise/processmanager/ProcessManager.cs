using System;
using System.Collections.Generic;
using Akka.Actor;
using Akka.Event;
using Akka.Util.Internal;
using ReactiveMessagingPatterns.ActorModel;
using ReactiveMessagingPatterns.ActorModel.co.vaughnvernon.reactiveenterprise;
using Xunit;
using Xunit.Abstractions;

namespace ReactiveMessagingPatterns.ActorModel1.co.vaughnvernon.reactiveenterprise.processmanager
{
    public class ProcessManagerDriver : CompletableApp
    {
        public ProcessManagerDriver(ITestOutputHelper helper) : base(5, helper)
        {
        }

        [Fact]
        public void Main()
        {
            var creditBureau =
                Sys.ActorOf(Props.Create<CreditBureau>(),
                    "creditBureau");

            var bank1 =
                Sys.ActorOf(
                    Props.Create<Bank>("bank1", 2.75, 0.30),
                    "bank1");
            var bank2 =
                Sys.ActorOf(
                    Props.Create<Bank>("bank2", 2.73, 0.31),
                    "bank2");
            var bank3 =
                Sys.ActorOf(
                    Props.Create<Bank>("bank3", 2.80, 0.29),
                    "bank3");

            var loanBroker = Sys.ActorOf(
                Props.Create<LoanBroker>(creditBureau,
                    new[] { bank1, bank2, bank3 }),
                "loanBroker");

            loanBroker.Tell(new QuoteBestLoanRate("111-11-1111", 100000, 84));

            AwaitCompletion();
        }
    }

    //=========== ProcessManager

    public class ProcessStarted:NicePrint
    {
        public string ProcessId { get; }
        public IActorRef Process { get; }

        public ProcessStarted(
            string processId,
            IActorRef process)
        {
            ProcessId = processId;
            Process = process;
        }
    }

    public class ProcessStopped : NicePrint
    {
        public string ProcessId { get; }
        public IActorRef Process { get; }

        public ProcessStopped(
            string processId,
            IActorRef process)
        {
            ProcessId = processId;
            Process = process;
        }
    }

    public abstract class ProcessManager : ReceiveActor
    {
        private Dictionary<string, IActorRef> processes = new Dictionary<string, IActorRef>();
        protected ILoggingAdapter Log = Context.GetLogger();

        protected IActorRef ProcessOf(string processId)
        {
            if (processes.ContainsKey(processId))
            {
                return processes[processId];
            }
            return null;
        }

        protected void StartProcess(string processId, IActorRef process)
        {
            if (!processes.ContainsKey(processId))
            {
                processes.Add(processId, process);
                Self.Tell(new ProcessStarted(processId, process));
            }
        }

        protected void StopProcess(string processId)
        {
            if (!processes.ContainsKey(processId))
            {
                var process = processes[processId];
                processes.Remove(processId);
                Self.Tell(new ProcessStopped(processId, process));
            }
        }
    }
    //=========== LoanBroker

    public class QuoteBestLoanRate : NicePrint
    {
        public string TaxId { get; }
        public int Amount { get; }
        public int TermInMonths { get; }

        public QuoteBestLoanRate(string taxId, int amount, int termInMonths)
        {
            TaxId = taxId;
            Amount = amount;
            TermInMonths = termInMonths;
        }
    }

    public class BestLoanRateQuoted : NicePrint
    {
        public string BankId { get; }
        public string LoanRateQuoteId { get; }
        public string TaxId { get; }
        public int Amount { get; }
        public int TermInMonths { get; }
        public int CreditScore { get; }
        public double InterestRate { get; }

        public BestLoanRateQuoted
        (
            string bankId,
            string loanRateQuoteId,
            string taxId,
            int amount,
            int termInMonths,
            int creditScore,
            double interestRate)
        {
            BankId = bankId;
            LoanRateQuoteId = loanRateQuoteId;
            TaxId = taxId;
            Amount = amount;
            TermInMonths = termInMonths;
            CreditScore = creditScore;
            InterestRate = interestRate;
        }
    }

    public class BestLoanRateDenied : NicePrint
    {
        public string LoanRateQuoteId { get; }
        public string TaxId { get; }
        public int Amount { get; }
        public int TermInMonths { get; }
        public int CreditScore { get; }

        public BestLoanRateDenied(
            string loanRateQuoteId,
            string taxId,
            int amount,
            int termInMonths,
            int creditScore)
        {
            LoanRateQuoteId = loanRateQuoteId;
            TaxId = taxId;
            Amount = amount;
            TermInMonths = termInMonths;
            CreditScore = creditScore;
        }
    }

    public class LoanBroker : ProcessManager
    {
        public LoanBroker(IActorRef creditBureau,
                          IActorRef[] banks)
        {
            Receive<BankLoanRateQuoted>(message =>
            {
                Log.Info(message.ToString());

                ProcessOf(message.LoadQuoteReferenceId).Tell(new RecordLoanRateQuote(message.BankId,
                    message.BankLoanRateQuoteId,
                    message.InterestRate));
            });

            Receive<CreditChecked>(message =>
            {
                Log.Info(message.ToString());

                ProcessOf(message.CreditProcessingReferenceId).Tell(new EstablishCreditScoreForLoanRateQuote(message.CreditProcessingReferenceId,
                    message.TaxId,
                    message.Score));
            });

            Receive<CreditScoreForLoanRateQuoteDenied>(message =>
            {
                Log.Info(message.ToString());

                ProcessManagerDriver.CompleteAll();

                var denied =
                    new BestLoanRateDenied(
                        message.LoanRateQuoteId,
                        message.TaxId,
                        message.Amount,
                        message.TermInMonths,
                        message.Score);

                Log.Info($"Would be sent to original requester: {denied}");
            });

            Receive<CreditScoreForLoanRateQuoteEstablished>(message =>
            {
                Log.Info(message.ToString());
                banks.ForEach(bank => bank.Tell(new QuoteLoanRate(
                    message.LoanRateQuoteId,
                    message.TaxId,
                    message.Score,
                    message.Amount,
                    message.TermInMonths)));

                ProcessManagerDriver.CompletedStep();
            });

            Receive<LoanRateBestQuoteFilled>(message =>
            {
                Log.Info(message.ToString());

                ProcessManagerDriver.CompletedStep();

                StopProcess(message.LoanRateQuoteId);

                var best = new BestLoanRateQuoted(
                    message.BestBankLoanRateQuote.BankId,
                    message.LoanRateQuoteId,
                    message.TaxId,
                    message.Amount,
                    message.TermInMonths,
                    message.CreditScore,
                    message.BestBankLoanRateQuote.InterestRate);

                Log.Info($"Would be sent to original requester:{best.ToString()}");
            });

            Receive<LoanRateQuoteRecorded>(message =>
            {
                Log.Info(message.ToString());

                ProcessManagerDriver.CompletedStep();
            });

            Receive<LoanRateQuoteStarted>(message =>
            {
                Log.Info(message.ToString());

                creditBureau.Tell(new CheckCredit(
                    message.LoanRateQuoteId,
                    message.TaxId));
            });

            Receive<LoanRateQuoteTerminated>(message =>
            {
                Log.Info(message.ToString());

                StopProcess(message.LoanRateQuoteId);
            });

            Receive<ProcessStarted>(message =>
            {
                Log.Info(message.ToString());

                message.Process.Tell(new StartLoanRateQuote(banks.Length));
            });

            Receive<ProcessStopped>(message =>
            {
                Log.Info(message.ToString());

                Context.Stop(message.Process);
            });

            Receive<QuoteBestLoanRate>(message =>
            {
                var loanRateQuoteId = LoanRateQuote.Id;

                Log.Info($"{message} for: {loanRateQuoteId}");

                var loanRateQuote =
                    LoanRateQuote.Create(
                        Context.System,
                        loanRateQuoteId,
                        message.TaxId,
                        message.Amount,
                        message.TermInMonths,
                        Self);

                StartProcess(loanRateQuoteId, loanRateQuote);
            });
        }
    }
    //=========== LoanRateQuote

    public class StartLoanRateQuote : NicePrint
    {
        public int ExpectedLoanRateQuotes { get; }

        public StartLoanRateQuote(
            int expectedLoanRateQuotes)
        {
            ExpectedLoanRateQuotes = expectedLoanRateQuotes;
        }
    }

    public class LoanRateQuoteStarted : NicePrint
    {
        public string LoanRateQuoteId { get; }
        public string TaxId { get; }

        public LoanRateQuoteStarted(
            string loanRateQuoteId,
            string taxId)
        {
            LoanRateQuoteId = loanRateQuoteId;
            TaxId = taxId;
        }
    }

    public class TerminateLoanRateQuote : NicePrint
    {
    }

    public class LoanRateQuoteTerminated : NicePrint
    {
        public string LoanRateQuoteId { get; }
        public string TaxId { get; }

        public LoanRateQuoteTerminated(
            string loanRateQuoteId,
            string taxId)
        {
            LoanRateQuoteId = loanRateQuoteId;
            TaxId = taxId;
        }
    }

    public class EstablishCreditScoreForLoanRateQuote : NicePrint
    {
        public string LoanRateQuoteId { get; }
        public string TaxId { get; }
        public int Score { get; }

        public EstablishCreditScoreForLoanRateQuote(
            string loanRateQuoteId,
            string taxId,
            int score)
        {
            LoanRateQuoteId = loanRateQuoteId;
            TaxId = taxId;
            Score = score;
        }
    }

    public class CreditScoreForLoanRateQuoteEstablished : NicePrint
    {
        public string LoanRateQuoteId { get; }
        public string TaxId { get; }
        public int Score { get; }
        public int Amount { get; }
        public int TermInMonths { get; }

        public CreditScoreForLoanRateQuoteEstablished(
            string loanRateQuoteId,
            string taxId,
            int score,
            int amount,
            int termInMonths)
        {
            LoanRateQuoteId = loanRateQuoteId;
            TaxId = taxId;
            Score = score;
            Amount = amount;
            TermInMonths = termInMonths;
        }
    }

    public class CreditScoreForLoanRateQuoteDenied : NicePrint
    {
        public string LoanRateQuoteId { get; }
        public string TaxId { get; }
        public int Amount { get; }
        public int TermInMonths { get; }
        public int Score { get; }

        public CreditScoreForLoanRateQuoteDenied(
            string loanRateQuoteId,
            string taxId,
            int amount,
            int termInMonths,
            int score)
        {
            LoanRateQuoteId = loanRateQuoteId;
            TaxId = taxId;
            Amount = amount;
            TermInMonths = termInMonths;
            Score = score;
        }
    }

    public class RecordLoanRateQuote : NicePrint
    {
        public string BankId { get; }
        public string BankLoanRateQuoteId { get; }
        public double InterestRate { get; }

        public RecordLoanRateQuote(
            string bankId,
            string bankLoanRateQuoteId,
            double interestRate)
        {
            BankId = bankId;
            BankLoanRateQuoteId = bankLoanRateQuoteId;
            InterestRate = interestRate;
        }
    }

    public class LoanRateQuoteRecorded : NicePrint
    {
        public string LoanRateQuoteId { get; }
        public string TaxId { get; }
        public BankLoanRateQuote BankLoanRateQuote { get; }

        public LoanRateQuoteRecorded(
            string loanRateQuoteId,
            string taxId,
            BankLoanRateQuote bankLoanRateQuote)
        {
            LoanRateQuoteId = loanRateQuoteId;
            TaxId = taxId;
            BankLoanRateQuote = bankLoanRateQuote;
        }
    }

    public class LoanRateBestQuoteFilled : NicePrint
    {
        public string LoanRateQuoteId { get; }
        public string TaxId { get; }
        public int Amount { get; }
        public int TermInMonths { get; }
        public int CreditScore { get; }
        public BankLoanRateQuote BestBankLoanRateQuote { get; }

        public LoanRateBestQuoteFilled(
            string loanRateQuoteId,
            string taxId,
            int amount,
            int termInMonths,
            int creditScore,
            BankLoanRateQuote bestBankLoanRateQuote)
        {
            LoanRateQuoteId = loanRateQuoteId;
            TaxId = taxId;
            Amount = amount;
            TermInMonths = termInMonths;
            CreditScore = creditScore;
            BestBankLoanRateQuote = bestBankLoanRateQuote;
        }
    }

    public class BankLoanRateQuote : NicePrint
    {
        public string BankId { get; }
        public string BankLoanRateQuoteId { get; }
        public double InterestRate { get; }

        public BankLoanRateQuote(
            string bankId,
            string bankLoanRateQuoteId,
            double interestRate)
        {
            BankId = bankId;
            BankLoanRateQuoteId = bankLoanRateQuoteId;
            InterestRate = interestRate;
        }
    }

    public class LoanRateQuote : ReceiveActor
    {
        private static Random randomLoanRateQuoteId = new Random();
        private List<BankLoanRateQuote> bankLoanRateQuotes = new List<BankLoanRateQuote>();
        private int creditRatingScore;
        private int expectedLoanRateQuotes;
        public static string Id => randomLoanRateQuoteId.Next(1000).ToString();

        public static IActorRef Create(ActorSystem system, string loanRateQuoteId, string taxId, int amount, int termInMonths, IActorRef loanBroker)
        {
            var loanRateQuote =
                system.ActorOf(Props.Create<LoanRateQuote>(loanRateQuoteId, taxId, amount, termInMonths, loanBroker), "loanRateQuote-" + loanRateQuoteId);

            return loanRateQuote;
        }

        public LoanRateQuote(
            string loanRateQuoteId,
            string taxId,
            int amount,
            int termInMonths,
            IActorRef loanBroker)
        {
            Receive<StartLoanRateQuote>(message =>
            {
                expectedLoanRateQuotes =
                    message.ExpectedLoanRateQuotes;
                loanBroker.Tell(new
                    LoanRateQuoteStarted(
                        loanRateQuoteId,
                        taxId));
            });

            Receive<StartLoanRateQuote>(message =>
            {
                expectedLoanRateQuotes =
                    message.ExpectedLoanRateQuotes;
                loanBroker.Tell(new
                    LoanRateQuoteStarted(
                        loanRateQuoteId,
                        taxId));
            });

            Receive<EstablishCreditScoreForLoanRateQuote>(message =>
            {
                creditRatingScore = message.Score;
                if (QuotableCreditScore(creditRatingScore))
                    loanBroker.Tell(new
                        CreditScoreForLoanRateQuoteEstablished(
                            loanRateQuoteId,
                            taxId,
                            creditRatingScore,
                            amount,
                            termInMonths));
                else
                    loanBroker.Tell(new
                        CreditScoreForLoanRateQuoteDenied(
                            loanRateQuoteId,
                            taxId,
                            amount,
                            termInMonths,
                            creditRatingScore));
            });
            Receive<RecordLoanRateQuote>(message =>
            {
                var bankLoanRateQuote =
                    new BankLoanRateQuote(
                        message.BankId,
                        message.BankLoanRateQuoteId,
                        message.InterestRate);
                bankLoanRateQuotes.Add(bankLoanRateQuote);
                loanBroker.Tell(new LoanRateQuoteRecorded(loanRateQuoteId, taxId, bankLoanRateQuote));

                if (bankLoanRateQuotes.Count >=expectedLoanRateQuotes)
                    loanBroker.Tell(new LoanRateBestQuoteFilled(loanRateQuoteId, taxId, amount, termInMonths, creditRatingScore, BestBankLoanRateQuote()));
            });
            Receive<TerminateLoanRateQuote>(message =>
            {
                loanBroker.Tell(new
                    LoanRateQuoteTerminated(
                        loanRateQuoteId,
                        taxId));
            });
        }

        private BankLoanRateQuote BestBankLoanRateQuote()
        {
            var best = bankLoanRateQuotes[0];
            bankLoanRateQuotes.ForEach(bankLoanRateQuote =>
            {
                if (best.InterestRate > bankLoanRateQuote.InterestRate)
                {
                    best = bankLoanRateQuote;
                }
            });

            return best;
        }

        private bool QuotableCreditScore(int score) => score > 399;
    }

    //=========== CreditBureau

    public class CheckCredit : NicePrint
    {
        public string CreditProcessingReferenceId { get; }
        public string TaxId { get; }

        public CheckCredit(string creditProcessingReferenceId, string taxId)
        {
            CreditProcessingReferenceId = creditProcessingReferenceId;
            TaxId = taxId;
        }
    }

    public class CreditChecked : NicePrint
    {
        public string CreditProcessingReferenceId { get; }
        public string TaxId { get; }
        public int Score { get; }

        public CreditChecked(string creditProcessingReferenceId, string taxId, int score)
        {
            CreditProcessingReferenceId = creditProcessingReferenceId;
            TaxId = taxId;
            Score = score;
        }
    }

    public class CreditBureau : ReceiveActor
    {
        private readonly int[] creditRanges = new int[] { 300, 400, 500, 600, 700 };
        private readonly Random randomCreditRangeGenerator = new Random();
        private readonly Random randomCreditScoreGenerator = new Random();

        public CreditBureau()
        {
            Receive<CheckCredit>(message =>
            {
                var range = creditRanges[randomCreditRangeGenerator.Next(0, 4)];

                var score =
                    range
                    + randomCreditScoreGenerator.Next(20);

                Sender.Tell(new CreditChecked(message.CreditProcessingReferenceId, message.TaxId, score));
            });
        }
    }

    //=========== Bank

    public class QuoteLoanRate : NicePrint
    {
        public string LoadQuoteReferenceId { get; }
        public string TaxId { get; }
        public int CreditScore { get; }
        public int Amount { get; }
        public int TermInMonths { get; }

        public QuoteLoanRate(
            string loadQuoteReferenceId,
            string taxId,
            int creditScore,
            int amount,
            int termInMonths)
        {
            LoadQuoteReferenceId = loadQuoteReferenceId;
            TaxId = taxId;
            CreditScore = creditScore;
            Amount = amount;
            TermInMonths = termInMonths;
        }
    }

    public class BankLoanRateQuoted : NicePrint
    {
        public string BankId { get; }
        public string BankLoanRateQuoteId { get; }
        public string LoadQuoteReferenceId { get; }
        public string TaxId { get; }
        public double InterestRate { get; }

        public BankLoanRateQuoted(
            string bankId,
            string bankLoanRateQuoteId,
            string loadQuoteReferenceId,
            string taxId,
            double interestRate)
        {
            BankId = bankId;
            BankLoanRateQuoteId = bankLoanRateQuoteId;
            LoadQuoteReferenceId = loadQuoteReferenceId;
            TaxId = taxId;
            InterestRate = interestRate;
        }
    }

    public class Bank : ReceiveActor
    {
        private readonly string bankId;
        private readonly double primeRate;
        private readonly double ratePremium;
        private Random randomDiscount = new Random();
        private Random randomQuoteId = new Random();

        public Bank(
            string bankId,
            double primeRate,
            double ratePremium)
        {
            this.bankId = bankId;
            this.primeRate = primeRate;
            this.ratePremium = ratePremium;

            Receive<QuoteLoanRate>(message =>
            {
                var interestRate =
                    calculateInterestRate(
                        message.Amount,
                        message.TermInMonths,
                        message.CreditScore);

                Sender.Tell(new BankLoanRateQuoted(
                    bankId, randomQuoteId.Next(1000).ToString(),
                    message.LoadQuoteReferenceId, message.TaxId, interestRate));
            });
        }

        private double calculateInterestRate(double amount, double months, double creditScore)
        {
            var creditScoreDiscount = creditScore / 100.0 / 10.0 - (randomDiscount.Next(5) * 0.05);
            return primeRate + ratePremium + ((months / 12.0) / 10.0) - creditScoreDiscount;
        }
    }
}