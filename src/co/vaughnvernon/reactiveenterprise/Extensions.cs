using System.Linq;

namespace ReactiveMessagingPatterns.ActorModel1.co.vaughnvernon.reactiveenterprise
{
    public static class Extensions
    {
        public static TObject[] With<TObject>(this TObject[] original, TObject subject)
        {
            var clone = original.ToList();
            clone.Add(subject);
            return clone.ToArray();
        }
    }
}