using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// MRUK (Meta MR Utility Kit)
using Meta.XR.MRUtilityKit;

namespace WalkingTest
{
    [RequireComponent(typeof(LineRenderer))]
    public class TrackWaypointGenerator_MetaMR : MonoBehaviour
    {
        // ---------- ORIENTASI ----------
        public enum TrackOrientation { PlusX, MinusX, PlusZ, MinusZ }

        [Header("Referensi & Orientasi")]
        [SerializeField] private Transform player; // optional, kalau kosong bisa pakai OVRPlugin Head
        [SerializeField] private TrackOrientation orientation = TrackOrientation.PlusX;
        [SerializeField] private bool matchClockwiseToOrientation = true;

        [SerializeField] private Button prevBtn;
        [SerializeField] private Button nextBtn;
        [SerializeField] private UIToggleButtonsGroup _toggleRotation;

        // ---------- UKURAN TRACK ----------
        [Header("Ukuran (meter)")]
        [SerializeField] private float straightLength = 10f;
        [SerializeField] private float radius = 2f;
        [SerializeField] private bool clockwise = true;

        // ---------- SAMPLING ----------
        [Header("Sampling / Kehalusan")]
        [SerializeField] private int arcSegments = 32;
        [SerializeField] private float straightStep = 0.25f;

        // ---------- KETINGGIAN TRACK (fallback jika MRUK off) ----------
        [Header("Penempatan (Fallback tanpa MRUK)")]
        [SerializeField] private float trackY = 0f;

        // ---------- CONES ----------
        [Header("Cone Settings")]
        [SerializeField] private GameObject conePrefab;
        [SerializeField] private float coneOffsetFromTurn = 0.3f;
        [SerializeField] private float coneY = 0.245f; // fallback jika MRUK off

        [HideInInspector] public GameObject coneLeftInstance;
        [HideInInspector] public GameObject coneRightInstance;

        // ---------- ARROW (LineRenderer) ----------
        [Header("Arrow (LineRenderer)")]
        [Tooltip("Jika ON, gambar panah arah jalan pada awal track menggunakan LineRenderer (dibuat otomatis dari lr).")]
        [SerializeField] private bool showArrow = true;

        [Tooltip("Offset panah dari titik start (meter) sepanjang arah track).")]
        [SerializeField] private float arrowOffsetAlongStart = 0.5f;

        [Tooltip("Panjang batang panah (meter).")]
        [SerializeField] private float arrowLength = 0.8f;

        [Tooltip("Panjang kepala panah (meter).")]
        [SerializeField] private float arrowHeadLength = 0.25f;

        [Tooltip("Sudut kepala panah (derajat).")]
        [SerializeField] private float arrowHeadAngleDeg = 25f;

        [Tooltip("Offset panah di atas lantai (meter) untuk menghindari z-fighting).")]
        [SerializeField] private float arrowHeightOnFloor = 0.01f;

        // ---------- COLLIDERS ----------
        [Header("Track Colliders")]
        [SerializeField] private bool buildColliders = true;
        [SerializeField] private float trackWidth = 1.0f;
        [SerializeField] private float colliderThickness = 0.05f;
        [SerializeField] private int trackLayer = 0;
        [SerializeField] private Transform collidersParent;

        // ---------- GAMIFICATION ARENA ----------
        [Header("Gamification Arena")]
        [SerializeField] private List<PositionIdentity> _positionIdentity;
        [SerializeField] private Transform _gamificationArena;

        [Serializable]
        public struct PositionIdentity
        {
            public TrackOrientation targetOrientation;
            public bool isForClockWise;
            public Vector3 position; // offset local (x=RIGHT, z=FWD, y=UP/normal)
            public float rotation;   // yaw degrees (around up/normal)
        }

        // ---------- LAP COUNTER ----------
        [Header("Lap Counter - Checkpoint Settings")]
        [SerializeField] private int checkpointCount = 4;
        private readonly List<GameObject> checkpointColliders = new List<GameObject>();

        public int lapsCompleted;
        [SerializeField] private bool lapCountingEnabled = false;

        [Space(8f)]
        public UnityEvent<int> onReachingLap;
        [SerializeField] private UnityEvent onWrongWay;
        [SerializeField] private UnityEvent onBackToCorrectWay;

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
        private LineRenderer lr;          // main track line
        private LineRenderer arrowLR;     // arrow line (auto created)
        private readonly List<Vector3> pts = new List<Vector3>();

