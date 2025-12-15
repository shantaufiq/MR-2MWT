using Meta.XR.Util;
using UnityEngine;

namespace Meta.XR.MRUtilityKit
{
    public class FloorAnchorSpawner : MonoBehaviour
    {
        public enum OffsetSpace
        {
            AnchorLocal, // offset relatif terhadap transform FloorAnchor
            World        // offset di world space
        }

        [Header("When to spawn")]
        [Tooltip("Saat scene data selesai load, spawn di room mana?")]
        public MRUK.RoomFilter SpawnOnSceneLoaded = MRUK.RoomFilter.CurrentRoomOnly;

        [Header("What to spawn")]
        [Tooltip("Prefab lantai yang mau di-spawn. Bisa juga object di scene (akan dipindah).")]
        public GameObject FloorPrefabOrObject;

        [Tooltip("Kalau true, object akan di-parent ke FloorAnchor (enak buat ikut drift/relokasi anchor).")]
        public bool ParentToFloorAnchor = true;

        [Header("Placement (editable in Inspector)")]
        public OffsetSpace PositionOffsetSpace = OffsetSpace.AnchorLocal;

        [Tooltip("Offset posisi yang bisa kamu atur di Inspector.")]
        public Vector3 PositionOffset = Vector3.zero;

        [Tooltip("Offset rotasi (Euler) tambahan yang bisa kamu atur di Inspector.")]
        public Vector3 RotationOffsetEuler = Vector3.zero;

        [Tooltip("Offset tambahan sepanjang normal lantai (biasanya ke atas).")]
        public float NormalOffsetMeters = 0.0f;

        [Header("Optional: scale to floor")]
        [Tooltip("Jika true dan FloorAnchor punya PlaneRect, object akan di-scale agar menutup area plane rect.")]
        public bool FitToFloorPlaneRect = false;

        [Tooltip("Ukuran 'asli' prefab pada sumbu X & Y (meter) saat scale = (1,1,1). " +
                 "Untuk Unity Quad default biasanya 1x1.")]
        public Vector2 PrefabSizeXYMeters = new Vector2(1f, 1f);

        private GameObject _spawnedInstance;

        // cache supaya fungsi external bisa dipanggil kapan saja
        private Transform _cachedFloorAnchor;
        private MRUKRoom _cachedRoom;

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

        [ContextMenu("Spawn Floor Now (Current Room)")]
        public void SpawnNowCurrentRoom()
        {
            if (!MRUK.Instance) return;
            SpawnOrMoveOnRoomFloor(MRUK.Instance.GetCurrentRoom());
        }

        public void SpawnOrMoveOnRoomFloor(MRUKRoom room)
        {
            if (!room)
            {
                Debug.LogWarning("[FloorAnchorSpawner] Room is null.");
                return;
            }

            var floorAnchor = room.FloorAnchor;
            if (!floorAnchor)
            {
                Debug.LogWarning("[FloorAnchorSpawner] FloorAnchor not found in this room.");
                return;
            }

            _cachedRoom = room;
            _cachedFloorAnchor = floorAnchor.transform;

            if (!FloorPrefabOrObject)
            {
                Debug.LogWarning("[FloorAnchorSpawner] FloorPrefabOrObject is not assigned.");
                return;
            }

            GameObject targetGO;

            // Kalau object sudah ada di scene (bukan prefab asset), kita pindahkan
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
            {
                TryFitScaleToPlaneRect(targetGO.transform, _cachedFloorAnchor);
            }
        }

