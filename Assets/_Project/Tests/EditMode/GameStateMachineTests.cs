using CarWash.Core;
using CarWash.Net;
using NUnit.Framework;

namespace CarWash.Tests
{
    public class GameStateMachineTests
    {
        [Test]
        public void FullWeekFlow_IsAllowed()
        {
            var m = new GameStateMachine();
            Assert.IsTrue(m.TryChange(GameState.Menu));
            Assert.IsTrue(m.TryChange(GameState.Lobby));
            Assert.IsTrue(m.TryChange(GameState.Day));
            Assert.IsTrue(m.TryChange(GameState.Summary));
            Assert.IsTrue(m.TryChange(GameState.Shop));
            Assert.IsTrue(m.TryChange(GameState.Day));
            Assert.IsTrue(m.TryChange(GameState.Summary));
            Assert.IsTrue(m.TryChange(GameState.WeekEnd));
        }

        [Test]
        public void IllegalTransitions_AreRejected()
        {
            var m = new GameStateMachine();
            Assert.IsFalse(m.TryChange(GameState.Day));
            m.TryChange(GameState.Menu);
            Assert.IsFalse(m.TryChange(GameState.Shop));
            Assert.AreEqual(GameState.Menu, m.State);
        }

        [Test]
        public void Bankrupt_GoesToGameOver_ThenRetryDay()
        {
            var m = new GameStateMachine();
            m.TryChange(GameState.Menu); m.TryChange(GameState.Lobby); m.TryChange(GameState.Day); m.TryChange(GameState.Summary);
            Assert.IsTrue(m.TryChange(GameState.GameOver));
            Assert.IsTrue(m.TryChange(GameState.Day));
        }

        [Test]
        public void ChangedEvent_Fires()
        {
            var m = new GameStateMachine();
            GameState seen = GameState.Boot;
            m.Changed += (_, to) => seen = to;
            m.TryChange(GameState.Menu);
            Assert.AreEqual(GameState.Menu, seen);
        }

        [Test]
        public void CommandLine_ParsesHostAndJoin()
        {
            NetSession.ParseArgs(new[] { "game.exe", "-join", "10.0.0.5" });
            Assert.IsFalse(NetSession.ArgHost);
            Assert.AreEqual("10.0.0.5", NetSession.ArgJoin);
            NetSession.ParseArgs(new[] { "game.exe", "-host" });
            Assert.IsTrue(NetSession.ArgHost);
            Assert.IsNull(NetSession.ArgJoin);
        }
    }
}
