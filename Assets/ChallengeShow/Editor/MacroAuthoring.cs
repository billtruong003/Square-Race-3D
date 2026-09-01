using UnityEditor;
using UnityEngine;

namespace ChallengeShow.EditorTools
{
    /// <summary>
    /// Builds the authoring form of every environment macro out of individual Kenney pieces.
    ///
    /// This is the descendant of the old DungeonEnvironment builder, but pointed somewhere else. It
    /// used to place ~610 pieces straight into the production scene; now it places them into a
    /// throwaway hierarchy that is saved as an authoring prefab and immediately baked down to one
    /// renderer per material. Nothing here ever runs against the live arena.
    ///
    /// Two scales are still in play and confusing them is still the fastest way to wreck the scene:
    ///
    ///   <see cref="Arch"/>    (3.0) - floors, walls, columns, gates. A floor tile is 3 m, so the
    ///                         validated 9 m lane is exactly 3 tiles.
    ///   <see cref="Clutter"/> (1.2) - rocks and stones. At architecture scale these become 1.7 m
    ///                         boulders that hide the smaller monsters; at 1.2 they read as 0.6 m
    ///                         rubble, which is what an edge wants.
    ///
    /// Every macro is authored with +Z running down-lane and its origin at the section's centre.
    /// </summary>
    public static class MacroAuthoring
    {
        public const float Arch = 3.0f;
        public const float Clutter = 1.2f;
        public const float Grid = Arch;
        public const float LaneWidth = 9f;
        public const float HalfDeck = LaneWidth * 0.5f;

        /// <summary>
        /// How far out solid architecture has to stand to stay clear of every gameplay camera state.
        ///
        /// The rig watches the runner side-on and ALTERNATES sides, so any solid mass between x = 0
        /// and this line ends up between lens and runner on half the attempts. Court walls, the arm
        /// tower and the finish gate towers were each discovered the hard way.
        ///
        /// The number has to clear the WIDEST state, not the usual one. Follow shots sit at
        /// 11 m * sin(78 deg) = 10.8 m lateral, but ragdoll shots pull out to 13 m * sin(78 deg) =
        /// 12.7 m - so an earlier value of 12.5 put the finish colonnade INSIDE the ragdoll camera,
        /// which then framed a pier from 1.7 m away instead of the runner. 16.5 leaves roughly 3.8 m
        /// of margin past the widest shot, plus room for the pier's own footprint.
        /// </summary>
        public const float CameraLateral = 16.5f;

        /// <summary>
        /// Bastion architecture is shorter than lane architecture.
        ///
        /// Walls are scaled non-uniformly - full width so they still tile on the 3 m grid, but 0.67
        /// height, giving 2.2 m instead of 3.3 m. A typical contestant is about 1.4 m tall, and a
        /// 3.3 m wall directly behind it turned the roster into tiny NPCs standing against a castle.
        /// The kit is flat-shaded with no tiling texture, so the non-uniform scale shows no stretch.
        /// </summary>
        private const float BastionWallY = 0.67f;

        // ------------------------------------------------------------------ shared pieces

        /// <summary>Flagstone deck for one lane section, laid on the 3 m grid.</summary>
        private static void Deck(Transform root, int tilesLong, System.Random rng, float detailChance = 0.22f)
        {
            var deck = Child(root, "Deck");
            int lanes = Mathf.RoundToInt(LaneWidth / Grid);
            float halfLen = tilesLong * Grid * 0.5f;

            for (int r = 0; r < tilesLong; r++)
            {
                float z = -halfLen + (r + 0.5f) * Grid;
                for (int c = 0; c < lanes; c++)
                {
                    float x = -HalfDeck + (c + 0.5f) * Grid;
                    bool detail = rng.NextDouble() < detailChance;
                    KenneyKit.Put(deck, detail ? KenneyKit.FloorDetail : KenneyKit.Floor,
                                  new Vector3(x, 0f, z), rng.Next(4) * 90f, Arch, KenneyKit.Palette.Lane);
                }
            }
        }

