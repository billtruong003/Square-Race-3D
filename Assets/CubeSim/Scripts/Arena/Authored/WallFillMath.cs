using UnityEngine;

namespace CubeSim.Arena.Authored
{
    public enum ArenaWallType
    {
        /// <summary>Playable space on both sides. Keeps its authored thickness.</summary>
        Internal = 0,

        /// <summary>Only one side is playable. The other side fills the unused region solid.</summary>
        BoundaryFill = 1
    }

    public enum WallFillMode
    {
        FixedThickness = 0,
        ExtendToArenaBounds = 1
    }

    public enum FillDirection
    {
        PlusX = 0,
        MinusX = 1,
        PlusZ = 2,
        MinusZ = 3
    }

    /// <summary>
    /// The geometry rule behind boundary-fill walls.
    ///
    /// A designer places a wall so that its <em>inner face</em> - the one racers bounce off - sits
    /// exactly where the corridor should end. Filling the dead space behind it must not move that
    /// face, so the box cannot simply be scaled about its centre: size and centre both change, and
    /// the inner face coordinate is the invariant.
    ///
    /// Pure math, no Unity scene state, so the invariant is unit tested.
    /// </summary>
    public static class WallFillMath
    {
        /// <summary>The axis a fill direction runs along. 0 = X, 1 = Z.</summary>
        public static int Axis(FillDirection direction)
            => direction == FillDirection.PlusX || direction == FillDirection.MinusX ? 0 : 1;

        /// <summary>+1 when the wall grows toward increasing coordinates, -1 otherwise.</summary>
        public static float Sign(FillDirection direction)
            => direction == FillDirection.PlusX || direction == FillDirection.PlusZ ? 1f : -1f;

        /// <summary>
        /// The face that must not move: the one on the playable side, i.e. opposite the fill.
        /// </summary>
        public static float InnerFace(Rect authored, FillDirection direction)
        {
            switch (direction)
            {
                case FillDirection.PlusX: return authored.xMin;
                case FillDirection.MinusX: return authored.xMax;
                case FillDirection.PlusZ: return authored.yMin;
                default: return authored.yMax;
            }
        }

        /// <summary>
        /// Extends an authored footprint to the arena edge along <paramref name="direction"/>, keeping
        /// the inner face exactly where the author put it.
        ///
        /// Rects are XZ footprints: Rect.y is the Z axis.
        /// </summary>
        public static Rect Extend(Rect authored, Rect arenaBounds, FillDirection direction)
        {
            float inner = InnerFace(authored, direction);

            switch (direction)
            {
                case FillDirection.PlusX:
                {
                    float outer = Mathf.Max(arenaBounds.xMax, inner);
                    return Rect.MinMaxRect(inner, authored.yMin, outer, authored.yMax);
                }
                case FillDirection.MinusX:
                {
                    float outer = Mathf.Min(arenaBounds.xMin, inner);
                    return Rect.MinMaxRect(outer, authored.yMin, inner, authored.yMax);
                }
                case FillDirection.PlusZ:
                {
                    float outer = Mathf.Max(arenaBounds.yMax, inner);
                    return Rect.MinMaxRect(authored.xMin, inner, authored.xMax, outer);
                }
                default:
                {
                    float outer = Mathf.Min(arenaBounds.yMin, inner);
                    return Rect.MinMaxRect(authored.xMin, outer, authored.xMax, inner);
                }
            }
        }

        /// <summary>
        /// True when the fill direction points away from the playable region, i.e. the wall grows
        /// outward rather than eating into the course. Used by authored-map validation.
        /// </summary>
        public static bool PointsAwayFrom(Rect authored, Vector2 playablePoint, FillDirection direction)
        {
            float inner = InnerFace(authored, direction);
            float sign = Sign(direction);
            float coordinate = Axis(direction) == 0 ? playablePoint.x : playablePoint.y;

            // The playable sample must lie on the inner side of the face.
            return (coordinate - inner) * sign <= 0f;
        }

        /// <summary>
        /// Rect from a centre and a size. The size is taken as an absolute extent, so a designer who
        /// mirrors an object with a negative scale still gets the rect they see in the Scene view.
        /// </summary>
        public static Rect FromCenterSize(Vector2 center, Vector2 size)
        {
            float width = Mathf.Abs(size.x);
            float height = Mathf.Abs(size.y);
            return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
        }
    }
}
