using UnityEngine;

public class CubeSpawner : MonoBehaviour
{
    //public GameObject notePrefab; // 拖入刚才做的 Note 预制体
    public float beatRate = 1.0f; // 每 1 秒生成一个
    [Header("预制体库")]
    // 在Inspector里：Element 0 拖入水平预制体，Element 1 拖入竖直预制体
    public GameObject[] notePrefabs;
    public void StartSpawning()
    {
        // 只有被调用时，才会开始定时生成
        InvokeRepeating("SpawnNote", 0.5f, beatRate);
    }
    public void StopSpawning()
    {
        CancelInvoke("SpawnNote"); // 停止名为 SpawnNote 的所有定时调用
    }
    void SpawnNote()
    {
        if (notePrefabs.Length == 0) return;

        // 随机选择一个预制体（预制体里已经手动设置好了RequiredHorizontal）
        int index = Random.Range(0, notePrefabs.Length);

        Vector3 randomPos = transform.position + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.3f, 0.3f), 0);
        Instantiate(notePrefabs[index], randomPos, Quaternion.identity);
        // 在生成器位置的基础上，随机左右上下偏移
    }
}