using Newtonsoft.Json;

namespace ReactiveMessagingPatterns.ActorModel
{
    public class NicePrint
    {
        public override string ToString()
        {
            return $"{GetType().Name} {JsonConvert.SerializeObject(this)}";
        }
    }
}