        // ===== Meta Quest / MR integration =====
        [Header("Meta Quest MR Integration")]
        [Tooltip("Jika ON dan player null, ORI memakai OVRPlugin Head Pose (roomscale).")]
        [SerializeField] private bool useOVRPluginHeadAsPlayer = true;

        [Tooltip("Tracking origin untuk convert pose ke world. Isi OVRCameraRig/TrackingSpace.")]
        [SerializeField] private Transform trackingOrigin;

        [Tooltip("Jika ON, titik & arena akan di-snap ke FLOOR dari MRUK (Space Setup).")]
        [SerializeField] private bool snapToMRUKFloor = true;

        [Tooltip("Offset track di atas lantai (meter) saat snap aktif.")]
        [SerializeField] private float trackOffsetOnFloor = 0.0f;

        [Tooltip("Offset cone di atas lantai (meter) saat snap aktif.")]
        [SerializeField] private float coneOffsetOnFloor = 0.245f;

        [Tooltip("Offset arena di atas lantai (meter) saat snap aktif.")]
        [SerializeField] private float arenaOffsetOnFloor = 0.0f;

        [Tooltip("Log warning jika MRUK floor tidak ada.")]
        [SerializeField] private bool logIfNoFloor = true;

        private MRUKAnchor _floorAnchor;
        private bool _hasFloor = false;

        // cache normal untuk orientasi flat
        private Vector3 _lastPlaneNormal = Vector3.up;

        // =========================================================
        // ======================= UNITY ============================
        // =========================================================

        private void Awake()
        {
            lr = GetComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;

            // agar line terlihat “flat” mengikuti dunia
            lr.alignment = LineAlignment.TransformZ;

            EnsureArrowLineRenderer();

            if (prevBtn) prevBtn.onClick.AddListener(Prev);
            if (nextBtn) nextBtn.onClick.AddListener(Next);
            if (_toggleRotation) _toggleRotation.onToggleChanged.AddListener(UpdateRotation);

            // MRUK scene load hook
            if (snapToMRUKFloor && MRUK.Instance != null)
                MRUK.Instance.SceneLoadedEvent.AddListener(OnMRUKSceneLoaded);
        }

        private void Start()
        {
            // kalau MRUK dipakai, boleh cache floor dulu (aman walau belum load)
            if (snapToMRUKFloor) CacheFloorAnchor();

            // ini yang akan generate track + cones + arrow line + collider
            SpawnGamificationArena();
        }

        private void OnDestroy()
        {
            if (MRUK.Instance != null)
                MRUK.Instance.SceneLoadedEvent.RemoveListener(OnMRUKSceneLoaded);
        }

        private void OnMRUKSceneLoaded()
        {
            CacheFloorAnchor();
            // kalau track sudah ada, update arrow supaya ikut normal terbaru
            UpdateArrowLine();
        }

        // =========================================================
        // ================== PUBLIC API ============================
        // =========================================================

        public void SpawnGamificationArena()
        {
            if (!player && !useOVRPluginHeadAsPlayer)
            {
                Debug.LogWarning("[Trackway] Player belum di-assign dan useOVRPluginHeadAsPlayer OFF.");
                return;
            }

            if (snapToMRUKFloor) CacheFloorAnchor();

            PositionIdentity offset = _positionIdentity.Find(x => x.targetOrientation == orientation && x.isForClockWise == clockwise);

            ResolveAxes(out Vector3 RIGHT, out Vector3 FWD);

            Vector3 ori = GetPlayerWorldPosition();
            Vector3 n;
            ori = SnapPointToFloor(ori, out n);
            _lastPlaneNormal = n;

            Vector3 RIGHTp = SafeProjectOnPlane(RIGHT, n);
            Vector3 FWDp = SafeProjectOnPlane(FWD, n);

            Vector3 arenaPos =
                ori +
                RIGHTp * offset.position.x +
                FWDp * offset.position.z +
                n * (offset.position.y + arenaOffsetOnFloor);

            if (_gamificationArena != null)
            {
                _gamificationArena.gameObject.SetActive(true);
                _gamificationArena.position = arenaPos;
                _gamificationArena.rotation = Quaternion.AngleAxis(offset.rotation, n);
            }

            RegenerateAll();
        }