        /// <summary>
        /// Low rubble edging.
        ///
        /// Deliberately knee height. Full walls were tried along the lane first and they completely
        /// occlude the runner from the side-on gameplay camera, which is the shot the show is built
        /// around.
        /// </summary>
        private static void Parapet(Transform root, int tilesLong, System.Random rng, float density = 1f)
        {
            var edge = Child(root, "Parapet");
            float halfLen = tilesLong * Grid * 0.5f;
            int steps = Mathf.CeilToInt(tilesLong * 2 * density);

            for (int i = 0; i < steps; i++)
            {
                float z = Mathf.Lerp(-halfLen, halfLen, (i + 0.5f) / steps);
                for (int s = -1; s <= 1; s += 2)
                {
                    if (rng.NextDouble() < 0.12) continue;      // gaps stop it reading as a kerb
                    string piece = rng.NextDouble() < 0.4 ? KenneyKit.Rocks : KenneyKit.Stones;
                    float k = Clutter * (0.75f + (float)rng.NextDouble() * 0.7f);
                    KenneyKit.Put(edge, piece,
                        new Vector3(s * (HalfDeck + (float)rng.NextDouble() * 0.35f), -0.06f, z),
                        new Vector3((float)(rng.NextDouble() * 10.0 - 5.0),
                                    (float)(rng.NextDouble() * 360.0),
                                    (float)(rng.NextDouble() * 10.0 - 5.0)),
                        Vector3.one * k, KenneyKit.Palette.Lane);
                }
            }
        }

        /// <summary>
        /// Broken masonry hanging under a section.
        ///
        /// Chunks are placed by their TOP, not their pivot: the kit's pivots sit at the base centre,
        /// so setting y directly makes understructure grow upward through the deck.
        /// </summary>
        private static void Understructure(Transform root, int tilesLong, System.Random rng, float depth = 1f)
        {
            var under = Child(root, "Understructure");
            float halfLen = tilesLong * Grid * 0.5f;
            int count = tilesLong * 2;

            for (int i = 0; i < count; i++)
            {
                float z = Mathf.Lerp(-halfLen, halfLen, (float)rng.NextDouble());
                float x = (float)(rng.NextDouble() * 2.0 - 1.0) * (HalfDeck + 1.2f);

                // Mass concentrates on the centreline so the bridge reads as a keel, not a slab with
                // lumps glued underneath.
                float bias = 1f - Mathf.Abs(x) / (HalfDeck + 1.2f);
                float top = -0.2f - (float)rng.NextDouble() * (1.0f + bias * 3.0f) * depth;
                float k = Arch * (0.55f + (float)rng.NextDouble() * bias * 1.5f);

                KenneyKit.PutHanging(under, i % 3 == 0 ? KenneyKit.Dirt : KenneyKit.Rocks,
                                     x, top, z, (float)(rng.NextDouble() * 360.0), k);
            }
        }

