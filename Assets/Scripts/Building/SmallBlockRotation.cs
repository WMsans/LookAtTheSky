using UnityEngine;

namespace Building
{
    /// <summary>
    /// Computes one of 24 discrete rotations for small blocks.
    /// Bottom face (-Y local) points into the raycasted surface.
    /// Front face (+Z local) points toward the player.
    /// </summary>
    public static class SmallBlockRotation
    {
        private static readonly Vector3[] CardinalAxes =
        {
            Vector3.right,   // +X
            Vector3.left,    // -X
            Vector3.up,      // +Y
            Vector3.down,    // -Y
            Vector3.forward, // +Z
            Vector3.back     // -Z
        };

        /// <summary>
        /// Compute the rotation for a small block given the hit surface normal,
        /// player position, and the block's world-space center.
        /// </summary>
        public static Quaternion ComputeRotation(
            Vector3 hitNormal, Vector3 playerPosition, Vector3 blockWorldPosition)
        {
            // Bottom face points into the surface (opposite of normal)
            Vector3 bottomDir = SnapToAxis(-hitNormal);
            Vector3 upDir = -bottomDir;

            // Front face points toward the player, projected onto the plane
            // perpendicular to the bottom direction
            Vector3 toPlayer = (playerPosition - blockWorldPosition).normalized;
            Vector3 frontDir = ProjectAndSnapToPlane(toPlayer, upDir);

            // Fallback: if toPlayer is parallel to upDir, pick an arbitrary front
            if (frontDir.sqrMagnitude < 0.01f)
            {
                frontDir = GetArbitraryPerpendicular(upDir);
            }

            return Quaternion.LookRotation(frontDir, upDir);
        }

        /// <summary>
        /// Snap a direction vector to the nearest cardinal axis.
        /// </summary>
        public static Vector3 SnapToAxis(Vector3 dir)
        {
            Vector3 best = CardinalAxes[0];
            float bestDot = Vector3.Dot(dir, best);

            for (int i = 1; i < CardinalAxes.Length; i++)
            {
                float dot = Vector3.Dot(dir, CardinalAxes[i]);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = CardinalAxes[i];
                }
            }

            return best;
        }

        /// <summary>
        /// Project a direction onto the plane defined by planeNormal,
        /// then snap the result to the nearest cardinal axis in that plane.
        /// </summary>
        public static Vector3 ProjectAndSnapToPlane(Vector3 dir, Vector3 planeNormal)
        {
            // Project onto plane
            Vector3 projected = dir - Vector3.Dot(dir, planeNormal) * planeNormal;

            if (projected.sqrMagnitude < 0.001f)
                return Vector3.zero;

            projected.Normalize();

            // Snap to nearest cardinal axis that lies in the plane
            Vector3 best = Vector3.zero;
            float bestDot = -2f;

            for (int i = 0; i < CardinalAxes.Length; i++)
            {
                // Skip axes that are parallel to the plane normal
                if (Mathf.Abs(Vector3.Dot(CardinalAxes[i], planeNormal)) > 0.9f)
                    continue;

                float dot = Vector3.Dot(projected, CardinalAxes[i]);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = CardinalAxes[i];
                }
            }

            return best;
        }

        private static Vector3 GetArbitraryPerpendicular(Vector3 normal)
        {
            // Pick the axis least parallel to normal
            Vector3 candidate = Mathf.Abs(Vector3.Dot(normal, Vector3.right)) < 0.9f
                ? Vector3.right
                : Vector3.forward;

            return SnapToAxis(Vector3.Cross(normal, candidate));
        }
    }
}
