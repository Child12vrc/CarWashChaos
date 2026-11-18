using System.Linq;
using CarWash.Core;
using CarWash.Editor;
using CarWash.Net;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarWash.Tests
{
    /// <summary>P7: release build content (dev yard stripped, scenes), join codes, LAN IP, command line.</summary>
    public class ReleaseTests
    {
        [TestCase("abc123", "ABC123")]
        [TestCase("  ab c1-23 ", "ABC123")]
        [TestCase("", "")]
        public void JoinCodeIsNormalised(string raw, string expected) => Assert.AreEqual(expected, RelayConnector.NormalizeCode(raw));

        [Test]
        public void JoinCodeValidation()
        {
            Assert.IsTrue(RelayConnector.IsValidCode("K7Q2MD"));
            Assert.IsFalse(RelayConnector.IsValidCode("K7Q2M"));
            Assert.IsFalse(RelayConnector.IsValidCode("K7Q2MDX"));
            Assert.IsFalse(RelayConnector.IsValidCode("k7q2md")); // must be normalised first
            Assert.IsFalse(RelayConnector.IsValidCode(null));
        }

        [TestCase("192.168.0.12", true)]
        [TestCase("10.1.2.3", true)]
        [TestCase("172.16.0.1", true)]
        [TestCase("172.31.255.1", true)]
        [TestCase("172.32.0.1", false)]
        [TestCase("8.8.8.8", false)]
        public void PrivateIpDetection(string ip, bool priv) => Assert.AreEqual(priv, NetSession.IsPrivate(ip));

        [Test]
        public void LocalIpIsIPv4()
        {
            var ip = NetSession.LocalIPv4();
            Assert.IsTrue(System.Net.IPAddress.TryParse(ip, out var a) && a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork, ip);
        }

        [Test]
        public void CommandLineRelayArgs()
        {
            NetSession.ParseArgs(new[] { "x.exe", "-joincode", "k7q2md" });
            Assert.AreEqual("k7q2md", NetSession.ArgJoinCode);
            Assert.IsFalse(NetSession.ArgHostRelay);
            NetSession.ParseArgs(new[] { "x.exe", "-host-relay" });
            Assert.IsTrue(NetSession.ArgHostRelay);
            Assert.IsNull(NetSession.ArgJoinCode);
            NetSession.ParseArgs(new string[0]);
        }

        [Test]
        public void ReleaseScenesAreOnlyTheGame()
        {
            var scenes = ReleaseBuild.Scenes();
            CollectionAssert.AreEqual(new[] { SceneNames.Path(SceneNames.Bootstrap), SceneNames.Path(SceneNames.MainMenu), SceneNames.Path(SceneNames.SudsyLot) }, scenes);
            foreach (var s in scenes) Assert.IsTrue(System.IO.File.Exists(s), s);
            Assert.IsTrue(System.IO.File.Exists(ReleaseBuild.ReadmeSource));
        }

        [Test]
        public void DevYardIsDevOnlyInTheLot()
        {
            var scene = EditorSceneManager.OpenScene(SceneNames.Path(SceneNames.SudsyLot), OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var yard = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "DevYard");
                Assert.IsNotNull(yard, "DevYard object");
                Assert.IsNotNull(yard.GetComponent<DevOnlyObject>(), "DevYard is removed in release");

                var list = roots.SelectMany(r => r.GetComponentsInChildren<NetworkSpawnList>(true)).Single();
                var arr = new SerializedObject(list).FindProperty("_entries");
                int dev = 0, devNearYard = 0, gameNearYard = 0;
                bool cagePoleIsGame = false;
                for (int i = 0; i < arr.arraySize; i++)
                {
                    var e = arr.GetArrayElementAtIndex(i);
                    bool isDev = e.FindPropertyRelative("devOnly").boolValue;
                    var pos = e.FindPropertyRelative("position").vector3Value;
                    bool nearYard = (pos - DevYard.Origin).sqrMagnitude < 20f * 20f && pos.x > 22f;
                    if (isDev) dev++;
                    if (nearYard && isDev) devNearYard++;
                    if (nearYard && !isDev) gameNearYard++;
                    var prefab = e.FindPropertyRelative("prefab").objectReferenceValue;
                    if (prefab != null && prefab.name.StartsWith("T06") && !isDev && (pos - LotBuilder.CagePos).sqrMagnitude < 9f) cagePoleIsGame = true;
                }
                Assert.AreEqual(15, dev, "10 dev-yard tools/cars/critters + 5 cones");
                Assert.AreEqual(dev, devNearYard, "every dev-only entry sits in the dev yard");
                Assert.AreEqual(0, gameNearYard, "nothing in the dev yard ships");
                Assert.IsTrue(cagePoleIsGame, "the Animal Control net pole stays in the game");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
