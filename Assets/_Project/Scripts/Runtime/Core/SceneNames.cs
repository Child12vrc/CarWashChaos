namespace CarWash.Core
{
    public static class SceneNames
    {
        public const string Bootstrap = "Bootstrap";
        public const string MainMenu = "MainMenu";
        public const string Lobby = "Lobby";
        public const string SudsyLot = "SudsyLot";
        public const string DirtTest = "DirtTest";
        public const string Folder = "Assets/_Project/Scenes/";

        public static string Path(string name) => Folder + name + ".unity";
    }
}
