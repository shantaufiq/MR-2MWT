using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

[RequireComponent(typeof(LineRenderer))]
public class OvalTrackGenerator : MonoBehaviour
{
    // ---------- ORIENTASI ----------
    public enum TrackOrientation { PlusX, MinusX, PlusZ, MinusZ }

    [Header("Referensi & Orientasi")]
    [Tooltip("Posisi player menjadi origin track saat generate.")]
    public Transform player;
    [Tooltip("Orientasi memanjang lintasan. Bisa diubah runtime via tombol UI.")]
    public TrackOrientation orientation = TrackOrientation.PlusX;

    [Tooltip("Set true agar clockwise otomatis menyesuaikan orientasi sesuai tabel mapping.")]
    public bool matchClockwiseToOrientation = true;

    [Space(2f)]
    [SerializeField] private Button prevBtn;
    [SerializeField] private Button nextBtn;
    [SerializeField] private TMP_Text labelOrientation;

    // ---------- UKURAN TRACK ----------
    [Header("Ukuran (meter)")]
    [Tooltip("Panjang ruas lurus (jarak antar pusat belokan).")]
    public float straightLength = 10f;
    [Tooltip("Radius setengah lingkaran di kedua ujung. Lebar oval = 2*radius.")]
    public float radius = 2f;
    [Tooltip("True = searah jarum jam (belok kanan). False = berlawanan.")]
    public bool clockwise = true;

    [Space(2f)]
    [SerializeField] private Toggle toggleClockwise;
    [SerializeField] private TMP_Text labelClockwiseText;

    // ---------- SAMPLING ----------
    [Header("Sampling / Kehalusan")]
    [Tooltip("Jumlah titik per setengah lingkaran.")]
    public int arcSegments = 32;
    [Tooltip("Jarak sampling ruas lurus.")]
    public float straightStep = 0.25f;

    // ---------- KETINGGIAN TRACK ----------
    [Header("Penempatan")]
    [Tooltip("Ketinggian lintasan pada sumbu Y relatif origin (player).")]
    public float trackY = 0f;

    // ---------- CONES ----------
    [Header("Cone Settings")]
    public GameObject conePrefab;
    [Tooltip("Offset dari tikungan ke arah ruas lurus.")]
    public float coneOffsetFromTurn = 0.3f;
    [Tooltip("Tinggi Y untuk cone yang di-spawn.")]
    public float coneY = 0.245f;

    private GameObject coneLeftInstance;
    private GameObject coneRightInstance;

    // ---------- INTERNAL ----------
    private LineRenderer lr;
    private readonly List<Vector3> pts = new List<Vector3>();

    private TrackOrientation[] tOrientationValues;
    private int tOrientationIndex;

    private bool _suppress;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;                          // oval tertutup
        lr.alignment = LineAlignment.TransformZ; // supaya tidak menghadap kamera

        /* Setup Track orientation Value */

        // ambil semua nilai enum
        tOrientationValues = (TrackOrientation[])Enum.GetValues(typeof(TrackOrientation));

        // sinkronkan index awal dengan nilai orientation pada target
        tOrientationIndex = Array.IndexOf(tOrientationValues, orientation);
        if (tOrientationIndex < 0) tOrientationIndex = 0;

        // pasang listener tombol
        if (prevBtn) prevBtn.onClick.AddListener(Prev);
        if (nextBtn) nextBtn.onClick.AddListener(Next);


        /* Setup Tootle Clockwise */
        toggleClockwise.onValueChanged.AddListener(OnToggleClockwiseChanged);