        public void HideTrack()
        {
            lr.enabled = false;
            if (arrowLR != null) arrowLR.enabled = false;

            if (coneLeftInstance) coneLeftInstance.SetActive(false);
            if (coneRightInstance) coneRightInstance.SetActive(false);

            ClearTrackColliders();
            ResetLapState();
        }

        // =========================================================
        // ================== UI / ROTATION =========================
        // =========================================================

        private void UpdateRotation(int val)
        {
            ToggleClockwiseChanged(val == 0);
        }

        private void Next()
        {
            orientation = RotateCW(orientation);
            SetOrientation(orientation);
        }

        private void Prev()
        {
            orientation = RotateCCW(orientation);
            SetOrientation(orientation);
        }

        private TrackOrientation RotateCW(TrackOrientation o) =>
            o switch
            {
                TrackOrientation.PlusX => TrackOrientation.MinusZ,
                TrackOrientation.MinusZ => TrackOrientation.MinusX,
                TrackOrientation.MinusX => TrackOrientation.PlusZ,
                TrackOrientation.PlusZ => TrackOrientation.PlusX,
                _ => o
            };

        private TrackOrientation RotateCCW(TrackOrientation o) =>
            o switch
            {
                TrackOrientation.PlusX => TrackOrientation.PlusZ,
                TrackOrientation.PlusZ => TrackOrientation.MinusX,
                TrackOrientation.MinusX => TrackOrientation.MinusZ,
                TrackOrientation.MinusZ => TrackOrientation.PlusX,
                _ => o
            };

        private void ToggleClockwiseChanged(bool isOn)
        {
            clockwise = isOn == false ? !DefaultClockwiseFor(orientation) : DefaultClockwiseFor(orientation);
            RegenerateAll();
        }

        private void SetOrientation(TrackOrientation o)
        {
            orientation = o;

            if (matchClockwiseToOrientation && _toggleRotation != null)
                clockwise = _toggleRotation.GetActiveIndex() == 1 ? !DefaultClockwiseFor(o) : DefaultClockwiseFor(o);

            RegenerateAll();
        }

        private bool DefaultClockwiseFor(TrackOrientation o) =>
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

        private void ResolveAxes(out Vector3 RIGHT, out Vector3 FWD)
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

        private void Generate()
        {
            if (!player && !useOVRPluginHeadAsPlayer)
            {
                Debug.LogWarning("[Trackway] Player belum di-assign.");
                return;
            }

            pts.Clear();

            radius = Mathf.Max(0.01f, radius);
            straightLength = Mathf.Max(0.01f, straightLength);
            arcSegments = Mathf.Clamp(arcSegments, 8, 256);
            straightStep = Mathf.Max(0.05f, straightStep);

            ResolveAxes(out Vector3 RIGHT, out Vector3 FWD);

            Vector3 ori = GetPlayerWorldPosition();
            Vector3 n;
            ori = SnapPointToFloor(ori, out n);
            _lastPlaneNormal = n;

            Vector3 RIGHTp = SafeProjectOnPlane(RIGHT, n);
            Vector3 FWDp = SafeProjectOnPlane(FWD, n);

            float yOffset = (snapToMRUKFloor && _hasFloor) ? trackOffsetOnFloor : trackY;

            Vector3 P(float lx, float lz) => ori + RIGHTp * lx + FWDp * lz + n * yOffset;

            float L = straightLength;
            float R = radius;

            float zBottom = 0f;
            float zTop = clockwise ? -2f * R : 2f * R;

            int sSteps = Mathf.CeilToInt(L / straightStep);

            // 1) Straight bottom
            for (int i = 0; i <= sSteps; i++)
                pts.Add(P(L * (i / (float)sSteps), zBottom));

            // 2) Arc right
            if (!clockwise)
            {
                Vector3 centerRight = P(L, R);
                for (int i = 1; i <= arcSegments; i++)
                {
                    float a = Mathf.Lerp(-Mathf.PI * .5f, Mathf.PI * .5f, i / (float)arcSegments);
                    pts.Add(centerRight + RIGHTp * (R * Mathf.Cos(a)) + FWDp * (R * Mathf.Sin(a)));
                }
            }
            else
            {
                Vector3 centerRight = P(L, -R);
                for (int i = 1; i <= arcSegments; i++)
                {
                    float a = Mathf.Lerp(Mathf.PI * .5f, -Mathf.PI * .5f, i / (float)arcSegments);
                    pts.Add(centerRight + RIGHTp * (R * Mathf.Cos(a)) + FWDp * (R * Mathf.Sin(a)));
                }
            }

            // 3) Straight top
            for (int i = 1; i <= sSteps; i++)
                pts.Add(P(L * (1f - i / (float)sSteps), zTop));

            // 4) Arc left
            if (!clockwise)
            {
                Vector3 centerLeft = P(0f, R);
                for (int i = 1; i <= arcSegments; i++)
                {
                    float a = Mathf.Lerp(Mathf.PI * .5f, -Mathf.PI * .5f, i / (float)arcSegments);
                    float lx = -R * Mathf.Cos(a);
                    float lz = R * Mathf.Sin(a);
                    pts.Add(centerLeft + RIGHTp * lx + FWDp * lz);
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
                    pts.Add(centerLeft + RIGHTp * lx + FWDp * lz);
                }
            }

            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());