        private static void Socket(Transform root, string name, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void LaneSockets(Transform root, int tilesLong)
        {
            float half = tilesLong * Grid * 0.5f;
            Socket(root, KenneyMacroBaker.SocketEntrance, new Vector3(0f, 0f, -half));
            Socket(root, KenneyMacroBaker.SocketExit, new Vector3(0f, 0f, half));
        }

        // ------------------------------------------------------------------ lane macros

        /// <summary>Plain span. Mostly floor, almost no architecture - this is the quiet beat.</summary>
        public static void BuildStraight(Transform root, int tilesLong, int seed)
        {
            var rng = new System.Random(seed);
            Deck(root, tilesLong, rng);
            Parapet(root, tilesLong, rng);
            Understructure(root, tilesLong, rng);

            // One leaning brace per long section, and nothing else. Yaw stays near 0 so the flat
            // wood-support panel presents its thin edge to the side camera rather than its face.
            if (tilesLong >= 4)
            {
                int s = rng.NextDouble() < 0.5 ? -1 : 1;
                KenneyKit.Put(root, KenneyKit.WoodSupport,
                    new Vector3(s * (HalfDeck + 0.5f), -0.1f, (float)(rng.NextDouble() * 4.0 - 2.0)),
                    new Vector3(0f, (float)(rng.NextDouble() * 16.0 - 8.0), s * 7f),
                    Vector3.one * Arch * 0.8f);
            }

            LaneSockets(root, tilesLong);
        }

        /// <summary>Broad, clean court where the runner starts. Minimal decoration by design.</summary>
        public static void BuildSpawnCourt(Transform root, int tilesLong, int seed)
        {
            var rng = new System.Random(seed);
            Deck(root, tilesLong, rng, 0.45f);       // more worn stone: this is trafficked ground
            Parapet(root, tilesLong, rng, 0.8f);
            Understructure(root, tilesLong, rng);

            // No colonnade. Court verticals were tried twice and failed both ways: at the deck edge
            // they stood between the side camera and the runner, and pushed past CameraLateral to
            // fix that they detached from the lane entirely and read as towers floating in open sky
            // 16 m from anything. The court gets its identity from worn flagstones, props and the
            // crystal wall behind it, which costs nothing and blocks no shot.
            // Props sit on the deck's own edge, among the parapet rubble. They used to be placed
            // relative to CameraLateral so they could stand on the colonnade's piers; once those
            // went they were left hanging in open sky 15 m from any floor.
            for (int s = -1; s <= 1; s += 2)
                KenneyKit.Put(root, s < 0 ? KenneyKit.Barrel : KenneyKit.Pot,
                              new Vector3(s * (HalfDeck - 0.5f), 0f, -Grid * 0.6f),
                              (float)(rng.NextDouble() * 360.0), Arch * 0.7f);

            LaneSockets(root, tilesLong);
        }

        /// <summary>
        /// The crystal wall's masonry surround and the lane's rear end.
        ///
        /// The wall itself is a gameplay object with authored collision and stays in the scene; this
        /// macro is the ruin it bursts out of. Height is capped at two courses: the establishing
        /// camera looks down the lane from behind the spawn, and at three courses this stood about
        /// 10 m and curtained off the entire bridge behind it.
        /// </summary>
        public static void BuildImpactZone(Transform root, int tilesLong, int seed,
                                           float wallLocalZ, float crystalLocalZ)
        {
            var rng = new System.Random(seed);
            Deck(root, tilesLong, rng, 0.5f);
            Parapet(root, tilesLong, rng, 1.2f);
            Understructure(root, tilesLong, rng, 1.3f);

            var frame = Child(root, "WallFraming");
            float wallH = KenneyKit.HeightOf(KenneyKit.Wall) * Arch;

            // Three bays wide, matching the 9 m lane. At five it overhung the bridge and read as a
            // barrier walling the arena off rather than the back of a court.
            for (int course = 0; course < 2; course++)
                for (int i = -1; i <= 1; i++)
                {
                    if (course == 1 && i == 0) continue;      // centre broken open, so it reads as ruin
                    KenneyKit.Put(frame, (i + course) % 2 == 0 ? KenneyKit.Wall : KenneyKit.WallHalf,
                                  new Vector3(i * Arch, course * wallH, wallLocalZ), 0f, Arch);
                }

            for (int s = -1; s <= 1; s += 2)
                KenneyKit.Put(frame, KenneyKit.Column,
                              new Vector3(s * (HalfDeck + 0.8f), 0f, wallLocalZ + 0.3f), 0f, Arch);

            // Rubble heaped where the crystals burst through the masonry.
            for (int i = 0; i < 16; i++)
                KenneyKit.Put(frame, i % 2 == 0 ? KenneyKit.Rocks : KenneyKit.Stones,
                    new Vector3((float)(rng.NextDouble() * 2.0 - 1.0) * HalfDeck * 1.15f, -0.05f,
                                wallLocalZ + 1.3f + (float)rng.NextDouble() * 0.9f),
                    new Vector3(0f, (float)(rng.NextDouble() * 360.0), 0f),
                    Vector3.one * Clutter * (1.0f + (float)rng.NextDouble() * 1.1f),
                    KenneyKit.Palette.Lane);

            // The crystals themselves. They used to be placed with Random.Range straight into the
            // scene, which meant the hero of the impact shot was different after every rebuild and
            // could not be art-directed; a seeded generator makes the wall reproducible.
            var crystals = Child(root, "Crystals");
            var spike = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Spike/model_0.prefab");
            if (spike != null)
                for (int i = 0; i < 14; i++)
                {
                    var c = (GameObject)PrefabUtility.InstantiatePrefab(spike, crystals);
                    c.transform.localPosition = new Vector3(
                        (float)(rng.NextDouble() * 2.0 - 1.0) * HalfDeck,
                        0.5f + (float)rng.NextDouble() * 3.1f,
                        crystalLocalZ + (float)rng.NextDouble() * 0.5f);
                    c.transform.localRotation = Quaternion.Euler(
                        (float)(rng.NextDouble() * 70.0 - 35.0),
                        (float)(rng.NextDouble() * 360.0),
                        (float)(rng.NextDouble() * 50.0 - 25.0));
                    c.transform.localScale = Vector3.one * (9f + (float)rng.NextDouble() * 9f);

                    // The Spike prefab ships colliders. The wall's collision is one authored box, so
                    // a ragdoll has a predictable surface to bounce off rather than 20 shards to snag on.
                    foreach (var col in c.GetComponentsInChildren<Collider>(true))
                        Object.DestroyImmediate(col);
                }

            LaneSockets(root, tilesLong);
        }

        /// <summary>
        /// The obstacle section: deck, plus the gantry the arm hangs from.
        ///
        /// The arm sweeps a 5.2 m radius in the plane x = 0, so it can only be supported from the
        /// side - and the side is where the lens is. Four geometries failed before this one: a
        /// stacked tower at the deck edge blacked out the strike; a short corbel was smaller but
        /// still on the sightline; a horizontal run from an outboard pier passed straight THROUGH
        /// the camera position and fanned across the whole frame. The answer is to go over the top.
        /// The beam runs at 10.8 m, clear of both the camera (about 4 m) and the arm's highest reach
        /// (4.6 + 5.2 = 9.8 m), so it frames the obstacle instead of hiding it.
        /// </summary>
        public static void BuildArmZone(Transform root, int tilesLong, int seed,
                                        float obstacleLocalZ, float pivotHeight)
        {
            var rng = new System.Random(seed);
            Deck(root, tilesLong, rng, 0.6f);
            Parapet(root, tilesLong, rng, 1.1f);
            Understructure(root, tilesLong, rng, 1.2f);

            var mount = Child(root, "Gantry");
            mount.localPosition = new Vector3(0f, pivotHeight, obstacleLocalZ);

            float pierX = -(CameraLateral + 1.5f);
            float wallH = KenneyKit.HeightOf(KenneyKit.Wall) * Arch;
            float baseY = -pivotHeight;                  // local y is relative to the pivot
            const float BeamLocalY = 6.2f;
            const float HangerX = -1.7f;

            int courses = Mathf.CeilToInt((BeamLocalY - baseY) / wallH);
            for (int i = 0; i < courses; i++)
                KenneyKit.Put(mount, i == courses - 1 ? KenneyKit.WallNarrow : KenneyKit.Wall,
                              new Vector3(pierX, baseY + i * wallH, 0f), 90f, Arch);

            KenneyKit.Put(mount, KenneyKit.Floor, new Vector3(pierX, baseY, 0f), 0f, Arch);
            for (int i = 0; i < 9; i++)
                KenneyKit.PutHanging(mount, i % 3 == 0 ? KenneyKit.Dirt : KenneyKit.Rocks,
                    pierX + (float)(rng.NextDouble() * 2.0 - 1.0) * Grid * 0.5f,
                    baseY - 0.15f - (float)rng.NextDouble() * 2.4f,
                    (float)(rng.NextDouble() * 2.0 - 1.0) * Grid * 0.6f,
                    (float)(rng.NextDouble() * 360.0),
                    Arch * (0.6f + (float)rng.NextDouble() * 1.0f));

            // Overhead beam. Yaw 0 keeps wood-support's 0.24 depth facing the side camera; yawed 90
            // its broad face turns to the lens and the open timber becomes a row of solid boards.
            int spans = Mathf.CeilToInt((Mathf.Abs(pierX) - Mathf.Abs(HangerX)) / (Arch * 0.95f));
            for (int i = 0; i < spans; i++)
            {
                float x = Mathf.Lerp(pierX, HangerX, (i + 0.5f) / spans);
                KenneyKit.Put(mount, KenneyKit.WoodSupport, new Vector3(x, BeamLocalY, 0f), 0f, Arch * 0.95f);
            }
            KenneyKit.Put(mount, KenneyKit.WoodStructure,
                          new Vector3(pierX + Arch * 0.9f, BeamLocalY - Arch * 0.15f, 0f), 0f, Arch);

            // Diagonal stays bracing the axle back to the pier. A vertical hanger read as a pillar
            // floating in the sky, because the beam it hung from is above frame at gameplay FOV; a
            // diagonal keeps its lower length in shot and points the eye toward the pier.
            var from = new Vector2(HangerX, -0.3f);
            var to = new Vector2(pierX * 0.62f, BeamLocalY * 0.82f);
            float ang = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            for (int i = 0; i < 3; i++)
            {
                Vector2 at = Vector2.Lerp(from, to, (i + 0.5f) / 3f);
                // Offset upstream in Z so the brace sits alongside the strike rather than across it.
                // Sitting at z = 0 it lined up with the runner at the exact moment of the hit for a
                // left-side camera, which is the one frame that must never be obstructed.
                KenneyKit.Put(mount, KenneyKit.WoodSupport, new Vector3(at.x, at.y, -Arch * 0.75f),
                              new Vector3(0f, 0f, ang), Vector3.one * Arch * 0.85f);
            }

            // Axle housing, so the arm reads as turning on something.
            KenneyKit.Put(mount, KenneyKit.Column, new Vector3(HangerX * 0.5f, -0.85f, 0f),
                          new Vector3(0f, 0f, 90f), Vector3.one * Arch * 0.55f);

            LaneSockets(root, tilesLong);
            Socket(root, KenneyMacroBaker.SocketObstacle, new Vector3(0f, pivotHeight, obstacleLocalZ));
        }

        /// <summary>The arrival court. The gate itself is a separate landmark macro.</summary>
        public static void BuildFinishCourt(Transform root, int tilesLong, int seed)
        {
            var rng = new System.Random(seed);
            Deck(root, tilesLong, rng, 0.5f);
            Parapet(root, tilesLong, rng, 0.9f);
            Understructure(root, tilesLong, rng);
            // See BuildSpawnCourt: the finish court's verticals are the gate, not a colonnade.

            LaneSockets(root, tilesLong);
        }

        // ------------------------------------------------------------------ landmarks

        /// <summary>
        /// The finish gate: mass outboard, opening slim.
        ///
        /// Built first as two solid towers just off the deck edge, which put 3.6 m of masonry inside
        /// the camera's 10.8 m lateral offset - so the runner crossing the finish line, the payoff
        /// shot of the whole show, was hidden behind a wall. The bulk that makes this a landmark now
        /// stands beyond CameraLateral where the lens passes inside it, and all that crosses the
        /// lane is a pair of slim columns under an overhead lintel.
        /// </summary>
        public static void BuildFinishGate(Transform root, int seed)
        {
            var rng = new System.Random(seed);
            const float GateScale = Arch * 1.2f;
            float wallH = KenneyKit.HeightOf(KenneyKit.Wall) * GateScale;
            float bastionX = CameraLateral + 2.2f;
            float uprightX = HalfDeck + 1.2f;
            const float LintelY = 2.15f;

            for (int s = -1; s <= 1; s += 2)
            {
                // Two courses over a two-tile footing. At three courses on a single tile these were
                // 12 m tall and one tile wide, and standing 18.7 m out with nothing around them they
                // read as free-floating pillars rather than the ruined ends of a gatehouse.
                for (int i = 0; i < 2; i++)
                    for (int t = -1; t <= 0; t++)
                        KenneyKit.Put(root, (i + t) % 2 == 0 ? KenneyKit.Wall : KenneyKit.WallHalf,
                                      new Vector3(s * bastionX + t * GateScale * 0.9f, i * wallH, 0f),
                                      s < 0 ? 90f : -90f, GateScale);

                KenneyKit.Put(root, KenneyKit.Column, new Vector3(s * bastionX, 2f * wallH, 0f), 0f, GateScale);
                for (int t = -1; t <= 1; t++)
                    KenneyKit.Put(root, KenneyKit.Floor,
                                  new Vector3(s * bastionX + t * GateScale * 0.9f, 0f, 0f), 0f, GateScale);

                for (int i = 0; i < 5; i++)
                    KenneyKit.PutHanging(root, i % 2 == 0 ? KenneyKit.Dirt : KenneyKit.Rocks,
                        s * bastionX + (float)(rng.NextDouble() * 2.0 - 1.0) * Grid * 0.5f,
                        -0.15f - (float)rng.NextDouble() * 1.8f,
                        (float)(rng.NextDouble() * 2.0 - 1.0) * Grid * 0.6f,
                        (float)(rng.NextDouble() * 360.0),
                        Arch * (0.5f + (float)rng.NextDouble() * 0.9f));

                // Slender posts, not columns.
                //
                // At a uniform GateScale these were 1.8 m wide and sat 5.7 m out - inside the camera
                // keepout by definition, since the gate has to straddle the lane - and from a side
                // camera they filled a sixth of the frame at exactly the moment a runner crosses the
                // finish line. Squeezing them to 45% on X and Z keeps the full height that defines
                // the opening while cutting the obstruction to 0.8 m.
                for (int i = 0; i < 2; i++)
                    KenneyKit.Put(root, KenneyKit.Column,
                                  new Vector3(s * uprightX, i * KenneyKit.HeightOf(KenneyKit.Column) * GateScale, 0f),
                                  Vector3.zero,
                                  new Vector3(GateScale * 0.45f, GateScale, GateScale * 0.45f));

                for (int t = 0; t < 2; t++)
                    KenneyKit.Put(root, KenneyKit.WoodSupport,
                                  new Vector3(Mathf.Lerp(s * uprightX, s * bastionX, (t + 0.5f) / 2f),
                                              wallH * LintelY - 0.4f, 0f),
                                  0f, GateScale * 1.5f);
                KenneyKit.Put(root, KenneyKit.Banner,
                              new Vector3(s * (uprightX - 0.5f), wallH * LintelY - 1.4f, -0.05f),
                              180f, GateScale * 0.85f);
            }

            for (int i = -1; i <= 1; i++)
                KenneyKit.Put(root, KenneyKit.WallOpening,
                              new Vector3(i * GateScale * 0.98f, wallH * LintelY, 0f), 0f, GateScale);

            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "FinishAccent";
            bar.transform.SetParent(root, false);
            bar.transform.localPosition = new Vector3(0f, wallH * LintelY + GateScale * 0.95f, 0f);
            bar.transform.localScale = new Vector3(uprightX * 2f + GateScale, 0.45f, 0.6f);
            bar.GetComponent<MeshRenderer>().sharedMaterial = EnvironmentMaterials.Accent();
            Object.DestroyImmediate(bar.GetComponent<Collider>());

            // A threshold band painted across the deck.
            //
            // This is what actually makes the finish line readable. Every vertical marker is either
            // an occluder from one camera or out of frame from another, but a stripe on the ground
            // is visible from the side, from overhead and from the establishing shot, and it can
            // never come between the lens and the runner. Raised a hair to clear the deck's own
            // surface rather than z-fighting it.
            var threshold = GameObject.CreatePrimitive(PrimitiveType.Cube);
            threshold.name = "FinishThreshold";
            threshold.transform.SetParent(root, false);
            threshold.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            threshold.transform.localScale = new Vector3(LaneWidth, 0.06f, 0.7f);
            threshold.GetComponent<MeshRenderer>().sharedMaterial = EnvironmentMaterials.Accent();
            Object.DestroyImmediate(threshold.GetComponent<Collider>());
        }

        // ------------------------------------------------------------------ bastions

        /// <summary>How a family's stage differs from its neighbours.</summary>
        public readonly struct BastionSpec
        {
            public readonly int Cols;          // deck width in 3 m tiles
            public readonly int Rows;          // deck depth in 3 m tiles
            public readonly float WallFill;    // 0 = open colonnade, 1 = solid back wall
            public readonly bool RaisedCentre; // a keep rather than a stage
            public readonly float Rubble;

            public BastionSpec(int cols, int rows, float wallFill, bool raisedCentre, float rubble)
            {
                Cols = cols; Rows = rows; WallFill = wallFill; RaisedCentre = raisedCentre; Rubble = rubble;
            }
        }

        /// <summary>
        /// A family's spectator bastion.
        ///
        /// Sized to hold exactly three contestants, a banner and the minimum architecture that reads
        /// - nothing more. The earlier version built 3-row mini-castles with 3.3 m back walls and
        /// they dominated the monsters standing on them. Depth is down to two rows, walls to 2.2 m,
        /// and the front is left completely open toward the lane.
        /// </summary>
        public static void BuildBastion(Transform root, BastionSpec spec, Color accent, int seed)
        {
            var rng = new System.Random(seed);
            float wallH = KenneyKit.HeightOf(KenneyKit.Wall) * Arch * BastionWallY;
            float backZ = (spec.Rows - 1) * 0.5f * Grid + Grid * 0.5f;

            var deck = Child(root, "Deck");
            for (int c = 0; c < spec.Cols; c++)
                for (int r = 0; r < spec.Rows; r++)
                {
                    float x = (c - (spec.Cols - 1) * 0.5f) * Grid;
                    float z = (r - (spec.Rows - 1) * 0.5f) * Grid;
                    KenneyKit.Put(deck, rng.NextDouble() < 0.3 ? KenneyKit.FloorDetail : KenneyKit.Floor,
                                  new Vector3(x, 0f, z), rng.Next(4) * 90f, Arch);
                }

            var walls = Child(root, "Walls");
            var wallScale = new Vector3(Arch, Arch * BastionWallY, Arch);

            for (int c = 0; c < spec.Cols; c++)
            {
                float x = (c - (spec.Cols - 1) * 0.5f) * Grid;
                bool centre = c == spec.Cols / 2;

                // WallFill decides how much of the back is solid. A low value leaves gaps that read
                // as an open colonnade; a high value gives a continuous wall to frame against.
                bool solid = centre || rng.NextDouble() < spec.WallFill;
                if (solid)
                    KenneyKit.Put(walls, centre ? KenneyKit.WallOpening : KenneyKit.Wall,
                                  new Vector3(x, 0f, backZ), new Vector3(0f, 180f, 0f), wallScale);
                else
                    KenneyKit.Put(walls, KenneyKit.Column, new Vector3(x, 0f, backZ),
                                  new Vector3(0f, 0f, 0f), wallScale);
            }

            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * (spec.Cols * 0.5f * Grid);
                KenneyKit.Put(walls, KenneyKit.Column, new Vector3(x, 0f, backZ), new Vector3(0f, 0f, 0f), wallScale);
                KenneyKit.Put(walls, KenneyKit.Column, new Vector3(x, 0f, -backZ + Grid * 0.4f),
                              new Vector3(0f, 0f, 0f), wallScale);
            }

            if (spec.RaisedCentre)
            {
                // The keep silhouette: narrower and taller, so it reads as an endpoint rather than a
                // second arena competing with the lane.
                for (int i = 0; i < 2; i++)
                    KenneyKit.Put(walls, i == 1 ? KenneyKit.WallNarrow : KenneyKit.Wall,
                                  new Vector3(0f, wallH + i * wallH, backZ), new Vector3(0f, 180f, 0f), wallScale);
                KenneyKit.Put(walls, KenneyKit.Column, new Vector3(0f, wallH * 3f, backZ), 0f, Arch * 0.9f);
            }

            KenneyKit.Put(walls, KenneyKit.Banner,
                          new Vector3(0f, wallH * 0.42f, backZ - 0.5f * Arch + 0.06f), 180f, Arch * 0.85f);

            // Family-coloured band capping the back wall. Hung above it with sky behind it read as a
            // loose plank; sitting on the wall's top edge it reads as heraldry.
            var standard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            standard.name = "FamilyStandard";
            standard.transform.SetParent(walls, false);
            standard.transform.localPosition = new Vector3(0f, wallH - 0.26f, backZ - 0.5f * Arch - 0.04f);
            standard.transform.localScale = new Vector3(spec.Cols * Grid * 0.88f, 0.5f, 0.2f);
            standard.GetComponent<MeshRenderer>().sharedMaterial = EnvironmentMaterials.Family(accent);
            Object.DestroyImmediate(standard.GetComponent<Collider>());

            var skirt = Child(root, "Skirt");
            int rubble = Mathf.RoundToInt(spec.Cols * 3 * spec.Rubble);
            for (int i = 0; i < rubble; i++)
                KenneyKit.Put(skirt, rng.NextDouble() < 0.5 ? KenneyKit.Rocks : KenneyKit.Stones,
                    new Vector3((float)(rng.NextDouble() * 2.0 - 1.0) * spec.Cols * 0.5f * Grid, -0.05f,
                                -backZ + (float)rng.NextDouble() * 0.5f),
                    new Vector3(0f, (float)(rng.NextDouble() * 360.0), 0f),
                    Vector3.one * Clutter * (0.7f + (float)rng.NextDouble() * 0.7f));

            KenneyKit.Put(skirt, KenneyKit.Barrel,
                          new Vector3(-spec.Cols * 0.42f * Grid, 0f, backZ - Grid * 0.6f),
                          (float)(rng.NextDouble() * 360.0), Arch * 0.7f);

            var under = Child(root, "Understructure");
            for (int i = 0; i < spec.Cols * 3 + 4; i++)
            {
                float x = (float)(rng.NextDouble() * 2.0 - 1.0) * spec.Cols * 0.55f * Grid;
                float z = (float)(rng.NextDouble() * 2.0 - 1.0) * backZ;
                float bias = 1f - Mathf.Abs(x) / Mathf.Max(0.01f, spec.Cols * 0.55f * Grid);
                float top = -0.2f - (float)rng.NextDouble() * (1.2f + bias * 3.5f);
                float k = Arch * (0.5f + (float)rng.NextDouble() * bias * 1.6f);
                KenneyKit.PutHanging(under, i % 3 == 0 ? KenneyKit.Dirt : KenneyKit.Rocks,
                                     x, top, z, (float)(rng.NextDouble() * 360.0), k);
            }

            // The label sits IN FRONT of the back wall and above anything stacked on it.
            //
            // Anchored at the wall itself it was hidden behind that wall from the establishing
            // camera, which looks at the bastion's open front - and on the Dog keep the raised
            // centre buried it completely. Pulling it forward off the wall plane and lifting it
            // clear of the keep's extra courses makes all five read from the same shot.
            float labelY = wallH * 1.75f + (spec.RaisedCentre ? wallH * 2f : 0f);
            Socket(root, "Socket_Label", new Vector3(0f, labelY, -Grid * 0.35f));
            Socket(root, "Socket_Roster", Vector3.zero);
        }
    }
}