        //_suppress = true;
        toggleClockwise.isOn = clockwise;
        // _suppress = false;
        UpdateLabelToggleClockwise(clockwise);
    }

    void Start()
    {
        if (matchClockwiseToOrientation)
            clockwise = DefaultClockwiseFor(orientation);

        if (Application.isPlaying)
        {
            Generate();
            SpawnCones();
        }
    }

    // =========================================================
    // ===================  ORIENTATION API  ===================
    // Panggil fungsi2 ini dari Button.onClick
    public void Next()
    {
        SetByIndex((tOrientationIndex + 1) % tOrientationValues.Length);
    }

    public void Prev()
    {
        int len = tOrientationValues.Length;
        SetByIndex((tOrientationIndex - 1 + len) % len);     // wrap-around ke belakang
    }

    private void SetByIndex(int i)
    {
        tOrientationIndex = i;
        var value = tOrientationValues[tOrientationIndex];

        SetOrientation(value);

        if (labelOrientation) labelOrientation.text = tOrientationValues[tOrientationIndex].ToString();
    }

    private void UpdateLabelToggleClockwise(bool isOn)
    {
        if (!labelClockwiseText) return;
        labelClockwiseText.text = isOn ? "Searah Jarum Jam" : "Berlawanan Jarum Jam";
    }

    public void OnToggleClockwiseChanged(bool isOn)
    {
        // if (_suppress) return;

        Debug.Log("called...");

        matchClockwiseToOrientation = false;
        clockwise = !isOn;
        UpdateLabelToggleClockwise(isOn);

        RegenerateAll();
    }

    public void SetOrientation(TrackOrientation o)
    {
        orientation = o;
        if (matchClockwiseToOrientation)
            clockwise = DefaultClockwiseFor(o);
        RegenerateAll();
    }

    bool DefaultClockwiseFor(TrackOrientation o)
    {
        // Mapping sesuai permintaan:
        // PlusX  -> true
        // MinusX -> false
        // PlusZ  -> false
        // MinusZ -> true
        switch (o)
        {
            case TrackOrientation.PlusX: return true;
            case TrackOrientation.MinusX: return false;
            case TrackOrientation.PlusZ: return false;
            case TrackOrientation.MinusZ: return true;
            default: return true;
        }
    }

    void RegenerateAll()
    {
        Generate();
        SpawnCones();
    }
    // =========================================================

    // Hitung RIGHT (arah memanjang) & FWD (arah lebar) sesuai orientasi (world axes)
    void ResolveAxes(out Vector3 RIGHT, out Vector3 FWD)
    {
        switch (orientation)
        {
            case TrackOrientation.PlusX:
                RIGHT = Vector3.right; FWD = Vector3.forward; break;
            case TrackOrientation.MinusX:
                RIGHT = Vector3.left; FWD = Vector3.forward; break;
            case TrackOrientation.PlusZ:
                RIGHT = Vector3.forward; FWD = Vector3.right; break;
            case TrackOrientation.MinusZ:
                RIGHT = Vector3.back; FWD = Vector3.right; break;
            default:
                RIGHT = Vector3.right; FWD = Vector3.forward; break;
        }
    }

    public void Generate()
    {
        if (player == null)
        {
            Debug.LogWarning("Player belum di-assign ke OvalTrackGenerator.");
            return;
        }

        pts.Clear();

        // Validasi
        radius = Mathf.Max(0.01f, radius);
        straightLength = Mathf.Max(0.01f, straightLength);
        arcSegments = Mathf.Clamp(arcSegments, 8, 256);
        straightStep = Mathf.Max(0.05f, straightStep);

        // Basis sumbu & origin
        ResolveAxes(out Vector3 RIGHT, out Vector3 FWD);
        Vector3 ORI = player.position;

        // Helper: koordinat lokal oval (lx,lz) -> world
        Vector3 P(float lx, float lz) => ORI + RIGHT * lx + FWD * lz + Vector3.up * trackY;

        float L = straightLength;
        float R = radius;

        // Z bawah/atas (sepanjang FWD)
        float zBottom = 0f;
        float zTop = clockwise ? -2f * R : 2f * R;

        // 1) Straight bawah (0,zBottom) -> (L,zBottom)
        int sSteps = Mathf.Max(1, Mathf.CeilToInt(L / straightStep));
        for (int i = 0; i <= sSteps; i++)
        {
            float t = (float)i / sSteps;
            pts.Add(P(L * t, zBottom));
        }

        // 2) Arc kanan (pusat di (L, ±R))
        if (!clockwise) // CCW
        {
            Vector3 centerRight = P(L, R);
            for (int i = 1; i <= arcSegments; i++)
            {
                float a = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, (float)i / arcSegments);
                pts.Add(centerRight + RIGHT * (R * Mathf.Cos(a)) + FWD * (R * Mathf.Sin(a)));
            }
        }
        else // CW
        {
            Vector3 centerRight = P(L, -R);
            for (int i = 1; i <= arcSegments; i++)
            {
                float a = Mathf.Lerp(Mathf.PI * 0.5f, -Mathf.PI * 0.5f, (float)i / arcSegments);
                pts.Add(centerRight + RIGHT * (R * Mathf.Cos(a)) + FWD * (R * Mathf.Sin(a)));
            }
        }

        // 3) Straight atas (L,zTop) -> (0,zTop)
        for (int i = 1; i <= sSteps; i++)
        {
            float t = (float)i / sSteps;
            pts.Add(P(L * (1f - t), zTop));
        }

        // 4) Arc kiri (pusat di (0, ±R)) — mirror di X (pakai -cos)
        if (!clockwise) // CCW
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
        else // CW
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
    }

    // Spawn 2 cone dari prefab di tengah lebar lintasan, dekat tikungan
    public void SpawnCones()
    {
        if (conePrefab == null) return;

        // Hapus yang lama
        if (coneLeftInstance != null) Destroy(coneLeftInstance);
        if (coneRightInstance != null) Destroy(coneRightInstance);

        ResolveAxes(out Vector3 RIGHT, out Vector3 FWD);
        Vector3 ORI = player ? player.position : transform.position;

        float L = Mathf.Max(0.01f, straightLength);
        float R = Mathf.Max(0.01f, radius);

        float zBottom = 0f;
        float zTop = clockwise ? -2f * R : 2f * R;
        float zCenter = 0.5f * (zBottom + zTop);

        float xLeft = Mathf.Clamp(coneOffsetFromTurn, 0f, L);
        float xRight = Mathf.Clamp(L - coneOffsetFromTurn, 0f, L);

        Vector3 P(float lx, float lz) => ORI + RIGHT * lx + FWD * lz;

        Vector3 leftPos = P(xLeft, zCenter); leftPos.y = coneY;
        Vector3 rightPos = P(xRight, zCenter); rightPos.y = coneY;

        coneLeftInstance = Instantiate(conePrefab, leftPos, Quaternion.identity);
        coneRightInstance = Instantiate(conePrefab, rightPos, Quaternion.identity);

        // Arahkan cone mengikuti arah memanjang lintasan
        Quaternion look = Quaternion.LookRotation(RIGHT, Vector3.up);
        coneLeftInstance.transform.rotation = look;
        coneRightInstance.transform.rotation = look;
    }

    // (Opsional) set total panjang lintasan lalu auto hitung straight
    public void SetTotalLength(float totalLength)
    {
        float minTotal = 2f * Mathf.PI * Mathf.Max(radius, 0.01f);
        totalLength = Mathf.Max(totalLength, minTotal + 1e-4f);
        straightLength = (totalLength - 2f * Mathf.PI * radius) * 0.5f;
    }
}
