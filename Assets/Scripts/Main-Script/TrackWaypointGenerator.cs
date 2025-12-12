using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Events;
using System.Linq;

namespace WalkingTest
{
    [RequireComponent(typeof(LineRenderer))]
    public class TrackWaypointGenerator : MonoBehaviour
    {
        // ---------- ORIENTASI ----------
        public enum TrackOrientation { PlusX, MinusX, PlusZ, MinusZ }

        [Header("Referensi & Orientasi")]
        public Transform player;
        public TrackOrientation orientation = TrackOrientation.PlusX;
        public bool matchClockwiseToOrientation = true;

        [SerializeField] private Button prevBtn;
        [SerializeField] private Button nextBtn;
        [SerializeField] private UIToggleButtonsGroup _toggleRotation;

        // ---------- UKURAN TRACK ----------
        [Header("Ukuran (meter)")]
        public float straightLength = 10f;
        public float radius = 2f;
        public bool clockwise = true;

        // ---------- SAMPLING ----------
        [Header("Sampling / Kehalusan")]
        public int arcSegments = 32;
        public float straightStep = 0.25f;

        // ---------- KETINGGIAN TRACK ----------
        [Header("Penempatan")]
        public float trackY = 0f;

        // ---------- CONES ----------
        [Header("Cone Settings")]
        public GameObject conePrefab;
        public float coneOffsetFromTurn = 0.3f;
        public float coneY = 0.245f;

        [HideInInspector] public GameObject coneLeftInstance;
        [HideInInspector] public GameObject coneRightInstance;

        // ---------- COLLIDERS ----------
        [Header("Track Colliders")]
        public bool buildColliders = true;
        public float trackWidth = 1.0f;
        public float colliderThickness = 0.05f;
        public int trackLayer = 0;
        public Transform collidersParent;

        // ---------- GAMIFICATION ARENA ----------
        [Header("Gamification Arena")]
        [SerializeField] private List<PositionIdentity> _positionIdentity;
        [SerializeField] private Transform _gamificationArena;
        [Serializable]
        public struct PositionIdentity
        {
            public TrackOrientation targetOrientation;
            public bool isForClockWise;
            public Vector3 position;
            public float rotation;
        }

        // ---------- LAP COUNTER ----------
        [Header("Lap Counter - Checkpoint Settings")]
        public int checkpointCount = 4;
        private List<GameObject> checkpointColliders = new List<GameObject>();

        public int lapsCompleted;
        public bool lapCountingEnabled = false;

        [Space(8f)]
        public UnityEvent<int> onReachingLap;
        public UnityEvent onWrongWay;
        public UnityEvent onBackToCorrectWay;

        private GameObject collidersRoot;

        // ---------- LAP STATE ----------
        private int lastCheckpointPassed = -1;
        private bool isWrongWay = false;
        private bool lapStarted = false;
        private bool passedStartEarly = false;
        private bool passedMiddle = false;
        private bool passedEndLate = false;

        private int cpA, cpB, midCp, cpEndA, cpEndB, cpTotal;

        // ---------- INTERNAL ----------
        private LineRenderer lr;
        private readonly List<Vector3> pts = new List<Vector3>();

        private TrackOrientation[] tOrientationValues;
        private int tOrientationIndex;


        // =========================================================
        // ======================= AWAKE ============================
        // =========================================================

        void Awake()
        {
            lr = GetComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.alignment = LineAlignment.TransformZ;

            tOrientationValues = (TrackOrientation[])Enum.GetValues(typeof(TrackOrientation));
            tOrientationIndex = Array.IndexOf(tOrientationValues, orientation);
            if (tOrientationIndex < 0) tOrientationIndex = 0;

            if (prevBtn) prevBtn.onClick.AddListener(Prev);
            if (nextBtn) nextBtn.onClick.AddListener(Next);
            if (_toggleRotation) _toggleRotation.onToggleChanged.AddListener(UpdateRotation);
        }


        public void SpawnGamificationArena()
        {
            PositionIdentity offset = _positionIdentity.Find((x) => x.targetOrientation == orientation && x.isForClockWise == clockwise);
            Vector3 ORI = new Vector3(player.position.x, 0f, player.position.z);
            ORI += offset.position;

            _gamificationArena.gameObject.SetActive(true);
            _gamificationArena.position = ORI;
            _gamificationArena.rotation = Quaternion.Euler(0f, offset.rotation, 0f);
        }

        // =========================================================
        // ================== ORIENTATION API =======================
        // =========================================================