            // update arrow setelah track selesai
            UpdateArrowLine();
        }

        // =========================================================
        // ==================== ARROW LINE ==========================
        // =========================================================

        private void EnsureArrowLineRenderer()
        {
            if (arrowLR != null) return;

            // buat child object untuk arrow line
            var arrowGO = transform.Find("ArrowLineRenderer");
            if (arrowGO == null)
            {
                arrowGO = new GameObject("ArrowLineRenderer").transform;
                arrowGO.SetParent(transform, false);
            }

            arrowLR = arrowGO.GetComponent<LineRenderer>();
            if (arrowLR == null) arrowLR = arrowGO.gameObject.AddComponent<LineRenderer>();

            // Copy style dari lr (property lr)
            CopyLineRendererStyle(lr, arrowLR);
            arrowLR.loop = false;
            arrowLR.enabled = false;
        }

        private void CopyLineRendererStyle(LineRenderer src, LineRenderer dst)
        {
            if (src == null || dst == null) return;

            dst.sharedMaterial = src.sharedMaterial;

            dst.widthMultiplier = src.widthMultiplier;
            dst.startWidth = src.startWidth;
            dst.endWidth = src.endWidth;

            dst.numCapVertices = src.numCapVertices;
            dst.numCornerVertices = src.numCornerVertices;

            dst.textureMode = src.textureMode;
            dst.alignment = src.alignment;
            dst.useWorldSpace = true;

            dst.shadowCastingMode = src.shadowCastingMode;
            dst.receiveShadows = src.receiveShadows;
            dst.motionVectorGenerationMode = src.motionVectorGenerationMode;
            dst.generateLightingData = src.generateLightingData;

            dst.colorGradient = src.colorGradient;
        }

        private void UpdateArrowLine()
        {
            EnsureArrowLineRenderer();

            if (!showArrow || arrowLR == null || lr == null || !lr.enabled)
            {
                if (arrowLR != null) arrowLR.enabled = false;
                return;
            }

            if (pts == null || pts.Count < 2)
            {
                arrowLR.enabled = false;
                return;
            }

            Vector3 n = (_lastPlaneNormal.sqrMagnitude > 0.0001f) ? _lastPlaneNormal.normalized : Vector3.up;

            // arah travel di awal track
            Vector3 p0 = pts[0];
            Vector3 p1 = pts[1];

            Vector3 dir = Vector3.ProjectOnPlane(p1 - p0, n);
            if (dir.sqrMagnitude < 0.0001f)
            {
                arrowLR.enabled = false;
                return;
            }
            dir.Normalize();

            Vector3 basePos = p0 + dir * arrowOffsetAlongStart + n * arrowHeightOnFloor;
            Vector3 tipPos = basePos + dir * Mathf.Max(0.05f, arrowLength);

            float headLen = Mathf.Min(Mathf.Max(0.01f, arrowHeadLength), (tipPos - basePos).magnitude * 0.9f);
            float ang = arrowHeadAngleDeg;

            // head kiri/kanan: putar dir di sekitar normal lantai
            Quaternion leftRot = Quaternion.AngleAxis(180f - ang, n);
            Quaternion rightRot = Quaternion.AngleAxis(180f + ang, n);

            Vector3 leftDir = (leftRot * dir).normalized;
            Vector3 rightDir = (rightRot * dir).normalized;

            Vector3 leftHead = tipPos + leftDir * headLen;
            Vector3 rightHead = tipPos + rightDir * headLen;

            // polyline: base -> tip -> leftHead -> tip -> rightHead
            arrowLR.enabled = true;
            arrowLR.positionCount = 5;
            arrowLR.SetPosition(0, basePos);
            arrowLR.SetPosition(1, tipPos);
            arrowLR.SetPosition(2, leftHead);
            arrowLR.SetPosition(3, tipPos);
            arrowLR.SetPosition(4, rightHead);
        }

