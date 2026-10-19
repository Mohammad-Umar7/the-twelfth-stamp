using System.Collections;

namespace TwelfthStamp
{
    /// <summary>Shop prologue (Marrakech). Filled in by the ShopNormal cell's chapter script.</summary>
    public static class Prologue
    {
        public static System.Func<IEnumerator> Routine;
        public static IEnumerator Run()
        {
            if (Routine != null) yield return Routine();
        }
    }

    /// <summary>Homecoming (Abu Dhabi apartment): unpacking, the voicemail, putting on the glasses.</summary>
    public static class Homecoming
    {
        public static System.Func<IEnumerator> Routine;
        public static IEnumerator Run()
        {
            if (Routine != null) yield return Routine();
        }
    }
}
