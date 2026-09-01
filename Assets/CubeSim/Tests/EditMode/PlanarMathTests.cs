using NUnit.Framework;
using UnityEngine;
using CubeSim.Arena;
using CubeSim.Arena.Authored;
using CubeSim.Combat;
using CubeSim.Core;

namespace CubeSim.Tests
{
    /// <summary>
    /// Covers the movement math only - the part where a mistake silently changes every simulation.
    /// Scene plumbing is deliberately not tested here.
    /// </summary>
    public class PlanarMathTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void Flatten_RemovesVerticalComponent()
        {
            Vector3 flat = PlanarMath.Flatten(new Vector3(1f, 7f, -3f));
            Assert.AreEqual(0f, flat.y);
            Assert.AreEqual(1f, flat.x, Tolerance);
            Assert.AreEqual(-3f, flat.z, Tolerance);
        }

        [Test]
        public void TryNormalizePlanar_ProducesUnitLength()
        {
            Assert.IsTrue(PlanarMath.TryNormalizePlanar(new Vector3(3f, 99f, 4f), out Vector3 result));
            Assert.AreEqual(1f, result.magnitude, Tolerance);
            Assert.AreEqual(0f, result.y);
        }

        [Test]
        public void TryNormalizePlanar_RejectsPurelyVerticalVectors()
        {
            Assert.IsFalse(PlanarMath.TryNormalizePlanar(Vector3.up, out Vector3 result));
            Assert.AreEqual(Vector3.zero, result);
        }

        [Test]
        public void Reflect_HeadOnHitReversesDirection()
        {
            Vector3 reflected = PlanarMath.Reflect(Vector3.right, Vector3.left);
            Assert.AreEqual(-1f, reflected.x, Tolerance);
            Assert.AreEqual(0f, reflected.z, Tolerance);
        }

        [Test]
        public void Reflect_FortyFiveDegreeHitTurnsNinetyDegrees()
        {
            // Travelling +X+Z into a wall whose normal is -Z must come out as +X-Z.
            Vector3 incoming = new Vector3(1f, 0f, 1f).normalized;
            Vector3 reflected = PlanarMath.Reflect(incoming, Vector3.back);

            Assert.AreEqual(incoming.x, reflected.x, Tolerance);
            Assert.AreEqual(-incoming.z, reflected.z, Tolerance);
        }

        [Test]
        public void Reflect_PreservesUnitLengthSoSpeedIsConstant()
        {
            var normals = new[]
            {
                Vector3.left, Vector3.right, Vector3.forward, Vector3.back,
                new Vector3(0.6f, 0f, 0.8f), new Vector3(-0.28f, 0f, 0.96f)
            };

            Vector3 direction = PlanarMath.DirectionFromAngle(37f);
            for (int i = 0; i < normals.Length; i++)
            {
                direction = PlanarMath.Reflect(direction, normals[i]);
                Assert.AreEqual(1f, direction.magnitude, Tolerance, $"after bounce {i}");
                Assert.AreEqual(0f, direction.y, Tolerance, $"left the XZ plane at bounce {i}");
            }
        }

        [Test]
        public void Reflect_IgnoresVerticalNormalComponent()
        {
            Vector3 tilted = PlanarMath.Reflect(Vector3.right, new Vector3(-1f, 5f, 0f));
            Assert.AreEqual(-1f, tilted.x, Tolerance);
            Assert.AreEqual(0f, tilted.y, Tolerance);
        }

        [Test]
        public void Reflect_OnDegenerateNormalKeepsDirection()
        {
            Vector3 kept = PlanarMath.Reflect(Vector3.forward, Vector3.up);
            Assert.AreEqual(Vector3.forward, kept);
        }

        [Test]
        public void AngleAndDirection_RoundTrip()
        {
            foreach (float angle in new[] { 0f, 37f, 90f, 180f, 271.5f, 359f })
            {
                Vector3 direction = PlanarMath.DirectionFromAngle(angle);
                Assert.AreEqual(1f, direction.magnitude, Tolerance);
                Assert.AreEqual(angle, PlanarMath.AngleFromDirection(direction), 1e-2f);
            }
        }

