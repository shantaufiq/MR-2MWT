using Meta.XR.Util;
using UnityEngine;
using UnityEngine.Events;

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

        [Header("Events")]
        public UnityEvent onFloorTransformChanged;

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
                var room = MRUK.Instance.GetCurrentRoom();
                if (room)
                    SpawnOrMoveOnRoomFloor(room);
            });
        }
        #endregion

        #region Public API

        public void SpawnNowCurrentRoom()
        {
            if (!MRUK.Instance) return;
            SpawnOrMoveOnRoomFloor(MRUK.Instance.GetCurrentRoom());
        }

        public void SetPlacementFromPlayerPosition(Transform playerWorldTransform)
        {
            if (!playerWorldTransform) return;

            CacheFloorAnchorIfNeeded();
            if (_cachedFloorAnchor == null) return;

            Vector3 playerPos = playerWorldTransform.position;

            Vector3 projectedWorldPos = new Vector3(
                playerPos.x,
                _cachedFloorAnchor.position.y,
                playerPos.z
            );

            PositionOffsetSpace = OffsetSpace.AnchorLocal;
            PositionOffset = _cachedFloorAnchor.InverseTransformPoint(projectedWorldPos);
            RotationOffsetEuler = Vector3.zero;
        }

        public void RotateFloorAroundNormal(float deltaDegrees)
        {
            CacheFloorAnchorIfNeeded();
            if (_cachedFloorAnchor == null) return;

            RotationOffsetEuler.z = Mathf.Repeat(RotationOffsetEuler.z + deltaDegrees, 360f);

            var target = GetCurrentTargetTransform();
            if (target != null)
            {
                ApplyPlacement(target, _cachedFloorAnchor);
                onFloorTransformChanged?.Invoke();
            }
        }

        #endregion

        #region Core Logic

        public void SpawnOrMoveOnRoomFloor(MRUKRoom room)
        {
            if (!room || !room.FloorAnchor) return;

            _cachedRoom = room;
            _cachedFloorAnchor = room.FloorAnchor.transform;

            GameObject targetGO;

            if (FloorPrefabOrObject.scene.IsValid())
            {
                targetGO = FloorPrefabOrObject;
            }
            else
            {
                if (_spawnedInstance == null)
                    _spawnedInstance = Instantiate(FloorPrefabOrObject);

                targetGO = _spawnedInstance;
            }

            ApplyPlacement(targetGO.transform, _cachedFloorAnchor);
            onFloorTransformChanged?.Invoke();
        }

        #endregion

        #region Helpers

        private void ApplyPlacement(Transform target, Transform floorAnchor)
        {
            Quaternion rotOffset = Quaternion.Euler(RotationOffsetEuler);

            target.SetParent(floorAnchor, false);
            target.localPosition = PositionOffset + Vector3.forward * NormalOffsetMeters;
            target.localRotation = rotOffset;
        }

        private void CacheFloorAnchorIfNeeded()
        {
            if (_cachedFloorAnchor != null) return;
            if (!MRUK.Instance) return;

            var room = MRUK.Instance.GetCurrentRoom();
            if (room && room.FloorAnchor)
                _cachedFloorAnchor = room.FloorAnchor.transform;
        }

        private Transform GetCurrentTargetTransform()
        {
            if (FloorPrefabOrObject.scene.IsValid())
                return FloorPrefabOrObject.transform;

            return _spawnedInstance ? _spawnedInstance.transform : null;
        }

        public Transform GetSpawnedFloorRoot()
        {
            return GetCurrentTargetTransform();
        }

        public Transform GetFloorMainObject()
        {
            var root = GetSpawnedFloorRoot();
            return root ? root.Find("Main Object") : null;
        }

        #endregion
    }
}