        void UpdateRotation(int val)
        {
            ToggleClockwiseChanged(val == 0);
        }

        public void Next()
        {
            // Rotasi searah jarum jam (CW)
            orientation = RotateCW(orientation);
            SetOrientation(orientation);
        }

        public void Prev()
        {
            // Rotasi berlawanan jarum jam (CCW)
            orientation = RotateCCW(orientation);
            SetOrientation(orientation);
        }

        private TrackOrientation RotateCW(TrackOrientation o)
        {
            return o switch
            {
                TrackOrientation.PlusX => TrackOrientation.MinusZ,
                TrackOrientation.MinusZ => TrackOrientation.MinusX,
                TrackOrientation.MinusX => TrackOrientation.PlusZ,
                TrackOrientation.PlusZ => TrackOrientation.PlusX,
                _ => o
            };
        }

        private TrackOrientation RotateCCW(TrackOrientation o)
        {
            return o switch
            {
                TrackOrientation.PlusX => TrackOrientation.PlusZ,
                TrackOrientation.PlusZ => TrackOrientation.MinusX,
                TrackOrientation.MinusX => TrackOrientation.MinusZ,
                TrackOrientation.MinusZ => TrackOrientation.PlusX,
                _ => o
            };
        }

        private void SetByIndex(int i)
        {
            tOrientationIndex = i;
            SetOrientation(tOrientationValues[tOrientationIndex]);
        }

        public void ToggleClockwiseChanged(bool isOn)
        {
            clockwise = isOn == false ? !DefaultClockwiseFor(orientation) : DefaultClockwiseFor(orientation);
            RegenerateAll();
        }

        public void SetOrientation(TrackOrientation o)
        {
            orientation = o;
            if (matchClockwiseToOrientation && _toggleRotation != null)
                clockwise = _toggleRotation.GetActiveIndex() == 1 ? !DefaultClockwiseFor(o) : DefaultClockwiseFor(o);

            RegenerateAll();
        }

        bool DefaultClockwiseFor(TrackOrientation o) =>
            o switch
            {
                TrackOrientation.PlusX => true,
                TrackOrientation.MinusX => false,
                TrackOrientation.PlusZ => false,
                TrackOrientation.MinusZ => true,
                _ => true
            };


        // =========================================================
        // ================= TRACK GENERATION =======================
        // =========================================================

        void ResolveAxes(out Vector3 RIGHT, out Vector3 FWD)
        {
            switch (orientation)
            {
                case TrackOrientation.PlusX: RIGHT = Vector3.right; FWD = Vector3.forward; break;
                case TrackOrientation.MinusX: RIGHT = Vector3.left; FWD = Vector3.forward; break;
                case TrackOrientation.PlusZ: RIGHT = Vector3.forward; FWD = Vector3.right; break;
                case TrackOrientation.MinusZ: RIGHT = Vector3.back; FWD = Vector3.right; break;
                default: RIGHT = Vector3.right; FWD = Vector3.forward; break;
            }
        }

