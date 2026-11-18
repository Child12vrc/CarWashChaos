using CarWash.Dirt;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CarWash.Tests
{
    /// <summary>M4.7 N2: compact network encodings.</summary>
    public class M47NetTests
    {
        [Test]
        public void DirtStroke_IsEighteenBytes_AndDecodesToItsQuantizedSelf()
        {
            var s = new DirtStroke(new Vector3(1.234f, 0.876f, -2.345f), 0.41f, new Vector4(1.5f, 0.75f, 0f, 1.5f), StrokeType.Clean) { source = 3 };
            using var w = new FastBufferWriter(64, Allocator.Temp);
            w.WriteNetworkSerializable(s);
            Assert.AreEqual(18, w.Length);
            using var r = new FastBufferReader(w, Allocator.Temp);
            r.ReadNetworkSerializable(out DirtStroke d);
            var q = s.Quantized();
            Assert.AreEqual(q.pos, d.pos);
            Assert.AreEqual(q.radius, d.radius);
            Assert.AreEqual(q.strength, d.strength);
            Assert.AreEqual(StrokeType.Clean, d.type);
            Assert.AreEqual(3, d.source);
            Assert.Less(Vector3.Distance(s.pos, d.pos), 0.005f, "~mm precision on a car");
            Assert.Less(Mathf.Abs(s.radius - d.radius), 0.001f);
        }

        [Test]
        public void SprayBurst_NewerWrapsAround()
        {
            Assert.IsTrue(Player.PlayerTools.NewerBurst(5, 4));
            Assert.IsFalse(Player.PlayerTools.NewerBurst(4, 4));
            Assert.IsFalse(Player.PlayerTools.NewerBurst(3, 4));
            Assert.IsTrue(Player.PlayerTools.NewerBurst(1, 65535), "wraps");
        }

        [Test]
        public void RelayRetry_OnlyTransientErrors()
        {
            Assert.IsTrue(Net.RelayConnector.IsTransient(new System.Exception("HTTP/1.1 504 Gateway Timeout (504)")));
            Assert.IsTrue(Net.RelayConnector.IsTransient(new System.Exception("Request timed out")));
            Assert.IsFalse(Net.RelayConnector.IsTransient(new System.Exception("HTTP/1.1 404 Not Found (404)")));
        }
    }
}
