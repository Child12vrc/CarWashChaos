using System;

namespace CarWash.Core
{
    /// <summary>
    /// Pure game-flow state machine (TECH §2): MENU→LOBBY→DAY→SUMMARY→SHOP→(DAY/WEEK_END/GAMEOVER).
    /// The server drives it; the networked mirror lives in the SudsyLot scene (NetworkVariable).
    /// </summary>
    public class GameStateMachine
    {
        public GameState State { get; private set; } = GameState.Boot;
        public event Action<GameState, GameState> Changed;

        public static bool IsAllowed(GameState from, GameState to)
        {
            if (to == GameState.Menu) return true; // leave/abort always allowed
            switch (from)
            {
                case GameState.Boot: return false;
                case GameState.Menu: return to == GameState.Lobby;
                case GameState.Lobby: return to == GameState.Day;
                case GameState.Day: return to == GameState.Summary;
                case GameState.Summary: return to == GameState.Shop || to == GameState.GameOver || to == GameState.WeekEnd;
                case GameState.Shop: return to == GameState.Day;
                case GameState.WeekEnd: return to == GameState.Day || to == GameState.Lobby;
                case GameState.GameOver: return to == GameState.Day || to == GameState.Lobby;
                default: return false;
            }
        }

        /// <summary>Client mirror of the server's replicated state (late join may skip steps).</summary>
        public void Force(GameState to)
        {
            if (to == State) return;
            var from = State;
            State = to;
            Changed?.Invoke(from, to);
        }

        public bool TryChange(GameState to)
        {
            if (to == State || !IsAllowed(State, to)) return false;
            var from = State;
            State = to;
            Changed?.Invoke(from, to);
            return true;
        }
    }
}
