using System;
using Akka.Actor;
using Akka.Event;
using ReactiveMessagingPatterns.ActorModel1.co.vaughnvernon.reactiveenterprise;
using Xunit;
using Xunit.Abstractions;
using static ReactiveMessagingPatterns.ActorModel.co.vaughnvernon.reactiveenterprise.messagemetadata.MetaData;

namespace ReactiveMessagingPatterns.ActorModel.co.vaughnvernon.reactiveenterprise.messagemetadata
{
    public class MessageMetaDataDriver : CompletableApp
    {
        public MessageMetaDataDriver(ITestOutputHelper helper) : base(3, helper)
        {
        }

        [Fact]
        public void Main()
        {
            var processor3 = Sys.ActorOf(Props.Create(() => new Processor(ActorRefs.Nobody)), "processor3");
            var processor2 = Sys.ActorOf(Props.Create(() => new Processor(processor3)), "processor2");
            var processor1 = Sys.ActorOf(Props.Create(() => new Processor(processor2)), "processor1");

            var entry = new Entry(
                new Who("driver"),
                new What("Started"),
                new Where(GetType().Name, "driver"),
                DateTime.Now,
                new Why("Running processors"));

            processor1.Tell(new SomeMessage("Data...", entry.AsMetaData()));

            AwaitCompletion();
        }
    }

    public class MetaData
    {
        public class Who
        {
            public string Name { get; }

            public Who(string name)
            {
                Name = name;
            }
        }

        public class What
        {
            public string Happened { get; }

            public What(string happened)
            {
                Happened = happened;
            }
        }

        public class Where
        {
            public string ActorType { get; }
            public string ActorName { get; }

            public Where(string actorType, string actorName)
            {
                ActorType = actorType;
                ActorName = actorName;
            }
        }

        public class Why
        {
            public string Explanation { get; }

            public Why(string explanation)
            {
                Explanation = explanation;
            }
        }

        public class Entry
        {
            public Who Who { get; }
            public What What { get; }
            public Where Where { get; }
            public DateTime When { get; }
            public Why Why { get; }

            public Entry(Who who, What what, Where where, DateTime when, Why why)
            {
                Who = who;
                What = what;
                Where = @where;
                When = when;
                Why = why;
            }

            public Entry(Who who, What what, Where @where, Why why) : this(who, what, where, DateTime.Now, why)
            {
            }

            public Entry(Who who, What what, string actorType, string actorName, Why why)
                : this(who, what, new Where(actorType, actorName), DateTime.Now, why)
            {
            }

            public MetaData AsMetaData()
            {
                return new MetaData(this);
            }
        }

        public readonly Entry[] Entries;


        public MetaData(params Entry[] entries)
        {
            Entries = entries;
        }

        public MetaData() : this(new Entry[0])
        {
        }

        public MetaData Including(Entry entry)
        {
            return new MetaData(Entries.With(entry));
        }
    }

    public class SomeMessage : NicePrint
    {
        public string Payload { get; }
        public MetaData MetaData { get; }

        public SomeMessage(string payload, MetaData metaData)
        {
            Payload = payload;
            MetaData = metaData;
        }

        public SomeMessage Including(Entry entry)
        {
            return new SomeMessage(Payload, MetaData.Including(entry));
        }
    }

    public class Processor : ReceiveActor
    {
        public Processor(IActorRef next)
        {
            var user = $"user{new Random().Next(10)}";
            var wasProcessed = $"Processed: {new Random().Next(5)}";
            var because = $"Because: {new Random().Next(10)}";

            Entry entry = new Entry(
                new Who(user),
                new What(wasProcessed),
                new Where(GetType().Name, Self.Path.Name),
                new Why(because));


            Receive<SomeMessage>(message =>
            {
                Report(message);

                var nextMessage = message.Including(entry);

                if (!next.IsNobody())
                {
                    next.Tell(nextMessage);
                }
                else
                {
                    Report(nextMessage, "complete");
                }

                MessageMetaDataDriver.CompletedStep();
            });
        }

        public void Report(SomeMessage message, string heading = "recevied")
        {
            Context.GetLogger().Info($"{Self.Path.Name} {heading}:{message}");
        }
    }
}