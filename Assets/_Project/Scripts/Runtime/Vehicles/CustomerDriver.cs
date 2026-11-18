using CarWash.Player;
using UnityEngine;

namespace CarWash.Vehicles
{
    /// <summary>
    /// M4.8 F4: the customer behind the wheel — purely local on every peer (nothing networked; the look comes from the car's
    /// NetworkObjectId so everybody sees the same person). A Quaternius character is posed sitting with HumanPoseHandler muscles
    /// (no animation clip), hands put on the steering wheel with a two-bone IK, breathes (chest / shoulders), yawns now and then,
    /// blinks if the mesh has a blink blend shape, turns the head toward the nearest player (clamped, smoothed) and gets
    /// annoyed for a moment when sprayed (head shake + fist).
    /// </summary>
    public class CustomerDriver : MonoBehaviour
    {
        [SerializeField] private GameObject[] _looks = System.Array.Empty<GameObject>();
        [SerializeField] private Transform _seat, _wheel;

        Animator _anim;
        HumanPoseHandler _poser;
        HumanPose _pose;
        float[] _rest;
        Vector3 _bodyPos; Quaternion _bodyRot;
        float _hipY = 0.06f; // hips above the cushion top (negative = sunk into a soft seat in a low cabin)
        Transform _model, _head, _chest, _lUp, _lLo, _lHand, _rUp, _rLo, _rHand;
        Quaternion _headOff;
        SkinnedMeshRenderer _blinkSmr; int _blinkIdx = -1;
        float _seed, _nextYawn, _yawnT = -1f, _angryT = -1f, _nextBlink, _blinkT = -1f;
        Quaternion _headCur;

        // muscle indices (HumanTrait names)
        int mLULFB, mRULFB, mLLLS, mRLLS, mLAFB, mRAFB, mLADU, mRADU, mLFS, mRFS, mChestFB, mSpineFB, mLShDU, mRShDU, mNeckFB;

        public Transform Head => _head;
        public bool Angry => _angryT >= 0f;
        public static int Count { get; private set; }

