using System;

namespace VRLicensing
{
    /// <summary>
    /// v1.12.0-pre.16 (spec 11 §6) - the demo clock, without any Unity time source so it can be
    /// tested frame by frame. It replaces "now minus session start" (wall-clock time, which kept
    /// counting while the headset was off and added the whole gap on the next frame) with an
    /// accumulator fed one frame at a time:
    /// <list type="bullet">
    /// <item>a frame counts only while the caller says the demo is really being played
    /// (app not paused, focused, user present);</item>
    /// <item>a single frame longer than <see cref="MaxFrameGapSeconds"/> is dropped whole
    /// (frames stopped: the user was not playing);</item>
    /// <item>the first frame after a pause is dropped (its delta covers the pause);</item>
    /// <item>the total never passes the limit, and never drops below what was already stored.</item>
    /// </list>
    /// </summary>
    public sealed class DemoStopwatch
    {
        /// <summary>Default for <see cref="MaxFrameGapSeconds"/>. A Quest frame is 11-14 ms; a scene load can stall for about a second.</summary>
        public const float DefaultMaxFrameGapSeconds = 3f;

        /// <summary>A single frame delta above this is ignored instead of being counted.</summary>
        public float MaxFrameGapSeconds = DefaultMaxFrameGapSeconds;

        /// <summary>Seconds counted since <see cref="Restart"/> (not yet persisted).</summary>
        public float SessionSeconds { get; private set; }

        /// <summary>Seconds offered to <see cref="Advance"/> but not counted since <see cref="Restart"/> (diagnostics).</summary>
        public float IgnoredSeconds { get; private set; }

        /// <summary>Frames dropped for being longer than <see cref="MaxFrameGapSeconds"/> since <see cref="Restart"/>.</summary>
        public int GapsIgnored { get; private set; }

        private bool wasCounting;

        /// <summary>Clears the session; the next frame is the first of a new segment and is not counted.</summary>
        public void Restart()
        {
            SessionSeconds = 0f;
            IgnoredSeconds = 0f;
            GapsIgnored = 0;
            wasCounting = false;
        }

        /// <summary>
        /// Offers one frame. Returns the seconds actually counted (0 when the frame is dropped).
        /// </summary>
        /// <param name="deltaSeconds">Unscaled duration of the frame.</param>
        /// <param name="counting">True while the demo is being played: Demo state, app running, focused, user present.</param>
        /// <param name="storedSeconds">Demo seconds already persisted before this session.</param>
        /// <param name="limitSeconds">Effective demo limit.</param>
        public float Advance(float deltaSeconds, bool counting, float storedSeconds, float limitSeconds)
        {
            if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds)) { wasCounting = counting; return 0f; }

            if (!counting)
            {
                wasCounting = false;
                IgnoredSeconds += deltaSeconds;
                return 0f;
            }

            // First frame after a pause: its delta spans the pause itself.
            if (!wasCounting)
            {
                wasCounting = true;
                IgnoredSeconds += deltaSeconds;
                return 0f;
            }

            if (deltaSeconds > MaxFrameGapSeconds)
            {
                GapsIgnored++;
                IgnoredSeconds += deltaSeconds;
                return 0f;
            }

            // Never above the limit; a stored total already above it (older builds, a lowered
            // limit) is left alone rather than lowered.
            float ceiling = Math.Max(limitSeconds, storedSeconds);
            float room = ceiling - (storedSeconds + SessionSeconds);
            if (room <= SnapSeconds) return 0f;

            float counted = Math.Min(deltaSeconds, room);
            SessionSeconds += counted;
            return counted;
        }

        /// <summary>Float sums can land a hair under the limit; within this distance the total IS the limit.</summary>
        public const float SnapSeconds = 0.001f;

        /// <summary>
        /// Total demo seconds: what was stored plus this session, snapped to the ceiling (the
        /// limit, or the stored total when that is higher) once it is within a millisecond of it,
        /// so "reached the limit" is exact and the persisted value is the limit itself.
        /// </summary>
        public float Total(float storedSeconds, float limitSeconds)
        {
            float total = storedSeconds + SessionSeconds;
            float ceiling = Math.Max(limitSeconds, storedSeconds);
            return total > ceiling - SnapSeconds ? ceiling : total;
        }
    }
}
