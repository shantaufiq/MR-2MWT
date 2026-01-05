using Meta.XR.Util;
using UnityEngine;

namespace Meta.XR.MRUtilityKit
{
    public class FloorAnchorSpawner : MonoBehaviour
    {
        public enum OffsetSpace
        {
            AnchorLocal,
            World
        }

        [Header("When to spawn")]
        public MRUK.RoomFilter SpawnOnSceneLoaded = MRUK.RoomFilter.CurrentRoomOnly;

        [Header("What to spawn")]
        public GameObject FloorPrefabOrObject;
        public bool ParentToFloorAnchor = true;

        [Header("Placement")]
        public OffsetSpace PositionOffsetSpace = OffsetSpace.AnchorLocal;
        public Vector3 PositionOffset = Vector3.zero;
        public Vector3 RotationOffsetEuler = Vector3.zero;
        public float NormalOffsetMeters = 0f;

        [Header("Optional: Fit To Floor")]
        public bool FitToFloorPlaneRect = false;
        public Vector2 PrefabSizeXYMeters = new Vector2(1f, 1f);

        private GameObject _spawnedInstance;
        private Transform _cachedFloorAnchor;
        private MRUKRoom _cachedRoom;

        #region Unity Lifecycle
        private void Start()
        {
            if (!MRUK.Instance || SpawnOnSceneLoaded == MRUK.RoomFilter.None)
                return;

            MRUK.Instance.RegisterSceneLoadedCallback(() =>
            {
                switch (SpawnOnSceneLoaded)
                {
                    case MRUK.RoomFilter.AllRooms:
                        foreach (var room in MRUK.Instance.Rooms)
                            SpawnOrMoveOnRoomFloor(room);
                        break;

                    case MRUK.RoomFilter.CurrentRoomOnly:
                        SpawnOrMoveOnRoomFloor(MRUK.Instance.GetCurrentRoom());
                        break;
                }
            });
        }
        #endregion

        #region Public API

        [ContextMenu("Spawn Floor Now (Current Room)")]
        public void SpawnNowCurrentRoom()
        {
            if (!MRUK.Instance) return;
            SpawnOrMoveOnRoomFloor(MRUK.Instance.GetCurrentRoom());
        }

        /// <summary>
        /// === FUNGSI UTAMA YANG ANDA MINTA ===
        /// Mengatur agar lantai di-spawn tepat di posisi player saat ini (di lantai).
        /// Biasanya dipanggil sebelum SpawnNowCurrentRoom().
        /// </summary>
        public void SetPlacementFromPlayerPosition(
            Transform playerWorldTransform,
            bool applyImmediately = true)
        {
            if (!playerWorldTransform)
            {
                Debug.LogWarning("[FloorAnchorSpawner] Player transform null.");
                return;
            }

            CacheFloorAnchorIfNeeded();
            if (_cachedFloorAnchor == null) return;

            Transform floorAnchor = _cachedFloorAnchor;

            // Ambil posisi player (CenterEye)
            Vector3 playerWorldPos = playerWorldTransform.position;

            // Proyeksikan ke lantai
            Vector3 projectedWorldPos = new Vector3(
                playerWorldPos.x,
                0f,
                floorAnchor.position.z
            );

            // Simpan sebagai offset lokal terhadap FloorAnchor
            PositionOffsetSpace = OffsetSpace.AnchorLocal;
            PositionOffset = floorAnchor.InverseTransformPoint(projectedWorldPos);

            // Reset rotasi tambahan
            RotationOffsetEuler = Vector3.zero;

            if (!applyImmediately)
                return;

            var target = GetCurrentTargetTransform();
            if (target != null)
                ApplyPlacement(target, floorAnchor);
        }

        /// <summary>
        /// Memutar lantai di sekitar normal lantai (yaw).
        /// </summary>
        public void RotateFloorAroundNormal(float deltaDegrees, bool applyImmediately = true)
        {
            CacheFloorAnchorIfNeeded();
            if (_cachedFloorAnchor == null) return;

            RotationOffsetEuler.z = Mathf.Repeat(RotationOffsetEuler.z + deltaDegrees, 360f);

            if (!applyImmediately) return;

            var target = GetCurrentTargetTransform();
            if (target != null)
                ApplyPlacement(target, _cachedFloorAnchor);
        }

