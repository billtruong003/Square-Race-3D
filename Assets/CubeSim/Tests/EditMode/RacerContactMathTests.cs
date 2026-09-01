using NUnit.Framework;
using UnityEngine;
using CubeSim.Racers;

namespace CubeSim.Tests
{
    /// <summary>
    /// Racer-vs-racer contact math.
    ///
    /// The response has to satisfy three things at once that are easy to break independently: the
    /// bounce follows the real contact normal rather than snapping to an axis or blindly reversing,
    /// the configured speed survives every contact, and a pair closing faster than their own diameter
    /// per step still registers instead of passing through each other.
    /// </summary>
    public class RacerContactMathTests
    {
        private const float Tolerance = 1e-4f;
        private const float ContactDistance = 1f;

        private static Vector3 At(float x, float z) => new Vector3(x, 0.5f, z);

        // ------------------------------------------------------------------ overlap

        [Test]
        public void TryOverlap_ReportsNothingWhenClear()
        {
            Assert.IsFalse(RacerContactMath.TryOverlap(At(0f, 0f), At(2f, 0f), ContactDistance,
                out _, out _));
        }

        [Test]
        public void TryOverlap_NormalPointsFromAToBAndIsUnitLength()
        {
            Assert.IsTrue(RacerContactMath.TryOverlap(At(0f, 0f), At(0.6f, 0f), ContactDistance,
                out Vector3 normal, out float penetration));

            Assert.AreEqual(1f, normal.x, Tolerance);
            Assert.AreEqual(0f, normal.z, Tolerance);
            Assert.AreEqual(0f, normal.y, Tolerance, "the contact normal left the XZ plane");
            Assert.AreEqual(0.4f, penetration, Tolerance);
        }

        [Test]
        public void TryOverlap_DiagonalContactGivesADiagonalNormal()
        {
            // A box response could only ever push along X or Z here; the whole point of the disc is
            // that a corner approach produces a corner normal.
            Assert.IsTrue(RacerContactMath.TryOverlap(At(0f, 0f), At(0.5f, 0.5f), ContactDistance,
                out Vector3 normal, out _));

            Assert.AreEqual(Mathf.Sqrt(0.5f), normal.x, Tolerance);
            Assert.AreEqual(Mathf.Sqrt(0.5f), normal.z, Tolerance);
            Assert.AreEqual(1f, normal.magnitude, Tolerance);
        }

        [Test]
        public void TryOverlap_CoincidentRacersPickAStableNormal()
        {
            // Two racers on exactly the same point have no centre line. The answer only has to be the
            // same every run, or the same seed would stop reproducing.
            Assert.IsTrue(RacerContactMath.TryOverlap(At(3f, -2f), At(3f, -2f), ContactDistance,
                out Vector3 first, out float penetration));
            Assert.IsTrue(RacerContactMath.TryOverlap(At(3f, -2f), At(3f, -2f), ContactDistance,
                out Vector3 second, out _));

            Assert.AreEqual(first, second);
            Assert.AreEqual(1f, first.magnitude, Tolerance);
            Assert.AreEqual(ContactDistance, penetration, Tolerance);
        }

        // ------------------------------------------------------------------ response

        [Test]
        public void Respond_HeadOnReversesBoth()
        {
            Vector3 a = Vector3.right;
            Vector3 b = Vector3.left;

            RacerContactMath.Respond(ref a, ref b, Vector3.right, out bool changedA, out bool changedB);

            Assert.IsTrue(changedA);
            Assert.IsTrue(changedB);
            Assert.AreEqual(-1f, a.x, Tolerance);
            Assert.AreEqual(1f, b.x, Tolerance);
        }

