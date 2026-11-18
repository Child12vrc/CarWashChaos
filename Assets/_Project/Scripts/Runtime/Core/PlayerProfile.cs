using System.Globalization;
using System.Text;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// M4.9 G4: the local player's nickname (PlayerPrefs "cw.nickname"). 2–12 characters (Korean / Latin letters, digits,
    /// space, _ - .), no markup. Required before hosting / joining from the main menu. Command-line auto starts (tests) use
    /// <c>-nick NAME</c> or a temporary "손님NN" that is not saved.
    /// </summary>
    public static class PlayerProfile
    {
        public const int MinLen = 2, MaxLen = 12, MaxBytes = 61; // FixedString64Bytes payload
        const string Key = "cw.nickname";
        static string _session;

        public static bool HasNickname => !string.IsNullOrEmpty(Nickname);

        public static string Nickname
        {
            get
            {
                if (_session != null) return _session;
                var a = System.Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < a.Length; i++) if (a[i] == "-nick" && Validate(a[i + 1]) == null) return _session = Clean(a[i + 1]);
                return PlayerPrefs.GetString(Key, "");
            }
            set
            {
                var c = Clean(value);
                if (Validate(c) != null) return;
                _session = null;
                PlayerPrefs.SetString(Key, c);
                PlayerPrefs.Save();
            }
        }

        /// <summary>A not-saved name for command-line auto starts when nothing is stored yet.</summary>
        public static void EnsureTemporary()
        {
            if (HasNickname) return;
            _session = "손님" + (System.Diagnostics.Process.GetCurrentProcess().Id % 100).ToString("00");
        }

        public static string Clean(string s) => string.IsNullOrEmpty(s) ? "" : s.Trim().Replace('\n', ' ').Replace('\r', ' ');

        public static int Length(string s) => string.IsNullOrEmpty(s) ? 0 : new StringInfo(s).LengthInTextElements;

        /// <summary>null when OK, else a Korean message for the menu.</summary>
        public static string Validate(string raw)
        {
            var s = Clean(raw);
            int n = Length(s);
            if (n < MinLen) return $"닉네임은 {MinLen}자 이상이어야 해요";
            if (n > MaxLen) return $"닉네임은 {MaxLen}자까지예요";
            if (Encoding.UTF8.GetByteCount(s) > MaxBytes) return "닉네임이 너무 길어요";
            foreach (char ch in s)
            {
                bool ok = char.IsLetterOrDigit(ch) || ch == ' ' || ch == '_' || ch == '-' || ch == '.'
                          || (ch >= 0x3131 && ch <= 0x318E)   // 한글 자모 (composing)
                          || (ch >= 0xAC00 && ch <= 0xD7A3);  // 한글 음절
                if (!ok) return $"'{ch}' 문자는 쓸 수 없어요 (한글·영문·숫자·공백·_-.)";
            }
            return null;
        }
    }
}