        [Test]
        public void StepDistance_IsSpeedTimesDelta()
        {
            Assert.AreEqual(0.15f, PlanarMath.StepDistance(9f, 1f / 60f), Tolerance);
            Assert.AreEqual(0f, PlanarMath.StepDistance(9f, -1f), Tolerance);
            Assert.AreEqual(0f, PlanarMath.StepDistance(-9f, 1f), Tolerance);
        }

        [Test]
        public void ConsumeDistance_NeverGoesNegative()
        {
            Assert.AreEqual(0.05f, PlanarMath.ConsumeDistance(0.15f, 0.10f), Tolerance);
            Assert.AreEqual(0f, PlanarMath.ConsumeDistance(0.15f, 0.99f), Tolerance);
            Assert.AreEqual(0.15f, PlanarMath.ConsumeDistance(0.15f, -1f), Tolerance);
        }

        [Test]
        public void ConsumeDistance_SplitStepCoversTheSameTotal()
        {
            // A bounce splits one step into two legs; together they must equal the step budget.
            float budget = PlanarMath.StepDistance(12f, 1f / 60f);
            float firstLeg = budget * 0.35f;
            float remaining = PlanarMath.ConsumeDistance(budget, firstLeg);

            Assert.AreEqual(budget, firstLeg + remaining, Tolerance);
        }

        [Test]
        public void HalfSpacePenetration_DetectsAnAdvancingBoundary()
        {
            // Playable region is x >= boundary. A racer of half extent 0.5 centred at 10.2 with the
            // boundary at 10.0 overlaps by 0.3.
            float penetration = PlanarMath.HalfSpacePenetration(10.2f, 0.5f, 10f, 1f);
            Assert.AreEqual(0.3f, penetration, Tolerance);

            // Clear of the boundary, so no correction.
            Assert.Less(PlanarMath.HalfSpacePenetration(12f, 0.5f, 10f, 1f), 0f);
        }