        public void Generate()
        {
            if (!player)
            {
                Debug.LogWarning("Player belum di-assign ke TrackWaypointGenerator.");
                return;
            }

            pts.Clear();

            radius = Mathf.Max(0.01f, radius);
            straightLength = Mathf.Max(0.01f, straightLength);
            arcSegments = Mathf.Clamp(arcSegments, 8, 256);
            straightStep = Mathf.Max(0.05f, straightStep);

            ResolveAxes(out Vector3 RIGHT, out Vector3 FWD);
            Vector3 ORI = player.position;
            Vector3 P(float lx, float lz) => ORI + RIGHT * lx + FWD * lz + Vector3.up * trackY;

            float L = straightLength;
            float R = radius;

            float zBottom = 0f;
            float zTop = clockwise ? -2f * R : 2f * R;

            int sSteps = Mathf.CeilToInt(L / straightStep);

            // 1. Straight bottom
            for (int i = 0; i <= sSteps; i++)
                pts.Add(P(L * (i / (float)sSteps), zBottom));

            // 2. Arc right
            if (!clockwise)
            {
                Vector3 centerRight = P(L, R);
                for (int i = 1; i <= arcSegments; i++)
                {
                    float a = Mathf.Lerp(-Mathf.PI * .5f, Mathf.PI * .5f, i / (float)arcSegments);
                    pts.Add(centerRight + RIGHT * (R * Mathf.Cos(a)) + FWD * (R * Mathf.Sin(a)));
                }
            }
            else
            {
                Vector3 centerRight = P(L, -R);
                for (int i = 1; i <= arcSegments; i++)
                {
                    float a = Mathf.Lerp(Mathf.PI * .5f, -Mathf.PI * .5f, i / (float)arcSegments);
                    pts.Add(centerRight + RIGHT * (R * Mathf.Cos(a)) + FWD * (R * Mathf.Sin(a)));
                }
            }

            // 3. Straight top
            for (int i = 1; i <= sSteps; i++)
                pts.Add(P(L * (1f - i / (float)sSteps), zTop));

            // 4. Arc left
            if (!clockwise)
            {
                Vector3 centerLeft = P(0f, R);
                for (int i = 1; i <= arcSegments; i++)
                {
                    float a = Mathf.Lerp(Mathf.PI * .5f, -Mathf.PI * .5f, i / (float)arcSegments);
                    float lx = -R * Mathf.Cos(a);
                    float lz = R * Mathf.Sin(a);
                    pts.Add(centerLeft + RIGHT * lx + FWD * lz);
                }
            }
            else
            {
                Vector3 centerLeft = P(0f, -R);
                for (int i = 1; i <= arcSegments; i++)
                {
                    float a = Mathf.Lerp(-Mathf.PI * .5f, Mathf.PI * .5f, i / (float)arcSegments);
                    float lx = -R * Mathf.Cos(a);
                    float lz = R * Mathf.Sin(a);
                    pts.Add(centerLeft + RIGHT * lx + FWD * lz);
                }
            }

            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
        }


        // =========================================================
        // ===================== CONE SPAWN =========================
        // =========================================================

        public void SpawnCones()
        {
            if (!conePrefab) return;

            if (coneLeftInstance) Destroy(coneLeftInstance);
            if (coneRightInstance) Destroy(coneRightInstance);

            ResolveAxes(out Vector3 RIGHT, out Vector3 FWD);
            Vector3 ORI = player ? player.position : transform.position;

            float L = Mathf.Max(0.01f, straightLength);
            float R = Mathf.Max(0.01f, radius);

            float zBottom = 0f;
            float zTop = clockwise ? -2f * R : 2f * R;
            float zCenter = (zBottom + zTop) * .5f;

            float xLeft = Mathf.Clamp(coneOffsetFromTurn, 0f, L);
            float xRight = Mathf.Clamp(L - coneOffsetFromTurn, 0f, L);

            Vector3 P(float lx, float lz) => ORI + RIGHT * lx + FWD * lz;

            Vector3 leftPos = P(xLeft, zCenter);
            leftPos.y = coneY;

            Vector3 rightPos = P(xRight, zCenter);
            rightPos.y = coneY;

            coneLeftInstance = Instantiate(conePrefab, leftPos, Quaternion.identity);
            coneRightInstance = Instantiate(conePrefab, rightPos, Quaternion.identity);

            Quaternion look = Quaternion.LookRotation(RIGHT, Vector3.up);
            coneLeftInstance.transform.rotation = look;
            coneRightInstance.transform.rotation = look;
        }


        // =========================================================
        // ==================== COLLIDER BUILDER ====================
        // =========================================================

        void ClearTrackColliders()
        {
            if (collidersRoot)
            {
                if (Application.isPlaying) Destroy(collidersRoot);
                else DestroyImmediate(collidersRoot);

                collidersRoot = null;
            }
        }

        void BuildTrackColliders()
        {
            if (pts == null || pts.Count < 2) return;

            ClearTrackColliders();
            checkpointColliders.Clear();

            collidersRoot = new GameObject("TrackColliders");
            var parent = collidersParent ? collidersParent : transform;
            collidersRoot.transform.SetParent(parent, true);

            float width = Mathf.Max(0.05f, trackWidth);
            float thick = Mathf.Max(0.01f, colliderThickness);

            int count = pts.Count;
            for (int i = 0; i < count; i++)
            {
                Vector3 p0 = pts[i];
                Vector3 p1 = pts[(i + 1) % count];

                Vector3 dir = p1 - p0;
                dir.y = 0f;

                float segLen = dir.magnitude;
                if (segLen < 1e-3f) continue;
                dir /= segLen;

                Vector3 mid = (p0 + p1) * .5f;
                float centerY = pts[0].y + 0.2f;

                var go = new GameObject($"SegCol_{i:000}");
                go.layer = trackLayer;
                go.transform.SetParent(collidersRoot.transform, false);
                go.transform.position = new Vector3(mid.x, centerY, mid.z);
                go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(width, thick, segLen);
                box.isTrigger = true;

                checkpointCount = Mathf.Clamp(checkpointCount, 2, Mathf.Max(2, count));
                int step = Mathf.Max(1, count / checkpointCount);

                if (i % step == 0)
                {
                    var cp = go.AddComponent<TrackCheckpoint>();
                    cp.checkpointIndex = checkpointColliders.Count;
                    cp.generator = this;
                    checkpointColliders.Add(go);
                }
            }

            cpTotal = checkpointColliders.Count;

            cpA = 0;
            cpB = Mathf.Min(1, cpTotal - 1);

            midCp = cpTotal / 2;

            cpEndA = cpTotal - 2;
            cpEndB = cpTotal - 1;

            ResetLapState();
        }


