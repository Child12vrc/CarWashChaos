namespace CarWash.Core
{
    /// <summary>Layer ids / masks (TECH §5). Set in project by SceneBuilder.ConfigureProject.</summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int Player = 6;
        public const int Vehicle = 7;
        public const int Prop = 8;
        public const int Critter = 9;
        public const int Tool = 10;
        public const int Interactable = 11;
        public const int FoamArea = 12;
        public const int Trigger = 13;
        public const int FPArms = 14;
        /// <summary>P3 ragdoll bones: collide with the world, props, cars, critters and other ragdolls — not with players' capsules, tools or triggers.</summary>
        public const int Ragdoll = 15;
        /// <summary>GLB vehicles: exact body mesh used only by washing raycasts (spray, towel). Collides with nothing — physics uses the simple VehicleHull boxes on Vehicle.</summary>
        public const int WashSurface = 16;
        /// <summary>Invisible play-area walls (+ ceiling): stop players, props, tools, critters and ragdolls — vehicles drive through (road in/out).</summary>
        public const int PlayBounds = 17;
        /// <summary>Decoration outside the play area (city blocks, trees, lamps, parked cars): render only, no colliders.</summary>
        public const int Scenery = 18;

        public static readonly string[] Names =
        {
            null, null, null, null, null, null,
            "Player", "Vehicle", "Prop", "Critter", "Tool", "Interactable", "FoamArea", "Trigger", "FPArms", "Ragdoll", "WashSurface", "PlayBounds", "Scenery"
        };

        public const int SprayMask = (1 << Default) | (1 << Player) | (1 << Vehicle) | (1 << Prop) | (1 << Critter) | (1 << Tool) | (1 << WashSurface);
        /// <summary>Towel wipe targets: graybox car bodies (Vehicle) and GLB wash surfaces.</summary>
        public const int WashMask = (1 << Vehicle) | (1 << WashSurface);
        public const int InteractMask = (1 << Interactable) | (1 << Vehicle) | (1 << Tool) | (1 << Prop) | (1 << Critter);
        public const int GroundMask = (1 << Default) | (1 << Vehicle) | (1 << Prop);

        public const string TagTrash = "Trash";
        public const string TagVacSmall = "VacSmall";
        public const string TagDirtable = "Dirtable";
    }
}