        [Test]
        public void HalfSpacePenetration_HandlesTheMirroredSide()
        {
            // Playable region is x <= boundary.
            Assert.AreEqual(0.3f, PlanarMath.HalfSpacePenetration(-10.2f, 0.5f, -10f, -1f), Tolerance);
            Assert.Less(PlanarMath.HalfSpacePenetration(-12f, 0.5f, -10f, -1f), 0f);
        }
    }

    public class SimulationRandomTests
    {
        [Test]
        public void SameSeedProducesTheSameStream()
        {
            var a = new SimulationRandom(4242);
            var b = new SimulationRandom(4242);

            for (int i = 0; i < 64; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt(), $"at draw {i}");
        }

        [Test]
        public void NeighbouringSeedsDiverge()
        {
            var a = new SimulationRandom(1);
            var b = new SimulationRandom(2);

            bool differs = false;
            for (int i = 0; i < 16 && !differs; i++) differs = a.NextUInt() != b.NextUInt();

            Assert.IsTrue(differs, "seeds 1 and 2 produced identical streams");
        }

        [Test]
        public void NextFloatStaysInRange()
        {
            var random = new SimulationRandom(7);
            for (int i = 0; i < 2000; i++)
            {
                float value = random.NextFloat();
                Assert.GreaterOrEqual(value, 0f);
                Assert.Less(value, 1f);
            }
        }

        [Test]
        public void BiasedDirectionsStayAwayFromTheAxes()
        {
            var random = new SimulationRandom(99);
            for (int i = 0; i < 500; i++)
            {
                Vector3 direction = random.NextPlanarDirectionBiased(20f);
                Assert.AreEqual(1f, direction.magnitude, 1e-4f);
                Assert.GreaterOrEqual(Mathf.Abs(direction.x), 0.3f, "too close to the Z axis");
                Assert.GreaterOrEqual(Mathf.Abs(direction.z), 0.3f, "too close to the X axis");
            }
        }
    }

    /// <summary>The rules that keep a generated arena playable.</summary>
    public class ArenaGenerationTests
    {
        [Test]
        public void MinimumCorridorWidthScalesWithRacerSize()
        {
            var settings = new ArenaGenerationSettings
            {
                minimumCorridorWidth = 0f,
                corridorWidthMultiplier = 3f,
                corridorSafetyMargin = 0.4f
            };

            Assert.AreEqual(3.4f, settings.ResolveMinimumCorridorWidth(1f), 1e-4f);
            Assert.AreEqual(6.4f, settings.ResolveMinimumCorridorWidth(2f), 1e-4f);
        }

        [Test]
        public void ExplicitMinimumCorridorWidthOverridesTheDerivedValue()
        {
            var settings = new ArenaGenerationSettings { minimumCorridorWidth = 5f };
            Assert.AreEqual(5f, settings.ResolveMinimumCorridorWidth(1f), 1e-4f);
        }

        [Test]
        public void CentralClearingKeepOutIsLargerThanTheClearingItself()
        {
            var clearing = new CentralClearing
            {
                halfExtents = new Vector2(6f, 4f),
                margin = 1.5f
            };

            Rect inner = clearing.Rect;
            Rect keepOut = clearing.KeepOutRect;

            Assert.AreEqual(12f, inner.width, 1e-4f);
            Assert.AreEqual(8f, inner.height, 1e-4f);
            Assert.AreEqual(15f, keepOut.width, 1e-4f);
            Assert.AreEqual(11f, keepOut.height, 1e-4f);
            Assert.IsTrue(keepOut.Contains(new Vector2(inner.xMin, inner.yMin)));
        }

        [Test]
        public void ProfilesProduceDistinctWallBudgets()
        {
            int Budget(ArenaGenerationProfile profile)
            {
                var s = new ArenaGenerationSettings { profile = profile };
                s.ApplyProfile();
                return s.wallBudget;
            }

            int sparse = Budget(ArenaGenerationProfile.Sparse);
            int medium = Budget(ArenaGenerationProfile.Medium);
            int dense = Budget(ArenaGenerationProfile.Dense);

            Assert.Less(sparse, medium, "Sparse should place fewer structures than Medium");
            Assert.Less(medium, dense, "Medium should place fewer structures than Dense");
        }

        [Test]
        public void CustomProfileLeavesAuthoredValuesAlone()
        {
            var settings = new ArenaGenerationSettings
            {
                profile = ArenaGenerationProfile.Custom,
                wallBudget = 7,
                openAreaBias = 0.42f
            };

            settings.ApplyProfile();

            Assert.AreEqual(7, settings.wallBudget);
            Assert.AreEqual(0.42f, settings.openAreaBias, 1e-4f);
        }
    }

    public class WeaponConfigTests
    {
        [Test]
        public void DefaultCatalogShipsOneMeleeAndOneRangedArchetype()
        {
            var catalog = WeaponConfig.DefaultCatalog();

            int melee = 0, ranged = 0;
            foreach (WeaponDefinition weapon in catalog)
            {
                if (weapon.category == WeaponCategory.Melee) melee++;
                else ranged++;

                Assert.Greater(weapon.damage, 0f, weapon.id + " must do damage");
                Assert.Greater(weapon.attackCooldown, 0f, weapon.id + " needs a cooldown");
                Assert.Greater(weapon.attackRange, 0f, weapon.id + " needs a range");
            }

            Assert.AreEqual(1, melee);
            Assert.AreEqual(1, ranged);
        }

        [Test]
        public void RangedArchetypeRequiresLineOfSightSoWallsBlockShots()
        {
            foreach (WeaponDefinition weapon in WeaponConfig.DefaultCatalog())
            {
                if (weapon.category != WeaponCategory.Ranged) continue;
                Assert.IsTrue(weapon.requireLineOfSight, weapon.id + " should not shoot through walls");
                Assert.Greater(weapon.projectileSpeed, 0f);
            }
        }

        [Test]
        public void MeleeReachIsShorterThanRangedReach()
        {
            float melee = 0f, ranged = 0f;
            foreach (WeaponDefinition weapon in WeaponConfig.DefaultCatalog())
            {
                if (weapon.category == WeaponCategory.Melee) melee = weapon.attackRange;
                else ranged = weapon.attackRange;
            }

            Assert.Less(melee, ranged);
        }
    }

    /// <summary>
    /// The boundary-fill invariant: filling the dead space behind a wall must never move the face
    /// racers bounce off. Getting this wrong silently changes the shape of every authored course.
    /// </summary>
    public class WallFillMathTests
    {
        private const float Tolerance = 1e-4f;

        private static readonly Rect Arena = Rect.MinMaxRect(-36f, -26f, 36f, 26f);

        [Test]
        public void ExtendPlusX_KeepsTheInnerFaceAndGrowsOnlyOutward()
        {
            // Playable side is -X, so the wall's xMin must survive untouched.
            Rect authored = Rect.MinMaxRect(5f, -10f, 6f, 10f);
            Rect filled = WallFillMath.Extend(authored, Arena, FillDirection.PlusX);

            Assert.AreEqual(5f, filled.xMin, Tolerance, "the playable face moved");
            Assert.AreEqual(Arena.xMax, filled.xMax, Tolerance, "the wall did not reach the arena edge");
            Assert.AreEqual(authored.yMin, filled.yMin, Tolerance);
            Assert.AreEqual(authored.yMax, filled.yMax, Tolerance);
        }

        [Test]
        public void ExtendMinusX_KeepsTheInnerFace()
        {
            Rect authored = Rect.MinMaxRect(-6f, -10f, -5f, 10f);
            Rect filled = WallFillMath.Extend(authored, Arena, FillDirection.MinusX);

            Assert.AreEqual(-5f, filled.xMax, Tolerance, "the playable face moved");
            Assert.AreEqual(Arena.xMin, filled.xMin, Tolerance);
        }

        [Test]
        public void ExtendPlusZ_KeepsTheInnerFace()
        {
            Rect authored = Rect.MinMaxRect(-10f, 20f, 10f, 21f);
            Rect filled = WallFillMath.Extend(authored, Arena, FillDirection.PlusZ);

            Assert.AreEqual(20f, filled.yMin, Tolerance, "the playable face moved");
            Assert.AreEqual(Arena.yMax, filled.yMax, Tolerance);
            Assert.AreEqual(authored.xMin, filled.xMin, Tolerance);
            Assert.AreEqual(authored.xMax, filled.xMax, Tolerance);
        }

        [Test]
        public void ExtendMinusZ_KeepsTheInnerFace()
        {
            Rect authored = Rect.MinMaxRect(-10f, -21f, 10f, -20f);
            Rect filled = WallFillMath.Extend(authored, Arena, FillDirection.MinusZ);

            Assert.AreEqual(-20f, filled.yMax, Tolerance, "the playable face moved");
            Assert.AreEqual(Arena.yMin, filled.yMin, Tolerance);
        }

        [Test]
        public void Extend_MovesTheCentreNotJustTheSize()
        {
            // Scaling about the centre would have moved the inner face; both must change together.
            Rect authored = Rect.MinMaxRect(5f, -10f, 6f, 10f);
            Rect filled = WallFillMath.Extend(authored, Arena, FillDirection.PlusX);

            Assert.Greater(filled.width, authored.width);
            Assert.AreNotEqual(authored.center.x, filled.center.x);
            Assert.AreEqual(filled.xMin + filled.width * 0.5f, filled.center.x, Tolerance);
        }

        [Test]
        public void Extend_IsIdempotent()
        {
            Rect authored = Rect.MinMaxRect(5f, -10f, 6f, 10f);
            Rect once = WallFillMath.Extend(authored, Arena, FillDirection.PlusX);
            Rect twice = WallFillMath.Extend(once, Arena, FillDirection.PlusX);

            Assert.AreEqual(once.xMin, twice.xMin, Tolerance);
            Assert.AreEqual(once.xMax, twice.xMax, Tolerance);
        }

        [Test]
        public void Extend_WallAlreadyPastTheEdgeDoesNotInvert()
        {
            Rect authored = Rect.MinMaxRect(40f, -10f, 42f, 10f);
            Rect filled = WallFillMath.Extend(authored, Arena, FillDirection.PlusX);

            Assert.GreaterOrEqual(filled.width, 0f, "the rect flipped inside out");
            Assert.AreEqual(40f, filled.xMin, Tolerance);
        }

        [Test]
        public void Extend_WithVisualPadding_GrowsOutwardOnlyAndPinsTheInnerFace()
        {
            // The presentation pass hands Extend a padded bounds so the mass runs well past the
            // playfield. That must add thickness behind the wall and nothing else - if the inner face
            // moved, every corridor on the map would silently narrow.
            Rect authored = Rect.MinMaxRect(5f, -10f, 6f, 10f);
            Rect padded = Rect.MinMaxRect(Arena.xMin - 20f, Arena.yMin - 20f, Arena.xMax + 20f, Arena.yMax + 20f);

            Rect plain = WallFillMath.Extend(authored, Arena, FillDirection.PlusX);
            Rect filled = WallFillMath.Extend(authored, padded, FillDirection.PlusX);

            Assert.AreEqual(5f, filled.xMin, Tolerance, "padding moved the playable face");
            Assert.AreEqual(padded.xMax, filled.xMax, Tolerance, "the mass did not reach the padded edge");
            Assert.Greater(filled.width, plain.width, "padding did not thicken the wall");

            // The perpendicular axis is untouched: padding never widens a wall sideways into the course.
            Assert.AreEqual(authored.yMin, filled.yMin, Tolerance);
            Assert.AreEqual(authored.yMax, filled.yMax, Tolerance);
        }

        [Test]
        public void Extend_WithVisualPadding_PinsTheInnerFaceInEveryDirection()
        {
            Rect padded = Rect.MinMaxRect(Arena.xMin - 20f, Arena.yMin - 20f, Arena.xMax + 20f, Arena.yMax + 20f);

            Assert.AreEqual(-5f,
                WallFillMath.Extend(Rect.MinMaxRect(-6f, -10f, -5f, 10f), padded, FillDirection.MinusX).xMax,
                Tolerance);
            Assert.AreEqual(20f,
                WallFillMath.Extend(Rect.MinMaxRect(-10f, 20f, 10f, 21f), padded, FillDirection.PlusZ).yMin,
                Tolerance);
            Assert.AreEqual(-20f,
                WallFillMath.Extend(Rect.MinMaxRect(-10f, -21f, 10f, -20f), padded, FillDirection.MinusZ).yMax,
                Tolerance);
        }

        [Test]
        public void AxisAndSign_MatchTheDirection()
        {
            Assert.AreEqual(0, WallFillMath.Axis(FillDirection.PlusX));
            Assert.AreEqual(0, WallFillMath.Axis(FillDirection.MinusX));
            Assert.AreEqual(1, WallFillMath.Axis(FillDirection.PlusZ));
            Assert.AreEqual(1, WallFillMath.Axis(FillDirection.MinusZ));

            Assert.AreEqual(1f, WallFillMath.Sign(FillDirection.PlusX));
            Assert.AreEqual(-1f, WallFillMath.Sign(FillDirection.MinusX));
            Assert.AreEqual(1f, WallFillMath.Sign(FillDirection.PlusZ));
            Assert.AreEqual(-1f, WallFillMath.Sign(FillDirection.MinusZ));
        }

        [Test]
        public void PointsAwayFrom_CatchesAWallFillingIntoTheCourse()
        {
            Rect authored = Rect.MinMaxRect(5f, -10f, 6f, 10f);

            // Course is at x = 0, wall fills toward +X: correct, the fill goes away from play.
            Assert.IsTrue(WallFillMath.PointsAwayFrom(authored, new Vector2(0f, 0f), FillDirection.PlusX));

            // Course is at x = 20, still filling +X: the wall would swallow the playable space.
            Assert.IsFalse(WallFillMath.PointsAwayFrom(authored, new Vector2(20f, 0f), FillDirection.PlusX));
        }

        [Test]
        public void FromCenterSize_HandlesNegativeScale()
        {
            Rect r = WallFillMath.FromCenterSize(new Vector2(2f, 3f), new Vector2(-4f, 6f));

            Assert.AreEqual(4f, r.width, Tolerance);
            Assert.AreEqual(0f, r.xMin, Tolerance);
            Assert.AreEqual(6f, r.height, Tolerance);
        }
    }
}
