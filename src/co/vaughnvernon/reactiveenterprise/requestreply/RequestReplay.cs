using Akka.Actor;
using Akka.Event;
using Xunit;

namespace ReactiveMessagingPatterns.ActorModel.co.vaughnvernon.reactiveenterprise.requestreply
{
    public class Request
    {
        public string What { get; }

        public Request(string what)
        {
            What = what;
        }
    }

    public class Reply
    {
        public string What { get; }

        public Reply(string what)
        {
            What = what;
        }
    }

    public class StartWith
    {
        public IActorRef Server { get; }

        public StartWith(IActorRef server)
        {
            Server = server;
        }
    }

    public class RequestReplayDiver : CompletableApp
    {
        public RequestReplayDiver() : base(1)
        {
        }

        [Fact]
        public void Main()
        {
            var client = Sys.ActorOf(Props.Create<Client>(), "client");
            var server = Sys.ActorOf(Props.Create<Server>(), "server");

            client.Tell(new StartWith(server));


            AwaitCompletion();

            Sys.Log.Info("RequestReply: is completed.");
        }
    }

    public class Client : ReceiveActor
    {
        public Client()
        {
            Receive<StartWith>(server =>
            {
                Context.GetLogger().Info("Client: is starting");
                server.Server.Tell(new Request("REQ-1"));
            });
            Receive<Reply>(what =>
            {
                Context.GetLogger().Info("Client: received response: " + what.What);
                RequestReplayDiver.CompletedStep();
            });
        }
    }

    public class Server : ReceiveActor
    {
        public Server()
        {
            Receive<Request>(what =>
            {
                Context.GetLogger().Info("Server: received request value: " + what.What);
                Sender.Tell(new Reply("RESP-1 for " + what.What));
            });
        }
    }
}