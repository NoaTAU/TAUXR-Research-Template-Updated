// RecenterCollector.cs
// shouldRecenter  -> read each frame from OVRPlugin (continuous 0/1)
// recenterEvent   -> 1 only on the first frame where shouldRecenter changes 0->1; else 0

using System;
using static TXRData.CollectorUtils;

namespace TXRData
{
    public sealed class RecenterCollector : IContinuousCollector
    {
        public string CollectorName => "RecenterCollector";

        private int _idxShouldRecenter = -1;
        private int _idxRecenterEvent = -1;

        private int _prevShouldRecenter = 0;   // last seen 0/1, only valid if _haveSignal = true
        private bool _haveSignal = false;      // becomes true after the first successful read
        private bool _enabled = true;

        public void Configure(ColumnIndex schema, RecordingOptions options)
        {
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            TryIndex(schema, "shouldRecenter", out _idxShouldRecenter);
            TryIndex(schema, "recenterEvent", out _idxRecenterEvent);

            _enabled = (_idxShouldRecenter >= 0) || (_idxRecenterEvent >= 0);

            // prime the previous value if the signal exists; otherwise leave _haveSignal=false
            if (TryGetShouldRecenter(out int sr))
            {
                _prevShouldRecenter = sr;
                _haveSignal = true;
            }
        }

        public void Collect(RowBuffer row, float timeSinceStartup)
        {
            if (!_enabled) return;

            // If the plugin exposes the flag, write it and compute a 0->1 pulse.
            if (TryGetShouldRecenter(out int sr))
            {
                if (_idxShouldRecenter >= 0) row.Set(_idxShouldRecenter, sr);

                int pulse = 0;
                if (_haveSignal && _idxRecenterEvent >= 0)
                    pulse = (_prevShouldRecenter == 0 && sr == 1) ? 1 : 0;

                if (_idxRecenterEvent >= 0) row.Set(_idxRecenterEvent, pulse);

                _prevShouldRecenter = sr;
                _haveSignal = true;
            }
            // If not available, leave both cells blank this frame.
        }

        public void Dispose() { }

        // --- plugin wrapper ---
        private static bool TryGetShouldRecenter(out int value)
        {
            try
            {
                value = OVRPlugin.shouldRecenter ? 1 : 0;
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }
    }
}
