using System.Threading;
using Akka.TestKit.Xunit2;
using Xunit.Abstractions;

namespace ReactiveMessagingPatterns.ActorModel.co.vaughnvernon.reactiveenterprise
{
    public class CompletableApp : TestKit
    {
        protected readonly ITestOutputHelper helper;

        private static CountdownEvent completion;
        private static CountdownEvent canStart;
        private static CountdownEvent canComplete;

        public CompletableApp(int steps, ITestOutputHelper helper) : base(output: helper)
        {
            this.helper = helper;
            completion = new CountdownEvent(steps);
            canStart = new CountdownEvent(1);
            canComplete = new CountdownEvent(1);
        }


        public static void AwaitCompletion()
        {
            completion.Wait();
        }

        public static void CompletedStep()
        {
            completion.Signal();
        }

        public static void CompleteAll() {
            while (completion.CurrentCount > 0)
            {
                completion.Signal();
            }
        }
    }
}