using UnityEngine;

namespace CubeSim.Visuals
{
    /// <summary>
    /// Screen shake for hits and blasts. Purely cosmetic: it offsets the camera in LateUpdate and
    /// puts it back when the shake ends, so the framing code never sees it. Amplitude decays
    /// linearly; several shakes stack to the largest.
    /// </summary>
    public sealed class CameraShake : MonoBehaviour
    {
        private Vector3 _base;
        private bool _stored;
        private float _amplitude;
        private float _duration;
        private float _left;
        private readonly System.Random _random = new System.Random(1234);

        private Vector3 _punchTarget;
        private float _punchAmount;
        private float _punchIn, _punchHold, _punchOut, _punchT = -1f;

        /// <summary>
        /// Kill-cam: slide the camera a fraction of the way toward a world point, hold, ease back.
        /// The framing code never sees it: the base position is restored when the move ends.
        /// </summary>
        public void PunchIn(Vector3 target, float amount, float inSeconds, float holdSeconds, float outSeconds)
        {
            _punchTarget = target; _punchAmount = amount;
            _punchIn = Mathf.Max(0.01f, inSeconds); _punchHold = holdSeconds; _punchOut = Mathf.Max(0.01f, outSeconds);
            _punchT = 0f;
        }

        public void Add(float amplitude, float seconds)
        {
            if (amplitude <= 0f || seconds <= 0f) return;
            if (amplitude >= _amplitude || _left <= 0f)
            {
                _amplitude = amplitude;
                _duration = seconds;
                _left = seconds;
            }
        }

        private void LateUpdate()
        {
            bool shaking = _left > 0f;
            bool punching = _punchT >= 0f;
            if (!shaking && !punching)
            {
                if (_stored) { transform.position = _base; _stored = false; }
                return;
            }

            if (!_stored) { _base = transform.position; _stored = true; }
            Vector3 offset = Vector3.zero;

            if (shaking)
            {
                _left -= Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_left / Mathf.Max(0.01f, _duration));
                float a = _amplitude * t;
                offset += new Vector3(((float)_random.NextDouble() * 2f - 1f) * a, 0f, ((float)_random.NextDouble() * 2f - 1f) * a);
            }

            if (punching)
            {
                _punchT += Time.unscaledDeltaTime;
                float total = _punchIn + _punchHold + _punchOut;
                float k = _punchT < _punchIn ? Mathf.SmoothStep(0f, 1f, _punchT / _punchIn)
                    : _punchT < _punchIn + _punchHold ? 1f
                    : _punchT < total ? Mathf.SmoothStep(1f, 0f, (_punchT - _punchIn - _punchHold) / _punchOut)
                    : 0f;
                if (_punchT >= total) _punchT = -1f;
                offset += (_punchTarget - _base) * (_punchAmount * k);
            }

            transform.position = _base + offset;
        }

        private void OnDisable()
        {
            if (_stored) { transform.position = _base; _stored = false; }
            _left = 0f;
            _punchT = -1f;
        }
    }
}