        [Test]
        public void Respond_AngledContactDeflectsRatherThanReversing()
        {
            // A runs along +X into B sitting on the diagonal. A straight reversal would send it back
            // down -X; the correct billiard answer turns it to -Z.
            Vector3 a = Vector3.right;
            Vector3 b = Vector3.forward;
            var normal = new Vector3(Mathf.Sqrt(0.5f), 0f, Mathf.Sqrt(0.5f));

            RacerContactMath.Respond(ref a, ref b, normal, out bool changedA, out _);

            Assert.IsTrue(changedA);
            Assert.AreEqual(0f, a.x, Tolerance);
            Assert.AreEqual(-1f, a.z, Tolerance);
            Assert.AreNotEqual(-1f, a.x, "an angled contact was resolved as a straight reversal");
        }

        [Test]
        public void Respond_GrazingContactLeavesBothAlone()
        {
            // Both travelling along the contact plane: neither is closing, so neither should turn.
            Vector3 a = Vector3.forward;
            Vector3 b = Vector3.forward;

            RacerContactMath.Respond(ref a, ref b, Vector3.right, out bool changedA, out bool changedB);

            Assert.IsFalse(changedA);
            Assert.IsFalse(changedB);
            Assert.AreEqual(Vector3.forward, a);
            Assert.AreEqual(Vector3.forward, b);
        }

        [Test]
        public void Respond_OnlyTheApproachingRacerTurns()
        {
            // A chases B from behind. B is already moving away and must not be turned back into A.
            Vector3 a = Vector3.right;
            Vector3 b = Vector3.right;

            RacerContactMath.Respond(ref a, ref b, Vector3.right, out bool changedA, out bool changedB);

            Assert.IsTrue(changedA);
            Assert.IsFalse(changedB, "a separating racer was reflected back into the contact");
        }

        [Test]
        public void Respond_PreservesUnitLengthAndThePlane()
        {
            // Speed is direction magnitude times the configured speed, so a direction that drifts off
            // unit length is a racer that silently changed pace.
            var normal = new Vector3(0.31f, 0f, 0.95f).normalized;
            Vector3 a = new Vector3(0.8f, 0f, 0.6f).normalized;
            Vector3 b = new Vector3(-0.2f, 0f, -0.98f).normalized;

            RacerContactMath.Respond(ref a, ref b, normal, out _, out _);

            Assert.AreEqual(1f, a.magnitude, Tolerance);
            Assert.AreEqual(1f, b.magnitude, Tolerance);
            Assert.AreEqual(0f, a.y, Tolerance);
            Assert.AreEqual(0f, b.y, Tolerance);
        }

        [Test]
        public void Respond_DoesNotDependOnWhichRacerIsA()
        {
            // The contact grid always passes the lower-indexed racer as A, but the physics must not
            // care. Handing it the pair the other way round - which flips the normal with it, since
            // the normal runs A to B - has to produce the same two outcomes.
            Vector3 a1 = new Vector3(0.6f, 0f, 0.8f), b1 = new Vector3(-0.28f, 0f, -0.96f);
            Vector3 normal = new Vector3(0.38f, 0f, 0.92f).normalized;

            Vector3 a2 = b1, b2 = a1;

            RacerContactMath.Respond(ref a1, ref b1, normal, out _, out _);
            RacerContactMath.Respond(ref a2, ref b2, -normal, out _, out _);

            // a2/b2 came in swapped, so they come out swapped too.
            Assert.AreEqual(a1.x, b2.x, Tolerance);
            Assert.AreEqual(a1.z, b2.z, Tolerance);
            Assert.AreEqual(b1.x, a2.x, Tolerance);
            Assert.AreEqual(b1.z, a2.z, Tolerance);
        }

        // ------------------------------------------------------------------ sweep

        [Test]
        public void TrySweep_CatchesAPairThatWouldPassThrough()
        {
            // Closing at 4 units over one step with a 1 unit contact distance: nothing overlaps at
            // either end of the step, and a discrete test would see two racers that swapped sides.
            Assert.IsTrue(RacerContactMath.TrySweep(
                At(-2f, 0f), At(2f, 0f),
                At(2f, 0f), At(-2f, 0f),
                ContactDistance, out float toi));

            // They start 4 apart, close at 8 per step, and touch at a gap of 1.
            Assert.AreEqual(3f / 8f, toi, Tolerance);
        }

