using CarWash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CarWash.Tests
{
    /// <summary>docs/ART_IMPORT.md rules are applied by ArtImportPostprocessor.</summary>
    public class ArtImportTests
    {
        [Test]
        public void BorderTable_IsParsed()
        {
            ArtImportPostprocessor.ResetCache();
            var b = ArtImportPostprocessor.Borders;
            Assert.AreEqual(11, b.Count, "11 sliced UI frames in the table");
            Assert.AreEqual(new Vector4(28, 34, 28, 28), b["ui_panel"]);
            Assert.AreEqual(new Vector4(24, 22, 24, 22), b["ui_btn_pressed"]);
            Assert.IsFalse(b.ContainsKey("ui_crosshair"));
        }

        [TestCase("Frames/ui_panel", 28, 34, 28, 28)]
        [TestCase("Frames/ui_btn_primary", 24, 26, 24, 22)]
        [TestCase("HUD/ui_slot", 24, 28, 24, 24)]
        [TestCase("HUD/ui_crosshair", 0, 0, 0, 0)]
        public void UiSprites_HaveSpecImportSettings(string rel, float l, float bo, float r, float t)
        {
            var path = ArtImportPostprocessor.UiRoot + rel + ".png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.NotNull(ti, path);
            Assert.AreEqual(TextureImporterType.Sprite, ti.textureType);
            Assert.AreEqual(SpriteImportMode.Single, ti.spriteImportMode);
            Assert.IsFalse(ti.mipmapEnabled);
            Assert.IsTrue(ti.alphaIsTransparency);
            Assert.AreEqual(100f, ti.spritePixelsPerUnit);
            Assert.AreEqual(TextureImporterCompression.CompressedHQ, ti.textureCompression);
            var s = new TextureImporterSettings(); ti.ReadTextureSettings(s);
            Assert.AreEqual(SpriteMeshType.FullRect, s.spriteMeshType);
            Assert.AreEqual(new Vector4(l, bo, r, t), ti.spriteBorder);
        }

        [TestCase("fx_splash_sheet")]
        [TestCase("fx_water_droplet")]
        [TestCase("fx_foam_decal")]
        public void VfxTextures_HaveSpecImportSettings(string name)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(ArtImportPostprocessor.VfxRoot + name + ".png");
            Assert.NotNull(ti);
            Assert.AreEqual(TextureImporterType.Default, ti.textureType);
            Assert.IsTrue(ti.mipmapEnabled);
            Assert.IsTrue(ti.sRGBTexture);
            Assert.IsTrue(ti.alphaIsTransparency);
            Assert.AreEqual(TextureWrapMode.Clamp, ti.wrapMode);
        }

        [Test]
        public void Skin_HasAllSprites()
        {
            var skin = ArtBuilder.BuildSkin();
            foreach (var f in typeof(UI.UiSkin).GetFields())
                if (f.FieldType == typeof(Sprite)) Assert.NotNull(f.GetValue(skin), f.Name);
        }

        [Test]
        public void Skin_HasAll34IconsAndEveryToolIcon()
        {
            var skin = ArtBuilder.BuildSkin();
            Assert.AreEqual(34, skin.icons.Length);
            foreach (var id in new[] { "T01", "T02", "T03", "T04", "T05", "T06", "T07", "T08" })
                Assert.NotNull(skin.ToolIcon(id), id);
        }
    }
}
