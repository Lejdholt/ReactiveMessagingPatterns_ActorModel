using System;
using Akka.Actor;
using Akka.Util;
using Newtonsoft.Json;

namespace ReactiveMessagingPatterns.ActorModel
{
    public class NicePrint
    {
        public override string ToString()
        {
            return $"{GetType().Name} {JsonConvert.SerializeObject(this,Formatting.Indented,new ActorRefConverter())}";
        }


    }


    public class ActorRefConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(value.GetType().Name);
            writer.WriteValue(Convert.ToString(value));
            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        { 
          return  objectType.Implements<IActorRef>();
        }
    }

}