        [Test]
        public void TrySweep_AlreadyTouchingReportsTheStartOfTheStep()
        {
            Assert.IsTrue(RacerContactMath.TrySweep(
                At(0f, 0f), At(0.1f, 0f),
                At(0.5f, 0f), At(0.6f, 0f),
                ContactDistance, out float toi));

            Assert.AreEqual(0f, toi, Tolerance);
        }

        [Test]
        public void TrySweep_IgnoresASeparatingPair()
        {
            Assert.IsFalse(RacerContactMath.TrySweep(
                At(-2f, 0f), At(-4f, 0f),
                At(2f, 0f), At(4f, 0f),
                ContactDistance, out _));
        }

        [Test]
        public void TrySweep_IgnoresAPairThatNeverGetsCloseEnough()
        {
            // Parallel tracks two units apart: they pass each other but never come within contact.
            Assert.IsFalse(RacerContactMath.TrySweep(
                At(-3f, 0f), At(3f, 0f),
                At(3f, 2f), At(-3f, 2f),
                ContactDistance, out _));
        }

        [Test]
        public void TrySweep_IgnoresRacersWithNoRelativeMotion()
        {
            // Same heading, same speed: the gap never changes, so there is no impact to find.
            Assert.IsFalse(RacerContactMath.TrySweep(
                At(0f, 0f), At(5f, 0f),
                At(3f, 0f), At(8f, 0f),
                ContactDistance, out _));
        }

        [Test]
        public void Lerp_LandsThePairExactlyOnContact()
        {
            RacerContactMath.TrySweep(
                At(-2f, 0f), At(2f, 0f),
                At(2f, 0f), At(-2f, 0f),
                ContactDistance, out float toi);

            Vector3 a = RacerContactMath.Lerp(At(-2f, 0f), At(2f, 0f), toi);
            Vector3 b = RacerContactMath.Lerp(At(2f, 0f), At(-2f, 0f), toi);

            Assert.AreEqual(ContactDistance, Vector3.Distance(
                new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z)), Tolerance);
            Assert.AreEqual(0.5f, a.y, Tolerance, "the rewind moved a racer off its plane");
        }

        // ------------------------------------------------------------------ separation

        [Test]
        public void SplitCorrection_SharesEquallyWhenBothCanMove()
        {
            RacerContactMath.SplitCorrection(0.4f, true, true, out float a, out float b);

            Assert.AreEqual(0.2f, a, Tolerance);
            Assert.AreEqual(0.2f, b, Tolerance);
        }

        [Test]
        public void SplitCorrection_GivesTheWholeShareToTheRacerWithRoom()
        {
            // The pinned racer has a wall behind it. Pushing it anyway is the one outcome that is
            // never acceptable, so the free racer absorbs the entire correction.
            RacerContactMath.SplitCorrection(0.4f, false, true, out float a, out float b);
            Assert.AreEqual(0f, a, Tolerance);
            Assert.AreEqual(0.4f, b, Tolerance);

            RacerContactMath.SplitCorrection(0.4f, true, false, out float c, out float d);
            Assert.AreEqual(0.4f, c, Tolerance);
            Assert.AreEqual(0f, d, Tolerance);
        }

        [Test]
        public void SplitCorrection_MovesNobodyWhenNeitherHasRoom()
        {
            // Wedged between geometry on both sides. The contact pass leaves them; whether this is a
            // crush is the constraint solver's call, not this one.
            RacerContactMath.SplitCorrection(0.4f, false, false, out float a, out float b);

            Assert.AreEqual(0f, a, Tolerance);
            Assert.AreEqual(0f, b, Tolerance);
        }

        [Test]
        public void SplitCorrection_NeverReturnsANegativeShare()
        {
            RacerContactMath.SplitCorrection(-1f, true, true, out float a, out float b);

            Assert.AreEqual(0f, a, Tolerance);
            Assert.AreEqual(0f, b, Tolerance);
        }
    }
}