        // =========================================================
        // =================== LAP SYSTEM LOGIC =====================
        // =========================================================

        public void OnCheckpointPassed(int index)
        {
            if (!lapCountingEnabled) return;

            // ====================================================
            // WRONG WAY + BACK TO CORRECT WAY (NEW)
            // ====================================================
            if (lastCheckpointPassed != -1)
            {
                // Wrong direction
                if (index < lastCheckpointPassed)
                {
                    if (!isWrongWay)
                    {
                        isWrongWay = true;
                        onWrongWay?.Invoke();
                    }
                }
                // Correct direction again
                else if (index > lastCheckpointPassed)
                {
                    if (isWrongWay)
                    {
                        isWrongWay = false;
                        onBackToCorrectWay?.Invoke();
                    }
                }
            }

            lastCheckpointPassed = index;

            // ====================================================
            // LAP LOGIC (UNCHANGED)
            // ====================================================

            if (!lapStarted)
            {
                if (index == cpA)
                {
                    lapStarted = true;

                    passedStartEarly = true;
                    passedMiddle = false;
                    passedEndLate = false;
                }
                return;
            }

            if (index == cpA || index == cpB)
                passedStartEarly = true;

            if (index == midCp)
                passedMiddle = true;

            if (index == cpEndA || index == cpEndB)
                passedEndLate = true;

            if (index == cpA)
            {
                if (passedStartEarly && passedMiddle && passedEndLate)
                {
                    lapsCompleted++;
                    onReachingLap?.Invoke(lapsCompleted);
                }

                passedStartEarly = true;
                passedMiddle = false;
                passedEndLate = false;
            }
        }

        public void ResetLapState()
        {
            lapsCompleted = 0;
            lapStarted = false;

            passedStartEarly = false;
            passedMiddle = false;
            passedEndLate = false;
        }

        public void EnableLapCounting(bool enabled)
        {
            lapCountingEnabled = enabled;
            if (!enabled) ResetLapState();
        }


        // =========================================================
        // ================== TRACK REGENERATE API ==================
        // =========================================================

        /// <summary>
        /// Regenerate isi track (line, cones, colliders) tanpa mengubah on/off line renderer.
        /// Dipakai ketika orientasi / clockwise diubah.
        /// </summary>
        public void RegenerateAll()
        {
            // simpan visibility saat ini
            bool wasVisible = lr.enabled;

            // update jalur & geometry
            Generate();

            if (wasVisible)
            {
                // jika sedang terlihat, rebuild semuanya
                SpawnCones();

                if (buildColliders)
                    BuildTrackColliders();
                else
                    ClearTrackColliders();
            }
            else
            {
                // kalau lagi disembunyikan, pastikan cones & colliders tidak aktif
                if (coneLeftInstance) coneLeftInstance.SetActive(false);
                if (coneRightInstance) coneRightInstance.SetActive(false);
                ClearTrackColliders();
            }

            ResetLapState();
        }


        // =========================================================
        // ================= TRACK VISIBILITY API ===================
        // =========================================================

        public void ShowTrack()
        {
            lr.enabled = true;
            RegenerateAll();

            Debug.Log("TRACK SHOWN");
        }

        public void HideTrack()
        {
            lr.enabled = false;

            if (coneLeftInstance) coneLeftInstance.SetActive(false);
            if (coneRightInstance) coneRightInstance.SetActive(false);

            ClearTrackColliders();
            ResetLapState();

            Debug.Log("TRACK HIDDEN");
        }

        public void ToggleTrack(bool show)
        {
            if (show) ShowTrack();
            else HideTrack();
        }
    }
}