        #endregion

        #region Core Spawn Logic

        public void SpawnOrMoveOnRoomFloor(MRUKRoom room)
        {
            if (!room || !room.FloorAnchor)
            {
                Debug.LogWarning("[FloorAnchorSpawner] Room or FloorAnchor invalid.");
                return;
            }

            _cachedRoom = room;
            _cachedFloorAnchor = room.FloorAnchor.transform;

            if (!FloorPrefabOrObject)
            {
                Debug.LogWarning("[FloorAnchorSpawner] FloorPrefabOrObject not assigned.");
                return;
            }

            GameObject targetGO;

            bool isSceneObject = FloorPrefabOrObject.scene.IsValid();
            if (isSceneObject)
            {
                targetGO = FloorPrefabOrObject;
            }
            else
            {
                if (_spawnedInstance == null)
                {
                    _spawnedInstance = Instantiate(FloorPrefabOrObject);
                    _spawnedInstance.name = $"{FloorPrefabOrObject.name} (SpawnedOnFloor)";
                }
                targetGO = _spawnedInstance;
            }

            ApplyPlacement(targetGO.transform, _cachedFloorAnchor);

            if (FitToFloorPlaneRect)
                TryFitScaleToPlaneRect(targetGO.transform, _cachedFloorAnchor);
        }

        #endregion

        #region Placement Helpers

        private void ApplyPlacement(Transform target, Transform floorAnchor)
        {
            Quaternion rotOffset = Quaternion.Euler(RotationOffsetEuler);

            if (ParentToFloorAnchor)
            {
                target.SetParent(floorAnchor, false);

                Vector3 localOffset = PositionOffset;
                if (PositionOffsetSpace == OffsetSpace.World)
                    localOffset = Quaternion.Inverse(floorAnchor.rotation) * PositionOffset;

                localOffset += Vector3.forward * NormalOffsetMeters;

                target.localPosition = localOffset;
                target.localRotation = rotOffset;
            }
            else
            {
                Vector3 basePos = floorAnchor.position;
                Quaternion baseRot = floorAnchor.rotation;

                Vector3 worldOffset =
                    (PositionOffsetSpace == OffsetSpace.AnchorLocal)
                        ? baseRot * PositionOffset
                        : PositionOffset;

                worldOffset += baseRot * Vector3.forward * NormalOffsetMeters;

                target.position = basePos + worldOffset;
                target.rotation = baseRot * rotOffset;
            }
        }

        private void TryFitScaleToPlaneRect(Transform target, Transform floorAnchor)
        {
            var mrukAnchor = floorAnchor.GetComponent<MRUKAnchor>();
            if (!mrukAnchor || !mrukAnchor.PlaneRect.HasValue) return;

            var rect = mrukAnchor.PlaneRect.Value;

            float sx = rect.size.x / Mathf.Max(0.0001f, PrefabSizeXYMeters.x);
            float sy = rect.size.y / Mathf.Max(0.0001f, PrefabSizeXYMeters.y);

            target.localScale = new Vector3(sx, sy, target.localScale.z);
        }

        private Transform GetCurrentTargetTransform()
        {
            if (!FloorPrefabOrObject) return null;
            if (FloorPrefabOrObject.scene.IsValid()) return FloorPrefabOrObject.transform;
            if (_spawnedInstance != null) return _spawnedInstance.transform;
            return null;
        }

        private void CacheFloorAnchorIfNeeded()
        {
            if (_cachedFloorAnchor != null) return;
            if (!MRUK.Instance) return;

            var room = MRUK.Instance.GetCurrentRoom();
            if (room && room.FloorAnchor)
            {
                _cachedRoom = room;
                _cachedFloorAnchor = room.FloorAnchor.transform;
            }
        }

        #endregion
    }
}
