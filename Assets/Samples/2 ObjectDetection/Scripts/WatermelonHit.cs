using UnityEngine;

public class WatermelonHit : MonoBehaviour
{
    [Header("模拟重力设置")]
    // 设为 -1.5 到 -2.5 之间。越接近 0，西瓜飘得越慢
    public float customGravity = -0.2f;
    public float missHeight = -2.0f;
    public bool isBomb = false;

    private bool wasHit = false;
    private bool hasCountedMiss = false;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false; // 必须关闭系统重力
            rb.linearDamping = 0.1f; // 阻力设小，保证轨迹是完美的抛物线
        }
        Destroy(gameObject, 10.0f); // 慢动作下给足生存时间
    }

    void FixedUpdate()
    {
        if (rb != null)
        {
            // 持续施加微弱的向下引力
            rb.AddForce(Vector3.up * customGravity, ForceMode.Acceleration);
        }
    }

    void Update()
    {
        if (transform.position.y < missHeight) HandleMissAndDestroy();
    }

    void HandleMissAndDestroy()
    {
        if (wasHit || hasCountedMiss) { Destroy(gameObject); return; }
        if (!isBomb && WatermelonGameManager.Instance != null)
        {
            hasCountedMiss = true;
            WatermelonGameManager.Instance.watermelonMisses++;
        }
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (wasHit || hasCountedMiss) return;
        if (other.CompareTag("MyRightHand"))
        {
            wasHit = true;
            if (isBomb && WatermelonGameManager.Instance != null)
            {
                WatermelonGameManager.Instance.bombHits++;
                WatermelonGameManager.Instance.AddScore(-1);
            }
            else if (WatermelonGameManager.Instance != null)
            {
                WatermelonGameManager.Instance.watermelonHits++;
                WatermelonGameManager.Instance.AddScore(1);
            }
            Destroy(gameObject);
        }
    }
}