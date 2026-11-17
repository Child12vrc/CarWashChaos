using CarWash.Net;
using NUnit.Framework;
using UnityEngine;

namespace CarWash.Tests
{
    /// <summary>P1: the measuring tools used by the network-simulator checks + -netsim argument parsing.</summary>
    public class NetRobustnessTests
    {
        static MotionStats Line(int frames, float speed, float dt = 1f / 60f)
        {
            var m = new MotionStats();
            for (int i = 0; i < frames; i++) m.Add(new Vector3(i * speed * dt, 0f, 0f), dt);
            return m;
        }

        [Test] public void SmoothMotion_NoJumpsNoReversals()
        {
            var m = Line(120, 7f);
            Assert.AreEqual(0, m.Jumps(14f));
            Assert.AreEqual(0, m.Reversals());
            Assert.AreEqual(7f, m.MaxStepSpeed, 0.01f);
        }

        [Test] public void Teleport_CountsAsJump()
        {
            var m = Line(60, 5f);
            m.Add(new Vector3(20f, 0f, 0f), 1f / 60f);
            Assert.AreEqual(1, m.Jumps(14f));
        }

        [Test] public void SnapBack_CountsAsRubberBand()
        {
            var m = Line(60, 6f);
            var last = new Vector3(59 * 6f / 60f, 0f, 0f);
            m.Add(last - new Vector3(0.4f, 0f, 0f), 1f / 60f); // corrected backwards
            Assert.AreEqual(1, m.Reversals());
        }

        [Test] public void StandingStill_NoReversalsFromNoise()
        {
            var m = new MotionStats();
            for (int i = 0; i < 100; i++) m.Add(new Vector3((i % 2) * 0.005f, 0f, 0f), 1f / 60f);
            Assert.AreEqual(0, m.Reversals());
        }

        [Test] public void NetSimArg_Parsing()
        {
            CollectionAssert.AreEqual(new[] { 150, 30, 5 }, NetSession.ParseNetSim("150,30,5"));
            CollectionAssert.AreEqual(new[] { 50, 0, 0 }, NetSession.ParseNetSim("50"));
            CollectionAssert.AreEqual(new[] { 300, 30, 100 }, NetSession.ParseNetSim("300,30,250"));
            Assert.IsNull(NetSession.ParseNetSim("abc"));
            Assert.IsNull(NetSession.ParseNetSim("-5,0,0"));
            Assert.IsNull(NetSession.ParseNetSim(""));
        }

        [Test] public void AllNetworkPrefabs_UseLerpInterpolationAndUnreliableDeltas()
        {
            var list = CarWash.Editor.PrefabBuilder.LoadList();
            Assert.NotNull(list);
            int n = 0;
            foreach (var p in list.PrefabList)
                foreach (var nt in p.Prefab.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(true))
                {
                    n++;
                    Assert.IsTrue(nt.Interpolate, p.Prefab.name);
                    Assert.AreEqual(Unity.Netcode.Components.NetworkTransform.InterpolationTypes.SmoothDampening, nt.PositionInterpolationType, p.Prefab.name);
                    Assert.AreEqual(nt is CarWash.Net.OwnerNetworkTransform, nt.UseUnreliableDeltas, p.Prefab.name);
                }
            Assert.Greater(n, 20);
        }

        [Test] public void CritterHistory_RewindsToPastPosition()
        {
            var go = new GameObject("c");
            go.AddComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
            var c = go.AddComponent<CarWash.Critters.CritterBase>();
            Assert.AreEqual(go.transform.position, c.PositionAgo(0.3f)); // no history yet → current
            Object.DestroyImmediate(go);
        }

        [Test] public void SlimeSmearSpot_IsAlwaysOutsideTheCar()
        {
            var car = new Bounds(new Vector3(34f, 0.8f, 4f), new Vector3(2f, 1.6f, 4.5f)); // x 33..35, z 1.75..6.25
            var inside = CarWash.Critters.MudSlime.OutsideSpot(car, new Vector3(33.3f, 0f, 5.4f), 0.6f);
            Assert.AreEqual(32.4f, inside.x, 1e-4); // pushed out of the nearest (west) side
            var outside = CarWash.Critters.MudSlime.OutsideSpot(car, new Vector3(28f, 0f, 4f), 0.6f);
            Assert.AreEqual(32.4f, outside.x, 1e-4); Assert.AreEqual(4f, outside.z, 1e-4);
        }

        [Test] public void DisconnectReasons_AreKorean()
        {
            Assert.AreEqual("호스트가 게임을 종료했습니다.", NetSession.FriendlyReason("[Disconnect Event][Client-1][TransportClientId-4294967296][ClosedByRemote] Connection was closed by remote endpoint."));
            StringAssert.Contains("시간 초과", NetSession.FriendlyReason("[Disconnect Event][Client-2][Timeout] Connection timed out"));
            Assert.AreEqual("서버가 가득 찼습니다 (최대 4명)", NetSession.FriendlyReason("서버가 가득 찼습니다 (최대 4명)"));
            Assert.AreEqual("호스트와의 연결이 끊어졌습니다.", NetSession.FriendlyReason(null));
            StringAssert.Contains("접속할 수 없습니다", NetSession.FriendlyReason("MaxConnectionAttempts reached"));
        }

        [Test] public void Timeouts_AreShorterThanUtpDefault()
        {
            Assert.LessOrEqual(NetSession.DisconnectTimeoutMs, 10000);
            Assert.Less(NetSession.HeartbeatMs, NetSession.DisconnectTimeoutMs / 5);
        }
    }
}
