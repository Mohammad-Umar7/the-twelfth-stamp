using System;
using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>Builds cells on demand (once), keeps one active, and provides destination pockets for tears.</summary>
    public class CellManager : MonoBehaviour
    {
        public static CellManager I { get; private set; }
        readonly Dictionary<CellId, Cell> m_Cells = new Dictionary<CellId, Cell>();
        public Cell Active { get; private set; }

        public static readonly Dictionary<CellId, Func<GameObject, Cell>> Factory = new Dictionary<CellId, Func<GameObject, Cell>>();

        public static CellManager Create(Transform parent)
        {
            var go = new GameObject("Cells");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<CellManager>();
            CellRegistry.RegisterAll();
            return I;
        }

        public Cell Get(CellId id) => m_Cells.TryGetValue(id, out var c) ? c : null;

        /// <summary>Get or build a cell (inactive).</summary>
        public Cell Prepare(CellId id)
        {
            if (m_Cells.TryGetValue(id, out var c) && c != null) return c;
            if (!Factory.TryGetValue(id, out var make))
            {
                Debug.LogError("[TS-Cells] No builder for " + id);
                return null;
            }
            var go = new GameObject("Cell_" + id);
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            c = make(go);
            c.Id = id;
            var t0 = Time.realtimeSinceStartup;
            c.EnsureBuilt();
            Debug.Log($"[TS-Cells] Built {id} in {(Time.realtimeSinceStartup - t0) * 1000f:0} ms");
            m_Cells[id] = c;
            return c;
        }

        /// <summary>Make a cell the world (caller handles fades and player placement).</summary>
        public Cell Activate(CellId id)
        {
            var next = Prepare(id);
            if (next == null) return null;
            if (Active != null && Active != next)
            {
                if (Active.Encounter != null) { Active.Encounter.OnExit(); Active.Encounter.Entered = false; }
                Active.Deactivate();
            }
            if (next.Encounter != null) next.Encounter.Entered = false;
            Game.Tears?.ForceReset();
            Active = next;
            next.Activate();
            PostFx.I?.SetPreset(next.Lighting);
            AudioDirector.I?.SetAmbience(string.IsNullOrEmpty(next.Ambience) ? AmbienceOf(id) : next.Ambience);
            if (!string.IsNullOrEmpty(next.Music)) AudioDirector.I?.SetMusic(next.Music);
            // The menu's music doesn't follow the player into the story: places without music of their own play their
            // ambience (chapters and the chase set music explicitly).
            else if (AudioDirector.I != null && AudioDirector.I.CurrentMusic == "mus_menu") AudioDirector.I.SetMusic(null, 2f);
            Game.Passport?.Refresh();
            return next;
        }

        public void DeactivateAll()
        {
            foreach (var kv in m_Cells) if (kv.Value != null) kv.Value.Deactivate();
            Active = null;
        }

        public static string AmbienceOf(CellId id)
        {
            switch (id)
            {
                case CellId.ShopNormal: return "amb_shop";
                case CellId.ApartmentNormal: return "amb_apartment";
                case CellId.ApartmentCorrupt: return "amb_apartment_corrupt";
                case CellId.Egypt: return "amb_egypt";
                case CellId.Paris: return "amb_paris_rain";
                case CellId.NewYork: return "amb_ny_harbour";
                case CellId.Japan: return "amb_japan_night";
                case CellId.Riyadh: return "amb_riyadh_wind";
                case CellId.Agra: return "amb_india_garden";
                case CellId.Rio: return "amb_rio_frozen";
                case CellId.London: return "amb_london_river";
                case CellId.ShopCorrupt: return "amb_margin";
                case CellId.Berlin: return "amb_berlin_hall";
                case CellId.Menu: return "amb_apartment";
                default: return "amb_margin";
            }
        }
    }
}