        void Start()
        {
            if (_seat == null || _wheel == null || _looks == null || _looks.Length == 0) { enabled = false; return; }
            var no = GetComponentInParent<Unity.Netcode.NetworkObject>();
            ulong id = no != null ? no.NetworkObjectId : 0;
            var look = _looks[(int)(id * 2654435761UL % (ulong)_looks.Length)];
            if (look == null) { enabled = false; return; }
            _model = Instantiate(look, _seat).transform;
            _model.name = "Driver";
            _model.localPosition = Vector3.zero; _model.localRotation = Quaternion.identity;
            foreach (var t in _model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Core.Layers.Vehicle;
            foreach (var r in _model.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _anim = _model.GetComponent<Animator>();
            if (_anim == null || _anim.avatar == null || !_anim.avatar.isHuman) { Destroy(_model.gameObject); enabled = false; return; }
            _anim.enabled = false; // posed by hand
            _poser = new HumanPoseHandler(_anim.avatar, _model);
            _poser.GetHumanPose(ref _pose);
            int M(string n) => System.Array.IndexOf(HumanTrait.MuscleName, n);
            mLULFB = M("Left Upper Leg Front-Back"); mRULFB = M("Right Upper Leg Front-Back");
            mLLLS = M("Left Lower Leg Stretch"); mRLLS = M("Right Lower Leg Stretch");
            mLAFB = M("Left Arm Front-Back"); mRAFB = M("Right Arm Front-Back");
            mLADU = M("Left Arm Down-Up"); mRADU = M("Right Arm Down-Up");
            mLFS = M("Left Forearm Stretch"); mRFS = M("Right Forearm Stretch");
            mChestFB = M("Chest Front-Back"); mSpineFB = M("Spine Front-Back"); mNeckFB = M("Neck Nod Down-Up");
            mLShDU = M("Left Shoulder Down-Up"); mRShDU = M("Right Shoulder Down-Up");
            _rest = (float[])_pose.muscles.Clone();
            // cached once: reading the body position back every frame compounds the model scale (FitToSeat) and drifts away
            _bodyPos = new Vector3(0f, _pose.bodyPosition.y, 0f); _bodyRot = _pose.bodyRotation;
            SitMuscles(_rest);
            _head = _anim.GetBoneTransform(HumanBodyBones.Head);
            _chest = _anim.GetBoneTransform(HumanBodyBones.Chest) ?? _anim.GetBoneTransform(HumanBodyBones.Spine);
            _lUp = _anim.GetBoneTransform(HumanBodyBones.LeftUpperArm); _lLo = _anim.GetBoneTransform(HumanBodyBones.LeftLowerArm); _lHand = _anim.GetBoneTransform(HumanBodyBones.LeftHand);
            _rUp = _anim.GetBoneTransform(HumanBodyBones.RightUpperArm); _rLo = _anim.GetBoneTransform(HumanBodyBones.RightLowerArm); _rHand = _anim.GetBoneTransform(HumanBodyBones.RightHand);
            foreach (var smr in _model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                for (int i = 0; smr.sharedMesh != null && i < smr.sharedMesh.blendShapeCount; i++)
                    if (smr.sharedMesh.GetBlendShapeName(i).ToLowerInvariant().Contains("blink")) { _blinkSmr = smr; _blinkIdx = i; }
            _seed = (id % 97) * 0.37f;
            _nextYawn = Time.time + 8f + (id % 11);
            _nextBlink = Time.time + 2f;
            ApplyPose(0f);
            FitToSeat();
            if (_head != null) { _headOff = Quaternion.Inverse(Quaternion.LookRotation(_seat.forward, _seat.up)) * _head.rotation; _headCur = _head.rotation; }
            Count++;
        }

        void OnDestroy() { if (_model != null) Count--; _poser?.Dispose(); }

        /// <summary>Sitting: thighs forward, knees bent, arms reaching forward (the IK finishes them on the wheel).</summary>
        void SitMuscles(float[] m)
        {
            void S(int i, float v) { if (i >= 0) m[i] = v; }
            S(mLULFB, 0.62f); S(mRULFB, 0.62f);
            S(mLLLS, -0.85f); S(mRLLS, -0.85f);
            S(mLAFB, 0.55f); S(mRAFB, 0.55f);
            S(mLADU, -0.15f); S(mRADU, -0.15f);
            S(mLFS, 0.35f); S(mRFS, 0.35f);
            S(mSpineFB, 0.05f);
        }

        void ApplyPose(float breathe)
        {
            var m = _pose.muscles;
            System.Array.Copy(_rest, m, m.Length);
            if (mChestFB >= 0) m[mChestFB] += breathe * 0.04f;
            if (mLShDU >= 0) m[mLShDU] += breathe * 0.05f;
            if (mRShDU >= 0) m[mRShDU] += breathe * 0.05f;
            float yawn = _yawnT >= 0f ? Mathf.Sin(Mathf.Clamp01(_yawnT / 2.2f) * Mathf.PI) : 0f;
            if (mNeckFB >= 0) m[mNeckFB] -= yawn * 0.5f;            // head back
            if (mChestFB >= 0) m[mChestFB] -= yawn * 0.15f;
            if (_angryT >= 0f && mLADU >= 0) { float a = Mathf.Sin(Mathf.Clamp01(_angryT / 1.6f) * Mathf.PI); m[mLADU] += a * 0.9f; m[mLFS] -= a * 0.5f; } // fist up
            _pose.muscles = m;
            _pose.bodyPosition = _bodyPos; _pose.bodyRotation = _bodyRot;
            _poser.SetHumanPose(ref _pose);
        }

        /// <summary>Put the pelvis on the cushion and keep the head under the roof (scales a tall character down a little).</summary>
        void FitToSeat()
        {
            var hips = _anim.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) return;
            if (_head != null)
            {
                // top of the head (head bone + ~0.22 m of skull / hair, hips 0.06 above the cushion) must stay under the
                // headliner — the builder's DriverSeat/Roof anchor; the cartoon cabins are low, so short drivers shrink a bit
                // (the cartoon cabins are only ~0.45–0.6 m tall above the cushion: sink the hips into the cushion first, then shrink)
                var roofT = _seat.Find("Roof");
                float room = roofT != null ? roofT.localPosition.y : 0.95f;
                float above = _seat.InverseTransformVector(_head.position - hips.position).y + 0.22f; // hips → top of the head at scale 1
                if (above + _hipY > room)
                {
                    _hipY = Mathf.Max(-0.14f, room - above * 0.85f);
                    float s = Mathf.Clamp((room - _hipY) / above, 0.5f, 1f);
                    _model.localScale = Vector3.one * s; ApplyPose(0f);
                }
            }
            var d = _seat.InverseTransformPoint(hips.position);
            _model.localPosition -= new Vector3(d.x, d.y - _hipY, d.z + 0.02f);
        }

        /// <summary>Spray hit near the driver (SprayVfx, local): annoyed for a moment.</summary>
        public void Splashed() { if (_angryT < 0f) _angryT = 0f; }

        void LateUpdate()
        {
            if (_poser == null) return;
            float dt = Time.deltaTime, t = Time.time + _seed;
            if (_yawnT < 0f && Time.time > _nextYawn) { _yawnT = 0f; _nextYawn = Time.time + Random.Range(14f, 26f); }
            if (_yawnT >= 0f) { _yawnT += dt; if (_yawnT > 2.2f) _yawnT = -1f; }
            if (_angryT >= 0f) { _angryT += dt; if (_angryT > 1.6f) _angryT = -1f; }
            ApplyPose(Mathf.Sin(t * 1.6f));           // ~15 breaths a minute
            HandsOnWheel();
            LookAround(dt, t);
            Blink(dt);
        }

        void HandsOnWheel()
        {
            if (_angryT >= 0f) { TwoBone(_rUp, _rLo, _rHand, _wheel.position + _wheel.right * 0.15f + _wheel.forward * 0.02f); return; } // left fist up
            TwoBone(_lUp, _lLo, _lHand, _wheel.position - _wheel.right * 0.15f + _wheel.up * 0.0f + _wheel.forward * 0.02f);
            TwoBone(_rUp, _rLo, _rHand, _wheel.position + _wheel.right * 0.15f + _wheel.forward * 0.02f);
        }

        /// <summary>Analytic two-bone IK in world space (keeps the elbow bending the way it already bends).</summary>
        static void TwoBone(Transform a, Transform b, Transform c, Vector3 target)
        {
            if (a == null || b == null || c == null) return;
            float la = Vector3.Distance(a.position, b.position), lb = Vector3.Distance(b.position, c.position);
            var toT = target - a.position;
            float d = Mathf.Clamp(toT.magnitude, 0.05f, (la + lb) * 0.999f);
            var bendDir = Vector3.ProjectOnPlane(b.position - a.position, toT.normalized);
            if (bendDir.sqrMagnitude < 1e-6f) bendDir = Vector3.down;
            bendDir.Normalize();
            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
            var elbow = a.position + toT.normalized * (cosA * la) + bendDir * (Mathf.Sqrt(1f - cosA * cosA) * la);
            a.rotation = Quaternion.FromToRotation(b.position - a.position, elbow - a.position) * a.rotation;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, target - b.position) * b.rotation;
        }

        void LookAround(float dt, float t)
        {
            if (_head == null) return;
            // nearest player within 9 m, else gaze ahead with a little drift
            Vector3 target = _head.position + _seat.forward * 4f + _seat.right * Mathf.Sin(t * 0.3f) * 1.2f;
            float best = 81f;
            foreach (var p in PlayerNet.All)
            {
                if (p == null) continue;
                var hp = p.transform.position + Vector3.up * 1.6f;
                float dd = (hp - _head.position).sqrMagnitude;
                if (dd < best) { best = dd; target = hp; }
            }
            var dir = target - _head.position;
            // clamp: ±75° left / right of straight ahead, ±30° up / down
            var local = _seat.InverseTransformDirection(dir.normalized);
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -75f, 75f);
            float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -30f, 30f);
            if (_angryT >= 0f) yaw += Mathf.Sin(_angryT * 22f) * 18f; // head shake
            var want = _seat.rotation * Quaternion.Euler(-pitch, yaw, 0f) * _headOff;
            _headCur = Quaternion.Slerp(_headCur, want, 1f - Mathf.Exp(-dt * 4f));
            _head.rotation = _headCur;
        }

        void Blink(float dt)
        {
            if (_blinkSmr == null) return;
            if (_blinkT < 0f && Time.time > _nextBlink) { _blinkT = 0f; _nextBlink = Time.time + Random.Range(2.5f, 6f); }
            float w = 0f;
            if (_blinkT >= 0f) { _blinkT += dt; w = Mathf.Sin(Mathf.Clamp01(_blinkT / 0.16f) * Mathf.PI) * 100f; if (_blinkT > 0.16f) _blinkT = -1f; }
            _blinkSmr.SetBlendShapeWeight(_blinkIdx, w);
        }
    }
}