        /// <summary>
        /// Fungsi tambahan: bisa dipanggil dari script lain.
        /// Mengatur PositionOffset & RotationOffsetEuler berdasarkan pose Transform (world),
        /// lalu (opsional) langsung apply ke object lantai yang di-spawn.
        /// </summary>
        /// <param name="sourceWorldPose">Transform acuan (world pose) yang ingin kamu jadikan target posisi/rotasi.</param>
        /// <param name="applyImmediately">Kalau true dan object sudah ada, langsung dipindahkan.</param>
        public void SetPlacementFromTransform(Transform sourceWorldPose, bool applyImmediately = true)
        {
            if (!sourceWorldPose)
            {
                Debug.LogWarning("[FloorAnchorSpawner] SetPlacementFromTransform called with null Transform.");
                return;
            }

            // Pastikan kita punya floor anchor cache
            if (_cachedFloorAnchor == null)
            {
                if (MRUK.Instance)
                {
                    var room = MRUK.Instance.GetCurrentRoom();
                    if (room && room.FloorAnchor)
                    {
                        _cachedRoom = room;
                        _cachedFloorAnchor = room.FloorAnchor.transform;
                    }
                }
            }

            if (_cachedFloorAnchor == null)
            {
                Debug.LogWarning("[FloorAnchorSpawner] FloorAnchor not ready yet. Call after scene loaded / after Spawn.");
                return;
            }

            var floorAnchor = _cachedFloorAnchor;

            Vector3 desiredPos = sourceWorldPose.position;
            Quaternion desiredRot = sourceWorldPose.rotation;

            // NOTE: normal plane kita anggap +Z lokal anchor (floorAnchor.forward)
            Quaternion baseRot = floorAnchor.rotation;
            Vector3 worldNormal = baseRot * Vector3.forward;

            if (ParentToFloorAnchor)
            {
                // Kita set offset dalam local anchor agar konsisten dengan mode parenting
                PositionOffsetSpace = OffsetSpace.AnchorLocal;

                Vector3 localPos = floorAnchor.InverseTransformPoint(desiredPos);
                localPos -= Vector3.forward * NormalOffsetMeters; // buang normal offset agar tetap dipakai lewat field
                PositionOffset = localPos;

                Quaternion localRot = Quaternion.Inverse(baseRot) * desiredRot;
                RotationOffsetEuler = localRot.eulerAngles;
            }
            else
            {
                // Tanpa parenting, kita simpan offset dalam world (relative terhadap anchor basePos di ApplyPlacement)
                PositionOffsetSpace = OffsetSpace.World;

                Vector3 basePos = floorAnchor.position;
                PositionOffset = (desiredPos - basePos) - (worldNormal * NormalOffsetMeters);

                Quaternion rotOffset = Quaternion.Inverse(baseRot) * desiredRot;
                RotationOffsetEuler = rotOffset.eulerAngles;
            }

            if (applyImmediately)
            {
                var target = GetCurrentTargetTransform();
                if (target != null)
                {
                    ApplyPlacement(target, floorAnchor);
                    if (FitToFloorPlaneRect)
                        TryFitScaleToPlaneRect(target, floorAnchor);
                }
                else
                {
                    // Belum spawn/move apa-apa, tapi offset sudah tersimpan.
                    Debug.Log("[FloorAnchorSpawner] Offsets updated. Spawn/move will use the new placement.");
                }
            }
        }

        private Transform GetCurrentTargetTransform()
        {
            if (!FloorPrefabOrObject) return null;

            // Kalau object scene
            if (FloorPrefabOrObject.scene.IsValid())
                return FloorPrefabOrObject.transform;

            // Kalau prefab instance
            if (_spawnedInstance != null)
                return _spawnedInstance.transform;

            return null;
        }

        private void ApplyPlacement(Transform target, Transform floorAnchor)
        {
            Quaternion rotOffset = Quaternion.Euler(RotationOffsetEuler);

            if (ParentToFloorAnchor)
            {
                target.SetParent(floorAnchor, worldPositionStays: false);

                Vector3 localOffset = PositionOffset;
                if (PositionOffsetSpace == OffsetSpace.World)
                {
                    localOffset = Quaternion.Inverse(floorAnchor.rotation) * PositionOffset;
                }

                Vector3 localNormal = Vector3.forward; // normal plane dalam local anchor
                localOffset += localNormal * NormalOffsetMeters;

                target.localPosition = localOffset;
                target.localRotation = rotOffset;
            }
            else
            {
                Vector3 basePos = floorAnchor.position;
                Quaternion baseRot = floorAnchor.rotation;

                Vector3 worldOffset = (PositionOffsetSpace == OffsetSpace.AnchorLocal)
                    ? baseRot * PositionOffset
                    : PositionOffset;

                Vector3 worldNormal = baseRot * Vector3.forward;
                worldOffset += worldNormal * NormalOffsetMeters;

                target.position = basePos + worldOffset;
                target.rotation = baseRot * rotOffset;
            }
        }

        private void TryFitScaleToPlaneRect(Transform target, Transform floorAnchor)
        {
            var mrukAnchor = floorAnchor.GetComponent<MRUKAnchor>();
            if (!mrukAnchor || !mrukAnchor.PlaneRect.HasValue)
            {
                Debug.LogWarning("[FloorAnchorSpawner] FitToFloorPlaneRect aktif, tapi PlaneRect tidak tersedia di FloorAnchor.");
                return;
            }

            var rect = mrukAnchor.PlaneRect.Value;
            float sx = (PrefabSizeXYMeters.x <= 0.0001f) ? 1f : rect.size.x / PrefabSizeXYMeters.x;
            float sy = (PrefabSizeXYMeters.y <= 0.0001f) ? 1f : rect.size.y / PrefabSizeXYMeters.y;

            var ls = target.localScale;
            target.localScale = new Vector3(sx * ls.x, sy * ls.y, ls.z);
        }
    }
}
