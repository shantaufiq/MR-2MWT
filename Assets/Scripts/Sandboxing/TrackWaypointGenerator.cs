using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class OvalTrackGenerator : MonoBehaviour
{
    [Header("Referensi")]
    [Tooltip("Player yang akan menjadi titik awal lintasan.")]
    public Transform player;
    public GameObject conePrefab;
    [Tooltip("Jarak cone dari tikungan ke arah ruas lurus (meter).")]
    public float coneOffsetFromTurn = 0.3f;

    [Header("Ukuran (meter)")]
    [Tooltip("Panjang ruas lurus (jarak antar pusat belokan) sepanjang sumbu X.")]
    public float straightLength = 10f;
    [Tooltip("Radius belokan setengah lingkaran di kedua ujung. Lebar oval = 2*radius.")]
    public float radius = 2f;

    [Header("Sampling / Kehalusan")]
    [Tooltip("Jumlah titik pada tiap setengah lingkaran.")]
    public int arcSegments = 32;
    [Tooltip("Jarak sampling titik pada ruas lurus.")]
    public float straightStep = 0.25f;

    [Header("Penempatan")]
    [Tooltip("Ketinggian lintasan pada sumbu Y (opsional).")]
    public float heightY = 0f;
    [Tooltip("Jika aktif: pakai sumbu dunia (Vector3.right/forward). Jika mati: pakai sumbu lokal (transform.right/forward).")]
    public bool useWorldAxes = true;

    [Header("Arah Putar")]
    [Tooltip("True = searah jarum jam (belok kanan). False = berlawanan jarum jam (belok kiri).")]
    public bool clockwise = true;

    private LineRenderer lr;
    private readonly List<Vector3> pts = new List<Vector3>();

    private GameObject coneLeftInstance;
    private GameObject coneRightInstance;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;                               // oval tertutup
        lr.alignment = LineAlignment.TransformZ;      // agar tidak 'menghadap kamera'
    }

    void Start()
    {
        if (Application.isPlaying) Generate();        // hanya sekali saat runtime
    }

    [ContextMenu("Generate (Play Mode)")]
    public void Generate()
    {
        if (player == null)
        {
            Debug.LogWarning("Player belum di-assign ke OvalTrackGenerator!");
            return;
        }

        pts.Clear();

        // Validasi parameter
        radius = Mathf.Max(0.01f, radius);
        straightLength = Mathf.Max(0.01f, straightLength);
        arcSegments = Mathf.Clamp(arcSegments, 8, 256);
        straightStep = Mathf.Max(0.05f, straightStep);

        // Basis sumbu: memanjang di X, lebar di Z
        Vector3 RIGHT = useWorldAxes ? Vector3.right : transform.right;
        Vector3 FWD = useWorldAxes ? Vector3.forward : transform.forward;

        // Origin = posisi player sekarang
        Vector3 ORI = player.position;

        // Helper konversi koordinat (x,z) lokal oval -> world
        Vector3 P(float lx, float lz) => ORI + RIGHT * lx + FWD * lz + Vector3.up * heightY;

        float L = straightLength;
        float R = radius;

        // --- 1) Straight bawah: (0, z0) -> (L, z0)
        float zBottom = 0f;
        float zTop = clockwise ? -2f * R : 2f * R;

        int sSteps = Mathf.Max(1, Mathf.CeilToInt(L / straightStep));
        for (int i = 0; i <= sSteps; i++)
        {
            float t = (float)i / sSteps;
            pts.Add(P(L * t, zBottom));
        }

        // --- 2) Arc kanan
        if (!clockwise)
        {
            Vector3 centerRight = P(L, R);
            for (int i = 1; i <= arcSegments; i++)
            {
                float a = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, (float)i / arcSegments);
                float lx = R * Mathf.Cos(a);
                float lz = R * Mathf.Sin(a);
                pts.Add(centerRight + RIGHT * lx + FWD * lz);
            }
        }
        else
        {
            Vector3 centerRight = P(L, -R);
            for (int i = 1; i <= arcSegments; i++)
            {
                float a = Mathf.Lerp(Mathf.PI * 0.5f, -Mathf.PI * 0.5f, (float)i / arcSegments);
                float lx = R * Mathf.Cos(a);
                float lz = R * Mathf.Sin(a);
                pts.Add(centerRight + RIGHT * lx + FWD * lz);
            }
        }

        // --- 3) Straight atas
        for (int i = 1; i <= sSteps; i++)
        {
            float t = (float)i / sSteps;
            pts.Add(P(L * (1f - t), zTop));
        }

        // --- 4) Arc kiri
        if (!clockwise)
        {
            Vector3 centerLeft = P(0f, R);
            for (int i = 1; i <= arcSegments; i++)
            {
                float a = Mathf.Lerp(Mathf.PI * 0.5f, -Mathf.PI * 0.5f, (float)i / arcSegments);
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
                float a = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, (float)i / arcSegments);
                float lx = -R * Mathf.Cos(a);
                float lz = R * Mathf.Sin(a);
                pts.Add(centerLeft + RIGHT * lx + FWD * lz);
            }
        }

        // Apply ke LineRenderer
        lr.positionCount = pts.Count;
        lr.SetPositions(pts.ToArray());

        SpawnCones();
    }

    // Opsional: set total panjang lintasan, otomatis hitung straightLength dari radius
    public void SetTotalLength(float totalLength)
    {
        float minTotal = 2f * Mathf.PI * Mathf.Max(radius, 0.01f);
        totalLength = Mathf.Max(totalLength, minTotal + 1e-4f);
        straightLength = (totalLength - 2f * Mathf.PI * radius) * 0.5f;
    }

    public void SpawnCones()
    {
        if (conePrefab == null)
        {
            Debug.LogWarning("Cone Prefab belum di-assign!");
            return;
        }

        // Hapus cone lama kalau sudah ada
        if (coneLeftInstance != null) Destroy(coneLeftInstance);
        if (coneRightInstance != null) Destroy(coneRightInstance);

        // Basis sumbu sama seperti Generate()
        Vector3 RIGHT = useWorldAxes ? Vector3.right : transform.right;
        Vector3 FWD = useWorldAxes ? Vector3.forward : transform.forward;
        Vector3 ORI = (player ? player.position : transform.position);

        float L = Mathf.Max(0.01f, straightLength);
        float R = Mathf.Max(0.01f, radius);

        // Z bawah/atas (tergantung clockwise), lalu ambil garis tengah
        float zBottom = 0f;
        float zTop = clockwise ? -2f * R : 2f * R;
        float zCenter = 0.5f * (zBottom + zTop);

        // X dekat tikungan kiri/kanan
        float xLeft = Mathf.Clamp(coneOffsetFromTurn, 0f, L);
        float xRight = Mathf.Clamp(L - coneOffsetFromTurn, 0f, L);

        Vector3 P(float lx, float lz) => ORI + RIGHT * lx + FWD * lz;

        Vector3 leftPos = P(xLeft, zCenter);
        Vector3 rightPos = P(xRight, zCenter);

        // --- Set posisi Y tetap 0.245f ---
        leftPos.y = 0.245f;
        rightPos.y = 0.245f;

        // Instantiate prefab (bukan child dari generator)
        coneLeftInstance = Instantiate(conePrefab, leftPos, Quaternion.identity);
        coneRightInstance = Instantiate(conePrefab, rightPos, Quaternion.identity);

        // Optional: rotasi cone mengikuti arah lintasan (sumbu RIGHT)
        Vector3 fwdDir = RIGHT;
        coneLeftInstance.transform.rotation = Quaternion.LookRotation(fwdDir, Vector3.up);
        coneRightInstance.transform.rotation = Quaternion.LookRotation(fwdDir, Vector3.up);
    }

}