        // =========================================================
        // ===================== CONE SPAWN =========================
        // =========================================================

        private void SpawnCones()
        {
            if (!conePrefab) return;

            if (coneLeftInstance) Destroy(coneLeftInstance);
            if (coneRightInstance) Destroy(coneRightInstance);

            ResolveAxes(out Vector3 RIGHT, out Vector3 FWD);

            Vector3 ori = GetPlayerWorldPosition();
            Vector3 n;
            ori = SnapPointToFloor(ori, out n);
            _lastPlaneNormal = n;

            Vector3 RIGHTp = SafeProjectOnPlane(RIGHT, n);
            Vector3 FWDp = SafeProjectOnPlane(FWD, n);

            float L = Mathf.Max(0.01f, straightLength);
            float R = Mathf.Max(0.01f, radius);

            float zBottom = 0f;
            float zTop = clockwise ? -2f * R : 2f * R;
            float zCenter = (zBottom + zTop) * .5f;

            float xLeft = Mathf.Clamp(coneOffsetFromTurn, 0f, L);
            float xRight = Mathf.Clamp(L - coneOffsetFromTurn, 0f, L);

            Vector3 P(float lx, float lz) => ori + RIGHTp * lx + FWDp * lz;

            Vector3 leftPos = P(xLeft, zCenter);
            Vector3 rightPos = P(xRight, zCenter);

            float coneHeight = (snapToMRUKFloor && _hasFloor) ? coneOffsetOnFloor : coneY;
            leftPos += n * coneHeight;
            rightPos += n * coneHeight;

            coneLeftInstance = Instantiate(conePrefab, leftPos, Quaternion.identity);
            coneRightInstance = Instantiate(conePrefab, rightPos, Quaternion.identity);

            // upright mengikuti normal lantai
            Quaternion look = Quaternion.LookRotation(RIGHTp, n);
            coneLeftInstance.transform.rotation = look;
            coneRightInstance.transform.rotation = look;
        }

        // =========================================================
        // ==================== COLLIDER BUILDER ====================
        // =========================================================

        private void ClearTrackColliders()
        {
            if (collidersRoot)
            {
                if (Application.isPlaying) Destroy(collidersRoot);
                else DestroyImmediate(collidersRoot);

                collidersRoot = null;
            }
        }

