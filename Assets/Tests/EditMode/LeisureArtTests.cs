using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class LeisureArtTests
    {
        [TestCase("leisure_gym", "gym_6x1")]
        [TestCase("leisure_spa", "spa_6x1")]
        [TestCase("leisure_pool", "pool_8x2")]
        [TestCase("leisure_bowling", "bowling_10x1")]
        [TestCase("leisure_theater", "theater_8x2")]
        public void Leisure_dollhouse_leaf_mapped(string id, string leaf)
        {
            Assert.AreEqual(leaf, RoomDollhouseArt.ResourceLeaf(id));
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            Assert.IsTrue(RoomDollhouseArt.IsMapped(so));
            var tex = Resources.Load<Texture2D>(RoomDollhouseArt.ResourcePath(id));
            Assert.IsNotNull(tex, id);
        }

        [TestCase("leisure_gym")]
        [TestCase("leisure_spa")]
        [TestCase("leisure_pool")]
        [TestCase("leisure_bowling")]
        [TestCase("leisure_theater")]
        [TestCase("family_leisure")]
        public void Leisure_menu_icon_loads(string iconId)
        {
            Assert.IsTrue(MenuIconArt.TryGetTexture(iconId, out var tex), iconId);
            Assert.IsNotNull(tex, iconId);
        }
    }
}
