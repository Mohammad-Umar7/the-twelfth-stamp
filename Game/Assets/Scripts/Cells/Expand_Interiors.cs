using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// The two indoor countries get their postcards too: the UAE is the Margin's copy of home (ApartmentCorrupt) and
    /// Morocco is the Margin's copy of the Marrakech shop (ShopCorrupt). Kept off the seams, the bathroom where the
    /// Passenger waits, the bag, and the counter puzzle.
    /// </summary>
    public static class Expand_Interiors
    {
        public static void UaePostcards(ApartmentCell cell)
        {
            if (!cell.Corrupted) return;
            Postcard.Create(cell.transform, CountryId.UAE, 1, new Vector3(-1.6f, 1.2f, 0.4f), "Everything here is where I left it. That is how I knew it wasn't.");
            Postcard.Create(cell.transform, CountryId.UAE, 2, new Vector3(3.2f, 1.2f, 2.2f), "The window shows the city a minute late. I waved. It waved back, slowly.");
            Postcard.Create(cell.transform, CountryId.UAE, 3, new Vector3(-3.4f, 1.15f, 3.1f), "Someone keeps the kettle warm here. I never see who.");
        }

        public static void MoroccoPostcards(ShopCell cell)
        {
            if (!cell.Corrupted) return;
            // The aisles beyond the shelves are only seen (full-depth shelf units 1.7 m apart, no way between them), so
            // all three stay inside the shop: the mouth of the aisles, by the shelves, behind the counter.
            Postcard.Create(cell.transform, CountryId.Morocco, 1, new Vector3(-1.75f, 1.2f, -2.45f), "The aisle goes on past the last lamp. I counted shelves until I lost my name.");
            Postcard.Create(cell.transform, CountryId.Morocco, 2, new Vector3(1.75f, 1.25f, -1.5f), "Every camera on these shelves has my face in its last frame.");
            Postcard.Create(cell.transform, CountryId.Morocco, 3, new Vector3(0.95f, 1.3f, 0.8f), "The shopkeeper's chair is still warm. The tea is not.");
        }
    }
}
