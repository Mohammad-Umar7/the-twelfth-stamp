namespace TwelfthStamp
{
    /// <summary>Maps CellIds to their builders. A cell missing here is simply not reachable (never a crash).</summary>
    public static class CellRegistry
    {
        public static void RegisterAll()
        {
            var f = CellManager.Factory;
            f.Clear();
            f[CellId.Menu] = go => go.AddComponent<MenuCell>();
            f[CellId.Egypt] = go => go.AddComponent<EgyptCell>();
            f[CellId.Paris] = go => go.AddComponent<ParisCell>();
            f[CellId.ApartmentNormal] = go => ApartmentCell.Make(go, false);
            f[CellId.ApartmentCorrupt] = go => ApartmentCell.Make(go, true);
            f[CellId.ShopNormal] = go => ShopCell.Make(go, false);
            f[CellId.ShopCorrupt] = go => ShopCell.Make(go, true);
            f[CellId.NewYork] = go => go.AddComponent<NewYorkCell>();
            f[CellId.Japan] = go => go.AddComponent<JapanCell>();
            f[CellId.Riyadh] = go => go.AddComponent<RiyadhCell>();
            f[CellId.Agra] = go => go.AddComponent<AgraCell>();
            f[CellId.Rio] = go => go.AddComponent<RioCell>();
            f[CellId.London] = go => go.AddComponent<LondonCell>();
            f[CellId.Berlin] = go => go.AddComponent<BerlinCell>();
            f[CellId.VictoryGround] = go => go.AddComponent<VictoryCell>();
        }
    }
}
