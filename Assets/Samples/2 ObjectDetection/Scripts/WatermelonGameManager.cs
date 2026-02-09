using UnityEngine;
using System.Collections;
using TMPro; // 需要导入 TextMeshPro

public class WatermelonGameManager : MonoBehaviour
{
    public static WatermelonGameManager Instance;
    public GameObject bombPrefab; // 【新增】拖入炸弹预制体
    public GameObject watermelonPrefab; // 拖入刚才做的西瓜模板
    public TextMeshPro scoreText;      // 拖入一个 3D 文字显示分数
    public WaterPathRecord waterPathRecord;
    private int score = 0;
    private float timer = 35f;
    private bool isPlaying = true;
    [Header("抛射设置")]
    public float spawnZ = 0.5f;        // 距离玩家的前后距离 (0.8米)
    public float spawnY = -2.0f;        // 生成高度 (建议低一点，0.2米甚至更低，营造从下面出来的感觉)
    public float minUpForce = 4.5f;    // 最小向上力 (根据手感微调)
    public float maxUpForce = 5.5f;    // 最大向上力
    public float xSpread = 0.6f;       // 左右生成的宽度范围
    [Header("随机高度设置")]
    public float minPeakHeight = 1.4f; // 最低飞到胸口
    public float maxPeakHeight = 1.8f; // 最高飞到头顶以上
    // 统计数据
    [HideInInspector] public int watermelonHits = 0;
    [HideInInspector] public int watermelonMisses = 0;
    [HideInInspector] public int bombHits = 0;
    void Awake() { Instance = this; }

    void Start()
    {
        StartCoroutine(SpawnWatermelons());
        waterPathRecord.StartRecording();
    }

    void Update()
    {
        if (isPlaying)
        {
            timer -= Time.deltaTime;
            if (timer <= 0) EndGame();
            else UpdateUI();
        }
    }

    IEnumerator SpawnWatermelons()
    {
        for (int round = 1; round <= 11; round++)
        {
            if (!isPlaying) break;

            // 每一轮随机产生 1 或 2 个
            int count = Random.Range(1, 3);
            for (int i = 0; i < count; i++)
            {
                SpawnObject();
            }
            // 每轮间隔 3 秒
            yield return new WaitForSeconds(3f);
        }
    }

    //void SpawnObject()
    //{
    //    // 在玩家前方随机位置生成 (右手前方 0.5米左右的区域)
    //    Vector3 spawnPos = Camera.main.transform.position + Camera.main.transform.forward * 0.7f
    //                       + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.2f, 0.3f), Random.Range(-0.2f, 0.2f));

    //    // 20% 几率生成炸弹
    //    GameObject prefab = (Random.value > 0.7f) ? bombPrefab : watermelonPrefab;
    //    Instantiate(prefab, spawnPos, Quaternion.identity);
    //}
    void SpawnObject()
    {
        // 1. 确定生成位置 (从地板下方 -1.0 米处生成)
        Vector3 headPos = Camera.main.transform.position;
        Vector3 feetPos = new Vector3(headPos.x, 0, headPos.z);
        Vector3 forwardDir = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;

        Vector3 centerPos = feetPos + forwardDir * spawnZ;
        float randomX = Random.Range(-xSpread, xSpread);
        Vector3 spawnPos = centerPos + (Camera.main.transform.right * randomX);
        float startY = spawnY; // 设定的初始高度
        spawnPos.y = startY;

        // 2. 生成物体
        GameObject prefab = (Random.value > 0.7f) ? bombPrefab : watermelonPrefab;
        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);

        // 3. 计算并设置初速度
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        WatermelonHit hitScript = obj.GetComponent<WatermelonHit>();

        if (rb != null && hitScript != null)
        {
            // 随机一个目标最高点
            float targetHeight = Random.Range(minPeakHeight, maxPeakHeight);
            float g = hitScript.customGravity;

            // 物理公式计算向上初速度: v = sqrt(-2 * g * deltaH)
            float upVelocity = Mathf.Sqrt(-2f * g * (targetHeight - startY));

            // 左右斜着飞一点
            float sideVelocity = -randomX * 1.5f;

            // 应用速度
            rb.linearVelocity = new Vector3(sideVelocity, upVelocity, 0);
            rb.angularVelocity = Random.insideUnitSphere * 2f;
        }
    }

    public void AddScore(int value) { score += value; }

    void UpdateUI()
    {
        scoreText.text = $"Time: {Mathf.Max(0, timer):F0}s\nScore: {score}\nHit: {watermelonHits} Miss: {watermelonMisses}\nBombs: {bombHits}";
    }

    void EndGame()
    {
        isPlaying = false;
        waterPathRecord.StopRecording();
        scoreText.text = $"GAME OVER\nScore: {score}\nHit: {watermelonHits} Miss: {watermelonMisses}\nBombs: {bombHits}";
    }
}