        private void BuildTrackColliders()
        {
            if (pts == null || pts.Count < 2) return;

            ClearTrackColliders();
            checkpointColliders.Clear();

            collidersRoot = new GameObject("TrackColliders");
            var parent = collidersParent ? collidersParent : transform;
            collidersRoot.transform.SetParent(parent, true);

            float width = Mathf.Max(0.05f, trackWidth);
            float thick = Mathf.Max(0.01f, colliderThickness);

            Vector3 up = (_lastPlaneNormal.sqrMagnitude > 0.0001f) ? _lastPlaneNormal.normalized : Vector3.up;

            int count = pts.Count;
            checkpointCount = Mathf.Clamp(checkpointCount, 2, Mathf.Max(2, count));

            int step = Mathf.Max(1, count / checkpointCount);

            for (int i = 0; i < count; i++)
            {
                Vector3 p0 = pts[i];
                Vector3 p1 = pts[(i + 1) % count];

                Vector3 dir = Vector3.ProjectOnPlane(p1 - p0, up);
                float segLen = dir.magnitude;
                if (segLen < 1e-3f) continue;
                dir /= segLen;

                Vector3 mid = (p0 + p1) * 0.5f;

                var go = new GameObject($"SegCol_{i:000}");
                go.layer = trackLayer;
                go.transform.SetParent(collidersRoot.transform, false);

                // sedikit angkat collider dari permukaan
                go.transform.position = mid + up * 0.02f;
                go.transform.rotation = Quaternion.LookRotation(dir, up);

                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(width, thick, segLen);
                box.isTrigger = true;

                if (i % step == 0)
                {
                    var cp = go.AddComponent<TrackCheckpoint>();
                    cp.checkpointIndex = checkpointColliders.Count;
                    //! cp.generator = this;
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

            // WRONG WAY + BACK TO CORRECT WAY
            if (lastCheckpointPassed != -1)
            {
                if (index < lastCheckpointPassed)
                {
                    if (!isWrongWay)
                    {
                        isWrongWay = true;
                        onWrongWay?.Invoke();
                    }
                }
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

            // LAP LOGIC
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

            if (index == cpA || index == cpB) passedStartEarly = true;
            if (index == midCp) passedMiddle = true;
            if (index == cpEndA || index == cpEndB) passedEndLate = true;

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

        private void ResetLapState()
        {
            lapsCompleted = 0;
            lapStarted = false;

            passedStartEarly = false;
            passedMiddle = false;
            passedEndLate = false;

            lastCheckpointPassed = -1;
            isWrongWay = false;
        }

        // =========================================================
        // ================== TRACK REGENERATE API ==================
        // =========================================================

        private void RegenerateAll()
        {
            bool wasVisible = lr.enabled;

            Generate();

            if (wasVisible)
            {
                SpawnCones();

                if (buildColliders)
                    BuildTrackColliders();
                else
                    ClearTrackColliders();
            }
            else
            {
                if (coneLeftInstance) coneLeftInstance.SetActive(false);
                if (coneRightInstance) coneRightInstance.SetActive(false);

                ClearTrackColliders();
            }

            ResetLapState();
        }

        // =========================================================
        // ================= META / MRUK HELPERS ====================
        // =========================================================

        private void CacheFloorAnchor()
        {
            _hasFloor = false;
            _floorAnchor = null;

            if (!snapToMRUKFloor || MRUK.Instance == null)
                return;

            var room = MRUK.Instance.GetCurrentRoom();
            if (room == null) return;

            foreach (var a in room.Anchors)
            {
                if (a == null) continue;
                if (a.HasAnyLabel(MRUKAnchor.SceneLabels.FLOOR))
                {
                    _floorAnchor = a;
                    _hasFloor = true;
                    break;
                }
            }

            if (!_hasFloor && logIfNoFloor)
            {
                Debug.LogWarning("[Trackway] FLOOR anchor tidak ditemukan. Pastikan Space Setup/Scene API tersedia di headset.");
            }
        }

        private Vector3 SnapPointToFloor(Vector3 desiredWorldPos, out Vector3 floorNormal)
        {
            floorNormal = Vector3.up;

            if (!snapToMRUKFloor || !_hasFloor || _floorAnchor == null)
            {
                // fallback: pakai world y
                desiredWorldPos.y = trackY;
                return desiredWorldPos;
            }

            _floorAnchor.GetClosestSurfacePosition(desiredWorldPos, out Vector3 closest, out Vector3 n);
            if (n.sqrMagnitude > 0.0001f) floorNormal = n.normalized;

            return closest;
        }

        private static Vector3 SafeProjectOnPlane(Vector3 v, Vector3 planeNormal)
        {
            Vector3 p = Vector3.ProjectOnPlane(v, planeNormal);
            if (p.sqrMagnitude < 0.0001f) return v.normalized;
            return p.normalized;
        }

        // =========================================================
        // ================= META / OVRPLUGIN HELPERS ===============
        // =========================================================

        private Vector3 GetPlayerWorldPosition()
        {
            if (player != null) return player.position;

            if (!useOVRPluginHeadAsPlayer) return transform.position;

            if (!OVRPlugin.initialized)
                return transform.position;

            var state = OVRPlugin.GetNodePoseStateImmediate(OVRPlugin.Node.Head);

            // flip Z (OVRPlugin -> Unity)
            Vector3 trackingPos = new Vector3(state.Pose.Position.x, state.Pose.Position.y, -state.Pose.Position.z);

            // convert tracking -> world
            if (trackingOrigin != null)
                return trackingOrigin.TransformPoint(trackingPos);

            // fallback (tracking space as world)
            return trackingPos;
        }
    }
}
