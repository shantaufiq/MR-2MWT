using UnityEngine;
using DG.Tweening;

namespace WalkingTest
{
    public class ItemObject : MonoBehaviour
    {
        public int pointValue = 10;

        public WalkTestManager _walkTestManager;

        [Header("Tween Settings")]
        [SerializeField] private float floatHeight = 0.5f;
        [SerializeField] private float floatDuration = 1.0f;
        [SerializeField] private float rotateDuration = 1.0f;
        [SerializeField] private Vector3 rotateAxis = new Vector3(0, 1, 0); // sumbu putar

        private Tween _floatTween;
        private Tween _rotateTween;

        private void Start()
        {
            if (_walkTestManager == null)
            {
                _walkTestManager = FindObjectOfType<WalkTestManager>();
            }
        }

        private void OnEnable()
        {
            TweenAnimation();

            Vector3 idty = this.transform.position;
            this.transform.position = new Vector3(idty.x, 0.7f, idty.z);
        }

        private void OnDisable()
        {
            _floatTween?.Kill();
            _rotateTween?.Kill();
            _floatTween = null;
            _rotateTween = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _walkTestManager.AddScore(pointValue);
                SFXManager.Main.PlayFromSFXObjectLibrary("score");

                gameObject.SetActive(false);
            }
        }

        private void TweenAnimation()
        {
            _floatTween?.Kill();
            _rotateTween?.Kill();

            // Naik-turun (smooth)
            _floatTween = transform
                .DOLocalMoveY(floatHeight, floatDuration)
                .SetRelative(true)                 // gerak relatif, bukan ke posisi absolut
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);

            // Muter tanpa “snap” (additive)
            _rotateTween = transform
                .DOLocalRotate(rotateAxis * 360f, rotateDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart);
        }
    }
}
