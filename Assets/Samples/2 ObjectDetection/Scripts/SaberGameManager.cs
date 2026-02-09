using UnityEngine;
using TMPro;

public class SaberGameManager : MonoBehaviour
{
    public static SaberGameManager Instance;
    public PathRecorder pathRecorder;
    [Header("UI 引用")]
    // 现在只需要一个引用
    public TextMeshPro statusText;

    [HideInInspector] public int correctCount = 0;
    [HideInInspector] public int wrongDirCount = 0;
    [HideInInspector] public int missCount = 0;
    private int score = 0;
    private float timeLeft = 35f;
    private bool isGameActive = true;

    public CubeSpawner spawner;

    void Awake() { Instance = this; }
    private void Start()
    {
        StartGame();
    }
    public void StartGame()
    {
        score = 0; correctCount = 0; wrongDirCount = 0; missCount = 0;
        timeLeft = 35f;
        isGameActive = true;
        if (spawner != null)
        {
            spawner.enabled = true;
            spawner.StartSpawning(); // 核心：在这里手动开启生成器！
        }
        UpdateUI();
        pathRecorder.StartRecording();
    }

    void Update()
    {
        if (isGameActive)
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0)
            {
                EndGame();
            }
            else
            {
                UpdateUI();
            }
            
        }
    }

    // 根据判定结果加分并记录
    public void RegisterHit(bool isCorrectDir)
    {
        if (!isGameActive) return;

        if (isCorrectDir)
        {
            score += 10;
            correctCount++;
        }
        else
        {
            wrongDirCount++;
        }
        UpdateUI();
    }

    // --- 核心修改部分：合并显示逻辑 ---
    void UpdateUI()
    {
        // 使用 \n 实现换行显示
        statusText.text = $"Time: {Mathf.CeilToInt(timeLeft)}s\nScore: {score}\n" +
                          $"Correct:{correctCount} Wrong:{wrongDirCount} Miss:{missCount}";
    }

    void EndGame()
    {
        isGameActive = false;
        timeLeft = 0;

        if (spawner != null)
        {
            spawner.StopSpawning(); // 调用新写的停止函数
            spawner.enabled = false;
        }
        pathRecorder.StopRecording();
        // 游戏结束时的合并显示
        statusText.text = $"GAME OVER\nFinal Score: {score}\nPerfect: {correctCount}\nWrong: {wrongDirCount}\nMiss: {missCount}";
    }
}
